using System.Globalization;
using System.Numerics;

namespace Carve.Core;

/// <summary>
/// The edit operations: move, extrude and subdivide. Each returns a new mesh, by the id contract on <see cref="PolygonMesh"/>, and what
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

        errors.AddRange(UnknownElements(mesh, vertexList, edgeList, faceList));
        moved.UnionWith(vertexList.Where(v => mesh.VertexProblem(v) is null));
        foreach (var (a, b) in edgeList)
        {
            if (mesh.Topology.TryGetEdge(a, b, out var edge))
            {
                moved.Add(edge.Lo);
                moved.Add(edge.Hi);
            }
        }

        foreach (var f in faceList.Where(f => HasFace(mesh, f)))
            moved.UnionWith(mesh.Faces[f].Corners);

        // A bad or missing by is a problem with the whole call, and with it no move is tried. Otherwise the listed ids that
        // exist are moved, so that what Create would refuse in the result is named beside any unknown id.
        if (errors.Any(e => e.Element is "by" or "vertices, edges, faces"))
            return EditResult.Refused(errors);

        var positions = moved.Select(v => KeyValuePair.Create(v, mesh.Vertices[v] + by)).ToArray();
        // by so small against the coordinates that it rounds away changes nothing, like a zero by.
        if (positions.Length > 0 && positions.All(p => p.Value.Equals(mesh.Vertices[p.Key])))
            errors.Insert(0, new MeshError("by", "the move is zero: nothing would change"));
        if (positions.Length == 0 || errors.Any(e => e.Element == "by"))
            return EditResult.Refused(errors);

        var result = mesh.Edit(movedVertices: positions);
        return errors.Count == 0
            ? EditResult.From(mesh, result, moved.ToArray(), [], [], [])
            : EditResult.Refused([.. errors, .. result.Errors]);
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
            errors.Add(new MeshError("inset", Invariant($"the inset is negative ({inset:G6}); it must be 0 or more")));
        if (distance == 0 && inset == 0)
            errors.Add(new MeshError("distance, inset", "distance and inset are both zero: nothing would change"));
        // A bad distance or inset, or no faces, is a problem with the whole call: no face is tried. Otherwise each listed
        // face that exists is tried, so that a ring or a wall that fails on one face is named beside an unknown id.
        var argumentsOk = errors.Count == 0;

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

        // A bad inset or distance is already named; with one, no face's ring is tried.
        var ringsCanBeTried = float.IsFinite(inset) && float.IsFinite(distance) && inset >= 0;
        var addedVertices = new List<Vector3>();
        var addedFaces = new List<Face>();
        var reshaped = new List<KeyValuePair<int, Face>>();
        var extruded = new List<ExtrudedFace>();

        for (var i = 0; i < faceList.Length; i++)
        {
            var f = faceList[i];
            if (!ringsCanBeTried || !HasFace(mesh, f) || firstListed[f] != i) continue;
            var face = mesh.Faces[f];
            var loop = face.Loop;
            if (face.HasHoles && inset > 0)
            {
                errors.Add(new MeshError($"faces[{f}]", Invariant($"the inset {inset:G6} cannot be taken on this face: it has holes, and a holed face extrudes only straight, as a tube (inset 0)")));
                continue;
            }

            if (!TryRing(mesh.Vertices, loop, distance, inset, out var ring, out var why))
            {
                errors.Add(new MeshError($"faces[{f}]", Invariant($"the inset {inset:G6} is too large for this face: {why}")));
                continue;
            }
            if (!argumentsOk) continue;

            var firstVertex = mesh.Vertices.Count + addedVertices.Count;
            var firstWall = mesh.Faces.Count + addedFaces.Count;
            var n = face.Corners.Count;
            var ids = Enumerable.Range(firstVertex, n).ToArray();
            addedVertices.AddRange(ring);
            // A hole's corners are lifted along the face's (outer loop's) normal, as TryRing lifts the outer loop's.
            addedVertices.AddRange(Lift(mesh.Vertices, loop, face.Corners.Skip(loop.Count), distance));
            var hole = face.Holes.Select((h, k) => ids.Skip(face.CornerAt(k, 0)).Take(h.Count).ToArray()).ToArray();
            for (var h = -1; h < face.Holes.Count; h++)
            {
                // One wall per edge, old k to k + 1 then new k + 1 to new k; a hole runs clockwise, so its walls face into the hole.
                var old = face.LoopAt(h);
                var lifted = h < 0 ? ids[..loop.Count] : hole[h];
                for (var k = 0; k < old.Count; k++)
                    addedFaces.Add(new[] { old[k], old[(k + 1) % old.Count], lifted[(k + 1) % old.Count], lifted[k] });
            }
            reshaped.Add(KeyValuePair.Create(f, new Face(ids[..loop.Count], hole)));
            extruded.Add(new ExtrudedFace(f, f, Enumerable.Range(firstWall, n).ToArray(), ids));
        }

        if (!argumentsOk || extruded.Count == 0)
            return EditResult.Refused(errors);

        var result = mesh.Edit(reshapedFaces: reshaped, addedVertices: addedVertices, addedFaces: addedFaces);
        if (errors.Count > 0 || !result.Succeeded)
            return EditResult.Refused([.. errors, .. result.Errors.Select(e => ToSourceFace(mesh, extruded, e, distance, inset))]);
        return EditResult.From(mesh, result, [],
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
            // Defensive, and unreachable today: it needs two adjacent edges running exactly opposite, and Create already
            // refuses such a face (a fold for 4 or more corners, zero area for a triangle). No test covers it; it is kept
            // so the mitre below never divides by zero should that validation change.
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

    /// <summary>
    /// Names an error on a wall or ring vertex the extrude appended, which the caller has never seen, by the listed face it
    /// was made for, and says that the distance (or, with distance 0, the inset) did not suit that face. Any other error
    /// is returned as it is.
    /// </summary>
    private static MeshError ToSourceFace(PolygonMesh mesh, List<ExtrudedFace> extruded, MeshError error, float distance, float inset)
    {
        foreach (var x in extruded)
        {
            string? what = null;
            int? wallEdge = null;
            for (var k = 0; k < x.Walls.Count && what is null; k++)
            {
                if (error.Element == $"faces[{x.Walls[k]}]")
                {
                    wallEdge = k;
                    what = $"its new wall faces[{x.Walls[k]}], over the edge from {CornerText(mesh.Faces[x.Face], k)} to {CornerText(mesh.Faces[x.Face], NextCorner(mesh.Faces[x.Face], k))}";
                }
            }
            for (var k = 0; k < x.NewVertices.Count && what is null; k++)
            {
                if (error.Element == $"vertices[{x.NewVertices[k]}]")
                    what = $"its new vertex vertices[{x.NewVertices[k]}], over {CornerText(mesh.Faces[x.Face], k)}";
            }
            if (what is null) continue;

            // A wall is a strip one edge long and the step wide, so the step is small or large against that wall's own edge.
            // A ring vertex is refused for a non-finite coordinate, which is always large.
            var (name, value) = distance != 0 ? ("distance", distance) : ("inset", inset);
            var scale = "large";
            if (wallEdge is int wall)
            {
                var face = mesh.Faces[x.Face];
                var p = mesh.Vertices[face.Corners[wall]];
                var q = mesh.Vertices[face.Neighbours(wall).Next];
                double dx = (double)q.X - p.X, dy = (double)q.Y - p.Y, dz = (double)q.Z - p.Z;
                var edge = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                scale = Math.Abs((double)value) <= edge ? "small" : "large";
            }
            return new MeshError($"faces[{x.Face}]",
                Invariant($"the {name} {value:G6} is too {scale} for this face: {what}, is refused: {error.Message}"));
        }
        return error;
    }

    /// <summary><c>corner k</c> for an outer corner, as before holes; <c>corner k of hole h</c> for a hole's.</summary>
    private static string CornerText(Face face, int corner)
    {
        var (hole, i) = face.Locate(corner);
        return hole < 0 ? Invariant($"corner {i}") : Invariant($"corner {i} of hole {hole}");
    }

    /// <summary>The corner after <paramref name="corner"/> in its own loop.</summary>
    private static int NextCorner(Face face, int corner)
    {
        var (hole, i) = face.Locate(corner);
        return face.CornerAt(hole, (i + 1) % face.LoopAt(hole).Count);
    }

    /// <summary>
    /// The corners <paramref name="ids"/> moved <paramref name="distance"/> along the unit Newell normal of
    /// <paramref name="loop"/>, worked relative to the loop's first vertex in double, as <see cref="TryRing"/> works its ring.
    /// </summary>
    private static IEnumerable<Vector3> Lift(IReadOnlyList<Vector3> verts, IReadOnlyList<int> loop, IEnumerable<int> ids, float distance)
    {
        var normal = PolygonMesh.Unit(PolygonMesh.NewellVector(verts, loop));
        var o = verts[loop[0]];
        foreach (var i in ids)
        {
            var q = Add(((double)verts[i].X - o.X, (double)verts[i].Y - o.Y, (double)verts[i].Z - o.Z), Scale(normal, distance));
            yield return new Vector3((float)(o.X + q.X), (float)(o.Y + q.Y), (float)(o.Z + q.Z));
        }
    }

    /// <summary>The most levels one <see cref="Subdivide"/> takes.</summary>
    public const int MaxSubdivideLevels = 3;

    /// <summary>
    /// Catmull-Clark subdivision, <paramref name="levels"/> times (1 to <see cref="MaxSubdivideLevels"/>), as one edit
    /// (D-6, Subdivide is a destructive edit), by the id contract on <see cref="PolygonMesh"/>: every vertex keeps its id
    /// and moves to its vertex point, each face keeps its id as its first child, and face points, edge points and the
    /// other children are appended (see <see cref="Subdivision"/> for the order and the sharp-edge rules). Hard edges stay
    /// creased and are carried to both halves; children take their face's colour and groups; weights are blended by
    /// each point's own stencil and capped at four. Two levels are exactly one level done twice.
    /// </summary>
    public static EditResult Subdivide(PolygonMesh mesh, int levels)
    {
        if (levels < 1 || levels > MaxSubdivideLevels)
            return EditResult.Refused([new MeshError("levels", Invariant($"levels is {levels}; it must be 1 to {MaxSubdivideLevels}"))]);

        // Catmull-Clark of a face with holes has no one answer (a hole's face point, its quads), so it is refused by face.
        var holed = Enumerable.Range(0, mesh.Faces.Count).Where(f => mesh.Faces[f].HasHoles).ToArray();
        if (holed.Length > 0)
            return EditResult.Refused(holed.Select(f => new MeshError($"faces[{f}]",
                "this face has holes, and subdivide does not take a face with holes")).ToList());

        var level = new Subdivision.Level(mesh.Vertices.ToArray(), mesh.Faces.Select(f => f.Loop.ToArray()).ToArray(),
            mesh.Style, mesh.Weights, mesh.Shade);
        for (var i = 0; i < levels; i++) level = Subdivision.Once(level);

        // Tombstones stay tombstones: a deleted face's slot, and the face point each level makes for it, which no face uses.
        var used = new bool[level.Vertices.Length];
        foreach (var f in level.Faces) foreach (var v in f) used[v] = true;
        var gone = Enumerable.Range(0, level.Vertices.Length)
            .Where(v => mesh.IsVertexDeleted(v) || (v >= mesh.Vertices.Count && !used[v])).ToArray();
        var result = PolygonMesh.Create(level.Vertices, level.Faces.Select(f => f.Length == 0 ? Face.Deleted : (Face)f), gone);
        if (!result.Succeeded) return EditResult.Refused(result.Errors);
        var made = result.Mesh!.WithStyle(level.Style).WithWeights(level.Weights).WithShade(level.Shade);
        var moved = mesh.LiveVertices.Where(v => made.Vertices[v] != mesh.Vertices[v]).ToArray();
        return EditResult.Made(made, moved,
            Enumerable.Range(mesh.Vertices.Count, made.Vertices.Count - mesh.Vertices.Count).Where(v => !made.IsVertexDeleted(v)).ToArray(),
            Enumerable.Range(mesh.Faces.Count, made.Faces.Count - mesh.Faces.Count).ToArray());
    }

    /// <summary>Each listed element the mesh does not have, named by element and by its place in its list: what <c>move</c> and <c>render</c>'s highlight refuse.</summary>
    public static List<MeshError> UnknownElements(
        PolygonMesh mesh, IReadOnlyCollection<int> vertices, IReadOnlyCollection<(int A, int B)> edges, IReadOnlyCollection<int> faces)
    {
        var errors = new List<MeshError>();
        foreach (var (v, i) in vertices.Select((v, i) => (v, i)))
        {
            if (v < 0 || v >= mesh.Vertices.Count)
                errors.Add(new MeshError(Invariant($"vertices[{v}]"), Invariant($"the {Ordinal(i)} listed vertex: there is no vertex {v}: ids run 0..{mesh.Vertices.Count - 1}")));
            else if (mesh.IsVertexDeleted(v))
                errors.Add(new MeshError(Invariant($"vertices[{v}]"), PolygonMesh.DeletedMessage));
        }

        foreach (var ((a, b), i) in edges.Select((e, i) => (e, i)))
        {
            if (!mesh.Topology.TryGetEdge(a, b, out _))
                errors.Add(new MeshError(Invariant($"edges[{Math.Min(a, b)}-{Math.Max(a, b)}]"), Invariant($"the {Ordinal(i)} listed edge, [{a}, {b}], is not an edge of the mesh")));
        }

        foreach (var (f, i) in faces.Select((f, i) => (f, i)))
        {
            if (!HasFace(mesh, f))
                errors.Add(UnknownFace(mesh, i, f));
        }

        return errors;
    }

    /// <summary>Whether <paramref name="f"/> names a live face: an id out of range or a tombstone does not.</summary>
    private static bool HasFace(PolygonMesh mesh, int f) => mesh.FaceProblem(f) is null;

    private static MeshError UnknownFace(PolygonMesh mesh, int index, int f) =>
        mesh.IsFaceDeleted(f)
            ? new(Invariant($"faces[{f}]"), PolygonMesh.DeletedMessage)
            : new(Invariant($"faces[{f}]"), Invariant($"the {Ordinal(index)} listed face: there is no face {f}: ids run 0..{mesh.Faces.Count - 1}"));

    /// <summary>Agent-facing text is the same on every host: numbers (a negative id's sign among them) in the invariant culture.</summary>
    internal static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    internal static string Ordinal(int index)
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
