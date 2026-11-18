using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carve.Server.Tests;

/// <summary>select through the real server: its worked examples, its errors, and a selection feeding the edit tools.</summary>
[Trait("Tier", "regression")]
public class SelectToolTests
{
    private const string Example = "Example: ";

    private const string Result = "Result: ";

    private static async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync(bool define = true)
    {
        var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (define) await OkAsync(carve, "define_mesh", ToolList.Cube(tools));
        return (carve, tools);
    }

    private static async Task<string> OkAsync(CarveProcess carve, string tool, string arguments)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        Assert.False(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return CarveProcess.TextOf(result);
    }

    private static async Task<string> RefusedAsync(CarveProcess carve, string arguments)
    {
        var result = await carve.CallToolAsync("select", arguments);
        Assert.True(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return CarveProcess.TextOf(result);
    }

    private static string Select(string kind, string where) => JsonSerializer.Serialize(new { kind, where });

    /// <summary>A result's list as raw JSON, to be passed on untouched.</summary>
    private static string List(string result, string name) => JsonDocument.Parse(result).RootElement.GetProperty(name).GetRawText();

    private static int[] Ints(string result, string name) =>
        JsonDocument.Parse(result).RootElement.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToArray();

    [Theory]
    [InlineData("vertices", "vertices", "y == 1 and x < 0.5", new[] { 3, 7 })]
    [InlineData("edges", "edges", "y == 1 and z == 0.5", new[] { 2, 3, 6, 7 })]
    [InlineData("faces", "faces", "normal.x > 0.9", new[] { 1, 2, 5, 6 })]
    public async Task What_select_returns_is_what_move_takes(string kind, string argument, string where, int[] moved)
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;

        var selected = await OkAsync(carve, "select", Select(kind, where));
        var result = await OkAsync(carve, "move", $$"""{"{{argument}}":{{List(selected, kind)}},"by":[0.25,0,0]}""");

        Assert.Equal(moved, Ints(result, "moved"));
        if (kind == "vertices") Assert.Equal(Ints(selected, kind), Ints(result, "moved"));
    }

    [Fact]
    public async Task What_select_returns_for_faces_is_what_extrude_takes()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;

        var selected = await OkAsync(carve, "select", Select("faces", "normal.y > 0.9 or normal.x > 0.9"));
        Assert.Equal([3, 5], Ints(selected, "faces"));
        var body = JsonDocument.Parse(await OkAsync(carve, "extrude", $$"""{"faces":{{List(selected, "faces")}},"distance":0.5}""")).RootElement;

        Assert.True(body.GetProperty("closed").GetBoolean());
        // The caps keep their ids, so the selection still names them.
        Assert.Equal("""{"kind":"faces","faces":[3],"count":1,"of":14}""", await OkAsync(carve, "select", Select("faces", "id == 3 and y == 1.5")));
    }

    [Theory]
    [InlineData("vertices", "all", """{"kind":"vertices","vertices":[0,1,2,3,4,5,6,7],"count":8,"of":8}""")]
    [InlineData("vertices", "none", """{"kind":"vertices","vertices":[],"count":0,"of":8}""")]
    [InlineData("edges", "y == 1 and z == 0.5", """{"kind":"edges","edges":[[2,6],[3,7]],"count":2,"of":12}""")]
    [InlineData("edges", "a == 0", """{"kind":"edges","edges":[[0,1],[0,3],[0,4]],"count":3,"of":12}""")]
    [InlineData("edges", "none", """{"kind":"edges","edges":[],"count":0,"of":12}""")]
    [InlineData("faces", "id > 3", """{"kind":"faces","faces":[4,5],"count":2,"of":6}""")]
    [InlineData("faces", "none", """{"kind":"faces","faces":[],"count":0,"of":6}""")]
    public async Task A_result_is_the_list_named_for_its_kind_with_count_and_of(string kind, string where, string expected)
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Assert.Equal(expected, await OkAsync(carve, "select", Select(kind, where)));
    }

    [Fact]
    public async Task With_no_mesh_yet_select_says_so()
    {
        var (carve, _) = await StartAsync(define: false);
        using var _carve = carve;
        Assert.Equal(
            "select: there is no mesh yet; call define_mesh first, or open to read a saved .carve file",
            await RefusedAsync(carve, Select("faces", "all")));
    }

    [Theory]
    [InlineData("""{"kind":"bones","where":"all"}""", "kind: 'bones' is not a kind; use vertices, edges or faces")]
    [InlineData("""{"kind":"FACES","where":"all"}""", "kind: 'FACES' is not a kind; use vertices, edges or faces")]
    [InlineData("""{"where":"all"}""", "kind: required but missing")]
    [InlineData("""{"kind":3,"where":"all"}""", "kind: expected a string, got 3")]
    [InlineData("""{"kind":"faces"}""", "where: required but missing")]
    [InlineData("""{"kind":"faces","where":null}""", "where: required but missing")]
    [InlineData("""{"kind":"faces","where":5}""", "where: expected a string, got 5")]
    [InlineData("""{"kind":"faces","where":["all"]}""", "where: expected a string, got an array")]
    [InlineData("""{"kind":"faces","where":"all","limit":3}""", "limit: not an argument of select; its arguments are kind, where, mesh")]
    [InlineData("""{"kind":"faces","where":""}""", "where: where is empty")]
    [InlineData("""{"kind":"faces","where":"  "}""", "where: where is empty")]
    [InlineData("""{"kind":"faces","where":"y>1 and z >> 2"}""", "where: at character 11: '>>' is not an operator; use <, <=, ==, !=, >= or >")]
    [InlineData("""{"kind":"faces","where":"centre > 1"}""", "where: at character 1: 'centre' is not a property of faces; faces have id, x, y, z, min, max, normal, vertices, area, non_planar, open")]
    [InlineData("""{"kind":"edges","where":"id == 1"}""", "where: at character 1: 'id' is not a property of edges; edges have x, y, z, min, max, a, b, length, faces, open, manifold, nonmanifold")]
    [InlineData("""{"kind":"vertices","where":"area > 1"}""", "where: at character 1: 'area' is not a property of vertices; vertices have id, x, y, z, min, max, faces, edges, unattached, boundary")]
    [InlineData("""{"kind":"faces","where":"normal.w > 0"}""", "where: at character 8: 'normal.w' is not an axis; use .x, .y or .z")]
    [InlineData("""{"kind":"faces","where":"min > 0"}""", "where: at character 1: 'min' needs an axis; use .x, .y or .z")]
    [InlineData("""{"kind":"faces","where":"not open > 1"}""", "where: at character 5: a flag cannot be compared with a number")]
    [InlineData("""{"kind":"faces","where":"area"}""", "where: at character 1: 'area' is a number; compare it with <, <=, ==, !=, >= or >")]
    [InlineData("""{"kind":"faces","where":"y > 1 and x <"}""", "where: at character 14: expected a number")]
    [InlineData("""{"kind":"faces","where":"y > 1e99"}""", "where: at character 5: 1e99 is out of range: a number must lie within ±3.40282347E+38")]
    [InlineData("""{"kind":"faces","where":"(y > 1 and x < 0.25"}""", "where: at character 20: expected ')'")]
    [InlineData("""{"kind":"faces","where":"y > 1 and"}""", "where: at character 10: expected a property, a flag, all, none, not or '('")]
    [InlineData("""{"kind":"faces","where":"y > 1 )"}""", "where: at character 7: expected 'and', 'or' or the end of the expression")]
    public async Task A_bad_call_is_a_tool_error_and_the_mesh_is_untouched(string arguments, string problem)
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var before = CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        var text = await RefusedAsync(carve, arguments);

        Assert.Equal($"select failed with 1 problem(s); the current mesh is unchanged:\n{problem}", text);
        Assert.Equal(before, CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", "{}")));
    }

    [Fact]
    public async Task Every_problem_of_a_call_is_listed_at_once()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;

        Assert.Equal(
            "select failed with 3 problem(s); the current mesh is unchanged:\n"
            + "limit: not an argument of select; its arguments are kind, where, mesh\n"
            + "kind: 'bones' is not a kind; use vertices, edges or faces\n"
            + "where: at character 3: '>>' is not an operator; use <, <=, ==, !=, >= or >",
            await RefusedAsync(carve, """{"kind":"bones","where":"y >> 1","limit":3}"""));

        Assert.Equal(
            "select failed with 2 problem(s); the current mesh is unchanged:\n"
            + "where: at character 1: 'centre' is not a property of faces; faces have id, x, y, z, min, max, normal, vertices, area, non_planar, open\n"
            + "where: at character 19: expected a number",
            await RefusedAsync(carve, Select("faces", "centre > 1 or y > x")));
    }

    [Fact]
    public async Task Text_nested_past_any_stack_is_a_tool_error_and_the_server_stays_up()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;

        var text = await RefusedAsync(carve, Select("faces", new string('(', 500_000)));

        Assert.Contains("where: at character 201: nested too deeply; 'not' and '(' go 200 levels deep at most", text);
        Assert.Equal("""{"kind":"faces","faces":[3],"count":1,"of":6}""", await OkAsync(carve, "select", Select("faces", "normal.y > 0.9")));
    }

    [Fact]
    public async Task Select_leaves_undo_as_it_was()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        await OkAsync(carve, "move", """{"vertices":[0],"by":[0,-0.5,0]}""");

        await OkAsync(carve, "select", Select("faces", "all"));
        await RefusedAsync(carve, Select("faces", "centre > 1"));

        Assert.Contains("\"undone\":\"move\"", await OkAsync(carve, "undo", "{}"));
        Assert.Contains("nothing to undo", CarveProcess.TextOf(await carve.CallToolAsync("undo", "{}")));
    }

    /// <summary>
    /// The version is not on the wire. It moves only through the store's writes, each of which also moves undo, which the test
    /// above holds to; this one holds the tool to the store's one read, so no write can arrive in it unnoticed.
    /// </summary>
    [Fact]
    public void Select_reads_the_store_and_never_writes_it()
    {
        var source = File.ReadAllText(Path.Combine(Paths.RepoRoot, "src", "Carve.Server", "SelectTool.cs"));
        var uses = Regex.Matches(source, @"\bstore\.\w+").Select(m => m.Value).Distinct().ToArray();
        Assert.Equal(["store.Current"], uses);
        Assert.DoesNotContain("Edit(store", source);
    }
}
