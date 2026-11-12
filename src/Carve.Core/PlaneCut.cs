using System.Numerics;
using static Carve.Core.FaceSplit;

namespace Carve.Core;

/// <summary>Which side of the plane a <see cref="PlaneCut"/> keeps: both (only split), or the side the normal points to
/// (<see cref="Above"/>) or the other (<see cref="Below"/>).</summary>
public enum PlaneKeep
{
    Both,
    Above,
    Below,
}

/// <summary>
/// What <see cref="PlaneCut.Cut"/> made: the edit; the warnings (a cut loop left open, naming its ends); and the cut
/// edges, each <c>(lo, hi)</c> ascending (the split's segments for <c>keep: both</c>, the kept side's border on the plane
/// otherwise).
/// </summary>
public sealed record PlaneCutResult(EditResult Edit, IReadOnlyList<string> Warnings, IReadOnlyList<(int Lo, int Hi)> CutEdges);

/// <summary>
/// <c>plane_cut</c> (T-65): splits faces along a plane on <see cref="FaceSplit"/>, keeping both sides, or one side with
/// every face wholly on the other deleted (T-64's <see cref="MeshDelete.Closure"/>) and the cut capped.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A vertex within <see cref="FaceSplit.Tolerance"/> of the plane is on it. Each face is cut along the line where
/// the plane meets it: its crossings (corners on the plane, and edges whose ends are strictly on opposite sides) sorted
/// along that line, each gap between consecutive crossings whose middle is inside the face (not on its boundary) one
/// <see cref="FaceCut"/>. A face with every corner on the plane is not split.</item>
/// <item>With <see cref="PlaneKeep.Above"/> or <see cref="PlaneKeep.Below"/>, each face of the split mesh is on one side
/// (every corner on that side or on the plane; a face in the plane goes with the side its normal points to), and every
/// face on the other side is deleted with what <see cref="MeshDelete.Closure"/> takes.</item>
/// <item>The fill: the edges between a kept face and a deleted one, each taken the other way round, chain into loops;
/// each closed loop is a cap wound outward on the kept side, a loop wound the other way a hole of the smallest cap around
/// it (T-63). A chain that does not close (an open mesh) is left open, with a warning naming its ends. On a smooth mesh
/// the caps' border edges are hard.</item>
/// </list>
/// Refused: a non-finite point, a zero or non-finite normal, a cut limited to <c>faces</c> that keeps one side, an
/// unknown or deleted face, a plane that touches no face, and a cut that would leave nothing.
/// </remarks>
public static class PlaneCut
{
    public const string NormalMessage = "must be a finite, non-zero vector";
    public const string LimitedKeepMessage = "a cut limited to some faces keeps both sides: leave faces out to keep one side";
    public const string TouchesNoFaceMessage = "the plane touches no face";

    /// <summary>Cuts <paramref name="mesh"/> with the plane through <paramref name="point"/> with <paramref name="normal"/> (see <see cref="PlaneCut"/>).</summary>
    public static PlaneCutResult Cut(PolygonMesh mesh, Vector3 point, Vector3 normal, PlaneKeep keep = PlaneKeep.Both,
        bool fill = true, IReadOnlyCollection<int>? faces = null)
    {
        var errors = new List<MeshError>();
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
            errors.Add(new MeshError("point", "must be a finite point"));
        var nd = D(normal);
        var length = Math.Sqrt(Dot(nd, nd));
        if (!double.IsFinite(length) || length == 0) errors.Add(new MeshError("normal", NormalMessage));
        if (faces is not null && keep != PlaneKeep.Both) errors.Add(new MeshError("faces", LimitedKeepMessage));
        if (faces is not null && faces.Count == 0) errors.Add(new MeshError("faces", "list at least one face, or leave faces out to cut every face"));
        if (faces is not null) errors.AddRange(MeshEdits.UnknownElements(mesh, [], [], faces));
        if (errors.Count > 0) return Refused(errors);

        var n = nd * (1 / length);
        var origin = D(point);
        var tol = Tolerance(mesh);
        var targets = faces is null ? mesh.LiveFaces.ToArray() : faces.Distinct().Order().ToArray();
        double Dist(PolygonMesh m, int v) => Dot(D(m.Vertices[v]) - origin, n);

        if (!targets.Any(f => mesh.Faces[f].Corners.Any(c => Math.Abs(Dist(mesh, c)) <= tol)
                              || (mesh.Faces[f].Corners.Any(c => Dist(mesh, c) > tol) && mesh.Faces[f].Corners.Any(c => Dist(mesh, c) < -tol))))
            return Refused([new MeshError("point, normal", TouchesNoFaceMessage)]);

        var cuts = new List<FaceCut>();
        foreach (var f in targets) cuts.AddRange(Crossings(mesh, f, v => Dist(mesh, v), n, tol));

        EditResult split;
        var segments = new List<(int, int)>();
        if (cuts.Count > 0)
        {
            var r = FaceSplit.Split(mesh, cuts);
            if (!r.Edit.Succeeded) return new PlaneCutResult(r.Edit, [], []);
            split = r.Edit;
            segments.AddRange(r.Segments.Where(s => s.From != s.To).Select(s => s.From < s.To ? (s.From, s.To) : (s.To, s.From)));
        }
        else split = EditResult.Made(mesh, [], [], []);

        if (keep == PlaneKeep.Both)
            return new PlaneCutResult(split, [], [.. segments.Distinct().Order()]);

        // Keeping one side: every face of the split mesh on the other side goes.
        var cut = split.Mesh!;
        var above = keep == PlaneKeep.Above;
        var gone = new List<int>();
        foreach (var f in cut.LiveFaces)
            if (IsAbove(cut, f, v => Dist(cut, v), n, tol) != above) gone.Add(f);
        if (gone.Count == cut.LiveFaceCount)
            return Refused([new MeshError("keep", MeshEdits.Invariant($"this would leave nothing: every face is {(above ? "below" : "above")} the plane"))]);
        var deleted = MeshDelete.Closure(cut, [], gone);

        // The kept side's border on the plane: each kept face's edge a deleted face has the other way round.
        var dead = deleted.Faces.ToHashSet();
        var deadEdges = new HashSet<(int, int)>();
        foreach (var f in dead) foreach (var (a, b) in Edges(cut.Faces[f])) deadEdges.Add((a, b));
        var border = new List<(int A, int B)>();
        foreach (var f in cut.LiveFaces)
            if (!dead.Contains(f))
                foreach (var (a, b) in Edges(cut.Faces[f]))
                    if (deadEdges.Contains((b, a)) && Math.Abs(Dist(cut, a)) <= tol && Math.Abs(Dist(cut, b)) <= tol)
                        border.Add((b, a));
        var cutEdges = border.Select(e => e.A < e.B ? (e.A, e.B) : (e.B, e.A)).Distinct().Order().ToList();

        var warnings = new List<string>();
        var caps = fill ? Caps(cut, border, above ? n * -1 : n, warnings) : [];

        var result = cut.Edit(addedFaces: caps, deletedVertices: deleted.Vertices, deletedFaces: deleted.Faces);
        var newFaceIds = Enumerable.Range(cut.Faces.Count, caps.Count).ToArray();
        var made = EditResult.From(cut, result, [], [], newFaceIds, [], deleted);
        if (!made.Succeeded) return new PlaneCutResult(made, [], []);
        var final = made.Mesh!;
        if (caps.Count > 0 && final.Shade is { Mode: ShadingMode.Smooth } shade)
            final = final.WithShade(shade.WithEdges(caps.SelectMany(Edges), true));

        var newVertices = split.NewVertices.Where(v => !final.IsVertexDeleted(v)).ToArray();
        var newFaces = split.NewFaces.Concat(newFaceIds).Where(f => !final.IsFaceDeleted(f)).ToArray();
        return new PlaneCutResult(EditResult.Made(final, [], newVertices, newFaces, deleted, split.Split), warnings, cutEdges);
    }

    private static PlaneCutResult Refused(IReadOnlyList<MeshError> errors) => new(EditResult.Refused(errors), [], []);

    private static IEnumerable<(int A, int B)> Edges(Face face)
    {
        foreach (var loop in face.Holes.Prepend(face.Loop))
            for (var i = 0; i < loop.Count; i++) yield return (loop[i], loop[(i + 1) % loop.Count]);
    }

    /// <summary>Whether face <paramref name="f"/> is on the side the normal points to: every corner on that side or on
    /// the plane, or, for a face in the plane, its normal that way.</summary>
    private static bool IsAbove(PolygonMesh mesh, int f, Func<int, double> dist, Vector3D n, double tol)
    {
        var d = mesh.Faces[f].Corners.Select(dist).ToArray();
        var min = d.Min();
        var max = d.Max();
        if (max <= tol && min >= -tol) return Dot(D(mesh.FaceNormal(f)), n) > 0;
        if (min >= -tol) return true;
        if (max <= tol) return false;
        return d.Sum() > 0;
    }

    /// <summary>The segments of the plane's line across face <paramref name="f"/>.</summary>
    private static List<FaceCut> Crossings(PolygonMesh mesh, int f, Func<int, double> dist, Vector3D n, double tol)
    {
        var face = mesh.Faces[f];
        var cuts = new List<FaceCut>();
        if (face.Corners.All(c => Math.Abs(dist(c)) <= tol)) return cuts;

        var points = new List<(CutPoint Point, Vector3D At)>();
        foreach (var (a, b) in Edges(face))
        {
            var (da, db) = (dist(a), dist(b));
            if (Math.Abs(da) <= tol)
            {
                if (!points.Any(p => p.Point.IsVertex && p.Point.A == a)) points.Add((CutPoint.AtVertex(a), D(mesh.Vertices[a])));
            }
            else if ((da > tol && db < -tol) || (da < -tol && db > tol))
            {
                var t = da / (da - db);
                var (pa, pb) = (D(mesh.Vertices[a]), D(mesh.Vertices[b]));
                points.Add((CutPoint.OnEdge(a, b, t), pa + (pb - pa) * t));
            }
        }
        if (points.Count < 2) return cuts;

        var fn = PolygonMesh.Unit(PolygonMesh.NewellVector(mesh.Vertices, face.Loop));
        var faceNormal = new Vector3D(fn.X, fn.Y, fn.Z);
        var along = Cross(faceNormal, n);
        if (Dot(along, along) < 1e-24) return cuts;
        points.Sort((p, q) => Dot(p.At, along).CompareTo(Dot(q.At, along)));

        var (u, v) = Basis(fn);
        var o = D(mesh.Vertices[face.Loop[0]]);
        Vector2D Flat(Vector3D x) => new(Dot(x - o, u), Dot(x - o, v));
        var loops = face.Holes.Prepend(face.Loop).ToArray();
        var verts = mesh.Vertices;
        for (var i = 0; i + 1 < points.Count; i++)
        {
            var (p, q) = (points[i], points[i + 1]);
            var gap = q.At - p.At;
            if (Math.Sqrt(Dot(gap, gap)) <= tol) continue;
            var mid = (p.At + q.At) * 0.5;
            if (Edges(face).Any(e => SegmentDistance(mid, D(verts[e.A]), D(verts[e.B])) <= tol)) continue;
            if (!Inside(loops, c => Flat(D(verts[c])), Flat(mid))) continue;
            cuts.Add(new FaceCut(f, p.Point, q.Point));
        }
        return cuts;
    }

    /// <summary>The caps of the closed loops <paramref name="border"/> chains into, wound about <paramref name="outward"/>;
    /// each open chain is a warning.</summary>
    private static List<Face> Caps(PolygonMesh mesh, List<(int A, int B)> border, Vector3D outward, List<string> warnings)
    {
        var next = new SortedDictionary<int, List<int>>();
        var incoming = new Dictionary<int, int>();
        foreach (var (a, b) in border.Distinct().Order())
        {
            if (!next.TryGetValue(a, out var list)) next[a] = list = [];
            list.Add(b);
            incoming[b] = incoming.GetValueOrDefault(b) + 1;
        }

        List<int> Trace(int start)
        {
            var chain = new List<int> { start };
            var at = start;
            while (next.TryGetValue(at, out var outs) && outs.Count > 0)
            {
                at = outs[0];
                outs.RemoveAt(0);
                if (at == start) return chain;
                chain.Add(at);
            }
            chain.Add(-1);
            return chain;
        }

        var closed = new List<List<int>>();
        // Open chains first, from each vertex with more edges out than in.
        foreach (var start in next.Keys.ToArray())
            while (next[start].Count > incoming.GetValueOrDefault(start))
            {
                var chain = Trace(start);
                if (chain[^1] != -1) { closed.Add(chain); continue; }
                chain.RemoveAt(chain.Count - 1);
                warnings.Add(OpenMessage(chain));
                foreach (var w in chain.Skip(1)) incoming[w]--;
            }
        foreach (var start in next.Keys.ToArray())
            while (next[start].Count > 0)
            {
                var chain = Trace(start);
                if (chain[^1] == -1)
                {
                    chain.RemoveAt(chain.Count - 1);
                    warnings.Add(OpenMessage(chain));
                }
                else if (chain.Count >= 3) closed.Add(chain);
            }

        var (u, v) = Basis((outward.X, outward.Y, outward.Z));
        Vector2D Flat(int c) { var x = D(mesh.Vertices[c]); return new(Dot(x, u), Dot(x, v)); }
        var outlines = closed.Where(l => SignedArea(l, Flat) > 0).OrderBy(l => l.Min()).ToList();
        var holes = closed.Where(l => SignedArea(l, Flat) <= 0).OrderBy(l => l.Min()).ToList();
        var owned = outlines.Select(_ => new List<List<int>>()).ToList();
        foreach (var hole in holes)
        {
            var owner = -1;
            for (var k = 0; k < outlines.Count; k++)
                if (InPolygon(outlines[k], Flat, Flat(hole[0]))
                    && (owner < 0 || Math.Abs(SignedArea(outlines[k], Flat)) < Math.Abs(SignedArea(outlines[owner], Flat))))
                    owner = k;
            if (owner < 0)
                warnings.Add(MeshEdits.Invariant($"fill: the loop through vertex {hole.Min()} lies in no cap, so it is left open"));
            else owned[owner].Add(hole);
        }
        return [.. outlines.Select((l, k) => new Face(Lowest(l), owned[k].Select(Lowest)))];
    }

    /// <summary>The warning for a chain of cut edges that does not close.</summary>
    public static string OpenMessage(IReadOnlyList<int> chain) => MeshEdits.Invariant(
        $"fill: the cut from vertex {chain[0]} to vertex {chain[^1]} is not a closed loop (the mesh is open there), so it is left open");

    /// <summary>A loop rotated to start at its lowest vertex id.</summary>
    private static int[] Lowest(List<int> loop)
    {
        var start = loop.IndexOf(loop.Min());
        return [.. loop.Skip(start), .. loop.Take(start)];
    }
}
