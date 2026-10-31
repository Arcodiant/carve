using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

internal static class TestMeshes
{
    public static readonly Vector3[] CubeVertices =
    [
        new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
        new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1),
    ];

    // Counter-clockwise seen from outside.
    public static readonly int[][] CubeFaces =
    [
        [0, 3, 2, 1], // -z
        [4, 5, 6, 7], // +z
        [0, 1, 5, 4], // -y
        [3, 7, 6, 2], // +y
        [0, 4, 7, 3], // -x
        [1, 2, 6, 5], // +x
    ];

    public static PolygonMesh Make(IEnumerable<Vector3> vertices, IEnumerable<int[]> faces)
    {
        var result = PolygonMesh.Create(vertices, faces);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Mesh!;
    }

    public static PolygonMesh Cube() => Make(CubeVertices, CubeFaces);

    /// <summary>L-shaped hexagon in the XY plane, area 3, wound counter-clockwise seen from +z.</summary>
    public static readonly Vector3[] LVertices =
    [
        new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(1, 1, 0), new(1, 2, 0), new(0, 2, 0),
    ];
}
