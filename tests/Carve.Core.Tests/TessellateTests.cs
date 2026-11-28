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

    [Fact]
    public void An_unselected_faces_hole_on_the_border_takes_the_border_points()
    {
        // The dais's left peg (walls 17..20, cap 21) stands in a hole of the bottom face (2), which is not selected.
        var mesh = Dais();
        var r = Tessellation.Tessellate(mesh, 1, [17, 18, 19, 20, 21]);
        var m = Ok(r);
        Assert.True(MeshReport.Create(m).Closed);
        Assert.DoesNotContain(r.Split, s => s.Face == 2);
        Assert.Equal(mesh.Faces[2].Loop, m.Faces[2].Loop);
        // Each 3-long edge of the peg's bottom ring is cut in three: the hole gains two points an edge, all in y = 0.
        var n = mesh.Vertices.Count;
        var hole = m.Faces[2].Holes.Single(h => h.Any(v => v >= n));
        Assert.Equal(4 + 8, hole.Count);
        Assert.All(hole.Where(v => v >= n), v => Assert.Equal(0, m.Vertices[v].Y));
        Assert.Equal(mesh.Faces[2].Holes.Single(h => h.All(hole.Contains)), hole.Where(v => v < n));
    }

    [Fact]
    public void Across_a_seam_the_unselected_neighbour_takes_its_border_uvs_from_its_own_corners()
    {
        // The top (3) and the -z side (0, loop 0 3 2 1) share edge 3-2, a seam: each face has its own UVs there.
        var mesh = Cube().WithUvs(MeshUvs.None
            .WithFace(3, [new(0, 0), new(0, 1), new(1, 1), new(1, 0)])
            .WithFace(0, [new(10, 0), new(10, 1), new(11, 1), new(11, 0)])
            .WithSeams([(2, 3)], true));
        var m = Ok(Tessellation.Tessellate(mesh, 0.25, [3]));
        var loop = m.Faces[0].Loop;
        var uv = m.Uvs!.Of(0)!;
        Assert.Equal(7, loop.Count);
        for (var i = 0; i < loop.Count; i++)
        {
            if (loop[i] < 8) continue;
            // On edge 3 (0, 1, 0) to 2 (1, 1, 0), whose UVs on face 0 are (10, 1) and (11, 1).
            var p = m.Vertices[loop[i]];
            Assert.True(Vector2.Distance(uv[i], new Vector2(10 + p.X, 1)) < 1e-4, $"corner {i}: {uv[i]} at {p}");
        }
    }

    [Fact]
    public void A_call_predicted_over_the_budget_is_refused_at_once()
    {
        // RV-100: at 1e-14 on a quad far from the origin the old loops never ended; the prediction refuses it before any work.
        var mesh = Make([new(1000, 1000, 0), new(1001, 1000, 0), new(1001, 1001, 0), new(1000, 1001, 0)], [[0, 1, 2, 3]]);
        var r = Tessellation.Tessellate(mesh, 1e-14);
        Assert.False(r.Succeeded);
        var error = r.Errors.Single();
        Assert.Equal("max_edge", error.Element);
        Assert.Contains("over the budget of 200,000", error.ToString());
        Assert.Matches("about [0-9,]+ new vertices", error.ToString());
        // Just under: about 184,752 inside, 1,596 on the edges and 5,600 in the strip by the loops of a unit quad at 0.0025
        // (191,085 made, RV-101); over at 0.002.
        Assert.InRange(Tessellation.Predicted(Square(1, 1), [0], 0.0025), 191_085, 193_000);
        Assert.True(Tessellation.Predicted(Square(1, 1), [0], 0.002) > Tessellation.Budget);
    }

    [Fact]
    public void The_40_by_0_01_strip_that_would_fit_is_refused_at_once_by_the_margin()
    {
        // RV-101, the filter's answer of 2026-11-27: the 3.5 strip margin stands though this strip makes only 196,325;
        // the prediction is the gate, so it is refused before any work.
        var mesh = Square(40, 0.01f);
        Assert.InRange(Tessellation.Predicted(mesh, [0], 0.0025), 217_000, 219_000);
        var r = Tessellation.Tessellate(mesh, 0.0025);
        Assert.False(r.Succeeded);
        var error = r.Errors.Single();
        Assert.Equal("max_edge", error.Element);
        Assert.Matches("about 21[78],[0-9]{3} new vertices, over the budget of 200,000; give a larger max_edge", error.ToString());
        Assert.Equal(4, mesh.Vertices.Count);
        // A hard stop of 0 would refuse at the first check; the prediction's message instead shows the gate came first.
        var early = Tessellation.Tessellate(Square(40, 0.01f), 0.0025, null, 0);
        Assert.Matches("about 21[78],[0-9]{3} new vertices", early.Errors.Single().ToString());
        Assert.DoesNotContain("more than 0", early.Errors.Single().ToString());
    }

    [Theory]
    // RV-101's table scaled down to run in well under a second, each the same width in max_edge: a quad 100 max_edge
    // across (the unit quad at 0.0025 is 400), and strips 4, 1.6 and 4 max_edge wide (40 x 0.01, 125 x 0.004, 75 x 0.01).
    [InlineData(0.25f, 0.25f)]
    [InlineData(2f, 0.01f)]
    [InlineData(2f, 0.004f)]
    [InlineData(3f, 0.01f)]
    public void The_prediction_is_not_under_the_vertices_made(float w, float h)
    {
        var mesh = Square(w, h);
        var r = Tessellation.Tessellate(mesh, 0.0025);
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        var made = r.Mesh!.Vertices.Count - mesh.Vertices.Count;
        Assert.True(Tessellation.Predicted(mesh, [0], 0.0025) >= made, $"predicted {Tessellation.Predicted(mesh, [0], 0.0025):N0}, made {made:N0}");
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.0125)]
    public void A_call_that_makes_more_than_the_hard_stop_is_refused_and_changes_nothing(double maxEdge)
    {
        // A call the prediction lets through but that makes more than the budget: the hard stop set one under the made count.
        var mesh = Square(1, 1);
        var made = Tessellation.Tessellate(mesh, maxEdge).Mesh!.Vertices.Count - mesh.Vertices.Count;
        foreach (var stop in new[] { made - 1, made / 2, 10 })
        {
            var r = Tessellation.Tessellate(mesh, maxEdge, null, stop);
            Assert.False(r.Succeeded);
            var error = r.Errors.Single();
            Assert.Equal("max_edge", error.Element);
            Assert.Contains(MeshEdits.Invariant($"more than {stop:N0} new vertices, over the budget of {stop:N0}"), error.ToString());
            Assert.Equal(4, mesh.Vertices.Count);
        }
        Assert.True(Tessellation.Tessellate(mesh, maxEdge, null, made).Succeeded);
    }

    [Fact]
    public void A_thin_strip_turned_off_the_axes_is_refused_plainly()
    {
        // RV-101: 20 x 0.002 turned 10 degrees at max_edge 0.002 is refused by the triangulation (a bug, B-15); the message is plain.
        var (c, s) = (MathF.Cos(MathF.PI / 18), MathF.Sin(MathF.PI / 18));
        System.Numerics.Vector3 At(float x, float y) => new(x * c - y * s, x * s + y * c, 0);
        var mesh = Make([At(0, 0), At(20, 0), At(20, 0.002f), At(0, 0.002f)], [[0, 1, 2, 3]]);
        var r = Tessellation.Tessellate(mesh, 0.002);
        Assert.False(r.Succeeded);
        var error = r.Errors.Single();
        Assert.Equal("faces[0]", error.Element);
        Assert.Contains("is too thin for max_edge 0.002", error.ToString());
        Assert.DoesNotContain("linked", error.ToString());
    }

    /// <summary>The lattice filter before RV-100: <see cref="PlaneGeometry.Inside"/> and every segment, for every point.</summary>
    private static List<PlaneGeometry.Vector2D> OldLattice(PlaneGeometry.Vector2D[][] loops, double maxEdge)
    {
        var outer = loops[0];
        var (minX, minY, maxX, maxY) = (outer.Min(p => p.X), outer.Min(p => p.Y), outer.Max(p => p.X), outer.Max(p => p.Y));
        var points = loops.SelectMany(l => l).ToArray();
        var ids = new IReadOnlyList<int>[loops.Length];
        for (int i = 0, first = 0; i < loops.Length; first += loops[i].Length, i++) ids[i] = Enumerable.Range(first, loops[i].Length).ToArray();
        static Vector3D Flat(PlaneGeometry.Vector2D p) => new(p.X, p.Y, 0);
        var segments = loops.SelectMany(l => l.Select((v, i) => (A: Flat(v), B: Flat(l[(i + 1) % l.Length])))).ToArray();
        var kept = new List<PlaneGeometry.Vector2D>();
        var row = maxEdge * Math.Sqrt(3) / 2;
        for (var j = 0; minY + j * row < maxY; j++)
            for (var x = minX + (j % 2) * maxEdge / 2; x < maxX; x += maxEdge)
            {
                var p = new PlaneGeometry.Vector2D(x, minY + j * row);
                if (!PlaneGeometry.Inside(ids, v => points[v], p) || segments.Any(s => PlaneGeometry.SegmentDistance(Flat(p), s.A, s.B) < maxEdge / 2)) continue;
                kept.Add(p);
            }
        return kept;
    }

    [Theory]
    [InlineData("holed")]
    [InlineData("two holes")]
    [InlineData("comb")]
    [InlineData("slanted")]
    public void The_lattice_filter_keeps_the_same_points_as_the_rule_before_RV_100(string name)
    {
        static PlaneGeometry.Vector2D[] P(params double[] xy) => [.. xy.Chunk(2).Select(c => new PlaneGeometry.Vector2D(c[0], c[1]))];
        PlaneGeometry.Vector2D[][] loops = name switch
        {
            "holed" => [P(0, 0, 4, 0, 4, 4, 0, 4), P(1, 1, 1, 3, 3, 3, 3, 1)],
            "two holes" => [P(0, 0, 6, 0, 6, 3, 0, 3), P(0.5, 0.5, 0.5, 2.5, 2.5, 2.5, 2.5, 0.5), P(3.2, 1, 4, 2.6, 5.5, 0.4)],
            // Concave: three teeth and two notches, one tooth leaning.
            "comb" => [P(0, 0, 5, 0, 5, 3, 4, 3, 3.6, 1, 3, 1, 3, 3, 2, 3, 2, 1.2, 1, 1.2, 1.3, 3, 0, 3)],
            _ => [P(0.1, 0.3, 3.7, -0.4, 4.9, 2.2, 2.1, 1.6, 0.6, 3.3)],
        };
        foreach (var maxEdge in new[] { 0.5, 0.3, 0.17, 0.1, 0.037 })
        {
            var expected = OldLattice(loops, maxEdge);
            Assert.NotEmpty(expected);
            Assert.Equal(expected, Tessellation.Lattice(loops, maxEdge));
        }
    }
}
