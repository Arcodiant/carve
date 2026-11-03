using System.Text.Json;

namespace Carve.Server.Tests;

[Trait("Tier", "regression")]
public class DefineMeshTests
{
    private const string Marker = "Worked example (a unit cube): ";

    private static Task<JsonElement> CallAsync(CarveProcess carve, string arguments) =>
        carve.CallToolAsync("define_mesh", arguments);

    private static async Task<JsonElement> ToolAsync(CarveProcess carve) =>
        (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "define_mesh");

    private static async Task<(CarveProcess, string Cube)> StartAsync()
    {
        var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        var description = (await ToolAsync(carve)).GetProperty("description").GetString()!;
        return (carve, description[(description.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length)..]);
    }

    [Fact]
    public async Task The_worked_example_in_the_description_is_accepted_as_a_closed_cube()
    {
        var (carve, example) = await StartAsync();
        using var _ = carve;

        var result = await CallAsync(carve, example);

        Assert.False(IsError(result), TextOf(result));
        var body = JsonDocument.Parse(TextOf(result)).RootElement;
        Assert.Equal(8, body.GetProperty("vertices").GetInt32());
        Assert.Equal(6, body.GetProperty("faces").GetInt32());
        Assert.Equal(12, body.GetProperty("edges").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(0, body.GetProperty("warnings").GetArrayLength());
        Assert.Equal([0f, 0f, 0f], body.GetProperty("bounds").GetProperty("min").EnumerateArray().Select(x => x.GetSingle()));
        Assert.Equal([1f, 1f, 1f], body.GetProperty("bounds").GetProperty("max").EnumerateArray().Select(x => x.GetSingle()));
    }

    [Fact]
    public async Task A_bad_index_is_a_tool_error_naming_the_element_and_all_problems_are_listed()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var result = await CallAsync(carve,
            """{"vertices":[[0,0,0],[1,0,0],[1,1,0]],"faces":[[0,1,2],[0,1,2],[0,1,9],[0,1]]}""");

        Assert.True(IsError(result));
        var text = TextOf(result);
        Assert.Contains("faces[2][2]", text);
        Assert.Contains("faces[3]", text);
    }

    [Fact]
    public async Task A_vertex_without_three_numbers_is_a_tool_error()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var result = await CallAsync(carve, """{"vertices":[[0,0,0],[1,0],[1,1,0]],"faces":[[0,1,2]]}""");

        Assert.True(IsError(result));
        Assert.Contains("vertices[1]", TextOf(result));
    }

    [Fact]
    public async Task A_failed_call_keeps_the_previous_mesh_and_an_open_quad_succeeds_with_a_warning()
    {
        var (carve, cube) = await StartAsync();
        using var _ = carve;

        await CallAsync(carve, cube);
        var failed = await CallAsync(carve, """{"vertices":[[0,0,0]],"faces":[[0,1,2]]}""");
        Assert.True(IsError(failed));
        Assert.Contains("unchanged", TextOf(failed));

        // The mesh itself, seen through the render caption: still the cube.
        var render = await carve.CallToolAsync("render", "{}");
        Assert.Equal("512x512, view front, 8 vertices, 6 faces", TextOf(render, 1));

        var quad = await CallAsync(carve, """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0]],"faces":[[0,1,2,3]]}""");
        Assert.False(IsError(quad), TextOf(quad));
        var body = JsonDocument.Parse(TextOf(quad)).RootElement;
        Assert.False(body.GetProperty("closed").GetBoolean());
        Assert.NotEqual(0, body.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public async Task Tools_list_shows_the_schema_and_the_description_rules()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var tool = await ToolAsync(carve);

        var description = tool.GetProperty("description").GetString()!;
        foreach (var phrase in new[] { "unitless", "Y is up", "counter-clockwise", "any number of sides", "REPLACES", "A face has no holes; build an opening from several faces around it." })
        {
            Assert.Contains(phrase, description);
        }

        var properties = tool.GetProperty("inputSchema").GetProperty("properties");
        Assert.Equal(2, properties.EnumerateObject().Count());
        var vertex = properties.GetProperty("vertices").GetProperty("items");
        Assert.Equal(3, vertex.GetProperty("minItems").GetInt32());
        Assert.Equal(3, vertex.GetProperty("maxItems").GetInt32());
        Assert.Equal("number", vertex.GetProperty("items").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("faces").GetProperty("items").GetProperty("items").GetProperty("type").GetString());
    }

    private const string Tri = """[[0,0,0],[1,0,0],[0,1,0]]""";

    public static TheoryData<string, string[], int> BadArguments => new()
    {
        { $$"""{"vertices":{{Tri}},"faces":[[0,1.5,2]]}""", ["faces[0][1]"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,99999999999,2]]}""", ["faces[0][1]"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,null,2]]}""", ["faces[0][1]"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,"1",2]]}""", ["faces[0][1]"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,1.00000000000000001,2]]}""", ["faces[0][1]", "1.00000000000000001"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":[[1e-400,1,2]]}""", ["faces[0][0]"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,1.5e-400,2]]}""", ["faces[0][1]"], 1 },
        { """{"vertices":[[0,0,0],[1,null,0],[0,1,0]],"faces":[[0,1,2]]}""", ["vertices[1][1]"], 1 },
        { """{"vertices":[[0,0,0],[1,"x",0],[0,1,0]],"faces":[[0,1,2]]}""", ["vertices[1][1]"], 1 },
        { $$"""{"vertices":null,"faces":[[0,1,2]]}""", ["vertices"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":null}""", ["faces"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":{"a":1} }""", ["faces"], 1 },
        { $$"""{"vertices":{{Tri}}}""", ["faces"], 1 },
        { """{"faces":[[0,1,2]]}""", ["vertices"], 1 },
        { "{}", ["vertices", "faces"], 2 },
        { """{"vertices":[],"faces":[[0,1,2]]}""", ["faces[0][0]", "no vertices"], 3 },
        { """{"vertices":[[0,0,0],[1,0,0],[0,1,0],[1,1,0]],"faces":[[0,1.0,2],[0,1,2.5,3]]}""", ["faces[1][2]"], 1 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,9]]}""", ["faces[0]:", "faces[0][1]"], 2 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,0,9]]}""", ["faces[0][1]", "faces[0][2]"], 2 },
        { $$"""{"vertices":{{Tri}},"faces":[[0,0,1.5]]}""", ["faces[0][1]", "faces[0][2]"], 2 },
    };

    [Theory]
    [MemberData(nameof(BadArguments))]
    public async Task Bad_arguments_are_a_tool_error_naming_the_element(string arguments, string[] mentions, int problems)
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var result = await CallAsync(carve, arguments);

        Assert.True(IsError(result), result.ToString());
        var text = TextOf(result);
        Assert.StartsWith($"define_mesh failed with {problems} problem(s)", text);
        Assert.All(mentions, m => Assert.Contains(m, text));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1e0")]
    [InlineData("10e-1")]
    [InlineData("0.1e1")]
    public async Task An_integral_index_written_any_way_is_accepted(string one)
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var result = await CallAsync(carve, $$"""{"vertices":{{Tri}},"faces":[[0,{{one}},2]]}""");

        Assert.False(IsError(result), TextOf(result));
    }

    [Fact]
    public async Task Every_problem_in_the_arguments_comes_back_together()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var result = await CallAsync(carve,
            """{"vertices":[[0,0,0],[1,"x",0],[0,1],[0,1,0]],"faces":[[0,1.5,2],null,[0,1,9],[0,"1",2]]}""");

        var text = TextOf(result);
        Assert.True(IsError(result));
        foreach (var element in new[] { "vertices[1][1]", "vertices[2]", "faces[0][1]", "faces[1]", "faces[2][2]", "faces[3][1]" })
        {
            Assert.Contains(element, text);
        }
        Assert.StartsWith("define_mesh failed with 6 problem(s)", text);
    }

    [Fact]
    public async Task A_null_face_is_reported_once()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var text = TextOf(await CallAsync(carve, $$"""{"vertices":{{Tri}},"faces":[null]}"""));

        Assert.StartsWith("define_mesh failed with 1 problem(s)", text);
    }

    [Fact]
    public async Task An_integral_index_written_with_a_fraction_is_index_1()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var result = await CallAsync(carve, $$"""{"vertices":{{Tri}},"faces":[[0,1.0,2]]}""");

        Assert.False(IsError(result), TextOf(result));
    }

    [Fact]
    public async Task The_published_input_schema_is_the_typed_one()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var schema = (await ToolAsync(carve)).GetProperty("inputSchema").GetRawText();

        Assert.Equal(
            """{"type":"object","properties":{"vertices":{"description":"Vertex positions, each exactly three numbers [x, y, z]. Unitless; Y is up.","type":"array","items":{"type":"array","items":{"type":"number"},"minItems":3,"maxItems":3}},"faces":{"description":"Faces, each a list of at least 3 indices into vertices, counter-clockwise seen from outside.","type":"array","items":{"type":"array","items":{"type":"integer"}}}},"required":["vertices","faces"],"additionalProperties":false}""",
            schema);
    }
}
