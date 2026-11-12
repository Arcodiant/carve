using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>A face with a hole through the real server: define_mesh's object form, get_mesh and export (T-63).</summary>
[Trait("Tier", "regression")]
public sealed class HoledFaceToolTests() : FileToolTests("carve-holes")
{
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

    [Fact]
    public async Task A_plain_array_and_the_object_form_with_no_holes_define_the_same_face()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;

        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[{"loop":[0,1,2],"holes":[]}]}"""));
        var face = JsonDocument.Parse(Ok(await carve.CallToolAsync("get_mesh", "{}"))).RootElement.GetProperty("faces")[0].GetProperty("vertices");
        Assert.Equal(JsonValueKind.Array, face.ValueKind);
    }

    [Theory]
    [InlineData("""{"loop":[0,1,2,3],"holes":[[4,7,6,5]]}""", "faces[0].holes[0]", "clockwise")]
    [InlineData("""{"loop":[0,1,2,3],"holes":[[4,5]]}""", "faces[0].holes[0]", "at least 3")]
    [InlineData("""{"loop":[0,1,2,3],"holes":[[4,5,2,7]]}""", "faces[0].holes[0][2]", "repeats")]
    [InlineData("""{"loop":[0,1,2,3],"holes":[[4,5,6,9]]}""", "faces[0].holes[0][3]", "out of range")]
    [InlineData("""{"loop":[0,1,2,3]}""", "faces[0].holes", "required")]
    [InlineData("""{"holes":[[4,5,6,7]]}""", "faces[0].loop", "required")]
    [InlineData("""{"loop":[0,1,2,3],"holes":[[4,5,6,7]],"hole":1}""", "faces[0].hole", "only loop and holes")]
    [InlineData("""{"loop":[0,1,2,3],"holes":[[4,5,6,"x"]]}""", "faces[0].holes[0][3]", "integer")]
    public async Task A_bad_hole_is_a_tool_error_naming_it(string face, string element, string words)
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;

        var result = await carve.CallToolAsync("define_mesh",
            "{\"vertices\":[[-1,-1,0],[1,-1,0],[1,1,0],[-1,1,0],[-0.5,-0.5,0],[-0.5,0.5,0],[0.5,0.5,0],[0.5,-0.5,0]],\"faces\":[" + face + "]}");
        var text = Refused(result);
        var problem = text.Split('\n').FirstOrDefault(l => l.Contains(element + ":", StringComparison.Ordinal));
        Assert.True(problem is not null, text);
        Assert.Contains(words, problem);
    }
}
