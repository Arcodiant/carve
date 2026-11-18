using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class KnifeToolDefinitionTests() : TempFolderTests("carve-knife")
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
        using var carve = new ToolHarness(workingDirectory: Root);
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
}
