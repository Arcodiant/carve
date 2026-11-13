using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using Microsoft.Extensions.DependencyInjection;
using Model = Carve.Core.Model;
using PolygonMesh = Carve.Core.PolygonMesh;
using Skeleton = Carve.Core.Skeleton;

namespace Carve.Server.Tests;

/// <summary>
/// T-59: export writes each face corner's UV as TEXCOORD_0, splitting a vertex where its UV or normal differ,
/// read back through the engine's IModelGeometryLoader.Load from a content folder as a game would.
/// </summary>
[Trait("Tier", "regression")]
public class UvExportTests : IDisposable
{
    private readonly string _content = Path.Combine(Path.GetTempPath(), "carve-uv-export-" + Guid.NewGuid().ToString("N"));

    public UvExportTests() => Directory.CreateDirectory(Path.Combine(_content, "models"));

    public void Dispose() => Directory.Delete(_content, true);

    private IModelGeometryLoader Loader()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmpyreanCore();
        services.AddEmpyreanGeometry();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IContentRegistry>().Mount(ContentSources.Directory(_content));
        return provider.GetRequiredService<IModelGeometryLoader>();
    }

    private ParsedModel Load(byte[] glb)
    {
        File.WriteAllBytes(Path.Combine(_content, "models", "m.glb"), glb);
        return Loader().Load(new ContentPath("models/m.glb"));
    }

    /// <summary>Distinct UVs per corner, on the faces listed; the rest unmapped.</summary>
    private static PolygonMesh WithUvs(PolygonMesh mesh, params int[] faces)
    {
        var uvs = MeshUvs.None;
        foreach (var f in faces)
            uvs = uvs.WithFace(f, [.. mesh.Faces[f].Corners.Select((_, k) => new Vector2((f * 4 + k) / 32f + 0.01f, 1f - (f * 4 + k) / 37f))]);
        return mesh.WithUvs(uvs);
    }

    private static string Bits(Vector2 v) => $"{BitConverter.SingleToInt32Bits(v.X):X8} {BitConverter.SingleToInt32Bits(v.Y):X8} ({v.X:R}, {v.Y:R})";

    /// <summary>Carve's attributes for each triangle corner in the order the engine parser reads them: (A, C, B) of each triangle.</summary>
    private static (Vector3 Position, Vector3 Normal, Vector2 Uv)[] Expected(PolygonMesh mesh)
    {
        var shaded = Shading.Triangles(mesh);
        var ids = Triangulator.Triangulate(mesh);
        var result = new List<(Vector3, Vector3, Vector2)>();
        for (var t = 0; t < shaded.Count; t++)
        {
            var face = mesh.Faces[ids[t].Face];
            var at = t;
            Vector2 Uv(int v) => mesh.Uvs?.Of(ids[at].Face) is { } uv ? uv[face.CornerOf(v)] : Vector2.Zero;
            var s = shaded[t];
            result.Add((s.A, s.NormalA, Uv(ids[t].A)));
            result.Add((s.C, s.NormalC, Uv(ids[t].C)));
            result.Add((s.B, s.NormalB, Uv(ids[t].B)));
        }
        return [.. result];
    }

    private static void AssertRead(ParsedModel parsed, (Vector3 Position, Vector3 Normal, Vector2 Uv)[] expected)
    {
        var vertices = parsed.MeshData.Vertices;
        var indices = parsed.MeshData.Indices!;
        Assert.Equal(expected.Length, indices.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            var v = vertices[(int)indices[i]];
            Assert.True(Bits(expected[i].Uv) == Bits(v.TexCoord), $"corner {i}: UV {Bits(v.TexCoord)}, expected {Bits(expected[i].Uv)}");
            Assert.Equal(expected[i].Position, v.Position);
        }

        // One vertex per distinct (position, normal, UV): corners that agree merge, the others split.
        var distinct = expected.Select(e => (e.Position, e.Normal, Uv: Bits(e.Uv))).Distinct().Count();
        Assert.Equal(distinct, vertices.Length);
    }

    [Fact]
    public void A_cube_with_UVs_on_some_faces_reads_back_each_corners_UV_bit_for_bit_and_splits_vertices()
    {
        var mesh = WithUvs(TestMeshes.Cube(), 0, 1, 3);
        var parsed = Load(GlbExport.Write(mesh, Shading.Triangles(mesh), "m"));
        AssertRead(parsed, Expected(mesh));
        Assert.Equal(24, parsed.MeshData.Vertices.Length); // flat normals already split every corner of a cube
    }

    [Fact]
    public void A_smooth_mesh_splits_where_the_UV_differs_though_the_normal_is_the_same()
    {
        var smooth = TestMeshes.Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, []));
        PolygonMesh Mapped(float second) => smooth.WithUvs(Enumerable.Range(0, 6).Aggregate(MeshUvs.None,
            (uvs, f) => uvs.WithFace(f, [.. smooth.Faces[f].Corners.Select(v => new Vector2(v * 0.1f, f == 1 ? second : 0.5f))])));
        var shared = Mapped(0.5f);
        var apart = Mapped(0.75f);

        var together = Load(GlbExport.Write(shared, Shading.Triangles(shared), "m"));
        AssertRead(together, Expected(shared));
        var split = Load(GlbExport.Write(apart, Shading.Triangles(apart), "m"));
        AssertRead(split, Expected(apart));
        Assert.True(split.MeshData.Vertices.Length > together.MeshData.Vertices.Length,
            $"a differing UV must split: {together.MeshData.Vertices.Length} vertices with equal UVs, {split.MeshData.Vertices.Length} with differing");
    }

    [Fact]
    public void The_skinned_two_bone_chain_with_UVs_reads_back_each_corners_UV_bit_for_bit()
    {
        var rig = Skeleton.Create([
            new BoneSpec("Root", null, new Vector3(0, 0, 0), Quaternion.Identity),
            new BoneSpec("Tip", "Root", new Vector3(0, 1, 0), Quaternion.Identity),
        ]).Skeleton!;
        var cube = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v + new Vector3(0, 0.5f, 0)), TestMeshes.CubeFaces);
        var weights = VertexWeights.None;
        for (var v = 0; v < cube.Vertices.Count; v++)
            weights = weights.With([v], cube.Vertices[v].Y > 0.5f ? [new Influence(1, 1.0)] : [new Influence(0, 1.0)]);
        var mesh = WithUvs(cube.WithWeights(weights), 0, 2, 4, 5);
        var model = Model.Of("chain", mesh).WithSkeleton(rig);

        var glb = SkinnedExport.Write(model, "m");
        AssertRead(Load(glb.Bytes), Expected(mesh));
        Assert.Contains("chain: 2 faces have no UVs; their corners are written as (0, 0) (faces 1, 3)", glb.Warnings);
    }

    [Fact]
    public void Unmapped_faces_warn_with_the_count_and_the_first_face_ids()
    {
        var model = Model.Of("crate", WithUvs(TestMeshes.Cube(), 0, 1));
        Assert.Equal(["crate: 4 faces have no UVs; their corners are written as (0, 0) (faces 2, 3, 4, 5)"], GlbExport.UvWarnings(model));

        var many = Model.Of("big", WithUvs(TestMeshes.Crate(), 0));
        var warning = Assert.Single(GlbExport.UvWarnings(many));
        Assert.Matches(@"^big: \d+ faces have no UVs; their corners are written as \(0, 0\) \(faces 1, 2, 3, 4, 5 and \d+ more\)$", warning);

        var one = Model.Of("one", WithUvs(TestMeshes.Cube(), 0, 1, 2, 3, 4));
        Assert.Equal(["one: 1 face has no UVs; its corners are written as (0, 0) (face 5)"], GlbExport.UvWarnings(one));
    }

    [Fact]
    public void A_static_model_of_two_meshes_with_UVs_reads_back_each_corners_UV_in_mesh_order_and_warns_per_piece()
    {
        var a = WithUvs(TestMeshes.Cube(), 0, 1, 2, 3, 4, 5);
        var b = WithUvs(TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v + new Vector3(3, 0, 0)), TestMeshes.CubeFaces), 0, 2);
        var model = Model.Of("a", a).With("b", b);
        var combined = model.Combined!;

        var parsed = Load(GlbExport.Write(combined, Shading.Triangles(combined), "m"));
        // Mesh a's corners come first, then b's, each as that piece alone writes them.
        AssertRead(parsed, [.. Expected(a), .. Expected(b)]);
        Assert.Equal(["b: 4 faces have no UVs; their corners are written as (0, 0) (faces 1, 3, 4, 5)"], GlbExport.UvWarnings(model));
    }

    [Fact]
    public void No_warning_when_every_face_is_mapped_or_none_is()
    {
        Assert.Empty(GlbExport.UvWarnings(Model.Of("a", WithUvs(TestMeshes.Cube(), 0, 1, 2, 3, 4, 5))));
        Assert.Empty(GlbExport.UvWarnings(Model.Of("a", TestMeshes.Cube())));
    }

    [Fact]
    public void A_mesh_with_no_UVs_writes_no_TEXCOORD_and_the_bytes_it_always_did()
    {
        foreach (var (name, mesh, hash) in new (string, PolygonMesh, string)[]
        {
            ("cube", TestMeshes.Cube(), "F98BE3ED86E756BCB702F928869470F2D1267A6132CC1EFFBFA177F7FB06FBBB"),
            ("lblock", TestMeshes.LBlock(), "8655B26105B6B495E99C072DFDF29E930180A489B527828BF346A851A278FC08"),
            ("crate", TestMeshes.Crate(), "074FEBF3D9553EFFCF858B0C46EA286993FA5239FE27E4601413FE5DB30F15E9"),
        })
        {
            var bytes = GlbExport.Write(mesh, Shading.Triangles(mesh), name);
            Assert.DoesNotContain("TEXCOORD_0", Encoding.ASCII.GetString(bytes));
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }

    [Fact]
    public void Seams_alone_are_not_UVs_and_write_none()
    {
        var cube = TestMeshes.Cube();
        var seamed = cube.WithUvs(MeshUvs.None.WithSeams([(0, 1)], true));
        Assert.Equal(GlbExport.Write(cube, Shading.Triangles(cube), "m"), GlbExport.Write(seamed, Shading.Triangles(seamed), "m"));
        Assert.Empty(GlbExport.UvWarnings(Model.Of("a", seamed)));
    }
}
