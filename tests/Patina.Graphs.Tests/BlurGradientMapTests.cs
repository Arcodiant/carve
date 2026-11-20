using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// T-137's two node types worked by hand. <c>blur</c>: a one-texel impulse (its sum kept, its width as the radius says), a
/// step across the wrap, radius 0, size-invariance at 256 and 1024, tile-size invariance to the bit, the input rectangle,
/// and a table of broken copies and the check that catches each. <c>gradient_map</c>: each stop, between two, beyond the
/// ends, the faults, and its own table of broken copies.
/// </summary>
[Trait("Tier", "unit")]
public class BlurGradientMapTests
{
    private static bool In(TexelRect r, int x, int y) => x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom;

    // ---- sources for the tests: a one-texel impulse, and a half image that steps across the wrap ----------------------

    private sealed class ImpulseNode : NodeType
    {
        public override string Name => "impulse";
        public override IReadOnlyList<InputPort> Inputs { get; } = [];
        public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.IntPair("at", 0, 4095, 0, 0)];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

        public override void Evaluate(NodeEvaluation e)
        {
            var at = e.Parameters.IntPair("at");
            for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
                for (var x = e.Rect.X; x < e.Rect.Right; x++) e.Output().SetGrey(x, y, x == at.X && y == at.Y ? 1f : 0f);
        }
    }

    /// <summary>1 for the left half of every row (columns 0 to size / 2 - 1): a step at the middle and one across the wrap.</summary>
    private sealed class HalfNode : NodeType
    {
        public override string Name => "half";
        public override IReadOnlyList<InputPort> Inputs { get; } = [];
        public override IReadOnlyList<ParamSpec> Parameters { get; } = [];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

        public override void Evaluate(NodeEvaluation e)
        {
            for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
                for (var x = e.Rect.X; x < e.Rect.Right; x++) e.Output().SetGrey(x, y, x < e.Size / 2 ? 1f : 0f);
        }
    }

    private static NodeRegistry With(params NodeType[] types) => NodeRegistry.BuiltIn.With(types);

    private static readonly NodeRegistry Sources = With(new ImpulseNode(), new HalfNode());

    /// <summary>The registry with its blur replaced.</summary>
    private static NodeRegistry WithBlur(NodeType blur) =>
        new(NodeRegistry.BuiltIn.Names.Where(n => n != "blur").Select(n => NodeRegistry.BuiltIn.Find(n)!).Concat([new ImpulseNode(), new HalfNode(), blur]));

    private static string Blur(string source, double radius, string quality = "box") =>
        $$"""{ "version": 1, "size": SIZE, "seed": 3, "nodes": { {{source}}, "t": { "type": "blur", "inputs": { "in": "s" }, "params": { "radius": {{radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "quality": "{{quality}}" } }, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "roughness" } } } }""";

    private const string Impulse30x40 = """ "s": { "type": "impulse", "params": { "at": [30, 40] } } """;
    private const string HalfImage = """ "s": { "type": "half" } """;

    /// <summary>Evaluates a graph (its SIZE replaced) and returns the greyscale output, row-major.</summary>
    private static float[] Run(string graph, int size, NodeRegistry? registry = null, int? tileSize = null)
    {
        var read = TexGraphFile.Read(graph.Replace("SIZE", size.ToString()), registry ?? Sources);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        var result = new float[size * size];
        new GraphEvaluator(read.Graph!, registry ?? Sources).Evaluate(OutputRole.Roughness, size, tile =>
        {
            for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                for (var x = tile.Rect.X; x < tile.Rect.Right; x++) result[y * size + x] = tile.Colour(x, y).R;
        }, tileSize);
        return result;
    }

    // ---- blur: worked by hand -------------------------------------------------------------------------------------

    // 128 texels, radius 0.0625 = 8 texels: a box pass is the 17 texels centred on each, so the impulse at (30, 40) becomes a 17 x 17 square
    // of 1 / 289 over columns 22..38 and rows 32..48, and the sum stays 1.
    [Fact]
    public void ABoxBlurOfAnImpulseIsASquareOfTheRadiusAndKeepsItsSum()
    {
        var b = Run(Blur(Impulse30x40, 0.0625), 128);
        double sum = 0; int inside = 0;
        for (var y = 0; y < 128; y++)
            for (var x = 0; x < 128; x++)
            {
                var v = b[y * 128 + x];
                sum += v;
                if (x is >= 22 and <= 38 && y is >= 32 and <= 48) { inside++; Assert.Equal(1f / 289f, v, 1e-7f); }
                else Assert.Equal(0f, v);
            }
        Assert.Equal(289, inside);
        Assert.Equal(1.0, sum, 1e-5);
    }

    // radius 2 texels, smooth: three passes of a 5-texel box. The 1-D kernel is [1,1,1,1,1] convolved three times over 25, 125 / 125: worked
    // here term by term, 13 wide (6r + 1), and the 2-D one is its outer product.
    [Fact]
    public void ASmoothBlurOfAnImpulseIsThreeBoxesWideAndKeepsItsSum()
    {
        double[] kernel = [1];
        for (var pass = 0; pass < 3; pass++)
        {
            var next = new double[kernel.Length + 4];
            for (var i = 0; i < kernel.Length; i++)
                for (var k = 0; k < 5; k++) next[i + k] += kernel[i] / 5;
            kernel = next;
        }
        Assert.Equal(13, kernel.Length);
        Assert.Equal(1.0, kernel.Sum(), 1e-12);

        var b = Run(Blur(Impulse30x40, 2.0 / 64, "smooth"), 64);
        double sum = 0;
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var dx = x - 30 + 6; var dy = y - 40 + 6;
                var expected = dx is >= 0 and < 13 && dy is >= 0 and < 13 ? kernel[dx] * kernel[dy] : 0.0;
                Assert.Equal(expected, b[y * 64 + x], 1e-6);
                sum += b[y * 64 + x];
            }
        Assert.Equal(1.0, sum, 1e-5);
    }

    // The left half is 1, so the step across the wrap is at x = 0 (63 to 0) and the other at 32. Radius 4 texels, a 9-texel window.
    [Fact]
    public void ABlurTilesAcrossTheWrap()
    {
        var b = Run(Blur(HalfImage, 4.0 / 64), 64);
        float At(int x, int y = 5) => b[y * 64 + x];
        Assert.Equal(5f / 9f, At(0), 1e-6f);   // columns -4..4: 60..63 are 0 (4 texels), 0..4 are 1 (5)
        Assert.Equal(4f / 9f, At(63), 1e-6f);  // columns 59..67: 59..63 are 0 (5), 0..3 are 1 (4)
        Assert.Equal(3f / 9f, At(62), 1e-6f);
        Assert.Equal(1f / 9f, At(60), 1e-6f);
        Assert.Equal(0f, At(59), 1e-6f);
        Assert.Equal(1f, At(4), 1e-6f);
        Assert.Equal(5f / 9f, At(31), 1e-6f);  // the same step at 32
        Assert.Equal(At(0, 0), At(0, 63), 0f); // the rows do not change a thing
    }

    [Theory]
    [InlineData("box")]
    [InlineData("smooth")]
    public void RadiusZeroIsTheInput(string quality)
    {
        var b = Run(Blur(Impulse30x40, 0, quality), 64);
        for (var i = 0; i < b.Length; i++) Assert.Equal(i == 40 * 64 + 30 ? 1f : 0f, b[i]);
    }

    // The radius is a part of the width: 0.0625 is 16 texels at 256 and 64 at 1024. A step blurred by a box is a straight ramp 2r + 1 wide, so
    // each 256 texel is close to the mean of its four 1024 texels (the ramps differ by a texel's worth of quantising).
    [Theory]
    [InlineData("box")]
    [InlineData("smooth")]
    public void TheSameGraphAtTwoSizesGivesTheSamePicture(string quality)
    {
        var small = Run(Blur(HalfImage, 0.0625, quality), 256);
        var large = Run(Blur(HalfImage, 0.0625, quality), 1024);
        double worst = 0;
        for (var x = 0; x < 256; x++)
        {
            var mean = (large[3 * 1024 + 4 * x] + large[3 * 1024 + 4 * x + 1] + large[3 * 1024 + 4 * x + 2] + large[3 * 1024 + 4 * x + 3]) / 4.0;
            worst = Math.Max(worst, Math.Abs(mean - small[3 * 256 + x]));
        }
        Assert.True(worst < 0.015, $"worst difference {worst}"); // a box edge is quantised to a texel: at most 1 / (2r + 1) = 1 / 33 apart
    }

    [Theory]
    [InlineData("box")]
    [InlineData("smooth")]
    public void TheTileSizeChangesNotOneBit(string quality)
    {
        const string noise = """ "s": { "type": "fbm", "params": { "cells": [4, 4], "octaves": 4 } } """;
        var graph = Blur(noise, 0.07, quality);
        var whole = Run(graph, 128, Sources, tileSize: 128);
        foreach (var tile in new[] { 8, 32 }) Assert.True(whole.AsSpan().SequenceEqual(Run(graph, 128, Sources, tileSize: tile)), $"tile size {tile} differs");
    }

    [Fact]
    public void ABlurOfColourBlursEachChannelAndStaysColour()
    {
        const string source = """ "i": { "type": "impulse", "params": { "at": [30, 40] } }, "s": { "type": "gradient_map", "inputs": { "in": "i" }, "params": { "positions": [0, 1], "colours": ["#000000", "#ff8000"] } } """;
        var graph = Blur(source, 0.0625).Replace("\"role\": \"roughness\"", "\"role\": \"base_colour\"");
        var read = TexGraphFile.Read(graph.Replace("SIZE", "128"), Sources);
        Assert.Empty(read.Faults);
        var evaluator = new GraphEvaluator(read.Graph!, Sources);
        Assert.Equal(PortKind.Colour, evaluator.KindOf("base_colour"));
        (float R, float G, float B) at = default, outside = default;
        evaluator.Evaluate(OutputRole.BaseColour, 128, tile =>
        {
            if (In(tile.Rect, 30, 40)) at = tile.Colour(30, 40);
            if (In(tile.Rect, 5, 5)) outside = tile.Colour(5, 5);
        });
        Assert.Equal(1f / 289f, at.R, 1e-7f);
        Assert.Equal(0.5019608f / 289f, at.G, 1e-7f);
        Assert.Equal(0f, at.B);
        Assert.Equal((0f, 0f, 0f), outside);
    }

    [Fact]
    public void TheInputRectangleIsTheOutputWidenedByTheRadiusTimesThePasses()
    {
        var blur = new BlurNode();
        NodeParameters Params(string radius, string quality)
        {
            var read = TexGraphFile.Read($$"""{ "version": 1, "size": 256, "seed": 0, "nodes": { "s": { "type": "colour" }, "b": { "type": "blur", "inputs": { "in": "s" }, "params": { "radius": {{radius}}, "quality": "{{quality}}" } }, "o": { "type": "output", "inputs": { "in": "b" } } } }""");
            Assert.Empty(read.Faults);
            return new NodeParameters(blur, read.Graph!.Nodes["b"].Params);
        }
        var rect = new TexelRect(128, 0, 64, 64);
        Assert.Equal(rect.Inflate(13), blur.InputRect("in", rect, Params("0.05", "box"), 256));       // 0.05 * 256 = 12.8 -> 13
        Assert.Equal(rect.Inflate(39), blur.InputRect("in", rect, Params("0.05", "smooth"), 256));
        Assert.Equal(rect.Inflate(0), blur.InputRect("in", rect, Params("0", "smooth"), 256));
    }

    // ---- blur: a table of broken copies and what caught each ------------------------------------------------------

    /// <summary>A blur written the slow way (every texel sums its window), with one thing wrong if asked: the checks below hold the real one to it.</summary>
    private sealed class NaiveBlur(bool clamp = false, int windowLow = 0, int windowHigh = 0, int reachShort = 0, int passesOverride = 0) : NodeType
    {
        public override string Name => "blur";
        public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];
        public override IReadOnlyList<ParamSpec> Parameters { get; } = new BlurNode().Parameters;
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => inputs["in"];

        private int Passes(NodeParameters p) => passesOverride != 0 ? passesOverride : BlurNode.Passes(p.Text("quality"));

        public override TexelRect InputRect(string port, TexelRect output, NodeParameters p, int size) =>
            output.Inflate(Math.Max(0, BlurNode.Texels(p.Float("radius"), size) * Passes(p) - reachShort));

        public override void Evaluate(NodeEvaluation e)
        {
            var input = e.Input("in")!;
            var r = BlurNode.Texels(e.Parameters.Float("radius"), e.Size);
            var passes = Passes(e.Parameters);
            var rect = e.Rect.Inflate(r * passes);
            var w = rect.Width; var h = rect.Height;
            var buffer = new float[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    int sx = rect.X + x, sy = rect.Y + y;
                    if (clamp) { sx = Math.Clamp(sx, 0, e.Size - 1); sy = Math.Clamp(sy, 0, e.Size - 1); }
                    buffer[y * w + x] = input.Colour(sx, sy).R;
                }
            for (var pass = 0; pass < passes; pass++)
            {
                var nw = w - 2 * r;
                var next = new float[nw * h];
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < nw; x++)
                    {
                        double sum = 0;
                        for (var k = r - r + windowLow; k <= 2 * r + windowHigh; k++) sum += buffer[y * w + x + Math.Clamp(k, 0, w - 1 - x)];
                        next[y * nw + x] = (float)(sum / (2 * r + 1 + windowHigh - windowLow));
                    }
                buffer = next; w = nw;
            }
            for (var pass = 0; pass < passes; pass++)
            {
                var nh = h - 2 * r;
                var next = new float[w * nh];
                for (var y = 0; y < nh; y++)
                    for (var x = 0; x < w; x++)
                    {
                        double sum = 0;
                        for (var k = windowLow; k <= 2 * r + windowHigh; k++) sum += buffer[(y + Math.Clamp(k, 0, h - 1 - y)) * w + x];
                        next[y * w + x] = (float)(sum / (2 * r + 1 + windowHigh - windowLow));
                    }
                buffer = next; h = nh;
            }
            var output = e.Output();
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    if (output.Kind == PortKind.Grey) output.SetGrey(e.Rect.X + x, e.Rect.Y + y, buffer[y * w + x]);
                    else output.SetColour(e.Rect.X + x, e.Rect.Y + y, buffer[y * w + x], buffer[y * w + x], buffer[y * w + x]);
        }
    }

    /// <summary>The names of the checks a blur fails, each worked by hand above (a throw is a fail).</summary>
    private static List<string> BlurChecks(NodeRegistry registry)
    {
        var failed = new List<string>();
        void Check(string name, Action check) { try { check(); } catch (Exception) { failed.Add(name); } }
        Check("box impulse", () =>
        {
            var b = Run(Blur(Impulse30x40, 0.0625), 128, registry, 32);
            for (var y = 0; y < 128; y++)
                for (var x = 0; x < 128; x++)
                    Assert.Equal(x is >= 22 and <= 38 && y is >= 32 and <= 48 ? 1f / 289f : 0f, b[y * 128 + x], 1e-7f);
        });
        Check("smooth impulse", () =>
        {
            var b = Run(Blur(Impulse30x40, 2.0 / 64, "smooth"), 64, registry, 32);
            Assert.Equal(1.0, b.Sum(v => (double)v), 1e-5);
            Assert.Equal(b[40 * 64 + 30], b.Max()); // peak at the impulse
            Assert.Equal(0f, b[40 * 64 + 30 + 7]);  // 13 wide: 6 either side and no more
            Assert.NotEqual(0f, b[40 * 64 + 30 + 6]);
        });
        Check("wrap step", () =>
        {
            var b = Run(Blur(HalfImage, 4.0 / 64), 64, registry, 32);
            Assert.Equal(5f / 9f, b[5 * 64], 1e-6f);
            Assert.Equal(4f / 9f, b[5 * 64 + 63], 1e-6f);
        });
        Check("tile size", () =>
        {
            var graph = Blur(HalfImage, 4.0 / 64, "smooth");
            Assert.True(Run(graph, 64, registry, 64).AsSpan().SequenceEqual(Run(graph, 64, registry, 16)));
        });
        return failed;
    }

    [Fact]
    public void TheChecksPassForTheRealBlurAndForTheSlowWayRight()
    {
        Assert.Empty(BlurChecks(Sources));
        Assert.Empty(BlurChecks(WithBlur(new NaiveBlur())));
        // The running sums equal the slow sums texel for texel, to float rounding.
        var graph = Blur("""  "s": { "type": "fbm", "params": { "cells": [3, 3], "octaves": 3 } } """, 0.04, "smooth");
        var fast = Run(graph, 64, Sources); var slow = Run(graph, 64, WithBlur(new NaiveBlur()));
        for (var i = 0; i < fast.Length; i++) Assert.Equal(slow[i], fast[i], 1e-5f);
    }

    public static TheoryData<string, string> BrokenBlurs() => new()
    {
        { "the wrap dropped (reads clamp to the image's edge)", "wrap step" },
        { "an off-by-one window (r - 1 to r, the texel itself left out of the middle)", "box impulse" },
        { "one pass where smooth says three", "smooth impulse" },
    };

    [Theory]
    [MemberData(nameof(BrokenBlurs))]
    public void ABrokenBlurIsCaught(string what, string caughtBy)
    {
        NodeType broken = what.StartsWith("the wrap") ? new NaiveBlur(clamp: true)
            : what.StartsWith("an off") ? new NaiveBlur(windowHigh: -1)
            : new NaiveBlur(passesOverride: 1);
        Assert.Contains(caughtBy, BlurChecks(WithBlur(broken)));
    }

    [Fact]
    public void ARectangleOneTexelShortIsCaughtBecauseTheReadThrows()
    {
        // the window reaches r past the output; asking for r - 1 makes the first read outside the rectangle throw.
        var failed = BlurChecks(WithBlur(new NaiveBlur(reachShort: 1)));
        Assert.Contains("box impulse", failed);
        Assert.Contains("wrap step", failed);
    }

    // ---- gradient_map ---------------------------------------------------------------------------------------------

    private static readonly (float R, float G, float B)[] Rgb = [(1, 0, 0), (0, 1, 0), (0, 0, 1)];
    private static readonly float[] Stops = [0, 0.25f, 1];

    [Fact]
    public void AGradientMapGivesTheStopColoursAtTheStopsAndMixesBetween()
    {
        Assert.Equal((1f, 0f, 0f), GradientMapNode.Map(0f, Stops, Rgb));
        Assert.Equal((0f, 1f, 0f), GradientMapNode.Map(0.25f, Stops, Rgb));
        Assert.Equal((0f, 0f, 1f), GradientMapNode.Map(1f, Stops, Rgb));
        Assert.Equal((0.5f, 0.5f, 0f), GradientMapNode.Map(0.125f, Stops, Rgb));   // half way from red to green
        Assert.Equal((0f, 0.5f, 0.5f), GradientMapNode.Map(0.625f, Stops, Rgb));   // half way from green to blue
        var (r, g, b) = GradientMapNode.Map(0.0625f, Stops, Rgb);                  // a quarter of the way from red to green
        Assert.Equal((0.75f, 0.25f, 0f), (r, g, b));
    }

    [Fact]
    public void AGradientMapIsClampedBeyondItsEnds()
    {
        float[] stops = [0.2f, 0.8f];
        (float, float, float)[] colours = [(0.1f, 0.2f, 0.3f), (0.9f, 0.8f, 0.7f)];
        Assert.Equal((0.1f, 0.2f, 0.3f), GradientMapNode.Map(0f, stops, colours));
        Assert.Equal((0.1f, 0.2f, 0.3f), GradientMapNode.Map(-3f, stops, colours));
        Assert.Equal((0.9f, 0.8f, 0.7f), GradientMapNode.Map(1f, stops, colours));
        Assert.Equal((0.9f, 0.8f, 0.7f), GradientMapNode.Map(5f, stops, colours));
    }

    [Fact]
    public void TheMapRunsInTheGraphOnEachTexelOfItsInput()
    {
        // gradient u: texel x holds (x + 0.5) / 64. Stops 0, 0.25, 1 of red, green, blue: texel 8 holds 0.1328125.
        const string graph = """{ "version": 1, "size": 64, "seed": 0, "nodes": { "g": { "type": "gradient" }, "m": { "type": "gradient_map", "inputs": { "in": "g" }, "params": { "positions": [0, 0.25, 1], "colours": ["#ff0000", "#00ff00", "#0000ff"] } }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "base_colour" } } } }""";
        var read = TexGraphFile.Read(graph);
        Assert.Empty(read.Faults);
        var seen = new Dictionary<int, (float R, float G, float B)>();
        new GraphEvaluator(read.Graph!).Evaluate(OutputRole.BaseColour, 64, tile =>
        {
            foreach (var x in new[] { 0, 8, 15, 63 }) if (In(tile.Rect, x, 3)) seen[x] = tile.Colour(x, 3);
        });
        float V(int x) => (x + 0.5f) / 64f;
        foreach (var x in new[] { 0, 8, 15, 63 })
        {
            var (er, eg, eb) = GradientMapNode.Map(V(x), Stops, Rgb);
            Assert.Equal(er, seen[x].R, 1e-6f); Assert.Equal(eg, seen[x].G, 1e-6f); Assert.Equal(eb, seen[x].B, 1e-6f);
        }
        Assert.Equal(1f - V(8) / 0.25f, seen[8].R, 1e-6f);   // hand: 1 - 0.1328125 / 0.25
        Assert.Equal(V(8) / 0.25f, seen[8].G, 1e-6f);
    }

    private static List<string> MapFaults(string positions, string colours)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": 64, "seed": 0, "nodes": { "g": { "type": "gradient" }, "m": { "type": "gradient_map", "inputs": { "in": "g" }, "params": { "positions": {{positions}}, "colours": {{colours}} } }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "base_colour" } } } }""");
        Assert.True(read.Loaded, read.Error?.ToString());
        return [.. read.Faults.Select(f => f.Message)];
    }

    [Fact]
    public void ACountMismatchIsAFaultNamingTheNodeAndBothParameters()
    {
        var faults = MapFaults("[0, 0.5, 1]", """["#000000", "#ffffff"]""");
        var fault = Assert.Single(faults);
        Assert.Contains("node 'm'", fault);
        Assert.Contains("'positions'", fault);
        Assert.Contains("'colours'", fault);
        Assert.Contains("positions has 3 and colours has 2", fault);
    }

    [Fact]
    public void PositionsOutOfOrderAreAFaultNamingTheNodeAndBothParameters()
    {
        var fault = Assert.Single(MapFaults("[0, 0.8, 0.5]", """["#000000", "#808080", "#ffffff"]"""));
        Assert.Contains("node 'm'", fault);
        Assert.Contains("'positions'", fault);
        Assert.Contains("'colours'", fault);
        Assert.Contains("stop 3 (0.5) is below stop 2 (0.8)", fault);
    }

    [Fact]
    public void AGoodMapAndAnEqualPairHaveNoFault()
    {
        Assert.Empty(MapFaults("[0, 0.5, 0.5, 1]", """["#000000", "#404040", "#c0c0c0", "#ffffff"]"""));
        Assert.Equal((0.25f, 0.25f, 0.25f), GradientMapNode.Map(0.49999f, [0f, 0.5f, 0.5f, 1f], [(0f, 0f, 0f), (0.25f, 0.25f, 0.25f), (0.75f, 0.75f, 0.75f), (1f, 1f, 1f)]), new TupleComparer());
        Assert.Equal((0.75f, 0.75f, 0.75f), GradientMapNode.Map(0.5f, [0f, 0.5f, 0.5f, 1f], [(0f, 0f, 0f), (0.25f, 0.25f, 0.25f), (0.75f, 0.75f, 0.75f), (1f, 1f, 1f)]));
    }

    private sealed class TupleComparer : IEqualityComparer<(float R, float G, float B)>
    {
        public bool Equals((float R, float G, float B) a, (float R, float G, float B) b) => Math.Abs(a.R - b.R) < 1e-3 && Math.Abs(a.G - b.G) < 1e-3 && Math.Abs(a.B - b.B) < 1e-3;
        public int GetHashCode((float R, float G, float B) v) => 0;
    }

    // Broken copies of the lookup, each caught by the hand-worked values above.
    private static (float R, float G, float B) FromTheWrongEnd(float v, float[] stops, (float R, float G, float B)[] colours) =>
        GradientMapNode.Map(v, [.. stops.Select(s => 1 - s).Reverse()], colours);   // stops read from the wrong end: 1 - position, reversed

    private static (float R, float G, float B) NotClamped(float v, float[] stops, (float R, float G, float B)[] colours)
    {
        var i = v < stops[0] ? 1 : v >= stops[^1] ? stops.Length - 1 : Array.FindIndex(stops, s => s > v);
        var t = (v - stops[i - 1]) / (stops[i] - stops[i - 1]);
        var a = colours[i - 1]; var b = colours[i];
        return (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    private static (float R, float G, float B) StepsNotMixed(float v, float[] stops, (float R, float G, float B)[] colours) =>
        colours[Math.Max(0, Array.FindLastIndex(stops, s => s <= v))];

    private static bool MapChecksPass(Func<float, float[], (float R, float G, float B)[], (float R, float G, float B)> map)
    {
        bool Near((float R, float G, float B) a, (float R, float G, float B) b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) < 1e-5;
        float[] ends = [0.2f, 0.8f];
        (float, float, float)[] two = [(0.1f, 0.2f, 0.3f), (0.9f, 0.8f, 0.7f)];
        return Near(map(0f, Stops, Rgb), (1, 0, 0)) && Near(map(0.25f, Stops, Rgb), (0, 1, 0)) && Near(map(1f, Stops, Rgb), (0, 0, 1))
            && Near(map(0.125f, Stops, Rgb), (0.5f, 0.5f, 0)) && Near(map(0.625f, Stops, Rgb), (0, 0.5f, 0.5f))
            && Near(map(0f, ends, two), (0.1f, 0.2f, 0.3f)) && Near(map(1f, ends, two), (0.9f, 0.8f, 0.7f));
    }

    [Fact]
    public void ABrokenGradientMapIsCaughtByTheHandWorkedValues()
    {
        Assert.True(MapChecksPass(GradientMapNode.Map));
        Assert.False(MapChecksPass(FromTheWrongEnd));   // stops read from the wrong end: caught at the first stop
        Assert.False(MapChecksPass(NotClamped));        // not clamped beyond the ends: caught by the 0.2..0.8 pair
        Assert.False(MapChecksPass(StepsNotMixed));     // the stop colour held, not mixed: caught between stops
    }
}
