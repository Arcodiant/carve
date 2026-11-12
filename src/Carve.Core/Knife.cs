using System.Numerics;
using static Carve.Core.FaceSplit;

namespace Carve.Core;

/// <summary>
/// What <see cref="Knife.Cut"/> made: the edit, and for each segment of the path, in order (the closing one last), the
/// vertex ids its two ends became (<see cref="Edges"/>; empty on failure).
/// </summary>
public sealed record KnifeResult(EditResult Edit, IReadOnlyList<(int From, int To)> Edges);

/// <summary>
/// The knife (T-67): a path of points on the mesh's surface, cut into its faces through <see cref="FaceSplit"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Points.</b> Each point must lie within <see cref="FaceSplit.Tolerance"/> (1e-4 of the mesh's bounds diagonal)
/// of the surface. A point that close to a vertex is that vertex; otherwise one that close to an edge is on that edge;
/// otherwise it is inside a face, projected onto its plane. A point off the surface is refused with its distance and the
/// nearest surface point; one inside two faces is refused naming both.</item>
/// <item><b>Segments.</b> Consecutive points (and with <c>closed</c> the last and the first) must share a face, and the
/// segment between them must run across it: one that leaves it, or passes through one of its holes, is refused.</item>
/// <item><b>The path.</b> An open path must end on edges or vertices; a closed path wholly inside a face cuts out an
/// inner face and leaves the face a hole (<see cref="FaceSplit"/>).</item>
/// </list>
/// </remarks>
public static class Knife
{
    private enum Kind { Vertex, Edge, Inside }

    private readonly record struct Snap(Kind Kind, CutPoint Point, int[] Faces, Vector3 At);

    /// <summary>Cuts <paramref name="points"/> into <paramref name="mesh"/> (see <see cref="Knife"/>).</summary>
    public static KnifeResult Cut(PolygonMesh mesh, IReadOnlyList<Vector3> points, bool closed = false)
    {
        var errors = new List<MeshError>();
        var least = closed ? 3 : 2;
        if (points.Count < least)
            return Refused([new MeshError("points", MeshEdits.Invariant($"{(closed ? "a closed" : "an open")} path needs at least {least} points, got {points.Count}"))]);

        var tol = Tolerance(mesh);
        var snaps = new Snap[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var at = MeshEdits.Invariant($"points[{i}]");
            var p = points[i];
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z))
            {
                errors.Add(new MeshError(at, "must be a finite point"));
                continue;
            }
            if (SnapPoint(mesh, p, tol, at, errors) is { } snap) snaps[i] = snap;
        }
        if (errors.Count > 0) return Refused(errors);

        if (!closed)
            foreach (var i in new[] { 0, points.Count - 1 })
                if (snaps[i].Kind == Kind.Inside)
                    errors.Add(new MeshError(MeshEdits.Invariant($"points[{i}]"), MeshEdits.Invariant(
                        $"{Show(points[i])} is inside faces[{snaps[i].Faces[0]}], not on an edge or a vertex, and an open path must end on one: a cut that stops inside a face is not a face")));
        if (errors.Count > 0) return Refused(errors);

        var count = closed ? points.Count : points.Count - 1;
        var cuts = new List<FaceCut>();
        for (var k = 0; k < count; k++)
        {
            var (i, j) = (k, (k + 1) % points.Count);
            if (Segment(mesh, snaps[i], snaps[j], i, j, tol, errors) is { } face)
                cuts.Add(new FaceCut(face, snaps[i].Point, snaps[j].Point));
        }
        if (errors.Count > 0) return Refused(errors);

        // A closed path in one face that touches its boundary at one point only: the face around it would pass through
        // that point twice, which a face cannot (the person's ruling, 2026-11-12).
        if (closed && cuts.Select(c => c.Face).Distinct().Count() == 1)
        {
            var touching = Enumerable.Range(0, points.Count).Where(i => snaps[i].Kind != Kind.Inside).ToArray();
            if (touching.Length == 1)
            {
                var i = touching[0];
                return Refused([new MeshError(MeshEdits.Invariant($"points[{i}]"), MeshEdits.Invariant(
                    $"the closed path touches the face's boundary at one point only, at {Show(OnSurface(mesh, snaps[i]))}; touch it at two points to split the face, or keep it clear of the edge to cut a hole"))]);
            }
        }

        var r = Split(mesh, cuts);
        if (!r.Edit.Succeeded)
            return Refused(r.Edit.Errors.Select(e => new MeshError(Segments(e.Element, points.Count), e.Message)).ToArray());
        return new KnifeResult(r.Edit, r.Segments);
    }

    private static KnifeResult Refused(IReadOnlyList<MeshError> errors) => new(EditResult.Refused(errors), []);

    /// <summary><c>cuts[k]</c>, in <see cref="FaceSplit"/>'s errors, named as the knife's segment from points[k].</summary>
    private static string Segments(string element, int n)
    {
        if (!element.StartsWith("cuts[", StringComparison.Ordinal) || !int.TryParse(element[5..^1], out var k)) return element;
        return MeshEdits.Invariant($"points[{k}], points[{(k + 1) % n}]");
    }

    /// <summary>Where a vertex or edge snap lies: the vertex, or the point at its fraction along the edge.</summary>
    private static Vector3 OnSurface(PolygonMesh mesh, Snap s) => s.Point.IsVertex || s.Point.IsInterior
        ? s.At
        : Vector3.Lerp(mesh.Vertices[s.Point.A], mesh.Vertices[s.Point.B], (float)s.Point.T);

    internal static string Show(Vector3 p) => MeshEdits.Invariant($"[{p.X:G6}, {p.Y:G6}, {p.Z:G6}]");

    private static Snap? SnapPoint(PolygonMesh mesh, Vector3 p, double tol, string at, List<MeshError> errors)
    {
        var verts = mesh.Vertices;
        var faces = mesh.LiveFaces.ToArray();
        var q = D(p);

        // A vertex, the nearest within the tolerance (the lowest id on a tie).
        var used = faces.SelectMany(f => mesh.Faces[f].Corners).Distinct().Order();
        int vertex = -1;
        var best = double.MaxValue;
        foreach (var v in used)
        {
            var d = Length(D(verts[v]) - q);
            if (d <= tol && d < best) (vertex, best) = (v, d);
        }
        if (vertex >= 0)
            return new Snap(Kind.Vertex, CutPoint.AtVertex(vertex), faces.Where(f => mesh.Faces[f].Corners.Contains(vertex)).ToArray(), verts[vertex]);

        // An edge, the nearest within the tolerance (the lowest pair on a tie).
        var edges = new SortedSet<(int Lo, int Hi)>();
        foreach (var f in faces)
            foreach (var loop in mesh.Faces[f].Holes.Prepend(mesh.Faces[f].Loop))
                for (var i = 0; i < loop.Count; i++)
                {
                    var (a, b) = (loop[i], loop[(i + 1) % loop.Count]);
                    edges.Add(a < b ? (a, b) : (b, a));
                }
        (int Lo, int Hi) edge = (-1, -1);
        best = double.MaxValue;
        foreach (var e in edges)
        {
            var d = SegmentDistance(q, D(verts[e.Lo]), D(verts[e.Hi]));
            if (d <= tol && d < best) (edge, best) = (e, d);
        }
        if (edge.Lo >= 0)
        {
            var (a, b) = (D(verts[edge.Lo]), D(verts[edge.Hi]));
            var t = Math.Clamp(Dot(q - a, b - a) / Dot(b - a, b - a), 0, 1);
            var on = faces.Where(f => HasEdge(mesh.Faces[f], edge.Lo, edge.Hi)).ToArray();
            return new Snap(Kind.Edge, CutPoint.OnEdge(edge.Lo, edge.Hi, t), on, p);
        }

        // A face it lies inside, projected onto its plane.
        var inside = new List<(int Face, Vector3 At)>();
        foreach (var f in faces)
        {
            var (near, inPlane) = Nearest(mesh, f, q);
            if (inPlane && Length(near - q) <= tol) inside.Add((f, new Vector3((float)near.X, (float)near.Y, (float)near.Z)));
        }
        if (inside.Count == 1)
            return new Snap(Kind.Inside, CutPoint.Interior(inside[0].At), [inside[0].Face], inside[0].At);
        if (inside.Count > 1)
        {
            errors.Add(new MeshError(at, MeshEdits.Invariant(
                $"{Show(p)} lies inside {string.Join(" and ", inside.Select(x => MeshEdits.Invariant($"faces[{x.Face}]")))}, which overlap there")));
            return null;
        }

        // Off the surface: its distance and the nearest surface point.
        var nearest = (Distance: double.MaxValue, Point: q, Face: -1);
        foreach (var f in faces)
        {
            var (near, _) = Nearest(mesh, f, q);
            var d = Length(near - q);
            if (d < nearest.Distance) nearest = (d, near, f);
        }
        errors.Add(new MeshError(at, nearest.Face < 0
            ? MeshEdits.Invariant($"{Show(p)} is not on the surface: the mesh has no face")
            : MeshEdits.Invariant(
                $"{Show(p)} is not on the surface: it is {nearest.Distance:G6} from it, more than the tolerance {tol:G6}; the nearest surface point is {Show(new Vector3((float)nearest.Point.X, (float)nearest.Point.Y, (float)nearest.Point.Z))}, on faces[{nearest.Face}]")));
        return null;
    }

    /// <summary>The point of face <paramref name="f"/> nearest <paramref name="q"/>, and whether it is the projection onto
    /// the face's plane (inside the face, clear of its holes).</summary>
    private static (Vector3D Point, bool InPlane) Nearest(PolygonMesh mesh, int f, Vector3D q)
    {
        var verts = mesh.Vertices;
        var face = mesh.Faces[f];
        var n = PolygonMesh.Unit(PolygonMesh.NewellVector(verts, face.Loop));
        var normal = new Vector3D(n.X, n.Y, n.Z);
        var origin = D(verts[face.Loop[0]]);
        var projected = q - normal * Dot(q - origin, normal);
        var (u, v) = Basis(n);
        Vector2D P(int vertex)
        {
            var d = D(verts[vertex]) - origin;
            return new Vector2D(Dot(d, u), Dot(d, v));
        }
        var loops = face.Holes.Prepend(face.Loop).ToArray();
        if (Inside(loops, P, new Vector2D(Dot(projected - origin, u), Dot(projected - origin, v)))) return (projected, true);
        var best = (Distance: double.MaxValue, Point: q);
        foreach (var loop in loops)
            for (var i = 0; i < loop.Count; i++)
            {
                var (a, b) = (D(verts[loop[i]]), D(verts[loop[(i + 1) % loop.Count]]));
                var ab = b - a;
                var len2 = Dot(ab, ab);
                var t = len2 == 0 ? 0 : Math.Clamp(Dot(q - a, ab) / len2, 0, 1);
                var c = a + ab * t;
                var d = Length(c - q);
                if (d < best.Distance) best = (d, c);
            }
        return (best.Point, false);
    }

    /// <summary>The face segment i-j runs across, or null with an error.</summary>
    private static int? Segment(PolygonMesh mesh, Snap from, Snap to, int i, int j, double tol, List<MeshError> errors)
    {
        var at = MeshEdits.Invariant($"points[{i}], points[{j}]");
        var common = from.Faces.Intersect(to.Faces).Order().ToArray();
        if (common.Length == 0)
        {
            errors.Add(new MeshError(at, MeshEdits.Invariant($"points[{i}] and points[{j}] share no face: a segment runs across one face")));
            return null;
        }
        var across = new List<int>();
        var along = new List<int>();
        string? why = null;
        foreach (var f in common)
        {
            switch (Runs(mesh, f, from.At, to.At, tol))
            {
                case Run.Along: along.Add(f); break;
                case Run.Across: across.Add(f); break;
                case Run.Leaves: why ??= MeshEdits.Invariant($"the segment from points[{i}] to points[{j}] leaves faces[{f}]"); break;
                case var hole: why ??= MeshEdits.Invariant($"the segment from points[{i}] to points[{j}] passes through hole {(int)hole - (int)Run.Hole} of faces[{f}]"); break;
            }
        }
        if (across.Count == 1) return across[0];
        if (across.Count > 1)
        {
            errors.Add(new MeshError(at, MeshEdits.Invariant(
                $"the segment from points[{i}] to points[{j}] runs across {string.Join(" and ", across.Select(f => MeshEdits.Invariant($"faces[{f}]")))}, which overlap there")));
            return null;
        }
        if (along.Count > 0) return along[0];
        errors.Add(new MeshError(at, why!));
        return null;
    }

    private enum Run { Across, Along, Leaves, Hole }

    /// <summary>How segment a-b lies on face f: across it, along its boundary, leaving it, or through hole k (Hole + k).</summary>
    private static Run Runs(PolygonMesh mesh, int f, Vector3 a, Vector3 b, double tol)
    {
        var verts = mesh.Vertices;
        var face = mesh.Faces[f];
        var (pa, pb) = (D(a), D(b));
        var mid = (pa + pb) * 0.5;
        var loops = face.Holes.Prepend(face.Loop).ToArray();
        foreach (var loop in loops)
            for (var k = 0; k < loop.Count; k++)
                if (SegmentDistance(mid, D(verts[loop[k]]), D(verts[loop[(k + 1) % loop.Count]])) <= tol) return Run.Along;

        var n = PolygonMesh.Unit(PolygonMesh.NewellVector(verts, face.Loop));
        var origin = D(verts[face.Loop[0]]);
        var (u, v) = Basis(n);
        Vector2D Q(Vector3D x) => new(Dot(x - origin, u), Dot(x - origin, v));
        Vector2D P(int vertex) => Q(D(verts[vertex]));
        var (qa, qb) = (Q(pa), Q(pb));
        for (var h = 0; h < loops.Length; h++)
        {
            var loop = loops[h];
            var crosses = false;
            for (var k = 0; k < loop.Count && !crosses; k++)
            {
                var (c, d) = (D(verts[loop[k]]), D(verts[loop[(k + 1) % loop.Count]]));
                // An edge the segment's ends lie on does not cross it.
                if (SegmentDistance(pa, c, d) <= tol || SegmentDistance(pb, c, d) <= tol) continue;
                crosses = Crosses(qa, qb, Q(c), Q(d));
            }
            var inHole = h > 0 && InPolygon(loop, P, Q(mid));
            if (crosses || inHole) return h == 0 ? Run.Leaves : Run.Hole + (h - 1);
        }
        return InPolygon(loops[0], P, Q(mid)) ? Run.Across : Run.Leaves;
    }

    private static double Length(Vector3D d) => Math.Sqrt(Dot(d, d));
}
