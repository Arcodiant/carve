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

    private const string Drift = """{"name":"drift","duration":1,"loop":true,"channels":{"Hips":{"translation":{"keys":[{"time":0,"value":[0,1,0]},{"time":1,"value":[0,1,0.5]}]}}}}""";

    [Fact]
    public async Task Clip_warnings_reach_define_clip_get_clips_save_and_export()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("define_skeleton", Hips));

        var clean = await carve.CallToolAsync("define_clip", Idle);
        Ok(clean);
        Assert.Equal(0, JsonDocument.Parse(TextOf(clean)).RootElement.GetProperty("warnings").GetArrayLength());

        var defined = await carve.CallToolAsync("define_clip", Drift);
        Ok(defined);
        Assert.Equal(2, JsonDocument.Parse(TextOf(defined)).RootElement.GetProperty("warnings").GetArrayLength());

        var all = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_clips", "{}"))).RootElement.GetProperty("clips");
        Assert.Equal(["idle", "drift"], all.EnumerateArray().Select(c => c.GetProperty("name").GetString()));
        var one = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_clips", """{"name":"drift"}"""))).RootElement.GetProperty("clips")[0];
        Assert.True(one.GetProperty("loop").GetBoolean());
        Assert.Equal(1, one.GetProperty("duration").GetDouble());
        var keys = one.GetProperty("channels").GetProperty("Hips").GetProperty("translation").GetProperty("keys");
        Assert.Equal(2, keys.GetArrayLength());
        Assert.Equal(0.5, keys[1].GetProperty("value")[2].GetDouble());
        Assert.False(one.GetProperty("channels").GetProperty("Hips").TryGetProperty("rotation", out _), "a part the clip does not key is left out, not null");
        Assert.Equal(2, one.GetProperty("warnings").GetArrayLength());
        var unknown = await carve.CallToolAsync("get_clips", """{"name":"nope"}""");
        Assert.True(IsError(unknown));
        Assert.Equal("get_clips: no clip named 'nope'; the clips are 'idle', 'drift'", TextOf(unknown));

        var saved = TextOf(await carve.CallToolAsync("save", """{"path":"d.carve"}"""));
        Assert.Contains("2 warning(s): clip 'drift' loops", saved);
        var exported = TextOf(await carve.CallToolAsync("export", """{"path":"d.clips.glb","mode":"clips"}"""));
        Assert.Contains("2 warning(s): clip 'drift' loops", exported);

        Ok(await carve.CallToolAsync("delete_clip", """{"name":"drift"}"""));
        Assert.DoesNotContain("warning", TextOf(await carve.CallToolAsync("save", """{"path":"e.carve"}""")));
    }
}
