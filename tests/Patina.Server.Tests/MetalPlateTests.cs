using System.Numerics;
using System.Text.RegularExpressions;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using StbImageSharp;
using Xunit;

namespace Patina.Server.Tests;

/// <summary>T-134: the metal plate's worked examples in define_graph's, export's and swatch's descriptions, run as written, and the sheet's columns for a graph with every role and two named outputs.</summary>
[Trait("Tier", "unit")]
public class MetalPlateTests
{
    private static async Task<(byte[] Png, string Caption)> ImageAsync(PatinaHarness patina, string arguments)
    {
        var result = await patina.CallToolAsync("swatch", arguments);
        if (Results.IsError(result)) Assert.Fail($"swatch {arguments} failed: {Results.TextOf(result)}");
        var content = result.GetProperty("content");
        return (Convert.FromBase64String(content[0].GetProperty("data").GetString()!), content[1].GetProperty("text").GetString()!);
    }

    [Fact]
    public async Task Define_graph_takes_the_metal_plate_and_export_writes_its_three_files()
    {
        Assert.Contains("define_graph " + DefineGraphTool.WorkedMetal + "\nreturns: " + DefineGraphTool.WorkedMetalResult, DefineGraphTool.Text(NodeRegistry.BuiltIn));
        Assert.Contains("export " + ExportTool.WorkedMetal + "\nreturns:\n" + ExportTool.WorkedMetalResult, ExportTool.Text);
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        Assert.Equal(DefineGraphTool.WorkedMetalResult, await patina.Ok("define_graph", DefineGraphTool.WorkedMetal));

        var result = folder.Normalise(await patina.Ok("export", ExportTool.WorkedMetal));
        var expected = "^" + Regex.Escape(ExportTool.WorkedMetalResult.Replace("<folder>", "<folder>/textures/plate")).Replace("<bytes>", @"\d+") + "$";
        Assert.Matches(new Regex(expected), result);

        var plate = Path.Combine(folder.Path, "textures", "plate");
        Assert.Equal(["base_colour.png", "edge.png", "roughness.png"], Directory.GetFiles(plate).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        var rough = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(plate, "roughness.png")), ColorComponents.RedGreenBlueAlpha).Data;
        // Texel 10 of row 0: metallic is the gradient, (10.5 / 64) * 255 = 41.84 -> 42; roughness 0.7 - 0.4 * 10.5 / 64 = 0.634 -> 162.
        Assert.Equal([255, 162, 42, 255], rough.AsSpan(10 * 4, 4).ToArray());
        var edge = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(plate, "edge.png")), ColorComponents.RedGreenBlueAlpha).Data;
        // edge: a triangle gradient down v, grey; row 16: 2 * (16.5 / 64) * 255 = 131.48 -> 131.
        Assert.Equal([131, 131, 131, 255], edge.AsSpan((16 * 64 + 5) * 4, 4).ToArray());

        Assert.Equal("export: metallic has no file of its own: it is packed into roughness.png's blue channel, so name roughness to write it",
            await patina.Refused("export", """{"folder":"t","outputs":["metallic"]}"""));
    }

    [Fact]
    public async Task Swatch_shows_the_named_output_flat_as_the_description_says()
    {
        Assert.Contains("swatch " + SwatchTool.WorkedEdge + " returns an image and:\n" + SwatchTool.WorkedEdgeResult, SwatchTool.Text);
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.WorkedMetal);
        var (png, caption) = await ImageAsync(patina, SwatchTool.WorkedEdge);
        Assert.Equal(SwatchTool.WorkedEdgeResult, caption);
        var graph = TexGraphFile.Read(await patina.GraphText()).Graph!;
        Assert.Equal(Png.Encode(OutputImage.Rgba8(new GraphEvaluator(graph), "edge", 64), 64, 64), png);
        var (_, metallic) = await ImageAsync(patina, """{"view":"flat","output":"metallic"}""");
        Assert.Equal("swatch flat: metallic of the open graph, 64 x 64 px, exact texels", metallic);
    }

    /// <summary>A graph with every role and two named outputs, each a constant so every cell's texel is known.</summary>
    private static TextureGraph EveryOutput()
    {
        string Const(string id, double v) =>
            $$""" "{{id}}_g": { "type": "gradient" }, "{{id}}": { "type": "levels", "inputs": { "in": "{{id}}_g" }, "params": { "out_low": {{v}}, "out_high": {{v}} } }, """;
        string Out(string id, string from, string param) => $$""" "o_{{id}}": { "type": "output", "inputs": { "in": "{{from}}" }, "params": { {{param}} } }, """;
        var nodes = """ "base": { "type": "colour", "params": { "colour": "#c86432" } }, "flat": { "type": "colour", "params": { "colour": "#8080ff" } }, "tint": { "type": "colour", "params": { "colour": "#204060" } }, """
            + Const("r", 0.6) + Const("m", 0.4) + Const("a", 0.8) + Const("h", 0.2) + Const("w", 1.0)
            + Out("base", "base", "\"role\": \"base_colour\"") + Out("n", "flat", "\"role\": \"normal\"") + Out("r", "r", "\"role\": \"roughness\"")
            + Out("m", "m", "\"role\": \"metallic\"") + Out("a", "a", "\"role\": \"occlusion\"") + Out("h", "h", "\"role\": \"height\"")
            + Out("wear", "w", "\"name\": \"wear\"") + Out("dirt", "tint", "\"name\": \"dirt\"");
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": 64, "seed": 0, "nodes": { {{nodes.TrimEnd().TrimEnd(',')}} } }""");
        Assert.Null(read.Error);
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    [Fact]
    public void The_sheet_has_a_column_per_output_roles_in_order_then_named_a_to_z_each_cell_worked_from_its_texels()
    {
        var evaluator = new GraphEvaluator(EveryOutput());
        var columns = evaluator.Outputs;
        Assert.Equal(["base_colour", "normal", "roughness", "metallic", "occlusion", "height", "dirt", "wear"], columns);
        var labels = SwatchSheet.Labels("unsaved graph   64 px   seed 0", columns, 0);
        Assert.Equal("dirt 3 x 3", labels[14]);
        Assert.Equal("wear", labels[15]);

        var images = columns.Select(c => OutputImage.Rgba8(evaluator, c, 64)).ToList();
        // Each column's texel, as the sheet shows it: roughness alone (B 0), metallic, height and the greyscale named output grey.
        byte[][] texel =
        [
            [0xc8, 0x64, 0x32, 255], [192, 192, 255, 255], [255, 153, 0, 255], [102, 102, 102, 255],
            [204, 204, 204, 255], [51, 51, 51, 255], [0x20, 0x40, 0x60, 255], [255, 255, 255, 255],
        ];
        for (var c = 0; c < columns.Count; c++) Assert.Equal(texel[c], images[c].AsSpan((33 * 64 + 17) * 4, 4).ToArray());

        var sphere = new byte[512 * 512 * 4];
        Array.Fill(sphere, (byte)7);
        var atlas = new byte[LabelAtlas.Size * LabelAtlas.Size * 4];
        var extents = labels.Select(_ => new Vector2(40, 12)).ToList();
        var (pixels, layout) = SwatchSheet.Compose(images, 64, sphere, atlas, extents);
        Assert.Equal((8 * 258 + 512, 538), (layout.Width, layout.Height));
        byte[] At(int x, int y) => pixels.AsSpan((y * layout.Width + x) * 4, 4).ToArray();
        for (var c = 0; c < columns.Count; c++)
        {
            var left = c * 258;
            Assert.Equal(texel[c], At(left + 200, 24 + 100));  // flat
            Assert.Equal(texel[c], At(left + 120, 282 + 200)); // tiled
            Assert.Equal(SwatchImages.Slate, At(left + 257, 300));
        }
        Assert.Equal(8 * 258, layout.SphereLeft);
        Assert.Equal(sphere.AsSpan(0, 4).ToArray(), At(layout.SphereLeft + 300, 25 + 300));
    }
}
