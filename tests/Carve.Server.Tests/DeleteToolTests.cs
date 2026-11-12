using System.Text.Json;
using Empyrean;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-64: <c>delete</c> and <c>get_mesh</c> through the client, and a topless cube exported and read back through the engine's loader.</summary>
[Trait("Tier", "regression")]
public sealed class DeleteToolTests() : TempFolderTests("carve-delete")
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

    private async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new CarveProcess([], workingDirectory: Root);
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

    [Fact]
    public async Task A_topless_cube_exports_its_five_faces_and_reads_back_through_the_engine()
    {
        var (carve, _) = await StartAsync();
        using (carve)
        {
            Ok(await carve.CallToolAsync("delete", """{"faces":[3]}"""));
            var exported = await carve.CallToolAsync("export", """{"path":"topless.glb"}""");
            Assert.False(IsError(exported), TextOf(exported));
        }
        var parsed = GlbExportTests.Parser.Parse(File.ReadAllBytes(Path.Combine(Root, "topless.glb")), ModelFormat.Gltf);
        Assert.Empty(parsed.Warnings ?? []);
        var indices = parsed.MeshData.Indices!;
        Assert.Equal(5 * 2 * 3, indices.Length);
        Assert.All(indices, i => Assert.InRange((int)i, 0, parsed.MeshData.Vertices.Length - 1));
        // Nothing of the top face: no triangle has all three corners at y = 1.
        var ys = parsed.MeshData.Vertices.Select(v => v.Position.Y).ToArray();
        for (var t = 0; t < indices.Length; t += 3)
            Assert.False(ys[indices[t]] > 0.999f && ys[indices[t + 1]] > 0.999f && ys[indices[t + 2]] > 0.999f, $"triangle {t / 3} lies at y = 1");
    }
}
