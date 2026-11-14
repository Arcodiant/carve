using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>What the UV tests share (T-53): a unit square's UVs, a cube mapped by projection, and bit-exact comparison.</summary>
internal static class Uv
{
    public static Vector2 V(float u, float v) => new(u, v);

    public static readonly Vector2[] Square = [V(0, 0), V(1, 0), V(1, 1), V(0, 1)];

    /// <summary>
    /// A UV for <paramref name="p"/> on a face of normal <paramref name="normal"/>: its position projected on the two axes
    /// the normal's main axis leaves. Affine in the face, so every interpolation of it gives it back.
    /// </summary>
    public static Vector2 Projected(Vector3 p, Vector3 normal)
    {
        var n = Vector3.Abs(normal);
        return n.Z >= n.X && n.Z >= n.Y ? V(p.X, p.Y) : n.Y >= n.X ? V(p.X, p.Z) : V(p.Y, p.Z);
    }

    public static Vector3 Normal(PolygonMesh m, int face)
    {
        var c = m.Faces[face].Loop;
        var n = Vector3.Zero;
        for (var i = 0; i < c.Count; i++)
        {
            var (a, b) = (m.Vertices[c[i]], m.Vertices[c[(i + 1) % c.Count]]);
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }
        return n;
    }

    /// <summary>Every live face of <paramref name="m"/> mapped by <see cref="Projected"/>, with seams on <paramref name="seams"/>.</summary>
    public static PolygonMesh Mapped(PolygonMesh m, params (int, int)[] seams) => m.WithUvs(MeshUvs.None
        .WithFaces(m.LiveFaces.Select(f => KeyValuePair.Create(f, (IReadOnlyList<Vector2>?)ProjectedCorners(m, f))))
        .WithSeams(seams, true));

    public static Vector2[] ProjectedCorners(PolygonMesh m, int f) =>
        m.Faces[f].Corners.Select(v => Projected(m.Vertices[v], Normal(m, f))).ToArray();

    public static void BitEqual(IReadOnlyList<Vector2> expected, IReadOnlyList<Vector2>? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(BitConverter.SingleToInt32Bits(expected[i].X) == BitConverter.SingleToInt32Bits(actual[i].X)
                && BitConverter.SingleToInt32Bits(expected[i].Y) == BitConverter.SingleToInt32Bits(actual[i].Y),
                $"corner {i}: expected {expected[i]}, got {actual[i]}");
        }
    }

    public static void BitEqual(MeshUvs? expected, MeshUvs? actual)
    {
        Assert.Equal(expected is null, actual is null);
        if (expected is null) return;
        Assert.Equal(expected.MappedFaces, actual!.MappedFaces);
        foreach (var f in expected.MappedFaces) BitEqual(expected.Of(f)!, actual.Of(f));
        Assert.Equal(expected.Seams, actual.Seams);
    }
}

[Trait("Tier", "unit")]
public class MeshUvsTests
{
    [Fact]
    public void Empty_uvs_are_null_on_the_mesh()
    {
        var cube = TestMeshes.Cube();
        Assert.Null(cube.Uvs);
        Assert.Null(cube.WithUvs(MeshUvs.None).Uvs);
        Assert.Null(cube.WithUvs(MeshUvs.None.WithFace(0, Uv.Square).WithFace(0, null)).Uvs);
        Assert.Empty(cube.Seams);
    }

    [Fact]
    public void A_face_is_mapped_by_its_corners_and_unmapped_by_null()
    {
        var uvs = MeshUvs.None.WithFace(3, Uv.Square).WithFace(1, Uv.Square);
        Assert.Equal([1, 3], uvs.MappedFaces);
        Assert.True(uvs.IsMapped(3));
        Assert.False(uvs.IsMapped(0));
        Assert.Null(uvs.Of(0));
        Uv.BitEqual(Uv.Square, uvs.Of(3));
        Assert.Equal([3], uvs.WithFace(1, null).MappedFaces);
    }

    [Fact]
    public void Seams_are_sorted_deduplicated_and_order_free()
    {
        var uvs = MeshUvs.None.WithSeams([(5, 1), (1, 5), (3, 0)], true);
        Assert.Equal([(0, 3), (1, 5)], uvs.Seams);
        Assert.True(uvs.IsSeam(5, 1));
        Assert.True(uvs.IsSeam(0, 3));
        Assert.Equal([(1, 5)], uvs.WithSeams([(3, 0)], false).Seams);
        var cube = TestMeshes.Cube().WithUvs(uvs);
        Assert.Equal([(0, 3), (1, 5)], cube.Seams);
    }

    [Fact]
    public void Check_names_each_fault()
    {
        var cube = MeshDelete.Delete(TestMeshes.Cube(), null, [5]).Mesh!;
        var uvs = MeshUvs.None
            .WithFace(0, [Uv.V(0, 0), Uv.V(float.NaN, 0), Uv.V(1, 1), Uv.V(0, float.PositiveInfinity)])
            .WithFace(1, Uv.Square.Take(3).ToArray())
            .WithFace(5, Uv.Square)
            .WithFace(9, Uv.Square)
            .WithSeams([(0, 6), (0, 1)], true);
        var errors = MeshUvs.Check(cube, uvs, "meshes[0].");
        Assert.Equal(
        [
            ("meshes[0].faces[0].uv[1]", "a UV is not finite"),
            ("meshes[0].faces[0].uv[3]", "a UV is not finite"),
            ("meshes[0].faces[1]", "3 UVs given, but the face has 4 corners; write 4 UVs, one per corner"),
            ("meshes[0].faces[5]", "face 5 is deleted"),
            ("meshes[0].faces[9]", "face 9 is not a face of the mesh, which has 6"),
            ("meshes[0].seams[0-6]", "0-6 is not an edge of the mesh"),
        ], errors.Select(e => (e.Element, e.Message)));
        Assert.Empty(MeshUvs.Check(TestMeshes.Cube(), Uv.Mapped(TestMeshes.Cube(), (0, 1)).Uvs!));
    }

    [Fact]
    public void Uvs_and_the_other_attributes_are_independent()
    {
        var cube = Uv.Mapped(TestMeshes.Cube(), (0, 1));
        var uvs = cube.Uvs;
        var style = FaceStyle.None.WithColour([0], "#ff0000");
        var weights = VertexWeights.Of([KeyValuePair.Create(0, new[] { new Influence(0, 1) })]);
        var shade = MeshShade.Of(ShadingMode.Smooth, [(0, 1)]);

        Assert.Same(uvs, cube.WithStyle(style).Uvs);
        Assert.Same(uvs, cube.WithWeights(weights).Uvs);
        Assert.Same(uvs, cube.WithShade(shade).Uvs);
        Assert.Same(uvs, cube.WithShade(Shading.MarkEdges(cube, [(1, 2)], true, out _)).Uvs);
        Assert.Same(uvs, cube.WithVertices(cube.Vertices.Select(v => v * 2).ToArray()).Uvs);

        var dressed = TestMeshes.Cube().WithStyle(style).WithWeights(weights).WithShade(shade);
        var mapped = dressed.WithUvs(uvs);
        Assert.Same(dressed.Style, mapped.Style);
        Assert.Same(dressed.Weights, mapped.Weights);
        Assert.Same(dressed.Shade, mapped.Shade);
        Assert.Equal([(0, 1)], mapped.HardEdges);
        Assert.Equal([(0, 1)], mapped.Seams);
    }
}

/// <summary>The barrel M-6 maps (T-53's Notes), as checked through carve on 2026-11-09.</summary>
[Trait("Tier", "unit")]
public class BarrelTests
{
    [Fact]
    public void The_barrel_is_40_vertices_34_faces_72_edges_closed_with_its_volume()
    {
        var barrel = TestMeshes.Barrel();
        var stats = MeshHealth.Create(barrel).Stats;
        Assert.Equal((40, 34, 72, true), (stats.Vertices, stats.Faces, stats.Edges, stats.Closed));
        Assert.Equal(0.4398, Math.Round(stats.Volume!.Value, 4));
        Assert.Null(barrel.Uvs);
        Assert.Empty(barrel.Seams);
        Assert.Equal(new System.Numerics.Vector3(0.296985f, 0.5f, -0.296985f), barrel.Vertices[17]);
    }
}
