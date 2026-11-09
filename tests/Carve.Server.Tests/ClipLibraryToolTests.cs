using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// RV-24 B2/D5, the person's ruling of 2026-11-09: a skeleton and clips with no mesh, a clip library, exports in clips
/// mode, saves as version 2 with "meshes": [] and opens again; model mode, and a save of nothing at all, still need more.
/// </summary>
[Trait("Tier", "regression")]
public sealed class ClipLibraryToolTests() : TempFolderTests("carve-clip-library")
{
    private const string Hips = """{"bones":[{"name":"Hips","parent":null,"position":[0,1,0],"rotation":[0,0,0,1]}]}""";
    private const string Idle = """{"name":"idle","duration":1,"channels":{"Hips":{"translation":{"keys":[{"time":0,"value":[0,1,0]},{"time":1,"value":[0,1.1,0]}]}}}}""";

    private static void Ok(JsonElement result) => Assert.False(IsError(result), TextOf(result));

    private async Task<CarveProcess> StartAsync()
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        return carve;
    }

    [Fact]
    public async Task A_clip_library_exports_as_clips_saves_and_opens_again()
    {
        using (var carve = await StartAsync())
        {
            Ok(await carve.CallToolAsync("define_skeleton", Hips));
            Ok(await carve.CallToolAsync("define_clip", Idle));

            var exported = await carve.CallToolAsync("export", """{"path":"anim.clips.glb","mode":"clips"}""");
            Ok(exported);
            Assert.Contains("as clips, with no mesh: 1 bone(s) 'Hips', 1 clip(s) 'idle'", TextOf(exported));
            Assert.True(File.Exists(Path.Combine(Root, "anim.clips.glb")));

            var model = await carve.CallToolAsync("export", """{"path":"anim.glb"}""");
            Assert.True(IsError(model), TextOf(model));
            Assert.Contains("there is no mesh yet", TextOf(model));
            Assert.False(File.Exists(Path.Combine(Root, "anim.glb")));

            var saved = await carve.CallToolAsync("save", """{"path":"anim.carve"}""");
            Ok(saved);
            Assert.Contains("0 vertices, 0 faces, 1 bone, 1 clip", TextOf(saved));
        }
        Assert.Contains("\"meshes\": [],", File.ReadAllText(Path.Combine(Root, "anim.carve")));

        using (var again = await StartAsync())
        {
            var opened = await again.CallToolAsync("open", """{"path":"anim.carve"}""");
            Ok(opened);
            var body = JsonDocument.Parse(TextOf(opened)).RootElement;
            Assert.Equal(0, body.GetProperty("meshes").GetArrayLength());
            Assert.Equal(1, body.GetProperty("bones").GetInt32());
            Assert.Equal(["idle"], body.GetProperty("clips").EnumerateArray().Select(c => c.GetString()));
            Ok(await again.CallToolAsync("save", """{"path":"again.carve"}"""));
        }
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root, "anim.carve")), File.ReadAllBytes(Path.Combine(Root, "again.carve")));
    }

    [Fact]
    public async Task With_no_mesh_and_no_skeleton_save_and_export_are_still_refused()
    {
        using var carve = await StartAsync();
        var saved = await carve.CallToolAsync("save", """{"path":"nothing.carve"}""");
        Assert.True(IsError(saved), TextOf(saved));
        Assert.Contains("there is no mesh yet", TextOf(saved));
        var exported = await carve.CallToolAsync("export", """{"path":"nothing.clips.glb","mode":"clips"}""");
        Assert.True(IsError(exported), TextOf(exported));
        Assert.StartsWith("export: mode clips: ", TextOf(exported));
        Assert.Empty(Directory.GetFiles(Root));
    }
}
