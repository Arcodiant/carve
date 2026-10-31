using System.Numerics;
using Carve.Core;

namespace Carve.Server.Tests;

public class MeshBridgeTests
{
    private static PolygonMesh Mesh(Vector3[] vertices, int[][] faces) => PolygonMesh.Create(vertices, faces).Mesh!;

    private static readonly PolygonMesh Cube = Mesh(
        [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
        [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]]);

    [Fact]
    public void Every_triangle_is_clockwise_from_outside()
    {
        var data = MeshBridge.ToMeshData(Cube);

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
        var data = MeshBridge.ToMeshData(Cube);

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
        var hexagon = Mesh(
            [new(2, 0, 0), new(1, 2, 0), new(-1, 2, 0), new(-2, 0, 0), new(-1, -2, 0), new(1, -2, 0)],
            [[0, 1, 2, 3, 4, 5]]);

        var data = MeshBridge.ToMeshData(hexagon);

        Assert.Equal(4 * 3, data.Vertices.Length);
        Assert.All(data.Vertices, v => Assert.Equal(Vector3.UnitZ, v.Normal));
    }
}
