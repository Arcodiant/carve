using System.Numerics;

namespace Carve.Core;

/// <summary>
/// The edit operations: move and extrude. Each returns a new mesh, by the id contract on <see cref="PolygonMesh"/>, and what
/// it made, or refuses with every problem named; the mesh given is never changed. Every result also passes
/// <see cref="PolygonMesh.Create"/>'s validation, which may refuse it by element.
/// </summary>
public static class MeshEdits
{
    /// <summary>
    /// Moves the union of <paramref name="vertices"/>, both ends of each of <paramref name="edges"/> and every corner of
    /// each of <paramref name="faces"/> by <paramref name="by"/>, each vertex once. Ids and face loops are unchanged. A face
    /// bent out of its plane is legal (<see cref="MeshReport"/> warns of it).
    /// </summary>
    public static EditResult Move(PolygonMesh mesh, IEnumerable<int>? vertices, IEnumerable<(int A, int B)>? edges,
        IEnumerable<int>? faces, Vector3 by)
    {
        var vertexList = vertices?.ToArray() ?? [];
        var edgeList = edges?.ToArray() ?? [];
        var faceList = faces?.ToArray() ?? [];
        var errors = new List<MeshError>();
        var moved = new SortedSet<int>();

        if (vertexList.Length == 0 && edgeList.Length == 0 && faceList.Length == 0)
            errors.Add(new MeshError("vertices, edges, faces", "nothing to move: list at least one vertex, edge or face"));
        if (!float.IsFinite(by.X) || !float.IsFinite(by.Y) || !float.IsFinite(by.Z))
            errors.Add(new MeshError("by", "the move is not finite"));
        else if (by == Vector3.Zero)
            errors.Add(new MeshError("by", "the move is zero: nothing would change"));

        for (var i = 0; i < vertexList.Length; i++)
        {
            var v = vertexList[i];
            if (v < 0 || v >= mesh.Vertices.Count)
                errors.Add(new MeshError($"vertices[{v}]", $"the {Ordinal(i)} listed vertex: there is no vertex {v}: ids run 0..{mesh.Vertices.Count - 1}"));
            else
                moved.Add(v);
        }

        for (var i = 0; i < edgeList.Length; i++)
        {
            var (a, b) = edgeList[i];
            if (!mesh.Topology.TryGetEdge(a, b, out var edge))
                errors.Add(new MeshError($"edges[{Math.Min(a, b)}-{Math.Max(a, b)}]", $"the {Ordinal(i)} listed edge, [{a}, {b}], is not an edge of the mesh"));
            else
            {
                moved.Add(edge.Lo);
                moved.Add(edge.Hi);
            }
        }

        for (var i = 0; i < faceList.Length; i++)
        {
            var f = faceList[i];
            if (!HasFace(mesh, f))
                errors.Add(UnknownFace(mesh, i, f));
            else
                moved.UnionWith(mesh.Faces[f]);
        }

        if (errors.Count > 0)
            return EditResult.Refused(errors);

        var result = mesh.Edit(movedVertices: moved.Select(v => KeyValuePair.Create(v, mesh.Vertices[v] + by)));
        return EditResult.From(result, moved.ToArray(), [], [], []);
    }

    /// <summary>
    /// Extrudes each of <paramref name="faces"/> separately (no region extrude). Each corner gets a new vertex, offset
    /// inward in the face's plane by <paramref name="inset"/> measured perpendicular to each edge (a mitred offset), then
    /// along the face's Newell normal by <paramref name="distance"/> (negative recesses). The face's loop becomes the new
    /// ring, so the cap keeps the face's id, and one quad wall per edge, <c>[old k, old k+1, new k+1, new k]</c>, is
    /// appended. New vertices and walls are appended face by face, in the order listed. A distance of 0 with an inset is a
    /// plain inset.
    /// </summary>
    public static EditResult Extrude(PolygonMesh mesh, IEnumerable<int> faces, float distance, float inset = 0)
    {
        var faceList = faces?.ToArray() ?? [];
        var errors = new List<MeshError>();

        if (faceList.Length == 0)
            errors.Add(new MeshError("faces", "nothing to extrude: list at least one face"));
        if (!float.IsFinite(distance))
            errors.Add(new MeshError("distance", "the distance is not finite"));
        if (!float.IsFinite(inset))
            errors.Add(new MeshError("inset", "the inset is not finite"));
        else if (inset < 0)
            errors.Add(new MeshError("inset", $"the inset is negative ({inset:G6}); it must be 0 or more"));
        if (distance == 0 && inset == 0)
            errors.Add(new MeshError("distance, inset", "distance and inset are both zero: nothing would change"));

        var firstListed = new Dictionary<int, int>();
        for (var i = 0; i < faceList.Length; i++)
        {
            var f = faceList[i];
            if (!HasFace(mesh, f))
                errors.Add(UnknownFace(mesh, i, f));
            else if (firstListed.TryGetValue(f, out var first))
                errors.Add(new MeshError($"faces[{f}]", $"the {Ordinal(i)} listed face is listed {Ordinal(first)} too: each face is extruded once"));
            else
                firstListed[f] = i;
        }

        var argumentsOk = errors.Count == 0;
        // A bad inset or distance is already named; with one, no face's ring is tried.
        var ringsCanBeTried = float.IsFinite(inset) && float.IsFinite(distance) && inset >= 0;
        var addedVertices = new List<Vector3>();
        var addedFaces = new List<int[]>();
        var reshaped = new List<KeyValuePair<int, IEnumerable<int>>>();
        var extruded = new List<ExtrudedFace>();

        for (var i = 0; i < faceList.Length; i++)
        {
            var f = faceList[i];
            if (!ringsCanBeTried || !HasFace(mesh, f) || firstListed[f] != i) continue;
            var loop = mesh.Faces[f];

            if (!TryRing(mesh.Vertices, loop, distance, inset, out var ring, out var why))
            {
                errors.Add(new MeshError($"faces[{f}]", $"the inset {inset:G6} is too large for this face: {why}"));
                continue;
            }
            if (!argumentsOk) continue;

            var firstVertex = mesh.Vertices.Count + addedVertices.Count;
            var firstWall = mesh.Faces.Count + addedFaces.Count;
            var n = loop.Count;
            var ids = Enumerable.Range(firstVertex, n).ToArray();
            addedVertices.AddRange(ring);
            for (var k = 0; k < n; k++)
                addedFaces.Add([loop[k], loop[(k + 1) % n], ids[(k + 1) % n], ids[k]]);
            reshaped.Add(KeyValuePair.Create(f, (IEnumerable<int>)ids));
            extruded.Add(new ExtrudedFace(f, f, Enumerable.Range(firstWall, n).ToArray(), ids));
        }

        if (errors.Count > 0)
            return EditResult.Refused(errors);

        var result = mesh.Edit(reshapedFaces: reshaped, addedVertices: addedVertices, addedFaces: addedFaces);
        return EditResult.From(result, [],
            Enumerable.Range(mesh.Vertices.Count, addedVertices.Count).ToArray(),
            Enumerable.Range(mesh.Faces.Count, addedFaces.Count).ToArray(),
            extruded);
    }

    /// <summary>
    /// The new ring for one face: each corner offset inward by <paramref name="inset"/> along the mitre of its two edges'
    /// inward perpendiculars, then along the unit Newell normal by <paramref name="distance"/>. Fails, saying why, when an
    /// offset edge reverses or vanishes or the inset outline crosses itself.
    /// </summary>
    private static bool TryRing(IReadOnlyList<Vector3> verts, IReadOnlyList<int> loop, float distance, float inset,
        out Vector3[] ring, out string why)
    {
        var n = loop.Count;
        var normal = PolygonMesh.Unit(PolygonMesh.NewellVector(verts, loop));
        var o = verts[loop[0]];
        var p = new (double X, double Y, double Z)[n];
        for (var k = 0; k < n; k++)
            p[k] = ((double)verts[loop[k]].X - o.X, (double)verts[loop[k]].Y - o.Y, (double)verts[loop[k]].Z - o.Z);

        // Inward perpendicular of edge k (corner k to k + 1): the normal crossed with the edge, for a loop wound
        // counter-clockwise about the normal.
        var inward = new (double X, double Y, double Z)[n];
        for (var k = 0; k < n; k++)
            inward[k] = PolygonMesh.Unit(Cross(normal, Sub(p[(k + 1) % n], p[k])));

        var offset = new (double X, double Y, double Z)[n];
        why = "";
        for (var k = 0; k < n; k++)
        {
            var before = inward[(k + n - 1) % n];
            var after = inward[k];
            var denominator = 1 + Dot(before, after);
            if (inset > 0 && denominator <= 0)
            {
                ring = [];
                why = $"its edges fold back at corner {k}, so the corner cannot be offset";
                return false;
            }
            var mitre = inset > 0 ? Scale(Add(before, after), inset / denominator) : (0, 0, 0);
            offset[k] = Add(p[k], mitre);
        }

        if (inset > 0)
        {
            for (var k = 0; k < n; k++)
            {
                var j = (k + 1) % n;
                if (Dot(Sub(offset[j], offset[k]), Sub(p[j], p[k])) <= 0)
                {
                    ring = [];
                    why = $"the offset of its edge from corner {k} to corner {j} collapses or runs backwards";
                    return false;
                }
            }
        }

        ring = new Vector3[n];
        for (var k = 0; k < n; k++)
        {
            var q = Add(offset[k], Scale(normal, distance));
            ring[k] = new Vector3((float)(o.X + q.X), (float)(o.Y + q.Y), (float)(o.Z + q.Z));
        }

        if (inset > 0 && Triangulator.FindCrossing(ring, Enumerable.Range(0, n).ToArray()) is not null)
        {
            why = "the inset outline crosses itself";
            return false;
        }
        return true;
    }

    private static bool HasFace(PolygonMesh mesh, int f) => f >= 0 && f < mesh.Faces.Count;

    private static MeshError UnknownFace(PolygonMesh mesh, int index, int f) =>
        new($"faces[{f}]", $"the {Ordinal(index)} listed face: there is no face {f}: ids run 0..{mesh.Faces.Count - 1}");

    private static string Ordinal(int index)
    {
        var n = index + 1;
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    private static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static (double X, double Y, double Z) Sub((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, double s) => (a.X * s, a.Y * s, a.Z * s);
    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
