using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>move, extrude, get_mesh and undo through the real server.</summary>
[Trait("Tier", "regression")]
public class EditToolTests
{
    private static async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync(bool define = true)
    {
        var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (define) Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        return (carve, tools);
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return JsonDocument.Parse(CarveProcess.TextOf(result)).RootElement;
    }

    private static void ExpectError(JsonElement result, params string[] mentions)
    {
        Assert.True(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        foreach (var m in mentions) Assert.Contains(m, CarveProcess.TextOf(result));
    }

    private static void ExpectCleanClosed(JsonElement body)
    {
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(0, body.GetProperty("warnings").GetArrayLength());
    }

    private static async Task<string> MeshAsync(CarveProcess carve) => CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", "{}"));

    [Theory]
    [InlineData("move")]
    [InlineData("extrude")]
    [InlineData("get_mesh")]
    public async Task Every_worked_example_in_a_description_runs_on_the_cube_closed_and_warning_free(string tool)
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;

        var examples = Examples(tools, tool);
        Assert.NotEmpty(examples);
        foreach (var example in examples) ExpectCleanClosed(Ok(await carve.CallToolAsync(tool, example)));
    }

    [Fact]
    public async Task The_undo_example_undoes_an_extrude()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        var example = Examples(tools, "undo").Single();
        Ok(await carve.CallToolAsync("extrude", Examples(tools, "extrude")[0]));

        var body = Ok(await carve.CallToolAsync("undo", example));

        Assert.Equal("extrude", body.GetProperty("undone").GetString());
        ExpectCleanClosed(body);
    }

    [Fact]
    public async Task Move_returns_the_report_and_the_ids_it_moved_and_the_example_raises_the_top()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;

        var body = Ok(await carve.CallToolAsync("move", Examples(tools, "move").Single()));

        Assert.Equal(8, body.GetProperty("vertices").GetInt32());
        Assert.Equal(1.5f, body.GetProperty("bounds").GetProperty("max")[1].GetSingle());
        Assert.Empty(body.GetProperty("created").GetProperty("vertices").EnumerateArray());
        Assert.Empty(body.GetProperty("created").GetProperty("faces").EnumerateArray());
        Assert.Equal([2, 3, 6, 7], body.GetProperty("moved").EnumerateArray().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task Extrude_returns_what_it_created_cap_walls_and_ring()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var body = Ok(await carve.CallToolAsync("extrude", """{"faces":[3],"distance":0.5}"""));

        Assert.Equal(12, body.GetProperty("vertices").GetInt32());
        Assert.Equal(10, body.GetProperty("faces").GetInt32());
        Assert.Equal([8, 9, 10, 11], body.GetProperty("created").GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal([6, 7, 8, 9], body.GetProperty("created").GetProperty("faces").EnumerateArray().Select(x => x.GetInt32()));
        var one = body.GetProperty("extruded").EnumerateArray().Single();
        Assert.Equal(3, one.GetProperty("face").GetInt32());
        Assert.Equal(3, one.GetProperty("cap").GetInt32());
        Assert.Equal([6, 7, 8, 9], one.GetProperty("walls").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal([8, 9, 10, 11], one.GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task Get_mesh_lists_vertices_faces_and_edges_with_the_report_fields()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var body = Ok(await carve.CallToolAsync("get_mesh", "{}"));

        Assert.Equal(8, body.GetProperty("vertices").GetArrayLength());
        Assert.Equal(6, body.GetProperty("faces").GetArrayLength());
        Assert.Equal(12, body.GetProperty("edges").GetArrayLength());
        var v6 = body.GetProperty("vertices")[6];
        Assert.Equal(6, v6.GetProperty("id").GetInt32());
        Assert.Equal([1f, 1f, 1f], v6.GetProperty("at").EnumerateArray().Select(x => x.GetSingle()));
        var top = body.GetProperty("faces")[3];
        Assert.Equal(3, top.GetProperty("id").GetInt32());
        Assert.Equal([3, 7, 6, 2], top.GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal([0f, 1f, 0f], top.GetProperty("normal").EnumerateArray().Select(x => x.GetSingle()));
        Assert.Equal([0.5f, 1f, 0.5f], top.GetProperty("centre").EnumerateArray().Select(x => x.GetSingle()));
        var edge = body.GetProperty("edges")[0];
        Assert.Equal([0, 1], edge.GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal(2, edge.GetProperty("faces").GetArrayLength());
        ExpectCleanClosed(body);
        Assert.True(body.TryGetProperty("bounds", out var bounds) && bounds.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task With_no_mesh_get_mesh_move_extrude_and_undo_are_tool_errors()
    {
        var (carve, _) = await StartAsync(define: false);
        using var _ = carve;

        ExpectError(await carve.CallToolAsync("get_mesh", "{}"), "define_mesh");
        ExpectError(await carve.CallToolAsync("move", """{"faces":[0],"by":[1,0,0]}"""), "define_mesh");
        ExpectError(await carve.CallToolAsync("extrude", """{"faces":[0],"distance":1}"""), "define_mesh");
        ExpectError(await carve.CallToolAsync("undo", "{}"), "nothing to undo");
    }

    [Theory]
    [InlineData("move", """{"faces":[9],"by":[0,1,0]}""", "faces[9]")]
    [InlineData("move", """{"vertices":[0],"by":[0,0,0]}""", "by")]
    [InlineData("move", """{"edges":[[0,6]],"by":[0,1,0]}""", "edges[0-6]")]
    [InlineData("move", """{"vertices":[0]}""", "by")]
    [InlineData("move", """{"vertices":[0],"by":[1,2]}""", "by")]
    [InlineData("move", """{"by":[1,0,0]}""", "vertices, edges, faces")]
    [InlineData("move", """{"vertices":[99,98],"by":[1,0,0]}""", "vertices[98]")]
    [InlineData("extrude", """{"faces":[9],"distance":1}""", "faces[9]")]
    [InlineData("extrude", """{"faces":[0,0],"distance":1}""", "faces[0]")]
    [InlineData("extrude", """{"faces":[0],"distance":0,"inset":5}""", "inset")]
    [InlineData("extrude", """{"faces":[0],"distance":1,"inset":-1}""", "inset")]
    [InlineData("extrude", """{"faces":[0]}""", "distance")]
    [InlineData("extrude", """{"distance":1}""", "faces")]
    // RV-10: every argument-parsing branch, a list entry named by its place in words, numbers beyond float range.
    [InlineData("move", """{"faces":3,"by":[0,1,0]}""", "faces: expected an array of integer ids, got 3")]
    [InlineData("move", """{"faces":[3,"x"],"by":[0,1,0]}""", "faces: the 2nd listed entry: expected an integer id, got a string")]
    [InlineData("move", """{"faces":[3.5],"by":[0,1,0]}""", "faces: the 1st listed entry: expected an integer id, got 3.5")]
    [InlineData("move", """{"edges":{"a":1},"by":[0,1,0]}""", "edges: expected an array of [a, b] pairs, got an object")]
    [InlineData("move", """{"edges":[[0]],"by":[0,1,0]}""", "edges: the 1st listed entry: expected a pair")]
    [InlineData("move", """{"edges":[[0,1,2]],"by":[0,1,0]}""", "edges: the 1st listed entry: expected a pair")]
    [InlineData("move", """{"edges":[[0,1],["a",1]],"by":[0,1,0]}""", "edges: the 2nd listed entry: expected a pair")]
    [InlineData("move", """{"vertices":[0],"by":[0,"up",0]}""", "by[1]: expected a number, got a string")]
    [InlineData("move", """{"vertices":[0],"by":[1e39,0,0]}""", "by[0]: 1e39 is out of range")]
    [InlineData("extrude", """{"faces":[0],"distance":"far"}""", "distance: expected a number, got a string")]
    [InlineData("extrude", """{"faces":[0],"distance":1,"inset":true}""", "inset: expected a number, got a boolean")]
    [InlineData("extrude", """{"faces":[0],"distance":1e39}""", "distance: 1e39 is out of range")]
    [InlineData("extrude", """{"faces":[0],"distance":1e400}""", "distance: 1e400 is out of range")]
    [InlineData("extrude", """{"faces":[0],"distance":0,"inset":0}""", "distance, inset")]
    // RV-10: a misspelt or unknown argument is a problem, not silently dropped.
    [InlineData("extrude", """{"faces":[1],"distance":-0.2,"Inset":0.1}""", "Inset: not an argument of extrude; its arguments are faces, distance, inset")]
    [InlineData("move", """{"face":[3],"by":[0,1,0]}""", "face: not an argument of move; its arguments are vertices, edges, faces, by")]
    [InlineData("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]],"name":"tri"}""", "name: not an argument of define_mesh")]
    // RV-10: refused by the whole-mesh check, after the arguments passed: corner 6 past the far side folds two faces.
    [InlineData("move", """{"vertices":[6],"by":[-2,0,0]}""", "faces[1]: face is self-intersecting")]
    // RV-10: a wall the distance breaks is named by the face listed, not by the wall's new id.
    [InlineData("extrude", """{"faces":[1],"distance":1e-30}""", "faces[1]: the distance 1E-30 is too small for this face: its new wall faces[6]")]
    public async Task A_bad_edit_is_a_tool_error_naming_the_problem_and_changes_nothing(string tool, string arguments, string mention)
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
        var before = await MeshAsync(carve);

        ExpectError(await carve.CallToolAsync(tool, arguments), mention);

        Assert.Equal(before, await MeshAsync(carve));
        // The failed call was not a step: undo goes back over the move, not over it.
        Assert.Equal("move", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
    }

    [Fact]
    public async Task A_bad_edit_lists_every_problem()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var result = await carve.CallToolAsync("move", """{"vertices":[50],"faces":[60],"by":[1,0,0]}""");

        ExpectError(result, "vertices[50]", "faces[60]");
    }

    [Theory]
    [InlineData("get_mesh", "get_mesh")]
    [InlineData("undo", "undo")]
    [InlineData("render", "render; its arguments are view, direction, zoom, target")]
    public async Task An_unknown_argument_to_a_reading_tool_is_a_tool_error_and_changes_nothing(string tool, string says)
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
        var before = await MeshAsync(carve);

        var result = await carve.CallToolAsync(tool, """{"steps":2,"veiw":"back"}""");

        ExpectError(result, $"steps: not an argument of {says}", $"veiw: not an argument of {says}", "2 problem(s)");
        Assert.Equal(before, await MeshAsync(carve));
        Assert.Equal("move", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
    }

    [Fact]
    public async Task Ids_written_as_whole_numbers_with_a_fraction_or_exponent_are_accepted_as_define_mesh_does()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var moved = Ok(await carve.CallToolAsync("move", """{"faces":[3.0],"edges":[[0e0,1.00]],"by":[0,0.5,0]}"""));
        Assert.Equal([0, 1, 2, 3, 6, 7], moved.GetProperty("moved").EnumerateArray().Select(x => x.GetInt32()));
        var extruded = Ok(await carve.CallToolAsync("extrude", """{"faces":[1e0],"distance":0.25}"""));
        Assert.Equal(1, extruded.GetProperty("extruded")[0].GetProperty("face").GetInt32());
    }

    [Fact]
    public async Task The_get_mesh_sample_output_matches_the_real_output_on_the_cube()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        const string marker = "Output: ";
        var line = Description(tools, "get_mesh").Split('\n').Single(l => l.StartsWith(marker, StringComparison.Ordinal));
        var sample = JsonDocument.Parse(line[marker.Length..].Replace(", ...", "", StringComparison.Ordinal)).RootElement;
        var real = Ok(await carve.CallToolAsync("get_mesh", "{}"));

        static string Raw(JsonElement x) => JsonSerializer.Serialize(x);
        foreach (var v in sample.GetProperty("vertices").EnumerateArray())
            Assert.Equal(Raw(v), Raw(real.GetProperty("vertices")[v.GetProperty("id").GetInt32()]));
        foreach (var f in sample.GetProperty("faces").EnumerateArray())
            Assert.Equal(Raw(f), Raw(real.GetProperty("faces")[f.GetProperty("id").GetInt32()]));
        foreach (var e in sample.GetProperty("edges").EnumerateArray())
            Assert.Contains(Raw(e), real.GetProperty("edges").EnumerateArray().Select(Raw));
        foreach (var key in new[] { "closed", "bounds", "warnings" })
            Assert.Equal(Raw(sample.GetProperty(key)), Raw(real.GetProperty(key)));
    }

    [Fact]
    public async Task Undo_after_an_extrude_restores_the_exact_mesh_and_names_the_tool()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;
        var before = await MeshAsync(carve);
        Ok(await carve.CallToolAsync("extrude", """{"faces":[1],"distance":0.3,"inset":0.1}"""));
        Assert.NotEqual(before, await MeshAsync(carve));

        var body = Ok(await carve.CallToolAsync("undo", "{}"));

        Assert.Equal("extrude", body.GetProperty("undone").GetString());
        Assert.Equal(8, body.GetProperty("vertices").GetInt32());
        Assert.Equal(before, await MeshAsync(carve));
    }

    [Fact]
    public async Task Undo_can_take_back_define_mesh_but_not_the_first_one()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));

        var body = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("define_mesh", body.GetProperty("undone").GetString());
        Assert.Equal(8, body.GetProperty("vertices").GetInt32());

        ExpectError(await carve.CallToolAsync("undo", "{}"), "nothing to undo");
    }

    [Fact]
    public async Task The_hundred_and_first_undo_back_fails()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;
        var first = await MeshAsync(carve);
        for (var i = 0; i < 101; i++) Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.01,0]}"""));

        for (var i = 0; i < 100; i++) Ok(await carve.CallToolAsync("undo", "{}"));
        ExpectError(await carve.CallToolAsync("undo", "{}"), "nothing to undo");
        Assert.NotEqual(first, await MeshAsync(carve)); // the define_mesh step itself had already fallen off
    }

    [Fact]
    public async Task Tools_list_shows_each_schema()
    {
        var (carve, tools) = await StartAsync(define: false);
        using var _ = carve;

        var by = Tool(tools, "move").GetProperty("inputSchema").GetProperty("properties").GetProperty("by");
        Assert.Equal(3, by.GetProperty("minItems").GetInt32());
        Assert.Equal(3, by.GetProperty("maxItems").GetInt32());
        var pair = Tool(tools, "move").GetProperty("inputSchema").GetProperty("properties").GetProperty("edges").GetProperty("items");
        Assert.Equal(2, pair.GetProperty("minItems").GetInt32());
        Assert.Equal(2, pair.GetProperty("maxItems").GetInt32());
        Assert.Equal(["by"], Tool(tools, "move").GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(x => x.GetString()));

        var extrude = Tool(tools, "extrude").GetProperty("inputSchema");
        Assert.Equal(["faces", "distance"], extrude.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(0, extrude.GetProperty("properties").GetProperty("inset").GetProperty("default").GetInt32());

        foreach (var name in new[] { "get_mesh", "undo" })
            Assert.Equal("object", Tool(tools, name).GetProperty("inputSchema").GetProperty("type").GetString());
        foreach (var name in new[] { "move", "extrude", "get_mesh", "undo" })
            Assert.Contains("Worked example", Description(tools, name));
        foreach (var name in new[] { "define_mesh", "render", "move", "extrude", "get_mesh", "undo" })
            Assert.False(Tool(tools, name).GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean(), name);
        Assert.Equal(["view", "direction", "zoom", "target", "projection", "size", "mode", "highlight", "labels", "views", "columns"],
            Tool(tools, "render").GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Contains("ids are the positions given here", Description(tools, "define_mesh"));
    }

    // ---- the crate, T-10's 12-call sequence, through the client ----

    private static async Task<JsonElement> CallAsync(CarveProcess carve, string tool, string arguments) =>
        Ok(await carve.CallToolAsync(tool, arguments));

    /// <summary>The face's highest and lowest edges in Y, as vertex pairs, read from get_mesh.</summary>
    private static async Task<((int A, int B) Top, (int A, int B) Bottom)> TopAndBottomAsync(CarveProcess carve, int face)
    {
        var mesh = JsonDocument.Parse(await MeshAsync(carve)).RootElement;
        var y = mesh.GetProperty("vertices").EnumerateArray().Select(v => v.GetProperty("at")[1].GetSingle()).ToArray();
        var loop = mesh.GetProperty("faces")[face].GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var level = Enumerable.Range(0, loop.Length)
            .Select(k => (A: loop[k], B: loop[(k + 1) % loop.Length]))
            .Where(e => Math.Abs(y[e.A] - y[e.B]) < 1e-6f)
            .OrderBy(e => y[e.A])
            .ToArray();
        Assert.Equal(2, level.Length);
        return (level[1], level[0]);
    }

    [Fact]
    public async Task The_crate_is_built_in_twelve_calls_to_a_closed_72_vertex_70_face_mesh()
    {
        var (carve, _) = await StartAsync(); // 1: define_mesh, the example cube
        using var _ = carve;
        const string all = "[0,1,2,3,4,5]";

        await CallAsync(carve, "move", """{"faces":[3],"by":[0,-0.2,0]}""");                              // 2
        await CallAsync(carve, "extrude", $$"""{"faces":{{all}},"distance":0,"inset":0.12}""");            // 3
        await CallAsync(carve, "extrude", $$"""{"faces":{{all}},"distance":-0.08}""");                     // 4

        // -X: the slot's edges drawn together by moving their vertices.
        await CallAsync(carve, "extrude", """{"faces":[4],"distance":0,"inset":0.05}""");                  // 5
        var (top, bottom) = await TopAndBottomAsync(carve, 4);
        await CallAsync(carve, "move", $$"""{"vertices":[{{top.A}},{{top.B}}],"by":[0,-0.17,0]}""");       // 6
        await CallAsync(carve, "move", $$"""{"vertices":[{{bottom.A}},{{bottom.B}}],"by":[0,0.17,0]}"""); // 7
        await CallAsync(carve, "extrude", """{"faces":[4],"distance":0.1}""");                            // 8

        // +X: the same by moving the edges.
        await CallAsync(carve, "extrude", """{"faces":[5],"distance":0,"inset":0.05}""");                  // 9
        (top, bottom) = await TopAndBottomAsync(carve, 5);
        await CallAsync(carve, "move", $$"""{"edges":[[{{top.A}},{{top.B}}]],"by":[0,-0.17,0]}""");        // 10
        await CallAsync(carve, "move", $$"""{"edges":[[{{bottom.A}},{{bottom.B}}]],"by":[0,0.17,0]}""");  // 11
        var body = await CallAsync(carve, "extrude", """{"faces":[5],"distance":0.1}""");                 // 12

        Assert.Equal(72, body.GetProperty("vertices").GetInt32());
        Assert.Equal(70, body.GetProperty("faces").GetInt32());
        ExpectCleanClosed(body);
        var mesh = JsonDocument.Parse(await MeshAsync(carve)).RootElement;
        Assert.Equal(72, mesh.GetProperty("vertices").GetArrayLength());
        Assert.Equal(70, mesh.GetProperty("faces").GetArrayLength());
    }
}
