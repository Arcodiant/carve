using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs.Evaluation;

/// <summary>
/// Evaluates a graph with no faults, one output at a time, in row bands.
/// <para><b>Bands.</b> An output is made in bands of <see cref="BandRowsFor"/> rows: <c>max(1, 2^18 / size)</c>,
/// capped at the size, so a band is 2^18 texels (1 MB greyscale, 3 MB colour). Sizes up to 512 are therefore evaluated
/// whole in one band (the threshold); 1024 in bands of 256 rows, 2048 of 128, 8192 of 32. For each band the evaluator
/// works each node's row range from its consumers (the hull of every consumer's range widened by the consumer's
/// <see cref="NodeType.Radius"/>, wrapping around the image), evaluates the nodes inputs-first into one band each,
/// hands the output band to the caller and drops them all before the next band.</para>
/// <para><b>Memory bound.</b> At any moment each node the output depends on holds exactly one band, of
/// <c>bandRows + 2 * R</c> rows of the full width, where R is the sum of the radii on its longest way to the output
/// (no node's full image is held above 512, unless R is near the size). At 8192 with no radius that is 1 MB per
/// greyscale node and 3 MB per colour node, plus the caller's own result (<see cref="OutputImage"/> keeps the RGBA8
/// image, 256 MB at 8192, which the PNG encoder needs whole). <see cref="LastRun"/> reports what one output held.</para>
/// <para><b>Determinism.</b> Evaluation is single-threaded, in a fixed order, in <c>float</c> with integer hashing, so
/// the same graph, size and seed give the same bytes whatever the machine, the band height or the thread count.</para>
/// </summary>
public sealed class GraphEvaluator
{
    /// <summary>The texels in one band: 2^18.</summary>
    public const int BandTexels = 1 << 18;

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

    /// <summary>What the last <see cref="Evaluate"/> held: its band height, the most rows any node's band had, and the most bytes all bands held at once.</summary>
    public EvaluationRun? LastRun { get; private set; }

    /// <summary>The band height at a size: <c>max(1, 2^18 / size)</c>, no more than the size.</summary>
    public static int BandRowsFor(int size) => Math.Clamp(BandTexels / size, 1, size);

    /// <summary>
    /// Evaluates the output with <paramref name="role"/> at <paramref name="size"/>, band by band from the top, calling
    /// <paramref name="onBand"/> with the band reaching the output node and the logical rows <c>y0</c> to
    /// <c>y0 + rows - 1</c> of it that are this band's. The band is dropped after the call. <paramref name="bandRows"/>
    /// overrides the band height (tests).
    /// </summary>
    public void Evaluate(OutputRole role, int size, Action<Band, int, int> onBand, int? bandRows = null)
    {
        if (!TextureGraph.IsValidSize(size))
            throw new ArgumentOutOfRangeException(nameof(size), size, "size is " + TextureGraph.SizesText);
        if (!_analysis.Outputs.TryGetValue(role, out var outputId))
            throw new ArgumentException($"the graph has no {OutputRoles.Name(role)} output; it has {string.Join(", ", Roles.Select(OutputRoles.Name))}", nameof(role));
        var rows = bandRows ?? BandRowsFor(size);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1, nameof(bandRows));
        rows = Math.Min(rows, size);

        // The nodes the output depends on, inputs first.
        var needed = new HashSet<string>(StringComparer.Ordinal);
        void Need(string id)
        {
            if (!needed.Add(id)) return;
            foreach (var (source, _) in _analysis.Nodes[id].Sources.Values) Need(source);
        }
        Need(outputId);
        var order = _analysis.Order.Where(needed.Contains).ToList();
        var output = _analysis.Nodes[outputId];
        var (inNode, inPort) = output.Sources["in"];
        var radius = order.ToDictionary(id => id, id => _analysis.Nodes[id].Type.Radius(_analysis.Nodes[id].Parameters, size));

        var run = new EvaluationRun(rows, 0, 0);
        for (var y0 = 0; y0 < size; y0 += rows)
        {
            var count = Math.Min(rows, size - y0);
            var ranges = new Dictionary<string, (int Lo, int Hi)>(StringComparer.Ordinal) { [outputId] = (y0, y0 + count) };
            for (var i = order.Count - 1; i >= 0; i--)
            {
                var id = order[i];
                if (!ranges.TryGetValue(id, out var range)) continue;
                var r = radius[id];
                foreach (var (source, _) in _analysis.Nodes[id].Sources.Values)
                {
                    var wanted = (Lo: range.Lo - r, Hi: range.Hi + r);
                    ranges[source] = ranges.TryGetValue(source, out var had) ? (Math.Min(had.Lo, wanted.Lo), Math.Max(had.Hi, wanted.Hi)) : wanted;
                }
            }

            var bands = new Dictionary<(string Node, string Port), Band>();
            long bytes = 0;
            var mostRows = run.MostRows;
            foreach (var id in order)
            {
                var node = _analysis.Nodes[id];
                if (node.Type is OutputNode) continue;
                var (lo, hi) = ranges[id];
                var outputs = new Dictionary<string, Band>(StringComparer.Ordinal);
                foreach (var port in node.Type.Outputs)
                {
                    var band = new Band(node.OutputKinds[port], size, lo, hi - lo);
                    outputs[port] = band;
                    bands[(id, port)] = band;
                    bytes += band.Bytes;
                    mostRows = Math.Max(mostRows, hi - lo);
                }
                var inputs = node.Sources.ToDictionary(p => p.Key, p => bands[p.Value], StringComparer.Ordinal);
                node.Type.Evaluate(new NodeEvaluation(id, size, Graph.Seed, node.Parameters, lo, hi - lo, inputs, outputs));
            }
            run = new EvaluationRun(rows, mostRows, Math.Max(run.PeakBytes, bytes));
            onBand(bands[(inNode, inPort)], y0, count);
        }
        LastRun = run;
    }
}

/// <summary>What one output's evaluation held: the band height, the most rows any node's band had, and the most bytes of bands held at once.</summary>
public sealed record EvaluationRun(int BandRows, int MostRows, long PeakBytes);

/// <summary>What a node's <see cref="NodeType.Evaluate"/> is given: where it is, its parameters, its input bands and the output bands it fills.</summary>
public sealed class NodeEvaluation(string nodeId, int size, uint seed, NodeParameters parameters, int y0, int rows,
    IReadOnlyDictionary<string, Band> inputs, IReadOnlyDictionary<string, Band> outputs)
{
    public string NodeId { get; } = nodeId;

    /// <summary>The image's size in texels, across and down.</summary>
    public int Size { get; } = size;

    /// <summary>The graph's seed.</summary>
    public uint Seed { get; } = seed;

    public NodeParameters Parameters { get; } = parameters;

    /// <summary>The first logical row to fill (it may be negative or past the size: the image's row is <c>Band.Wrap(y, Size)</c>).</summary>
    public int Y0 { get; } = y0;

    public int Rows { get; } = rows;

    /// <summary>The band reaching an input port, holding rows <see cref="Y0"/> - radius to <see cref="Y0"/> + <see cref="Rows"/> + radius - 1, or null for an optional input left unconnected.</summary>
    public Band? Input(string port) => inputs.TryGetValue(port, out var b) ? b : null;

    /// <summary>The band to fill for an output port.</summary>
    public Band Output(string port = "out") => outputs[port];
}
