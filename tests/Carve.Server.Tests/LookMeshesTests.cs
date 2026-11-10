using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>What the looks draw, with no device: the lines and colours, and how a highlight compares.</summary>
[Trait("Tier", "unit")]
public class LookMeshesTests
{
    private static PolygonMesh Cube() => TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces);

    private static DrawnSpace SpaceOf(PolygonMesh mesh)
    {
        var report = MeshReport.Create(mesh);
        return DrawnSpace.Of(report.BoundsMin, report.BoundsMax);
    }

    private static Vector3[] Ends(MeshData<PositionVertex>? lines) => lines!.Value.Vertices.Select(v => v.Position).ToArray();

    /// <summary>The rule, worked out here: 0.004 along the mean of the edge's face normals.</summary>
    private static Vector3 Lift(PolygonMesh mesh, MeshEdge edge) =>
        Vector3.Normalize(edge.Faces.Aggregate(Vector3.Zero, (sum, use) => sum + mesh.FaceNormal(use.Face))) * 0.004f;

    private static void AssertClose(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-5f, $"{expected} against {actual}");

    [Fact]
    public void The_wireframe_is_every_edge_lifted_off_its_faces()
    {
        var mesh = Cube();
        var space = SpaceOf(mesh);

        var lines = LookMeshes.WireframeLines(mesh, space, Highlight.None);
        var ends = Ends(lines);

        Assert.Equal(Empyrean.MeshTopology.LineList, lines!.Value.Topology);
        Assert.Equal(2 * mesh.Topology.Edges.Count, ends.Length);
        for (var i = 0; i < mesh.Topology.Edges.Count; i++)
        {
            var edge = mesh.Topology.Edges[i];
            var lift = Lift(mesh, edge);
            AssertClose(space.Apply(mesh.Vertices[edge.Lo]) + lift, ends[2 * i]);
            AssertClose(space.Apply(mesh.Vertices[edge.Hi]) + lift, ends[2 * i + 1]);
        }
    }

    [Fact]
    public void An_edge_in_a_concave_corner_comes_out_of_both_its_faces()
    {
        // An L in profile: the inner corner is the edge from (1, 1, 0) to (1, 1, 1), between a floor and a wall.
        var mesh = TestMeshes.Make(
            [
                new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(1, 1, 0), new(1, 2, 0), new(0, 2, 0),
                new(0, 0, 1), new(2, 0, 1), new(2, 1, 1), new(1, 1, 1), new(1, 2, 1), new(0, 2, 1),
            ],
            [
                [5, 4, 3, 2, 1, 0], [6, 7, 8, 9, 10, 11],
                [0, 1, 7, 6], [1, 2, 8, 7], [2, 3, 9, 8], [3, 4, 10, 9], [4, 5, 11, 10], [5, 0, 6, 11],
            ]);
        var space = SpaceOf(mesh);
        var inner = mesh.Topology.Edges.Single(e => e.Lo == 3 && e.Hi == 9);

        var lift = Lift(mesh, inner);

        // Away from the solid, toward the open quarter: +X off the wall, +Y off the floor.
        Assert.True(lift.X > 0 && lift.Y > 0, lift.ToString());
        Assert.Equal(0f, lift.Z, 1e-6f);
        Assert.Equal(0.004f, lift.Length(), 1e-6f);
        var ends = Ends(LookMeshes.WireframeLines(mesh, space, Highlight.None));
        var at = mesh.Topology.Edges.ToList().IndexOf(inner);
        AssertClose(space.Apply(mesh.Vertices[3]) + lift, ends[2 * at]);
    }

    [Fact]
    public void A_highlighted_edge_leaves_the_wireframe_for_the_orange_lines()
    {
        var mesh = Cube();
        var space = SpaceOf(mesh);
        var edge = mesh.Topology.Edges[0];
        var highlight = Highlight.Of([], [(edge.Hi, edge.Lo)], []);

        var scene = LookMeshes.Compose(mesh, space, Look.Wireframe, highlight);

        Assert.Equal(2 * (mesh.Topology.Edges.Count - 1), Ends(scene.EdgeLines).Length);
        var lift = Lift(mesh, edge);
        Assert.Equal([space.Apply(mesh.Vertices[edge.Lo]) + lift, space.Apply(mesh.Vertices[edge.Hi]) + lift], Ends(scene.OrangeLines));
    }

    [Fact]
    public void A_spike_stands_on_each_face_along_its_normal()
    {
        var mesh = Cube();
        var space = SpaceOf(mesh);

        var ends = Ends(LookMeshes.Spikes(mesh, space));

        Assert.Equal(2 * mesh.Faces.Count, ends.Length);
        for (var f = 0; f < mesh.Faces.Count; f++)
        {
            AssertClose(space.Apply(mesh.FaceCentre(f)), ends[2 * f]);
            AssertClose(mesh.FaceNormal(f) * 0.1f, ends[2 * f + 1] - ends[2 * f]);
            Assert.Equal(0.1f, (ends[2 * f + 1] - ends[2 * f]).Length(), 1e-5f);
        }
    }

    [Theory]
    [InlineData(1, 0, 0, 255, 128, 128)]
    [InlineData(-1, 0, 0, 0, 128, 128)]
    [InlineData(0, 1, 0, 128, 255, 128)]
    [InlineData(0, -1, 0, 128, 0, 128)]
    [InlineData(0, 0, 1, 128, 128, 255)]
    [InlineData(0, 0, -1, 128, 128, 0)]
    public void A_face_is_coloured_by_its_normal(float x, float y, float z, byte r, byte g, byte b) =>
        Assert.Equal(new Rgba8(r, g, b, 255), LookMeshes.NormalColour(new Vector3(x, y, z)));

    [Fact]
    public void Each_vertex_in_normals_carries_its_faces_colour()
    {
        var mesh = Cube();
        var scene = LookMeshes.Compose(mesh, SpaceOf(mesh), Look.Normals, Highlight.None);

        var triangles = FlatShading.Triangles(mesh);
        Assert.Equal(triangles.Count * 3, scene.VertexColours!.Length);
        for (var i = 0; i < scene.VertexColours.Length; i++)
        {
            Assert.Equal(LookMeshes.NormalColour(mesh.FaceNormal(triangles[i / 3].Face)), scene.VertexColours[i]);
        }

        Assert.Equal(Vector4.One, scene.FlatColour);
    }

    [Fact]
    public void A_smooth_mesh_in_normals_carries_its_corner_normals_in_colour_and_spikes()
    {
        var mesh = Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, []));
        var space = SpaceOf(mesh);
        var scene = LookMeshes.Compose(mesh, space, Look.Normals, Highlight.None);

        var triangles = Shading.Triangles(mesh);
        for (var i = 0; i < scene.VertexColours!.Length; i++)
        {
            var corner = i % 3 == 0 ? triangles[i / 3].NormalA : i % 3 == 1 ? triangles[i / 3].NormalB : triangles[i / 3].NormalC;
            Assert.Equal(LookMeshes.NormalColour(corner), scene.VertexColours[i]);
        }

        Assert.Contains(scene.VertexColours, c => c != LookMeshes.NormalColour(mesh.FaceNormal(0)) && c != LookMeshes.NormalColour(mesh.FaceNormal(1)));
        Assert.Equal(2 * mesh.Faces.Sum(f => f.Count), Ends(scene.EdgeLines).Length);
    }

    [Fact]
    public void Only_highlighted_faces_turn_orange_in_shaded_and_the_rest_stay_white()
    {
        var mesh = Cube();
        var scene = LookMeshes.Compose(mesh, SpaceOf(mesh), Look.Shaded, Highlight.Of([], [], [3]));

        var triangles = FlatShading.Triangles(mesh);
        var orange = Rgba8.FromVector(new Vector3(1, 0.55f, 0.1f));
        for (var i = 0; i < scene.VertexColours!.Length; i++)
        {
            Assert.Equal(triangles[i / 3].Face == 3 ? orange : Rgba8.White, scene.VertexColours[i]);
        }

        Assert.Null(scene.FlatColour);
        Assert.Null(scene.EdgeLines);
    }

    [Fact]
    public void Only_highlighted_faces_turn_orange_in_wireframe_and_the_rest_stay_the_fill()
    {
        var mesh = Cube();
        var scene = LookMeshes.Compose(mesh, SpaceOf(mesh), Look.Wireframe, Highlight.Of([], [], [3]));

        var triangles = FlatShading.Triangles(mesh);
        var orange = Rgba8.FromVector(new Vector3(1, 0.55f, 0.1f));
        var fill = Rgba8.FromVector(new Vector3(0.09f, 0.095f, 0.10f));
        for (var i = 0; i < scene.VertexColours!.Length; i++)
        {
            Assert.Equal(triangles[i / 3].Face == 3 ? orange : fill, scene.VertexColours[i]);
        }

        Assert.Equal(Vector4.One, scene.FlatColour);
    }

    [Fact]
    public void In_normals_a_highlighted_face_keeps_its_colour_and_its_loop_turns_orange()
    {
        var mesh = Cube();
        var space = SpaceOf(mesh);
        var plain = LookMeshes.Compose(mesh, space, Look.Normals, Highlight.None);

        var scene = LookMeshes.Compose(mesh, space, Look.Normals, Highlight.Of([], [], [3]));

        Assert.Equal(plain.VertexColours, scene.VertexColours);
        Assert.Equal(2 * mesh.Faces[3].Count, Ends(scene.OrangeLines).Length);
    }

    [Theory]
    [InlineData(Look.Shaded)]
    [InlineData(Look.Wireframe)]
    public void With_no_highlighted_face_the_mesh_carries_no_colours_and_no_orange(Look look)
    {
        var mesh = Cube();

        var scene = LookMeshes.Compose(mesh, SpaceOf(mesh), look, Highlight.None);

        Assert.Null(scene.VertexColours);
        Assert.Null(scene.OrangeLines);
        Assert.Equal(look == Look.Wireframe ? LookMeshes.Fill : null, scene.FlatColour);
    }

    [Fact]
    public void A_star_is_three_axis_segments_through_the_vertex()
    {
        var mesh = Cube();
        var space = SpaceOf(mesh);
        var at = space.Apply(mesh.Vertices[6]);

        var ends = Ends(LookMeshes.OrangeLines(mesh, space, Look.Shaded, Highlight.Of([6], [], [])));

        Assert.Equal(
            [
                at - Vector3.UnitX * 0.02f, at + Vector3.UnitX * 0.02f,
                at - Vector3.UnitY * 0.02f, at + Vector3.UnitY * 0.02f,
                at - Vector3.UnitZ * 0.02f, at + Vector3.UnitZ * 0.02f,
            ],
            ends);
    }

    [Fact]
    public void Ids_a_new_mesh_lacks_draw_nothing()
    {
        var mesh = Cube();

        var scene = LookMeshes.Compose(mesh, SpaceOf(mesh), Look.Shaded, Highlight.Of([99], [(90, 91)], [99]));

        Assert.Null(scene.VertexColours);
        Assert.Null(scene.OrangeLines);
    }

    [Fact]
    public void Equal_highlights_compare_equal_whatever_their_order()
    {
        var a = Highlight.Of([1, 2, 2], [(3, 1), (1, 2)], [5, 4]);
        var b = Highlight.Of([2, 1], [(1, 2), (1, 3)], [4, 5]);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Highlight.Of([1, 2], [(1, 2)], [4, 5]));
        Assert.Equal(Highlight.None, Highlight.Of([], [], []));
        Assert.Equal(new RenderSpec(new CameraSpec(Vector3.One)) { Highlight = a }, new RenderSpec(new CameraSpec(Vector3.One)) { Highlight = b });
    }

    [Theory]
    [InlineData(new int[0], new int[0], "")]
    [InlineData(new[] { 1 }, new int[0], "1 vertex")]
    [InlineData(new[] { 1, 2 }, new[] { 3 }, "1 face, 2 vertices")]
    public void The_counts_name_only_what_is_there(int[] vertices, int[] faces, string expected) =>
        Assert.Equal(expected, Highlight.Of(vertices, [], faces).Counts());

    [Fact]
    public void The_counts_go_faces_edges_vertices() =>
        Assert.Equal("3 faces, 2 edges, 1 vertex", Highlight.Of([0], [(0, 1), (1, 2)], [1, 2, 3]).Counts());
}
