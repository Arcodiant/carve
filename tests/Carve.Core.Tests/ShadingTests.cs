using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class ShadingTests
{
    private static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

    private static void SameBits(Vector3 expected, Vector3 actual) =>
        Assert.True(Bits(expected.X) == Bits(actual.X) && Bits(expected.Y) == Bits(actual.Y) && Bits(expected.Z) == Bits(actual.Z),
            $"expected {expected}, got {actual}");

    private static Vector3 CornerOf(PolygonMesh mesh, int face, int vertex) =>
        Shading.Corners(mesh)[face][mesh.Faces[face].ToList().IndexOf(vertex)];

    private static void Near(Vector3 expected, Vector3 actual) =>
        Assert.True((expected - actual).Length() < 1e-6f, $"expected {expected}, got {actual}");

    // Two quads meeting at a right angle along the edge 1-2: a floor (y = 0) and a wall (x = 1).
    private static PolygonMesh Hinge(bool hard, ShadingMode mode = ShadingMode.Smooth)
    {
        var mesh = Make([new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1), new(1, 1, 0), new(1, 1, 1)],
            [[0, 1, 2, 3], [2, 1, 4, 5]]);
        return mesh.WithShade(MeshShade.Of(mode, hard ? [(1, 2)] : []));
    }

    [Theory]
    [InlineData("cube")]
    [InlineData("lblock")]
    [InlineData("crate")]
    public void A_flat_mesh_gives_exactly_FlatShadings_triangles_and_normals(string name)
    {
        var mesh = name switch { "cube" => Cube(), "lblock" => LBlock(), _ => Crate() };
        var flat = FlatShading.Triangles(mesh);
        var shaded = Shading.Triangles(mesh);
        Assert.Equal(flat.Count, shaded.Count);
        for (var t = 0; t < flat.Count; t++)
        {
            Assert.Equal((flat[t].A, flat[t].B, flat[t].C, flat[t].Face), (shaded[t].A, shaded[t].B, shaded[t].C, shaded[t].Face));
            SameBits(flat[t].Normal, shaded[t].NormalA);
            SameBits(flat[t].Normal, shaded[t].NormalB);
            SameBits(flat[t].Normal, shaded[t].NormalC);
        }
    }

    [Fact]
    public void Hard_edges_marked_on_a_flat_mesh_change_nothing()
    {
        var flat = Cube().WithShade(MeshShade.Of(ShadingMode.Flat, [(0, 1)]));
        Assert.Equal(ShadingMode.Flat, flat.ShadingMode);
        var shaded = Shading.Triangles(flat);
        foreach (var t in shaded) SameBits(t.Normal, t.NormalB);
    }

    [Fact]
    public void A_smooth_cube_has_each_corner_pointing_out_along_its_diagonal()
    {
        var mesh = Cube().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth));
        for (var f = 0; f < mesh.Faces.Count; f++)
            foreach (var v in mesh.Faces[f])
            {
                var expected = Vector3.Normalize(mesh.Vertices[v] - new Vector3(0.5f));
                Near(expected, CornerOf(mesh, f, v));
                Assert.InRange(CornerOf(mesh, f, v).Length(), 0.999999f, 1.000001f);
            }
    }

    [Fact]
    public void A_cube_with_one_hard_edge_still_smooths_round_it_and_two_hard_edges_split_a_corner()
    {
        var one = Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1)]));
        // Vertex 0 is joined round its other two edges, so one crease alone leaves it as the unmarked cube has it.
        Near(Vector3.Normalize(new Vector3(-1, -1, -1)), CornerOf(one, 0, 0));
        Near(Vector3.Normalize(new Vector3(-1, -1, -1)), CornerOf(one, 2, 0));

        // 0-1 and 0-3 hard cut the -z face (face 0) off at vertex 0; -y and -x stay joined by 0-4.
        var two = Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1), (0, 3)]));
        SameBits(new Vector3(0, 0, -1), CornerOf(two, 0, 0));
        Near(Vector3.Normalize(new Vector3(-1, -1, 0)), CornerOf(two, 2, 0));
        Near(Vector3.Normalize(new Vector3(-1, -1, 0)), CornerOf(two, 4, 0));
    }

    [Fact]
    public void A_creased_hinge_keeps_each_face_its_own_normal_and_a_soft_one_blends_them()
    {
        var hard = Hinge(hard: true);
        SameBits(new Vector3(0, -1, 0), CornerOf(hard, 0, 1));
        SameBits(new Vector3(1, 0, 0), CornerOf(hard, 1, 1));

        var soft = Hinge(hard: false);
        var blend = Vector3.Normalize(new Vector3(1, -1, 0));
        Near(blend, CornerOf(soft, 0, 1));
        Near(blend, CornerOf(soft, 1, 1));
        // The far corners lie on one face's edge only: nothing to smooth across, so they keep the face normal.
        SameBits(new Vector3(0, -1, 0), CornerOf(soft, 0, 0));
        SameBits(new Vector3(1, 0, 0), CornerOf(soft, 1, 4));
    }

    [Fact]
    public void Corner_normals_weigh_each_face_by_its_corner_angle_not_its_area()
    {
        // At the origin: a floor triangle with a 90 degree corner and a wall triangle with a 30 degree corner, sharing the edge 0-1.
        var wide = MathF.Sqrt(3);
        var mesh = Make([new(0, 0, 0), new(1, 0, 0), new(0, 0, 1), new(0, wide / 2 * 100, 0)],
            [[0, 2, 1], [0, 1, 3]]).WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth));
        var n0 = mesh.FaceNormal(0);
        var n1 = mesh.FaceNormal(1);
        var a0 = Math.PI / 2;
        var a1 = Math.Atan2(Vector3.Cross(new Vector3(1, 0, 0), mesh.Vertices[3]).Length(), mesh.Vertices[3].X);
        var expected = Vector3.Normalize(n0 * (float)a0 + n1 * (float)a1);
        Near(expected, CornerOf(mesh, 0, 0));
        Near(expected, CornerOf(mesh, 1, 0));
        // An area weighting would lean far more to the wall (area 50 against 0.5).
        var byArea = Vector3.Normalize(n0 * 0.5f + n1 * 50f);
        Assert.True((byArea - expected).Length() > 0.1f);
    }

    [Fact]
    public void An_edge_of_three_faces_is_hard_whatever_the_order_of_the_faces()
    {
        // Three quads on the edge 0-1: a fin, floor and wall.
        Vector3[] vertices = [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1), new(1, 1, 0), new(0, 1, 0), new(1, -1, 0), new(0, -1, 0)];
        int[][] faces = [[0, 3, 2, 1], [1, 4, 5, 0], [0, 7, 6, 1]];
        var normals = new List<Vector3[]>();
        foreach (var order in new[] { new[] { 0, 1, 2 }, new[] { 2, 0, 1 }, new[] { 1, 2, 0 } })
        {
            var mesh = Make(vertices, order.Select(i => faces[i])).WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth));
            Assert.False(mesh.Topology.TryGetEdge(0, 1, out var edge) && edge.IsManifold);
            var byFace = new Vector3[3];
            for (var f = 0; f < 3; f++) byFace[order[f]] = CornerOf(mesh, f, 0);
            normals.Add(byFace);
        }
        for (var i = 0; i < 3; i++)
        {
            SameBits(normals[0][i], normals[1][i]);
            SameBits(normals[0][i], normals[2][i]);
        }
        SameBits(Make(vertices, faces).FaceNormal(0), normals[0][0]);
    }

    [Fact]
    public void An_open_edge_is_a_boundary_and_marking_it_hard_changes_nothing()
    {
        var open = Hinge(hard: false);
        var marked = open.WithShade(open.Shade!.WithEdges([(0, 3), (4, 5)], true));
        var a = Shading.Corners(open);
        var b = Shading.Corners(marked);
        for (var f = 0; f < a.Length; f++)
            for (var k = 0; k < a[f].Length; k++) SameBits(a[f][k], b[f][k]);
    }

    [Fact]
    public void MeshShade_orders_and_dedupes_its_pairs_and_removes_them()
    {
        var shade = MeshShade.Flat.WithEdges([(3, 1), (1, 3), (0, 2)], true);
        Assert.Equal([(0, 2), (1, 3)], shade.Hard);
        Assert.True(shade.IsHard(3, 1));
        Assert.Equal([(0, 2)], shade.WithEdges([(3, 1)], false).Hard);
        Assert.True(MeshShade.Flat.IsDefault);
    }

    [Fact]
    public void Shade_follows_a_move_and_an_extrude_and_drops_marks_on_edges_that_are_gone()
    {
        var mesh = Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1), (4, 5)]));
        var moved = MeshEdits.Move(mesh, [0], null, null, new Vector3(0, 0, -0.5f));
        Assert.Equal(ShadingMode.Smooth, moved.Mesh!.ShadingMode);
        Assert.Equal([(0, 1), (4, 5)], moved.Mesh.HardEdges);
        var extruded = MeshEdits.Extrude(mesh, [1], 0.5f);
        Assert.True(extruded.Succeeded, string.Join("; ", extruded.Errors));
        Assert.Equal(ShadingMode.Smooth, extruded.Mesh!.ShadingMode);
        Assert.All(extruded.Mesh.HardEdges, e => Assert.True(extruded.Mesh.Topology.TryGetEdge(e.Lo, e.Hi, out _)));
    }

    [Fact]
    public void A_model_of_a_smooth_and_a_flat_mesh_combines_each_as_it_is()
    {
        var smooth = Cube().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth));
        var model = Model.Of("a", smooth).With("b", Cube());
        var combined = model.Combined!;
        var corners = Shading.Corners(combined);
        for (var f = 0; f < 6; f++) Near(Shading.Corners(smooth)[f][0], corners[f][0]);
        for (var f = 6; f < 12; f++) SameBits(Cube().FaceNormal(f - 6), corners[f][0]);
    }
}
