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

    /// <summary>
    /// The L hexagon extruded one along +z, as the cube scenario builds it: vertices 0-5 the z=0 outline, 6-11 the z=1 one;
    /// face 0 the -z cap, face 1 the +z cap, faces 2-7 the sides, side <c>2 + i</c> running from outline corner i to i + 1.
    /// </summary>
    public static PolygonMesh LBlock()
    {
        var vertices = LVertices.Concat(LVertices.Select(v => v with { Z = 1 }));
        var faces = new List<int[]> { new[] { 5, 4, 3, 2, 1, 0 }, new[] { 6, 7, 8, 9, 10, 11 } };
        for (var i = 0; i < 6; i++)
        {
            var j = (i + 1) % 6;
            faces.Add([i, j, j + 6, i + 6]);
        }
        return Make(vertices, faces);
    }
}
