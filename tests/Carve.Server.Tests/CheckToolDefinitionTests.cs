extern alias carve;
using System.Text.Json;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public class CheckToolDefinitionTests
{
    private static async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness();
        try
        {
            await carve.InitializeAsync();
            return (carve, await carve.ListToolsAsync());
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return JsonDocument.Parse(CarveProcess.TextOf(result)).RootElement;
    }

    [Fact]
    public async Task Check_on_the_cube_is_the_worked_example_to_the_character()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));

        const string marker = "Output: ";
        var output = Description(tools, "check").Split('\n').Single(l => l.StartsWith(marker, StringComparison.Ordinal))[marker.Length..];
        var result = await carve.CallToolAsync("check", Examples(tools, "check").Single());

        Assert.Equal(output, CarveProcess.TextOf(result));
        Assert.Contains("\"triangles\":12", output);
        Assert.Contains("\"area\":6", output);
        Assert.Contains("\"volume\":1", output);
    }

    [Fact]
    public async Task Any_argument_is_a_tool_error_and_the_schema_is_closed()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));

        var result = await carve.CallToolAsync("check", """{"deep":true}""");

        Assert.True(CarveProcess.IsError(result));
        Assert.Contains("deep: not an argument of check; its arguments are mesh, targets", CarveProcess.TextOf(result));
        var schema = Tool(tools, "check").GetProperty("inputSchema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["targets", "mesh", "verbosity"], schema.GetProperty("properties").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Tools_list_has_check_and_its_description_teaches_the_fixes()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;

        // The warnings' fixes moved to the topic (T-199), so they are read from there.
        var text = Description(tools, "check") + "\n" + carve::Carve.Server.HelpTool.Topic("warnings");

        foreach (var phrase in new[] { "inside-out", "coincident-vertices", "duplicate-face", "invisible", "collision", "z-fight", "reverse", "move", "define_mesh", "call delete, which keeps every id", "1 unit = 1 m", "y = 0", "Worked example" })
            Assert.Contains(phrase, text);
    }
}
