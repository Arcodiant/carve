using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs.Evaluation;

/// <summary>
/// Evaluates a graph with no faults, one output at a time, in square tiles.
/// <para><b>Tiles.</b> An output is made in canonical tiles of side <c>T = min(tileSize, size)</c>
/// (<see cref="DefaultTileSize"/>, 128, unless a caller names another power of two), tile <c>(tx, ty)</c> holding
/// columns <c>tx * T</c> to <c>tx * T + T - 1</c> and the same rows, always inside the image. Output tiles are made in
/// bands of at most <see cref="MostBandTiles"/> tile columns (T-155), each band walked down a row at a time, left to
/// right in the row: up to 32 tiles across, unless a separable node narrows the band, that is the image's raster order. A tile is made depth first: each tile of
/// each input it reads (each node names, by <see cref="NodeType.InputRect"/>, the rectangle of each input it reads for
/// a tile; the tiles covering that rectangle, wrapping around the image, are read) is found or made and pinned, the
/// tile is filled, and its inputs are unpinned. So what is pinned at once is one path of reads through the graph.</para>
/// <para><b>Dependent inputs</b> (T-153). An input whose rectangles depend on another input's values
/// (<see cref="NodeType.DependsOn"/>) is read after the others: the node's <see cref="NodeType.InputRects"/> names from
/// reads of them the rectangles it needs, and those tiles are found or made the same way. A read outside every
/// rectangle asked for throws.</para>
/// <para><b>Separable passes</b> (T-155). A node whose <see cref="NodeType.Separable"/> is not null is made in two
/// passes: its tile is <see cref="NodeType.EvaluateColumns"/> over its row pass's tiles widened in y, and each row
/// pass tile is <see cref="NodeType.EvaluateRows"/> over its inputs widened in x, cached under a key of its own like
/// any node's tile. The band is narrowed so that a band's row tiles, as tall as the column pass reaches, fit in three
/// quarters of the cap.</para>
/// <para><b>Memory bound.</b> Tiles are kept between output tiles in a cache of at most <see cref="DefaultCacheBytes"/>
/// (64 MiB) unless a caller names another cap, dropped least recently used first, never while pinned; so held bytes
/// of tiles stay at most the cap or the most ever pinned at once, whichever is more (one tile a node in a pointwise
/// chain; a few for a node reading around or away from its texels; a strong minify's whole input, of the ports read),
/// plus arrays kept for reuse (<see cref="LastHeldBytes"/>, RV-76), plus the caller's own
/// result (<see cref="OutputImage"/> keeps the RGBA8 image, 256 MB at 8192, which the PNG encoder needs whole). The
/// cache is dropped at the end of the call. <see cref="LastRun"/> reports what one output held.</para>
/// <para><b>Determinism.</b> A tile's texels are worked from their image coordinates, from inputs holding exactly the
/// values a whole-image evaluation would, so an output's bytes depend on neither the tile size nor the cap: a dropped
/// tile asked for again comes out the same. Evaluation is single-threaded, in a fixed order, in <c>float</c> with
/// integer hashing, so the same graph, size and seed give the same bytes whatever the machine.</para>
/// </summary>
public sealed class GraphEvaluator
{
    /// <summary>The side of a tile unless a caller names another: 128 texels (64 KB greyscale, 192 KB colour).</summary>
    public const int DefaultTileSize = 128;

    /// <summary>The cache's cap unless a caller names another: 64 MiB.</summary>
    public const long DefaultCacheBytes = 64L << 20;

    private readonly GraphAnalysis _analysis;

    /// <summary>Throws <see cref="GraphFaultException"/> when the graph has a fault.</summary>
    public GraphEvaluator(TextureGraph graph, NodeRegistry? registry = null)
    {
        _analysis = GraphValidator.Analyse(graph, registry ?? NodeRegistry.BuiltIn);
        if (_analysis.Faults.Count > 0) throw new GraphFaultException(_analysis.Faults);
        Graph = graph;
    }

    public TextureGraph Graph { get; }

    /// <summary>The roles the graph has an output for, in role order.</summary>
    public IReadOnlyList<OutputRole> Roles => [.. OutputRoles.All.Where(r => _analysis.Outputs.ContainsKey(OutputRoles.Name(r)))];

    /// <summary>Every output the graph has, by its role's name or its own, in sheet order: the roles in role order, then the named outputs a to z.</summary>
    public IReadOnlyList<string> Outputs => OutputRoles.Order(_analysis.Outputs.Keys);

    /// <summary>The graph's named outputs (those with a <c>name</c>, not a role), a to z.</summary>
    public IReadOnlyList<string> Named => [.. Outputs.Where(o => !OutputRoles.TryParse(o, out _))];

    /// <summary>True when the graph has an output of this role's name or this name.</summary>
    public bool Has(string output) => _analysis.Outputs.ContainsKey(output);

    /// <summary>What reaches the output's <c>in</c>: greyscale or colour.</summary>
    public PortKind KindOf(string output)
    {
        var (node, port) = _analysis.Nodes[_analysis.Outputs[output]].Sources["in"];
        return _analysis.Nodes[node].OutputKinds[port];
    }

    /// <summary>The type names of every node the output reads, all the way up, nested graphs' nodes as expanded (RV-82).</summary>
    public IReadOnlySet<string> TypesReaching(string output)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var types = new HashSet<string>(StringComparer.Ordinal);
        var todo = new Stack<string>([_analysis.Outputs[output]]);
        while (todo.TryPop(out var id))
        {
            if (!seen.Add(id) || !_analysis.Nodes.TryGetValue(id, out var node)) continue;
            types.Add(node.Type.Name);
            foreach (var (from, _) in node.Sources.Values) todo.Push(from);
        }
        return types;
    }

    /// <summary>What the last <see cref="Evaluate"/> held: its tile size, the most tiles and bytes held at once, the cap, and how many node tiles it made.</summary>
    public EvaluationRun? LastRun { get; private set; }

    /// <summary>
    /// The most bytes the last <see cref="Evaluate"/> kept reachable at once (RV-76): the tiles of
    /// <see cref="EvaluationRun.PeakBytes"/>, with the dropped arrays waiting to be taken again (at most the larger of
    /// 4 MiB and a sixteenth of the bound) and the scratch of the node being made and of the last tile
    /// (<see cref="NodeEvaluation.Scratch"/>).
    /// </summary>
    public long LastHeldBytes { get; private set; }

    // Dependent: the port's rectangles come from InputRects once the inputs it depends on are made (T-153).
    private sealed record Source(string Port, int Node, int OutPort, string[] DependsOn)
    {
        public bool Dependent => DependsOn.Length > 0;
    }

    private sealed record Step(string Id, NodeType Type, NodeParameters Parameters, Source[] Sources, string[] Ports, PortKind[] Kinds);

    /// <summary>
    /// Evaluates the output with <paramref name="role"/> at <paramref name="size"/>, tile by tile, calling
    /// <paramref name="onTile"/> with the tile reaching the output node for each canonical tile, in the bands the
    /// class describes (the image's raster order up to 32 tiles across, unless a separable node narrows the band). The tile is the evaluator's: read it during the call and do not keep it. <paramref name="tileSize"/> (a
    /// power of two) and <paramref name="cacheBytes"/> override <see cref="DefaultTileSize"/> and
    /// <see cref="DefaultCacheBytes"/>; neither changes a byte of the output.
    /// </summary>
    public void Evaluate(OutputRole role, int size, Action<Tile> onTile, int? tileSize = null, long? cacheBytes = null) =>
        Evaluate(OutputRoles.Name(role), size, onTile, tileSize, cacheBytes);

    /// <summary>As <see cref="Evaluate(OutputRole, int, Action{Tile}, int?, long?)"/>, for the output of a role's name or a named output's name.</summary>
    public void Evaluate(string output, int size, Action<Tile> onTile, int? tileSize = null, long? cacheBytes = null)
    {
        if (!TextureGraph.IsValidSize(size))
            throw new ArgumentOutOfRangeException(nameof(size), size, "size is " + TextureGraph.SizesText);
        if (!_analysis.Outputs.TryGetValue(output, out var outputId))
            throw new ArgumentException($"the graph has no {output} output; it has {string.Join(", ", Outputs)}", nameof(output));
        var side = tileSize ?? DefaultTileSize;
        if (side < 1 || (side & (side - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(tileSize), side, "a tile's side is a power of two");
        var cap = cacheBytes ?? DefaultCacheBytes;
        ArgumentOutOfRangeException.ThrowIfNegative(cap, nameof(cacheBytes));
        var t = Math.Min(side, size);
        var n = size / t;

        // The nodes the output depends on, inputs first, numbered to key the cache; the output node itself is not one.
        var needed = new HashSet<string>(StringComparer.Ordinal);
        void Need(string id)
        {
            if (!needed.Add(id)) return;
            foreach (var (source, _) in _analysis.Nodes[id].Sources.Values) Need(source);
        }
        Need(outputId);
        var ids = _analysis.Order.Where(id => needed.Contains(id) && id != outputId).ToList();
        var index = ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i, StringComparer.Ordinal);
        int PortIndex(string id, string port) => _analysis.Nodes[id].Type.Outputs.ToList().IndexOf(port);
        var steps = ids.Select(id =>
        {
            var node = _analysis.Nodes[id];
            var ports = node.Type.Outputs.ToArray();
            return new Step(id, node.Type, node.Parameters,
                [.. node.Sources.Select(p =>
                    new Source(p.Key, index[p.Value.Node], PortIndex(p.Value.Node, p.Value.Port), DependsOn(node.Type, p.Key, node.Parameters)))],
                ports, [.. ports.Select(p => node.OutputKinds[p])]);
        }).ToArray();
        var (inNode, inPort) = _analysis.Nodes[outputId].Sources["in"];
        int outNode = index[inNode], outPort = PortIndex(inNode, inPort);
        // The ports some step (or the output) reads (RV-76): only these are kept in the cache, so a read of one port of
        // a node of several, such as tiles.mask, pins that port and not the node's others.
        var read = steps.Select(s => new bool[s.Ports.Length]).ToArray();
        foreach (var s in steps)
            foreach (var source in s.Sources) read[source.Node][source.OutPort] = true;
        read[outNode][outPort] = true;
        bool[] rowPorts = [true];

        var cache = new TileCache(n, cap);
        var evaluated = 0;
        var cold = false; // a row pass is being made, so what is unpinned is cold (TileCache)
        int bandStart = 0, bandEnd = 0; // the band of output tile columns being walked
        // Separable steps (T-155): their row pass's tiles are cached under node number steps.Length + i.
        var passes = steps.Select(s => s.Type.Separable(s.Parameters, size)).ToArray();
        for (var i = 0; i < steps.Length; i++)
            if (passes[i] is not null && (steps[i].Ports.Length != 1 || steps[i].Sources.Any(s => s.Dependent)))
                throw new InvalidOperationException($"node type {steps[i].Type.Name} runs as separable passes, so it has one output and no dependent input");
        TexelRect TileRect(int tx, int ty) => new(tx * t, ty * t, t, t);

        // The read of node `node`'s port `port` over `wanted`, each covering tile made if it is not held and pinned
        // into `pins` for the caller to unpin once it has made its own tile.
        TileRead Gather(int node, int port, PortKind kind, IReadOnlyList<TexelRect> wanted, List<TileCache.Entry> pins)
        {
            var parts = new (TexelRect Asked, Tile[] Tiles)[wanted.Count];
            for (var w = 0; w < wanted.Count; w++)
            {
                var want = TileRead.Clamp(wanted[w], size);
                var cover = TileRead.Cover(want, size, t);
                var tiles = new Tile[cover.Width * cover.Height];
                for (var r = 0; r < cover.Height; r++)
                    for (var c = 0; c < cover.Width; c++)
                    {
                        var entry = Ensure(node, Tile.Wrap(cover.X + c, n), Tile.Wrap(cover.Y + r, n));
                        pins.Add(entry);
                        tiles[r * cover.Width + c] = entry.Tiles[port];
                    }
                parts[w] = (want, tiles);
            }
            return parts.Length == 1 ? new TileRead(kind, size, t, parts[0].Asked, parts[0].Tiles) : new TileRead(kind, size, t, parts);
        }

        TileRead GatherSource(Source source, IReadOnlyList<TexelRect> wanted, List<TileCache.Entry> pins) =>
            Gather(source.Node, source.OutPort, steps[source.Node].Kinds[source.OutPort], wanted, pins);

        // Node `node`'s tile (x, y), held or made, pinned once for the caller. A tile is made depth first: its inputs'
        // tiles are made and pinned, it is filled, and they are unpinned, so what is pinned at once is one path of
        // reads through the graph, not every tile a step needs.
        TileCache.Entry Ensure(int node, int x, int y)
        {
            if (cache.Find(cache.Key(node, x, y)) is { } held) { cache.Pin(held); return held; }
            return node < steps.Length ? MakeTile(node, x, y) : MakeRows(node - steps.Length, x, y);
        }

        // Fills and caches a tile, then unpins its inputs: cold while a row pass is being made (TileCache).
        // Every port is filled, but only the ports read (`keep`) are cached; the others' arrays, and the node's scratch
        // (NodeEvaluation.Scratch), are released once it is made (RV-76).
        TileCache.Entry Finish(long key, Step step, bool[] keep, TexelRect rect, Action<Dictionary<string, Tile>, Func<int, float[]>> fill, List<TileCache.Entry> pins)
        {
            var lengths = step.Kinds.Select(k => (long)rect.Width * rect.Height * PortKinds.Channels(k)).ToArray();
            var arrays = cache.Allocate(lengths);
            var outputs = new Tile[step.Ports.Length];
            var named = new Dictionary<string, Tile>(StringComparer.Ordinal);
            for (var p = 0; p < outputs.Length; p++) named[step.Ports[p]] = outputs[p] = new Tile(step.Kinds[p], rect, arrays[p]);
            var scratch = new List<float[]>();
            fill(named, length =>
            {
                var data = cache.TakeScratch(length);
                scratch.Add(data);
                return data;
            });
            cache.ReleaseScratch(scratch);
            var unread = new List<float[]>();
            for (var p = 0; p < outputs.Length; p++)
                if (!keep[p]) { unread.Add(arrays[p]); outputs[p] = null!; }
            cache.Release(unread);
            var entry = cache.AddPinned(key, outputs);
            foreach (var pin in pins) cache.Unpin(pin, cold);
            cache.Trim();
            evaluated++;
            return entry;
        }

        TileCache.Entry MakeTile(int i, int x, int y)
        {
            var step = steps[i];
            var rect = TileRect(x, y);
            var pins = new List<TileCache.Entry>();
            var inputs = new Dictionary<string, TileRead>(StringComparer.Ordinal);
            if (passes[i] is { } separable)
            {
                // A column tile's row tiles are read again by the column tiles below, so they are unpinned warm even
                // when this tile is made for an outer row pass.
                var was = cold;
                cold = false;
                try
                {
                    WarmRows(i, x, rect.Inflate(0, separable.ColumnReach));
                    inputs[NodeType.RowsPort] = Gather(steps.Length + i, 0, step.Kinds[0], [rect.Inflate(0, separable.ColumnReach)], pins);
                    return Finish(cache.Key(i, x, y), step, read[i], rect,
                        (named, scratch) => step.Type.EvaluateColumns(new NodeEvaluation(step.Id, size, Graph.Seed, step.Parameters, rect, inputs, named, scratch)), pins);
                }
                finally { cold = was; }
            }
            foreach (var source in step.Sources)
                if (!source.Dependent) inputs[source.Port] = GatherSource(source, [step.Type.InputRect(source.Port, rect, step.Parameters, size)], pins);
            foreach (var source in step.Sources)
            {
                if (!source.Dependent) continue;
                var request = new InputRequest(step.Id, size, Graph.Seed, step.Parameters, rect, source.DependsOn,
                    source.DependsOn.Where(inputs.ContainsKey).ToDictionary(p => p, p => inputs[p], StringComparer.Ordinal));
                var wanted = step.Type.InputRects(source.Port, request);
                if (wanted.Count == 0)
                    throw new InvalidOperationException($"node type {step.Type.Name} asked for no rectangle of its input {source.Port}");
                inputs[source.Port] = GatherSource(source, wanted, pins);
            }
            return Finish(cache.Key(i, x, y), step, read[i], rect,
                (named, scratch) => step.Type.Evaluate(new NodeEvaluation(step.Id, size, Graph.Seed, step.Parameters, rect, inputs, named, scratch)), pins);
        }

        // Before a column tile reads its row tiles, each row of them not held is made across the rest of the band
        // (from x to the band's right edge), left to right, and unpinned: a row tile's source row is then read once
        // for the band, not once for each output tile beside it. Without it a band's first row makes its row tiles a
        // column at a time, each column reading a tall square of source tiles, which flushes the row tiles beside it,
        // and LRU never recovers (T-155: blur at 8192 made 21 row tiles for every output tile). A column tile in a column
        // outside the band's makes only its own. The test is the column's number alone (RV-76), so a deeper step's
        // column tile (under a transform, or read across a row pass) whose column falls inside the band warms row tiles
        // to the band's right edge it may not read, which costs time but never a byte.
        void WarmRows(int i, int x, TexelRect column)
        {
            var cover = TileRead.Cover(TileRead.Clamp(column, size), size, t);
            var last = x >= bandStart && x < bandEnd ? bandEnd : x + 1;
            for (var r = 0; r < cover.Height; r++)
            {
                var y = Tile.Wrap(cover.Y + r, n);
                if (cache.Find(cache.Key(steps.Length + i, x, y)) is not null) continue;
                for (var c = x; c < last; c++)
                    if (cache.Find(cache.Key(steps.Length + i, c, y)) is null) cache.Unpin(MakeRows(i, c, y));
            }
        }

        // The row pass's tile (x, y) of separable step i, from its inputs widened in x.
        // Everything made and unpinned under it is cold: read along this tile row only.
        TileCache.Entry MakeRows(int i, int x, int y)
        {
            var step = steps[i];
            var rect = TileRect(x, y);
            var pins = new List<TileCache.Entry>();
            var inputs = new Dictionary<string, TileRead>(StringComparer.Ordinal);
            var was = cold;
            cold = true;
            try
            {
                foreach (var source in step.Sources)
                    inputs[source.Port] = GatherSource(source, [rect.Inflate(passes[i]!.RowReach, 0)], pins);
                return Finish(cache.Key(steps.Length + i, x, y), step, rowPorts, rect,
                    (named, scratch) => step.Type.EvaluateRows(new NodeEvaluation(step.Id, size, Graph.Seed, step.Parameters, rect, inputs, named, scratch)), pins);
            }
            finally { cold = was; }
        }

        var band = BandTiles(steps, passes, read, n, t, cap);
        foreach (var (tx, ty) in Order(n, band))
        {
            if (tx >= bandEnd || tx < bandStart) (bandStart, bandEnd) = (tx, Math.Min(n, tx + band));
            var entry = Ensure(outNode, tx, ty);
            onTile(entry.Tiles[outPort]);
            cache.Unpin(entry);
            cache.Trim();
        }
        LastRun = new EvaluationRun(t, cache.MostTiles, cache.PeakBytes, cap, evaluated);
        LastHeldBytes = cache.PeakHeldBytes;
    }

    /// <summary>The widest band of tile columns walked: 32, or narrower when a separable node's row tiles for one column must all stay held.</summary>
    // 32, not 16: a node reading past its tile (height_to_normal, one texel) remakes the chain under each band's edge
    // columns once the band beside has passed, so fewer edges make fewer tiles; at 8192 the cobblestones' normal made
    // 148,877 tiles in bands of 16, 141,044 in 32 and 133,783 in raster order, where its other outputs lost the bands'
    // saving (T-155).
    public const int MostBandTiles = 32;

    // Bands of `band` tile columns, each walked down a row at a time, left to right in the row (T-155): a tile's later
    // reads by the rows below come while it is still held, where a row of the whole image between them would have
    // pushed it out of the cache.
    private static IEnumerable<(int Tx, int Ty)> Order(int n, int band)
    {
        for (var b = 0; b < n; b += band)
            for (var ty = 0; ty < n; ty++)
                for (var tx = b; tx < Math.Min(n, b + band); tx++)
                    yield return (tx, ty);
    }

    // So many tile columns that every separable node's row tiles for the band (a column of them as tall as its column
    // pass reaches, times the band) fit in three quarters of the cap less what one row tile pins while it is made (the
    // entries of its inputs across the row's reach, each holding the ports the evaluation reads, RV-76). The
    // quarter left is room for the churn of the tiles a row tile reads; LRU fails all at once when a cycle of reads only
    // just passes what it holds.
    // The quotient is clamped in long before the cast (RV-76: a cap of long.MaxValue truncated it to -2, and Order
    // looped for ever).
    private static int BandTiles(Step[] steps, SeparablePasses?[] passes, bool[][] read, int n, int t, long cap)
    {
        long EntryBytes(int node) => steps[node].Kinds.Where((_, p) => read[node][p]).Sum(k => (long)t * t * PortKinds.Channels(k) * sizeof(float));
        int Spanned(int reach) => Math.Min(n, (t + 2 * reach + t - 1) / t + 1);
        var band = Math.Min(n, MostBandTiles);
        for (var i = 0; i < steps.Length; i++)
        {
            if (passes[i] is not { } p) continue;
            var column = Spanned(p.ColumnReach) * (long)t * t * PortKinds.Channels(steps[i].Kinds[0]) * sizeof(float);
            var reading = Spanned(p.RowReach) * steps[i].Sources.Sum(s => EntryBytes(s.Node));
            band = (int)Math.Min(band, Math.Max(1, (cap / 4 * 3 - reading) / column));
        }
        return band;
    }

    // The inputs a port's rectangles depend on, checked: other inputs of the type, each with a static rectangle.
    private static string[] DependsOn(NodeType type, string port, NodeParameters parameters)
    {
        var on = type.DependsOn(port, parameters).ToArray();
        foreach (var other in on)
        {
            if (other == port || type.Input(other) is null)
                throw new InvalidOperationException($"node type {type.Name}: input {port} depends on {other}, which is not another of its inputs");
            if (type.DependsOn(other, parameters).Count > 0)
                throw new InvalidOperationException($"node type {type.Name}: input {port} depends on {other}, whose own rectangles depend on others");
        }
        return on;
    }
}

/// <summary>
/// What one output's evaluation held: the tile size; the most tiles (one port of one node at one position each) and
/// the most bytes of tiles held at once (RV-76: with the dropped arrays waiting and nodes' scratch, the most is
/// <see cref="GraphEvaluator.LastHeldBytes"/>); the cache's cap; and how many node tiles were made (a node's ports at one position
/// count once; a separable node's row pass tile counts as one too; a tile dropped and asked for again counts again).
/// </summary>
public sealed record EvaluationRun(int TileSize, int MostTiles, long PeakBytes, long CapBytes, int TilesEvaluated);

/// <summary>What a node's <see cref="NodeType.Evaluate"/> is given: where it is, its parameters, its input reads and the output tiles it fills.</summary>
public sealed class NodeEvaluation(string nodeId, int size, uint seed, NodeParameters parameters, TexelRect rect,
    IReadOnlyDictionary<string, TileRead> inputs, IReadOnlyDictionary<string, Tile> outputs, Func<int, float[]>? scratch = null)
{
    public string NodeId { get; } = nodeId;

    /// <summary>The image's size in texels, across and down.</summary>
    public int Size { get; } = size;

    /// <summary>The graph's seed.</summary>
    public uint Seed { get; } = seed;

    public NodeParameters Parameters { get; } = parameters;

    /// <summary>The tile to fill: a rectangle inside the image, of any size up to the image's (never assume which).</summary>
    public TexelRect Rect { get; } = rect;

    /// <summary>The read of an input port, holding what <see cref="NodeType.InputRect"/> asked for, or null for an optional input left unconnected.</summary>
    public TileRead? Input(string port) => inputs.TryGetValue(port, out var r) ? r : null;

    /// <summary>The tile to fill for an output port; every texel of it must be written.</summary>
    public Tile Output(string port = "out") => outputs[port];

    /// <summary>
    /// A zeroed array of exactly <paramref name="length"/> floats to work in while the tile is filled (RV-76): the
    /// evaluator's, counted in <see cref="GraphEvaluator.LastHeldBytes"/> and taken again by later tiles, so a node working in large buffers
    /// (blur, distance) makes no garbage a tile. Use it only until the fill returns, and do not keep it.
    /// </summary>
    public float[] Scratch(int length) => scratch?.Invoke(length) ?? new float[length];
}

/// <summary>
/// What a node's <see cref="NodeType.InputRects"/> is given for a dependent input (T-153): where it is, its parameters,
/// the tile it is about to fill, and reads of the inputs that input depends on, each holding that input's
/// <see cref="NodeType.InputRect"/> for the tile.
/// </summary>
public sealed class InputRequest(string nodeId, int size, uint seed, NodeParameters parameters, TexelRect output,
    IReadOnlyList<string> dependsOn, IReadOnlyDictionary<string, TileRead> inputs)
{
    public string NodeId { get; } = nodeId;

    /// <summary>The image's size in texels, across and down.</summary>
    public int Size { get; } = size;

    /// <summary>The graph's seed.</summary>
    public uint Seed { get; } = seed;

    public NodeParameters Parameters { get; } = parameters;

    /// <summary>The tile the node is about to fill, inside the image.</summary>
    public TexelRect Output { get; } = output;

    /// <summary>
    /// The read of an input the port depends on, or null for an optional one left unconnected. Asking for an input the
    /// port does not name in <see cref="NodeType.DependsOn"/> throws: its tiles are not made yet.
    /// </summary>
    public TileRead? Input(string port) => dependsOn.Contains(port)
        ? inputs.GetValueOrDefault(port)
        : throw new ArgumentException($"input {port} is not one this port's rectangles depend on ({string.Join(", ", dependsOn)})", nameof(port));
}
