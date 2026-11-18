using System.Text.Json;
using Xunit;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// The Carve file, version 2, through the real tools: the whole model (meshes, colours, groups, weights, skeleton,
/// clips) saved, read by a new process and identical there; save and open of one mesh by name (the person's ruling,
/// 2026-11-09).
/// </summary>
[Trait("Tier", "regression")]
public sealed class CarveFileVersion2Tests() : FileToolTests("carve-file-v2")
{
    internal static string Box(string name, float y0, float y1) =>
        $$"""{"name":"{{name}}","vertices":[[-0.2,{{y0}},-0.2],[0.2,{{y0}},-0.2],[0.2,{{y1}},-0.2],[-0.2,{{y1}},-0.2],[-0.2,{{y0}},0.2],[0.2,{{y0}},0.2],[0.2,{{y1}},0.2],[-0.2,{{y1}},0.2]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    internal const string Bones =
        """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]},{"name":"joint","parent":"base","position":[0,1,0],"rotation":[0,0,0,1]}]}""";

    /// <summary>define_clip's worked example, the wave: joint a quarter turn about +Z at 0.5 s and back at 1 s.</summary>
    internal const string Wave =
        """{"name":"wave","duration":1,"loop":true,"channels":{"joint":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0,0,0.70710677,0.70710677]},{"time":1,"value":[0,0,0,1]}]}}}}""";

    /// <summary>The two-bone chain of T-32 and T-33, coloured and grouped, with the wave clip, built through the tools.</summary>
    internal static async Task BuildChainAsync(IToolCaller carve)
    {
        Ok(await carve.CallToolAsync("define_mesh", Box("mesh", 0.2f, 0.9f)));
        Ok(await carve.CallToolAsync("define_mesh", Box("upper", 1.1f, 1.9f)));
        Ok(await carve.CallToolAsync("define_mesh", Box("plain", -1f, -0.6f)));
        Ok(await carve.CallToolAsync("define_group", """{"mesh":"upper","name":"all","faces":[0,1,2,3,4,5]}"""));
        Ok(await carve.CallToolAsync("set_colour", """{"mesh":"upper","colour":"#3C78C8","group":"all"}"""));
        Ok(await carve.CallToolAsync("set_colour", """{"mesh":"mesh","colour":"#c8503c","faces":[1,3]}"""));
        Ok(await carve.CallToolAsync("define_skeleton", Bones));
        Ok(await carve.CallToolAsync("set_weights", """{"mesh":"mesh","vertices":[0,1,2,3,4,5,6,7],"weights":{"base":1}}"""));
        Ok(await carve.CallToolAsync("set_weights", """{"mesh":"upper","group":"all","weights":{"joint":1}}"""));
        Ok(await carve.CallToolAsync("set_weights", """{"mesh":"upper","vertices":[0,1],"weights":{"base":1,"joint":2}}"""));
        Ok(await carve.CallToolAsync("define_clip", Wave));
    }

    internal static async Task<string> LookAsync(IToolCaller carve)
    {
        var parts = new List<string>();
        foreach (var name in new[] { "mesh", "upper", "plain" })
            parts.Add(Ok(await carve.CallToolAsync("get_mesh", $$"""{"mesh":"{{name}}"}""")));
        parts.Add(Ok(await carve.CallToolAsync("get_skeleton", "{}")));
        return string.Join("\n", parts);
    }

    private static async Task<byte[]> FrameAsync(CarveProcess carve, string arguments)
    {
        var result = await carve.CallToolAsync("render", arguments);
        Assert.False(IsError(result), result.ToString());
        return Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
    }

    [Fact]
    public async Task The_whole_model_saved_reads_back_in_a_new_process_identical()
    {
        string look;
        byte[] rest, wave;
        var file = Path.Combine(Root, "chain.carve");
        var (first, _) = await StartProcessAsync(mesh: null);
        using (first)
        {
            await BuildChainAsync(first);
            look = await LookAsync(first);
            rest = await FrameAsync(first, """{"direction":[0,0,1]}""");
            wave = await FrameAsync(first, """{"direction":[0,0,1],"clip":"wave","time":0.25}""");
            Assert.Equal($"saved chain.carve to {file}: 24 vertices, 18 faces in 3 meshes ('mesh', 'upper', 'plain'), 2 bones, 1 clip",
                Ok(await first.CallToolAsync("save", """{"path":"chain.carve"}""")));
        }
        var bytes = File.ReadAllBytes(file);

        var (second, _) = await StartProcessAsync(mesh: null);
        using var _second = second;
        var opened = JsonDocument.Parse(Ok(await second.CallToolAsync("open", """{"path":"chain.carve"}"""))).RootElement;
        Assert.Equal(["mesh", "upper", "plain"], opened.GetProperty("meshes").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(2, opened.GetProperty("bones").GetInt32());
        Assert.Equal(["wave"], opened.GetProperty("clips").EnumerateArray().Select(c => c.GetString()));

        Assert.Equal(look, await LookAsync(second));
        Assert.Equal(rest, await FrameAsync(second, """{"direction":[0,0,1]}"""));
        Assert.Equal(wave, await FrameAsync(second, """{"direction":[0,0,1],"clip":"wave","time":0.25}"""));
        Assert.NotEqual(rest, wave);

        Ok(await second.CallToolAsync("save", """{"path":"again.carve"}"""));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(Root, "again.carve")));
    }

    [Fact]
    public async Task Open_with_no_mesh_replaces_the_whole_model_as_one_undo_step()
    {
        var (carve, _) = await StartProcessAsync(mesh: null);
        using var _carve = carve;
        await BuildChainAsync(carve);
        Ok(await carve.CallToolAsync("save", """{"path":"chain.carve"}"""));

        Ok(await carve.CallToolAsync("define_mesh", Box("other", 0, 1)));
        Ok(await carve.CallToolAsync("save", """{"path":"plain.carve","mesh":"plain"}"""));
        var before = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}"""));

        // The file without 'other', its skeleton and clip: the model is the file's alone.
        Ok(await carve.CallToolAsync("open", """{"path":"plain.carve"}"""));
        Assert.Contains("no mesh named 'other'; the model's meshes are 'plain'", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}""")));
        Assert.Contains("no skeleton", TextOf(await carve.CallToolAsync("get_skeleton", "{}")));
        Assert.Contains("no clip named 'wave'", Refused(await carve.CallToolAsync("render", """{"clip":"wave","time":0}""")));

        Assert.Contains("\"undone\":\"open\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(before, Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}""")));
        Assert.False(IsError(await carve.CallToolAsync("render", """{"clip":"wave","time":0}""")));
    }
}
