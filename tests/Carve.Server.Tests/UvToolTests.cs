using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>set_uvs, mark_edges with seam, and get_mesh's uvs and seams, through the real tools (T-54).</summary>
[Trait("Tier", "regression")]
public sealed class UvToolTests() : TempFolderTests("carve-uvs")
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private const string Holed =
        """{"vertices":[[0,0,0],[4,0,0],[4,4,0],[0,4,0],[1,1,0],[1,3,0],[3,3,0],[3,1,0]],"faces":[{"loop":[0,3,2,1],"holes":[[4,7,6,5]]}]}""";

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static async Task<CarveProcess> StartAsync(string mesh = Cube, string? root = null)
    {
        var carve = new CarveProcess([], workingDirectory: root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", mesh));
        return carve;
    }

    private static async Task<string> Refused(CarveProcess carve, string tool, string arguments)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static string Pairs(JsonElement list) => string.Join(" ", list.EnumerateArray().Select(p => $"{p[0]}-{p[1]}"));

    private const string Face0 = """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}""";

    [Fact]
    public async Task The_described_examples_run_and_read_back()
    {
        using var carve = await StartAsync();
        var tools = await carve.ListToolsAsync();
        var set = Ok(await carve.CallToolAsync("set_uvs", ToolList.Examples(tools, "set_uvs").Single()));
        Assert.Equal(1, set.GetProperty("set").GetInt32());
        foreach (var example in ToolList.Examples(tools, "mark_edges")) Ok(await carve.CallToolAsync("mark_edges", example));
        Assert.Equal(2, ToolList.Examples(tools, "mark_edges").Length);
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal("0-3", Pairs(mesh.GetProperty("seams")));
    }

    [Fact]
    public async Task Set_and_clear_report_counts_and_undo_restores()
    {
        using var carve = await StartAsync();
        var before = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.False(before.TryGetProperty("uvs", out _));
        Assert.False(before.TryGetProperty("seams", out _));

        var set = Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]},{"face":2,"uv":[[0,0],[2.5,0],[2.5,-1],[0,-1]]}]}"""));
        Assert.Equal(2, set.GetProperty("set").GetInt32());
        Assert.Equal(0, set.GetProperty("cleared").GetInt32());
        Assert.Equal(4, set.GetProperty("unmapped").GetInt32());

        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var uvs = mesh.GetProperty("uvs");
        Assert.Equal(2, uvs.GetArrayLength());
        Assert.Equal(0, uvs[0].GetProperty("face").GetInt32());
        Assert.Equal(1, uvs[0].GetProperty("uv")[1][1].GetDouble());
        Assert.Equal(2, uvs[1].GetProperty("face").GetInt32());
        Assert.Equal(2.5, uvs[1].GetProperty("uv")[1][0].GetDouble());
        Assert.Equal(-1, uvs[1].GetProperty("uv")[2][1].GetDouble());

        var cleared = Ok(await carve.CallToolAsync("set_uvs", """{"clear":[0,3]}"""));
        Assert.Equal(0, cleared.GetProperty("set").GetInt32());
        Assert.Equal(2, cleared.GetProperty("cleared").GetInt32());
        Assert.Equal(5, cleared.GetProperty("unmapped").GetInt32());
        Assert.Equal(1, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("uvs").GetArrayLength());

        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(2, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("uvs").GetArrayLength());
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.False(Ok(await carve.CallToolAsync("get_mesh", "{}")).TryGetProperty("uvs", out _));

        // Set and clear in one call: one step for undo.
        Ok(await carve.CallToolAsync("set_uvs", Face0));
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":1,"uv":[[0,0],[1,0],[1,1],[0,1]]}],"clear":[0]}"""));
        var both = Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("uvs");
        Assert.Equal(1, both.GetArrayLength());
        Assert.Equal(1, both[0].GetProperty("face").GetInt32());
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(0, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("uvs")[0].GetProperty("face").GetInt32());
    }

    [Theory]
    [InlineData("""{"faces":[{"face":9,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}""", "faces[9]")]
    [InlineData("""{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1]]}]}""", "faces[0]: 3 UVs given, but the face has 4 corners")]
    [InlineData("""{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0],[0,0]]}]}""", "faces[0]: 5 UVs given, but the face has 4 corners")]
    [InlineData("""{"faces":[{"face":0,"uv":[[0,0],[0,1],[1e99,1],[1,0]]}]}""", "faces[0].uv[2][0]: 1e99 is not finite")]
    [InlineData("""{"faces":[{"face":0,"uv":[[0,0],[0,"a"],[1,1],[1,0]]}]}""", "faces[0].uv[1][1]: expected a number")]
    [InlineData("""{"faces":[{"face":0,"uv":[[0,0],[0,1,1],[1,1],[1,0]]}]}""", "faces[0].uv[1]: a UV is a pair of numbers")]
    [InlineData("""{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}],"clear":[0]}""", "faces[0]: face 0 is both set and cleared")]
    [InlineData("""{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]},{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}""", "faces[0]: face 0 is listed more than once")]
    [InlineData("""{"clear":[12]}""", "clear[12]: face 12 is not a face of the mesh")]
    [InlineData("""{"faces":[{"face":-1,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}""", "faces[-1]: face -1 is not a face of the mesh, which has 6")]
    [InlineData("""{"clear":[-1]}""", "clear[-1]: face -1 is not a face of the mesh, which has 6")]
    [InlineData("""{"faces":[{"face":0}]}""", "faces[0].uv: required but missing")]
    [InlineData("""{"faces":[{"uv":[]}]}""", "face: required but missing")]
    [InlineData("""{"faces":[{"face":0,"uv":[],"extra":1}]}""", "extra: not a key of an entry")]
    [InlineData("""{"faces":[3]}""", "expected {\"face\": id")]
    [InlineData("""{"clear":[0],"color":1}""", "color: not an argument of set_uvs")]
    [InlineData("""{}""", "nothing to do")]
    public async Task Every_error_names_its_element_and_changes_nothing(string arguments, string expected)
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("set_uvs", Face0));
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        var text = await Refused(carve, "set_uvs", arguments);

        Assert.Contains(expected, text);
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        // One undo step only: the earlier set is the last step still.
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.False(Ok(await carve.CallToolAsync("get_mesh", "{}")).TryGetProperty("uvs", out _));
    }

    [Fact]
    public async Task A_failed_call_that_would_have_set_one_face_and_failed_another_sets_neither()
    {
        using var carve = await StartAsync();
        var text = await Refused(carve, "set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]},{"face":1,"uv":[[0,0]]}]}""");
        Assert.Contains("faces[1]: 1 UVs given, but the face has 4 corners", text);
        Assert.False(Ok(await carve.CallToolAsync("get_mesh", "{}")).TryGetProperty("uvs", out _));
    }

    [Fact]
    public async Task A_deleted_face_cannot_be_given_or_cleared()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("delete", """{"faces":[5]}"""));
        Assert.Contains("faces[5]: face 5 is deleted", await Refused(carve, "set_uvs", """{"faces":[{"face":5,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}"""));
        Assert.Contains("clear[5]: face 5 is deleted", await Refused(carve, "set_uvs", """{"clear":[5]}"""));
        var set = Ok(await carve.CallToolAsync("set_uvs", Face0));
        Assert.Equal(4, set.GetProperty("unmapped").GetInt32());
    }

    /// <summary>RV-43, the person's ruling (2026-11-14): set_uvs, project_uvs and unwrap word the face and into checks once, in Core.</summary>
    [Fact]
    public async Task The_uv_tools_word_the_face_and_into_refusals_alike()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("delete", """{"faces":[5]}"""));
        static string Only(string text) => Assert.Single(text.Split('\n').Skip(1));
        const string uv = "[[0,0],[0,1],[1,1],[1,0]]";

        Assert.Equal("faces[5]: face 5 is deleted", Only(await Refused(carve, "set_uvs", $$"""{"faces":[{"face":5,"uv":{{uv}}}]}""")));
        Assert.Equal("clear[5]: face 5 is deleted", Only(await Refused(carve, "set_uvs", """{"clear":[5]}""")));
        Assert.Equal("faces[5]: face 5 is deleted", Only(await Refused(carve, "project_uvs", """{"method":"box","faces":[5]}""")));
        Assert.Equal("faces[5]: face 5 is deleted", Only(await Refused(carve, "unwrap", """{"faces":[5]}""")));

        Assert.Equal("faces[9]: face 9 is not a face of the mesh, which has 6", Only(await Refused(carve, "set_uvs", $$"""{"faces":[{"face":9,"uv":{{uv}}}]}""")));
        Assert.Equal("clear[9]: face 9 is not a face of the mesh, which has 6", Only(await Refused(carve, "set_uvs", """{"clear":[9]}""")));
        Assert.Equal("faces[9]: face 9 is not a face of the mesh, which has 6", Only(await Refused(carve, "project_uvs", """{"method":"box","faces":[9]}""")));
        Assert.Equal("faces[9]: face 9 is not a face of the mesh, which has 6", Only(await Refused(carve, "unwrap", """{"faces":[9]}""")));

        Assert.Equal("into: expected 4 numbers [u0, v0, u1, v1], got 3", Only(await Refused(carve, "project_uvs", """{"method":"box","into":[0,0,1]}""")));
        Assert.Equal("into: expected 4 numbers [u0, v0, u1, v1], got 3", Only(await Refused(carve, "unwrap", """{"into":[0,0,1]}""")));
        Assert.Equal("into: [0, 0, 0.1, 0] has no area; u1 must exceed u0 and v1 exceed v0", Only(await Refused(carve, "project_uvs", """{"method":"box","into":[0,0,0.1,0]}""")));
        Assert.Equal("into: [0, 0, 0.1, 0] has no area; u1 must exceed u0 and v1 exceed v0", Only(await Refused(carve, "unwrap", """{"into":[0,0,0.1,0]}""")));
    }

    [Fact]
    public async Task The_uv_tools_word_an_into_element_out_of_range_alike()
    {
        using var carve = await StartAsync();
        const string expected = "into[2]: 1e99 is out of range: an into element must be a finite number within ±3.40282347E+38";
        Assert.Equal(expected, Assert.Single((await Refused(carve, "project_uvs", """{"method":"box","into":[0,0,1e99,1]}""")).Split('\n').Skip(1)));
        Assert.Equal(expected, Assert.Single((await Refused(carve, "unwrap", """{"into":[0,0,1e99,1]}""")).Split('\n').Skip(1)));
    }

    [Fact]
    public async Task A_holed_face_takes_one_pair_per_corner_hole_corners_included()
    {
        using var carve = await StartAsync(Holed);
        var text = await Refused(carve, "set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}""");
        Assert.Contains("faces[0]: 4 UVs given, but the face has 8 corners (its loop 4, holes 4)", text);
        var set = Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0],[0.25,0.25],[0.25,0.75],[0.75,0.75],[0.75,0.25]]}]}"""));
        Assert.Equal(0, set.GetProperty("unmapped").GetInt32());
        Assert.Equal(8, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("uvs")[0].GetProperty("uv").GetArrayLength());
        var tools = await carve.ListToolsAsync();
        Assert.Contains("each hole", ToolList.Description(tools, "set_uvs"));
    }

    [Fact]
    public async Task Mark_edges_seam_alone_hard_alone_and_both_are_independent()
    {
        using var carve = await StartAsync();
        var seamOnly = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[1,0],[3,0]],"seam":true}"""));
        Assert.Equal("0-1 0-3", Pairs(seamOnly.GetProperty("seams")));
        Assert.Equal(0, seamOnly.GetProperty("hard_edges").GetArrayLength());
        Assert.Equal("0-1 0-3", Pairs(seamOnly.GetProperty("marked")));

        var hardOnly = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[1,2]],"hard":true}"""));
        Assert.Equal("1-2", Pairs(hardOnly.GetProperty("hard_edges")));
        Assert.False(hardOnly.TryGetProperty("seams", out _));
        Assert.Equal("0-1 0-3", Pairs(Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("seams")));

        var both = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[2,3]],"hard":true,"seam":true}"""));
        Assert.Equal("1-2 2-3", Pairs(both.GetProperty("hard_edges")));
        Assert.Equal("0-1 0-3 2-3", Pairs(both.GetProperty("seams")));

        // seam false clears the seam and leaves the hard mark; hard false clears the hard mark and leaves the seam.
        var unseam = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[2,3]],"seam":false}"""));
        Assert.Equal("1-2 2-3", Pairs(unseam.GetProperty("hard_edges")));
        Assert.Equal("0-1 0-3", Pairs(unseam.GetProperty("seams")));
        var unhard = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[2,3]],"hard":false,"seam":true}"""));
        Assert.Equal("1-2", Pairs(unhard.GetProperty("hard_edges")));
        Assert.Equal("0-1 0-3 2-3", Pairs(unhard.GetProperty("seams")));

        // T-42's call without either flag still makes the edges hard.
        var plain = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[4,5]]}"""));
        Assert.Equal("1-2 4-5", Pairs(plain.GetProperty("hard_edges")));
        Assert.False(plain.TryGetProperty("seams", out _));
        Assert.Equal("0-1 0-3 2-3", Pairs(Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("seams")));

        // Each call was one step for undo.
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("1-2", Pairs(Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[1,2]],"hard":true}""")).GetProperty("hard_edges")));
    }

    [Fact]
    public async Task Mark_edges_refuses_a_non_edge_for_a_seam_naming_the_pair_and_changes_nothing()
    {
        using var carve = await StartAsync();
        var text = await Refused(carve, "mark_edges", """{"edges":[[0,1],[6,0]],"seam":true}""");
        Assert.Contains("edges[0-6]: not an edge of the mesh; select with kind edges lists the real edges", text);
        Assert.False(Ok(await carve.CallToolAsync("get_mesh", "{}")).TryGetProperty("seams", out _));
        Assert.Contains("seam: expected true or false", await Refused(carve, "mark_edges", """{"edges":[[0,1]],"seam":"yes"}"""));
        Assert.Contains("color: not an argument of mark_edges; its arguments are edges, hard, seam, mesh", await Refused(carve, "mark_edges", """{"edges":[[0,1]],"color":1}"""));
    }

    [Fact]
    public async Task The_file_keeps_what_get_mesh_reports()
    {
        using var carve = await StartAsync(root: Root);
        Ok(await carve.CallToolAsync("set_uvs", Face0));
        Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[0,3]],"seam":true}"""));
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.False(IsError(await carve.CallToolAsync("save", """{"path":"uv.carve"}""")));
        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Assert.False(Ok(await carve.CallToolAsync("get_mesh", "{}")).TryGetProperty("uvs", out _));
        Assert.False(IsError(await carve.CallToolAsync("open", """{"path":"uv.carve"}""")));
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
    }

    [Fact]
    public async Task The_schemas_and_descriptions_name_the_new_arguments_and_the_rules()
    {
        using var carve = await StartAsync();
        var tools = await carve.ListToolsAsync();
        var set = ToolList.Tool(tools, "set_uvs");
        var properties = set.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(properties.TryGetProperty("faces", out _));
        Assert.True(properties.TryGetProperty("clear", out _));
        Assert.True(properties.TryGetProperty("mesh", out _));
        Assert.True(ToolList.Tool(tools, "mark_edges").GetProperty("inputSchema").GetProperty("properties").TryGetProperty("seam", out _));
        foreach (var tool in new[] { "set_uvs", "mark_edges" })
        {
            var text = ToolList.Description(tools, tool);
            Assert.Contains("top-left", text);
            Assert.Contains("v down", text);
            Assert.Contains("needs no seam", text);
            Assert.Contains("Example: ", text);
        }
        var setText = ToolList.Description(tools, "set_uvs");
        Assert.Contains("corner", setText);
        Assert.Contains("tiles", setText);
        Assert.Contains("unwrap cuts", ToolList.Description(tools, "mark_edges"));
        Assert.Contains("uvs:", ToolList.Description(tools, "get_mesh"));
        Assert.Contains("seams:", ToolList.Description(tools, "get_mesh"));
    }
}
