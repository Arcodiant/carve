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

    /// <summary>
    /// The barrel (T-53, M-6's UV sample): eight sides and five rings at y = 0, 0.1, 0.5, 0.9 and 1.0, radii 0.36, 0.38,
    /// 0.42, 0.38 and 0.36. Ring i's vertex k (id 8i + k) is at angle a = 2πk/8, at (r cos a, y, -r sin a) rounded to 6
    /// decimals; side faces 0..31 are [8i+k, 8i+(k+1)%8, 8(i+1)+(k+1)%8, 8(i+1)+k], the bottom cap (32) is [7, 6, ..., 0]
    /// and the top cap (33) [32, 33, ..., 39]. No UVs and no seams.
    /// </summary>
    public static PolygonMesh Barrel()
    {
        double[] ys = [0, 0.1, 0.5, 0.9, 1.0];
        double[] radii = [0.36, 0.38, 0.42, 0.38, 0.36];
        var vertices = new List<Vector3>();
        for (var i = 0; i < 5; i++)
            for (var k = 0; k < 8; k++)
            {
                var a = 2 * Math.PI * k / 8;
                vertices.Add(new Vector3((float)Math.Round(radii[i] * Math.Cos(a), 6), (float)ys[i], (float)Math.Round(-radii[i] * Math.Sin(a), 6)));
            }
        var faces = new List<int[]>();
        for (var i = 0; i < 4; i++)
            for (var k = 0; k < 8; k++)
                faces.Add([8 * i + k, 8 * i + (k + 1) % 8, 8 * (i + 1) + (k + 1) % 8, 8 * (i + 1) + k]);
        faces.Add([7, 6, 5, 4, 3, 2, 1, 0]);
        faces.Add([32, 33, 34, 35, 36, 37, 38, 39]);
        return Make(vertices, faces);
    }

    /// <summary>The barrel's seams (T-56): the four vertical edges at -Z and both cap rings.</summary>
    public static readonly (int A, int B)[] BarrelSeams =
    [
        (2, 10), (10, 18), (18, 26), (26, 34),
        .. Enumerable.Range(0, 8).Select(k => (k, (k + 1) % 8)),
        .. Enumerable.Range(0, 8).Select(k => (32 + k, 32 + (k + 1) % 8)),
    ];

    /// <summary>The barrel with <see cref="BarrelSeams"/> marked: three islands, the side (32 faces) and both caps.</summary>
    public static PolygonMesh SeamedBarrel() => Barrel().WithUvs(MeshUvs.None.WithSeams(BarrelSeams, true));

    /// <summary>The barrel's side, faces 0 to 31 only: a tube with two boundary loops and no seam.</summary>
    public static PolygonMesh Tube()
    {
        var barrel = Barrel();
        return Make(barrel.Vertices, barrel.Faces.Take(32).Select(f => f.Loop.ToArray()));
    }

    /// <summary>A flat n x m grid of quads of side 0.25 in the z = 0 plane, wound counterclockwise about +Z; vertex (i, j) is j * (n + 1) + i.</summary>
    public static PolygonMesh Grid(int n, int m)
    {
        var vertices = new List<Vector3>();
        for (var j = 0; j <= m; j++)
            for (var i = 0; i <= n; i++)
                vertices.Add(new Vector3(0.25f * i, 0.25f * j, 0));
        var faces = new List<int[]>();
        for (var j = 0; j < m; j++)
            for (var i = 0; i < n; i++)
            {
                var a = j * (n + 1) + i;
                faces.Add([a, a + 1, a + n + 2, a + n + 1]);
            }
        return Make(vertices, faces);
    }

    /// <summary>A strip of four 0.5 m quads, folded 90 degrees along its middle rung (x = 1): flat along +X, then up +Z.</summary>
    public static PolygonMesh BentStrip()
    {
        var vertices = new List<Vector3>();
        for (var k = 0; k <= 4; k++)
        {
            var p = k <= 2 ? new Vector3(0.5f * k, 0, 0) : new Vector3(1, 0, 0.5f * (k - 2));
            vertices.Add(p);
            vertices.Add(p + new Vector3(0, 0.5f, 0));
        }
        var faces = new List<int[]>();
        for (var k = 0; k < 4; k++) faces.Add([2 * k, 2 * k + 2, 2 * k + 3, 2 * k + 1]);
        return Make(vertices, faces);
    }

    /// <summary>
    /// The leaky shed: a gabled house with every fault once, for check and select to find. samples/leaky-shed.carve is
    /// this mesh, kept equal by a test. T-27 and T-28 build on these ids; T-23's As built lists them.
    /// Vertices 3 and 9 are not an edge: T-28's planted mistake.
    /// </summary>
    public static PolygonMesh LeakyShed()
    {
        Vector3[] vertices =
        [
            new(0, 0, 0), new(4, 0, 0), new(4, 0, 3), new(0, 0, 3), // 0-3 floor corners
            new(0, 2.55f, 0), // 4 lifted eave corner: the -Z wall's corner raised 0.05, which bends faces[11], the slope using it, out of plane
            new(4, 2.5f, 0), new(4, 2.5f, 3), new(0, 2.5f, 3), // 5-7 the other wall tops
            new(0, 3.5f, 1.5f), new(4, 3.5f, 1.5f), // 8-9 ridge
            new(4, 2.5f, 0), new(4, 2.5f, 3), // 10-11 unmerged seam: copies of 5 and 6, the two coincident-vertices problems
            new(2.5f, 0, 3), new(3.5f, 0, 3), new(3.5f, 2, 3), new(2.5f, 2, 3), // 12-15 door, off centre
            new(1, 3.6f, 1.3f), new(1.4f, 3.6f, 1.3f), new(1.4f, 3.9f, 1.3f), new(1, 3.9f, 1.3f), // 16-23 chimney, 0.1 above the ridge
            new(1, 3.6f, 1.7f), new(1.4f, 3.6f, 1.7f), new(1.4f, 3.9f, 1.7f), new(1, 3.9f, 1.7f),
            new(6, 0, 0), // 24 stray vertex: no face uses it
        ];

        int[][] house =
        [
            [0, 1, 2, 13, 12, 3], // 0 floor, its +Z side running along the door's foot
            [0, 4, 5, 1], // 1 -Z wall
            [4, 7, 3, 0], // 2 flipped wall: the -X wall's loop reversed, so edges 0-3, 0-4, 3-7 and 4-7 are crossed twice the same way
            [1, 5, 6, 2], // 3 +X wall, on the originals 5 and 6
            [3, 12, 15, 7], [15, 14, 6, 7], [13, 2, 6, 14], // 4-6 the +Z wall around the door
            [12, 13, 14, 15], // 7 door
            [12, 13, 14, 15], // 8 duplicated door: duplicate-face, and edges 12-13, 12-15, 13-14, 14-15 non-manifold and crossed twice the same way
            [4, 7, 8], // 9 -X gable
            [11, 10, 9], // 10 +X gable, on the copies 10 and 11, so edges 5-6 and 10-11 are the seam, open on both sides
            [4, 8, 9, 10], // 11 -Z roof slope, on the copy 10, so edges 4-5 and 4-10 are the seam along the eave
            // Missing roof face: no +Z slope, so edges 8-9 (ridge), 6-7 (eave), 7-8 and 9-11 (rakes) are open.
        ];

        // Inside-out chimney: faces 12-17, a closed box and a shell of its own with every loop reversed.
        return Make(vertices, house.Concat(CubeFaces.Select(f => f.Reverse().Select(i => i + 16).ToArray())));
    }

    /// <summary>What check finds in <see cref="LeakyShed"/>, as (check, element, message).</summary>
    public static readonly (string Check, string Element, string Message)[] LeakyShedProblems =
    [
        ("inside-out", "shells[1]", "closed shell of 6 faces (faces 12 to 17) is inside-out: its faces are wound clockwise seen from outside, so a game culls them and sees nothing"),
        ("coincident-vertices", "vertices[10]", "vertex is at the same position as vertices[5]"),
        ("coincident-vertices", "vertices[11]", "vertex is at the same position as vertices[6]"),
        ("duplicate-face", "faces[8]", "face has the same vertices as faces[7]"),
    ];

    private const string OpenEdge = "edge belongs to only one face";
    private const string SameWay = "two faces cross this edge in the same direction";
    private const string SharedByThree = "edge is shared by 3 faces";

    /// <summary>The warnings of <see cref="LeakyShed"/> as a client reads them, in <see cref="MeshReport"/>'s order: edges, faces, vertices.</summary>
    public static readonly string[] LeakyShedWarnings =
    [
        $"edges[0-3]: {SameWay}",
        $"edges[0-4]: {SameWay}",
        $"edges[3-7]: {SameWay}",
        $"edges[4-5]: {OpenEdge}",
        $"edges[4-7]: {SameWay}",
        $"edges[4-10]: {OpenEdge}",
        $"edges[5-6]: {OpenEdge}",
        $"edges[6-7]: {OpenEdge}",
        $"edges[7-8]: {OpenEdge}",
        $"edges[8-9]: {OpenEdge}",
        $"edges[9-11]: {OpenEdge}",
        $"edges[10-11]: {OpenEdge}",
        $"edges[12-13]: {SharedByThree}",
        $"edges[12-13]: {SameWay}",
        $"edges[12-15]: {SharedByThree}",
        $"edges[12-15]: {SameWay}",
        $"edges[13-14]: {SharedByThree}",
        $"edges[13-14]: {SameWay}",
        $"edges[14-15]: {SharedByThree}",
        $"edges[14-15]: {SameWay}",
        "faces[11]: face is not planar: a vertex is 0.0105 off its plane (limit 0.000439)",
        "vertices[24]: no face uses this vertex",
    ];

    /// <summary>The notes of <see cref="LeakyShed"/>.</summary>
    public static readonly string[] LeakyShedNotes =
    [
        "size 4 x 3.9 x 3 m",
        "base at y = 0",
        "origin inside the bounds",
        "2 separate shells; a model is usually one",
    ];

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
        var loop = mesh.Faces[face].Loop;
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
