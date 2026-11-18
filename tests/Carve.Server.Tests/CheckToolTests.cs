using System.Text.Json;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>check through the real server.</summary>
[Trait("Tier", "unit")]
public class CheckToolTests
{
    private static async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        return (carve, await carve.ListToolsAsync());
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return JsonDocument.Parse(CarveProcess.TextOf(result)).RootElement;
    }

    private static string ShedArguments()
    {
        var shed = TestMeshes.LeakyShed();
        return JsonSerializer.Serialize(new { vertices = shed.Vertices.Select(v => new[] { v.X, v.Y, v.Z }), faces = shed.Faces.Select(f => f.Loop) });
    }

    private static void ExpectShed(JsonElement body)
    {
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Equal(
            TestMeshes.LeakyShedProblems,
            body.GetProperty("problems").EnumerateArray().Select(p => (p.GetProperty("check").GetString()!, p.GetProperty("element").GetString()!, p.GetProperty("message").GetString()!)));
        Assert.Equal(TestMeshes.LeakyShedWarnings, body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!));
        Assert.Equal(TestMeshes.LeakyShedNotes, body.GetProperty("notes").EnumerateArray().Select(n => n.GetString()!));

        var stats = body.GetProperty("stats");
        Assert.Equal(
            """{"vertices":25,"faces":18,"edges":38,"triangles":36,"shells":2,"closed":false,"bounds":{"min":[0,0,0],"max":[4,3.9,3]},"size":[4,3.9,3],"area":60.09371484553011,"volume":null}""",
            stats.GetRawText());
    }

    [Fact]
    public async Task The_shed_through_define_mesh_gives_the_exact_findings()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        Ok(await carve.CallToolAsync("define_mesh", ShedArguments()));

        ExpectShed(Ok(await carve.CallToolAsync("check", "{}")));
    }

    [Fact]
    public async Task The_shed_opened_from_samples_gives_the_same_findings()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        var path = Path.Combine(Paths.RepoRoot, "samples", "leaky-shed.carve");
        Ok(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path })));

        ExpectShed(Ok(await carve.CallToolAsync("check", "{}")));
    }

    [Fact]
    public async Task Before_define_mesh_it_is_a_tool_error_saying_so()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;

        var result = await carve.CallToolAsync("check", "{}");

        Assert.True(CarveProcess.IsError(result));
        Assert.Contains("define_mesh", CarveProcess.TextOf(result));
    }

    [Fact]
    public async Task It_changes_nothing_the_mesh_and_undo_are_as_they_were()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        Ok(await carve.CallToolAsync("move", Examples(tools, "move").Single()));
        var before = CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        Ok(await carve.CallToolAsync("check", "{}"));
        Ok(await carve.CallToolAsync("check", "{}"));

        Assert.Equal(before, CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        // A check that pushed a version would be the step undone here.
        Assert.Equal("move", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
        Assert.True(CarveProcess.IsError(await carve.CallToolAsync("undo", "{}")));
    }

    private const string TwoSquares =
        """{"vertices":[[0,0,0],[1,0,0],[2,0,0],[0,1,0],[1,1,0],[2,1,0]],"faces":[[0,1,4,3],[1,2,5,4],[0,3,4,1]]}""";

    [Fact]
    public async Task A_mapped_mesh_gets_UV_warnings_and_uvs_in_stats_and_an_unmapped_one_does_not()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", TwoSquares));

        var before = Ok(await carve.CallToolAsync("check", "{}"));
        Assert.False(before.GetProperty("stats").TryGetProperty("uvs", out var absent), absent.ToString());
        Assert.DoesNotContain(before.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("UVs"));

        // Face 0 clean, face 1 flipped, face 2 unmapped; face 0 and face 1 overlap by half after the second call.
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]},{"face":1,"uv":[[0.5,0],[1.5,0],[1.5,1],[0.5,1]]}]}"""));
        var body = Ok(await carve.CallToolAsync("check", "{}"));
        var warnings = body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).Where(w => w.Contains("UVs")).ToArray();
        Assert.Equal(
            [
                "faces[1]: UVs flipped, so the texture is mirrored on the face",
                "faces[2]: no UVs, so the face samples the texture's corner",
                "faces[0]: UVs overlap faces[1], so the two faces show the same texels; mirrored halves sharing a texture area are deliberate and still warned of",
            ],
            warnings);
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Equal("""{"mapped":2,"unmapped":1}""", body.GetProperty("stats").GetProperty("uvs").GetRawText());

        // Face 2 sampled the other way is zero area.
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":1,"uv":[[2,0],[2,1],[3,1],[3,0]]},{"face":2,"uv":[[5,5],[5,5],[5,5],[5,5]]}]}"""));
        var fixedUp = Ok(await carve.CallToolAsync("check", "{}")).GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).Where(w => w.Contains("UVs")).ToArray();
        Assert.Equal(["faces[2]: UVs of zero area, so the face is one smeared texel"], fixedUp);
    }
}
