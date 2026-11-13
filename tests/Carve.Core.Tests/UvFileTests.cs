using System.Numerics;
using Xunit;
using static Carve.Core.Tests.Uv;

namespace Carve.Core.Tests;

/// <summary>Version 5 of the Carve file (T-53): <c>uvs</c> and <c>seams</c>, bit-exact, and every older file unchanged.</summary>
[Trait("Tier", "unit")]
public class UvFileTests
{
    private static readonly float Subnormal = BitConverter.Int32BitsToSingle(1);

    /// <summary>A cube with UVs on faces 0, 2 and 5 (one outside 0..1, one negative, one subnormal) and two seams.</summary>
    private static PolygonMesh MappedCube() => TestMeshes.Cube().WithUvs(MeshUvs.None
        .WithFace(0, Square)
        .WithFace(2, [V(0, 0), V(1.75f, 0), V(1.75f, -0.25f), V(0.1f, Subnormal)])
        .WithFace(5, [V(0.333333343f, 0.5f), V(1, 0.5f), V(1, 1), V(0.333333343f, 1)])
        .WithSeams([(6, 2), (0, 1)], true));

    private static Model Read(string text)
    {
        var read = CarveFile.Read(text);
        Assert.True(read.Model is not null, string.Join("; ", read.Errors));
        return read.Model!;
    }

    [Fact]
    public void A_mapped_cube_round_trips_bit_exact()
    {
        var cube = MappedCube();
        var text = CarveFile.Write(cube);
        var back = Read(text).Meshes[0].Mesh;
        BitEqual(cube.Uvs, back.Uvs);
        Assert.Equal(text, CarveFile.Write(back));
    }

    [Fact]
    public void The_written_text_is_version_5_with_uvs_and_seams_last_and_left_out_when_empty()
    {
        var model = Model.Of("box", MappedCube()).With("plain", TestMeshes.Cube());
        var text = CarveFile.Write(model).ReplaceLineEndings("\n");
        Assert.StartsWith("{\n  \"carve\": 5,\n", text);
        Assert.Contains("""
                  "shading": "flat",
                  "hard_edges": [],
                  "uvs": [
                    {"face": 0, "uv": [[0, 0], [1, 0], [1, 1], [0, 1]]},
                    {"face": 2, "uv": [[0, 0], [1.75, 0], [1.75, -0.25], [0.1, 1E-45]]},
                    {"face": 5, "uv": [[0.33333334, 0.5], [1, 0.5], [1, 1], [0.33333334, 1]]}
                  ],
                  "seams": [
                    [0, 1],
                    [2, 6]
                  ]
                },
                {
                  "name": "plain",
            """.ReplaceLineEndings("\n"), text);
        Assert.Contains("""
                  "hard_edges": []
                }
              ],
            """.ReplaceLineEndings("\n"), text);
        Assert.Equal(2, Read(text).Meshes.Count);
        Assert.Null(Read(text).Meshes[1].Mesh.Uvs);
    }

    [Fact]
    public void Seams_alone_and_uvs_alone_each_write_version_5()
    {
        var seams = TestMeshes.Cube().WithUvs(MeshUvs.None.WithSeams([(0, 1)], true));
        var text = CarveFile.Write(seams).ReplaceLineEndings("\n");
        Assert.Contains("\"carve\": 5,", text);
        Assert.Contains("\"hard_edges\": [],\n      \"seams\": [\n        [0, 1]\n      ]\n", text);
        Assert.DoesNotContain("\"uvs\"", text);
        Assert.Equal([(0, 1)], Read(text).Meshes[0].Mesh.Seams);

        var uvs = TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(1, Square));
        var only = CarveFile.Write(uvs).ReplaceLineEndings("\n");
        Assert.Contains("{\"face\": 1, \"uv\": [[0, 0], [1, 0], [1, 1], [0, 1]]}\n      ]\n    }", only);
        Assert.DoesNotContain("\"seams\"", only);
    }

    [Fact]
    public void A_holed_face_has_one_uv_per_corner_holes_included_and_round_trips()
    {
        Vector3[] plate =
        [
            new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0),
            new(-0.5f, -0.5f, 0), new(-0.5f, 0.5f, 0), new(0.5f, 0.5f, 0), new(0.5f, -0.5f, 0),
        ];
        var mesh = PolygonMesh.Create(plate, [new Face([0, 1, 2, 3], [[4, 5, 6, 7]])]).Mesh!;
        var mapped = Mapped(mesh);
        Assert.Equal(8, mapped.Uvs!.Of(0)!.Count);
        Assert.Equal(V(-0.5f, 0.5f), mapped.Uvs.Of(0)![5]);
        var text = CarveFile.Write(mapped);
        Assert.Contains("{\"face\": 0, \"uv\": [[-1, -1], [1, -1], [1, 1], [-1, 1], [-0.5, -0.5], [-0.5, 0.5], [0.5, 0.5], [0.5, -0.5]]}", text);
        BitEqual(mapped.Uvs, Read(text).Meshes[0].Mesh.Uvs);
    }

    [Fact]
    public void Uvs_beside_tombstones_round_trip()
    {
        var cut = MeshDelete.Delete(Mapped(TestMeshes.Cube(), (0, 1)), null, [3]).Mesh!;
        Assert.True(cut.IsFaceDeleted(3));
        var text = CarveFile.Write(cut);
        var back = Read(text).Meshes[0].Mesh;
        Assert.True(back.IsFaceDeleted(3));
        BitEqual(cut.Uvs, back.Uvs);
        Assert.Equal(text, CarveFile.Write(back));
    }

    [Fact]
    public void A_model_with_no_uvs_writes_the_version_it_did()
    {
        Assert.Contains("\"carve\": 2,", CarveFile.Write(TestMeshes.Cube()));
        Assert.Contains("\"carve\": 3,", CarveFile.Write(TestMeshes.Cube().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth))));
        Assert.Contains("\"carve\": 4,", CarveFile.Write(MeshDelete.Delete(TestMeshes.Cube(), null, [3]).Mesh!));
    }

    /// <summary>The v1 fixture and the v2, v3 and v4 samples read with no UVs; the samples re-write to their committed text.</summary>
    [Theory]
    [InlineData("tests/Carve.Core.Tests/Fixtures/leaky-shed-v1.carve", 1, false)]
    [InlineData("samples/leaky-shed.carve", 2, true)]
    [InlineData("samples/gardener.carve", 2, true)]
    [InlineData("samples/gardener-soft.carve", 3, true)]
    [InlineData("samples/gatehouse.carve", 4, true)]
    public void Older_files_read_unchanged(string path, int version, bool rewrites)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), path));
        var read = CarveFile.Read(text);
        Assert.True(read.Model is not null, string.Join("; ", read.Errors));
        Assert.Equal(version, read.Version);
        Assert.All(read.Model!.Meshes, m => Assert.Null(m.Mesh.Uvs));
        if (rewrites) Assert.Equal(text.ReplaceLineEndings("\n"), CarveFile.Write(read.Model).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Uvs_in_a_version_4_file_are_an_unknown_key()
    {
        var text = CarveFile.Write(MappedCube()).Replace("\"carve\": 5,", "\"carve\": 4,");
        var errors = CarveFile.Read(text).Errors;
        Assert.Contains(errors, e => e.Element == "meshes[0].uvs" && e.Message.StartsWith("unknown key"));
        Assert.Contains(errors, e => e.Element == "meshes[0].seams" && e.Message.StartsWith("unknown key"));
    }

    public static TheoryData<string, string, string, string> FileErrors => new()
    {
        { "{\"face\": 0, \"uv\": [[0, 0], [1, 0], [1, 1], [0, 1]]}", "{\"face\": 0, \"uv\": [[0, 0], [1, 0], [1, 1]]}",
            "meshes[0].uvs[0].uv", "3 UVs given, but the face has 4 corners; write 4 UVs, one per corner" },
        { "{\"face\": 0,", "{\"face\": 6,", "meshes[0].uvs[0].face", "face 6 is not a face of the mesh, which has 6" },
        { "{\"face\": 2,", "{\"face\": 0,", "meshes[0].uvs[1].face", "face 0 is listed earlier; list each face once" },
        { "[[0, 0], [1, 0], [1, 1], [0, 1]]", "[[0, 0], [1e39, 0], [1, 1], [0, 1]]", "meshes[0].uvs[0].uv[1][0]", "1e39 is not finite: it is outside the range of a float" },
        { "[[0, 0], [1, 0], [1, 1], [0, 1]]", "[[0, 0], [1, 0, 0], [1, 1], [0, 1]]", "meshes[0].uvs[0].uv[1]", "uv item has 3 numbers; a UV is a pair of numbers, [u, v]" },
        { "[0, 1],", "[0, 6],", "meshes[0].seams[0]", "0-6 is not an edge of the mesh" },
        { "[0, 1],", "[6, 2],", "meshes[0].seams[1]", "edge 2-6 is listed earlier; list each edge once" },
        { "{\"face\": 0, \"uv\"", "{\"face\": 0, \"corners\": 4, \"uv\"", "meshes[0].uvs[0].corners", "unknown key: a face's UVs has only the keys \"face\" and \"uv\"" },
    };

    [Theory]
    [MemberData(nameof(FileErrors))]
    public void Each_file_error_is_named_by_element(string find, string replace, string element, string message)
    {
        var text = CarveFile.Write(MappedCube());
        Assert.Contains(find, text);
        var errors = CarveFile.Read(text.Replace(find, replace)).Errors;
        Assert.Contains((element, message), errors.Select(e => (e.Element, e.Message)));
    }

    [Fact]
    public void A_uvs_entry_naming_a_deleted_face_is_refused()
    {
        var cut = MeshDelete.Delete(Mapped(TestMeshes.Cube()), null, [3]).Mesh!;
        var text = CarveFile.Write(cut).Replace("{\"face\": 4,", "{\"face\": 3,");
        Assert.Contains(("meshes[0].uvs[3].face", "face 3 is deleted, and a deleted face holds no UVs"),
            CarveFile.Read(text).Errors.Select(e => (e.Element, e.Message)));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "gatehouse.carve"))) dir = dir.Parent!;
        return dir.FullName;
    }
}
