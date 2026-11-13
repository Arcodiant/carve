using System.Numerics;
using static Carve.Core.PlaneGeometry;

namespace Carve.Core;

/// <summary>
/// Where a cut passes on a face's boundary: an existing vertex (<see cref="AtVertex"/>), or a point on an edge
/// (<see cref="OnEdge"/>) at fraction <see cref="T"/> from <see cref="A"/> toward <see cref="B"/>.
/// </summary>
public readonly record struct CutPoint(int A, int B, double T)
{
    /// <summary>The cut passes through vertex <paramref name="vertex"/>.</summary>
    public static CutPoint AtVertex(int vertex) => new(vertex, -1, 0);

    /// <summary>The cut crosses edge <paramref name="a"/>-<paramref name="b"/> at fraction <paramref name="t"/> from <paramref name="a"/>.</summary>
    public static CutPoint OnEdge(int a, int b, double t) => new(a, b, t);

    /// <summary>The cut passes through a point inside the face, at <paramref name="at"/> (T-67): a new vertex there.</summary>
    public static CutPoint Interior(Vector3 at) => new(-1, InteriorMark, 0) { At = at };

    private const int InteriorMark = -2;

    /// <summary>Where an <see cref="Interior"/> point is; zero for the other kinds.</summary>
    public Vector3 At { get; init; }

    public bool IsVertex => B == -1;

    /// <summary>A point inside the face (<see cref="Interior"/>, T-67).</summary>
    public bool IsInterior => B == InteriorMark;

    public override string ToString() => IsInterior
        ? MeshEdits.Invariant($"the point [{At.X:G6}, {At.Y:G6}, {At.Z:G6}] inside the face")
        : IsVertex
        ? MeshEdits.Invariant($"vertex {A}")
        : MeshEdits.Invariant($"edge {A}-{B} at {T:G6}");
}

/// <summary>One straight segment of a cut across face <see cref="Face"/>, from one boundary point to another.</summary>
public sealed record FaceCut(int Face, CutPoint From, CutPoint To);

/// <summary>
/// One face a cut split (T-65): <see cref="Face"/> is its id, and <see cref="Pieces"/> its pieces, the kept id
/// (<see cref="Face"/> itself) first and the appended ones after it, ascending.
/// </summary>
public sealed record SplitFace(int Face, IReadOnlyList<int> Pieces);

/// <summary>
/// What <see cref="FaceSplit.Split"/> made: the edit (<see cref="EditResult.Split"/>, <see cref="EditResult.NewVertices"/>,
/// <see cref="EditResult.NewFaces"/>), and for each <see cref="FaceCut"/>, in the order given, the vertex ids its two ends
/// became (<see cref="Segments"/>; empty on failure). A segment along an existing edge, or from a vertex to itself, is
/// still listed, though it split nothing.
/// </summary>
public sealed record FaceSplitResult(EditResult Edit, IReadOnlyList<(int From, int To)> Segments);

/// <summary>
/// The shared face-splitting step of the cuts (T-65; <c>plane_cut</c>, and T-66's <c>loop_cut</c> and T-67's
/// <c>knife</c> on it): given straight segments across faces, it inserts vertices on edges and splits faces into pieces.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Vertices.</b> A cut point within <see cref="Tolerance"/> (<see cref="PolygonMesh.PlanarityTolerance"/> of the
/// mesh's bounding-box diagonal) of an edge's end is that vertex, and two points on one edge that close are one. Every
/// other edge point is one new vertex, shared by both faces on that edge. New vertices are appended by edge, low id then
/// high, and along each edge from its low end, so the same call always gives the same ids.</item>
/// <item><b>Faces.</b> Every live face with a new vertex on one of its edges takes it into that loop, keeping its id. A
/// split face keeps its id for the piece that holds its first outer-loop corner, or, when that corner is on the cut, the
/// next corner of its outer loop off the cut; the other pieces are appended, faces in id order, and a face's pieces in
/// the order they are found from its corners. Each piece's outer loop starts at its corner that comes first in the
/// face's corners (with the new vertices in place), and keeps the face's winding.</item>
/// <item><b>Holes.</b> A segment ending on a hole cuts that hole's loop into the pieces' outlines; a hole no segment meets
/// stays a hole of the piece that contains it.</item>
/// <item><b>What carries.</b> Each piece takes the split face's colour and groups; each piece of a split hard edge stays
/// hard and the segments' new edges are not hard; a new vertex blends its edge's ends' weights by its fraction along it,
/// renormalised and capped at four as <c>subdivide</c> does.</item>
/// <item><b>Interior points</b> (T-67). A <see cref="CutPoint.Interior"/> point is a new vertex inside its face, appended
/// after the edge vertices in the order the cuts give them; two such points of one face within the tolerance of each
/// other are one. A closed loop of segments touching no boundary cuts out an inner face (appended, never the kept piece)
/// and leaves the face around it a hole on the same loop, wound opposite. In a piece's loop the interior vertices rank
/// after the face's corners, by id. A new interior vertex blends the weights of the corners of the face's triangle it
/// lies in, barycentrically, renormalised and capped at four.</item>
/// </list>
/// Refused, naming <c>cuts[i]</c>: a face that is not live, a point whose vertex or edge is not the face's, a fraction
/// not in 0..1, a segment that leaves the face or crosses its boundary or another segment, a segment that runs along part
/// of an edge and on across the face (RV-32: never skipped; only a segment wholly along the boundary splits nothing), and
/// segments that leave a face in one piece (a segment with the same piece on both sides).
/// </remarks>
public static class FaceSplit
{
    /// <summary>How close a cut point must be to a vertex to be that vertex: <see cref="PolygonMesh.PlanarityTolerance"/>
    /// of the bounding-box diagonal of the mesh's live vertices, one length for the whole mesh so both faces on an edge
    /// agree.</summary>
    public static double Tolerance(PolygonMesh mesh)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var i in mesh.LiveVertices)
        {
            var v = mesh.Vertices[i];
            minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X);
            minY = Math.Min(minY, v.Y); maxY = Math.Max(maxY, v.Y);
            minZ = Math.Min(minZ, v.Z); maxZ = Math.Max(maxZ, v.Z);
        }
        return minX > maxX ? 0 : PolygonMesh.PlanarityTolerance * PolygonMesh.Length((maxX - minX, maxY - minY, maxZ - minZ));
    }

    public const string OnePieceMessage = "the cut does not divide this face: a segment has the same piece on both sides";

    /// <summary>Splits the faces <paramref name="cuts"/> cross (see <see cref="FaceSplit"/>).</summary>
    public static FaceSplitResult Split(PolygonMesh mesh, IReadOnlyList<FaceCut> cuts)
    {
        var errors = new List<MeshError>();
        if (cuts.Count == 0) return Refused([new MeshError("cuts", "nothing to split: give at least one segment")]);
        for (var i = 0; i < cuts.Count; i++) Validate(mesh, cuts[i], MeshEdits.Invariant($"cuts[{i}]"), errors);
        if (errors.Count > 0) return Refused(errors);

        var tol = Tolerance(mesh);
        var verts = mesh.Vertices;

        // 1. Every edge point, snapped to an end or to an earlier point on its edge, else a new vertex.
        var onEdges = new SortedDictionary<(int Lo, int Hi), List<(double T, int Ref)>>();
        var ends = new int[cuts.Count * 2];
        var interior = new List<(int Face, Vector3 At, int Slot)>();
        for (var i = 0; i < cuts.Count; i++)
        {
            Place(cuts[i].From, 2 * i);
            Place(cuts[i].To, 2 * i + 1);
        }

        void Place(CutPoint p, int slot)
        {
            if (p.IsInterior) { ends[slot] = -1; interior.Add((cuts[slot / 2].Face, p.At, slot)); return; }
            if (p.IsVertex) { ends[slot] = p.A; return; }
            var (lo, hi, t) = p.A < p.B ? (p.A, p.B, p.T) : (p.B, p.A, 1 - p.T);
            var length = Distance(verts[lo], verts[hi]);
            if (t * length <= tol) ends[slot] = lo;
            else if ((1 - t) * length <= tol) ends[slot] = hi;
            else
            {
                ends[slot] = -1;
                if (!onEdges.TryGetValue((lo, hi), out var list)) onEdges[(lo, hi)] = list = [];
                list.Add((t, slot));
            }
        }

        var added = new List<Vector3>();
        var addedT = new List<(int Lo, int Hi, double T)>();
        var chains = new Dictionary<(int Lo, int Hi), List<int>>();
        foreach (var ((lo, hi), list) in onEdges)
        {
            var length = Distance(verts[lo], verts[hi]);
            var chain = new List<int>();
            double start = double.NaN;
            foreach (var (t, slot) in list.OrderBy(x => x.T).ThenBy(x => x.Ref))
            {
                if (chain.Count == 0 || (t - start) * length > tol)
                {
                    start = t;
                    chain.Add(verts.Count + added.Count);
                    added.Add(Lerp(verts[lo], verts[hi], t));
                    addedT.Add((lo, hi, t));
                }
                ends[slot] = chain[^1];
            }
            chains[(lo, hi)] = chain;
        }

        // 1b. Every interior point (T-67): one new vertex per face and place, after the edge vertices.
        var stencils = addedT.Select(e =>
        {
            var stencil = new Subdivision.Stencil();
            stencil.Add(e.Lo, 1 - e.T);
            stencil.Add(e.Hi, e.T);
            return stencil;
        }).ToList();
        var placed = new List<(int Face, Vector3 At, int Id)>();
        foreach (var (f, at, slot) in interior)
        {
            var same = placed.FindIndex(q => q.Face == f && Distance(q.At, at) <= tol);
            if (same >= 0) { ends[slot] = placed[same].Id; continue; }
            var id = verts.Count + added.Count;
            placed.Add((f, at, id));
            added.Add(at);
            stencils.Add(Barycentric(mesh, f, at));
            ends[slot] = id;
        }

        var all = verts.Concat(added).ToArray();

        // 2. Every live face takes the new vertices on its edges into its loops.
        var updated = new Dictionary<int, Face>();
        if (chains.Count > 0)
        {
            foreach (var f in mesh.LiveFaces)
            {
                var face = mesh.Faces[f];
                var changed = false;
                var loop = Insert(face.Loop, chains, ref changed);
                var holes = face.Holes.Select(h => Insert(h, chains, ref changed)).ToArray();
                if (changed) updated[f] = new Face(loop, holes);
            }
        }

        // 3. Each cut face, split along its segments.
        var segments = new List<(int From, int To)>();
        var byFace = new SortedDictionary<int, List<(int A, int B, int Cut)>>();
        for (var i = 0; i < cuts.Count; i++)
        {
            var (a, b) = (ends[2 * i], ends[2 * i + 1]);
            segments.Add((a, b));
            var f = cuts[i].Face;
            var face = updated.GetValueOrDefault(f) ?? mesh.Faces[f];
            if (a == b || AlongBoundary(all, face, a, b, tol)) continue;
            // A segment that runs along part of an edge and on across the face is refused, never skipped (RV-32).
            if (PartlyAlong(all, face, a, b, tol) is { } edge)
            {
                errors.Add(new MeshError(MeshEdits.Invariant($"cuts[{i}]"), MeshEdits.Invariant(
                    $"the segment {a}-{b} runs along part of edge {edge.C}-{edge.D} of faces[{f}] and on across the face: a cut segment runs wholly along the face's boundary or wholly across it")));
                continue;
            }
            if (!byFace.TryGetValue(f, out var list)) byFace[f] = list = [];
            if (!list.Any(s => (s.A == a && s.B == b) || (s.A == b && s.B == a))) list.Add((a, b, i));
        }

        if (errors.Count > 0) return Refused(errors);

        var reshaped = new SortedDictionary<int, Face>(updated);
        var newFaces = new List<Face>();
        var split = new List<SplitFace>();
        var made = new List<(int Face, IReadOnlyList<int> Added)>();
        foreach (var (f, list) in byFace)
        {
            var original = mesh.Faces[f];
            var face = updated.GetValueOrDefault(f) ?? original;
            var pieces = Pieces(all, f, original, face, list, errors);
            if (pieces is null) continue;
            reshaped[f] = pieces[0];
            var ids = new List<int> { f };
            foreach (var piece in pieces.Skip(1))
            {
                ids.Add(mesh.Faces.Count + newFaces.Count);
                newFaces.Add(piece);
            }
            split.Add(new SplitFace(f, ids));
            made.Add((f, ids.Skip(1).ToArray()));
        }
        if (errors.Count > 0) return Refused(errors);

        var result = mesh.Edit(reshapedFaces: reshaped, addedVertices: added, addedFaces: newFaces);
        if (!result.Succeeded) return new FaceSplitResult(EditResult.Refused(result.Errors), []);

        var edited = Carry(mesh, result.Mesh!, chains, stencils, made);
        var newVertexIds = Enumerable.Range(verts.Count, added.Count).ToArray();
        var newFaceIds = Enumerable.Range(mesh.Faces.Count, newFaces.Count).ToArray();
        return new FaceSplitResult(EditResult.Made(edited, [], newVertexIds, newFaceIds, split: split), segments);
    }

    private static FaceSplitResult Refused(IReadOnlyList<MeshError> errors) => new(EditResult.Refused(errors), []);

    private static void Validate(PolygonMesh mesh, FaceCut cut, string at, List<MeshError> errors)
    {
        if (mesh.FaceProblem(cut.Face) is { } problem)
        {
            errors.Add(new MeshError(at, MeshEdits.Invariant($"faces[{cut.Face}]: {problem}")));
            return;
        }
        var face = mesh.Faces[cut.Face];
        foreach (var (p, end) in new[] { (cut.From, "from"), (cut.To, "to") })
        {
            if (p.IsInterior)
            {
                if (!InsideFace(mesh, face, p.At))
                    errors.Add(new MeshError(at, MeshEdits.Invariant($"{end}: [{p.At.X:G6}, {p.At.Y:G6}, {p.At.Z:G6}] is not inside faces[{cut.Face}], clear of its edges")));
            }
            else if (p.IsVertex)
            {
                if (!face.Corners.Contains(p.A))
                    errors.Add(new MeshError(at, MeshEdits.Invariant($"{end}: vertex {p.A} is not a corner of faces[{cut.Face}]")));
            }
            else if (!double.IsFinite(p.T) || p.T < 0 || p.T > 1)
                errors.Add(new MeshError(at, MeshEdits.Invariant($"{end}: the fraction {p.T:G6} along edge {p.A}-{p.B} is not in 0..1")));
            else if (!HasEdge(face, p.A, p.B))
                errors.Add(new MeshError(at, MeshEdits.Invariant($"{end}: {p.A}-{p.B} is not an edge of faces[{cut.Face}]")));
        }
    }

    /// <summary>Whether <paramref name="point"/> lies in the face's plane (within the tolerance), inside it, and further than
    /// the tolerance from every edge of its loops.</summary>
    internal static bool InsideFace(PolygonMesh mesh, Face face, Vector3 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)) return false;
        var tol = Tolerance(mesh);
        var verts = mesh.Vertices;
        var n = PolygonMesh.Unit(PolygonMesh.NewellVector(verts, face.Loop));
        var origin = D(verts[face.Loop[0]]);
        var p = D(point);
        if (Math.Abs(Dot(p - origin, new Vector3D(n.X, n.Y, n.Z))) > tol) return false;
        var (u, v) = Basis(n);
        Vector2D P(int vertex)
        {
            var d = D(verts[vertex]) - origin;
            return new Vector2D(Dot(d, u), Dot(d, v));
        }
        var loops = face.Holes.Prepend(face.Loop).ToArray();
        if (!Inside(loops, P, new Vector2D(Dot(p - origin, u), Dot(p - origin, v)))) return false;
        foreach (var loop in loops)
            for (var i = 0; i < loop.Count; i++)
                if (SegmentDistance(p, D(verts[loop[i]]), D(verts[loop[(i + 1) % loop.Count]])) <= tol) return false;
        return true;
    }

    internal static bool HasEdge(Face face, int a, int b)
    {
        foreach (var loop in face.Holes.Prepend(face.Loop))
            for (var i = 0; i < loop.Count; i++)
            {
                var (p, q) = (loop[i], loop[(i + 1) % loop.Count]);
                if ((p == a && q == b) || (p == b && q == a)) return true;
            }
        return false;
    }

    private static int[] Insert(IReadOnlyList<int> loop, Dictionary<(int Lo, int Hi), List<int>> chains, ref bool changed)
    {
        var result = new List<int>();
        for (var i = 0; i < loop.Count; i++)
        {
            var (p, q) = (loop[i], loop[(i + 1) % loop.Count]);
            result.Add(p);
            if (!chains.TryGetValue(p < q ? (p, q) : (q, p), out var chain)) continue;
            changed = true;
            result.AddRange(p < q ? chain : Enumerable.Reverse(chain));
        }
        return [.. result];
    }

    /// <summary>Whether segment a-b lies wholly along a loop of <paramref name="face"/>: a path of the loop from a to b,
    /// either way round, every vertex between them on the segment (so a segment along an existing edge, which splits
    /// nothing). A segment only partly along the boundary is not (<see cref="PartlyAlong"/>).</summary>
    private static bool AlongBoundary(Vector3[] all, Face face, int a, int b, double tol)
    {
        foreach (var loop in face.Holes.Prepend(face.Loop))
        {
            var i = IndexOf(loop, a);
            if (i < 0 || IndexOf(loop, b) < 0) continue;
            foreach (var step in new[] { 1, loop.Count - 1 })
            {
                var dir = D(all[b]) - D(all[a]);
                var length = Length(dir);
                for (var k = (i + step) % loop.Count; ; k = (k + step) % loop.Count)
                {
                    if (loop[k] == b) return true;
                    // A vertex between them must lie on the segment, further along it than the one before.
                    var x = D(all[loop[k]]) - D(all[a]);
                    var off = x - dir * (Dot(x, dir) / (length * length));
                    if (Length(off) > tol || Dot(x, dir) <= 0 || Dot(x, dir) >= length * length) break;
                }
            }
        }
        return false;
    }

    private static int IndexOf(IReadOnlyList<int> loop, int v)
    {
        for (var i = 0; i < loop.Count; i++) if (loop[i] == v) return i;
        return -1;
    }

    /// <summary>The first edge of <paramref name="face"/>'s loops that segment a-b runs along for more than the tolerance,
    /// or null.</summary>
    private static (int C, int D)? PartlyAlong(Vector3[] all, Face face, int a, int b, double tol)
    {
        foreach (var loop in face.Holes.Prepend(face.Loop))
            for (var i = 0; i < loop.Count; i++)
            {
                var (c, d) = (loop[i], loop[(i + 1) % loop.Count]);
                if (Overlaps(D(all[a]), D(all[b]), D(all[c]), D(all[d]), tol)) return (c, d);
            }
        return null;
    }

    /// <summary>
    /// The pieces of <paramref name="face"/> (with the new vertices in its loops) along <paramref name="segments"/>, the
    /// kept piece first; null, with errors added, when the segments do not split it cleanly.
    /// </summary>
    private static List<Face>? Pieces(Vector3[] all, int id, Face original, Face face, List<(int A, int B, int Cut)> segments,
        List<MeshError> errors)
    {
        var errorsBefore = errors.Count;
        var n = PolygonMesh.Unit(PolygonMesh.NewellVector(all, face.Loop));
        var (u, v) = Basis(n);
        var origin = D(all[face.Loop[0]]);
        Vector2D P(int vertex)
        {
            var d = D(all[vertex]) - origin;
            return new Vector2D(Dot(d, u), Dot(d, v));
        }

        var loops = face.Holes.Prepend(face.Loop).ToArray();
        var boundary = new List<(int A, int B)>();
        foreach (var loop in loops)
            for (var i = 0; i < loop.Count; i++) boundary.Add((loop[i], loop[(i + 1) % loop.Count]));

        // Each segment inside the face, crossing no boundary edge and no other segment.
        foreach (var (a, b, cut) in segments)
        {
            var at = MeshEdits.Invariant($"cuts[{cut}]");
            var (pa, pb) = (P(a), P(b));
            if (!Inside(loops, P, (pa + pb) * 0.5))
                errors.Add(new MeshError(at, MeshEdits.Invariant($"the segment {a}-{b} runs outside faces[{id}]")));
            else if (boundary.Any(e => e.A != a && e.A != b && e.B != a && e.B != b && Crosses(pa, pb, P(e.A), P(e.B))))
                errors.Add(new MeshError(at, MeshEdits.Invariant($"the segment {a}-{b} crosses an edge of faces[{id}]")));
            else if (segments.Any(s => s.Cut != cut && s.A != a && s.A != b && s.B != a && s.B != b && Crosses(pa, pb, P(s.A), P(s.B))))
                errors.Add(new MeshError(at, MeshEdits.Invariant($"the segment {a}-{b} crosses another segment inside faces[{id}]")));
        }
        if (errors.Count > errorsBefore) return null;

        // Half-edges: the loops in their own winding (the face on their left), and every segment both ways.
        var outgoing = new Dictionary<int, List<int>>();
        void AddHalf(int a, int b)
        {
            if (!outgoing.TryGetValue(a, out var list)) outgoing[a] = list = [];
            list.Add(b);
        }
        foreach (var (a, b) in boundary) AddHalf(a, b);
        foreach (var (a, b, _) in segments) { AddHalf(a, b); AddHalf(b, a); }
        var total = outgoing.Values.Sum(l => l.Count);

        int Next(int from, int at)
        {
            var back = Angle(P(at), P(from));
            var best = -1;
            var bestTurn = double.MaxValue;
            foreach (var w in outgoing[at])
            {
                var turn = back - Angle(P(at), P(w));
                while (turn <= 0) turn += 2 * Math.PI;
                while (turn > 2 * Math.PI) turn -= 2 * Math.PI;
                if (w == from) turn = 2 * Math.PI;
                if (turn < bestTurn) { bestTurn = turn; best = w; }
            }
            return best;
        }

        var visited = new HashSet<(int, int)>();
        var traced = new List<List<int>>();
        var starts = boundary.Concat(segments.SelectMany(s => new[] { (s.A, s.B), (s.B, s.A) }));
        foreach (var (a0, b0) in starts)
        {
            if (visited.Contains((a0, b0))) continue;
            var loop = new List<int>();
            var (a, b) = (a0, b0);
            while (visited.Add((a, b)))
            {
                loop.Add(a);
                if (loop.Count > total) break;
                (a, b) = (b, Next(a, b));
            }
            traced.Add(loop);
        }

        foreach (var (a, b, cut) in segments)
        {
            if (traced.Any(l => HasDirected(l, a, b) && HasDirected(l, b, a)))
            {
                errors.Add(new MeshError(MeshEdits.Invariant($"cuts[{cut}]"), MeshEdits.Invariant($"faces[{id}]: {OnePieceMessage}")));
                return null;
            }
        }

        var outlines = traced.Where(l => SignedArea(l, P) > 0).ToList();
        var holes = traced.Where(l => SignedArea(l, P) <= 0).ToList();
        if (outlines.Count < 2)
        {
            errors.Add(new MeshError(MeshEdits.Invariant($"cuts[{segments[0].Cut}]"), MeshEdits.Invariant($"faces[{id}]: {OnePieceMessage}")));
            return null;
        }

        // The kept piece holds the first outer-loop corner, or the next one off the cut.
        var onCut = segments.SelectMany(s => new[] { s.A, s.B }).ToHashSet();
        var decider = original.Loop.FirstOrDefault(c => !onCut.Contains(c), original.Loop[0]);
        var kept = outlines.FindIndex(l => l.Contains(decider));
        if (kept < 0) kept = 0;
        outlines.Insert(0, outlines[kept]);
        outlines.RemoveAt(kept + 1);

        // Each piece's loop starts at its corner first in the face's corners; each hole goes to the piece around it.
        var rank = new Dictionary<int, int>();
        foreach (var c in face.Corners) rank.TryAdd(c, rank.Count);
        foreach (var c in segments.SelectMany(s => new[] { s.A, s.B }).Order()) rank.TryAdd(c, rank.Count);
        var pieceHoles = outlines.Select(_ => new List<(int Rank, IReadOnlyList<int> Loop)>()).ToList();
        foreach (var hole in holes)
        {
            var point = P(hole[0]);
            var owner = -1;
            for (var k = 0; k < outlines.Count; k++)
                if (!Reverses(outlines[k], hole) && InPolygon(outlines[k], P, point) && (owner < 0 || Math.Abs(SignedArea(outlines[k], P)) < Math.Abs(SignedArea(outlines[owner], P))))
                    owner = k;
            if (owner < 0)
            {
                errors.Add(new MeshError(MeshEdits.Invariant($"faces[{id}]"), "a hole lies in no piece of the split"));
                return null;
            }
            pieceHoles[owner].Add((hole.Min(c => rank[c]), Rotate(hole, rank)));
        }

        return outlines.Select((l, k) => new Face(Rotate(l, rank), pieceHoles[k].OrderBy(h => h.Rank).Select(h => h.Loop))).ToList();
    }

    /// <summary>Whether <paramref name="hole"/> runs round the same vertices as <paramref name="outline"/>: the far side of
    /// an inner face a closed interior cut made, which lies on that face's edge, not inside it (T-67).</summary>
    private static bool Reverses(List<int> outline, List<int> hole) => outline.Count == hole.Count && outline.ToHashSet().SetEquals(hole);

    /// <summary>The barycentric blend of the corners of the face's triangle that <paramref name="at"/> lies in (T-67; the
    /// one it is least outside, for a point on none).</summary>
    private static Subdivision.Stencil Barycentric(PolygonMesh mesh, int face, Vector3 at)
    {
        var verts = mesh.Vertices;
        var (u, v) = Basis(PolygonMesh.Unit(PolygonMesh.NewellVector(verts, mesh.Faces[face].Loop)));
        Vector2D P(Vector3 x) => new(Dot(D(x), u), Dot(D(x), v));
        var p = P(at);
        var best = (Score: double.MinValue, A: 0, B: 0, C: 0, Wa: 1.0, Wb: 0.0, Wc: 0.0);
        foreach (var tri in Triangulator.TriangulateFace(mesh, face))
        {
            var (a, b, c) = (P(verts[tri.A]), P(verts[tri.B]), P(verts[tri.C]));
            var area = Orient(a, b, c);
            if (area == 0) continue;
            var (wa, wb, wc) = (Orient(p, b, c) / area, Orient(a, p, c) / area, Orient(a, b, p) / area);
            var score = Math.Min(wa, Math.Min(wb, wc));
            if (score > best.Score) best = (score, tri.A, tri.B, tri.C, wa, wb, wc);
        }
        var stencil = new Subdivision.Stencil();
        stencil.Add(best.A, best.Wa);
        stencil.Add(best.B, best.Wb);
        stencil.Add(best.C, best.Wc);
        return stencil;
    }

    private static int[] Rotate(List<int> loop, Dictionary<int, int> rank)
    {
        var start = 0;
        for (var i = 1; i < loop.Count; i++)
            if (rank[loop[i]] < rank[loop[start]]) start = i;
        return [.. loop.Skip(start), .. loop.Take(start)];
    }

    private static bool HasDirected(List<int> loop, int a, int b)
    {
        for (var i = 0; i < loop.Count; i++)
            if (loop[i] == a && loop[(i + 1) % loop.Count] == b) return true;
        return false;
    }

    /// <summary>Colours, groups, weights and hard edges carried onto the split mesh.</summary>
    private static PolygonMesh Carry(PolygonMesh source, PolygonMesh edited, Dictionary<(int Lo, int Hi), List<int>> chains,
        List<Subdivision.Stencil> stencils, List<(int Face, IReadOnlyList<int> Added)> made)
    {
        var mesh = source.Style is { } style ? edited.WithStyle(style.Carry(made)) : edited;
        if (source.Weights is { } weights)
        {
            var blended = new List<KeyValuePair<int, Influence[]>>();
            for (var k = 0; k < stencils.Count; k++)
            {
                if (stencils[k].Blend(weights) is { } influences)
                    blended.Add(KeyValuePair.Create(source.Vertices.Count + k, influences));
            }
            mesh = mesh.WithWeights(blended.Count == 0 ? weights : VertexWeights.Of(
                Enumerable.Range(0, source.Vertices.Count).Where(weights.Has)
                    .Select(v => KeyValuePair.Create(v, weights.Of(v).ToArray())).Concat(blended)));
        }
        if (source.Shade is not { } shade) return mesh;
        var halves = new List<(int, int)>();
        var whole = new List<(int, int)>();
        foreach (var ((lo, hi), chain) in chains)
        {
            if (!shade.IsHard(lo, hi)) continue;
            whole.Add((lo, hi));
            var path = chain.Prepend(lo).Append(hi).ToArray();
            for (var i = 0; i + 1 < path.Length; i++) halves.Add((path[i], path[i + 1]));
        }
        return mesh.WithShade(whole.Count == 0 ? shade : shade.WithEdges(whole, false).WithEdges(halves, true));
    }

    private static double Distance(Vector3 a, Vector3 b)
    {
        var d = D(b) - D(a);
        return Math.Sqrt(Dot(d, d));
    }

    private static Vector3 Lerp(Vector3 a, Vector3 b, double t) => new(
        (float)(a.X + (b.X - (double)a.X) * t), (float)(a.Y + (b.Y - (double)a.Y) * t), (float)(a.Z + (b.Z - (double)a.Z) * t));

    private static double Angle(Vector2D from, Vector2D to) => Math.Atan2(to.Y - from.Y, to.X - from.X);
}
