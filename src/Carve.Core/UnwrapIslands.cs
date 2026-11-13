namespace Carve.Core;

/// <summary>
/// One island before it is flattened: its faces (ascending), its vertices (a mesh vertex is one island vertex per
/// group of its corners joined across uncut edges, so a seam vertex is two), its triangles over those vertices, wound
/// as their faces, and which vertices lie on its boundary.
/// </summary>
internal sealed class UnwrapPatch
{
    public required int[] Faces { get; init; }

    /// <summary>The mesh vertex of each island vertex, numbered by first appearance (faces ascending, corners in <see cref="Face.Corners"/> order).</summary>
    public required List<int> MeshVertex { get; init; }

    /// <summary>Per face of <see cref="Faces"/>, the island vertex of each of its corners.</summary>
    public required int[][] Corners { get; init; }

    public required List<(int A, int B, int C)> Triangles { get; init; }

    public required bool[] OnBoundary { get; init; }

    public int LowestFace => Faces[0];
}

/// <summary>
/// Cuts the chosen faces into islands (T-56). Faces are joined across an edge used by exactly two of them, in opposite
/// directions, that is not a seam; every other edge (open, seam, mis-wound, shared by three or more faces, or on the
/// border between the set and the rest) is cut. Each island must then be a disc: one boundary loop, besides any loops
/// that are a single face's hole loop (ruling 1), and an Euler characteristic to match.
/// </summary>
internal static class UnwrapIslands
{
    public static List<UnwrapPatch> Find(PolygonMesh mesh, IReadOnlyList<int> set, List<MeshError> errors)
    {
        var faceCount = mesh.Faces.Count;
        var inSet = new bool[faceCount];
        foreach (var f in set) inSet[f] = true;

        // Corners numbered globally, face by face.
        var offset = new int[faceCount + 1];
        for (var f = 0; f < faceCount; f++) offset[f + 1] = offset[f] + (inSet[f] ? mesh.Faces[f].Corners.Count : 0);

        var faceRoot = new int[faceCount];
        for (var f = 0; f < faceCount; f++) faceRoot[f] = f;
        var cornerRoot = new int[offset[faceCount]];
        for (var c = 0; c < cornerRoot.Length; c++) cornerRoot[c] = c;

        var joined = new HashSet<(int, int)>();
        var seams = mesh.Uvs;
        foreach (var edge in mesh.Topology.Edges)
        {
            if (edge.Faces.Count != 2 || !edge.IsManifold) continue;
            int f1 = edge.Faces[0].Face, f2 = edge.Faces[1].Face;
            if (f1 == f2 || !inSet[f1] || !inSet[f2]) continue;
            if (seams is not null && seams.IsSeam(edge.Lo, edge.Hi)) continue;
            joined.Add((edge.Lo, edge.Hi));
            Union(faceRoot, f1, f2);
            foreach (var v in (ReadOnlySpan<int>)[edge.Lo, edge.Hi])
                Union(cornerRoot, offset[f1] + mesh.Faces[f1].CornerOf(v), offset[f2] + mesh.Faces[f2].CornerOf(v));
        }

        // Islands in order of their lowest face.
        var byRoot = new Dictionary<int, List<int>>();
        var order = new List<List<int>>();
        foreach (var f in set)
        {
            var r = Find(faceRoot, f);
            if (!byRoot.TryGetValue(r, out var list)) { list = []; byRoot[r] = list; order.Add(list); }
            list.Add(f);
        }

        var patches = new List<UnwrapPatch>();
        foreach (var faces in order)
        {
            var patch = Build(mesh, [.. faces], offset, cornerRoot, joined, errors);
            if (patch is not null) patches.Add(patch);
        }
        return patches;
    }

    private static UnwrapPatch? Build(PolygonMesh mesh, int[] faces, int[] offset, int[] cornerRoot, HashSet<(int, int)> joined, List<MeshError> errors)
    {
        var vertexOf = new Dictionary<int, int>(); // corner root -> island vertex
        var meshVertex = new List<int>();
        var corners = new int[faces.Length][];
        for (var i = 0; i < faces.Length; i++)
        {
            var face = mesh.Faces[faces[i]];
            corners[i] = new int[face.Corners.Count];
            for (var c = 0; c < face.Corners.Count; c++)
            {
                var root = Find(cornerRoot, offset[faces[i]] + c);
                if (!vertexOf.TryGetValue(root, out var iv))
                {
                    iv = meshVertex.Count;
                    vertexOf[root] = iv;
                    meshVertex.Add(face.Corners[c]);
                }
                corners[i][c] = iv;
            }
        }

        var triangles = new List<(int A, int B, int C)>();
        for (var i = 0; i < faces.Length; i++)
        {
            var face = mesh.Faces[faces[i]];
            foreach (var t in Triangulator.TriangulateFace(mesh, faces[i]))
                triangles.Add((corners[i][face.CornerOf(t.A)], corners[i][face.CornerOf(t.B)], corners[i][face.CornerOf(t.C)]));
        }

        // Boundary half-edges: every loop edge of a face that is not joined, tagged with its face and loop.
        var onBoundary = new bool[meshVertex.Count];
        var loopRoot = new int[meshVertex.Count];
        for (var v = 0; v < loopRoot.Length; v++) loopRoot[v] = v;
        var halfEdges = new List<(int From, int Face, int Hole)>();
        for (var i = 0; i < faces.Length; i++)
        {
            var face = mesh.Faces[faces[i]];
            for (var hole = -1; hole < face.Holes.Count; hole++)
            {
                var loop = face.LoopAt(hole);
                for (var k = 0; k < loop.Count; k++)
                {
                    int a = loop[k], b = loop[(k + 1) % loop.Count];
                    if (joined.Contains((Math.Min(a, b), Math.Max(a, b)))) continue;
                    int ia = corners[i][face.CornerOf(a)], ib = corners[i][face.CornerOf(b)];
                    onBoundary[ia] = onBoundary[ib] = true;
                    Union(loopRoot, ia, ib);
                    halfEdges.Add((ia, faces[i], hole));
                }
            }
        }

        // Loops: the connected pieces of the boundary. A piece made of exactly one face's hole loop is allowed.
        var pieces = new Dictionary<int, (int Count, int Face, int Hole, bool Same)>();
        foreach (var (from, f, hole) in halfEdges)
        {
            var r = Find(loopRoot, from);
            pieces[r] = pieces.TryGetValue(r, out var p)
                ? (p.Count + 1, p.Face, p.Hole, p.Same && p.Face == f && p.Hole == hole)
                : (1, f, hole, true);
        }
        var loops = pieces.Count;
        var holeLoops = 0;
        foreach (var p in pieces.Values)
            if (p.Same && p.Hole >= 0 && p.Count == mesh.Faces[p.Face].Holes[p.Hole].Count) holeLoops++;

        // Euler characteristic of the cut island's triangulation: 3T = 2E_inner + E_boundary.
        var sides = 3 * triangles.Count + halfEdges.Count;
        var chi = sides % 2 == 0 ? meshVertex.Count - sides / 2 + triangles.Count : int.MinValue;
        if (loops - holeLoops != 1 || chi != 1 - holeLoops)
        {
            errors.Add(new MeshError($"faces[{faces[0]}]",
                $"an island of {Plural(faces.Length, "face")} with {Plural(loops, "boundary loop")} is not a disc; mark a seam across it with mark_edges, cutting every closed loop until each island is a disc"));
            return null;
        }

        return new UnwrapPatch { Faces = faces, MeshVertex = meshVertex, Corners = corners, Triangles = triangles, OnBoundary = onBoundary };
    }

    internal static string Plural(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";

    private static int Find(int[] root, int x)
    {
        while (root[x] != x)
        {
            root[x] = root[root[x]];
            x = root[x];
        }
        return x;
    }

    /// <summary>Joins two sets; the lower root wins, so a face island's root is its lowest face.</summary>
    private static void Union(int[] root, int a, int b)
    {
        int ra = Find(root, a), rb = Find(root, b);
        if (ra == rb) return;
        if (ra < rb) root[rb] = ra;
        else root[ra] = rb;
    }
}
