using System.Numerics;
using Xunit;
using static Carve.Core.ElementKind;

namespace Carve.Core.Tests;

/// <summary>The where-language: its grammar, every property and flag of every kind, its float rule and its errors.</summary>
[Trait("Tier", "unit")]
public class MeshQueryTests
{
    private const string VertexNames = "id, x, y, z, min, max, faces, edges, unattached, boundary";
    private const string EdgeNames = "x, y, z, min, max, a, b, length, faces, open, manifold, nonmanifold";
    private const string FaceNames = "id, x, y, z, min, max, normal, vertices, area, non_planar, open";
    private const string Operators = "<, <=, ==, !=, >= or >";

    private static Selection Run(PolygonMesh mesh, ElementKind kind, string where)
    {
        var parsed = MeshQuery.Parse(where);
        Assert.Empty(parsed.ErrorsFor(kind));
        return MeshQuery.Select(mesh, kind, parsed.Expression!);
    }

    private static int[] Ids(PolygonMesh mesh, ElementKind kind, string where) => Run(mesh, kind, where).Ids.ToArray();

    private static (int Lo, int Hi)[] Pairs(PolygonMesh mesh, string where) => Run(mesh, ElementKind.Edges, where).Edges.ToArray();

    private static string[] Errors(ElementKind kind, string where) =>
        MeshQuery.Parse(where).ErrorsFor(kind).Select(e => e.ToString()).ToArray();

    /// <summary>The unit cube without its top, face 3: open round vertices 2, 3, 6, 7.</summary>
    private static PolygonMesh OpenBox() => TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces.Where((_, f) => f != 3));

    private static PolygonMesh WithStrayVertex() =>
        TestMeshes.Make(TestMeshes.CubeVertices.Append(new Vector3(5, 5, 5)), TestMeshes.CubeFaces);

    // ---- The grammar ----

    [Theory]
    [InlineData("y < 1", new[] { 0, 1, 4, 5 })]
    [InlineData("y <= 0", new[] { 0, 1, 4, 5 })]
    [InlineData("y <= 1", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData("y == 1", new[] { 2, 3, 6, 7 })]
    [InlineData("y != 1", new[] { 0, 1, 4, 5 })]
    [InlineData("y >= 1", new[] { 2, 3, 6, 7 })]
    [InlineData("y >= 0", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData("y > 0", new[] { 2, 3, 6, 7 })]
    [InlineData("y > 1", new int[0])]
    public void Every_operator_compares_as_written(string where, int[] expected) =>
        Assert.Equal(expected, Ids(TestMeshes.Cube(), Vertices, where));

    [Theory]
    [InlineData("y == 1 and x == 0", new[] { 3, 7 })]
    [InlineData("y == 1 and x == 0 and z == 0", new[] { 3 })]
    [InlineData("id == 1 or id == 6", new[] { 1, 6 })]
    [InlineData("id == 1 or id == 6 or id == 2", new[] { 1, 2, 6 })]
    [InlineData("not y == 1", new[] { 0, 1, 4, 5 })]
    [InlineData("not not y == 1", new[] { 2, 3, 6, 7 })]
    [InlineData("(y == 1)", new[] { 2, 3, 6, 7 })]
    [InlineData("((y == 1))", new[] { 2, 3, 6, 7 })]
    [InlineData("all", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData("none", new int[0])]
    [InlineData("not none", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData("not all", new int[0])]
    public void Every_production_selects_what_it_says(string where, int[] expected) =>
        Assert.Equal(expected, Ids(TestMeshes.Cube(), Vertices, where));

    [Theory]
    // a or b and c is a or (b and c); (a or b) and c would differ on every row here.
    [InlineData("all or none and none", 8)]
    [InlineData("id == 0 or y == 1 and x == 1", 3)] // 0, then 2 and 6
    [InlineData("(all or none) and none", 0)]
    // not a and b is (not a) and b; not (a and b) would differ.
    [InlineData("not none and none", 0)]
    [InlineData("not (none and none)", 8)]
    [InlineData("not y == 1 and x == 0", 2)] // 0 and 4
    [InlineData("not all or all", 8)]
    [InlineData("not (all or all)", 0)]
    public void Not_binds_tighter_than_and_which_binds_tighter_than_or(string where, int count) =>
        Assert.Equal(count, Run(TestMeshes.Cube(), Vertices, where).Count);

    [Theory]
    [InlineData("y == 1")]
    [InlineData("y == +1")]
    [InlineData("y == 1.")]
    [InlineData("y == 1.0")]
    [InlineData("y == 1e0")]
    [InlineData("y == 1E0")]
    [InlineData("y == 1.0e+0")]
    [InlineData("y == 10e-1")]
    [InlineData("y == .1e1")]
    [InlineData("y > -.5e-1 and y > 0.5")]
    public void A_number_is_a_decimal_with_an_optional_sign_and_exponent(string where) =>
        Assert.Equal([2, 3, 6, 7], Ids(TestMeshes.Cube(), Vertices, where));

    [Theory]
    [InlineData("y==1and x<0.5")]
    [InlineData("y==1.0and(x<.5)")]
    [InlineData("  y\t==\n1   and x<0.5  ")]
    [InlineData("not(y<1)and(x<1e-1)")]
    [InlineData("y>-1and y>=1and x<=+0")]
    public void Whitespace_is_free(string where) =>
        Assert.Equal([3, 7], Ids(TestMeshes.Cube(), Vertices, where));

    [Theory]
    [InlineData(Vertices, "NOT Y < 1 AND X == 0 OR NONE", new[] { 3, 7 })]
    [InlineData(Vertices, "Min.Y == 1 And MAX.X == 0", new[] { 3, 7 })]
    [InlineData(Vertices, "ALL AND NOT UNATTACHED AND ID == 3", new[] { 3 })]
    [InlineData(Faces, "NORMAL.Y > 0.9", new[] { 3 })]
    [InlineData(Faces, "normal . Y > 0.9", new[] { 3 })]
    [InlineData(Faces, "NOT NON_PLANAR AND AREA == 1 AND VERTICES == 4 AND ID == 2", new[] { 2 })]
    public void Keywords_and_names_are_in_any_case(ElementKind kind, string where, int[] expected) =>
        Assert.Equal(expected, Ids(TestMeshes.Cube(), kind, where));

    [Fact]
    public void Nesting_goes_to_the_limit_and_a_long_chain_of_or_is_not_nesting()
    {
        var cube = TestMeshes.Cube();
        var depth = MeshQuery.MaxDepth;
        Assert.Equal(8, Run(cube, Vertices, new string('(', depth) + "all" + new string(')', depth)).Count);
        Assert.Equal(8, Run(cube, Vertices, string.Concat(Enumerable.Repeat("not ", depth)) + "all").Count);
        Assert.Equal(1, Run(cube, Vertices, string.Join(" or ", Enumerable.Repeat("id == 3", 100_000))).Count);
    }

    // ---- Vertices ----

    [Theory]
    [InlineData("id == 5", new[] { 5 })]
    [InlineData("id >= 6", new[] { 6, 7 })]
    [InlineData("x == 1", new[] { 1, 2, 5, 6 })]
    [InlineData("y == 0", new[] { 0, 1, 4, 5 })]
    [InlineData("z == 1", new[] { 4, 5, 6, 7 })]
    [InlineData("min.x == 1", new[] { 1, 2, 5, 6 })]
    [InlineData("min.y == 1", new[] { 2, 3, 6, 7 })]
    [InlineData("min.z == 1", new[] { 4, 5, 6, 7 })]
    [InlineData("max.x == 0", new[] { 0, 3, 4, 7 })]
    [InlineData("max.y == 0", new[] { 0, 1, 4, 5 })]
    [InlineData("max.z == 0", new[] { 0, 1, 2, 3 })]
    [InlineData("faces == 3", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData("edges == 3", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData("unattached", new int[0])]
    [InlineData("boundary", new int[0])]
    public void Vertices_on_the_cube(string where, int[] expected) =>
        Assert.Equal(expected, Ids(TestMeshes.Cube(), Vertices, where));

    [Fact]
    public void An_unattached_vertex_has_no_faces_and_no_edges()
    {
        var mesh = WithStrayVertex();
        Assert.Equal([8], Ids(mesh, Vertices, "unattached"));
        Assert.Equal([8], Ids(mesh, Vertices, "faces == 0"));
        Assert.Equal([8], Ids(mesh, Vertices, "edges == 0"));
        Assert.Equal(Enumerable.Range(0, 8), Ids(mesh, Vertices, "not unattached"));
        Assert.Equal(9, Run(mesh, Vertices, "none").Of);
    }

    [Fact]
    public void On_the_open_box_the_rim_vertices_are_the_boundary()
    {
        var mesh = OpenBox();
        Assert.Equal([2, 3, 6, 7], Ids(mesh, Vertices, "boundary"));
        Assert.Equal([2, 3, 6, 7], Ids(mesh, Vertices, "faces == 2"));
        Assert.Equal([0, 1, 4, 5], Ids(mesh, Vertices, "faces == 3"));
        Assert.Equal(8, Run(mesh, Vertices, "edges == 3").Count);
    }

    [Fact]
    public void A_vertex_on_a_nonmanifold_edge_alone_is_not_boundary()
    {
        // Two tetrahedra on one edge: every edge closed, the shared one by four faces.
        var mesh = TestMeshes.Make(
            [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, -1, 0), new(0, 0, -1)],
            [[0, 1, 2], [1, 0, 3], [1, 3, 2], [0, 2, 3], [0, 1, 4], [1, 0, 5], [1, 5, 4], [0, 4, 5]]);
        Assert.Equal([(0, 1)], Pairs(mesh, "nonmanifold"));
        Assert.Empty(Ids(mesh, Vertices, "boundary"));
        Assert.Equal([0, 1], Ids(mesh, Vertices, "faces == 6"));
        Assert.Equal([0, 1], Ids(mesh, Vertices, "edges == 5"));
    }

    // ---- Edges ----

    [Fact]
    public void Edges_on_the_cube()
    {
        var cube = TestMeshes.Cube();
        (int, int)[] every = [(0, 1), (0, 3), (0, 4), (1, 2), (1, 5), (2, 3), (2, 6), (3, 7), (4, 5), (4, 7), (5, 6), (6, 7)];
        Assert.Equal(every, Pairs(cube, "all"));
        Assert.Equal([(0, 1), (2, 3), (4, 5), (6, 7)], Pairs(cube, "x == 0.5"));
        Assert.Equal([(0, 3), (1, 2), (4, 7), (5, 6)], Pairs(cube, "y == 0.5"));
        Assert.Equal([(0, 4), (1, 5), (2, 6), (3, 7)], Pairs(cube, "z == 0.5"));
        Assert.Equal([(2, 6), (3, 7)], Pairs(cube, "y == 1 and z == 0.5"));
        Assert.Equal([(1, 2), (1, 5), (2, 6), (5, 6)], Pairs(cube, "min.x == 1"));
        Assert.Equal([(2, 3), (2, 6), (3, 7), (6, 7)], Pairs(cube, "min.y == 1"));
        Assert.Equal([(4, 5), (4, 7), (5, 6), (6, 7)], Pairs(cube, "min.z == 1"));
        Assert.Equal([(0, 3), (0, 4), (3, 7), (4, 7)], Pairs(cube, "max.x == 0"));
        Assert.Equal([(0, 1), (0, 4), (1, 5), (4, 5)], Pairs(cube, "max.y == 0"));
        Assert.Equal([(0, 1), (0, 3), (1, 2), (2, 3)], Pairs(cube, "max.z == 0"));
        Assert.Equal([(0, 3), (1, 2), (4, 7), (5, 6)], Pairs(cube, "min.y == 0 and max.y == 1"));
        Assert.Equal([(0, 1), (0, 3), (0, 4)], Pairs(cube, "a == 0"));
        Assert.Equal([(3, 7), (4, 7), (6, 7)], Pairs(cube, "b == 7"));
        Assert.Equal(every, Pairs(cube, "length == 1"));
        Assert.Equal(every, Pairs(cube, "faces == 2"));
        Assert.Equal(every, Pairs(cube, "manifold"));
        Assert.Empty(Pairs(cube, "open"));
        Assert.Empty(Pairs(cube, "nonmanifold"));
    }

    [Fact]
    public void On_the_open_box_the_rim_is_open_and_the_rest_manifold()
    {
        var mesh = OpenBox();
        (int, int)[] rim = [(2, 3), (2, 6), (3, 7), (6, 7)];
        Assert.Equal(rim, Pairs(mesh, "open"));
        Assert.Equal(rim, Pairs(mesh, "faces == 1"));
        Assert.Equal(rim, Pairs(mesh, "not manifold"));
        Assert.Equal(8, Run(mesh, ElementKind.Edges, "manifold").Count);
        Assert.Equal(12, Run(mesh, ElementKind.Edges, "none").Of);
    }

    [Fact]
    public void Three_faces_on_an_edge_are_nonmanifold()
    {
        var mesh = Fin();
        Assert.Equal([(0, 1)], Pairs(mesh, "nonmanifold"));
        Assert.Equal([(0, 1)], Pairs(mesh, "faces > 2"));
        Assert.Empty(Pairs(mesh, "manifold"));
        Assert.Equal(6, Run(mesh, ElementKind.Edges, "open").Count);
    }

    [Fact]
    public void Two_faces_running_the_same_way_along_an_edge_are_neither_manifold_nor_nonmanifold_nor_open()
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)], [[0, 1, 2], [0, 1, 3]]);
        Assert.Equal([(0, 1)], Pairs(mesh, "faces == 2"));
        Assert.Empty(Pairs(mesh, "manifold"));
        Assert.Empty(Pairs(mesh, "nonmanifold"));
        Assert.DoesNotContain((0, 1), Pairs(mesh, "open"));
    }

    // ---- Faces ----

    [Theory]
    [InlineData("id == 4", new[] { 4 })]
    [InlineData("x == 0.5", new[] { 0, 1, 2, 3 })]
    [InlineData("y == 1", new[] { 3 })]
    [InlineData("z == 0", new[] { 0 })]
    [InlineData("min.x == 1", new[] { 5 })]
    [InlineData("min.y == 1", new[] { 3 })]
    [InlineData("min.z == 1", new[] { 1 })]
    [InlineData("max.x == 0", new[] { 4 })]
    [InlineData("max.y == 0", new[] { 2 })]
    [InlineData("max.z == 0", new[] { 0 })]
    [InlineData("max.y == 1", new[] { 0, 1, 3, 4, 5 })]
    [InlineData("normal.x > 0.9", new[] { 5 })]
    [InlineData("normal.x < -0.9", new[] { 4 })]
    [InlineData("normal.y > 0.9", new[] { 3 })]
    [InlineData("normal.y < -0.9", new[] { 2 })]
    [InlineData("normal.z > 0.9", new[] { 1 })]
    [InlineData("normal.z == -1", new[] { 0 })]
    [InlineData("vertices == 4", new[] { 0, 1, 2, 3, 4, 5 })]
    [InlineData("area == 1", new[] { 0, 1, 2, 3, 4, 5 })]
    [InlineData("non_planar", new int[0])]
    [InlineData("open", new int[0])]
    public void Faces_on_the_cube(string where, int[] expected) =>
        Assert.Equal(expected, Ids(TestMeshes.Cube(), Faces, where));

    [Fact]
    public void On_the_open_box_a_face_on_the_rim_is_open()
    {
        var mesh = OpenBox(); // face 3 is gone, so ids 0, 1, 2, 3, 4 are -z, +z, -y, -x, +x.
        Assert.Equal([0, 1, 3, 4], Ids(mesh, Faces, "open"));
        Assert.Equal([2], Ids(mesh, Faces, "not open"));
        Assert.Equal(5, Run(mesh, Faces, "none").Of);
    }

    [Fact]
    public void Non_planar_is_the_faces_the_report_warns_of()
    {
        var bent = TestMeshes.Ok(MeshEdits.Move(TestMeshes.Cube(), [6], null, null, new Vector3(0.1f, 0.1f, 0.1f)));
        Assert.Equal([1, 3, 5], Ids(bent, Faces, "non_planar"));
        Assert.Equal([0, 2, 4], Ids(bent, Faces, "not non_planar"));
        Assert.Equal(
            MeshReport.Create(bent).Warnings.Where(w => w.Kind == MeshWarningKind.NonPlanarFace).Select(w => w.Element),
            Ids(bent, Faces, "non_planar").Select(f => $"faces[{f}]"));

        // Below the report's limit a bend is no warning, so no match.
        var barely = TestMeshes.Ok(MeshEdits.Move(TestMeshes.Cube(), [6], null, null, new Vector3(0, 1e-5f, 0)));
        Assert.Empty(Ids(barely, Faces, "non_planar"));
    }

    // ---- The L-block ----

    [Fact]
    public void Every_kind_on_the_L_block()
    {
        var block = TestMeshes.LBlock();

        Assert.Equal(12, Run(block, Vertices, "faces == 3").Count);
        Assert.Equal(12, Run(block, Vertices, "edges == 3").Count);
        Assert.Equal([4, 5, 10, 11], Ids(block, Vertices, "y == 2"));
        Assert.Equal(Enumerable.Range(6, 6), Ids(block, Vertices, "id > 5"));

        Assert.Equal(18, Run(block, ElementKind.Edges, "manifold").Count);
        Assert.Equal([(0, 1), (0, 5), (6, 7), (6, 11)], Pairs(block, "length == 2"));
        Assert.Equal([(0, 5), (6, 11)], Pairs(block, "length == 2 and x == 0"));
        Assert.Equal([(0, 1), (0, 5), (0, 6)], Pairs(block, "a == 0"));

        Assert.Equal([0, 1], Ids(block, Faces, "vertices == 6"));
        Assert.Equal([2, 3, 4, 5, 6, 7], Ids(block, Faces, "vertices == 4"));
        Assert.Equal([0, 1], Ids(block, Faces, "area == 3"));
        Assert.Equal([2, 7], Ids(block, Faces, "area == 2"));
        Assert.Equal([3, 4, 5, 6], Ids(block, Faces, "area == 1"));
        Assert.Equal([4, 6], Ids(block, Faces, "normal.y > 0.9"));
        Assert.Equal([3], Ids(block, Faces, "min.x == 2"));
        // The centre is the mean of the corners, (1, 1), and not the hexagon's centroid, (5/6, 5/6).
        Assert.Equal([0, 1], Ids(block, Faces, "x == 1 and y == 1"));
        Assert.Empty(Ids(block, Faces, "non_planar or open"));
    }

    // ---- The crate, against the mesh's own numbers ----

    [Fact]
    public void Every_kind_on_the_crate_agrees_with_the_mesh()
    {
        var crate = TestMeshes.Crate();
        var verts = crate.Vertices;
        var edges = crate.Topology.Edges;

        Assert.Equal(Enumerable.Range(0, verts.Count), Ids(crate, Vertices, "all"));
        Assert.Equal(Enumerable.Range(0, crate.Faces.Count), Ids(crate, Faces, "all"));
        Assert.Equal(edges.Count, Run(crate, ElementKind.Edges, "manifold").Count);
        Assert.Empty(Pairs(crate, "open or nonmanifold"));
        Assert.Empty(Ids(crate, Vertices, "unattached or boundary"));
        Assert.Empty(Ids(crate, Faces, "open or non_planar"));

        Assert.Equal(Enumerable.Range(0, verts.Count).Where(i => verts[i].Y > 0.5f), Ids(crate, Vertices, "y > 0.5"));
        Assert.Equal(Enumerable.Range(0, crate.Faces.Count).Where(f => crate.FaceNormal(f).Y > 0.9f), Ids(crate, Faces, "normal.y > 0.9"));
        Assert.Equal(Enumerable.Range(0, crate.Faces.Count).Where(f => crate.FaceCentre(f).X < 0.1f), Ids(crate, Faces, "x < 0.1"));
        Assert.Equal(Enumerable.Range(0, crate.Faces.Count).Where(f => crate.Faces[f].Min(v => verts[v].Y) >= 0.5f), Ids(crate, Faces, "min.y >= 0.5"));
        Assert.Equal(Enumerable.Range(0, crate.Faces.Count).Where(f => crate.Faces[f].Max(v => verts[v].Z) <= 0.5f), Ids(crate, Faces, "max.z <= 0.5"));
        Assert.Equal(edges.Where(e => Vector3.Distance(verts[e.Lo], verts[e.Hi]) > 0.3f).Select(e => (e.Lo, e.Hi)), Pairs(crate, "length > 0.3"));
        Assert.Equal(edges.Where(e => Math.Max(verts[e.Lo].Y, verts[e.Hi].Y) < 0.2f).Select(e => (e.Lo, e.Hi)), Pairs(crate, "max.y < 0.2"));

        // Each vertex's faces add up to the corners, and its edges to both ends of every edge.
        var corners = crate.Faces.Sum(f => f.Count);
        Assert.Equal(corners, Enumerable.Range(1, 12).Sum(n => n * Run(crate, Vertices, $"faces == {n}").Count));
        Assert.Equal(2 * edges.Count, Enumerable.Range(1, 12).Sum(n => n * Run(crate, Vertices, $"edges == {n}").Count));
        Assert.Equal(corners, Enumerable.Range(3, 10).Sum(n => n * Run(crate, Faces, $"vertices == {n}").Count));
    }

    [Theory]
    [InlineData(Vertices)]
    [InlineData(ElementKind.Edges)]
    [InlineData(Faces)]
    public void The_result_lists_are_ascending_and_of_counts_the_kind(ElementKind kind)
    {
        var crate = TestMeshes.Crate();
        var all = Run(crate, kind, "all");
        Assert.Equal(all.Of, all.Count);
        Assert.Equal(kind switch { Vertices => crate.Vertices.Count, ElementKind.Edges => crate.Topology.Edges.Count, _ => crate.Faces.Count }, all.Of);

        // The other kinds' counts differ on the crate, so a wrong table cannot pass.
        Assert.Equal(all.Of, Run(crate, kind, "none").Of);
        if (kind == ElementKind.Edges)
        {
            Assert.Empty(all.Ids);
            Assert.Equal(all.Edges.OrderBy(e => e.Lo).ThenBy(e => e.Hi), all.Edges);
            Assert.All(all.Edges, e => Assert.True(e.Lo < e.Hi));
            Assert.Equal(all.Edges.Distinct().Count(), all.Count);
        }
        else
        {
            Assert.Empty(all.Edges);
            Assert.Equal(all.Ids.Order(), all.Ids);
            Assert.Equal(all.Ids.Distinct().Count(), all.Count);
        }
    }

    // ---- Float ----

    private static PolygonMesh TriangleAt(float y) => TestMeshes.Make([new(0, y, 0), new(1, y, 0), new(0, y, 1)], [[0, 1, 2]]);

    [Fact]
    public void A_literal_is_narrowed_to_float_before_it_is_compared()
    {
        var mesh = TriangleAt(1.02f);
        Assert.Equal([0, 1, 2], Ids(mesh, Vertices, "y == 1.02"));
        Assert.Empty(Ids(mesh, Vertices, "y == 1.0199999"));
        Assert.Empty(Ids(mesh, Vertices, "y != 1.02"));
        Assert.Empty(Ids(mesh, Vertices, "y > 1.02"));
        Assert.Empty(Ids(mesh, Vertices, "y < 1.02"));
        Assert.Equal([0], Ids(mesh, Faces, "y == 1.02"));
        Assert.Equal([0], Ids(mesh, Faces, "min.y >= 1.02 and max.y <= 1.02"));
    }

    [Fact]
    public void A_computed_property_is_narrowed_to_float_before_it_is_compared()
    {
        // The double midpoint of 0.1f and 0.2f is not 0.15, nor 0.15f; narrowed, it is 0.15f.
        var mesh = TestMeshes.Make([new(0, 0.1f, 0), new(0, 0.2f, 0), new(1, 0.1f, 0)], [[0, 1, 2]]);
        Assert.Equal([(0, 1)], Pairs(mesh, "x == 0 and y == 0.15"));
    }

    [Fact]
    public void A_literal_too_small_for_a_float_is_zero()
    {
        Assert.Equal([0, 1, 4, 5], Ids(TestMeshes.Cube(), Vertices, "y == 1e-99"));
    }

    // ---- Errors ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void An_empty_where_says_so(string where)
    {
        var parsed = MeshQuery.Parse(where);
        Assert.False(parsed.Succeeded);
        Assert.Null(parsed.Expression);
        Assert.Equal([new MeshError("where", "where is empty")], parsed.Errors);
        Assert.Equal(parsed.Errors, parsed.ErrorsFor(Faces));
    }

    [Theory]
    // The wording of the spec, at the spec's own positions.
    [InlineData(Faces, "y>1 and z >> 2", "at character 11: '>>' is not an operator; use " + Operators)]
    [InlineData(Faces, "centre > 1", "at character 1: 'centre' is not a property of faces; faces have " + FaceNames)]
    [InlineData(Faces, "normal.w > 0", "at character 8: 'normal.w' is not an axis; use .x, .y or .z")]
    [InlineData(Faces, "not open > 1", "at character 5: a flag cannot be compared with a number")]
    [InlineData(Faces, "y > 1 and x <", "at character 14: expected a number")]
    [InlineData(Faces, "(y > 1 and x < 0.25", "at character 20: expected ')'")]
    // Every other operator-shaped run.
    [InlineData(Vertices, "y = 1", "at character 3: '=' is not an operator; use " + Operators)]
    [InlineData(Vertices, "y => 1", "at character 3: '=>' is not an operator; use " + Operators)]
    [InlineData(Vertices, "y =< 1", "at character 3: '=<' is not an operator; use " + Operators)]
    [InlineData(Vertices, "y <> 1", "at character 3: '<>' is not an operator; use " + Operators)]
    [InlineData(Vertices, "y ! 1", "at character 3: '!' is not an operator; use " + Operators)]
    [InlineData(Vertices, "y === 1", "at character 3: '===' is not an operator; use " + Operators)]
    // A name each kind does not have, in its own list.
    [InlineData(Vertices, "normal.y > 0", "at character 1: 'normal.y' is not a property of vertices; vertices have " + VertexNames)]
    [InlineData(Vertices, "open", "at character 1: 'open' is not a property of vertices; vertices have " + VertexNames)]
    [InlineData(Vertices, "area > 0", "at character 1: 'area' is not a property of vertices; vertices have " + VertexNames)]
    [InlineData(Edges, "id == 1", "at character 1: 'id' is not a property of edges; edges have " + EdgeNames)]
    [InlineData(Edges, "boundary", "at character 1: 'boundary' is not a property of edges; edges have " + EdgeNames)]
    [InlineData(Edges, "vertices == 2", "at character 1: 'vertices' is not a property of edges; edges have " + EdgeNames)]
    [InlineData(Faces, "faces > 0", "at character 1: 'faces' is not a property of faces; faces have " + FaceNames)]
    [InlineData(Faces, "unattached", "at character 1: 'unattached' is not a property of faces; faces have " + FaceNames)]
    [InlineData(Faces, "manifold", "at character 1: 'manifold' is not a property of faces; faces have " + FaceNames)]
    [InlineData(Faces, "CENTRE > 1", "at character 1: 'CENTRE' is not a property of faces; faces have " + FaceNames)]
    // An axis where there is none, and none where there must be.
    [InlineData(Faces, "id.x > 1", "at character 1: 'id.x' is not a property of faces; faces have " + FaceNames)]
    [InlineData(Faces, "x.y > 1", "at character 1: 'x.y' is not a property of faces; faces have " + FaceNames)]
    [InlineData(Faces, "open.x", "at character 1: 'open.x' is not a property of faces; faces have " + FaceNames)]
    [InlineData(Faces, "min.w > 1", "at character 5: 'min.w' is not an axis; use .x, .y or .z")]
    [InlineData(Faces, "normal.5 > 1", "at character 8: 'normal.5' is not an axis; use .x, .y or .z")]
    [InlineData(Faces, "min > 1", "at character 1: 'min' needs an axis; use .x, .y or .z")]
    [InlineData(Faces, "normal > 1", "at character 1: 'normal' needs an axis; use .x, .y or .z")]
    [InlineData(Faces, "max. > 1", "at character 1: 'max' needs an axis; use .x, .y or .z")]
    // Flags and numbers the wrong way round.
    [InlineData(Edges, "manifold == 1", "at character 1: a flag cannot be compared with a number")]
    [InlineData(Vertices, "unattached != 0", "at character 1: a flag cannot be compared with a number")]
    [InlineData(Faces, "area", "at character 1: 'area' is a number; compare it with " + Operators)]
    [InlineData(Faces, "normal.y", "at character 1: 'normal.y' is a number; compare it with " + Operators)]
    [InlineData(Vertices, "all and y", "at character 9: 'y' is a number; compare it with " + Operators)]
    // What a comparison's right side cannot be.
    [InlineData(Faces, "y > z", "at character 5: expected a number")]
    [InlineData(Faces, "y > and x > 1", "at character 5: expected a number")]
    [InlineData(Faces, "y >", "at character 4: expected a number")]
    [InlineData(Vertices, "y > 1e99", "at character 5: 1e99 is out of range: a number must lie within ±3.40282347E+38")]
    [InlineData(Vertices, "y > -1e99", "at character 5: -1e99 is out of range: a number must lie within ±3.40282347E+38")]
    // Where an atom or the end was expected.
    [InlineData(Faces, "y > 1 and", "at character 10: expected a property, a flag, all, none, not or '('")]
    [InlineData(Faces, "y > 1 or or x > 1", "at character 10: expected a property, a flag, all, none, not or '('")]
    [InlineData(Faces, "not", "at character 4: expected a property, a flag, all, none, not or '('")]
    [InlineData(Faces, "()", "at character 2: expected a property, a flag, all, none, not or '('")]
    [InlineData(Faces, ")", "at character 1: expected a property, a flag, all, none, not or '('")]
    [InlineData(Faces, "$", "at character 1: expected a property, a flag, all, none, not or '('")]
    [InlineData(Faces, "y > 1)", "at character 6: expected 'and', 'or' or the end of the expression")]
    [InlineData(Faces, "y > 1 $", "at character 7: expected 'and', 'or' or the end of the expression")]
    [InlineData(Faces, "all all", "at character 5: expected 'and', 'or' or the end of the expression")]
    [InlineData(Faces, "y > 1 2", "at character 7: expected 'and', 'or' or the end of the expression")]
    public void An_error_gives_its_character_and_says_what_is_wrong(ElementKind kind, string where, string message)
    {
        var errors = MeshQuery.Parse(where).ErrorsFor(kind);
        Assert.Equal(new MeshError("where", message), Assert.Single(errors));
    }

    [Fact]
    public void Grammar_errors_stop_the_parse_and_name_errors_wait_for_the_kind()
    {
        var bad = MeshQuery.Parse("y >> 1");
        Assert.False(bad.Succeeded);
        Assert.Null(bad.Expression);
        Assert.Single(bad.Errors);

        var named = MeshQuery.Parse("centre > 1");
        Assert.True(named.Succeeded);
        Assert.Empty(named.Errors);
        Assert.Single(named.ErrorsFor(Faces));
        Assert.Throws<ArgumentException>(() => MeshQuery.Select(TestMeshes.Cube(), Faces, named.Expression!));
    }

    [Fact]
    public void Every_error_that_can_be_found_is_listed_in_order_of_position()
    {
        const string where = "centre > 1 and y >> 2 and normal.w > 0 and not open > abc and x > and (area > 1";
        int At(string text) => where.IndexOf(text, StringComparison.Ordinal) + 1;

        Assert.Equal(
        [
            $"where: at character 1: 'centre' is not a property of faces; faces have {FaceNames}",
            $"where: at character {At(">>")}: '>>' is not an operator; use {Operators}",
            $"where: at character {At("w >")}: 'normal.w' is not an axis; use .x, .y or .z",
            $"where: at character {At("open")}: a flag cannot be compared with a number",
            $"where: at character {At("abc")}: expected a number",
            $"where: at character {At("and (")}: expected a number",
            $"where: at character {where.Length + 1}: expected ')'",
        ], Errors(Faces, where));
    }

    [Theory]
    [InlineData("(y > 1 and x <", 15, "expected a number")]
    [InlineData("(not", 5, "expected a property, a flag, all, none, not or '('")]
    public void A_missing_bracket_is_listed_after_another_error_at_the_same_character(string where, int at, string first)
    {
        Assert.Equal([$"where: at character {at}: {first}", $"where: at character {at}: expected ')'"], Errors(Faces, where));
    }

    [Fact]
    public void A_missing_connective_is_reported_and_what_follows_it_is_still_read()
    {
        Assert.Equal(
        [
            "where: at character 7: expected 'and', 'or' or the end of the expression",
            "where: at character 11: expected a number",
        ], Errors(Faces, "y > 1 x > z"));
    }

    [Fact]
    public void Past_the_limit_nesting_is_one_error_however_deep_the_text_goes()
    {
        var depth = MeshQuery.MaxDepth;
        var message = $"nested too deeply; 'not' and '(' go {depth} levels deep at most";

        Assert.Equal([$"where: at character {depth + 1}: {message}"], Errors(Faces, new string('(', depth + 1) + "all" + new string(')', depth + 1)));
        Assert.Equal([$"where: at character {4 * depth + 1}: {message}"], Errors(Faces, string.Concat(Enumerable.Repeat("not ", depth + 1)) + "all"));
        Assert.Equal([$"where: at character {depth + 1}: {message}"], Errors(Faces, new string('(', 1_000_000)));
    }

    /// <summary>Three triangles on the edge 0-1, a fin: the shared edge non-manifold, the other six open.</summary>
    private static PolygonMesh Fin() => TestMeshes.Make(
        [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1)],
        [[0, 1, 2], [1, 0, 3], [0, 1, 4]]);
}
