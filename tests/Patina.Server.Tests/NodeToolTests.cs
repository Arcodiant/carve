using Patina.Graphs.Nodes;

namespace Patina.Server.Tests;

/// <summary>
/// T-170: the nodes tool, its worked examples run as written, and every description held under the length at which
/// Claude Code cuts a tool description.
/// </summary>
[Trait("Tier", "unit")]
public class NodeToolTests
{
    /// <summary>
    /// Claude Code (2.1.329) cuts each MCP tool description at 4,096 characters and appends "… [truncated]", measured by
    /// a probe session quoting the end of each description (T-170's As built).
    /// </summary>
    public const int ClientCut = 4096;

    /// <summary>The margin, approved by the person (2026-11-20): 10% under the cut, so at most 3,686 characters.</summary>
    public const int Limit = ClientCut * 9 / 10;

    [Fact]
    public async Task Every_description_is_under_the_limit()
    {
        await using var patina = await PatinaHarness.StartAsync();
        var tools = (await patina.ListToolsAsync()).EnumerateArray()
            .Select(t => (Name: t.GetProperty("name").GetString()!, Length: t.GetProperty("description").GetString()!.Length)).ToList();

        Assert.Contains(tools, t => t.Name == "nodes");
        var over = tools.Where(t => t.Length > Limit).Select(t => $"{t.Name} {t.Length}").ToList();
        Assert.True(over.Count == 0, $"over {Limit} characters (Claude Code cuts at {ClientCut}): {string.Join(", ", over)}");
    }

    [Fact]
    public void Define_graph_names_every_type_and_points_at_nodes()
    {
        var registry = NodeRegistry.BuiltIn;
        var text = DefineGraphTool.Text(registry);
        Assert.Contains("(nodes gives each one's inputs, outputs, parameters with ranges and defaults, and a worked example", text, StringComparison.Ordinal);
        foreach (var name in registry.Names) Assert.Contains(name + " (", text, StringComparison.Ordinal);
        Assert.Contains(NodeCatalogue.Index(registry), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_type_has_a_worked_example()
    {
        foreach (var name in NodeRegistry.BuiltIn.Names) Assert.NotEmpty(NodeCatalogue.Examples(name));
    }

    [Fact]
    public async Task Every_types_worked_examples_run_as_written()
    {
        using var folder = new TempFolder();
        File.Copy(Path.Combine(Paths.RepoRoot, "samples", "graphs", "bricks.texgraph"), Path.Combine(folder.Path, "bricks.texgraph"));
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        foreach (var name in NodeRegistry.BuiltIn.Names)
            foreach (var (_, graph, returns) in NodeCatalogue.Examples(name))
            {
                Assert.Equal(returns, await patina.Ok("define_graph", graph));
                Assert.Contains($"\"type\":\"{name}\"", graph, StringComparison.Ordinal);
                Assert.Contains("define_graph " + graph + "\nreturns: " + returns + "\n", NodeCatalogue.Entry(NodeRegistry.BuiltIn, name), StringComparison.Ordinal);
            }
    }

    [Fact]
    public async Task Nodes_returns_one_type_or_every_type_and_refuses_an_unknown_one()
    {
        await using var patina = await PatinaHarness.StartAsync();
        var registry = NodeRegistry.BuiltIn;

        var one = await patina.Ok("nodes", NodeTool.Worked);
        Assert.Equal(NodeCatalogue.Entry(registry, "stripes").TrimEnd('\n'), one);
        Assert.EndsWith("returns:\n" + one, NodeTool.Description(registry), StringComparison.Ordinal);

        var all = await patina.Ok("nodes", "{}");
        Assert.StartsWith($"nodes: {registry.Names.Count()} types\n", all, StringComparison.Ordinal);
        foreach (var name in registry.Names) Assert.Contains(NodeCatalogue.Entry(registry, name).TrimEnd('\n'), all, StringComparison.Ordinal);

        Assert.Equal("nodes: there is no node type 'brick'; the types are " + string.Join(", ", registry.Names), await patina.Refused("nodes", """{"type":"brick"}"""));
        Assert.Equal("nodes: unknown argument 'kind'; nodes takes type", await patina.Refused("nodes", """{"kind":"tiles"}"""));
    }
}
