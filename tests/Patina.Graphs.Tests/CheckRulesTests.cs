using Patina.Graphs.Checks;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// Each of check's rules (T-141) on hand-made images just inside and just outside its threshold, for a graph's floats
/// and for a PNG's bytes, and a table of broken copies of the rules with the case that tells each from the right rule.
/// </summary>
[Trait("Tier", "unit")]
public class CheckRulesTests
{
    public static TheoryData<CheckSource> Sources => new() { CheckSource.Graph, CheckSource.Png };

    /// <summary>An image of <paramref name="w"/> x <paramref name="h"/> texels, each given as one byte (grey) or three (colour), read as a PNG's bytes are.</summary>
    private static CheckOutput Image(string name, int w, int h, Func<int, int, byte[]> texel)
    {
        var rgba = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var t = texel(x, y);
                var i = (y * w + x) * 4;
                rgba[i] = t[0];
                rgba[i + 1] = t[t.Length == 1 ? 0 : 1];
                rgba[i + 2] = t[t.Length == 1 ? 0 : 2];
                rgba[i + 3] = 255;
            }
        return CheckOutput.FromRgba8(name, w, h, rgba);
    }

    private static CheckSet Of(CheckSource source, params CheckOutput[] outputs) => new(source, outputs);

    private static byte[] G(int v) => [(byte)v];

    private static byte[] C(int r, int g, int b) => [(byte)r, (byte)g, (byte)b];

    private static IReadOnlyList<CheckProblem> Run(ICheckRule rule, CheckSet set) => [.. rule.Run(set)];

    // ---- base-colour-range: 10 x 10 texels, so one texel is 1%.

    private static CheckOutput Colour(int outCount, byte[] outColour, byte[] inColour) =>
        Image("base_colour", 10, 10, (x, y) => y * 10 + x < outCount ? outColour : inColour);

    [Theory]
    [MemberData(nameof(Sources))]
    public void Base_colour_one_percent_out_is_clean_and_two_percent_is_a_fault(CheckSource source)
    {
        var rule = new BaseColourRangeRule();
        Assert.Empty(Run(rule, Of(source, Colour(1, C(10, 10, 10), C(100, 100, 100)))));
        var two = Assert.Single(Run(rule, Of(source, Colour(2, C(10, 10, 10), C(100, 100, 100)))));
        Assert.Contains("2%", two.Message);
        Assert.Contains("#0a0a0a at (0, 0)", two.Message);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Base_colour_bounds_are_20_and_245_on_the_brightest_channel(CheckSource source)
    {
        var rule = new BaseColourRangeRule();
        Assert.Empty(Run(rule, Of(source, Colour(100, C(20, 5, 5), C(20, 5, 5)))));
        Assert.Single(Run(rule, Of(source, Colour(100, C(19, 5, 5), C(19, 5, 5)))));
        Assert.Empty(Run(rule, Of(source, Colour(100, C(245, 245, 245), C(245, 245, 245)))));
        Assert.Single(Run(rule, Of(source, Colour(100, C(246, 246, 246), C(246, 246, 246)))));
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void A_metal_texel_is_judged_by_120_and_a_non_metal_one_by_20(CheckSource source)
    {
        var rule = new BaseColourRangeRule();
        CheckSet With(int metallicByte, int level) =>
            Of(source, Colour(100, C(level, level, level), C(level, level, level)), Image("metallic", 10, 10, (_, _) => G(metallicByte)));
        Assert.Empty(Run(rule, With(255, 120)));
        Assert.Single(Run(rule, With(255, 119)));
        Assert.Empty(Run(rule, With(0, 60))); // a non-metal at 60 is fine
        Assert.Empty(Run(rule, With(127, 60))); // 127/255 is 0.498: still a non-metal
        Assert.Single(Run(rule, With(128, 60))); // 128/255 is 0.502: a metal
    }

    // ---- metallic-not-binary: 100 texels, over 5 is the line; 0.1 and 0.9 count as between.

    private static CheckOutput Metallic(int between, int value) =>
        Image("metallic", 10, 10, (x, y) => G(y * 10 + x < between ? value : 255));

    [Theory]
    [MemberData(nameof(Sources))]
    public void Metallic_five_percent_between_is_clean_and_six_is_a_fault(CheckSource source)
    {
        var rule = new MetallicNotBinaryRule();
        Assert.Empty(Run(rule, Of(source, Metallic(5, 128))));
        Assert.Contains("6%", Assert.Single(Run(rule, Of(source, Metallic(6, 128)))).Message);
    }

    [Fact]
    public void Metallic_endpoints_point_one_and_point_nine_are_not_binary_and_just_beyond_them_is()
    {
        var rule = new MetallicNotBinaryRule();
        CheckSet Floats(float v) => Of(CheckSource.Graph, new CheckOutput("metallic", 10, 10, 1, [.. Enumerable.Repeat(v, 100)]));
        Assert.Single(Run(rule, Floats(0.1f)));
        Assert.Single(Run(rule, Floats(0.9f)));
        Assert.Empty(Run(rule, Floats(0.099f)));
        Assert.Empty(Run(rule, Floats(0.901f)));
        // As a PNG: 26/255 = 0.102 is between and 25/255 = 0.098 is not; 229/255 = 0.898 is and 230/255 = 0.902 is not.
        Assert.Single(Run(rule, Of(CheckSource.Png, Metallic(100, 26))));
        Assert.Empty(Run(rule, Of(CheckSource.Png, Metallic(100, 25))));
        Assert.Single(Run(rule, Of(CheckSource.Png, Metallic(100, 229))));
        Assert.Empty(Run(rule, Of(CheckSource.Png, Metallic(100, 230))));
    }

    // ---- normal-not-unit: 1000 texels, so one texel is 0.1%; 0.02 for a graph and 0.03 for a PNG.

    private static CheckOutput Normals(int off, float length) =>
        new("normal", 40, 25, 3, [.. Enumerable.Range(0, 1000).SelectMany(i => new[] { 0f, 0f, i < off ? length : 1f })]);

    [Theory]
    [MemberData(nameof(Sources))]
    public void Normal_one_texel_in_a_thousand_is_clean_and_two_are_a_fault(CheckSource source)
    {
        var rule = new NormalNotUnitRule();
        Assert.Empty(Run(rule, Of(source, Normals(1, 1.1f))));
        Assert.Contains("0.2%", Assert.Single(Run(rule, Of(source, Normals(2, 1.1f)))).Message);
    }

    [Fact]
    public void Normal_tolerance_is_point_zero_two_for_a_graph_and_point_zero_three_for_a_png()
    {
        var rule = new NormalNotUnitRule();
        Assert.Empty(Run(rule, Of(CheckSource.Graph, Normals(1000, 1.019f))));
        Assert.Single(Run(rule, Of(CheckSource.Graph, Normals(1000, 1.025f))));
        Assert.Empty(Run(rule, Of(CheckSource.Png, Normals(1000, 1.029f))));
        Assert.Single(Run(rule, Of(CheckSource.Png, Normals(1000, 1.035f))));
    }

    // ---- one-value

    [Theory]
    [MemberData(nameof(Sources))]
    public void One_value_is_a_fault_for_one_byte_and_clean_for_two(CheckSource source)
    {
        var rule = new OneValueRule();
        var flat = Assert.Single(Run(rule, Of(source, Image("wear", 8, 8, (_, _) => G(255)))));
        Assert.Contains("1.000 (255)", flat.Message);
        Assert.Empty(Run(rule, Of(source, Image("wear", 8, 8, (x, y) => G(x + y == 0 ? 254 : 255)))));
        Assert.Single(Run(rule, Of(source, Image("occlusion", 8, 8, (_, _) => G(7)))));
    }

    [Fact]
    public void One_value_reads_bytes_so_two_floats_that_pack_to_one_byte_are_one_value()
    {
        var o = new CheckOutput("wear", 2, 2, 1, [128f / 255, 128.2f / 255, 127.9f / 255, 128f / 255]);
        Assert.Single(Run(new OneValueRule(), Of(CheckSource.Graph, o)));
    }

    /// <summary>A packed image of <paramref name="w"/> x <paramref name="h"/>: G from <paramref name="g"/>, B from <paramref name="b"/>, R 0.</summary>
    private static byte[] Packed(int w, int h, Func<int, int, int> g, Func<int, int, int> b)
    {
        var rgba = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                rgba[i + 1] = (byte)g(x, y); rgba[i + 2] = (byte)b(x, y); rgba[i + 3] = 255;
            }
        return rgba;
    }

    [Fact]
    public void A_metallic_roughness_pngs_blue_channel_is_read_as_metallic_and_is_exempt_from_one_value()
    {
        // G varies (the roughness); B is 0 everywhere (a constant metallic).
        var rgba = Packed(8, 8, (x, y) => 100 + y * 8 + x, (_, _) => 0);
        var set = CheckSet.OfPngs([(CheckSet.PackedRole, 8, 8, rgba)]);
        Assert.Equal(["roughness", "metallic"], set.Outputs.Select(o => o.Name));
        Assert.DoesNotContain(CheckRules.Run(set), p => p.Check == OneValueRule.Id);
        Assert.Contains("metallic read from metallic_roughness's blue channel", set.Notes);
        // A constant G is the roughness's own fault, and only its.
        var one = Assert.Single(CheckRules.Run(CheckSet.OfPngs([(CheckSet.PackedRole, 8, 8, Packed(8, 8, (_, _) => 128, (_, _) => 0))])), p => p.Check == OneValueRule.Id);
        Assert.Equal("roughness", one.Output);
    }

    [Fact]
    public void A_roughness_png_is_roughness_only_and_its_blue_channel_is_never_metallic()
    {
        // RV-76: a plain greyscale roughness (R = G = B, mid grey) is not a half-metal.
        var grey = new byte[8 * 8 * 4];
        for (var i = 0; i < 64; i++) grey[i * 4] = grey[i * 4 + 1] = grey[i * 4 + 2] = (byte)(100 + 20 * ((i % 8 + i / 8) % 2)); // a checker, which tiles
        for (var i = 0; i < 64; i++) grey[i * 4 + 3] = 255;
        var set = CheckSet.OfPngs([("roughness", 8, 8, grey)]);
        Assert.Equal(["roughness"], set.Outputs.Select(o => o.Name));
        Assert.Empty(set.Notes);
        Assert.Empty(CheckRules.Run(set));
        // The same bytes as a metallic-roughness image are a half-metal.
        Assert.Contains(CheckRules.Run(CheckSet.OfPngs([(CheckSet.PackedRole, 8, 8, grey)])), p => p.Check == MetallicNotBinaryRule.Id);
    }

    [Fact]
    public void A_seam_in_a_metallic_roughness_pngs_blue_channel_is_a_seam_in_metallic()
    {
        // RV-76: the metallic read from B is exempt from one-value only. A binary B, 0 then 1 halfway, steps at its edge
        // inside as it does at the wrap, so it tiles; a ramp of B steps 93 back at the wrap, a seam.
        var tiles = CheckSet.OfPngs([(CheckSet.PackedRole, 32, 32, Packed(32, 32, (_, _) => 128, (x, _) => x < 16 ? 0 : 255))]);
        Assert.DoesNotContain(CheckRules.Run(tiles), p => p.Check == DoesNotTileRule.Id);
        var seam = CheckSet.OfPngs([(CheckSet.PackedRole, 32, 32, Packed(32, 32, (x, y) => 100 + (x + y) % 2, (x, _) => 3 * x))]);
        var problem = Assert.Single(CheckRules.Run(seam), p => p.Check == DoesNotTileRule.Id);
        Assert.Equal("metallic", problem.Output);
    }

    [Fact]
    public void A_graphs_constant_metallic_is_exempt_from_one_value_as_a_pngs_is()
    {
        // RV-76, the person's ruling: bare steel (metallic 1 everywhere) and an all-non-metal material are legitimate.
        foreach (var level in new[] { 0, 255 })
            Assert.Empty(Run(new OneValueRule(), Of(CheckSource.Graph, Image("metallic", 8, 8, (_, _) => G(level)))));
        // metallic-not-binary still judges a constant half-metal.
        Assert.Single(Run(new MetallicNotBinaryRule(), Of(CheckSource.Graph, Image("metallic", 8, 8, (_, _) => G(128)))));
        // A PNG of role metallic is exempt too.
        Assert.Empty(Run(new OneValueRule(), Of(CheckSource.Png, Image("metallic", 8, 8, (_, _) => G(255)))));
    }

    // ---- does-not-tile: 32 x 32. The wrap is compared with the 99th percentile of the interior boundaries.

    private static CheckOutput Columns(Func<int, int> level) => Image("mask", 32, 32, (x, _) => G(level(x)));

    private static CheckOutput Rows(Func<int, int> level) => Image("mask", 32, 32, (_, y) => G(level(y)));

    /// <summary>A ramp of step s over k columns and then flat: interior steps s, wrap step s * k. A seam when s * k is over 2 * s + 2.</summary>
    private static CheckOutput Ramp(int s, int k) => Columns(x => 100 + s * Math.Min(x, k));

    private static int Bands(int i) => i / 8 % 2 == 0 ? 200 : 50;

    private static int Noise(int x, int y) => (int)((uint)(x * 73856093 ^ y * 19349663) % 21) - 10;

    [Theory]
    [MemberData(nameof(Sources))]
    public void Wrap_step_at_twice_the_percentile_plus_two_passes_and_one_level_more_is_a_seam(CheckSource source)
    {
        var rule = new DoesNotTileRule();
        Assert.Empty(Run(rule, Of(source, Ramp(2, 3)))); // wrap 6 against 2 * 2 + 2 = 6
        var seam = Assert.Single(Run(rule, Of(source, Ramp(3, 3)))); // wrap 9 against 8
        Assert.Contains("seam in u (the step across the wrap is 9.0 levels; the 99th percentile of the steps inside is 3.0)", seam.Message);
        Assert.DoesNotContain("in u and v", seam.Message);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void A_hard_edge_on_the_wrap_that_tiles_passes_in_u_and_in_v(CheckSource source)
    {
        // Bands of 200 and 50, eight wide: an edge every eighth column and one at the wrap.
        Assert.Empty(Run(new DoesNotTileRule(), Of(source, Columns(Bands))));
        Assert.Empty(Run(new DoesNotTileRule(), Of(source, Rows(Bands))));
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void A_lone_seam_on_a_flat_or_a_noisy_image_is_a_fault(CheckSource source)
    {
        var rule = new DoesNotTileRule();
        // Nearly flat: one level a column, then the wrap steps 31 back.
        Assert.Contains("seam in u", Assert.Single(Run(rule, Of(source, Ramp(1, 31)))).Message);
        // Noisy: noise of +-10 and a ramp of 3 a column, so the wrap steps by about 93.
        var noisy = Image("mask", 32, 32, (x, y) => G(120 + 3 * x + Noise(x, y)));
        Assert.Contains("seam in u", Assert.Single(Run(rule, Of(source, noisy))).Message);
        // The same noise with no ramp tiles.
        Assert.Empty(Run(rule, Of(source, Image("mask", 32, 32, (x, y) => G(120 + Noise(x, y))))));
    }

    /// <summary>
    /// 256 x 32 (RV-76): a slow ramp, 0.1 level a column spread over the rows, so each interior boundary steps about 0.1
    /// and the wrap steps back about 25, a seam; with outliers, boundaries inside that step far more. Of 255 interior
    /// boundaries the 99th percentile by nearest rank is the 253rd smallest, so the two largest are dropped and the third is not.
    /// </summary>
    private static CheckOutput WideRamp(bool spike, bool step) => Image("mask", 256, 32, (x, y) =>
        G((int)Math.Floor(40 + 0.1 * x + y / 32.0) + (spike && x == 100 ? 100 : 0) + (step && x >= 200 ? 60 : 0)));

    [Theory]
    [MemberData(nameof(Sources))]
    public void The_two_largest_interior_steps_of_255_are_dropped_by_the_percentile_and_the_third_is_not(CheckSource source)
    {
        var rule = new DoesNotTileRule();
        Assert.Contains("seam in u", Assert.Single(Run(rule, Of(source, WideRamp(spike: false, step: false)))).Message);
        // A spike column steps 100 up and 100 down: two outliers, ignored, so the wrap is still a seam (the largest step would hide it).
        Assert.Contains("seam in u", Assert.Single(Run(rule, Of(source, WideRamp(spike: true, step: false)))).Message);
        // And a step of 60 that holds to the wrap: a third outlier, the percentile, and a wrap of about 85 under 2 x 60 + 2.
        Assert.Empty(Run(rule, Of(source, WideRamp(spike: true, step: true))));
    }

    [Fact]
    public void A_seam_in_v_is_found_and_named()
    {
        var seam = Assert.Single(Run(new DoesNotTileRule(), Of(CheckSource.Graph, Rows(y => 100 + 4 * y))));
        Assert.Contains("seam in v", seam.Message);
    }

    // ---- the table of broken copies: each is a wrong version of a rule, and the case that tells it from the right one.

    public sealed record Broken(string Copy, string Rule, string Case, Func<CheckSet> Set, bool Fault)
    {
        public override string ToString() => $"{Rule}: {Copy}, caught by '{Case}'";
    }

    public static readonly IReadOnlyList<Broken> BrokenCopies =
    [
        new("a share of 1% or more is a fault", BaseColourRangeRule.Id, "exactly 1% out is clean",
            () => Of(CheckSource.Graph, Colour(1, C(10, 10, 10), C(100, 100, 100))), false),
        new("a share of over 2% is a fault", BaseColourRangeRule.Id, "2% out is a fault",
            () => Of(CheckSource.Graph, Colour(2, C(10, 10, 10), C(100, 100, 100))), true),
        new("the floor is 20 exclusive", BaseColourRangeRule.Id, "all at 20 is clean",
            () => Of(CheckSource.Graph, Colour(100, C(20, 20, 20), C(20, 20, 20))), false),
        new("metal texels judged as non-metal", BaseColourRangeRule.Id, "metal at 60 is a fault",
            () => Of(CheckSource.Graph, Colour(100, C(60, 60, 60), C(60, 60, 60)), Image("metallic", 10, 10, (_, _) => G(255))), true),
        new("non-metal texels judged as metal", BaseColourRangeRule.Id, "non-metal at 60 is clean",
            () => Of(CheckSource.Graph, Colour(100, C(60, 60, 60), C(60, 60, 60)), Image("metallic", 10, 10, (_, _) => G(0))), false),
        new("the band's ends excluded", MetallicNotBinaryRule.Id, "all at 0.1 is a fault",
            () => Of(CheckSource.Graph, new CheckOutput("metallic", 10, 10, 1, [.. Enumerable.Repeat(0.1f, 100)])), true),
        new("a share of 5% or more is a fault", MetallicNotBinaryRule.Id, "exactly 5% is clean",
            () => Of(CheckSource.Graph, Metallic(5, 128)), false),
        new("the PNG tolerance used for a graph", NormalNotUnitRule.Id, "a graph's 1.025 is a fault",
            () => Of(CheckSource.Graph, Normals(1000, 1.025f)), true),
        new("the graph tolerance used for a PNG", NormalNotUnitRule.Id, "a PNG's 1.025 is clean",
            () => Of(CheckSource.Png, Normals(1000, 1.025f)), false),
        new("floats compared, not bytes", OneValueRule.Id, "floats within one byte are one value",
            () => Of(CheckSource.Graph, new CheckOutput("wear", 2, 2, 1, [128f / 255, 128.2f / 255, 127.9f / 255, 128f / 255])), true),
        new("the wrap not compared", DoesNotTileRule.Id, "a ramp's wrap is a seam",
            () => Of(CheckSource.Graph, Ramp(3, 8)), true),
        new("the wrap compared with the mean step", DoesNotTileRule.Id, "an edge on the wrap with tiling bands is clean",
            () => Of(CheckSource.Graph, Columns(Bands)), false),
        new("the wrap compared with the step alone, without 2 x and 2 levels", DoesNotTileRule.Id, "a wrap of 6 against a step of 2 is clean",
            () => Of(CheckSource.Graph, Ramp(2, 3)), false),
        new("the wrap compared with the largest step inside", DoesNotTileRule.Id, "a wrap of 25 beside two lone steps of 100 is a seam",
            () => Of(CheckSource.Graph, WideRamp(spike: true, step: false)), true),
        new("the percentile one rank too high", DoesNotTileRule.Id, "the second-largest step of 255 is dropped",
            () => Of(CheckSource.Graph, WideRamp(spike: true, step: false)), true),
        new("the percentile one rank too low", DoesNotTileRule.Id, "the third-largest step of 255 is kept",
            () => Of(CheckSource.Graph, WideRamp(spike: true, step: true)), false),
        new("only u compared", DoesNotTileRule.Id, "a seam in v is a fault",
            () => Of(CheckSource.Graph, Rows(y => 100 + 4 * y)), true),
    ];

    public static TheoryData<int> BrokenIndexes() => [.. Enumerable.Range(0, BrokenCopies.Count)];

    [Theory]
    [MemberData(nameof(BrokenIndexes))]
    public void The_rule_gives_the_right_answer_where_a_broken_copy_gives_the_wrong_one(int index)
    {
        var broken = BrokenCopies[index];
        var rule = CheckRules.All.Single(r => r.Name == broken.Rule);
        Assert.True(rule.Run(broken.Set()).Any() == broken.Fault, broken.ToString());
    }

    [Fact]
    public void Every_rule_is_in_the_one_list_and_has_a_broken_copy_in_the_table()
    {
        Assert.Equal(CheckRules.All.Select(r => r.Name).Order(), BrokenCopies.Select(b => b.Rule).Distinct().Order());
    }
}
