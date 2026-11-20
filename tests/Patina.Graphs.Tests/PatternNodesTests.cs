using System.Globalization;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// T-136's three node types, tiles, stripes and shape, worked by hand: values at chosen texels, tiling, size-invariance, the gap and
/// bevel measured in texels, jitter held to its slot, and a table of broken copies and the check that catches each.
/// </summary>
[Trait("Tier", "unit")]
public class PatternNodesTests
{
    private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Runs node "t" of the given JSON (a type with params) in a graph of <paramref name="size"/> and returns one channel of its port as floats, row-major.</summary>
    private static float[] Run(string node, int size, string port = "", int channel = 0, uint seed = 7, int? tileSize = null)
    {
        var from = port == "" ? "t" : "t." + port;
        var role = port == "uv" ? OutputRole.BaseColour : OutputRole.Roughness;
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": {{seed}}, "nodes": { "t": {{node}}, "o": { "type": "output", "inputs": { "in": "{{from}}" }, "params": { "role": "{{OutputRoles.Name(role)}}" } } } }""");
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        var result = new float[size * size];
        new GraphEvaluator(read.Graph!).Evaluate(role, size, tile =>
        {
            for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                for (var x = tile.Rect.X; x < tile.Rect.Right; x++)
                {
                    var (r, g, _) = tile.Colour(x, y);
                    result[y * size + x] = channel == 0 ? r : g;
                }
        }, tileSize);
        return result;
    }

    private static string Tiles(string count, double stagger = 0, double gap = 0.1, double bevel = 0.1, double jitter = 0, string orientation = "rows") =>
        $$"""{ "type": "tiles", "params": { "count": {{count}}, "stagger": {{F(stagger)}}, "gap": {{F(gap)}}, "bevel": {{F(bevel)}}, "jitter": {{F(jitter)}}, "orientation": "{{orientation}}" } }""";

    // ---- tiles: values worked by hand ----------------------------------------------------------------------------

    // 64 texels, count [4, 2], gap 0.25, bevel 0.25: a slot is 16 wide and 32 tall; the gap is 0.25 / 4 of 64 = 4 texels, 2 either side of a
    // slot's edge, and the bevel 4 texels. In row 1 (from y = 32) stagger 0.5 shifts the slots by 8 texels.
    private const string Hand = "[4, 2]";

    [Fact]
    public void TilesGiveTheirHandWorkedValuesAtChosenTexels()
    {
        float[] Mask(double stagger) => Run(Tiles(Hand, stagger, 0.25, 0.25), 64, "mask");
        float[] Height(double stagger) => Run(Tiles(Hand, stagger, 0.25, 0.25), 64, "height");
        var mask = Mask(0.5); var height = Height(0.5);
        float M(int x, int y) => mask[y * 64 + x];
        float H(int x, int y) => height[y * 64 + x];

        Assert.Equal(0f, M(0, 10));          // row 0: x 0 and 1 are the gap
        Assert.Equal(0f, M(1, 10));
        Assert.Equal(1f, M(2, 10));          // the tile starts at 2: centre 2.5 is 0.5 in
        Assert.Equal(0f, H(1, 10));
        Assert.Equal(0.125f, H(2, 10), 5);   // 0.5 / 4
        Assert.Equal(0.375f, H(3, 10), 5);
        Assert.Equal(0.875f, H(5, 10), 5);
        Assert.Equal(1f, H(6, 10));          // 6.5 - 2 = 4.5: past the bevel
        Assert.Equal(1f, H(8, 16));          // a tile centre
        Assert.Equal(0f, M(8, 0));           // the top gap, rows 0 and 1
        Assert.Equal(0f, M(8, 1));
        Assert.Equal(0.125f, H(8, 2), 5);    // the top bevel
        Assert.Equal(0f, M(8, 30));          // the bottom gap, rows 30 and 31
        // the stagger row: the slots start at 8, so x 0 is inside the tile that wraps from the right, and x 8 and 9 are its gap
        Assert.Equal(1f, M(0, 40));
        Assert.Equal(0f, M(8, 40));
        Assert.Equal(0f, M(9, 40));
        Assert.Equal(0.125f, H(10, 40), 5);
        // with no stagger, row 1's x 0 is a gap like row 0's
        Assert.Equal(0f, Mask(0)[40 * 64 + 0]);
    }

    [Fact]
    public void TheUvOutputIsTheTexelsPlaceInItsTile()
    {
        var r = Run(Tiles(Hand, 0, 0.25, 0.25), 64, "uv", 0);
        var g = Run(Tiles(Hand, 0, 0.25, 0.25), 64, "uv", 1);
        // the tile's rect runs 2..14 across (12 wide) and 2..30 down (28 tall)
        Assert.Equal((7 + 0.5 - 2) / 12, r[15 * 64 + 7], 5);
        Assert.Equal((15 + 0.5 - 2) / 28, g[15 * 64 + 7], 5);
        Assert.Equal(0f, r[15 * 64 + 0]);    // the gap holds the nearest edge
        Assert.Equal(1f, r[15 * 64 + 15]);
    }

    [Fact]
    public void TheRandomOutputIsOneValueForATileAndDiffersBetweenTiles()
    {
        var random = Run(Tiles("[4, 4]", 0, 0, 0), 64, "random");
        var values = new HashSet<float>();
        for (var ty = 0; ty < 4; ty++)
            for (var tx = 0; tx < 4; tx++)
            {
                var first = random[ty * 16 * 64 + tx * 16];
                values.Add(first);
                for (var y = 0; y < 16; y++)
                    for (var x = 0; x < 16; x++) Assert.Equal(first, random[(ty * 16 + y) * 64 + tx * 16 + x]);
                Assert.InRange(first, 0f, 1f);
            }
        Assert.True(values.Count >= 14, $"only {values.Count} of 16 tiles differ");
        Assert.NotEqual(random, Run(Tiles("[4, 4]", 0, 0, 0), 64, "random", seed: 8));
        Assert.Equal(random, Run(Tiles("[4, 4]", 0, 0, 0), 64, "random"));
    }

    // ---- tiling, tile sizes and size ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("mask")]
    [InlineData("height")]
    [InlineData("random")]
    [InlineData("uv")]
    public void EveryOutputIsTheSameWhateverTheTileSize(string port)
    {
        var node = Tiles("[3, 5]", 0.5, 0.2, 0.2, 0.6);
        var whole = Run(node, 64, port);
        foreach (var side in new[] { 1, 16, 32 }) Assert.Equal(whole, Run(node, 64, port, tileSize: side));
        Assert.Equal(64 * 64, whole.Length);
    }

    [Fact]
    public void ATileOnTheWrapIsOneTileOnBothSides()
    {
        // row 1 of [4, 2] at stagger 0.5: a tile runs from 56 across the wrap to 7: one random value, one rect, so the uv is continuous
        var node = Tiles(Hand, 0.5, 0.25, 0.25, 0.5);
        var random = Run(node, 64, "random");
        var u = Run(node, 64, "uv", 0);
        Assert.Equal(random[40 * 64 + 63], random[40 * 64 + 0]);
        Assert.Equal(random[40 * 64 + 60], random[40 * 64 + 5]);
        Assert.InRange(Math.Abs(u[40 * 64 + 63] - u[40 * 64 + 0]), 0.0001f, 0.2f);   // consecutive texels of one tile
        Assert.True(u[40 * 64 + 63] < u[40 * 64 + 0], "u should rise across the wrap");
    }

    [Fact]
    public void AnImageOfWholeSlotsRepeatsItselfAcrossBothWraps()
    {
        // no jitter: texel (x, y) and (x + 2 slots, y + 2 slots) are alike, in the mask and the height, for rows and columns, with and without stagger
        foreach (var orientation in new[] { "rows", "columns" })
            foreach (var stagger in new[] { 0.0, 0.5 })
            {
                var mask = Run(Tiles("[4, 4]", stagger, 0.2, 0.2, 0, orientation), 64, "height");
                for (var y = 0; y < 64; y++)
                    for (var x = 0; x < 64; x++)
                        Assert.Equal(mask[y * 64 + x], mask[((y + 32) % 64) * 64 + (x + 32) % 64]);   // [4, 4]: a slot is 16; two slots on either way keeps the parity of the row or column
            }
    }

    [Fact]
    public void ARunningBondShiftsTheJointsHalfATile()
    {
        var height = Run(Tiles("[4, 4]", 0.5, 0.2, 0, 0), 64, "mask");
        // row 0's vertical joints are at x = 0, 16, ..; row 1's at 8, 24, ..
        Assert.Equal(0f, height[8 * 64 + 0]);
        Assert.Equal(1f, height[8 * 64 + 8]);
        Assert.Equal(1f, height[24 * 64 + 0]);
        Assert.Equal(0f, height[24 * 64 + 8]);
    }

    [Fact]
    public void CoverageAndRandomAgreeAt256And1024()
    {
        var node = Tiles("[4, 8]", 0.5, 0.15, 0.1, 0.4);
        var a = Run(node, 256, "mask"); var b = Run(node, 1024, "mask");
        Assert.InRange(Math.Abs(a.Average() - b.Average()), 0, 0.01);
        var ra = Run(node, 256, "random"); var rb = Run(node, 1024, "random");
        var same = 0;
        for (var y = 0; y < 256; y++)
            for (var x = 0; x < 256; x++) if (ra[y * 256 + x] == rb[(4 * y + 2) * 1024 + 4 * x + 2]) same++;
        Assert.True(same >= 256 * 256 * 0.995, $"{same} texels hold the same tile at both sizes");
    }

    // ---- gap and bevel in texels, jitter in its slot, a table of broken copies -----------------------------------------------

    /// <summary>An implementation: a layout's texel. The real one is <see cref="TilesNode.Layout.At"/>; a broken copy changes the layout or the answer.</summary>
    private delegate TilesNode.Texel Impl(TilesNode.Layout layout, int x, int y, int size);

    private static readonly Impl Real = (l, x, y, s) => l.At(x, y, s);

    private static TilesNode.Layout Layout(int cx, int cy, double stagger, double gap, double bevel, double jitter, bool columns = false) =>
        new(cx, cy, stagger, gap, bevel, jitter, columns, 7);

    /// <summary>256 texels, [4, 4] (a slot is 64), gap 0.25 and bevel 0.25 of a tile's width: 16 texels each, across and down, tiles 48 across.</summary>
    private static void GapAndBevelAreSixteenTexels(Impl at)
    {
        var l = Layout(4, 4, 0, 0.25, 0.25, 0);
        foreach (var vertical in new[] { false, true })
        {
            int Gap = 0, Ramp = 0, Flat = 0;
            for (var i = 0; i < 64; i++)
            {
                var t = vertical ? at(l, 96, 64 + i, 256) : at(l, 64 + i, 96, 256);
                if (!t.Inside) Gap++; else if (t.Height < 1) Ramp++; else Flat++;
            }
            // one slot: 8 gap + [16 ramp, 16 flat, 16 ramp, ...] + 8 gap; the run is 64 texels: gap 16, ramp 32, flat 16 (two ramps at the sides of 16)
            if (Gap != 16 || Ramp != 32 || Flat != 16) throw new Exception($"{(vertical ? "down" : "across")} one slot: gap {Gap}, ramp {Ramp}, flat {Flat}; wanted 16, 32, 16");
        }
    }

    /// <summary>A gap in the left of a slot is gap on both sides: the layout of a 16-wide image's mask at stagger 0.5 and the hand values.</summary>
    private static void StaggerShiftsAlternateRowsHalfATile(Impl at)
    {
        var l = Layout(4, 2, 0.5, 0.25, 0.25, 0);
        if (at(l, 0, 10, 64).Inside || !at(l, 2, 10, 64).Inside) throw new Exception("row 0 should have its gap at x 0 and its tile from x 2");
        if (!at(l, 0, 40, 64).Inside || at(l, 8, 40, 64).Inside) throw new Exception("row 1 should be shifted by 8 texels");
    }

    private static void JitterKeepsEveryTileInsideItsSlot(Impl at)
    {
        // 128 texels, [4, 4]: a slot is 32 square, the gap 0.2 / 4 of 128 = 6.4, 3.2 either side of a slot edge. With jitter 1 no tile reaches within 3.2 of an edge,
        // every jittered tile texel is a texel of the unjittered tile, and the tiles are not all alike.
        var plain = Layout(4, 4, 0, 0.2, 0.1, 0); var wild = Layout(4, 4, 0, 0.2, 0.1, 1);
        int plainCount = 0, wildCount = 0;
        for (var y = 0; y < 128; y++)
            for (var x = 0; x < 128; x++)
            {
                var inside = at(wild, x, y, 128).Inside;
                var was = plain.At(x, y, 128).Inside;
                if (inside && !was) throw new Exception($"jittered tile texel ({x}, {y}) lies in the gap");
                if (inside && (x % 32 < 3 || x % 32 >= 29 || y % 32 < 3 || y % 32 >= 29)) throw new Exception($"jittered tile texel ({x}, {y}) is within 3 of its slot's edge");
                if (inside) wildCount++;
                if (was) plainCount++;
            }
        if (wildCount > plainCount * 0.75 || wildCount < plainCount * 0.3) throw new Exception($"jitter 1 covers {wildCount} of {plainCount}: expected 30% to 75%");
    }

    private static void OneRandomValueForATile(Impl at)
    {
        var l = Layout(4, 4, 0, 0, 0, 0);
        var seen = new HashSet<float>();
        for (var ty = 0; ty < 4; ty++)
            for (var tx = 0; tx < 4; tx++)
            {
                var first = at(l, tx * 16 + 3, ty * 16 + 3, 64).Random;
                seen.Add(first);
                if (at(l, tx * 16 + 12, ty * 16 + 9, 64).Random != first) throw new Exception("a tile holds two random values");
            }
        if (seen.Count < 14) throw new Exception("tiles share random values");
    }

    private static void ColumnsAreRowsTransposed(Impl at)
    {
        // rows [8, 4] at gap and bevel doubled (they are parts of a tile's width, the count across 8 against 4) against columns [4, 8]: the same picture turned over its diagonal
        var cols = Layout(4, 8, 0.5, 0.2, 0.1, 0.5, columns: true);
        var rows = Layout(8, 4, 0.5, 0.4, 0.2, 0.5);
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var a = at(cols, x, y, 64); var b = rows.At(y, x, 64);
                if (a.Inside != b.Inside || a.Random != b.Random || Math.Abs(a.Height - b.Height) > 1e-6) throw new Exception($"columns differ from the transposed rows at ({x}, {y})");
            }
    }

    private static Impl WithLayout(Func<TilesNode.Layout, TilesNode.Layout> change) => (l, x, y, s) => change(l).At(x, y, s);

    private static bool Caught(Action check)
    {
        try { check(); return false; } catch (Exception) { return true; }
    }

    private static bool Throws(Action<Impl> check, Impl impl)
    {
        try { check(impl); return false; } catch (Exception) { return true; }
    }

    [Fact]
    public void TheRealTilesPassEveryCheck()
    {
        GapAndBevelAreSixteenTexels(Real);
        StaggerShiftsAlternateRowsHalfATile(Real);
        JitterKeepsEveryTileInsideItsSlot(Real);
        OneRandomValueForATile(Real);
        ColumnsAreRowsTransposed(Real);
    }

    [Fact]
    public void EveryBrokenCopyOfTilesIsCaughtByItsCheck()
    {
        var table = new (string Name, Impl Impl, Action<Impl> Check)[]
        {
            ("the gap is twice as wide", WithLayout(l => l with { Gap = l.Gap * 2 }), GapAndBevelAreSixteenTexels),
            ("the bevel is half as wide", WithLayout(l => l with { Bevel = l.Bevel / 2 }), GapAndBevelAreSixteenTexels),
            ("the gap is made no wider than the tile's height (here by dividing by the count down)", WithLayout(l => l with { Gap = l.Gap * l.CountX / Math.Max(l.CountY, 2) * 1.5 }), GapAndBevelAreSixteenTexels),
            ("stagger is ignored", WithLayout(l => l with { Stagger = 0 }), StaggerShiftsAlternateRowsHalfATile),
            ("stagger shifts the other rows", (l, x, y, s) => l.At(x, y + (l.Stagger > 0 ? s / l.CountY : 0), s), StaggerShiftsAlternateRowsHalfATile),
            ("jitter moves a tile out of its slot", (l, x, y, s) => l.At(x + (int)(l.Jitter * 3), y, s), JitterKeepsEveryTileInsideItsSlot),
            ("jitter is ignored", WithLayout(l => l with { Jitter = 0 }), JitterKeepsEveryTileInsideItsSlot),
            ("every tile of a row has the first one's random value", (l, x, y, s) => l.At(x, y, s) with { Random = l.At(0, y, s).Random }, OneRandomValueForATile),
            ("columns laid as rows", WithLayout(l => l with { Columns = false }), ColumnsAreRowsTransposed),
        };
        foreach (var (name, impl, check) in table)
            Assert.True(Throws(check, impl), $"the broken copy '{name}' was not caught");
    }

    // ---- stripes ------------------------------------------------------------------------------------------------------------

    private static string Stripes(int count, string axis, double width, double softness) =>
        $$"""{ "type": "stripes", "params": { "count": {{count}}, "axis": "{{axis}}", "width": {{F(width)}}, "softness": {{F(softness)}} } }""";

    [Fact]
    public void StripesGiveTheirHandWorkedValues()
    {
        // 64 texels, 4 stripes, width 0.5: a period is 16 texels with a stripe centred on its start, so it covers phases -0.25..0.25
        var u = Run(Stripes(4, "u", 0.5, 0), 64);
        foreach (var (x, want) in new[] { (0, 1f), (3, 1f), (4, 0f), (8, 0f), (11, 0f), (12, 1f), (15, 1f), (16, 1f), (20, 0f) })
            Assert.Equal(want, u[5 * 64 + x]);
        for (var y = 0; y < 64; y++) Assert.Equal(u[3], u[y * 64 + 3]);   // constant down a column
        // axis v is the same turned over
        var v = Run(Stripes(4, "v", 0.5, 0), 64);
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++) Assert.Equal(u[y * 64 + x], v[x * 64 + y]);
        // diagonal: 4 stripes along each axis, a stripe where (u + v) * 4 has phase within 0.25 of a whole number
        var d = Run(Stripes(4, "diagonal", 0.5, 0), 64);
        Assert.Equal(1f, d[0]);                // (0.5 + 0.5) / 64 * 4 = 0.0625
        Assert.Equal(0f, d[3 * 64 + 3]);       // 7 / 64 * 4 = 0.4375 → distance 0.4375
        Assert.Equal(1f, d[7 * 64 + 8]);       // (7.5 + 8.5) / 16 = 1.0 → on a stripe centre
        // softness 0.25 at width 0.5: the edge (phase 0.25) is half-way; at texel 16 of 64 the phase is 16.5 / 16 - 1 = 0.03125 of the way... worked from the ramp's formula
        var soft = Run(Stripes(1, "u", 0.5, 0.25), 64);
        Assert.Equal(0.4766f, soft[16], 3);    // phase 0.2578: s = 0.5156, 1 - s^2 (3 - 2s)
    }

    [Fact]
    public void StripesTileAndHoldTheirCoverageAtEverySize()
    {
        foreach (var axis in new[] { "u", "v", "diagonal" })
        {
            var node = Stripes(8, axis, 0.5, 0);
            var a = Run(node, 256); var b = Run(node, 1024);
            var tolerance = axis == "diagonal" ? 0.035 : 0.01;   // a diagonal's texel centres land on a stripe's edge: 17 of every 32 are in, 0.531
            Assert.InRange(Math.Abs(a.Average() - 0.5), 0, tolerance);
            Assert.InRange(Math.Abs(b.Average() - 0.5), 0, tolerance);
            Assert.Equal(a, Run(node, 256, tileSize: 32));
            // whole stripes across: a shift by one period (size / count) leaves it unchanged
            for (var y = 0; y < 256; y += 7)
                for (var x = 0; x < 256; x++)
                    Assert.Equal(a[y * 256 + x], a[y * 256 + (x + 32) % 256]);
        }
    }

    private static float StripeValue(double t, double width, double softness) => StripesNode.Value(t, width, softness);

    [Fact]
    public void EveryBrokenCopyOfStripesIsCaught()
    {
        static void Check(Func<double, double, double, float> value)
        {
            if (value(0, 0.5, 0) != 1 || value(0.2, 0.5, 0) != 1 || value(0.3, 0.5, 0) != 0 || value(0.7, 0.5, 0) != 0 || value(0.9, 0.5, 0) != 1)
                throw new Exception("a hard stripe of width 0.5 is centred on phase 0 and covers 0.25 either side");
            if (Math.Abs(value(0.25, 0.5, 0.25) - 0.5) > 1e-6) throw new Exception("a soft edge is half-way on the stripe's edge");
            if (Math.Abs(value(0.5, 1, 0) - 1) > 0 || value(0.5, 0, 0) != 0) throw new Exception("width 1 fills the period and width 0 empties it");
        }
        Check((t, w, s) => StripeValue(t, w, s));
        var table = new (string Name, Func<double, double, double, float> Impl)[]
        {
            ("width is a half-width", (t, w, s) => StripeValue(t, w * 2, s)),
            ("a stripe starts at the period's start", (t, w, s) => StripeValue((t + 0.5) % 1, w, s)),
            ("the ramp lies outside the edge", (t, w, s) => StripeValue(t, w - s * 2, s)),
            ("softness ignored", (t, w, s) => StripeValue(t, w, 0)),
        };
        foreach (var (name, impl) in table)
            Assert.True(Caught(() => Check(impl)), $"the broken copy '{name}' was not caught");
    }

    // ---- shape ----------------------------------------------------------------------------------------------------------------

    private static string Shape(string count, string kind, double size, double softness = 0, double rotation = 0, int sides = 6) =>
        $$"""{ "type": "shape", "params": { "count": {{count}}, "kind": "{{kind}}", "sides": {{sides}}, "size": {{F(size)}}, "softness": {{F(softness)}}, "rotation": {{F(rotation)}} } }""";

    [Fact]
    public void ShapesGiveTheirHandWorkedValues()
    {
        // 64 texels, [2, 2]: a cell is 32 texels; size 0.5 is a radius of a quarter cell, 8 texels, about the centre (16, 16) between texels 15 and 16
        var circle = Run(Shape("[2, 2]", "circle", 0.5), 64);
        Assert.Equal(1f, circle[16 * 64 + 16]);
        Assert.Equal(1f, circle[16 * 64 + 23]);      // 7.5 from the centre
        Assert.Equal(0f, circle[16 * 64 + 24]);      // 8.5
        Assert.Equal(0f, circle[16 * 64 + 31]);      // the cell's edge
        Assert.Equal(1f, circle[48 * 64 + 48]);      // the next cell, its centre
        var square = Run(Shape("[2, 2]", "square", 0.5), 64);
        Assert.Equal(1f, square[23 * 64 + 23]);      // the corner is in a square, and (7.5, 7.5) is 10.6 from the centre, outside a circle
        Assert.Equal(0f, circle[23 * 64 + 23]);
        var diamond = Run(Shape("[2, 2]", "diamond", 0.5), 64);
        Assert.Equal(1f, diamond[16 * 64 + 23]);     // |7.5| + 0.5 = 8: the vertex reaches the radius
        Assert.Equal(0f, diamond[20 * 64 + 20]);     // 4.5 + 4.5 = 9
        var ring = Run(Shape("[2, 2]", "ring", 0.5), 64);
        Assert.Equal(0f, ring[16 * 64 + 16]);        // the hole
        Assert.Equal(1f, ring[16 * 64 + 22]);        // 6.5 from the centre, between 4 and 8
        Assert.Equal(0f, ring[16 * 64 + 25]);
        var triangle = Run(Shape("[2, 2]", "polygon", 0.5, sides: 3), 64);
        Assert.Equal(1f, triangle[9 * 64 + 16]);     // 6.5 above the centre, towards the up vertex (8 away)
        Assert.Equal(0f, triangle[23 * 64 + 16]);    // 7.5 below it, but the base is 4 below
        // a hexagon of circumradius 8 holds a point 7 away along a vertex direction (up), and not one 7.5 away on a side's normal (apothem 6.93)
        var hexagon = Run(Shape("[2, 2]", "polygon", 0.5, sides: 6), 64);
        Assert.Equal(1f, hexagon[9 * 64 + 16]);
        Assert.Equal(0f, hexagon[16 * 64 + 24]);
        // soft: the edge ramps inward over softness * depth
        var soft = Run(Shape("[2, 2]", "circle", 0.5, 0.5), 64);
        Assert.Equal(1f, soft[16 * 64 + 16], 3);
        Assert.InRange(soft[16 * 64 + 21], 0.01f, 0.99f);
        // rotation turns the shape inside its cell: a square of 0.6 has its corner at 0.3 and, turned 45 degrees, at 0.424 on the axes
        var plain = Run(Shape("[1, 1]", "square", 0.6), 128);
        var turned = Run(Shape("[1, 1]", "square", 0.6, rotation: 45), 128);
        Assert.Equal(0f, plain[64 * 128 + 109]);
        Assert.Equal(1f, turned[64 * 128 + 109]);
    }

    [Fact]
    public void ShapesTileHoldTheirAreaAtEverySizeAndStayInTheirCells()
    {
        foreach (var kind in new[] { "circle", "square", "ring", "diamond", "polygon" })
        {
            var node = Shape("[4, 4]", kind, 0.5, 0.2, 30);
            var a = Run(node, 256); var b = Run(node, 1024);
            Assert.InRange(Math.Abs(a.Average() - b.Average()), 0, 0.005);
            Assert.Equal(a, Run(node, 256, tileSize: 64));
            for (var y = 0; y < 256; y += 5)
                for (var x = 0; x < 256; x++) Assert.Equal(a[y * 256 + x], a[y * 256 + (x + 64) % 256]);   // a cell is 64 texels
        }
        var circle = Run(Shape("[4, 4]", "circle", 0.5), 1024).Average();
        Assert.InRange(circle, Math.PI / 16 - 0.003, Math.PI / 16 + 0.003);
        // a shape is round in a cell that is not square: [2, 4]'s cells are 2 : 1 and the circle spans the shorter side's quarter
        var wide = Run(Shape("[2, 4]", "circle", 0.5), 256);
        Assert.Equal(1f, wide[32 * 256 + 74]);       // the cell is 128 by 64, centred at (64, 32); the circle's radius is 16 texels, a quarter of the shorter side
        Assert.Equal(0f, wide[32 * 256 + 84]);       // 20.5 from the centre; an ellipse filling the wide cell would hold it
    }

    [Fact]
    public void EveryBrokenCopyOfShapeIsCaught()
    {
        // a check is a function of a way of running a shape: the real one runs the node; a broken copy changes a parameter the way a bug would
        static void Check(Func<string, double, double, int, float[]> run)
        {
            var circle = run("circle", 0.5, 0, 6);
            if (circle[16 * 64 + 23] != 1 || circle[16 * 64 + 24] != 0 || circle[23 * 64 + 23] != 0) throw new Exception("size 0.5 of a cell is a radius of a quarter cell");
            var square = run("square", 0.5, 0, 6);
            if (square[23 * 64 + 23] != 1) throw new Exception("a square holds its corner");
            var hexagon = run("polygon", 0.5, 0, 6);
            if (hexagon[9 * 64 + 16] != 1 || hexagon[16 * 64 + 24] != 0) throw new Exception("a hexagon has a vertex up");
            var triangle = run("polygon", 0.5, 0, 3);
            if (triangle[9 * 64 + 16] != 1 || triangle[23 * 64 + 16] != 0) throw new Exception("a triangle has a vertex up and a base below the centre");
            var turned = run("square", 0.6, 45, 6);
            if (turned.Length != 64 * 64) throw new Exception("size");
        }
        float[] Real(string kind, double size, double rot, int sides) => Run(Shape("[2, 2]", kind, size, 0, rot, sides), 64);
        Check(Real);
        var table = new (string Name, Func<string, double, double, int, float[]> Impl)[]
        {
            ("size is the diameter's whole cell", (k, s, r, n) => Real(k, s * 2, r, n)),
            ("the circle is drawn as a square", (k, s, r, n) => Real(k == "circle" ? "square" : k, s, r, n)),
            ("sides ignored", (k, s, r, n) => Real(k, s, r, 4)),
        };
        foreach (var (name, impl) in table)
            Assert.True(Caught(() => Check(impl)), $"the broken copy '{name}' was not caught");
    }

    [Fact]
    public void ShapeRotationIsWorkedWithoutTheSystemSineAndCosine()
    {
        foreach (var degrees in new[] { 0.0, 30, 45, 90, 137.5, -60, 360, -360, 179.9 })
        {
            var (s, c) = ShapeNode.SinCos(degrees * Math.PI / 180);
            Assert.Equal(Math.Sin(degrees * Math.PI / 180), s, 10);
            Assert.Equal(Math.Cos(degrees * Math.PI / 180), c, 10);
        }
    }
}
