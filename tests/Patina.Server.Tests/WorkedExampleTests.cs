namespace Patina.Server.Tests;

/// <summary>Every worked example in every description, run as written, with its result held to the description's text.</summary>
[Trait("Tier", "unit")]
public class WorkedExampleTests
{
    /// <summary>What a description says follows <paramref name="marker"/>, up to the end of the description.</summary>
    private static string After(string description, string marker) => description[(description.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];

    [Fact]
    public async Task Define_graph_takes_the_tone_per_stave_and_names_the_misspelt_input()
    {
        await using var patina = await PatinaHarness.StartAsync();

        Assert.Equal(DefineGraphTool.WorkedResult, await patina.Ok("define_graph", DefineGraphTool.Worked));
        Assert.Equal(DefineGraphTool.WorkedFaultyResult, await patina.Ok("define_graph", DefineGraphTool.WorkedFaulty));
    }

    [Fact]
    public async Task Define_graph_takes_the_brick_layout_as_written()
    {
        await using var patina = await PatinaHarness.StartAsync();

        Assert.Equal(DefineGraphTool.WorkedTilesResult, await patina.Ok("define_graph", DefineGraphTool.WorkedTiles));
        Assert.Contains("define_graph " + DefineGraphTool.WorkedTiles + "\nreturns: " + DefineGraphTool.WorkedTilesResult, NodeCatalogue.Text(Patina.Graphs.Nodes.NodeRegistry.BuiltIn), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Define_graph_takes_the_turned_stripes_and_says_they_do_not_tile()
    {
        await using var patina = await PatinaHarness.StartAsync();

        Assert.Equal(DefineGraphTool.WorkedTurnResult, await patina.Ok("define_graph", DefineGraphTool.WorkedTurn));
        var text = NodeCatalogue.Text(Patina.Graphs.Nodes.NodeRegistry.BuiltIn);
        Assert.Contains("define_graph " + DefineGraphTool.WorkedTurn + "\nreturns: " + DefineGraphTool.WorkedTurnResult, text, StringComparison.Ordinal);
        Assert.Contains("a turn that is not a quarter turn does not tile", text, StringComparison.Ordinal);
        Assert.Contains("- transform: ", text, StringComparison.Ordinal);
        Assert.Contains("- distance: ", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Define_graph_takes_the_tone_per_stone_from_worleys_random_output()
    {
        await using var patina = await PatinaHarness.StartAsync();

        Assert.Equal(DefineGraphTool.WorkedResult, await patina.Ok("define_graph", DefineGraphTool.WorkedStones));
        Assert.Contains("define_graph " + DefineGraphTool.WorkedStones + "\nreturns: " + DefineGraphTool.WorkedResult, NodeCatalogue.Text(Patina.Graphs.Nodes.NodeRegistry.BuiltIn));
    }

    [Fact]
    public async Task Define_graph_takes_the_blur_and_gradient_map_example_as_written()
    {
        await using var patina = await PatinaHarness.StartAsync();

        Assert.Equal(DefineGraphTool.WorkedMapResult, await patina.Ok("define_graph", DefineGraphTool.WorkedMap));
        Assert.Contains("define_graph " + DefineGraphTool.WorkedMap + "\nreturns: " + DefineGraphTool.WorkedMapResult, NodeCatalogue.Text(Patina.Graphs.Nodes.NodeRegistry.BuiltIn));
    }

    [Fact]
    public async Task Get_graph_after_define_graph_returns_the_description_s_text()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.Worked);

        Assert.Equal(After(GetGraphTool.Text, "get_graph {} returns:\n"), await patina.Ok("get_graph", "{}"));
    }

    [Fact]
    public async Task Edit_graph_adds_the_grain_then_refuses_removing_tone()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.Worked);

        Assert.Equal(EditGraphTool.WorkedResult, await patina.Ok("edit_graph", EditGraphTool.Worked));
        var before = await patina.GraphText();
        Assert.Equal(EditGraphTool.WorkedRefusedResult, await patina.Refused("edit_graph", EditGraphTool.WorkedRefused));
        Assert.Equal(before, await patina.GraphText());
    }

    [Fact]
    public async Task Edit_graph_s_other_examples_do_what_the_description_says()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.Worked);
        await patina.Ok("edit_graph", EditGraphTool.Worked);

        Assert.Contains("set 'tone' seed 3.", await patina.Ok("edit_graph", $"{{\"operations\":[{EditGraphTool.SetParamsExample}]}}"));
        Assert.Contains("\"tone\": { \"type\": \"noise\", \"params\": { \"cells\": [8, 1], \"interpolation\": \"step\", \"seed\": 3 } }", await patina.GraphText());

        var disconnect = await patina.Refused("edit_graph", $"{{\"operations\":[{EditGraphTool.DisconnectExample}]}}");
        Assert.StartsWith("edit_graph: nothing changed: the operations would leave 1 new fault:\n- node 'grained' input 'b'", disconnect);

        Assert.Contains("set size 512 (was 256), seed 7 (was 0). The graph is 6 nodes, size 512, seed 7; no faults", await patina.Ok("edit_graph", $"{{\"operations\":[{EditGraphTool.SetExample}]}}"));
    }

    [Fact]
    public async Task Save_and_open_write_and_read_planks_and_list_it_for_a_misspelt_name()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", DefineGraphTool.Worked);
        await patina.Ok("edit_graph", EditGraphTool.Worked);
        var saved = await patina.GraphText();

        Assert.Equal(SaveTool.WorkedResult, folder.Normalise(await patina.Ok("save", SaveTool.Worked)));
        Assert.Equal(saved + "\n", File.ReadAllText(Path.Combine(folder.Path, "planks.texgraph")));

        await patina.Ok("define_graph", DefineGraphTool.Worked);
        Assert.Equal(OpenTool.WorkedResult, folder.Normalise(await patina.Ok("open", OpenTool.Worked)));
        Assert.Equal(saved, await patina.GraphText());
        Assert.Equal(OpenTool.WorkedMissingResult, folder.Normalise(await patina.Refused("open", OpenTool.WorkedMissing)));
    }

    [Fact]
    public async Task Push_edit_pop_comes_back_to_before_the_grain_and_a_second_pop_is_refused()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.Worked);
        var before = await patina.GraphText();

        Assert.Equal(PushSnapshotTool.WorkedResult, await patina.Ok("push_snapshot", PushSnapshotTool.Worked));
        await patina.Ok("edit_graph", EditGraphTool.Worked);
        Assert.Equal(PopSnapshotTool.WorkedResult, await patina.Ok("pop_snapshot", "{}"));
        Assert.Equal(before, await patina.GraphText());
        Assert.Equal(PopSnapshotTool.Empty, await patina.Refused("pop_snapshot", "{}"));
    }

    [Fact]
    public void Every_description_has_a_worked_example()
    {
        foreach (var text in new[] { DefineGraphTool.Text(Patina.Graphs.Nodes.NodeRegistry.BuiltIn), EditGraphTool.Text, GetGraphTool.Text, SaveTool.Text, OpenTool.Text, PushSnapshotTool.Text, PopSnapshotTool.Text, ExportTool.Text, CheckTool.Text, NodeTool.Description(Patina.Graphs.Nodes.NodeRegistry.BuiltIn) })
            Assert.Contains("Worked example", text);
    }

    [Fact]
    public void Nodes_lists_every_registered_type_with_its_ports_and_parameters()
    {
        var registry = Patina.Graphs.Nodes.NodeRegistry.BuiltIn;
        var text = NodeCatalogue.Text(registry);
        foreach (var name in registry.Names)
        {
            var type = registry.Find(name)!;
            Assert.Contains($"- {name}", text);
            foreach (var p in type.Parameters) Assert.Contains($"{p.Name}: {p.Describe()}, " + (p.Optional ? "optional, no default" : $"default {p.Default}"), text);
        }
    }
}
