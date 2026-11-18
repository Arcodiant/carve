using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-67: <c>knife</c> through the client on <c>define_mesh</c>'s cube, its worked examples, the off-surface error, then <c>check</c>.</summary>
[Trait("Tier", "unit")]
public sealed class KnifeToolTests() : TempFolderTests("carve-knife")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    [Fact]
    public async Task An_off_surface_point_is_refused_with_its_distance_and_the_nearest_surface_point()
    {
        using var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));

        var off = await carve.CallToolAsync("knife", """{"points":[[0.5,0,1],[0.5,0.5,1.25],[0.5,1,1]]}""");
        Assert.True(IsError(off));
        Assert.Contains(
            "points[1]: [0.5, 0.5, 1.25] is not on the surface: it is 0.25 from it, more than the tolerance 0.000173205; the nearest surface point is [0.5, 0.5, 1], on faces[1]",
            TextOf(off));

        var bad = TextOf(await carve.CallToolAsync("knife", """{"points":[[0,0,1],[1,"a",1]],"closed":1,"edge":3}"""));
        Assert.Contains("points[1][1]: expected a number", bad);
        Assert.Contains("closed", bad);
        Assert.Contains("edge", bad);
        // The kind is named as every other argument error names it.
        Assert.Contains("points: expected an array of [x, y, z], got an object", TextOf(await carve.CallToolAsync("knife", """{"points":{}}""")));

        // Nothing was changed or added to undo.
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(8, mesh.GetProperty("vertices").GetArrayLength());
        Assert.Contains("\"closed\":true", TextOf(await carve.CallToolAsync("check", "{}")));
    }
}
