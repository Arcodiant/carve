using System.Text.Json;

namespace Carve.Server.Tests;

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
        foreach (var phrase in new[] { "unitless", "Y is up", "counter-clockwise", "any number of sides", "REPLACES" })
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
}
