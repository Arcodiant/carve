using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class HoledFaceToolDefinitionTests() : TempFolderTests("carve-holes")
{
    private static string Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync(string? mesh = "cube")
    {
        var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (mesh is not null) Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh == "cube" ? Cube(tools) : mesh)));
        return (carve, tools);
    }

    private const string HoleMarker = "Worked example (a face with a hole):";

    private static string Example(JsonElement tools)
    {
        var text = ToolList.Description(tools, "define_mesh");
        var at = text.IndexOf(HoleMarker, StringComparison.Ordinal);
        Assert.True(at >= 0, "the description has no holed-face worked example");
        var line = text[(at + HoleMarker.Length)..];
        line = line[..line.IndexOf('\n')];
        return line[line.IndexOf('{')..];
    }

    [Fact]
    public async Task The_worked_plate_with_a_hole_is_defined_read_back_in_the_object_form_and_exported()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _carve = carve;

        var defined = JsonDocument.Parse(Ok(await carve.CallToolAsync("define_mesh", Example(tools)))).RootElement;
        Assert.Equal(8, defined.GetProperty("vertices").GetInt32());
        Assert.Equal(1, defined.GetProperty("faces").GetInt32());
        Assert.Equal(8, defined.GetProperty("edges").GetInt32());
        Assert.False(defined.GetProperty("closed").GetBoolean());

        var mesh = JsonDocument.Parse(Ok(await carve.CallToolAsync("get_mesh", "{}"))).RootElement;
        var face = mesh.GetProperty("faces")[0].GetProperty("vertices");
        Assert.Equal(JsonValueKind.Object, face.ValueKind);
        Assert.Equal([0, 1, 2, 3], face.GetProperty("loop").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal([4, 5, 6, 7], face.GetProperty("holes")[0].EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal(8, mesh.GetProperty("edges").GetArrayLength());

        // The 2 x 2 plate less its 1 x 1 hole: 8 triangles (its 8 corners and the bridge's 2 ends, less 2).
        var line = Ok(await carve.CallToolAsync("export", """{"path":"plate.glb"}"""));
        Assert.Contains(": 8 triangles, 1 material 'carve'", line);
    }
}
