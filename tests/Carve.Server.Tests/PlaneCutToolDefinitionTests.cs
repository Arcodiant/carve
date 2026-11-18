using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class PlaneCutToolDefinitionTests() : TempFolderTests("carve-plane-cut")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    [Fact]
    public async Task The_worked_example_gives_a_half_height_closed_box_in_one_undo_step()
    {
        using var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));

        var body = Ok(await carve.CallToolAsync("plane_cut", ToolList.Examples(tools, "plane_cut").Single()));
        Assert.Equal(8, body.GetProperty("vertices").GetInt32());
        Assert.Equal(6, body.GetProperty("faces").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(0.5f, body.GetProperty("bounds").GetProperty("max")[1].GetSingle());
        Assert.Equal(0f, body.GetProperty("bounds").GetProperty("min")[1].GetSingle());
        var split = body.GetProperty("split").EnumerateArray()
            .Select(s => (s.GetProperty("face").GetInt32(), Ints(s.GetProperty("pieces")))).ToArray();
        Assert.Equal([0, 1, 4, 5], split.Select(s => s.Item1));
        Assert.All(split, s => Assert.Equal(2, s.Item2.Length));
        Assert.All(split, s => Assert.Equal(s.Item1, s.Item2[0]));
        var deletedFaces = Ints(body.GetProperty("deleted").GetProperty("faces"));
        Assert.Contains(3, deletedFaces);
        // Each upper piece the split appended is in split and in deleted.
        Assert.All(split, s => Assert.Contains(s.Item2[1], deletedFaces));
        Assert.Equal(5, deletedFaces.Length);
        Assert.Equal(4, Ints(body.GetProperty("deleted").GetProperty("vertices")).Length);
        Assert.Single(Ints(body.GetProperty("created").GetProperty("faces")));
        Assert.Empty(body.GetProperty("warnings").EnumerateArray());

        var check = TextOf(await carve.CallToolAsync("check", "{}"));
        Assert.Contains("\"closed\":true", check);

        Assert.Contains("plane_cut", TextOf(await carve.CallToolAsync("undo", "{}")));
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(8, mesh.GetProperty("vertices").GetArrayLength());
        Assert.Equal(6, mesh.GetProperty("faces").GetArrayLength());
        Assert.True(mesh.GetProperty("faces").EnumerateArray().All(f => f.ValueKind != JsonValueKind.Null));
    }
}
