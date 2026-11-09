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
    private static string Box(string name, float y0, float y1) =>
        $$"""{"name":"{{name}}","vertices":[[-0.2,{{y0}},-0.2],[0.2,{{y0}},-0.2],[0.2,{{y1}},-0.2],[-0.2,{{y1}},-0.2],[-0.2,{{y0}},0.2],[0.2,{{y0}},0.2],[0.2,{{y1}},0.2],[-0.2,{{y1}},0.2]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private const string Bones =
        """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]},{"name":"joint","parent":"base","position":[0,1,0],"rotation":[0,0,0,1]}]}""";

    /// <summary>define_clip's worked example, the wave: joint a quarter turn about +Z at 0.5 s and back at 1 s.</summary>
    private const string Wave =
        """{"name":"wave","duration":1,"loop":true,"channels":{"joint":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0,0,0.70710677,0.70710677]},{"time":1,"value":[0,0,0,1]}]}}}}""";

    /// <summary>The two-bone chain of T-32 and T-33, coloured and grouped, with the wave clip, built through the tools.</summary>
    private static async Task BuildChainAsync(CarveProcess carve)
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

    private static async Task<string> LookAsync(CarveProcess carve)
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
        var (first, _) = await StartAsync(mesh: null);
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

        var (second, _) = await StartAsync(mesh: null);
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
        var (carve, _) = await StartAsync(mesh: null);
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

    [Fact]
    public async Task Save_with_mesh_writes_that_mesh_with_the_skeleton_and_clips_its_weights_need()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        await BuildChainAsync(carve);

        Assert.Equal($"saved upper.carve to {Path.Combine(Root, "upper.carve")}: 8 vertices, 6 faces, 2 bones, 1 clip",
            Ok(await carve.CallToolAsync("save", """{"path":"upper.carve","mesh":"upper"}""")));
        var upper = Core.CarveFile.Read(File.ReadAllText(Path.Combine(Root, "upper.carve"))).Model!;
        Assert.Equal(["upper"], upper.Names);
        Assert.Equal(["base", "joint"], upper.Skeleton!.Names);
        Assert.Equal("wave", Assert.Single(upper.Clips).Name);

        Ok(await carve.CallToolAsync("save", """{"path":"plain.carve","mesh":"plain"}"""));
        var plain = Core.CarveFile.Read(File.ReadAllText(Path.Combine(Root, "plain.carve"))).Model!;
        Assert.Equal(["plain"], plain.Names);
        Assert.Null(plain.Skeleton);
        Assert.Empty(plain.Clips);

        Assert.Contains("no mesh named 'hat'", Refused(await carve.CallToolAsync("save", """{"path":"hat.carve","mesh":"hat"}""")));
        Assert.False(File.Exists(Path.Combine(Root, "hat.carve")));
    }

    [Fact]
    public async Task Open_with_mesh_takes_that_mesh_onto_the_models_bones_by_name_or_is_refused_leaving_the_model()
    {
        var (first, _) = await StartAsync(mesh: null);
        using (first)
        {
            await BuildChainAsync(first);
            Ok(await first.CallToolAsync("save", """{"path":"chain.carve"}"""));
        }

        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        Ok(await carve.CallToolAsync("define_mesh", Box("other", 0, 1)));
        var before = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}"""));

        // No skeleton: the weights name bones the model does not have.
        var refused = Refused(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"upper"}"""));
        Assert.Contains("mesh: the file's mesh 'upper' has weights on the bones 'base', 'joint', and the model has no skeleton", refused);
        Assert.Equal(before, Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Contains("nothing to undo", Refused(await carve.CallToolAsync("undo", "{}")));

        // A skeleton lacking 'joint' is still refused, naming it.
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"base","position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        Assert.Contains("does not have: 'joint'", Refused(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"upper"}""")));
        Assert.Contains("the file has no mesh named 'hat'; its meshes are 'mesh', 'upper', 'plain'",
            Refused(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"hat"}""")));

        // The same bones in the other order: the weights follow the names.
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"joint","position":[0,1,0],"rotation":[0,0,0,1]},{"name":"base","position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        var opened = JsonDocument.Parse(Ok(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"upper"}"""))).RootElement;
        Assert.Equal(8, opened.GetProperty("vertices").GetInt32());
        Assert.Equal(before, Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}""")));
        var upper = JsonDocument.Parse(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"upper"}"""))).RootElement.ToString();
        Assert.Contains("#3C78C8", upper);
        // get_mesh does not show weights; the model saved again does, on the model's own bone indices (joint 0, base 1).
        Ok(await carve.CallToolAsync("save", """{"path":"after.carve"}"""));
        var weights = Core.CarveFile.Read(File.ReadAllText(Path.Combine(Root, "after.carve"))).Model!.Find("upper")!.Weights!;
        Assert.Equal([new Core.Influence(0, 1)], weights.Of(2));
        Assert.Equal([new Core.Influence(0, 2.0 / 3), new Core.Influence(1, 1.0 / 3)], weights.Of(0));

        Assert.Contains("\"undone\":\"open\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Contains("no mesh named 'upper'", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"upper"}""")));
    }
}
