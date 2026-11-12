using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-67: <c>knife</c> through the client on <c>define_mesh</c>'s cube, its worked examples, the off-surface error, then <c>check</c>.</summary>
[Trait("Tier", "regression")]
public sealed class KnifeToolTests() : TempFolderTests("carve-knife")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    [Fact]
    public async Task The_worked_examples_cut_a_door_and_a_holed_front_each_in_one_undo_step()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        var examples = ToolList.Examples(tools, "knife");
        Assert.Equal(2, examples.Length);
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));

        // The door: the front (faces[1]) split, the door appended.
        var door = Ok(await carve.CallToolAsync("knife", examples[0]));
        Assert.Equal(12, door.GetProperty("vertices").GetInt32());
        Assert.Equal(7, door.GetProperty("faces").GetInt32());
        Assert.True(door.GetProperty("closed").GetBoolean());
        var split = Assert.Single(door.GetProperty("split").EnumerateArray());
        Assert.Equal(1, split.GetProperty("face").GetInt32());
        Assert.Equal([1, 6], Ints(split.GetProperty("pieces")));
        Assert.Equal([8, 9, 10, 11], Ints(door.GetProperty("created").GetProperty("vertices")));
        Assert.Contains("\"closed\":true", TextOf(await carve.CallToolAsync("check", "{}")));
        Assert.Contains("knife", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(8, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("vertices").GetArrayLength());

        // The closed square: the front holed, the inner face appended.
        var square = Ok(await carve.CallToolAsync("knife", examples[1]));
        Assert.Equal(12, square.GetProperty("vertices").GetInt32());
        Assert.Equal(7, square.GetProperty("faces").GetInt32());
        Assert.True(square.GetProperty("closed").GetBoolean());
        Assert.Equal([1, 6], Ints(Assert.Single(square.GetProperty("split").EnumerateArray()).GetProperty("pieces")));
        var front = Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("faces")[1].GetProperty("vertices");
        Assert.Equal([4, 5, 6, 7], Ints(front.GetProperty("loop")));
        Assert.Equal([8, 11, 10, 9], Ints(Assert.Single(front.GetProperty("holes").EnumerateArray())));
        Assert.Contains("\"closed\":true", TextOf(await carve.CallToolAsync("check", "{}")));
        Assert.Contains("knife", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(6, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("faces").GetArrayLength());
    }

    [Fact]
    public async Task An_off_surface_point_is_refused_with_its_distance_and_the_nearest_surface_point()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
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

        // Nothing was changed or added to undo.
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(8, mesh.GetProperty("vertices").GetArrayLength());
        Assert.Contains("\"closed\":true", TextOf(await carve.CallToolAsync("check", "{}")));
    }
}
