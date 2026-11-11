using System.Numerics;
using System.Text.Json;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using static Carve.Server.Tests.CarveProcess;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>What render draws and what the .glb holds are one function's normals (D-6, Smooth shading is per mesh).</summary>
[Trait("Tier", "unit")]
public class ShadingNormalsTests
{
    private static string Bits(Vector3 v) => $"{BitConverter.SingleToInt32Bits(v.X):X8} {BitConverter.SingleToInt32Bits(v.Y):X8} {BitConverter.SingleToInt32Bits(v.Z):X8}";

    public static TheoryData<string> Meshes => ["flat cube", "smooth cube", "creased cube", "smooth crate", "smooth lblock"];

    private static PolygonMesh Mesh(string name) => name switch
    {
        "flat cube" => TestMeshes.Cube(),
        "smooth cube" => TestMeshes.Cube().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth)),
        "creased cube" => TestMeshes.Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1), (0, 3), (4, 5)])),
        "smooth crate" => TestMeshes.Crate().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth)),
        _ => TestMeshes.LBlock().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth)),
    };

    [Theory]
    [MemberData(nameof(Meshes))]
    public void The_rendered_normals_are_the_loaded_normals_bit_for_bit_corner_by_corner(string name)
    {
        var mesh = Mesh(name);
        var space = DrawnSpace.Of(new Vector3(-1000), new Vector3(1000));
        var drawn = MeshBridge.ToMeshData(mesh, new DrawnSpace(0, 0, 0, 1));
        var parsed = GlbExportTests.Parser.Parse(GlbExport.Write(mesh, Shading.Triangles(mesh), name), ModelFormat.Gltf);
        var vertices = parsed.MeshData.Vertices;
        var indices = parsed.MeshData.Indices!;

        Assert.Equal(drawn.Vertices!.Length, indices.Length);
        for (var i = 0; i < indices.Length; i++)
        {
            Assert.True(Bits(drawn.Vertices[drawn.Indices![i]].Normal) == Bits(vertices[(int)indices[i]].Normal),
                $"corner {i}: drawn {drawn.Vertices[drawn.Indices[i]].Normal}, loaded {vertices[(int)indices[i]].Normal}");
        }
        _ = space;
    }

    [Fact]
    public void A_smooth_cubes_loaded_corner_normals_are_the_unit_diagonals_and_the_flat_cubes_the_faces()
    {
        var smooth = GlbExportTests.Parser.Parse(GlbExport.Write(Mesh("smooth cube"), Shading.Triangles(Mesh("smooth cube")), "c"), ModelFormat.Gltf);
        foreach (var v in smooth.MeshData.Vertices)
        {
            Assert.InRange(v.Normal.Length(), 0.99999f, 1.00001f);
            Assert.True(MathF.Abs(MathF.Abs(v.Normal.X) - 1 / MathF.Sqrt(3)) < 1e-5f && MathF.Abs(MathF.Abs(v.Normal.Z) - 1 / MathF.Sqrt(3)) < 1e-5f);
        }
        var flat = GlbExportTests.Parser.Parse(GlbExport.Write(Mesh("flat cube"), Shading.Triangles(Mesh("flat cube")), "c"), ModelFormat.Gltf);
        Assert.All(flat.MeshData.Vertices, v => Assert.Equal(1f, MathF.Max(MathF.Abs(v.Normal.X), MathF.Max(MathF.Abs(v.Normal.Y), MathF.Abs(v.Normal.Z)))));
    }
}

/// <summary>set_shading and mark_edges through the real tools: their results, refusals, undo and the file.</summary>
[Trait("Tier", "regression")]
public sealed class ShadingToolTests() : TempFolderTests("carve-shading")
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static void Plain(JsonElement result) => Assert.False(IsError(result), TextOf(result));

    private static string Pairs(JsonElement list) => string.Join(" ", list.EnumerateArray().Select(p => $"{p[0]}-{p[1]}"));

    [Fact]
    public async Task Shading_and_marks_set_report_undo_and_survive_a_save_and_open()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Cube));

        var smooth = Ok(await carve.CallToolAsync("set_shading", """{"shading":"smooth"}"""));
        Assert.Equal("smooth", smooth.GetProperty("shading").GetString());
        var marked = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[1,0],[3,0]]}"""));
        Assert.Equal("0-1 0-3", Pairs(marked.GetProperty("marked")));
        Assert.Equal("0-1 0-3", Pairs(marked.GetProperty("hard_edges")));
        var cleared = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[0,3]],"hard":false}"""));
        Assert.Equal("0-1", Pairs(cleared.GetProperty("hard_edges")));

        Plain(await carve.CallToolAsync("save", """{"path":"soft.carve"}"""));
        var text = File.ReadAllText(Path.Combine(Root, "soft.carve"));
        Assert.Contains("\"carve\": 3,", text);
        Assert.Contains("\"shading\": \"smooth\"", text);

        Plain(await carve.CallToolAsync("undo", "{}"));
        var again = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[0,3]]}"""));
        Assert.Equal("0-1 0-3", Pairs(again.GetProperty("hard_edges")));

        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Plain(await carve.CallToolAsync("open", """{"path":"soft.carve"}"""));
        var back = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[0,4]],"hard":false}"""));
        Assert.Equal("smooth", back.GetProperty("shading").GetString());
        Assert.Equal("0-1", Pairs(back.GetProperty("hard_edges")));
    }

    [Fact]
    public async Task A_bad_shading_or_edge_is_refused_naming_it_and_changes_nothing()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Assert.True(IsError(await carve.CallToolAsync("set_shading", """{"shading":"smooth"}""")));
        Ok(await carve.CallToolAsync("define_mesh", Cube));

        Assert.Contains("shading", TextOf(await carve.CallToolAsync("set_shading", """{"shading":"round"}""")));
        var refused = await carve.CallToolAsync("mark_edges", """{"edges":[[0,1],[0,6]]}""");
        Assert.True(IsError(refused));
        Assert.Contains("edges[0-6]", TextOf(refused));
        Assert.True(IsError(await carve.CallToolAsync("mark_edges", """{"edges":[]}""")));
        Assert.True(IsError(await carve.CallToolAsync("mark_edges", """{"edges":[[0]]}""")));
        var after = Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[1,2]]}"""));
        Assert.Equal("flat", after.GetProperty("shading").GetString());
        Assert.Equal("1-2", Pairs(after.GetProperty("hard_edges")));
    }
}
