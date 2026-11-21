namespace Patina.Server.Tests;

/// <summary>Each tool's refusals and rules: every fault path, all-or-nothing edits, the file rules and the snapshot stack.</summary>
[Trait("Tier", "unit")]
public class GraphToolTests
{
    /// <summary>A graph of one colour into base_colour.</summary>
    private const string Flat = """{"graph":{"version":1,"size":64,"seed":5,"nodes":{"c":{"type":"colour","params":{"colour":"#102030"}},"out":{"type":"output","inputs":{"in":"c"}}}}}""";

    /// <summary>A .texgraph file whose output reads a node that is not there (the planted bad input id).</summary>
    private const string PlantedFile = "{\n  \"version\": 1,\n  \"size\": 64,\n  \"seed\": 5,\n  \"nodes\": {\n    \"c\": { \"type\": \"colour\" },\n    \"out\": { \"type\": \"output\", \"inputs\": { \"in\": \"cc\" } }\n  }\n}\n";

    private static string Ops(params string[] operations) => "{\"operations\":[" + string.Join(",", operations) + "]}";

    [Fact]
    public async Task Tools_list_gives_the_nine_tools_in_order_each_closed_to_other_arguments()
    {
        await using var patina = await PatinaHarness.StartAsync();
        var tools = (await patina.ListToolsAsync()).EnumerateArray().ToList();

        Assert.Equal(["define_graph", "edit_graph", "get_graph", "save", "open", "push_snapshot", "pop_snapshot", "export", "swatch", "check"], tools.Select(t => t.GetProperty("name").GetString()));
        Assert.All(tools, t => Assert.False(t.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean()));
    }

    [Theory]
    [InlineData("edit_graph", """{"operations":[{"op":"set","seed":1}]}""")]
    [InlineData("get_graph", "{}")]
    [InlineData("save", """{"path":"x"}""")]
    [InlineData("push_snapshot", "{}")]
    public async Task A_tool_that_needs_the_graph_says_there_is_none_before_define_graph_or_open(string tool, string arguments)
    {
        await using var patina = await PatinaHarness.StartAsync();

        Assert.Equal($"{tool}: there is no open graph yet; define_graph or open one first", await patina.Refused(tool, arguments));
    }

    [Theory]
    [InlineData("define_graph", """{"graph":{},"extra":1}""", "define_graph: unknown argument 'extra'; define_graph takes graph")]
    [InlineData("get_graph", """{"path":"x"}""", "get_graph: unknown argument 'path'; get_graph takes no arguments")]
    [InlineData("define_graph", "{}", "define_graph: graph is missing; give the whole graph as a JSON object")]
    [InlineData("define_graph", """{"graph":"x"}""", ", not a string")]
    [InlineData("define_graph", """{"graph":{"size":256,"seed":0,"nodes":{}}}""", "define_graph: graph is not a .texgraph body: the file has no version; the open graph is unchanged")]
    [InlineData("define_graph", """{"graph":{"version":1,"size":300,"seed":0,"nodes":{}}}""", "graph is not a .texgraph body: size is a power of two")]
    [InlineData("define_graph", """{"graph":{"version":1,"size":64,"seed":0,"nodes":{"a":{"type":"colour","colour":"#ffffff"}}}}""", "node 'a': unknown key 'colour'")]
    [InlineData("save", """{"path":7}""", "save: path is a string, not a number")]
    [InlineData("push_snapshot", """{"label":[1]}""", "push_snapshot: label is a string, not an array")]
    public async Task A_bad_argument_is_refused_naming_it(string tool, string arguments, string expected)
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", Flat);
        var before = await patina.GraphText();

        Assert.Contains(expected, await patina.Refused(tool, arguments));
        Assert.Equal(before, await patina.GraphText());
    }

    [Fact]
    public async Task Define_graph_replaces_the_graph_and_clears_its_path()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Flat);
        await patina.Ok("save", """{"path":"flat"}""");
        Assert.Contains($"; path {Path.Combine(folder.Path, "flat.texgraph")}; ", await patina.Ok("get_graph", "{}"));

        await patina.Ok("define_graph", Flat);
        Assert.StartsWith("get_graph: 2 nodes, size 64, seed 5; no path (save gives it one); no faults", await patina.Ok("get_graph", "{}"));
    }

    [Theory]
    [InlineData("[]", "edit_graph: operations is empty; give at least one")]
    [InlineData("{}", "edit_graph: operations is missing; give an array of operations")]
    [InlineData("[1]", "edit_graph: nothing changed: operation 1 is a number; an operation is an object with op")]
    [InlineData("""[{"node":"c"}]""", "operation 1 has no op; op is one of set_node, remove_node, connect, disconnect, set_params, set")]
    [InlineData("""[{"op":"rename","node":"c"}]""", "operation 1 has op \"rename\"; op is one of")]
    [InlineData("""[{"op":"remove_node","node":"c","port":"x"}]""", "operation 1 (remove_node): unknown key 'port'; remove_node takes node")]
    [InlineData("""[{"op":"remove_node"}]""", "operation 1 (remove_node): node is missing")]
    [InlineData("""[{"op":"set","seed":1},{"op":"remove_node","node":"nope"}]""", "operation 2 (remove_node): there is no node 'nope'; the nodes are c, out")]
    [InlineData("""[{"op":"set_params","node":"nope","params":{}}]""", "operation 1 (set_params): there is no node 'nope'")]
    [InlineData("""[{"op":"set_node","node":"n"}]""", "operation 1 (set_node): type is missing")]
    [InlineData("""[{"op":"set_node","node":"n","type":"colour","inputs":[]}]""", "operation 1 (set_node): inputs is an array; it is an object of port: source")]
    [InlineData("""[{"op":"set_node","node":"n","type":"colour","inputs":{"a":1}}]""", "operation 1 (set_node): input 'a' is a number; a source is a string")]
    [InlineData("""[{"op":"set_params","node":"c","params":{"colour":true}}]""", "operation 1 (set_params): parameter 'colour' is true or false; a value is a number, a string, or an array of numbers or of strings")]
    [InlineData("""[{"op":"set_params","node":"c"}]""", "operation 1 (set_params): params is missing")]
    [InlineData("""[{"op":"connect","node":"out","port":"in"}]""", "operation 1 (connect): from is missing")]
    [InlineData("""[{"op":"disconnect","node":"c","port":"in"}]""", "operation 1 (disconnect): node 'c' input 'in' is not connected; its connected inputs are none")]
    [InlineData("""[{"op":"set","size":100}]""", "operation 1 (set): size 100 is not a power of two from 32 to 8192")]
    [InlineData("""[{"op":"set","seed":-1}]""", "operation 1 (set): seed -1 is not a whole number from 0 to 4294967295")]
    [InlineData("""[{"op":"set"}]""", "operation 1 (set): give size, seed or both")]
    [InlineData("""[{"op":"set_node","node":"x","type":"marble"}]""", "edit_graph: nothing changed: the operations would leave 1 new fault:\n- node 'x'")]
    [InlineData("""[{"op":"set_params","node":"c","params":{"colour":"red"}}]""", "would leave 1 new fault:\n- node 'c' parameter 'colour'")]
    [InlineData("""[{"op":"connect","node":"out","port":"colour","from":"c"}]""", "would leave 1 new fault:\n- node 'out'")]
    [InlineData("""[{"op":"disconnect","node":"out","port":"in"}]""", "would leave 1 new fault:\n- node 'out' input 'in'")]
    public async Task An_edit_list_that_fails_anywhere_changes_nothing_and_says_why(string operations, string expected)
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", Flat);
        await patina.Ok("push_snapshot", "{}");
        var before = await patina.GraphText();

        var said = await patina.Refused("edit_graph", "{\"operations\":" + operations + "}");

        Assert.Contains(expected, said);
        Assert.Equal(before, await patina.GraphText());
        Assert.Equal(1, patina.Store.Depth);
    }

    [Fact]
    public async Task A_list_may_pass_through_a_fault_as_long_as_it_ends_without_one()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", Flat);

        var said = await patina.Ok("edit_graph", Ops(
            """{"op":"remove_node","node":"c"}""",
            """{"op":"set_node","node":"d","type":"colour","params":{"colour":"#ffffff"}}""",
            """{"op":"connect","node":"out","port":"in","from":"d"}""",
            """{"op":"set_params","node":"d","params":{"colour":"#000000"}}"""));

        Assert.Equal("edit_graph: 4 operations applied: removed 'c' (colour); added 'd' (colour); connected 'out' input 'in' to 'd' (was 'c'); set 'd' colour \"#000000\". The graph is 2 nodes, size 64, seed 5; no faults: it evaluates", said);
    }

    [Fact]
    public async Task Set_node_on_an_existing_id_replaces_it_whole()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", Flat);

        Assert.Contains("replaced 'c' (colour)", await patina.Ok("edit_graph", Ops("""{"op":"set_node","node":"c","type":"colour"}""")));
        Assert.Contains("    \"c\": { \"type\": \"colour\" },", await patina.GraphText());
    }

    [Fact]
    public async Task An_opened_graph_s_faults_do_not_stop_an_edit_and_an_edit_can_fix_them()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "planted.texgraph"), PlantedFile);
        await using var patina = await PatinaHarness.StartAsync(folder.Path);

        var opened = await patina.Ok("open", """{"path":"planted"}""");
        Assert.StartsWith($"open: opened planted.texgraph from {Path.Combine(folder.Path, "planted.texgraph")}: 2 nodes, size 64, seed 5; 1 fault:\n- node 'out' input 'in': names 'cc', which is not a node", opened);
        Assert.EndsWith("\nIt is open: fix them with edit_graph (save refuses it until then).", opened);

        var unrelated = await patina.Ok("edit_graph", Ops("""{"op":"set","seed":6}"""));
        Assert.Contains("1 fault:\n- node 'out' input 'in'", unrelated);
        Assert.StartsWith("save: refused: the graph has 1 fault:\n- node 'out' input 'in'", await patina.Refused("save", """{"path":"fixed"}"""));
        Assert.False(File.Exists(Path.Combine(folder.Path, "fixed.texgraph")));

        Assert.EndsWith("no faults: it evaluates", await patina.Ok("edit_graph", Ops("""{"op":"connect","node":"out","port":"in","from":"c"}""")));
        Assert.StartsWith("save: saved fixed.texgraph", await patina.Ok("save", """{"path":"fixed"}"""));
    }

    [Fact]
    public async Task A_kind_mismatch_behind_another_input_s_fault_is_refused_at_the_edit_that_makes_it()
    {
        await using var patina = await PatinaHarness.StartAsync();
        // base's b names no node; its a is colour, so base gives colour whatever b becomes.
        var defined = await patina.Ok("define_graph", """{"graph":{"version":1,"size":64,"seed":5,"nodes":{"c":{"type":"colour"},"n":{"type":"noise"},"base":{"type":"blend","inputs":{"a":"c","b":"zzz"}},"out":{"type":"output","inputs":{"in":"base"}}}}}""");
        Assert.Contains("; 1 fault:\n- node 'base' input 'b'", defined);

        var said = await patina.Refused("edit_graph", Ops(
            """{"op":"set_node","node":"h","type":"height_to_normal","inputs":{"height":"base"}}""",
            """{"op":"set_node","node":"nout","type":"output","inputs":{"in":"h"},"params":{"role":"normal"}}"""));
        Assert.Equal("edit_graph: nothing changed: the operations would leave 1 new fault:\n- node 'h' input 'height': takes greyscale, but 'base' gives colour", said);

        Assert.EndsWith("no faults: it evaluates", await patina.Ok("edit_graph", Ops("""{"op":"connect","node":"base","port":"b","from":"n"}""")));
    }

    [Fact]
    public async Task A_kind_mismatch_on_a_node_on_a_cycle_is_refused_at_the_edit_that_makes_it()
    {
        await using var patina = await PatinaHarness.StartAsync();
        // base's b names no node; the edit turns that fault into a cycle at the same place, and gives base a colour mask.
        var defined = await patina.Ok("define_graph", """{"graph":{"version":1,"size":64,"seed":5,"nodes":{"c":{"type":"colour"},"n":{"type":"noise"},"base":{"type":"blend","inputs":{"a":"c","b":"zzz"}},"lv":{"type":"levels","inputs":{"in":"base"}},"out":{"type":"output","inputs":{"in":"base"}}}}}""");
        Assert.Contains("; 1 fault:\n- node 'base' input 'b'", defined);

        var said = await patina.Refused("edit_graph", Ops(
            """{"op":"connect","node":"base","port":"b","from":"lv"}""",
            """{"op":"connect","node":"base","port":"mask","from":"c"}"""));
        Assert.Equal("edit_graph: nothing changed: the operations would leave 1 new fault:\n- node 'base' input 'mask': takes greyscale, but 'c' gives colour", said);

        Assert.EndsWith("no faults: it evaluates", await patina.Ok("edit_graph", Ops("""{"op":"connect","node":"base","port":"b","from":"n"}""")));
    }

    [Fact]
    public async Task Save_adds_the_extension_says_replaced_and_takes_the_path_as_the_graph_s()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Flat);
        var target = Path.Combine(folder.Path, "sub", "flat.texgraph");

        Assert.Equal($"save: saved flat.texgraph to {target}: 2 nodes, size 64, seed 5", await patina.Ok("save", """{"path":"sub/flat"}"""));
        Assert.Equal($"save: saved flat.texgraph to {target} (replaced): 2 nodes, size 64, seed 5", await patina.Ok("save", """{"path":"sub/flat.TEXGRAPH"}""".Replace(".TEXGRAPH", ".texgraph")));
        Assert.Equal($"save: saved flat.v2.texgraph to {Path.Combine(folder.Path, "flat.v2.texgraph")}: 2 nodes, size 64, seed 5", await patina.Ok("save", """{"path":"flat.v2"}"""));
        Assert.Equal((await patina.GraphText()) + "\n", File.ReadAllText(target));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.writing", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("save", "", "save: path is empty")]
    [InlineData("save", "sub/", "names a folder")]
    [InlineData("save", "a|b", "holds '|', which a file path may not")]
    [InlineData("open", "", "open: path is empty")]
    public async Task A_bad_path_is_refused(string tool, string path, string expected)
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Flat);

        Assert.Contains(expected, await patina.Refused(tool, $"{{\"path\":\"{path}\"}}"));
        Assert.Empty(Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Open_lists_what_is_there_for_a_missing_file_or_folder_and_leaves_the_graph()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Flat);
        var before = await patina.GraphText();

        Assert.Equal($"open: no file at {Path.Combine(folder.Path, "a.texgraph")}; there are no .texgraph files in {folder.Path}", await patina.Refused("open", """{"path":"a"}"""));
        var gone = Path.Combine(folder.Path, "gone");
        Assert.Equal($"open: no file at {Path.Combine(gone, "a.texgraph")}; the folder {gone} does not exist", await patina.Refused("open", """{"path":"gone/a"}"""));
        for (var i = 0; i < OpenTool.ListLimit + 2; i++) File.WriteAllText(Path.Combine(folder.Path, $"g{i:00}.texgraph"), PlantedFile);
        Assert.EndsWith($"({OpenTool.ListLimit + 2}) are: g00.texgraph, g01.texgraph, g02.texgraph, g03.texgraph, g04.texgraph, g05.texgraph, g06.texgraph, g07.texgraph, g08.texgraph, g09.texgraph, g10.texgraph, g11.texgraph, g12.texgraph, g13.texgraph, g14.texgraph, g15.texgraph, g16.texgraph, g17.texgraph, g18.texgraph, g19.texgraph, and 2 more", await patina.Refused("open", """{"path":"a"}"""));
        Assert.Equal(before, await patina.GraphText());
    }

    [Theory]
    [InlineData("{\n  \"version\": 1,\n  \"size\": 64\n  \"seed\": 5\n}\n", "line 4, column 3: not JSON")]
    [InlineData("{\n  \"version\": 3,\n  \"size\": 64,\n  \"seed\": 5,\n  \"nodes\": {}\n}\n", "line 2, column 14: version 3 is not known")]
    [InlineData("{\n  \"version\": 1,\n  \"size\": 64,\n  \"seed\": 5,\n  \"nodes\": { \"a\": { \"kind\": \"colour\" } }\n}\n", "line 5, column 21: node 'a': unknown key 'kind'")]
    public async Task Open_refuses_a_file_that_does_not_parse_with_its_position(string file, string expected)
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "bad.texgraph");
        File.WriteAllText(path, file);
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Flat);
        await patina.Ok("push_snapshot", "{}");
        var before = await patina.GraphText();

        var said = await patina.Refused("open", """{"path":"bad"}""");

        Assert.StartsWith($"open: {path} is not a graph patina reads: ", said);
        Assert.Contains(expected, said);
        Assert.EndsWith("; the open graph is unchanged", said);
        Assert.Equal(before, await patina.GraphText());
        Assert.Equal(1, patina.Store.Depth);
    }
}
