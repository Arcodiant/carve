using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.ToolList;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class SubdivideToolDefinitionTests() : TempFolderTests("carve-subdivide")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        return (carve, tools);
    }

    [Fact]
    public async Task The_example_smooths_the_cube_in_one_step_and_one_undo_takes_it_back()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));

        var body = Ok(await carve.CallToolAsync("subdivide", Examples(tools, "subdivide").Single()));

        Assert.Equal(26, body.GetProperty("vertices").GetInt32());
        Assert.Equal(24, body.GetProperty("faces").GetInt32());
        Assert.Equal(48, body.GetProperty("edges").GetInt32());
        Assert.Equal(48, body.GetProperty("triangles").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(1, body.GetProperty("levels").GetInt32());
        Assert.Equal(8, body.GetProperty("created").GetProperty("vertices").GetProperty("first").GetInt32());
        Assert.Equal(18, body.GetProperty("created").GetProperty("vertices").GetProperty("count").GetInt32());
        Assert.Equal(6, body.GetProperty("created").GetProperty("faces").GetProperty("first").GetInt32());
        Assert.Equal(18, body.GetProperty("created").GetProperty("faces").GetProperty("count").GetInt32());
        Assert.DoesNotContain(body.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("triangles"));

        var undone = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("subdivide", undone.GetProperty("undone").GetString());
        Assert.Equal(8, undone.GetProperty("vertices").GetInt32());
        Assert.Equal(6, undone.GetProperty("faces").GetInt32());
    }
}
