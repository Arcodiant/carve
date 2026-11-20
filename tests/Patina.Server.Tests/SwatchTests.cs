using System.Numerics;
using System.Text.Json;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Server.Tests;

/// <summary>
/// swatch (T-114) with no engine: flat and tiled are exact texels, every refusal says what to do, and the sheet's CPU half
/// lays out as agreed. The sphere and the sheet as drawn are the scenario's (gpu).
/// </summary>
[Trait("Tier", "unit")]
public class SwatchTests
{
    private static readonly string Planks = File.ReadAllText(Path.Combine(Paths.RepoRoot, "samples", "graphs", "planks.texgraph"));

    private static async Task<PatinaHarness> WithPlanksAsync(TempFolder folder)
    {
        File.WriteAllText(Path.Combine(folder.Path, "planks.texgraph"), Planks);
        var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("open", """{"path":"planks"}""");
        return patina;
    }

    private static readonly IReadOnlyList<OutputRole> PlanksRoles = [OutputRole.BaseColour, OutputRole.Normal, OutputRole.Roughness, OutputRole.Occlusion];

    private static byte[] Texels(OutputRole role, int size)
    {
        var graph = TexGraphFile.Read(Planks, NodeRegistry.BuiltIn).Graph!;
        return OutputImage.Rgba8(new GraphEvaluator(graph, NodeRegistry.BuiltIn), role, size);
    }

    /// <summary>The image's PNG bytes and the caption of a swatch result.</summary>
    private static async Task<(byte[] Png, string Caption)> ImageAsync(PatinaHarness patina, string arguments)
    {
        var result = await patina.CallToolAsync("swatch", arguments);
        if (Results.IsError(result)) Assert.Fail($"swatch {arguments} failed: {Results.TextOf(result)}");
        var content = result.GetProperty("content");
        return (Convert.FromBase64String(content[0].GetProperty("data").GetString()!), content[1].GetProperty("text").GetString()!);
    }

    [Fact]
    public async Task The_worked_example_returns_the_description_s_caption_and_the_tiled_texels()
    {
        using var folder = new TempFolder();
        await using var patina = await WithPlanksAsync(folder);
        var (png, caption) = await ImageAsync(patina, SwatchTool.Worked);
        Assert.Equal(SwatchTool.WorkedResult, caption);
        Assert.Contains(SwatchTool.Worked + " returns an image and:\n" + SwatchTool.WorkedResult, SwatchTool.Text, StringComparison.Ordinal);
        var texels = Texels(OutputRole.BaseColour, 128);
        var expected = new byte[384 * 384 * 4];
        for (var y = 0; y < 384; y++)
            for (var x = 0; x < 384; x++)
                Buffer.BlockCopy(texels, ((y % 128) * 128 + x % 128) * 4, expected, (y * 384 + x) * 4, 4);
        Assert.Equal(Png.Encode(expected, 384, 384), png);
    }

    [Fact]
    public async Task Flat_is_the_output_s_texels_at_the_size_asked_and_the_graph_s_size_by_default()
    {
        using var folder = new TempFolder();
        await using var patina = await WithPlanksAsync(folder);
        var (png, caption) = await ImageAsync(patina, """{"view":"flat","output":"normal","size":64}""");
        Assert.Equal("swatch flat: normal of planks.texgraph, 64 x 64 px, exact texels", caption);
        Assert.Equal(Png.Encode(Texels(OutputRole.Normal, 64), 64, 64), png);
        var (whole, first) = await ImageAsync(patina, """{"view":"flat"}""");
        Assert.Equal("swatch flat: base_colour of planks.texgraph, 256 x 256 px, exact texels", first);
        Assert.Equal(Png.Encode(Texels(OutputRole.BaseColour, 256), 256, 256), whole);
    }

    [Fact]
    public async Task Tiled_above_512_is_scaled_down_to_1536_nearest()
    {
        using var folder = new TempFolder();
        await using var patina = await WithPlanksAsync(folder);
        var (png, caption) = await ImageAsync(patina, """{"view":"tiled","output":"roughness","size":1024}""");
        Assert.Equal("swatch tiled: roughness of planks.texgraph, 3 x 3 of 1024 px, 1536 x 1536 px, nearest-scaled from 3072 px", caption);
        var texels = Texels(OutputRole.Roughness, 1024);
        var expected = new byte[1536 * 1536 * 4];
        for (var y = 0; y < 1536; y++)
            for (var x = 0; x < 1536; x++)
                Buffer.BlockCopy(texels, ((2 * y + 1) % 1024 * 1024 + (2 * x + 1) % 1024) * 4, expected, (y * 1536 + x) * 4, 4);
        Assert.Equal(Png.Encode(expected, 1536, 1536), png);
    }

    [Theory]
    [InlineData("""{"size":4096}""", "swatch: size 4096 is above 2048, the largest swatch draws; use export for larger PNGs")]
    [InlineData("""{"size":100}""", "swatch: size 100 is not a power of two from 32 to 2048")]
    [InlineData("""{"size":16}""", "swatch: size 16 is not a power of two from 32 to 2048")]
    [InlineData("""{"size":"big"}""", "swatch: size is a whole number, a power of two from 32 to 2048, not a string \"big\"")]
    [InlineData("""{"view":"side"}""", "swatch: view is 'side'; give sheet, flat, tiled or sphere")]
    [InlineData("""{"output":"normal"}""", "swatch: output is for flat and tiled; the sheet shows every output")]
    [InlineData("""{"view":"flat","angle":90}""", "swatch: angle turns the sphere, for sheet and sphere; flat has none")]
    [InlineData("""{"view":"sphere","angle":1e300}""", "swatch: angle 1e300 is out of range; angle is a number of degrees")]
    [InlineData("""{"view":"flat","output":"emissive"}""", "swatch: the graph has no emissive output; its outputs are base_colour, normal, roughness, occlusion")]
    [InlineData("""{"view":"flat","output":"Edge!"}""", "swatch: output is 'Edge!'; give a role (base_colour, normal, roughness, metallic, occlusion, height) or a named output's name")]
    [InlineData("""{"zoom":2}""", "swatch: unknown argument 'zoom'; swatch takes view, output, size, angle, tiling, parameters")]
    [InlineData("""{"view":"flat","tiling":3}""", "swatch: tiling repeats the sphere's maps, for sheet and sphere; flat has none")]
    [InlineData("""{"view":"sphere","tiling":0}""", "swatch: tiling 0 is out of range; give a whole number from 1 to 8")]
    [InlineData("""{"view":"sphere","tiling":9}""", "swatch: tiling 9 is out of range; give a whole number from 1 to 8")]
    [InlineData("""{"view":"sphere","tiling":2.5}""", "swatch: tiling is a whole number from 1 to 8, not a number 2.5")]
    [InlineData("""{"view":"sphere","tiling":"3"}""", "swatch: tiling is a whole number from 1 to 8, not a string \"3\"")]
    [InlineData("""{"view":"sphere"}""", "swatch: " + PatinaHarness.NoEngine)]
    [InlineData("""{"view":"sphere","tiling":3}""", "swatch: " + PatinaHarness.NoEngine)]
    [InlineData("""{}""", "swatch: " + PatinaHarness.NoEngine)]
    public async Task Refusals_say_what_to_do(string arguments, string said)
    {
        using var folder = new TempFolder();
        await using var patina = await WithPlanksAsync(folder);
        Assert.Equal(said, await patina.Refused("swatch", arguments));
    }

    [Fact]
    public async Task A_role_the_graph_lacks_and_a_faulty_graph_are_refused()
    {
        await using var patina = await PatinaHarness.StartAsync();
        Assert.Equal("swatch: " + ToolSupport.NoGraph, await patina.Refused("swatch", "{}"));
        await patina.Ok("define_graph", DefineGraphTool.Worked);
        Assert.Equal("swatch: the graph has no normal output; its outputs are base_colour", await patina.Refused("swatch", """{"view":"flat","output":"normal"}"""));
        await patina.Ok("define_graph", """{"graph":{"version":1,"size":64,"seed":0,"nodes":{"out":{"type":"output","inputs":{"in":"nothing"}}}}}""");
        Assert.StartsWith("swatch: the graph does not evaluate; fix it first. 1 fault:\n- node 'out' input 'in': names 'nothing'", await patina.Refused("swatch", """{"view":"flat"}"""));
    }

    [Fact]
    public void The_sheet_lays_out_as_agreed_flat_over_tiled_with_labels_from_the_atlas()
    {
        var size = 64;
        var images = PlanksRoles.Select(r => Texels(r, size)).ToList();
        var sphere = new byte[512 * 512 * 4];
        Array.Fill(sphere, (byte)7);
        // An atlas whose rows are all text (coverage 1): every label box comes out white.
        var atlas = new byte[LabelAtlas.Size * LabelAtlas.Size * 4];
        Array.Fill(atlas, (byte)255);
        var labels = SwatchSheet.Labels("planks.texgraph   64 px   seed 77", PlanksRoles, 0);
        Assert.Equal(["planks.texgraph   64 px   seed 77", "base_colour", "base_colour 3 x 3", "normal", "normal 3 x 3", "roughness", "roughness 3 x 3",
            "occlusion", "occlusion 3 x 3", "sphere, Pbr, carve lights, angle 0"], labels);
        var extents = labels.Select(_ => new Vector2(40, 12)).ToList();
        var (pixels, layout) = SwatchSheet.Compose(images, size, sphere, atlas, extents);

        Assert.Equal((4 * 258 + 512, 538), (layout.Width, layout.Height));
        byte[] At(int x, int y) => pixels.AsSpan((y * layout.Width + x) * 4, 4).ToArray();
        // Flat: texel (x * 64 / 256) under each cell pixel; tiled: the 3 x 3 repeat, 192 texels across 256 pixels.
        for (var column = 0; column < 4; column++)
        {
            var left = column * 258;
            Assert.Equal(images[column].AsSpan(((100 * 64 / 256) * 64 + 200 * 64 / 256) * 4, 4).ToArray(), At(left + 200, 24 + 100));
            var tx = (int)((2 * 200 + 1) * 192L / 512) % 64;
            var ty = (int)((2 * 100 + 1) * 192L / 512) % 64;
            Assert.Equal(images[column].AsSpan((ty * 64 + tx) * 4, 4).ToArray(), At(left + 200, 282 + 100));
            Assert.Equal(SwatchImages.Slate, At(left + 257, 300));
            Assert.Equal([255, 255, 255, images[column][3]], At(left + 3, 24 + 3));
        }
        Assert.Equal(sphere.AsSpan(0, 4).ToArray(), At(layout.SphereLeft + 300, 25 + 300));
        Assert.Equal([255, 255, 255, 255], At(5, 5));
        Assert.Equal(SwatchImages.Slate, At(1000, 10));
    }

    [Fact]
    public void The_tiling_is_described_with_worked_examples_the_scenario_draws()
    {
        // The sphere needs an engine, so the drawn example is the scenario's (literals there); the refusal runs in the harness.
        Assert.Equal("""{"view":"sphere","tiling":3}""", SwatchTool.WorkedTiling);
        Assert.Contains("swatch " + SwatchTool.WorkedTiling + " returns an image and:\n" + SwatchTool.WorkedTilingResult, SwatchTool.Text, StringComparison.Ordinal);
        Assert.Contains("swatch " + SwatchTool.WorkedTilingRefused + " returns:\n" + SwatchTool.WorkedTilingRefusedResult, SwatchTool.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_worked_tiling_refusal_is_what_the_description_says()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.Worked);
        Assert.Equal(SwatchTool.WorkedTilingRefusedResult, await patina.Refused("swatch", SwatchTool.WorkedTilingRefused));
    }
}
