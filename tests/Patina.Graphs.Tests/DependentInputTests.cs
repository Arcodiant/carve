using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// Inputs whose rectangles depend on another input's values (T-153): <see cref="NodeType.DependsOn"/> and
/// <see cref="NodeType.InputRects"/>, worked on <c>pair</c> (one rectangle or two) and on <c>warp</c>.
/// </summary>
[Trait("Tier", "unit")]
public class DependentInputTests
{
    private static TextureGraph Graph(string nodes, int size)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": 7, "nodes": { {{nodes}} } }""", TestNodes.Dependent);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static string Pair(string type) => $$"""
        "h": { "type": "hash", "params": { "offset": 1 } },
        "g": { "type": "hash", "params": { "offset": 2 } },
        "p": { "type": "{{type}}", "inputs": { "in": "h", "gate": "g" } },
        "o": { "type": "output", "inputs": { "in": "p" }, "params": { "role": "occlusion" } }
        """;

    // Value noise (or, to count tiles cheaply, the hash) warped by a two-octave fbm along 30 degrees (by grey, so the
    // angle is used): T-135's measured warp made cheaper, since a test is about a second.
    private static string Warp(string type, double amount, string src = "\"noise\", \"params\": { \"cells\": [8, 8] }") => $$"""
        "src": { "type": {{src}} },
        "field": { "type": "fbm", "params": { "octaves": 2, "seed": 3 } },
        "w": { "type": "{{type}}", "inputs": { "in": "src", "by": "field" }, "params": { "amount": {{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "angle": 30 } },
        "o": { "type": "output", "inputs": { "in": "w" }, "params": { "role": "roughness" } }
        """;

    private static (byte[] Rgba, EvaluationRun Run) Run(TextureGraph g, OutputRole role, int size, int? tileSize = null, long? cap = null)
    {
        var evaluator = new GraphEvaluator(g, TestNodes.Dependent);
        var rgba = OutputImage.Rgba8(evaluator, role, size, tileSize, cap);
        return (rgba, evaluator.LastRun!);
    }

    [Fact]
    public void ANodeAskingForTwoRectanglesReadsBothAtEveryTileSizeAndCap()
    {
        const int n = 256;
        var g = Graph(Pair("pair"), n);
        foreach (var side in new[] { 16, 64, 256 })
            foreach (var cap in new long[] { 0, GraphEvaluator.DefaultCacheBytes })
            {
                var (rgba, _) = Run(g, OutputRole.Occlusion, n, side, cap);
                for (var y = 0; y < n; y++)
                    for (var x = 0; x < n; x++)
                    {
                        var gate = TestNodes.HashValue(x, y, 7, 2);
                        var v = gate > 0.5f ? TestNodes.HashValue(x, y, 7, 1) : TestNodes.HashValue(Tile.Wrap(x + n / 2, n), y, 7, 1);
                        var want = OutputPacking.Byte(255f * v);
                        if (rgba[(y * n + x) * 4] != want) Assert.Fail($"in tiles of {side} under a cap of {cap}, texel ({x}, {y}) is {rgba[(y * n + x) * 4]}, not {want}");
                    }
            }
    }

    [Fact]
    public void ANodeUnderDeclaringItsDependentRectangleThrows()
    {
        // pair_short reads half the image to the right where the gate is low but asks for its own tile alone.
        var g = Graph(Pair("pair_short"), 64);
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => Run(g, OutputRole.Occlusion, 64, tileSize: 32));
        Assert.Contains("is not in the rectangle asked for (columns 0 to 31, rows 0 to 31", e.Message);
    }

    [Fact]
    public void ADependentInputNamingNoOtherInputIsRefused()
    {
        var bad = NodeRegistry.BuiltIn.With(new SelfDependentNode());
        var read = TexGraphFile.Read("""
            { "version": 1, "size": 32, "seed": 7, "nodes": {
              "c": { "type": "colour" },
              "s": { "type": "self_dependent", "inputs": { "in": "c" } },
              "o": { "type": "output", "inputs": { "in": "s" }, "params": { "role": "base_colour" } } } }
            """, bad);
        var e = Assert.Throws<InvalidOperationException>(() => OutputImage.Rgba8(new GraphEvaluator(read.Graph!, bad), OutputRole.BaseColour, 32));
        Assert.Contains("input in depends on in, which is not another of its inputs", e.Message);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.25)]
    public void WarpGivesTheStaticWarpsBytesAtEveryTileSizeAndCap(double amount)
    {
        const int n = 256;
        var (want, _) = Run(Graph(Warp("warp_static", amount), n), OutputRole.Roughness, n);
        var g = Graph(Warp("warp", amount), n);
        foreach (var side in new[] { 16, 64, 256 })
            foreach (var cap in new long[] { 0, GraphEvaluator.DefaultCacheBytes })
                Assert.True(want.AsSpan().SequenceEqual(Run(g, OutputRole.Roughness, n, side, cap).Rgba),
                    $"at amount {amount} in tiles of {side} under a cap of {cap} it differs from the static warp");
    }

    private const string Hash = "\"hash\"";

    [Fact]
    public void WarpAtTheLargestAmountMakesFewMoreTilesThanTheMinimum()
    {
        // 1024 in tiles of 128 is 64 positions; three nodes make 192 tiles at the least. Under a 1 MiB cap (16 grey
        // tiles) the static warp's 6 x 6 source tiles a position cannot stay held, so it remade them for every output
        // tile: 2,481 tiles, 12.9 times the minimum (measured, T-153). The dependent warp asks for the 1 to 4 tiles its
        // offsets reach: 314, 1.64 times (measured). The bound held is twice.
        const int n = 1024;
        const long cap = 1L << 20;
        const int minimum = 3 * 64;
        var (_, run) = Run(Graph(Warp("warp", 0.25, Hash), n), OutputRole.Roughness, n, 128, cap);
        Assert.True(run.PeakBytes <= cap, $"held {run.PeakBytes} bytes, past the cap of {cap}");
        Assert.True(run.TilesEvaluated <= 2 * minimum, $"made {run.TilesEvaluated} tiles, more than twice the minimum of {minimum}");
    }

    /// <summary>A node whose input's rectangle names itself as what it depends on: refused when evaluated.</summary>
    private sealed class SelfDependentNode : NodeType
    {
        public override string Name => "self_dependent";
        public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Colour)];
        public override IReadOnlyList<ParamSpec> Parameters { get; } = [];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Colour;
        public override IReadOnlyList<string> DependsOn(string port, NodeParameters parameters) => [port];
        public override void Evaluate(NodeEvaluation e) => throw new InvalidOperationException("never reached");
    }
}
