using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>Catmull-Clark subdivision (T-43): counts, D-1's id rules, the limit surface, creases, boundaries, carried attributes.</summary>
[Trait("Tier", "unit")]
public class SubdivideTests
{
    private static PolygonMesh Sub(PolygonMesh mesh, int levels) => Ok(MeshEdits.Subdivide(mesh, levels));

    [Theory]
    [InlineData(1, 26, 24, 48)]
    [InlineData(2, 98, 96, 192)]
    [InlineData(3, 386, 384, 768)]
    public void A_cube_gives_the_textbook_counts_closed_and_all_quads(int levels, int vertices, int faces, int edges)
    {
        var result = MeshEdits.Subdivide(Cube(), levels);
        var mesh = Ok(result);

        Assert.Equal(vertices, mesh.Vertices.Count);
        Assert.Equal(faces, mesh.Faces.Count);
        Assert.Equal(edges, mesh.Topology.Edges.Count);
        Assert.True(MeshReport.Create(mesh).Closed);
        Assert.All(mesh.Faces, f => Assert.Equal(4, f.Loop.Count));
        Assert.Equal(Enumerable.Range(8, vertices - 8), result.NewVertices);
        Assert.Equal(Enumerable.Range(6, faces - 6), result.NewFaces);
        Assert.Equal(Enumerable.Range(0, 8), result.MovedVertices);
    }

    [Fact]
    public void Old_ids_stay_a_face_keeps_its_id_as_its_first_child_and_new_elements_are_appended_in_order()
    {
        var cube = Cube();
        var mesh = Sub(cube, 1);
        var edges = cube.Topology.Edges;

        // Vertex 0 keeps its id at its vertex point: (F + 2R + 0 P) / 3 = 2/9 on each axis for the unit cube's corner.
        Assert.Equal(2f / 9, mesh.Vertices[0].X, 6);
        Assert.Equal(mesh.Vertices[0].X, mesh.Vertices[0].Y);
        Assert.Equal(mesh.Vertices[0].X, mesh.Vertices[0].Z);

        // Face points are ids 8..13, one per face in face order, at the face's centre.
        for (var f = 0; f < 6; f++) Assert.Equal(cube.FaceCentre(f), mesh.Vertices[8 + f]);

        // Edge points are ids 14.., one per edge in the edge table's order.
        for (var e = 0; e < edges.Count; e++)
        {
            var id = 14 + e;
            var pair = (edges[e].Lo, edges[e].Hi);
            Assert.True(mesh.Topology.TryGetEdge(pair.Lo, id, out _), $"edge point {id} of {pair}");
            Assert.True(mesh.Topology.TryGetEdge(pair.Hi, id, out _), $"edge point {id} of {pair}");
        }

        int EdgePoint(int a, int b) => 14 + edges.ToList().FindIndex(x => x.Lo == Math.Min(a, b) && x.Hi == Math.Max(a, b));
        for (var f = 0; f < 6; f++)
        {
            var loop = cube.Faces[f].Loop;
            for (var k = 0; k < 4; k++)
            {
                // Child 0 is the face itself; children 1..3 follow face by face after the six old ids.
                var id = k == 0 ? f : 6 + f * 3 + (k - 1);
                Assert.Equal([loop[k], EdgePoint(loop[k], loop[(k + 1) % 4]), 8 + f, EdgePoint(loop[(k + 3) % 4], loop[k])], mesh.Faces[id].Loop);
            }
        }

        // The children face the way their face did.
        for (var f = 0; f < 6; f++)
            Assert.True(Vector3.Dot(mesh.FaceNormal(f), cube.FaceNormal(f)) > 0.5f);
    }

    /// <summary>The limit position of a vertex of an all-quad mesh, (n²P + 4 Σ edge neighbours + Σ diagonals) / (n (n + 5)).</summary>
    private static Vector3 Limit(PolygonMesh mesh, int v)
    {
        var faces = mesh.Topology.VertexFaces(v);
        var n = faces.Count;
        var sum = (double)n * n * new Vector3D(mesh.Vertices[v]);
        foreach (var f in faces)
        {
            var loop = mesh.Faces[f].Loop;
            var k = loop.ToList().IndexOf(v);
            sum += 4 * new Vector3D(mesh.Vertices[loop[(k + 1) % 4]]) + new Vector3D(mesh.Vertices[loop[(k + 2) % 4]]);
        }
        return (sum / (n * (n + 5))).ToVector3();
    }

    private readonly record struct Vector3D(double X, double Y, double Z)
    {
        public Vector3D(Vector3 v) : this(v.X, v.Y, v.Z) { }
        public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3D operator *(double s, Vector3D a) => new(s * a.X, s * a.Y, s * a.Z);
        public static Vector3D operator /(Vector3D a, double s) => new(a.X / s, a.Y / s, a.Z / s);
        public Vector3 ToVector3() => new((float)X, (float)Y, (float)Z);
    }

    [Theory]
    [InlineData(0)]   // a corner, valence 3
    [InlineData(8)]   // a face point, valence 4
    [InlineData(14)]  // an edge point, valence 4
    public void Each_vertex_converges_on_one_limit_point_whatever_the_level(int v)
    {
        var once = Sub(Cube(), 1);
        var thrice = Sub(once, 3);

        var limit = Limit(once, v);
        Assert.True(Vector3.Distance(limit, Limit(thrice, v)) < 1e-5f, $"{limit} against {Limit(thrice, v)}");
        Assert.True(Vector3.Distance(thrice.Vertices[v], limit) < Vector3.Distance(once.Vertices[v], limit) / 8);
        // The cube's corner limit, by symmetry on the diagonal; its smooth surface lies well inside the cube.
        if (v == 0) Assert.InRange(limit.X, 0.2f, 0.3f);
    }

    [Fact]
    public void A_cube_with_every_edge_hard_keeps_its_shape_and_its_creases()
    {
        var cube = Cube();
        var hard = cube.Topology.Edges.Select(e => (e.Lo, e.Hi)).ToArray();
        var mesh = Sub(cube.WithShade(MeshShade.Of(ShadingMode.Smooth, hard)), 2);

        for (var v = 0; v < 8; v++) Assert.Equal(cube.Vertices[v], mesh.Vertices[v]);
        Assert.All(mesh.Vertices, p => Assert.True(
            p.X is 0 or 1 || p.Y is 0 or 1 || p.Z is 0 or 1, $"{p} is off the cube"));
        Assert.Equal(ShadingMode.Smooth, mesh.ShadingMode);
        // Each of the 12 edges is cut in two, then in two again: 48 hard pieces, all of them real edges.
        Assert.Equal(48, mesh.HardEdges.Count);
        Assert.All(mesh.HardEdges, e => Assert.True(mesh.Topology.TryGetEdge(e.Lo, e.Hi, out _)));
    }

    [Fact]
    public void A_hard_edge_is_carried_to_both_halves_and_stays_straight()
    {
        var cube = Cube();
        var mesh = Sub(cube.WithShade(MeshShade.Of(ShadingMode.Flat, [(0, 1)])), 1);
        var e = 14 + cube.Topology.Edges.ToList().FindIndex(x => (x.Lo, x.Hi) == (0, 1));

        Assert.Equal([(0, e), (1, e)], mesh.HardEdges);
        Assert.Equal(new Vector3(0.5f, 0, 0), mesh.Vertices[e]);
    }

    [Fact]
    public void A_vertex_on_two_creases_follows_them_and_one_on_three_is_a_corner()
    {
        var cube = Cube();
        // Vertex 0 has edges to 1, 3 and 4; vertex 6 has edges to 2, 5 and 7.
        var shade = MeshShade.Of(ShadingMode.Smooth, [(0, 1), (0, 3), (6, 2), (6, 5), (6, 7)]);
        var mesh = Sub(cube.WithShade(shade), 1);

        var p = cube.Vertices;
        var crease = (p[1] + p[3] + 6 * p[0]) / 8;
        Assert.Equal(crease.X, mesh.Vertices[0].X, 6);
        Assert.Equal(crease.Y, mesh.Vertices[0].Y, 6);
        Assert.Equal(crease.Z, mesh.Vertices[0].Z, 6);
        Assert.Equal(p[6], mesh.Vertices[6]);
    }

    [Fact]
    public void An_open_box_keeps_its_rim_in_its_plane_by_the_boundary_rule()
    {
        // The cube without its +z lid: the rim is the loop 4-5-6-7.
        var box = Make(CubeVertices, CubeFaces.Where((_, f) => f != 1));
        var mesh = Sub(box, 2);

        foreach (var edge in mesh.Topology.Edges.Where(e => e.Faces.Count == 1))
        {
            Assert.Equal(1f, mesh.Vertices[edge.Lo].Z);
            Assert.Equal(1f, mesh.Vertices[edge.Hi].Z);
        }
        var once = Sub(box, 1);
        var p = box.Vertices;
        Assert.Equal((p[5] + p[7] + 6 * p[4]) / 8, once.Vertices[4]);
        Assert.Equal(16, mesh.Topology.Edges.Count(e => e.Faces.Count == 1));
        Assert.Empty(mesh.HardEdges);
    }

    [Fact]
    public void Children_take_their_face_colour_and_groups()
    {
        var style = FaceStyle.None.WithColour([1], "#C8503C").WithGroup("lid", [1]).WithGroup("sides", [2, 3]);
        var result = MeshEdits.Subdivide(Cube().WithStyle(style), 2);
        var mesh = Ok(result);

        var lid = mesh.Style!.FacesOf("lid")!;
        Assert.Equal(16, lid.Count);
        Assert.Contains(1, lid);
        Assert.All(lid, f => Assert.Equal("#C8503C", mesh.Style.ColourOf(f)));
        Assert.Equal(16, mesh.Style.Colours.Count);
        Assert.Equal(32, mesh.Style.FacesOf("sides")!.Count);
    }

    [Fact]
    public void Weights_blend_by_the_same_stencil_sum_to_1_and_keep_at_most_four_bones()
    {
        // Four corners of the -z face on bones 0..3, the +z corners on bone 4: an edge point beside -z sees five bones.
        var weights = VertexWeights.Of(Enumerable.Range(0, 8).Select(v =>
            KeyValuePair.Create(v, new[] { new Influence(v < 4 ? v : 4, 1) })));
        var cube = Cube().WithWeights(weights);
        var once = Sub(cube, 1);
        var mesh = Sub(cube, 3);

        Assert.Equal(mesh.Vertices.Count, mesh.Weights!.Count);
        foreach (var v in mesh.Weights.Vertices)
        {
            var influences = mesh.Weights.Of(v);
            Assert.InRange(influences.Count, 1, VertexWeights.MaxInfluences);
            Assert.Equal(1, influences.Sum(i => i.Weight), 12);
            Assert.All(influences, i => Assert.True(i.Weight > 0));
        }

        // The -z face point blends its four corners equally.
        Assert.Equal([new Influence(0, 0.25), new Influence(1, 0.25), new Influence(2, 0.25), new Influence(3, 0.25)], once.Weights!.Of(8));
        // A +z face point has bone 4 alone.
        Assert.Equal([new Influence(4, 1)], once.Weights.Of(9));
        // The edge point of 0-4 (between -y and -x) blends 0 and 4 and both faces' points: bones 0, 1, 3 and 4, none dropped.
        Assert.Equal([0, 1, 3, 4], once.Weights.Of(14 + cube.Topology.Edges.ToList().FindIndex(e => (e.Lo, e.Hi) == (0, 4))).Select(i => i.Bone));
    }

    [Fact]
    public void An_unweighted_mesh_stays_unweighted()
    {
        Assert.Null(Sub(Cube(), 2).Weights);
    }

    [Fact]
    public void Two_levels_are_one_level_twice_and_every_run_is_the_same_to_the_bit()
    {
        var weights = VertexWeights.Of(Enumerable.Range(0, 8).Select(v => KeyValuePair.Create(v, new[] { new Influence(v % 3, 1) })));
        var cube = Cube().WithWeights(weights).WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1), (1, 2)]))
            .WithStyle(FaceStyle.None.WithColour([0], "#3C78C8"));

        var twice = Sub(Sub(cube, 1), 1);
        foreach (var other in new[] { Sub(cube, 2), Sub(cube, 2) })
        {
            Assert.Equal(twice.Vertices, other.Vertices);
            Assert.Equal(twice.Faces.Select(f => f.Loop.ToArray()), other.Faces.Select(f => f.Loop.ToArray()));
            Assert.Equal(twice.HardEdges, other.HardEdges);
            Assert.Equal(twice.Style!.Colours, other.Style!.Colours);
            foreach (var v in twice.Weights!.Vertices) Assert.Equal(twice.Weights.Of(v), other.Weights!.Of(v));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Levels_outside_1_to_3_are_refused_by_name(int levels)
    {
        var result = MeshEdits.Subdivide(Cube(), levels);
        Assert.False(result.Succeeded);
        Assert.Equal("levels", Assert.Single(result.Errors).Element);
    }

    [Fact]
    public void An_n_gon_becomes_n_quads_round_its_centre()
    {
        // A flat hexagon: six quads, the face point at its centre, the rim by the boundary rule.
        var hexagon = Make(Enumerable.Range(0, 6).Select(k => new Vector3(MathF.Cos(k * MathF.PI / 3), MathF.Sin(k * MathF.PI / 3), 0)),
            [[0, 1, 2, 3, 4, 5]]);
        var mesh = Sub(hexagon, 1);

        Assert.Equal(6, mesh.Faces.Count);
        Assert.All(mesh.Faces, f => Assert.Contains(6, f.Loop));
        Assert.True(Vector3.Distance(Vector3.Zero, mesh.Vertices[6]) < 1e-6f);
        Assert.All(mesh.Vertices, p => Assert.Equal(0f, p.Z));
    }

    [Fact]
    public void Past_four_bones_the_smallest_are_dropped_and_the_rest_renormalised()
    {
        // Each corner on a bone of its own: vertex 0's point blends seven, 5/12 on bone 0, 1/6 on 1, 3 and 4, 1/36 on 2, 5 and 7.
        var weights = VertexWeights.Of(Enumerable.Range(0, 8).Select(v => KeyValuePair.Create(v, new[] { new Influence(v, 1) })));
        var influences = Sub(Cube().WithWeights(weights), 1).Weights!.Of(0);

        Assert.Equal(new[] { 0, 1, 3, 4 }, influences.Select(i => i.Bone).Order());
        Assert.Equal(5.0 / 11, influences.Single(i => i.Bone == 0).Weight, 6);
        Assert.All(influences.Where(i => i.Bone != 0), i => Assert.Equal(2.0 / 11, i.Weight, 6));
    }

    [Fact]
    public void An_old_vertex_with_no_weights_stays_unweighted_and_a_new_point_blends_only_its_weighted_sources()
    {
        // Vertices 0 to 3 (the -z face) on bone 0, 4 to 7 unweighted (the person, 2026-11-10).
        var weights = VertexWeights.Of(Enumerable.Range(0, 4).Select(v => KeyValuePair.Create(v, new[] { new Influence(0, 1) })));
        var once = Sub(Cube().WithWeights(weights), 1);

        for (var v = 4; v < 8; v++) Assert.False(once.Weights!.Has(v), $"vertex {v} was given weights");
        for (var v = 0; v < 4; v++) Assert.Equal([new Influence(0, 1)], once.Weights!.Of(v));
        // The +z face point (9) has only unweighted sources; the -y face point (10) blends its two weighted corners alone.
        Assert.False(once.Weights!.Has(9));
        Assert.Equal([new Influence(0, 1)], once.Weights.Of(10));
    }

    [Fact]
    public void A_vertex_on_one_crease_is_a_dart_and_takes_the_smooth_rule()
    {
        var smooth = Sub(Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [])), 1);
        var dart = Sub(Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1)])), 1);

        Assert.Equal(smooth.Vertices[0], dart.Vertices[0]);
        Assert.NotEqual(Cube().Vertices[0], dart.Vertices[0]);
    }

    [Fact]
    public void A_vertex_on_no_face_stays_where_it_is()
    {
        // Weighted, so a stencil worked from its zero edges would lose its weight as well as its place.
        var weights = VertexWeights.Of(Enumerable.Range(0, 9).Select(v => KeyValuePair.Create(v, new[] { new Influence(0, 1) })));
        var mesh = Sub(Make([.. CubeVertices, new(5, 6, 7)], CubeFaces).WithWeights(weights), 1);

        Assert.Equal(new Vector3(5, 6, 7), mesh.Vertices[8]);
        Assert.Equal([new Influence(0, 1)], mesh.Weights!.Of(8));
        Assert.All(mesh.Vertices, p => Assert.False(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z), $"{p} is not a number"));
    }

    [Fact]
    public void A_triangle_becomes_three_quads_round_its_centre()
    {
        // A closed tetrahedron: 4 vertices, 4 triangles, 6 edges.
        var tetra = Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)], [[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]]);
        var mesh = Sub(tetra, 1);

        Assert.Equal(4 + 4 + 6, mesh.Vertices.Count);
        Assert.Equal(12, mesh.Faces.Count);
        Assert.All(mesh.Faces, f => Assert.Equal(4, f.Loop.Count));
        Assert.All(mesh.Topology.Edges, e => Assert.Equal(2, e.Faces.Count));
        var centre = (tetra.Vertices[0] + tetra.Vertices[1] + tetra.Vertices[2]) / 3;
        Assert.True(Vector3.Distance(centre, mesh.Vertices[4]) < 1e-6f, $"{mesh.Vertices[4]} against {centre}");
        Assert.Contains(4, mesh.Faces[0].Loop);
        Assert.Equal(3, mesh.Faces.Count(f => f.Loop.Contains(4)));
    }
}
