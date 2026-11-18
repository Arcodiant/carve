namespace Patina.Server.Tests;

/// <summary>The snapshot stack: push, edit, pop restores byte for byte; pop on empty; open and define_graph clear it.</summary>
[Trait("Tier", "unit")]
public class SnapshotTests
{
    private const string Flat = """{"graph":{"version":1,"size":64,"seed":5,"nodes":{"c":{"type":"colour","params":{"colour":"#102030"}},"out":{"type":"output","inputs":{"in":"c"}}}}}""";

    private const string Recolour = """{"operations":[{"op":"set_params","node":"c","params":{"colour":"#ffffff"}},{"op":"set","size":128}]}""";

    [Fact]
    public async Task Pop_restores_each_pushed_graph_byte_for_byte_in_turn_and_keeps_the_path()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Flat);
        await patina.Ok("save", """{"path":"flat"}""");
        var first = await patina.GraphText();

        Assert.Equal("push_snapshot: pushed a snapshot with no label; depth 1", await patina.Ok("push_snapshot", "{}"));
        await patina.Ok("edit_graph", Recolour);
        var second = await patina.GraphText();
        Assert.NotEqual(first, second);
        Assert.Equal("push_snapshot: pushed 'white'; depth 2", await patina.Ok("push_snapshot", """{"label":"white"}"""));
        await patina.Ok("edit_graph", """{"operations":[{"op":"set","seed":9}]}""");

        Assert.Equal("pop_snapshot: restored 'white'; depth 1. The graph is 2 nodes, size 128, seed 5; no faults: it evaluates", await patina.Ok("pop_snapshot", "{}"));
        Assert.Equal(second, await patina.GraphText());
        Assert.StartsWith("pop_snapshot: restored a snapshot with no label; depth 0.", await patina.Ok("pop_snapshot", "{}"));
        Assert.Equal(first, await patina.GraphText());
        Assert.Contains($"; path {Path.Combine(folder.Path, "flat.texgraph")}; ", await patina.Ok("get_graph", "{}"));
        Assert.Equal(PopSnapshotTool.Empty, await patina.Refused("pop_snapshot", "{}"));
        Assert.Equal(first, await patina.GraphText());
    }

    [Fact]
    public async Task Define_graph_clears_the_stack()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", Flat);
        await patina.Ok("push_snapshot", "{}");
        await patina.Ok("push_snapshot", "{}");

        await patina.Ok("define_graph", Flat);

        Assert.Equal(0, patina.Store.Depth);
        Assert.Equal(PopSnapshotTool.Empty, await patina.Refused("pop_snapshot", "{}"));
    }

    [Fact]
    public async Task Open_clears_the_stack_and_a_refused_open_does_not()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Flat);
        await patina.Ok("save", """{"path":"flat"}""");
        await patina.Ok("push_snapshot", "{}");

        await patina.Refused("open", """{"path":"missing"}""");
        Assert.Equal(1, patina.Store.Depth);

        await patina.Ok("open", """{"path":"flat"}""");
        Assert.Equal(0, patina.Store.Depth);
        Assert.Equal(PopSnapshotTool.Empty, await patina.Refused("pop_snapshot", "{}"));
    }

    [Fact]
    public async Task A_refused_edit_or_save_leaves_the_stack_as_it_was()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", Flat);
        await patina.Ok("push_snapshot", """{"label":"a"}""");

        await patina.Refused("edit_graph", """{"operations":[{"op":"remove_node","node":"c"}]}""");
        await patina.Refused("save", """{"path":""}""");

        Assert.Equal(1, patina.Store.Depth);
        Assert.StartsWith("pop_snapshot: restored 'a'; depth 0.", await patina.Ok("pop_snapshot", "{}"));
    }
}
