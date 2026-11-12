using System.Text.Json;
using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// <c>export</c> of a model with a skeleton through the real <c>carve</c>: unweighted vertices refused with the file
/// untouched, <c>allow_unweighted</c>, the result line, and the same bytes from two <c>carve</c> processes.
/// </summary>
[Trait("Tier", "regression")]
public sealed class SkinnedExportToolTests() : TempFolderTests("carve-skinned")
{
    internal static string Cube(string name, float y) =>
        JsonSerializer.Serialize(new
        {
            name,
            vertices = TestMeshes.CubeVertices.Select(v => new[] { v.X * 0.4f, (v.Y * 0.4f) + y, v.Z * 0.4f }),
            faces = TestMeshes.CubeFaces,
        });

    internal const string Chain =
        """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]},{"name":"joint","parent":"base","position":[0,1,0],"rotation":[0,0,0,1]}]}""";

    /// <summary>define_clip's worked example.</summary>
    private const string Wave =
        """{"name":"wave","duration":1,"loop":true,"channels":{"joint":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0,0,0.70710677,0.70710677]},{"time":1,"value":[0,0,0,1]}]}}}}""";

    internal static void Ok(JsonElement result) => Assert.False(IsError(result), TextOf(result));

    /// <summary>Two boxes on a two-bone chain; the upper box weighted only when <paramref name="weighUpper"/>; two clips.</summary>
    internal static async Task<CarveProcess> StartChainAsync(string folder, bool weighUpper = true)
    {
        var carve = new CarveProcess([], workingDirectory: folder);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Cube("lower", 0.2f)));
        Ok(await carve.CallToolAsync("define_mesh", Cube("upper", 1.2f)));
        Ok(await carve.CallToolAsync("define_skeleton", Chain));
        Ok(await carve.CallToolAsync("set_weights", """{"mesh":"lower","vertices":[0,1,2,3,4,5,6,7],"weights":{"base":1}}"""));
        if (weighUpper)
            Ok(await carve.CallToolAsync("set_weights", """{"mesh":"upper","vertices":[0,1,2,3,4,5,6,7],"weights":{"joint":0.75,"base":0.25}}"""));
        else
            Ok(await carve.CallToolAsync("set_weights", """{"mesh":"upper","vertices":[0,1,2,4,5,7],"weights":{"joint":1}}"""));
        Ok(await carve.CallToolAsync("define_clip", Wave));
        Ok(await carve.CallToolAsync("define_clip",
            """{"name":"nod","duration":0.8,"channels":{"base":{"translation":{"interpolation":"STEP","keys":[{"time":0.2,"value":[0,0.1,0]}]}}}}"""));
        return carve;
    }

    [Fact]
    public async Task Unweighted_vertices_are_refused_and_listed_and_the_file_is_left_as_it_was()
    {
        using var carve = await StartChainAsync(Root, weighUpper: false);
        var path = Path.Combine(Root, "chain.glb");
        File.WriteAllText(path, "before");

        var refused = await carve.CallToolAsync("export", """{"path":"chain.glb"}""");
        Assert.True(IsError(refused), TextOf(refused));
        Assert.Contains("2 vertices have no weight", TextOf(refused));
        Assert.Contains("mesh 'upper' vertices 3, 6", TextOf(refused));
        Assert.Equal("before", File.ReadAllText(path));

        var allowed = await carve.CallToolAsync("export", """{"path":"chain.glb","allow_unweighted":true,"overwrite":true}""");
        Assert.False(IsError(allowed), TextOf(allowed));
        Assert.Contains("2 vertices with no weight were bound to the first root bone 'base'", TextOf(allowed));
        var parsed = GlbExportTests.Parser.Parse(File.ReadAllBytes(path), Empyrean.ModelFormat.Gltf);
        Assert.Empty(parsed.Warnings);
        Assert.Equal(["base", "joint"], parsed.Skin!.Skeleton.Bones.Select(b => b.Name));

        Assert.Contains("allow_unweighted: expected true or false", TextOf(await carve.CallToolAsync("export", """{"path":"chain.glb","allow_unweighted":1}""")));
    }

    [Fact]
    public async Task Clips_mode_writes_the_armature_skin_and_clips_with_no_mesh_and_refuses_without_either()
    {
        using var carve = await StartChainAsync(Root);
        var modelPath = Path.Combine(Root, "chain.glb");
        var clipsPath = Path.Combine(Root, "clips.glb");
        Ok(await carve.CallToolAsync("export", """{"path":"chain.glb"}"""));
        Ok(await carve.CallToolAsync("export", """{"path":"again/chain.glb","mode":"model"}"""));
        Assert.Equal(File.ReadAllBytes(modelPath), File.ReadAllBytes(Path.Combine(Root, "again", "chain.glb")));

        var result = await carve.CallToolAsync("export", """{"path":"clips.glb","mode":"clips"}""");
        Ok(result);
        Assert.Contains("as clips, with no mesh: 2 bone(s) 'base', 'joint', 2 clip(s) 'wave', 'nod'", TextOf(result));
        var bytes = File.ReadAllBytes(clipsPath);
        var set = GlbExportTests.Parser.ParseAnimationSet(bytes);
        Assert.Equal(["base", "joint"], set.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(["wave", "nod"], set.Clips.Select(c => c.Name));
        var model = GlbExportTests.Parser.Parse(File.ReadAllBytes(modelPath), Empyrean.ModelFormat.Gltf);
        Assert.Equal(model.Skin!.Skeleton.Bones, set.Skeleton.Bones);
        Assert.Equal("glTF contains no meshes.", Assert.ThrowsAny<Exception>(() => GlbExportTests.Parser.Parse(bytes, Empyrean.ModelFormat.Gltf)).Message);

        // An existing file is refused without overwrite, in clips mode too, and replaced with it, to the same bytes; a bad mode is refused.
        var again = await carve.CallToolAsync("export", """{"path":"clips.glb","mode":"clips"}""");
        Assert.True(IsError(again), TextOf(again));
        Assert.Contains(clipsPath, TextOf(again));
        Assert.Contains("overwrite", TextOf(again));
        Assert.Equal(bytes, File.ReadAllBytes(clipsPath));
        var modelBytes = File.ReadAllBytes(modelPath);
        var modelAgain = await carve.CallToolAsync("export", """{"path":"chain.glb"}""");
        Assert.True(IsError(modelAgain), TextOf(modelAgain));
        Assert.Contains("overwrite", TextOf(modelAgain));
        Assert.Equal(modelBytes, File.ReadAllBytes(modelPath));
        Assert.Contains("(replaced)", TextOf(await carve.CallToolAsync("export", """{"path":"clips.glb","mode":"clips","overwrite":true}""")));
        Assert.Equal(bytes, File.ReadAllBytes(clipsPath));
        Assert.Contains("(replaced)", TextOf(await carve.CallToolAsync("export", """{"path":"chain.glb","overwrite":true}""")));
        Assert.Equal(modelBytes, File.ReadAllBytes(modelPath));
        Assert.Contains("mode: expected", TextOf(await carve.CallToolAsync("export", """{"path":"x.glb","mode":"bogus"}""")));

        // No clips: refused, nothing written.
        Ok(await carve.CallToolAsync("delete_clip", """{"name":"wave"}"""));
        Ok(await carve.CallToolAsync("delete_clip", """{"name":"nod"}"""));
        var none = await carve.CallToolAsync("export", """{"path":"none.glb","mode":"clips"}""");
        Assert.True(IsError(none), TextOf(none));
        Assert.Contains("no clips", TextOf(none));
        Assert.False(File.Exists(Path.Combine(Root, "none.glb")));
    }

    [Fact]
    public async Task Clips_mode_refuses_a_model_with_no_skeleton()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Cube("lower", 0.2f)));
        var refused = await carve.CallToolAsync("export", """{"path":"clips.glb","mode":"clips"}""");
        Assert.True(IsError(refused), TextOf(refused));
        Assert.Contains("no skeleton", TextOf(refused));
        Assert.False(File.Exists(Path.Combine(Root, "clips.glb")));
    }

    [Fact]
    public async Task The_result_names_the_path_pieces_materials_bones_and_clips()
    {
        using var carve = await StartChainAsync(Root);
        var text = TextOf(await carve.CallToolAsync("export", """{"path":"chain.glb"}"""));
        Assert.StartsWith($"exported chain.glb to {Path.Combine(Root, "chain.glb")} skinned: ", text);
        Assert.Contains("2 piece(s) 'lower', 'upper', 2 material(s) 'lower', 'upper', 2 bone(s) 'base', 'joint', 2 clip(s) 'wave', 'nod', 24 triangles", text);
        Assert.DoesNotContain("warning", text);
        var parsed = GlbExportTests.Parser.Parse(File.ReadAllBytes(Path.Combine(Root, "chain.glb")), Empyrean.ModelFormat.Gltf);
        Assert.Equal(2, parsed.SubmeshMaterials.Length);
        Assert.Equal(["wave", "nod"], parsed.Skin!.Animations.Select(a => a.Name));
        Assert.Equal([1f, 0.8f], parsed.Skin.Animations.Select(a => a.Duration));
    }

    [Fact]
    public async Task Two_carve_processes_write_the_same_bytes_and_one_writes_them_twice()
    {
        var first = Path.Combine(Root, "a");
        var second = Path.Combine(Root, "b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        using (var carve = await StartChainAsync(first))
        {
            Ok(await carve.CallToolAsync("export", """{"path":"chain.glb"}"""));
            Ok(await carve.CallToolAsync("export", """{"path":"again/chain.glb"}"""));
        }
        using (var carve = await StartChainAsync(second))
            Ok(await carve.CallToolAsync("export", """{"path":"chain.glb"}"""));

        var a = File.ReadAllBytes(Path.Combine(first, "chain.glb"));
        Assert.Equal(a, File.ReadAllBytes(Path.Combine(second, "chain.glb")));
        // The file stem names the mesh, node and skin, so the second export keeps the stem in another folder.
        Assert.Equal(a, File.ReadAllBytes(Path.Combine(first, "again", "chain.glb")));
    }
}
