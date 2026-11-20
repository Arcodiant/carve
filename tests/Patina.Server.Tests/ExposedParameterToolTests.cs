using System.Text.Json;
using Patina.Graphs;
using Xunit;

namespace Patina.Server.Tests;

/// <summary>
/// T-139 through the tools, on samples/graphs/bricks.texgraph (mortar, tint and jitter exposed): edit_graph's
/// set_parameter, remove_parameter and set_parameters; get_graph's list and driven form; swatch's and export's overrides,
/// each equal byte for byte to the same values set in the graph. Each description's worked example is run as written.
/// </summary>
[Trait("Tier", "unit")]
public class ExposedParameterToolTests
{
    private static string Bricks => Path.Combine(Paths.RepoRoot, "samples", "graphs", "bricks.texgraph");

    private static async Task OpenBricksAsync(PatinaHarness patina) =>
        await patina.Ok("open", JsonSerializer.Serialize(new { path = Bricks }));

    private static async Task<(byte[] Png, string Caption)> ImageAsync(PatinaHarness patina, string arguments)
    {
        var result = await patina.CallToolAsync("swatch", arguments);
        if (Results.IsError(result)) Assert.Fail(Results.TextOf(result));
        var content = result.GetProperty("content");
        return (Convert.FromBase64String(content[0].GetProperty("data").GetString()!), content[1].GetProperty("text").GetString()!);
    }

    [Fact]
    public async Task Edit_graph_sets_adds_and_removes_parameters_as_its_description_says()
    {
        Assert.Contains("edit_graph " + EditGraphTool.WorkedParameters + "\nreturns: " + EditGraphTool.WorkedParametersResult, EditGraphTool.Text);
        await using var patina = await PatinaHarness.StartAsync();
        await OpenBricksAsync(patina);
        Assert.Equal(EditGraphTool.WorkedParametersResult, await patina.Ok("edit_graph", EditGraphTool.WorkedParameters));

        Assert.Equal("edit_graph: 1 operation applied: added parameter 'variation' value 0.7, drives tone.out_low, max 0.9. The graph is 18 nodes, size 256, seed 21; no faults: it evaluates",
            await patina.Ok("edit_graph", $$"""{"operations":[{{EditGraphTool.SetParameterExample}}]}"""));
        var text = await patina.GraphText();
        Assert.Contains("\"tone\": { \"type\": \"levels\", \"inputs\": { \"in\": \"layout.random\" }, \"params\": { \"out_high\": 1.1, \"out_low\": { \"driven_by\": \"variation\" } } }", text);
        Assert.Contains("- variation: a number, 0.7, from 0 to 0.9; drives tone.out_low", await patina.Ok("get_graph", "{}"));

        Assert.Equal("edit_graph: 1 operation applied: removed parameter 'jitter'; layout.jitter keeps 0.25. The graph is 18 nodes, size 256, seed 21; no faults: it evaluates",
            await patina.Ok("edit_graph", $$"""{"operations":[{{EditGraphTool.RemoveParameterExample}}]}"""));
        Assert.Contains("\"jitter\": 0.25", await patina.GraphText());

        // Out of range is a new fault, so nothing changes; an unknown parameter is refused by operation.
        Assert.Equal("edit_graph: nothing changed: the operations would leave 1 new fault:\n- parameters['mortar']: is 0.5, but must be a number from 0 to 0.45",
            await patina.Refused("edit_graph", """{"operations":[{"op":"set_parameters","values":{"mortar":0.5}}]}"""));
        Assert.Equal("edit_graph: nothing changed: operation 2 (set_parameters): there is no parameter 'wear'; the parameters are mortar, tint, variation",
            await patina.Refused("edit_graph", """{"operations":[{"op":"set_parameters","values":{"mortar":0.06}},{"op":"set_parameters","values":{"wear":1}}]}"""));
        Assert.Contains("\"mortar\": { \"value\": 0.08,", await patina.GraphText());
    }

    [Fact]
    public async Task Remove_parameter_keeps_the_value_in_the_nodes_it_drove()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await OpenBricksAsync(patina);
        await patina.Ok("edit_graph", """{"operations":[{"op":"set_parameters","values":{"mortar":0.08}},{"op":"remove_parameter","name":"mortar"}]}""");
        var graph = TexGraphFile.Read(await patina.GraphText()).Graph!;
        Assert.Equal(ParamValue.Of(0.08), graph.Nodes["layout"].Params["gap"]);
        Assert.False(graph.Parameters.ContainsKey("mortar"));
    }

    [Fact]
    public async Task Get_graph_lists_the_parameters_and_shows_what_is_driven()
    {
        Assert.Contains(GetGraphTool.WorkedBricksParameters, GetGraphTool.Text);
        await using var patina = await PatinaHarness.StartAsync();
        await OpenBricksAsync(patina);
        var text = await patina.Ok("get_graph", "{}");
        var lines = text.Split('\n');
        Assert.Equal(GetGraphTool.WorkedBricksParameters, string.Join("\n", lines[1..5]));
        Assert.Contains("\"layout\": { \"type\": \"tiles\", \"params\": { \"bevel\": 0.1, \"count\": [4, 8], \"gap\": { \"driven_by\": \"mortar\" }, \"jitter\": { \"driven_by\": \"jitter\" }, \"stagger\": 0.5 } }", text);
        // define_graph takes get_graph's form back, giving the same file.
        var shown = await patina.GraphText();
        await patina.Ok("define_graph", JsonSerializer.Serialize(new { graph = JsonDocument.Parse(shown).RootElement }));
        Assert.Equal(File.ReadAllText(Bricks).Replace("\r\n", "\n"), TexGraphFile.Write(TexGraphFile.Read(await patina.GraphText()).Graph!));
    }

    [Fact]
    public async Task Swatch_overrides_equal_the_same_values_set_in_the_graph()
    {
        Assert.Contains("swatch " + SwatchTool.WorkedParameters + " shows grey stone bricks' base colour and returns:\n" + SwatchTool.WorkedParametersResult, SwatchTool.Text);
        Assert.Contains("swatch " + SwatchTool.WorkedParametersRefused + " returns:\n" + SwatchTool.WorkedParametersRefusedResult, SwatchTool.Text);
        await using var patina = await PatinaHarness.StartAsync();
        await OpenBricksAsync(patina);
        var before = await patina.GraphText();
        var (overridden, caption) = await ImageAsync(patina, SwatchTool.WorkedParameters);
        Assert.Equal(SwatchTool.WorkedParametersResult, caption);
        Assert.Equal(before, await patina.GraphText()); // the graph is not changed
        Assert.Equal(SwatchTool.WorkedParametersRefusedResult, await patina.Refused("swatch", SwatchTool.WorkedParametersRefused));
        Assert.Equal("swatch: parameters['wear']: the graph has no such parameter; its parameters are jitter, mortar, tint",
            await patina.Refused("swatch", """{"view":"flat","parameters":{"wear":1}}"""));

        await patina.Ok("edit_graph", """{"operations":[{"op":"set_parameters","values":{"mortar":0.08,"tint":"#7a7a70"}}]}""");
        var (set, _) = await ImageAsync(patina, """{"view":"flat","output":"base_colour"}""");
        Assert.Equal(set, overridden);
    }

    [Fact]
    public async Task Export_overrides_equal_the_same_values_set_in_the_graph_and_the_committed_variant()
    {
        Assert.Contains("export " + ExportTool.WorkedParameters + " writes its four outputs as grey stone bricks (equal to samples/graphs/bricks/variants/grey/) and its first line is:\n" + ExportTool.WorkedParametersHead, ExportTool.Text);
        using var temp = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(temp.Path);
        await OpenBricksAsync(patina);
        var head = (await patina.Ok("export", ExportTool.WorkedParameters)).Split('\n')[0];
        var folder = Path.Combine(temp.Path, "textures", "bricks_grey");
        Assert.Equal(ExportTool.WorkedParametersHead.Replace("<folder>", folder), head);
        Assert.StartsWith("export: parameters['tint']: is \"grey\", but must be a colour \"#rrggbb\". Nothing was written.",
            await patina.Refused("export", """{"folder":"textures/never","parameters":{"tint":"grey"}}"""));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "textures", "never")));

        await patina.Ok("edit_graph", """{"operations":[{"op":"set_parameters","values":{"mortar":0.08,"tint":"#7a7a70"}}]}""");
        await patina.Ok("export", """{"folder":"textures/set"}""");
        foreach (var file in new[] { "base_colour.png", "normal.png", "roughness.png", "occlusion.png" })
        {
            var bytes = File.ReadAllBytes(Path.Combine(folder, file));
            Assert.Equal(File.ReadAllBytes(Path.Combine(temp.Path, "textures", "set", file)), bytes);
            Assert.Equal(File.ReadAllBytes(Path.Combine(Paths.RepoRoot, "samples", "graphs", "bricks", "variants", "grey", file)), bytes);
        }
    }
}
