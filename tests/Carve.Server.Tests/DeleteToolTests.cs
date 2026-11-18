using System.Text.Json;
using Empyrean;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-64: <c>delete</c> and <c>get_mesh</c> through the client, and a topless cube exported and read back through the engine's loader.</summary>
[Trait("Tier", "unit")]
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

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));
        return (carve, tools);
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
