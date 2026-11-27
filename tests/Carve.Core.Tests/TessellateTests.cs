using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>T-237: <see cref="Tessellation.Tessellate"/>.</summary>
[Trait("Tier", "unit")]
public class TessellateTests
{
    /// <summary>The faces the edit made or reshaped from <paramref name="source"/>: the pieces of each split face, and each selected face.</summary>
    private static IEnumerable<int> Pieces(EditResult r, IEnumerable<int> selected) =>
        selected.SelectMany(f => r.Split.FirstOrDefault(s => s.Face == f)?.Pieces ?? [f]);

    private static double Area(PolygonMesh m, int f) => m.FaceArea(f);

    /// <summary>Every piece a triangle on <paramref name="plane"/>'s plane (normal, offset), none with an edge over <paramref name="maxEdge"/>,
    /// none with an angle under 10 degrees, and none facing away from <paramref name="plane"/>'s normal.</summary>
    private static void AllFine(PolygonMesh m, IEnumerable<int> pieces, double maxEdge, Vector3 normal, float offset)
    {
        foreach (var f in pieces)
        {
            var loop = m.Faces[f].Loop;
            Assert.Equal(3, loop.Count);
            Assert.Empty(m.Faces[f].Holes);
            var p = loop.Select(v => m.Vertices[v]).ToArray();
            foreach (var v in p) Assert.True(Math.Abs(Vector3.Dot(v, normal) - offset) < 1e-4, $"faces[{f}] is off the plane");
            for (var i = 0; i < 3; i++)
            {
                var (a, b, c) = (p[i], p[(i + 1) % 3], p[(i + 2) % 3]);
                Assert.True((b - a).Length() <= maxEdge * (1 + 1e-4), $"faces[{f}] has an edge of {(b - a).Length()}");
                var angle = Math.Acos(Vector3.Dot(Vector3.Normalize(b - a), Vector3.Normalize(c - a))) * 180 / Math.PI;
                Assert.True(angle >= 10, $"faces[{f}] has an angle of {angle:F2} degrees");
            }
            Assert.True(Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), normal) > 0, $"faces[{f}] faces the wrong way");
        }
    }

    private static PolygonMesh Square(float w, float h) => Make([new(0, 0, 0), new(w, 0, 0), new(w, h, 0), new(0, h, 0)], [[0, 1, 2, 3]]);

    [Fact]
    public void A_planar_quad_becomes_flat_triangles_no_longer_than_max_edge()
    {
        var mesh = Square(1, 1);
        var r = Tessellation.Tessellate(mesh, 0.3);
        var m = Ok(r);
        var pieces = Pieces(r, [0]).ToList();
        AllFine(m, pieces, 0.3, Vector3.UnitZ, 0);
        Assert.Equal(1, pieces.Sum(f => Area(m, f)), 4);
        for (var v = 0; v < 4; v++) Assert.Equal(mesh.Vertices[v], m.Vertices[v]);
        Assert.Empty(r.MovedVertices);
        Assert.Equal(Enumerable.Range(4, m.Vertices.Count - 4), r.NewVertices);
        Assert.Equal(pieces.Count - 1, r.NewFaces.Count);
        Assert.Equal(0, r.Split.Single().Face);
        Assert.Equal(pieces, r.Split.Single().Pieces);
        // The face keeps its id on the triangle at its first corner.
        Assert.Contains(0, m.Faces[0].Loop);
    }

    [Fact]
    public void A_long_thin_strip_is_split_along_its_length_only()
    {
        var r = Tessellation.Tessellate(Square(127, 2), 5);
        var m = Ok(r);
        // 127 / 5 rounds up to 26 pieces each long edge; the 2-long ends are not cut. Inside, only the midpoints of
        // diagonals over 5 long are added, on the strip's middle line.
        Assert.Equal(2 * 26 + 2, m.Vertices.Count(v => v.Y is 0 or 2));
        Assert.Equal(2, m.Vertices.Count(v => v.X == 0));
        Assert.Equal(2, m.Vertices.Count(v => v.X == 127));
        Assert.All(m.Vertices, v => Assert.True(v.Y is 0 or 1 or 2));
        AllFine(m, Pieces(r, [0]), 5, Vector3.UnitZ, 0);
    }

    [Fact]
    public void A_hole_stays_a_hole()
    {
        Vector3[] verts = [new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(0, 4, 0), new(1, 1, 0), new(1, 3, 0), new(3, 3, 0), new(3, 1, 0)];
        var mesh = PolygonMesh.Create(verts, [new Face([0, 1, 2, 3], [[4, 5, 6, 7]])]).Mesh!;
        var r = Tessellation.Tessellate(mesh, 0.5);
        var m = Ok(r);
        var pieces = Pieces(r, [0]).ToList();
        AllFine(m, pieces, 0.5, Vector3.UnitZ, 0);
        Assert.Equal(12, pieces.Sum(f => Area(m, f)), 4);
        foreach (var f in pieces)
        {
            var c = m.Faces[f].Loop.Aggregate(Vector3.Zero, (s, v) => s + m.Vertices[v]) / 3;
            Assert.False(c.X > 1 && c.X < 3 && c.Y > 1 && c.Y < 3, $"faces[{f}] is in the hole");
        }
    }

    [Fact]
    public void A_selection_gives_its_border_points_to_the_faces_around_it_and_changes_nothing_else()
    {
        var mesh = Cube();
        var r = Tessellation.Tessellate(mesh, 0.25, [3]);
        var m = Ok(r);
        var report = MeshReport.Create(m);
        Assert.True(report.Closed);
        Assert.Empty(report.Warnings);
        for (var v = 0; v < 8; v++) Assert.Equal(mesh.Vertices[v], m.Vertices[v]);
        Assert.Equal(3, r.Split.Single().Face);
        AllFine(m, Pieces(r, [3]), 0.25, Vector3.UnitY, 1);
        // Each side face takes three points on its top edge, in order along it; the bottom is untouched.
        foreach (var f in new[] { 0, 1, 4, 5 })
        {
            Assert.Equal(7, m.Faces[f].Loop.Count);
            Assert.Equal(mesh.Faces[f].Loop, m.Faces[f].Loop.Where(v => v < 8));
            var added = m.Faces[f].Loop.Where(v => v >= 8).Select(v => m.Vertices[v]).ToList();
            Assert.All(added, p => Assert.Equal(1, p.Y));
        }
        Assert.Equal(mesh.Faces[2], m.Faces[2]);
    }

    [Fact]
    public void Uvs_at_new_points_are_interpolated_from_the_faces_own_corners()
    {
        var mesh = Square(1, 1);
        mesh = mesh.WithUvs(MeshUvs.None.WithFace(0, [new(0, 0), new(2, 0), new(2, 2), new(0, 2)]));
        var r = Tessellation.Tessellate(mesh, 0.3);
        var m = Ok(r);
        foreach (var f in Pieces(r, [0]))
        {
            var uv = m.Uvs!.Of(f)!;
            var loop = m.Faces[f].Loop;
            for (var i = 0; i < 3; i++)
            {
                var p = m.Vertices[loop[i]];
                Assert.True(Vector2.Distance(uv[i], new Vector2(2 * p.X, 2 * p.Y)) < 1e-4, $"faces[{f}] corner {i}: {uv[i]} at {p}");
            }
        }
    }

    [Fact]
    public void Every_piece_keeps_its_faces_groups_colour_and_material_and_hard_edges_stay_hard()
    {
        var mesh = Cube();
        // A face has a colour or a material, not both: the top a colour, the front a material.
        mesh = mesh.WithStyle(FaceStyle.None.WithColour([3], "#AA3311").WithMaterial([1], "stone").WithGroup("top", [3]))
            .WithShade(MeshShade.Of(ShadingMode.Flat, [(3, 7)]));
        var r = Tessellation.Tessellate(mesh, 0.25, [1, 3]);
        var m = Ok(r);
        var pieces = Pieces(r, [3]).ToList();
        Assert.All(pieces, f => Assert.Equal("#AA3311", m.Style!.ColourOf(f)));
        Assert.All(Pieces(r, [1]), f => Assert.Equal("stone", m.Style!.MaterialOf(f)));
        Assert.Equal(pieces.Order(), m.Style!.FacesOf("top")!.Order());
        Assert.Null(m.Style.ColourOf(0));
        // Edge 3-7 is cut in four: every piece of it is hard, and nothing else is.
        Assert.Equal(4, m.HardEdges.Count);
        Assert.All(m.HardEdges, e => Assert.True(m.Vertices[e.Lo].X == 0 && m.Vertices[e.Lo].Y == 1 && m.Vertices[e.Hi].X == 0 && m.Vertices[e.Hi].Y == 1));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void Max_edge_not_above_zero_is_refused(double maxEdge)
    {
        var r = Tessellation.Tessellate(Cube(), maxEdge);
        Assert.False(r.Succeeded);
        Assert.Equal("max_edge", r.Errors.Single().Element);
    }

    [Fact]
    public void An_unknown_face_is_refused()
    {
        var r = Tessellation.Tessellate(Cube(), 0.5, [9]);
        Assert.False(r.Succeeded);
        Assert.Contains("9", r.Errors.Single().ToString());
    }

    /// <summary>M-41's Done-when, the tessellate half: the dais's top at 0.75.</summary>
    [Fact]
    public void The_dais_top_tessellated_at_0_75_stays_closed_and_changes_nothing_outside_the_group()
    {
        var mesh = Dais();
        Assert.True(MeshReport.Create(mesh).Closed);
        Assert.Empty(MeshReport.Create(mesh).Warnings);
        var wear = mesh.Style!.FacesOf(DaisGroup)!;
        var r = Tessellation.Tessellate(mesh, DaisMaxEdge, wear);
        var m = Ok(r);
        var report = MeshReport.Create(m);
        Assert.True(report.Closed);
        Assert.Empty(report.Warnings);
        Assert.Empty(MeshHealth.Create(m).Problems);
        for (var v = 0; v < mesh.Vertices.Count; v++) Assert.Equal(mesh.Vertices[v], m.Vertices[v]);
        Assert.Equal(wear.Order(), r.Split.Select(s => s.Face));
        var pieces = Pieces(r, wear).ToList();
        AllFine(m, pieces, DaisMaxEdge, Vector3.UnitY, 6);
        Assert.Equal(2 * (600 - 16), pieces.Sum(f => Area(m, f)), 2);
        Assert.Equal(pieces.Order(), m.Style!.FacesOf(DaisGroup)!.Order());
        // Every face outside the group keeps its corners in order, gaining only points on its edges, and its area.
        for (var f = 2; f < mesh.Faces.Count; f++)
        {
            Assert.Equal(mesh.Faces[f].Loop, m.Faces[f].Loop.Where(v => v < mesh.Vertices.Count));
            Assert.Equal(Area(mesh, f), Area(m, f), 3);
        }
        // Every edge of every piece of every split edge is hard, as the dais's edges are.
        Assert.All(m.Topology.Edges.Where(e => e.Faces.Select(u => u.Face).Distinct().Count(f => !pieces.Contains(f)) > 0),
            e => Assert.Contains((e.Lo, e.Hi), m.HardEdges));
    }
}
