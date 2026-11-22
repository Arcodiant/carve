using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// T-138's two far-reading filters: transform (tile, scale, rotate, mirror, offset) and distance (exact Euclidean to a
/// mask's edge, windowed by its range), worked by hand, across the wrap, size- and tile-invariant, and each shown failing
/// on a broken copy.
/// </summary>
[Trait("Tier", "unit")]
public class TransformDistanceTests
{
    private const int N = 64;

    // ---- running a graph --------------------------------------------------------------------------------------

    private static TextureGraph Graph(string nodes, int size, string source)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": 7, "nodes": { {{nodes}}, "o": { "type": "output", "inputs": { "in": "{{source}}" }, "params": { "role": "base_colour" } } } }""");
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    /// <summary>One channel of <paramref name="source"/>, row-major, evaluated in tiles of <paramref name="tileSize"/> (0: whole).</summary>
    private static float[] Run(string nodes, string source, int size = N, int tileSize = 0, int channel = 0)
    {
        var result = new float[size * size];
        new GraphEvaluator(Graph(nodes, size, source)).Evaluate(OutputRole.BaseColour, size, tile =>
        {
            for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                for (var x = tile.Rect.X; x < tile.Rect.Right; x++)
                {
                    var (r, g, b) = tile.Colour(x, y);
                    result[y * size + x] = channel == 0 ? r : channel == 1 ? g : b;
                }
        }, tileSize == 0 ? size : tileSize);
        return result;
    }

    /// <summary>Node <paramref name="id"/>'s parameters, as the validator resolves them.</summary>
    private static NodeParameters Params(string nodes, int size, string id)
    {
        var node = Graph(nodes, size, id).Nodes[id];
        return new NodeParameters(NodeRegistry.BuiltIn.Find(node.Type)!, node.Params);
    }

    private static float At(float[] image, int x, int y, int size = N) => image[Tile.Wrap(y, size) * size + Tile.Wrap(x, size)];

    /// <summary>The source every transform test turns: smooth grey fbm, so bilinear reads differ from nearest ones.</summary>
    private const string Source = "\"src\": { \"type\": \"fbm\", \"params\": { \"cells\": [4, 4], \"octaves\": 3 } }";

    /// <summary>A colour source: fbm toned by a colour, so its three channels differ.</summary>
    private const string ColourSource = Source + ", \"tint\": { \"type\": \"colour\", \"params\": { \"colour\": \"#c08040\" } }, \"csrc\": { \"type\": \"blend\", \"inputs\": { \"a\": \"tint\", \"b\": \"src\" }, \"params\": { \"mode\": \"multiply\" } }";

    // ---- transform: the settings, the real node and a breakable copy ---------------------------------------------

    private sealed record T(double Ox = 0, double Oy = 0, int Tx = 1, int Ty = 1, string Mirror = "none", double Rotation = 0, double Scale = 1, string Filter = "bilinear")
    {
        public string Json => System.FormattableString.Invariant(
            $"\"tf\": {{ \"type\": \"transform\", \"inputs\": {{ \"in\": \"src\" }}, \"params\": {{ \"offset\": [{Ox}, {Oy}], \"tile\": [{Tx}, {Ty}], \"mirror\": \"{Mirror}\", \"rotation\": {Rotation}, \"scale\": {Scale}, \"filter\": \"{Filter}\" }} }}");
    }

    private delegate float[] TransformFn(float[] src, T t);

    private static float[] RealTransform(float[] src, T t) => Run(Source + ", " + t.Json, "tf");

    /// <summary>A copy of transform over a whole image, with the angle's sign, the order of the steps, the wrap or the bilinear centre optionally broken.</summary>
    private static float[] TransformCopy(float[] src, T t, bool angleSign = true, bool order = true, bool wrap = true, bool centres = true, bool mirrorAxes = true)
    {
        var (sin, cos) = TransformNode.SinCos(t.Rotation);
        if (!angleSign) sin = -sin;
        double c = N / 2.0;
        var result = new float[N * N];
        float Read(int x, int y) => wrap ? At(src, x, y) : src[Math.Clamp(y, 0, N - 1) * N + Math.Clamp(x, 0, N - 1)];
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                double qx = x + 0.5, qy = y + 0.5;
                void Offset() { qx -= t.Ox * N; qy -= t.Oy * N; }
                void Mirror()
                {
                    bool u = t.Mirror is "u" or "both", v = t.Mirror is "v" or "both";
                    if (!mirrorAxes) (u, v) = (v, u);
                    if (u) qx = N - qx;
                    if (v) qy = N - qy;
                }
                void Rotate() { double dx = qx - c, dy = qy - c; qx = c + cos * dx + sin * dy; qy = c - sin * dx + cos * dy; }
                if (order) { Offset(); Mirror(); Rotate(); }
                else { Rotate(); Mirror(); Offset(); }
                qx = (c + (qx - c) / t.Scale) * t.Tx; qy = (c + (qy - c) / t.Scale) * t.Ty;
                double fx = centres ? qx - 0.5 : qx, fy = centres ? qy - 0.5 : qy;
                int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
                float tx = (float)(fx - ix), ty = (float)(fy - iy);
                result[y * N + x] = TileRead.Bilinear(Read(ix, iy), Read(ix + 1, iy), Read(ix, iy + 1), Read(ix + 1, iy + 1), tx, ty);
            }
        return result;
    }

    private static float[] SourceImage() => Run(Source, "src");

    // the checks a transform must pass, each against the source worked by hand

    private static void CheckIdentity(TransformFn f)
    {
        var src = SourceImage();
        Assert.Equal(src, f(src, new T()));
    }

    /// <summary>An offset of whole texels, (3, -5), is the source shifted, wrapping: out(x, y) = in(x - 3, y + 5).</summary>
    private static void CheckOffset(TransformFn f)
    {
        var src = SourceImage();
        var got = f(src, new T(Ox: 3.0 / N, Oy: -5.0 / N));
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
                Assert.Equal(At(src, x - 3, y + 5), got[y * N + x]);
    }

    /// <summary>A quarter turn clockwise is the source transposed and flipped: out(x, y) = in(y, N - 1 - x); -90 the other way.</summary>
    private static void CheckQuarterTurn(TransformFn f)
    {
        var src = SourceImage();
        var cw = f(src, new T(Rotation: 90));
        var ccw = f(src, new T(Rotation: -90));
        var half = f(src, new T(Rotation: 180));
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                Assert.Equal(src[(N - 1 - x) * N + y], cw[y * N + x]);
                Assert.Equal(src[x * N + (N - 1 - y)], ccw[y * N + x]);
                Assert.Equal(src[(N - 1 - y) * N + (N - 1 - x)], half[y * N + x]);
            }
    }

    /// <summary>Mirror u is left for right, v top for bottom.</summary>
    private static void CheckMirror(TransformFn f)
    {
        var src = SourceImage();
        var u = f(src, new T(Mirror: "u"));
        var v = f(src, new T(Mirror: "v"));
        var both = f(src, new T(Mirror: "both"));
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                Assert.Equal(src[y * N + (N - 1 - x)], u[y * N + x]);
                Assert.Equal(src[(N - 1 - y) * N + x], v[y * N + x]);
                Assert.Equal(src[(N - 1 - y) * N + (N - 1 - x)], both[y * N + x]);
            }
    }

    /// <summary>
    /// The order, worked by hand: a quarter turn and then an offset of a quarter in u. Output texel (8, 4), centre (8.5, 4.5):
    /// undo the offset, (-7.5, 4.5); undo the turn about (32, 32), (dx, dy) = (-39.5, -27.5) to (dy, -dx) = (-27.5, 39.5),
    /// (4.5, 71.5), texel (4, 71), wrapped (4, 7). Turned after the offset it would read (4, 55).
    /// </summary>
    private static void CheckOrder(TransformFn f)
    {
        var src = SourceImage();
        var got = f(src, new T(Ox: 0.25, Rotation: 90));
        Assert.Equal(At(src, 4, 7), got[4 * N + 8]);
        Assert.NotEqual(At(src, 4, 55), got[4 * N + 8]);
    }

    // ---- transform: tests -------------------------------------------------------------------------------------

    [Fact]
    public void TheIdentityEqualsItsInputBitForBit()
    {
        CheckIdentity(RealTransform);
        // colour, all three channels, at two tile sizes
        var tf = ", \"tf\": { \"type\": \"transform\", \"inputs\": { \"in\": \"csrc\" } }";
        for (var ch = 0; ch < 3; ch++)
            foreach (var tile in new[] { 0, 16 })
                Assert.Equal(Run(ColourSource, "csrc", channel: ch), Run(ColourSource + tf, "tf", tileSize: tile, channel: ch));
    }

    [Fact]
    public void AnOffsetOfWholeTexelsIsAShiftAcrossTheWrap() => CheckOffset(RealTransform);

    [Fact]
    public void AQuarterTurnIsTheImageTransposedAndFlipped() => CheckQuarterTurn(RealTransform);

    [Fact]
    public void MirrorSwapsLeftForRightAndTopForBottom() => CheckMirror(RealTransform);

    [Fact]
    public void TheStepsApplyInTheirOrder() => CheckOrder(RealTransform);

    [Fact]
    public void TileTwoIsTheInputAtHalfSizeRepeated()
    {
        var src = SourceImage();
        var got = RealTransform(src, new T(Tx: 2, Ty: 2));
        // texel (x, y) reads the point (2x + 1, 2y + 1) of the source: the mean of the 2 x 2 block at (2x, 2y), repeated twice across
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                int sx = 2 * x % N, sy = 2 * y % N;
                var mean = (At(src, sx, sy) + At(src, sx + 1, sy) + At(src, sx, sy + 1) + At(src, sx + 1, sy + 1)) / 4;
                Assert.Equal(mean, got[y * N + x], 1e-6f);
            }
        // and 3 x 1: a third the width across, the full height down
        var three = RealTransform(src, new T(Tx: 3));
        Assert.Equal(At(src, 3 * 10 + 1, 5), three[5 * N + 10], 1e-6f);
    }

    [Fact]
    public void ScaleHalfIsTileTwoMovedAQuarter()
    {
        // halved about the centre, the copy spans 16..48: tile 2's copies moved a quarter
        var src = SourceImage();
        Assert.Equal(RealTransform(src, new T(Tx: 2, Ty: 2, Ox: 0.25, Oy: 0.25)), RealTransform(src, new T(Scale: 0.5)));
    }

    [Fact]
    public void ThirtyDegreesMatchesTheMappingAtEveryTexel()
    {
        // the mapping worked independently: undo a 30-degree clockwise turn about (32, 32), then read bilinear between texel centres
        var src = SourceImage();
        var got = RealTransform(src, new T(Rotation: 30));
        double cos = Math.Sqrt(3) / 2, sin = 0.5;
        float worst = 0;
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                double dx = x + 0.5 - 32, dy = y + 0.5 - 32;
                double sx = 32 + cos * dx + sin * dy - 0.5, sy = 32 - sin * dx + cos * dy - 0.5;
                int ix = (int)Math.Floor(sx), iy = (int)Math.Floor(sy);
                double fx = sx - ix, fy = sy - iy;
                var want = (1 - fy) * ((1 - fx) * At(src, ix, iy) + fx * At(src, ix + 1, iy)) + fy * ((1 - fx) * At(src, ix, iy + 1) + fx * At(src, ix + 1, iy + 1));
                worst = Math.Max(worst, Math.Abs((float)want - got[y * N + x]));
            }
        Assert.True(worst < 1e-5f, $"worst difference {worst}");
        // one texel by hand: (40, 32), centre (40.5, 32.5), d = (8.5, 0.5), back (32 + 7.6112, 32 - 3.8170) = (39.611, 28.183)
        var (bx, by) = TransformNode.Mapping.Of(Params(Source + ", " + new T(Rotation: 30).Json, N, "tf"), N).Back(40.5, 32.5);
        Assert.Equal(39.6112, bx, 3);
        Assert.Equal(28.1830, by, 3);
    }

    [Fact]
    public void NearestReadsOneSourceTexel()
    {
        var src = SourceImage();
        var values = src.ToHashSet();
        var got = Run(Source + ", " + new T(Rotation: 30, Filter: "nearest").Json, "tf");
        Assert.All(got, v => Assert.Contains(v, values));
        // (40, 32) falls in texel (39, 28)
        Assert.Equal(At(src, 39, 28), got[32 * N + 40]);
    }

    [Fact]
    public void AnyTileSizeGivesTheSameBytes()
    {
        var nodes = ColourSource + ", \"tf\": { \"type\": \"transform\", \"inputs\": { \"in\": \"csrc\" }, \"params\": { \"offset\": [0.3, -0.1], \"tile\": [3, 2], \"mirror\": \"u\", \"rotation\": 33, \"scale\": 0.7 } }";
        for (var ch = 0; ch < 3; ch++)
        {
            var whole = Run(nodes, "tf", size: 128, channel: ch);
            foreach (var tile in new[] { 16, 32, 64 }) Assert.Equal(whole, Run(nodes, "tf", size: 128, tileSize: tile, channel: ch));
        }
    }

    [Fact]
    public void ATurnedTileReadsOnlyTheFewSourceTilesUnderIt()
    {
        var p = Params(Source + ", " + new T(Rotation: 30).Json, 512, "tf");
        var type = new TransformNode();
        // a 64-square tile turned 30 degrees spans 64 (cos 30 + sin 30) = 87.4 texels, and 2 each side
        var rect = type.InputRect("in", new TexelRect(192, 64, 64, 64), p, 512);
        Assert.InRange(rect.Width, 87, 93);
        Assert.InRange(rect.Height, 87, 93);
        var cover = TileRead.Cover(rect, 512, 64);
        Assert.True(cover.Width / 64 * (cover.Height / 64) <= 9, $"covers {cover}");
        // a strong minify, tile 16 at scale 0.25, asks for the whole image
        var minify = Params(Source + ", " + new T(Tx: 16, Ty: 16, Scale: 0.25).Json, 512, "tf");
        var wide = type.InputRect("in", new TexelRect(0, 0, 64, 64), minify, 512);
        Assert.True(wide.Width >= 512 && wide.Height >= 512, $"asks {wide}");
    }

    // ---- distance: the real node and a breakable copy --------------------------------------------------------------

    /// <summary>A 16-texel square centred in the image (texels 24 to 39 on each axis).</summary>
    private const string Square = "\"m\": { \"type\": \"shape\", \"params\": { \"count\": [1, 1], \"kind\": \"square\", \"size\": 0.25 } }";

    /// <summary>The same square moved half the image, so it straddles the corner (texels 56 to 7 on each axis, wrapping).</summary>
    private const string Corner = Square + ", \"mw\": { \"type\": \"transform\", \"inputs\": { \"in\": \"m\" }, \"params\": { \"offset\": [0.5, 0.5] } }";

    private delegate float[] DistanceFn(string maskNodes, string mask, double range, string side);

    private static string DistanceJson(string mask, double range, string side) => System.FormattableString.Invariant(
        $"\"d\": {{ \"type\": \"distance\", \"inputs\": {{ \"in\": \"{mask}\" }}, \"params\": {{ \"range\": {range}, \"side\": \"{side}\" }} }}");

    private static float[] RealDistance(string maskNodes, string mask, double range, string side) => Run(maskNodes + ", " + DistanceJson(mask, range, side), "d", tileSize: 16);

    /// <summary>
    /// A copy of distance over the whole image: the two passes (u, then the parabolas' envelope in v), with the wrap,
    /// the squares in the second pass, the second pass itself or the side optionally broken.
    /// </summary>
    private static float[] DistanceCopy(string maskNodes, string mask, double range, string side, bool wrap = true, bool squared = true, bool secondPass = true, bool sides = true)
    {
        var m = Run(maskNodes, mask);
        bool In(int x, int y) => wrap ? At(m, x, y) >= 0.5f : x >= 0 && x < N && y >= 0 && y < N && m[y * N + x] >= 0.5f;
        // the first pass: along rows -N to 2N - 1 (so the second can reach N either way), the u distance to the nearest texel of each side
        var rowTo = new double[2, 3 * N, N];
        for (var t = 0; t < 2; t++)
            for (var ry = 0; ry < 3 * N; ry++)
                for (var x = 0; x < N; x++)
                {
                    var row = double.PositiveInfinity;
                    for (var dx = -N; dx <= N; dx++)
                        if (In(x + dx, ry - N) == (t == 1)) row = Math.Min(row, Math.Abs(dx));
                    rowTo[t, ry, x] = row;
                }
        var r = (double)(float)range * N;   // the node reads range as a float
        var result = new float[N * N];
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                var self = In(x, y);
                var measured = side == "both" || (side == "inside") == (sides ? self : !self);
                if (!measured) continue;
                var other = self ? 0 : 1;
                var best = double.PositiveInfinity;
                for (var dy = -N; dy <= N; dy++)
                {
                    if (!secondPass && dy != 0) continue;
                    var row = rowTo[other, y + dy + N, x];
                    if (double.IsPositiveInfinity(row)) continue;
                    best = Math.Min(best, squared ? (double)dy * dy + row * row : Math.Abs(dy) + row);
                }
                result[y * N + x] = squared ? DistanceNode.Value(best, r) : DistanceNode.Value(best * best, r);
            }
        return result;
    }

    /// <summary>
    /// The square, worked by hand at chosen texels (range 0.25, 16 texels, so a value is the distance / 16): inside at
    /// (24, 30), one texel from the edge, 1/16; at (31, 31), 8 texels in, 8/16; outside at (20, 30), 4 texels left of
    /// column 24, 4/16; at (21, 21), diagonal (3, 3) from the corner texel (24, 24), sqrt 18 / 16; at (41, 44) from
    /// (39, 39), sqrt(4 + 25) / 16. Measured inside, every outside texel is 0, and outside every inside texel is.
    /// </summary>
    private static void CheckSquare(DistanceFn f)
    {
        var inside = f(Square, "m", 0.25, "inside");
        var outside = f(Square, "m", 0.25, "outside");
        var both = f(Square, "m", 0.25, "both");
        Assert.Equal(1f / 16, inside[30 * N + 24], 1e-6f);
        Assert.Equal(8f / 16, inside[31 * N + 31], 1e-6f);
        Assert.Equal(0f, inside[30 * N + 20]);
        Assert.Equal(4f / 16, outside[30 * N + 20], 1e-6f);
        Assert.Equal((float)(Math.Sqrt(18) / 16), outside[21 * N + 21], 1e-6f);
        Assert.Equal((float)(Math.Sqrt(29) / 16), outside[44 * N + 41], 1e-6f);
        Assert.Equal(0f, outside[31 * N + 31]);
        Assert.Equal(inside[31 * N + 31], both[31 * N + 31]);
        Assert.Equal(outside[21 * N + 21], both[21 * N + 21]);
        // a texel further than the range from the square, (0, 0): 24 texels in u and v, clamped to 1
        Assert.Equal(1f, outside[0]);
    }

    /// <summary>The square across the corner (texels 56..63 and 0..7): inside at (0, 0), 8 texels from column 55 and from 8, 8/16; outside at (10, 2), 3 texels from column 7, 3/16; at (60, 11), 4 from row 7.</summary>
    private static void CheckWrap(DistanceFn f)
    {
        var inside = f(Corner, "mw", 0.25, "inside");
        var outside = f(Corner, "mw", 0.25, "outside");
        Assert.Equal(8f / 16, inside[0], 1e-6f);
        Assert.Equal(1f / 16, inside[63 * N + 56], 1e-6f);
        Assert.Equal(3f / 16, outside[2 * N + 10], 1e-6f);
        Assert.Equal(4f / 16, outside[11 * N + 60], 1e-6f);
    }

    // ---- distance: tests --------------------------------------------------------------------------------------

    [Fact]
    public void TheSquareMeasuresTheEuclideanFormulaAtChosenTexels() => CheckSquare(RealDistance);

    [Fact]
    public void ASquareAcrossTheCornerMeasuresAcrossTheWrap() => CheckWrap(RealDistance);

    [Fact]
    public void EveryTexelEqualsTheBruteForceDistanceAtAnyTileSize()
    {
        // an irregular mask: fbm thresholded at its middle
        var mask = Source + ", \"mk\": { \"type\": \"levels\", \"inputs\": { \"in\": \"src\" }, \"params\": { \"in_low\": 0.45, \"in_high\": 0.55 } }";
        foreach (var side in new[] { "inside", "outside", "both" })
            foreach (var range in new[] { 0.0, 0.05, 0.1, 0.25 })
            {
                var want = DistanceCopy(mask, "mk", range, side);
                foreach (var tile in new[] { 0, 16, 32 })
                    Assert.Equal(want, Run(mask + ", " + DistanceJson("mk", range, side), "d", tileSize: tile));
            }
    }

    [Fact]
    public void TheWindowIsTheRangeAndOne()
    {
        var p = Params(Square + ", " + DistanceJson("m", 0.1, "both"), N, "d");
        Assert.Equal(new TexelRect(128 - 821, -821, 128 + 2 * 821, 128 + 2 * 821), new DistanceNode().InputRect("in", new TexelRect(128, 0, 128, 128), p, 8192));
    }

    // ---- the broken copies ---------------------------------------------------------------------------------

    [Fact]
    public void EveryNodeIsCaughtOnABrokenCopy()
    {
        // the copies, unbroken, pass every check the real nodes pass
        TransformFn t = (s, p) => TransformCopy(s, p);
        CheckIdentity(t); CheckOffset(t); CheckQuarterTurn(t); CheckMirror(t); CheckOrder(t);
        DistanceFn d = (n, m, r, s) => DistanceCopy(n, m, r, s);
        CheckSquare(d); CheckWrap(d);

        var breaks = new (string Name, Action Check)[]
        {
            ("transform: the angle's sign flipped", () => CheckQuarterTurn((s, p) => TransformCopy(s, p, angleSign: false))),
            ("transform: the steps undone in the wrong order", () => CheckOrder((s, p) => TransformCopy(s, p, order: false))),
            ("transform: reads clamped instead of wrapping", () => CheckOffset((s, p) => TransformCopy(s, p, wrap: false))),
            ("transform: bilinear about texel corners, not centres", () => CheckIdentity((s, p) => TransformCopy(s, p, centres: false))),
            ("transform: mirror's u and v swapped", () => CheckMirror((s, p) => TransformCopy(s, p, mirrorAxes: false))),
            ("distance: the second pass sums distances, not squares", () => CheckSquare((n, m, r, s) => DistanceCopy(n, m, r, s, squared: false))),
            ("distance: the second pass left out", () => CheckSquare((n, m, r, s) => DistanceCopy(n, m, r, s, secondPass: false))),
            ("distance: no wrap", () => CheckWrap((n, m, r, s) => DistanceCopy(n, m, r, s, wrap: false))),
            ("distance: inside and outside swapped", () => CheckSquare((n, m, r, s) => DistanceCopy(n, m, r, s, sides: false))),
        };
        foreach (var (name, check) in breaks)
            Assert.True(Throws(check), $"the broken copy '{name}' was not caught");
    }

    private static bool Throws(Action check)
    {
        try { check(); return false; }
        catch (Xunit.Sdk.XunitException) { return true; }
    }
}
