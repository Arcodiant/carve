using System.Text.Json;
using Empyrean;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class DeleteToolDefinitionTests() : TempFolderTests("carve-delete")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static void Refused(JsonElement result, string mention)
    {
        Assert.True(IsError(result), TextOf(result));
        Assert.True(TextOf(result).Contains(mention, StringComparison.Ordinal), TextOf(result));
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));
        return (carve, tools);
    }

    [Fact]
    public async Task The_worked_example_leaves_an_open_box_and_get_mesh_writes_null_in_the_slot()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;

        var body = Ok(await carve.CallToolAsync("delete", ToolList.Examples(tools, "delete").Single()));
        Assert.Equal([3], Ints(body.GetProperty("deleted").GetProperty("faces")));
        Assert.Empty(Ints(body.GetProperty("deleted").GetProperty("vertices")));
        // Every edit's report carries split (T-65), empty when nothing was split.
        Assert.Equal(System.Text.Json.JsonValueKind.Array, body.GetProperty("split").ValueKind);
        Assert.Equal(0, body.GetProperty("split").GetArrayLength());
        Assert.Equal(5, body.GetProperty("faces").GetInt32());
        Assert.Equal(8, body.GetProperty("vertices").GetInt32());
        Assert.False(body.GetProperty("closed").GetBoolean());

        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var faces = mesh.GetProperty("faces").EnumerateArray().ToArray();
        Assert.Equal(6, faces.Length);
        Assert.Equal(JsonValueKind.Null, faces[3].ValueKind);
        Assert.Equal(4, faces[4].GetProperty("id").GetInt32());
        Assert.False(mesh.GetProperty("closed").GetBoolean());

        // Vertex 6 is now on faces 1 and 5 only: it takes both, and no vertex is left with no face.
        body = Ok(await carve.CallToolAsync("delete", """{"vertices":[6]}"""));
        Assert.Equal([6], Ints(body.GetProperty("deleted").GetProperty("vertices")));
        Assert.Equal([1, 5], Ints(body.GetProperty("deleted").GetProperty("faces")));
        Assert.Equal(3, body.GetProperty("faces").GetInt32());
        Assert.Equal(7, body.GetProperty("vertices").GetInt32());
        mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(JsonValueKind.Null, mesh.GetProperty("vertices")[6].ValueKind);
        Assert.Equal(7, mesh.GetProperty("vertices")[7].GetProperty("id").GetInt32());

        // A deleted id is "deleted", one that never existed is not.
        Refused(await carve.CallToolAsync("move", """{"vertices":[6],"by":[0,1,0]}"""), "vertices[6]: deleted");
        Refused(await carve.CallToolAsync("extrude", """{"faces":[3],"distance":1}"""), "faces[3]: deleted");
        Refused(await carve.CallToolAsync("delete", """{"faces":[9]}"""), "there is no face 9");
        Refused(await carve.CallToolAsync("delete", "{}"), "nothing to delete");
        Refused(await carve.CallToolAsync("delete", """{"faces":[0,2,4]}"""), "a mesh keeps at least one face");

        // Every edit reports deleted, empty when it deletes nothing; new elements append after the last slot.
        body = Ok(await carve.CallToolAsync("extrude", """{"faces":[0],"distance":0.5}"""));
        Assert.Empty(Ints(body.GetProperty("deleted").GetProperty("faces")));
        Assert.Equal([8, 9, 10, 11], Ints(body.GetProperty("created").GetProperty("vertices")));
        Assert.Equal([6, 7, 8, 9], Ints(body.GetProperty("created").GetProperty("faces")));

        // undo of the extrude, then of the vertex delete, restores the slots and what was in them.
        Ok(await carve.CallToolAsync("undo", "{}"));
        Ok(await carve.CallToolAsync("undo", "{}"));
        mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal([4, 5, 6, 7], Ints(mesh.GetProperty("faces")[1].GetProperty("vertices")));
        Assert.Equal(6, mesh.GetProperty("vertices")[6].GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, mesh.GetProperty("faces")[3].ValueKind);
    }
}
