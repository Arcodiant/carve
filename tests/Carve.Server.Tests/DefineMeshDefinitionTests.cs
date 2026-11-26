using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public class DefineMeshDefinitionTests
{
    private const string Marker = "Worked example (a unit cube): ";

    private static Task<JsonElement> CallAsync(ToolHarness carve, string arguments) =>
        carve.CallToolAsync("define_mesh", arguments);

    private static async Task<JsonElement> ToolAsync(ToolHarness carve) =>
        (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "define_mesh");

    private static async Task<(ToolHarness, string Cube)> StartAsync()
    {
        var carve = new ToolHarness();
        try
        {
            await carve.InitializeAsync();
            var description = (await ToolAsync(carve)).GetProperty("description").GetString()!;
            return (carve, description[(description.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length)..]);
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
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
    public async Task Tools_list_shows_the_schema_and_the_description_rules()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var tool = await ToolAsync(carve);

        var description = tool.GetProperty("description").GetString()!;
        foreach (var phrase in new[] { "1 unit = 1 m", "Y is up", "counter-clockwise", "any number of sides", "REPLACES", "A face may have holes", "CLOCKWISE seen from outside", "a hole no face fills shows as open edges" })
        {
            Assert.Contains(phrase, description);
        }

        var properties = tool.GetProperty("inputSchema").GetProperty("properties");
        Assert.Equal(5, properties.EnumerateObject().Count());
        var vertex = properties.GetProperty("vertices").GetProperty("items");
        Assert.Equal(3, vertex.GetProperty("minItems").GetInt32());
        Assert.Equal(3, vertex.GetProperty("maxItems").GetInt32());
        Assert.Equal("number", vertex.GetProperty("items").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("faces").GetProperty("items").GetProperty("anyOf")[0].GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public async Task The_published_input_schema_is_the_typed_one()
    {
        var (carve, _) = await StartAsync();
        using var _ = carve;

        var schema = (await ToolAsync(carve)).GetProperty("inputSchema").GetRawText();

        Assert.Equal(
            """{"type":"object","properties":{"name":{"description":"The name of the mesh, non-empty and without a colon (a skinned export names materials mesh:group, so such a name is refused); left out, it is mesh. An existing name replaces that mesh in its place; a new one adds a mesh after the others.","type":"string"},"vertices":{"description":"Vertex positions, each exactly three numbers [x, y, z]. In metres (1 unit = 1 m); Y is up.","type":"array","items":{"type":"array","items":{"type":"number"},"minItems":3,"maxItems":3}},"faces":{"description":"Faces, each a list of at least 3 indices into vertices, counter-clockwise seen from outside; or, for a face with holes, {loop: [...], holes: [[...], ...]}: the outer loop counter-clockwise and each hole clockwise, seen from outside.","type":"array","items":{"anyOf":[{"type":"array","items":{"type":"integer"}},{"type":"object","properties":{"loop":{"type":"array","items":{"type":"integer"}},"holes":{"type":"array","items":{"type":"array","items":{"type":"integer"}}}},"required":["loop","holes"],"additionalProperties":false}]}},"drop_targets":{"description":"Default false. Replacing a mesh that has morph targets is refused unless this is true, which drops the targets with the old mesh. Has no effect on a new name or a mesh with no targets.","type":"boolean"},"verbosity":{"description":"How much the result says. \u0022summary\u0022, the default: the result as always, a kind of warning with 4 or more listed as one line (its count, the worst case, three samples and the select query that names them all). \u0022full\u0022: the same, with every warning one per line. \u0022silent\u0022: only {\u0022ok\u0022:true}, no counts, ids or warnings, and the change is made all the same (one undo step); check answers {\u0022ok\u0022:\u003Cits own ok\u003E}, and export answers that JSON though its result is otherwise a line of text. A tool error is returned whole at every level. Any other value is a tool error.","type":"string","enum":["silent","summary","full"]}},"required":["vertices","faces"],"additionalProperties":false}""",
            schema);
    }
}
