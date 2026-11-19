using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-102: <c>define_target</c>, <c>delete_target</c>, <c>get_mesh</c>'s targets and <c>define_mesh</c>'s refusal, on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class TargetToolTests() : TempFolderTests("carve-targets")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        // save and undo answer in plain text, the rest in JSON.
        return TextOf(result).StartsWith('{') ? JsonDocument.Parse(TextOf(result)).RootElement : default;
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static string[] Names(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static string[] TargetNames(JsonElement mesh) =>
        mesh.TryGetProperty("targets", out var t) ? t.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToArray() : [];

    private static float[] Floats(JsonElement array) => array.EnumerateArray().Select(x => x.GetSingle()).ToArray();

    private const string Bulge = """{"name":"Bulge","positions":{"2":[1.1,1.3,-0.1],"3":[-0.1,1.3,-0.1],"6":[1.1,1.3,1.1],"7":[-0.1,1.3,1.1]}}""";

    private const string Wide = """{"name":"Wide","positions":{"0":[-0.5,0,0]}}""";

    private const string Pyramid = """{"vertices":[[0,0,0],[1,0,0],[1,0,1],[0,0,1],[0.5,1,0.5]],"faces":[[0,3,2,1],[0,1,4],[1,2,4],[2,3,4],[3,0,4]]}""";

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        try
        {
            await carve.InitializeAsync();
            var tools = await carve.ListToolsAsync();
            Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));
            return (carve, tools);
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    [Fact]
    public async Task The_sparse_example_makes_a_target_get_mesh_lists_it_and_reads_back_its_absolute_positions()
    {
        var (carve, tools) = await StartAsync();
        using var guard = carve;

        var body = Ok(await carve.CallToolAsync("define_target", ToolList.Examples(tools, "define_target")[0]));
        Assert.Equal("Bulge", body.GetProperty("name").GetString());
        Assert.Equal("mesh", body.GetProperty("mesh").GetString());
        Assert.Equal(4, body.GetProperty("vertices").GetInt32());
        Assert.Equal(MathF.Sqrt(0.01f + 0.09f + 0.01f), body.GetProperty("largest_move").GetSingle(), 1e-5f);
        Assert.False(body.GetProperty("replaced").GetBoolean());
        Assert.Equal(["Bulge"], Names(body.GetProperty("targets")));

        var plain = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var listed = plain.GetProperty("targets").EnumerateArray().Single();
        Assert.Equal("Bulge", listed.GetProperty("name").GetString());
        Assert.Equal(4, listed.GetProperty("vertices").GetInt32());
        Assert.False(plain.TryGetProperty("target", out _));
        Assert.Equal([0f, 0f, 0f], Floats(plain.GetProperty("vertices")[0].GetProperty("at")));

        // The example in get_mesh's description.
        var example = Assert.Single(ToolList.Examples(tools, "get_mesh"), e => e.Contains("target", StringComparison.Ordinal));
        var asked = Ok(await carve.CallToolAsync("get_mesh", example));
        var positions = asked.GetProperty("target").GetProperty("positions");
        Assert.Equal("Bulge", asked.GetProperty("target").GetProperty("name").GetString());
        Assert.Equal(["2", "3", "6", "7"], positions.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal([1.1f, 1.3f, -0.1f], Floats(positions.GetProperty("2")));
        Assert.Equal([-0.1f, 1.3f, 1.1f], Floats(positions.GetProperty("7")));
    }

    [Fact]
    public async Task A_mesh_with_no_targets_reports_none_and_an_unknown_target_to_read_lists_them()
    {
        var (carve, _) = await StartAsync();
        using var guard = carve;
        Assert.False(Ok(await carve.CallToolAsync("get_mesh", "{}")).TryGetProperty("targets", out _));
        Assert.Contains("it has no targets", Refused(await carve.CallToolAsync("get_mesh", """{"target":"Bulge"}""")));
        Ok(await carve.CallToolAsync("define_target", Bulge));
        Ok(await carve.CallToolAsync("define_target", Wide));
        Assert.Contains("its targets are 'Bulge', 'Wide'", Refused(await carve.CallToolAsync("get_mesh", """{"target":"Nope"}""")));
        Assert.Contains("target: expected a string", Refused(await carve.CallToolAsync("get_mesh", """{"target":3}""")));
    }

    [Fact]
    public async Task A_dense_array_gives_every_slot_with_null_at_a_deleted_vertex()
    {
        var (carve, _) = await StartAsync();
        using var guard = carve;
        Ok(await carve.CallToolAsync("delete", """{"vertices":[6]}"""));
        const string all = "[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],null,[0,2,1]]";
        var body = Ok(await carve.CallToolAsync("define_target", "{\"name\":\"Tall\",\"positions\":" + all + "}"));
        Assert.Equal(1, body.GetProperty("vertices").GetInt32());
        Assert.Equal(1f, body.GetProperty("largest_move").GetSingle(), 1e-6f);
        var read = Ok(await carve.CallToolAsync("get_mesh", """{"target":"Tall"}"""));
        Assert.Equal(["7"], read.GetProperty("target").GetProperty("positions").EnumerateObject().Select(p => p.Name).ToArray());

        Assert.Contains("positions[2]: null, but vertex 2 exists",
            Refused(await carve.CallToolAsync("define_target", """{"name":"Bad","positions":[[0,0,0],[1,0,0],null,[0,1,0],[0,0,1],[1,0,1],null,[0,2,1]]}""")));
        Assert.Contains("2 positions given; the mesh has 8 vertex slots",
            Refused(await carve.CallToolAsync("define_target", """{"name":"Bad","positions":[[0,0,0],[1,0,0]]}""")));
        // A position at the deleted slot is ignored.
        Ok(await carve.CallToolAsync("define_target", "{\"name\":\"Ignored\",\"positions\":" + all.Replace("null", "[9,9,9]", StringComparison.Ordinal) + "}"));
    }

    [Fact]
    public async Task A_name_the_mesh_has_is_replaced_and_the_result_says_so_in_one_undo_step()
    {
        var (carve, _) = await StartAsync();
        using var guard = carve;
        Ok(await carve.CallToolAsync("define_target", Bulge));
        var again = Ok(await carve.CallToolAsync("define_target", """{"name":"Bulge","positions":{"0":[-1,0,0]}}"""));
        Assert.True(again.GetProperty("replaced").GetBoolean());
        Assert.Equal(1, again.GetProperty("vertices").GetInt32());
        Assert.Equal(["Bulge"], Names(again.GetProperty("targets")));
        Assert.Equal(["0"], Ok(await carve.CallToolAsync("get_mesh", """{"target":"Bulge"}""")).GetProperty("target").GetProperty("positions").EnumerateObject().Select(p => p.Name).ToArray());

        // One undo puts the first Bulge back, a second takes it away.
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(4, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("targets")[0].GetProperty("vertices").GetInt32());
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Empty(TargetNames(Ok(await carve.CallToolAsync("get_mesh", "{}"))));
    }

    [Fact]
    public async Task Every_argument_refusal_is_listed_and_leaves_the_model_unchanged()
    {
        var (carve, _) = await StartAsync();
        using var guard = carve;
        async Task<string> Call(string arguments) => Refused(await carve.CallToolAsync("define_target", arguments));

        Assert.Contains("name: required but missing", await Call("""{"positions":{"0":[0,0,0]}}"""));
        Assert.Contains("a target name must not be empty", await Call("""{"name":"","positions":{"0":[0,0,0]}}"""));
        Assert.Contains("must not contain ':'", await Call("""{"name":"a:b","positions":{"0":[0,0,0]}}"""));
        Assert.Contains("name: expected a string", await Call("""{"name":4,"positions":{"0":[0,0,0]}}"""));
        Assert.Contains("neither was given", await Call("""{"name":"A"}"""));
        Assert.Contains("not both", await Call("""{"name":"A","positions":{"0":[0,0,0]},"file":"x.carve"}"""));
        Assert.Contains("no file was given", await Call("""{"name":"A","positions":{"0":[0,0,0]},"file_mesh":"m"}"""));
        Assert.Contains("positions[9]", await Call("""{"name":"A","positions":{"9":[0,0,0]}}"""));
        Assert.Contains("positions[\"1.5\"]: a key is a vertex id", await Call("""{"name":"A","positions":{"1.5":[0,0,0]}}"""));
        Assert.Contains("positions[2]: expected 3 numbers", await Call("""{"name":"A","positions":{"2":[0,0]}}"""));
        Assert.Contains("positions[3][1]: expected a number", await Call("""{"name":"A","positions":{"3":[0,"y",0]}}"""));
        Assert.Contains("positions: expected an array", await Call("""{"name":"A","positions":5}"""));
        Assert.Contains("zz: not an argument of define_target", await Call("""{"name":"A","positions":{"0":[0,0,0]},"zz":1}"""));
        // Every problem together.
        Assert.Contains("2 problem(s)", await Call("""{"name":"","positions":{"9":[0,0,0],"x":[0,0,0]}}"""));
        Assert.Empty(TargetNames(Ok(await carve.CallToolAsync("get_mesh", "{}"))));

        // A deleted vertex is refused as the Core words it.
        Ok(await carve.CallToolAsync("delete", """{"vertices":[6]}"""));
        Assert.Contains("positions[6]", await Call("""{"name":"A","positions":{"6":[0,0,0]}}"""));
    }

    [Fact]
    public async Task No_mesh_and_an_unnamed_mesh_among_several_are_refused()
    {
        using var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Assert.Contains("there is no mesh yet", Refused(await carve.CallToolAsync("define_target", Bulge)));
        Assert.Contains("there is no mesh yet", Refused(await carve.CallToolAsync("delete_target", """{"name":"Bulge"}""")));
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));
        Ok(await carve.CallToolAsync("define_mesh", """{"name":"other","vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Assert.Contains("'mesh', 'other'", Refused(await carve.CallToolAsync("define_target", Bulge)));
        Ok(await carve.CallToolAsync("define_target", Bulge.Replace("{\"name\"", "{\"mesh\":\"mesh\",\"name\"", StringComparison.Ordinal)));
        Assert.Equal(["Bulge"], TargetNames(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"mesh"}"""))));
        Assert.Empty(TargetNames(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}"""))));
    }

    [Fact]
    public async Task The_file_example_makes_a_target_from_a_second_file_of_the_same_topology()
    {
        var (carve, tools) = await StartAsync();
        using var guard = carve;
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0.2,0,0]}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"cube-lean.carve"}"""));
        Ok(await carve.CallToolAsync("undo", "{}"));

        var body = Ok(await carve.CallToolAsync("define_target", ToolList.Examples(tools, "define_target")[1]));
        Assert.Equal("Lean", body.GetProperty("name").GetString());
        Assert.Equal(4, body.GetProperty("vertices").GetInt32());
        Assert.Equal(0.2f, body.GetProperty("largest_move").GetSingle(), 1e-6f);
        var positions = Ok(await carve.CallToolAsync("get_mesh", """{"target":"Lean"}""")).GetProperty("target").GetProperty("positions");
        Assert.Equal(new[] { 0.2f, 1f, 0f }, Floats(positions.GetProperty("3")));
        // The mesh itself is where it was.
        Assert.Equal(new[] { 0f, 1f, 0f }, Floats(Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("vertices")[3].GetProperty("at")));
    }

    [Fact]
    public async Task A_file_with_another_topology_a_missing_file_and_a_missing_mesh_are_refused()
    {
        var (carve, _) = await StartAsync();
        using var guard = carve;
        async Task<string> Call(string arguments) => Refused(await carve.CallToolAsync("define_target", arguments));

        Ok(await carve.CallToolAsync("define_mesh", Pyramid.Replace("{", "{\"name\":\"pyramid\",", StringComparison.Ordinal)));
        Ok(await carve.CallToolAsync("save", """{"path":"pyramid.carve","mesh":"pyramid"}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"both.carve"}"""));

        Assert.Contains("vertices: 8 here, 5 in the other mesh", await Call("""{"name":"X","mesh":"mesh","file":"pyramid.carve"}"""));
        Assert.Contains("no file at", await Call("""{"name":"X","mesh":"mesh","file":"missing.carve"}"""));
        Assert.Contains(".carve", await Call("""{"name":"X","mesh":"mesh","file":"pyramid.glb"}"""));
        Assert.Contains("has no mesh 'cube'; its meshes are 'mesh', 'pyramid'", await Call("""{"name":"X","mesh":"mesh","file":"both.carve","file_mesh":"cube"}"""));
        // The mesh's own name is found by default in a file of several; file_mesh picks another.
        Assert.Equal(0, Ok(await carve.CallToolAsync("define_target", """{"name":"Same","mesh":"mesh","file":"both.carve"}""")).GetProperty("vertices").GetInt32());
        Assert.Contains("vertices: 5 here, 8 in the other mesh", await Call("""{"name":"X","mesh":"pyramid","file":"both.carve","file_mesh":"mesh"}"""));
        // With no mesh of its name, a file of one mesh is read whatever the mesh is called; a file of several asks for file_mesh.
        Ok(await carve.CallToolAsync("define_mesh", Pyramid.Replace("{", "{\"name\":\"p2\",", StringComparison.Ordinal)));
        Assert.Equal(0, Ok(await carve.CallToolAsync("define_target", """{"name":"Only","mesh":"p2","file":"pyramid.carve"}""")).GetProperty("vertices").GetInt32());
        Assert.Contains("name one of 'mesh', 'pyramid'", await Call("""{"name":"X","mesh":"p2","file":"both.carve"}"""));
        // Faces differ though the vertex count is the same: the first difference is named.
        Ok(await carve.CallToolAsync("define_mesh", "{\"name\":\"p3\",\"vertices\":[[0,0,0],[1,0,0],[1,0,1],[0,0,1],[0.5,1,0.5]],\"faces\":[[0,3,2,1],[1,0,4],[1,2,4],[2,3,4],[3,0,4]]}"));
        Ok(await carve.CallToolAsync("save", """{"path":"p3.carve","mesh":"p3"}"""));
        Assert.Contains("faces[1][0]: vertex 0 here, 1 in the other mesh", await Call("""{"name":"X","mesh":"p2","file":"p3.carve"}"""));
    }

    [Fact]
    public async Task Delete_target_removes_one_lists_the_targets_on_a_wrong_name_and_undoes()
    {
        var (carve, tools) = await StartAsync();
        using var guard = carve;
        Assert.Contains("it has no targets", Refused(await carve.CallToolAsync("delete_target", """{"name":"Bulge"}""")));
        Ok(await carve.CallToolAsync("define_target", Bulge));
        Ok(await carve.CallToolAsync("define_target", Wide));
        Assert.Contains("its targets are 'Bulge', 'Wide'", Refused(await carve.CallToolAsync("delete_target", """{"name":"Nope"}""")));
        Assert.Contains("name: required but missing", Refused(await carve.CallToolAsync("delete_target", "{}")));

        var body = Ok(await carve.CallToolAsync("delete_target", ToolList.Examples(tools, "delete_target").Single()));
        Assert.Equal("Bulge", body.GetProperty("deleted").GetString());
        Assert.Equal(["Wide"], Names(body.GetProperty("targets")));
        Assert.Equal(["Wide"], TargetNames(Ok(await carve.CallToolAsync("get_mesh", "{}"))));
        Ok(await carve.CallToolAsync("delete_target", """{"name":"Wide"}"""));
        Assert.False(Ok(await carve.CallToolAsync("get_mesh", "{}")).TryGetProperty("targets", out _));
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(["Wide"], TargetNames(Ok(await carve.CallToolAsync("get_mesh", "{}"))));
    }

    [Fact]
    public async Task Define_mesh_over_a_mesh_with_targets_is_refused_by_name_unless_drop_targets()
    {
        var (carve, tools) = await StartAsync();
        using var guard = carve;
        Ok(await carve.CallToolAsync("define_target", Bulge));
        Ok(await carve.CallToolAsync("define_target", Wide));
        var cube = ToolList.Cube(tools);
        string With(string extra) => "{" + extra + cube[1..];

        var refused = Refused(await carve.CallToolAsync("define_mesh", cube));
        Assert.Contains("'Bulge', 'Wide'", refused);
        Assert.Contains("drop_targets", refused);
        Assert.Equal(["Bulge", "Wide"], TargetNames(Ok(await carve.CallToolAsync("get_mesh", "{}"))));
        Assert.Contains("drop_targets: expected true or false", Refused(await carve.CallToolAsync("define_mesh", With("\"drop_targets\":1,"))));

        // A new name is not a replacement; drop_targets false is the default.
        Ok(await carve.CallToolAsync("define_mesh", With("\"name\":\"second\",")));
        Refused(await carve.CallToolAsync("define_mesh", With("\"name\":\"mesh\",\"drop_targets\":false,")));
        Ok(await carve.CallToolAsync("define_mesh", With("\"name\":\"mesh\",\"drop_targets\":true,")));
        Assert.Empty(TargetNames(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"mesh"}"""))));
        // True does nothing to a mesh with none, and undo brings the targets back.
        Ok(await carve.CallToolAsync("define_mesh", With("\"name\":\"second\",\"drop_targets\":true,")));
        Ok(await carve.CallToolAsync("undo", "{}"));
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(["Bulge", "Wide"], TargetNames(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"mesh"}"""))));
    }

    [Fact]
    public async Task The_descriptions_and_schemas_name_the_arguments_and_the_examples_are_parseable()
    {
        var (carve, tools) = await StartAsync();
        using var guard = carve;
        var define = ToolList.Tool(tools, "define_target");
        var schema = define.GetProperty("inputSchema");
        var props = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["file", "file_mesh", "mesh", "name", "positions"], props);
        Assert.Equal(["name"], Names(schema.GetProperty("required")));
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.All(props, p => Assert.False(string.IsNullOrWhiteSpace(schema.GetProperty("properties").GetProperty(p).GetProperty("description").GetString())));
        Assert.Equal(2, ToolList.Examples(tools, "define_target").Length);
        Assert.All(ToolList.Examples(tools, "define_target"), e => JsonDocument.Parse(e).Dispose());
        Assert.Single(ToolList.Examples(tools, "delete_target"));
        foreach (var word in new[] { "ABSOLUTE", "replaced", "largest_move", "FIRST difference", "delete_target" })
            Assert.Contains(word, ToolList.Description(tools, "define_target"));
        Assert.Contains("drop_targets", ToolList.Description(tools, "define_mesh"));
        Assert.Equal(["drop_targets", "faces", "name", "vertices"],
            ToolList.Tool(tools, "define_mesh").GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(["level", "mesh", "target"], ToolList.Tool(tools, "get_mesh").GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Contains("targets:[{name, vertices}]", ToolList.Description(tools, "get_mesh"));
    }
}
