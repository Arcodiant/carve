using Carve.Scenario.Patina;
using Patina.Graphs.Nodes;

namespace Patina.Server.Tests;

/// <summary>
/// T-140 through the tools: define_graph's worked example using bricks.texgraph as a node, get_graph's graph-node lines,
/// open naming the nested files it read, save writing a graph with an input lacking a default (and never a nested file),
/// and export of old-wall evaluating through the expansion (swatch needs the engine: the scenario's stage draws it).
/// </summary>
[Trait("Tier", "unit")]
public class NestedGraphToolTests
{
    private static string Sample(string name) => Path.Combine(Paths.RepoRoot, "samples", "graphs", name);

    private static void CopySamples(string folder, params string[] names)
    {
        foreach (var name in names) File.Copy(Sample(name), Path.Combine(folder, name));
    }

    [Fact]
    public async Task Define_graphs_worked_example_uses_bricks_as_a_node_with_the_sample_beside_it()
    {
        using var folder = new TempFolder();
        CopySamples(folder.Path, "bricks.texgraph");
        await using var patina = await PatinaHarness.StartAsync(folder.Path);

        Assert.Equal(DefineGraphTool.WorkedNestedResult, await patina.Ok("define_graph", DefineGraphTool.WorkedNested));
        Assert.Contains("define_graph " + DefineGraphTool.WorkedNested + "\nreturns: " + DefineGraphTool.WorkedNestedResult, NodeCatalogue.Text(NodeRegistry.BuiltIn), StringComparison.Ordinal);

        var graph = folder.Normalise(await patina.Ok("get_graph", "{}"));
        Assert.Contains("graph nodes (1), paths resolved against the tool folder <folder> (the graph has never been saved):\n", graph);
        Assert.Contains("- wall: <folder>/bricks.texgraph; parameters: ", graph);
        Assert.Contains("mortar a number, 0.06, ", graph);
        Assert.Contains("; inputs: none; outputs: wall.base_colour, wall.normal, wall.roughness, wall.occlusion", graph);
    }

    [Fact]
    public async Task Define_graphs_pass_through_example_takes_bricks_range_for_mortar()
    {
        using var folder = new TempFolder();
        CopySamples(folder.Path, "bricks.texgraph");
        await using var patina = await PatinaHarness.StartAsync(folder.Path);

        Assert.Equal(DefineGraphTool.WorkedNestedResult, await patina.Ok("define_graph", DefineGraphTool.WorkedPassThrough));
        Assert.Contains("define_graph " + DefineGraphTool.WorkedPassThrough + "\nreturns: " + DefineGraphTool.WorkedNestedResult, NodeCatalogue.Text(NodeRegistry.BuiltIn), StringComparison.Ordinal);
        Assert.Contains("parameters['mortar']: is 0.5, but must be a number from 0 to 0.45",
            await patina.Refused("export", """{"folder":"out","parameters":{"mortar":0.5}}"""));
        await patina.Ok("export", """{"folder":"out","parameters":{"mortar":0.06}}""");
    }

    [Fact]
    public async Task Open_names_the_nested_files_it_read_and_save_writes_only_the_open_graph()
    {
        using var folder = new TempFolder();
        CopySamples(folder.Path, "bricks.texgraph", "grime.texgraph", "old-wall.texgraph");
        var bricks = File.ReadAllBytes(Path.Combine(folder.Path, "bricks.texgraph"));
        var grime = File.ReadAllBytes(Path.Combine(folder.Path, "grime.texgraph"));
        await using var patina = await PatinaHarness.StartAsync(folder.Path);

        var opened = folder.Normalise(await patina.Ok("open", """{"path":"old-wall.texgraph"}"""));
        Assert.Contains("\nread 2 nested files for its graph nodes: <folder>/grime.texgraph, <folder>/bricks.texgraph", opened);

        await patina.Ok("save", """{"path":"copy.texgraph"}""");
        // By text, line endings aside: a checkout may give the sample CRLF.
        Assert.Equal(File.ReadAllText(Sample("old-wall.texgraph")).ReplaceLineEndings(), File.ReadAllText(Path.Combine(folder.Path, "copy.texgraph")).ReplaceLineEndings());
        Assert.Equal(bricks, File.ReadAllBytes(Path.Combine(folder.Path, "bricks.texgraph")));
        Assert.Equal(grime, File.ReadAllBytes(Path.Combine(folder.Path, "grime.texgraph")));
    }

    [Fact]
    public async Task Save_into_another_folder_is_judged_there_and_refused_when_a_graph_node_would_not_resolve()
    {
        // RV-76: the never-saved graph uses bricks.texgraph from the tool folder; saved into materials/ it would name
        // materials/bricks.texgraph, which does not exist, so the save is refused and nothing changes.
        using var folder = new TempFolder();
        CopySamples(folder.Path, "bricks.texgraph");
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", DefineGraphTool.WorkedNested);

        var refused = folder.Normalise(await patina.Refused("save", """{"path":"materials/wall.texgraph"}"""));
        Assert.StartsWith("save: refused: the graph saved in <folder>/materials, where its graph nodes' paths resolve, has 1 fault", refused);
        Assert.Contains("node 'wall' parameter 'path'", refused);
        Assert.Contains("materials", refused);
        Assert.EndsWith("Nothing was written.", refused);
        Assert.False(File.Exists(Path.Combine(folder.Path, "materials", "wall.texgraph")));
        Assert.Contains("paths resolved against the tool folder <folder> (the graph has never been saved)", folder.Normalise(await patina.Ok("get_graph", "{}")));
        await patina.Ok("export", """{"folder":"out","outputs":["base_colour"]}""");

        // With the nested file beside it, the same save is judged there and written, and the file opens with no fault.
        Directory.CreateDirectory(Path.Combine(folder.Path, "materials"));
        CopySamples(Path.Combine(folder.Path, "materials"), "bricks.texgraph");
        await patina.Ok("save", """{"path":"materials/wall.texgraph"}""");
        Assert.Contains("no faults", await patina.Ok("open", """{"path":"materials/wall.texgraph"}"""));
    }

    [Fact]
    public async Task Pop_snapshot_after_a_save_into_another_folder_lists_the_faults_the_snapshot_has_there()
    {
        // RV-76: a snapshot pushed while its graph node resolved against the tool folder comes back in the saved file's
        // folder, where bricks.texgraph is not, and pop_snapshot names that fault rather than reporting the graph sound.
        using var folder = new TempFolder();
        CopySamples(folder.Path, "bricks.texgraph");
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", DefineGraphTool.WorkedNested);
        await patina.Ok("push_snapshot", """{"label":"with bricks"}""");
        await patina.Ok("edit_graph", """{"operations":[{"op":"set_node","node":"c","type":"colour","params":{"colour":"#808080"}},{"op":"connect","node":"out","port":"in","from":"c"},{"op":"remove_node","node":"rough"},{"op":"remove_node","node":"wall"}]}""");
        await patina.Ok("save", """{"path":"materials/plain.texgraph"}""");

        var popped = await patina.Ok("pop_snapshot", "{}");
        Assert.StartsWith("pop_snapshot: restored 'with bricks'; depth 0. The graph is 3 nodes, size 256, seed 0; 1 fault", popped);
        Assert.Contains("node 'wall' parameter 'path'", popped);
        Assert.Contains("materials", popped);
        Assert.Contains("refused", await patina.Refused("export", """{"folder":"out"}"""));
    }

    [Fact]
    public async Task Save_writes_a_graph_whose_input_lacks_a_default()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        var defined = await patina.Ok("define_graph", """{"graph":{"version":2,"size":64,"seed":0,"nodes":{"in":{"type":"input","params":{"name":"in","kind":"colour"}},"out":{"type":"output","inputs":{"in":"in"}}}}}""");
        Assert.Contains("input 'in' has no default, so the graph cannot be evaluated on its own", defined);

        await patina.Ok("save", """{"path":"filter.texgraph"}""");
        Assert.Contains("\"type\": \"input\"", File.ReadAllText(Path.Combine(folder.Path, "filter.texgraph")));
    }

    [Fact]
    public async Task Export_of_old_wall_evaluates_through_the_expansion()
    {
        using var folder = new TempFolder();
        CopySamples(folder.Path, "bricks.texgraph", "grime.texgraph", "old-wall.texgraph");
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("open", """{"path":"old-wall.texgraph"}""");

        await patina.Ok("export", """{"folder":"out"}""");
        foreach (var role in new[] { "base_colour", "normal", "roughness", "occlusion" })
            Assert.Equal(File.ReadAllBytes(Path.Combine(Sample("old-wall"), role + ".png")), File.ReadAllBytes(Path.Combine(folder.Path, "out", role + ".png")));

    }
}
