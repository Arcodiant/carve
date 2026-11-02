using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

[Trait("Tier", "unit")]
public class MeshBridgeTests
{
    private static readonly PolygonMesh Cube = TestMeshes.Cube();

    private static Empyrean.MeshData<Empyrean.PositionUvNormalVertex> Drawn(PolygonMesh mesh)
    {
        var report = MeshReport.Create(mesh);
        return MeshBridge.ToMeshData(mesh, DrawnSpace.Of(report.BoundsMin, report.BoundsMax));
    }

    [Fact]
    public void Every_triangle_is_clockwise_from_outside()
    {
        var data = Drawn(Cube);

        Assert.Equal(36, data.Indices!.Length);
        for (var i = 0; i < data.Indices!.Length; i += 3)
        {
            var a = data.Vertices[data.Indices![i]];
            var b = data.Vertices[data.Indices![i + 1]];
            var c = data.Vertices[data.Indices![i + 2]];
            var wound = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);

            // Counter-clockwise would point along the outward normal.
            Assert.True(Vector3.Dot(wound, a.Normal) < 0, $"triangle at {i} faces inward");
        }
    }

    [Fact]
    public void Each_triangle_has_its_own_vertices_and_the_faces_normal()
    {
        var data = Drawn(Cube);

        Assert.Equal(36, data.Vertices.Length);
        Assert.Equal(36, data.Indices!.Distinct().Count());
        for (var i = 0; i < 12; i++)
        {
            var face = i / 2; // Two triangles per quad, in face order.
            Assert.Equal(Cube.FaceNormal(face), data.Vertices[i * 3].Normal);
            Assert.Equal(Cube.FaceNormal(face), data.Vertices[i * 3 + 1].Normal);
            Assert.Equal(Cube.FaceNormal(face), data.Vertices[i * 3 + 2].Normal);
        }
    }

    [Fact]
    public void A_polygon_is_fanned_into_its_triangles()
    {
        var hexagon = TestMeshes.Make(
            [new(2, 0, 0), new(1, 2, 0), new(-1, 2, 0), new(-2, 0, 0), new(-1, -2, 0), new(1, -2, 0)],
            [[0, 1, 2, 3, 4, 5]]);

        var data = Drawn(hexagon);

        Assert.Equal(4 * 3, data.Vertices.Length);
        Assert.All(data.Vertices, v => Assert.Equal(Vector3.UnitZ, v.Normal));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1e-30, 0)]
    [InlineData(1e30, 0)]
    [InlineData(1, 1e7)]
    [InlineData(1e30, -1e30)]
    public void The_drawn_copy_is_centred_with_half_diagonal_one_and_the_stored_mesh_stays(double side, double offset)
    {
        var at = (double c) => (float)(c * side + offset);
        var mesh = TestMeshes.Make(
            [new(at(0), at(0), at(0)), new(at(1), at(0), at(0)), new(at(1), at(1), at(0)), new(at(0), at(1), at(1))],
            [[0, 1, 2], [0, 2, 3]]);
        var stored = mesh.Vertices.ToArray();

        var data = Drawn(mesh);

        Assert.Equal(1, data.Vertices.Max(v => v.Position.Length()), 1e-5f);
        Assert.All(data.Vertices, v => Assert.InRange(v.Position.Length(), 0, 1.00001f));
        Assert.Equal(stored, mesh.Vertices);
    }

    [Fact]
    public void A_uniform_scale_leaves_the_normals_alone()
    {
        var far = TestMeshes.Make([new(10, 20, 30), new(1e4f, 20, 30), new(1e4f, 1e4f, 30), new(10, 1e4f, 1e4f)], [[0, 1, 2], [0, 2, 3]]);

        var data = Drawn(far);

        Assert.Equal(far.FaceNormal(0), data.Vertices[0].Normal);
        Assert.Equal(far.FaceNormal(1), data.Vertices[3].Normal);
    }

    [Fact]
    public void Bounds_of_no_size_have_radius_one()
    {
        var point = new Vector3(3, 4, 5);

        var space = DrawnSpace.Of(point, point);

        Assert.Equal(1, space.Radius);
        Assert.Equal(Vector3.Zero, space.Apply(point));
    }
}
