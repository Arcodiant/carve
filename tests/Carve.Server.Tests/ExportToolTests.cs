using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>export through the real server, in a temp working directory.</summary>
[Trait("Tier", "regression")]
public sealed class ExportToolTests() : FileToolTests("carve-export")
{
    /// <summary>export's result: not an error, and one line.</summary>
    private static string Line(JsonElement result)
    {
        var text = Ok(result);
        Assert.DoesNotContain('\n', text);
        return text;
    }

    [Fact]
    public async Task Export_writes_UVs_and_the_result_warns_of_unmapped_faces()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var set = await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]},{"face":2,"uv":[[0,0],[1,0],[1,1],[0,1]]}]}""");
        Assert.False(IsError(set), TextOf(set));

        var line = Line(await carve.CallToolAsync("export", """{"path":"out/uv.glb"}"""));
        Assert.True(line.EndsWith("; 1 warning(s): mesh: 4 faces have no UVs; their corners are written as (0, 0) (faces 1, 3, 4, 5)"), line);
        Assert.Contains("TEXCOORD_0", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(Root, "out", "uv.glb"))));
    }

    [Fact]
    public async Task A_skinned_export_warns_of_unmapped_faces_after_the_limit_warnings()
    {
        using var carve = await SkinnedExportToolTests.StartChainAsync(Root);
        // Every face of 'lower' mapped, and all of 'upper' but face 3.
        static string Faces(IEnumerable<int> faces) => string.Join(",", faces.Select(f => $$"""{"face":{{f}},"uv":[[0,0],[1,0],[1,1],[0,1]]}"""));
        Ok(await carve.CallToolAsync("set_uvs", $$"""{"mesh":"lower","faces":[{{Faces(Enumerable.Range(0, 6))}}]}"""));
        Ok(await carve.CallToolAsync("set_uvs", $$"""{"mesh":"upper","faces":[{{Faces([0, 1, 2, 4, 5])}}]}"""));
        var limits = Path.Combine(Root, "carve-limits.json");
        File.WriteAllText(limits, """{"max_triangles": 23}""");

        var line = Line(await carve.CallToolAsync("export", """{"path":"chain.glb"}"""));
        Assert.Contains(" skinned: ", line);
        Assert.True(line.EndsWith($"; limits from {limits}; 2 warning(s): 24 triangles, above the game's max_triangles 23; "
            + "upper: 1 face has no UVs; its corners are written as (0, 0) (face 3)"), line);
    }

    [Fact]
    public async Task Export_writes_the_cube_resolving_a_relative_path_and_says_when_it_replaced()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));
        var full = Path.Combine(Root, "out", "cube.glb");

        var first = Line(await carve.CallToolAsync("export", """{"path":"out/cube.glb"}"""));
        Assert.Equal($"exported cube.glb to {full}: 12 triangles, 1 material 'carve', size 1 x 1 x 1 m, bounds (0, 0, 0) to (1, 1, 1)"
            + $"; no game limits: no carve-limits.json in {Path.GetDirectoryName(full)} or a folder above it (write_limits: true starts one there)", first);
        var bytes = File.ReadAllBytes(full);
        Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var refused = await carve.CallToolAsync("export", """{"path":"out/cube.glb"}""");
        Assert.True(IsError(refused), TextOf(refused));
        Assert.Contains($"{full} already exists", TextOf(refused));
        Assert.Contains("overwrite: true", TextOf(refused));
        Assert.Equal(bytes, File.ReadAllBytes(full));
        Assert.Contains("overwrite: expected true or false", TextOf(await carve.CallToolAsync("export", """{"path":"out/cube.glb","overwrite":"yes"}""")));

        var second = Line(await carve.CallToolAsync("export", """{"path":"out/CUBE.GLB","overwrite":true}"""));
        Assert.StartsWith($"exported CUBE.GLB to {Path.Combine(Root, "out", "CUBE.GLB")} (replaced): 12 triangles", second);

        // export changes nothing: the mesh is the same, and it is not a step on the undo stack.
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.False(IsError(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}""")));
        var moved = TextOf(await carve.CallToolAsync("get_mesh", "{}"));
        Line(await carve.CallToolAsync("export", """{"path":"out/moved.glb"}"""));
        Assert.Equal(moved, TextOf(await carve.CallToolAsync("get_mesh", "{}")));

        // undo goes straight back over the move, then finds only the first define_mesh, which it cannot take back.
        Assert.Contains("\"undone\":\"move\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Contains("nothing to undo", Refused(await carve.CallToolAsync("undo", "{}")));
    }

    [Fact]
    public async Task Export_before_define_mesh_is_an_error_saying_so()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        Assert.Contains("call define_mesh first", Refused(await carve.CallToolAsync("export", """{"path":"cube.glb"}""")));
        Assert.Empty(Directory.GetFileSystemEntries(Root));
    }

    [Fact]
    public async Task A_wrong_extension_or_an_empty_path_is_an_error_and_writes_nothing()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Assert.Contains("must end in .glb", Refused(await carve.CallToolAsync("export", """{"path":"models/cube.obj"}""")));
        Assert.Contains("path is empty", Refused(await carve.CallToolAsync("export", """{"path":""}""")));
        Assert.Contains("path: required but missing", Refused(await carve.CallToolAsync("export", "{}")));
        Assert.Empty(Directory.GetFileSystemEntries(Root));
    }

    [Fact]
    public async Task A_mesh_with_warnings_exports_and_the_result_repeats_them()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        var defined = TextOf(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Assert.DoesNotContain("\"warnings\":[]", defined);

        var line = Line(await carve.CallToolAsync("export", """{"path":"open.glb"}"""));
        Assert.Contains(": 1 triangles, 1 material 'carve'", line);
        // One lone triangle has three open edges, and each is its own warning.
        Assert.Contains("; 3 warning(s): ", line);
        Assert.Contains("edge belongs to only one face", line);
        Assert.True(File.Exists(Path.Combine(Root, "open.glb")));
    }
}
