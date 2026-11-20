using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs.Evaluation;

/// <summary>
/// Evaluates a graph with no faults, one output at a time, in square tiles.
/// <para><b>Tiles.</b> An output is made in canonical tiles of side <c>T = min(tileSize, size)</c>
/// (<see cref="DefaultTileSize"/>, 128, unless a caller names another power of two), tile <c>(tx, ty)</c> holding
/// columns <c>tx * T</c> to <c>tx * T + T - 1</c> and the same rows, always inside the image. Output tiles are made top
/// row first, left to right. For each, the evaluator works back through the graph which tiles of each node it needs
/// (each node names, by <see cref="NodeType.InputRect"/>, the rectangle of each input it reads for a tile; the tiles
/// covering that rectangle, wrapping around the image, are needed), pins them, makes those not already held, inputs
/// first, hands the output tile to the caller, then unpins them.</para>
/// <para><b>Memory bound.</b> Tiles are kept between output tiles in a cache of at most <see cref="DefaultCacheBytes"/>
/// (64 MiB) unless a caller names another cap, dropped least recently used first, never while a step needs them; so
/// held bytes stay at most the cap or one step's pinned tiles, whichever is more (one tile a node in a pointwise
/// chain; a few for a node reading around or away from its texels), plus the caller's own result
/// (<see cref="OutputImage"/> keeps the RGBA8 image, 256 MB at 8192, which the PNG encoder needs whole). The cache is
/// dropped at the end of the call. <see cref="LastRun"/> reports what one output held.</para>
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
    public IReadOnlyList<OutputRole> Roles => [.. OutputRoles.All.Where(_analysis.Outputs.ContainsKey)];

    /// <summary>What the last <see cref="Evaluate"/> held: its tile size, the most tiles and bytes held at once, the cap, and how many node tiles it made.</summary>
    public EvaluationRun? LastRun { get; private set; }

    private sealed record Source(string Port, int Node, int OutPort);

    private sealed record Step(string Id, NodeType Type, NodeParameters Parameters, Source[] Sources, string[] Ports, PortKind[] Kinds);

    /// <summary>
    /// Evaluates the output with <paramref name="role"/> at <paramref name="size"/>, tile by tile, calling
    /// <paramref name="onTile"/> with the tile reaching the output node for each canonical tile, top row first, left to
    /// right. The tile is the evaluator's: read it during the call and do not keep it. <paramref name="tileSize"/> (a
    /// power of two) and <paramref name="cacheBytes"/> override <see cref="DefaultTileSize"/> and
    /// <see cref="DefaultCacheBytes"/>; neither changes a byte of the output.
    /// </summary>
    public void Evaluate(OutputRole role, int size, Action<Tile> onTile, int? tileSize = null, long? cacheBytes = null)
    {
        if (!TextureGraph.IsValidSize(size))
            throw new ArgumentOutOfRangeException(nameof(size), size, "size is " + TextureGraph.SizesText);
        if (!_analysis.Outputs.TryGetValue(role, out var outputId))
            throw new ArgumentException($"the graph has no {OutputRoles.Name(role)} output; it has {string.Join(", ", Roles.Select(OutputRoles.Name))}", nameof(role));
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
                [.. node.Sources.Select(p => new Source(p.Key, index[p.Value.Node], PortIndex(p.Value.Node, p.Value.Port)))],
                ports, [.. ports.Select(p => node.OutputKinds[p])]);
        }).ToArray();
        var (inNode, inPort) = _analysis.Nodes[outputId].Sources["in"];
        int outNode = index[inNode], outPort = PortIndex(inNode, inPort);

        var cache = new TileCache(n, cap);
        var needs = steps.Select(_ => new List<(int Tx, int Ty)>()).ToArray();
        var seen = steps.Select(_ => new HashSet<int>()).ToArray();
        var pinned = new List<TileCache.Entry>();
        var evaluated = 0;
        TexelRect TileRect(int tx, int ty) => new(tx * t, ty * t, t, t);
        void Add(int node, int tx, int ty)
        {
            if (seen[node].Add(ty * n + tx)) needs[node].Add((tx, ty));
        }

        for (var ty = 0; ty < n; ty++)
            for (var tx = 0; tx < n; tx++)
            {
                // Which tiles of each node this output tile needs, worked back from the output.
                for (var i = 0; i < steps.Length; i++) { needs[i].Clear(); seen[i].Clear(); }
                Add(outNode, tx, ty);
                for (var i = steps.Length - 1; i >= 0; i--)
                {
                    var step = steps[i];
                    foreach (var (x, y) in needs[i])
                        foreach (var source in step.Sources)
                        {
                            var cover = TileRead.Cover(step.Type.InputRect(source.Port, TileRect(x, y), step.Parameters, size), size, t);
                            for (var r = cover.Y; r < cover.Bottom; r++)
                                for (var c = cover.X; c < cover.Right; c++)
                                    Add(source.Node, Tile.Wrap(c, n), Tile.Wrap(r, n));
                        }
                }

                // Pin what is held already, so nothing this step needs is dropped while it runs.
                pinned.Clear();
                for (var i = 0; i < steps.Length; i++)
                    foreach (var (x, y) in needs[i])
                        if (cache.Find(cache.Key(i, x, y)) is { } held) { cache.Pin(held); pinned.Add(held); }

                // Make the rest, inputs first.
                for (var i = 0; i < steps.Length; i++)
                {
                    var step = steps[i];
                    foreach (var (x, y) in needs[i])
                    {
                        var key = cache.Key(i, x, y);
                        if (cache.Find(key) is not null) continue;
                        var rect = TileRect(x, y);
                        var inputs = new Dictionary<string, TileRead>(StringComparer.Ordinal);
                        foreach (var source in step.Sources)
                        {
                            var want = TileRead.Clamp(step.Type.InputRect(source.Port, rect, step.Parameters, size), size);
                            var cover = TileRead.Cover(want, size, t);
                            var tiles = new Tile[cover.Width * cover.Height];
                            for (var r = 0; r < cover.Height; r++)
                                for (var c = 0; c < cover.Width; c++)
                                    tiles[r * cover.Width + c] = cache.Find(cache.Key(source.Node, Tile.Wrap(cover.X + c, n), Tile.Wrap(cover.Y + r, n)))!.Tiles[source.OutPort];
                            inputs[source.Port] = new TileRead(steps[source.Node].Kinds[source.OutPort], size, t, want, tiles);
                        }
                        var outputs = new Tile[step.Ports.Length];
                        var named = new Dictionary<string, Tile>(StringComparer.Ordinal);
                        for (var p = 0; p < outputs.Length; p++) named[step.Ports[p]] = outputs[p] = new Tile(step.Kinds[p], rect);
                        step.Type.Evaluate(new NodeEvaluation(step.Id, size, Graph.Seed, step.Parameters, rect, inputs, named));
                        pinned.Add(cache.AddPinned(key, outputs));
                        evaluated++;
                    }
                }

                onTile(cache.Find(cache.Key(outNode, tx, ty))!.Tiles[outPort]);
                foreach (var entry in pinned) cache.Unpin(entry);
                cache.Trim();
            }
        LastRun = new EvaluationRun(t, cache.MostTiles, cache.PeakBytes, cap, evaluated);
    }
}

/// <summary>
/// What one output's evaluation held: the tile size; the most tiles (one port of one node at one position each) and
/// the most bytes held at once; the cache's cap; and how many node tiles were made (a node's ports at one position
/// count once; a tile dropped and asked for again counts again).
/// </summary>
public sealed record EvaluationRun(int TileSize, int MostTiles, long PeakBytes, long CapBytes, int TilesEvaluated);

/// <summary>What a node's <see cref="NodeType.Evaluate"/> is given: where it is, its parameters, its input reads and the output tiles it fills.</summary>
public sealed class NodeEvaluation(string nodeId, int size, uint seed, NodeParameters parameters, TexelRect rect,
    IReadOnlyDictionary<string, TileRead> inputs, IReadOnlyDictionary<string, Tile> outputs)
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
}
