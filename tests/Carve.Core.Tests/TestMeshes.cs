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

    /// <summary>Every face id of <see cref="Cube"/>.</summary>
    public static readonly int[] AllCubeFaces = [0, 1, 2, 3, 4, 5];

    /// <summary>The mesh a successful edit made; fails the test, with the edit's errors, otherwise.</summary>
    public static PolygonMesh Ok(EditResult result)
    {
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Mesh!;
    }

    /// <summary>The planning probe's crate, built through <see cref="MeshEdits"/> alone: the seed cube and 11 edits.</summary>
    public static PolygonMesh Crate()
    {
        var mesh = Cube();
        mesh = Ok(MeshEdits.Move(mesh, null, null, [3], new Vector3(0, -0.2f, 0)));
        mesh = Ok(MeshEdits.Extrude(mesh, AllCubeFaces, 0, 0.12f));
        mesh = Ok(MeshEdits.Extrude(mesh, AllCubeFaces, -0.08f));

        // -X: the slot's edges drawn together by moving their vertices.
        mesh = Ok(MeshEdits.Extrude(mesh, [4], 0, 0.05f));
        var (top, bottom) = TopAndBottom(mesh, 4);
        mesh = Ok(MeshEdits.Move(mesh, [top.A, top.B], null, null, new Vector3(0, -0.17f, 0)));
        mesh = Ok(MeshEdits.Move(mesh, [bottom.A, bottom.B], null, null, new Vector3(0, 0.17f, 0)));
        mesh = Ok(MeshEdits.Extrude(mesh, [4], 0.1f));

        // +X: the same by moving the edges.
        mesh = Ok(MeshEdits.Extrude(mesh, [5], 0, 0.05f));
        (top, bottom) = TopAndBottom(mesh, 5);
        mesh = Ok(MeshEdits.Move(mesh, null, [top], null, new Vector3(0, -0.17f, 0)));
        mesh = Ok(MeshEdits.Move(mesh, null, [bottom], null, new Vector3(0, 0.17f, 0)));
        mesh = Ok(MeshEdits.Extrude(mesh, [5], 0.1f));
        return mesh;
    }

    /// <summary>The face's highest and lowest edges in Y, as vertex pairs.</summary>
    private static ((int A, int B) Top, (int A, int B) Bottom) TopAndBottom(PolygonMesh mesh, int face)
    {
        var loop = mesh.Faces[face];
        var pairs = Enumerable.Range(0, loop.Count).Select(k => (A: loop[k], B: loop[(k + 1) % loop.Count])).ToArray();
        float Y((int A, int B) e) => Math.Min(mesh.Vertices[e.A].Y, mesh.Vertices[e.B].Y);
        bool Level((int A, int B) e) => Math.Abs(mesh.Vertices[e.A].Y - mesh.Vertices[e.B].Y) < 1e-6f;
        var level = pairs.Where(Level).OrderBy(Y).ToArray();
        Assert.Equal(2, level.Length);
        return (level[1], level[0]);
    }

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
