using static Carve.Core.FaceSplit;
using static Carve.Core.PlaneGeometry;

namespace Carve.Core;

/// <summary>What <see cref="LoopCut.Cut"/> made: the edit, and the ring's quads in the order walked (the start edge's
/// quads first, then on from the one found first), empty on failure.</summary>
public sealed record LoopCutResult(EditResult Edit, IReadOnlyList<int> Ring);

/// <summary>
/// <c>loop_cut</c> (T-66): edge loops across a ring of quads, on <see cref="FaceSplit"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>The ring.</b> It starts at the given edge and walks both ways, across each quad on the current edge to that
/// quad's opposite edge. It stops where it closes, at an open edge, or at a face that is not a plain quad (a triangle,
/// an n-gon, or a face with holes).</item>
/// <item><b>The fractions.</b> Each one is a new loop, at that fraction of every ring edge measured from the end on the
/// first vertex's side, which carries across each quad by its side edges so the loops stay parallel.</item>
/// <item>Ids, <c>split</c> and what carries follow <see cref="FaceSplit"/>.</item>
/// </list>
/// Refused: a pair that is not an edge of the mesh, an edge with no quad on it, an edge on more than two faces (a
/// non-manifold edge: an open rule, provisional), a ring that closes on itself reversed (a Mobius band, B-3), and a bad <c>at</c> (empty, not
/// strictly ascending, a fraction not strictly between 0 and 1, or one too near an end of the shortest ring edge by
/// <see cref="FaceSplit.Tolerance"/>). A cut across a quad that would run along one of its edges or cross one (a concave
/// quad can make either; a cut through a corner runs along the entry or exit edge that corner ends) is refused naming the
/// fraction, the face and the edge (the person's ruling, 2026-11-13), never as a <see cref="FaceSplit"/> <c>cuts[k]</c>.
/// </remarks>
public static class LoopCut
{
    public static LoopCutResult Cut(PolygonMesh mesh, (int A, int B) edge, IReadOnlyList<double> at)
    {
        var errors = new List<MeshError>();
        if (at.Count == 0) errors.Add(new MeshError("at", "list at least one fraction"));
        for (var i = 0; i < at.Count; i++)
        {
            if (!double.IsFinite(at[i]) || at[i] <= 0 || at[i] >= 1)
                errors.Add(new MeshError(MeshEdits.Invariant($"at[{i}]"), MeshEdits.Invariant($"{at[i]} is not strictly between 0 and 1")));
            else if (i > 0 && at[i] <= at[i - 1])
                errors.Add(new MeshError(MeshEdits.Invariant($"at[{i}]"), MeshEdits.Invariant($"{at[i]} is not above the fraction before it, {at[i - 1]}: at must be strictly ascending")));
        }

        // Every live edge's faces, by unordered pair.
        var onEdge = new Dictionary<(int, int), List<int>>();
        foreach (var f in mesh.LiveFaces)
            foreach (var loop in mesh.Faces[f].Holes.Prepend(mesh.Faces[f].Loop))
                for (var i = 0; i < loop.Count; i++)
                {
                    var k = Key(loop[i], loop[(i + 1) % loop.Count]);
                    if (!onEdge.TryGetValue(k, out var list)) onEdge[k] = list = [];
                    list.Add(f);
                }

        var (a, b) = edge;
        var name = MeshEdits.Invariant($"edge[{Math.Min(a, b)}-{Math.Max(a, b)}]");
        if (!onEdge.TryGetValue(Key(a, b), out var start))
            errors.Add(new MeshError(name, MeshEdits.Invariant($"[{a}, {b}] is not an edge of the mesh; select with kind edges lists the real edges")));
        else if (start.Count > 2)
            errors.Add(new MeshError(name, MeshEdits.Invariant($"[{a}, {b}] is on {start.Count} faces: loop_cut does not cross a non-manifold edge")));
        else if (!start.Any(f => IsQuad(mesh, f)))
            errors.Add(new MeshError(name, MeshEdits.Invariant($"[{a}, {b}] has no quad on it: loop_cut crosses plain four-cornered faces")));
        if (errors.Count > 0) return new LoopCutResult(EditResult.Refused(errors), []);

        // The ring: each quad with its entry and exit edges, both oriented from the a side to the b side.
        var steps = new List<(int Face, (int S, int T) In, (int S, int T) Out)>();
        var seen = new HashSet<int>();
        string? problem = null;

        // Walks from the start edge across quad `first`, on until the ring stops; true when it closed.
        bool Walk(int first)
        {
            var face = first;
            var cur = (S: a, T: b);
            while (true)
            {
                if (!seen.Add(face))
                {
                    // Back at a visited quad. A ring that comes home to this walk's first quad across the start edge the same
                    // way round has closed; one that arrives with the edge's ends swapped has half a twist in it, and a loop
                    // would meet itself. Any other revisit enters a quad already crossed, by another side: the ring crosses itself.
                    if (face == first && cur == (a, b)) return true;
                    if (face == first && cur == (b, a))
                        problem ??= MeshEdits.Invariant($"the ring from edge [{a}, {b}] comes back to it with its ends swapped: loop_cut does not cut a ring that closes on itself reversed");
                    else
                        problem ??= MeshEdits.Invariant($"the ring from edge [{a}, {b}] comes back into face {face}, which it has already crossed, across edge [{cur.S}, {cur.T}]: loop_cut does not cut a ring that crosses itself");
                    return true;
                }
                var next = Across(mesh, face, cur);
                steps.Add((face, cur, next));
                var k = Key(next.S, next.T);
                var label = MeshEdits.Invariant($"{Math.Min(next.S, next.T)}-{Math.Max(next.S, next.T)}");
                if (onEdge[k].Count > 2)
                {
                    problem ??= MeshEdits.Invariant($"the ring meets edge {label}, on {onEdge[k].Count} faces: loop_cut does not cross a non-manifold edge");
                    return false;
                }
                var others = onEdge[k].Where(f => f != face).ToList();
                if (others.Count != 1 || !IsQuad(mesh, others[0])) return false;
                face = others[0];
                cur = next;
            }
        }

        var quads = start!.Where(f => IsQuad(mesh, f)).Distinct().ToList();
        var closed = Walk(quads[0]);
        if (!closed && quads.Count > 1 && !seen.Contains(quads[1])) Walk(quads[1]);
        if (problem is not null)
            return new LoopCutResult(EditResult.Refused([new MeshError("edge", problem)]), []);

        var shortest = steps.SelectMany(s => new[] { s.In, s.Out })
            .Select(e => (double)(mesh.Vertices[e.S] - mesh.Vertices[e.T]).Length()).Min();
        var tol = Tolerance(mesh);
        for (var i = 0; i < at.Count; i++)
        {
            if (at[i] * shortest <= tol || (1 - at[i]) * shortest <= tol)
                errors.Add(new MeshError(MeshEdits.Invariant($"at[{i}]"),
                    MeshEdits.Invariant($"{at[i]} is too near an end of the shortest ring edge ({shortest}): it must be more than the tolerance {tol} from either end")));
        }
        if (errors.Count > 0) return new LoopCutResult(EditResult.Refused(errors), []);

        // Each cut must run across its quad: one along an edge or over an edge is refused in loop_cut's
        // own words (the person's ruling, 2026-11-13).
        foreach (var s in steps.OrderBy(s => s.Face))
            foreach (var t in at)
                if (Across(mesh, s.Face, s.In, s.Out, t, tol) is { } why)
                    errors.Add(new MeshError("edge", MeshEdits.Invariant($"the cut at {t:G6} across faces[{s.Face}] {why}; loop_cut cuts only across a quad")));
        if (errors.Count > 0) return new LoopCutResult(EditResult.Refused(errors), []);

        var cuts = new List<FaceCut>();
        foreach (var s in steps.OrderBy(s => s.Face))
            foreach (var t in at)
                cuts.Add(new FaceCut(s.Face, CutPoint.OnEdge(s.In.S, s.In.T, t), CutPoint.OnEdge(s.Out.S, s.Out.T, t)));
        var r = Split(mesh, cuts);
        return new LoopCutResult(r.Edit, r.Edit.Succeeded ? [.. steps.Select(s => s.Face)] : []);
    }

    /// <summary>Why the cut at <paramref name="t"/> from <paramref name="entry"/> to <paramref name="exit"/> does not run
    /// across quad <paramref name="f"/>, or null when it does.</summary>
    private static string? Across(PolygonMesh mesh, int f, (int S, int T) entry, (int S, int T) exit, double t, double tol)
    {
        var verts = mesh.Vertices;
        var loop = mesh.Faces[f].Loop;
        Vector3D At(int v) => D(verts[v]);
        var p = At(entry.S) + (At(entry.T) - At(entry.S)) * t;
        var q = At(exit.S) + (At(exit.T) - At(exit.S)) * t;
        for (var k = 0; k < 4; k++)
            if (Overlaps(p, q, At(loop[k]), At(loop[(k + 1) % 4]), tol))
                return MeshEdits.Invariant($"runs along its edge [{loop[k]}, {loop[(k + 1) % 4]}]");
        var n = PolygonMesh.Unit(PolygonMesh.NewellVector(verts, loop));
        var (u, w) = Basis(n);
        var origin = At(loop[0]);
        Vector2D Q(Vector3D x) => new(Dot(x - origin, u), Dot(x - origin, w));
        for (var k = 0; k < 4; k++)
        {
            var (c, d) = (At(loop[k]), At(loop[(k + 1) % 4]));
            // An edge the cut starts or ends on (its entry and exit edges) is not crossed by it: projected, the cut's own
            // end is rounding noise of either sign against it (RV-33). Within the tolerance is on it, as in FaceSplit.
            if (SegmentDistance(p, c, d) <= tol || SegmentDistance(q, c, d) <= tol) continue;
            if (Crosses(Q(p), Q(q), Q(c), Q(d)))
                return MeshEdits.Invariant($"crosses its edge [{loop[k]}, {loop[(k + 1) % 4]}]");
        }
        return null;
    }

    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    private static bool IsQuad(PolygonMesh mesh, int f) => mesh.Faces[f].Holes.Count == 0 && mesh.Faces[f].Loop.Count == 4;

    /// <summary>The edge opposite <paramref name="entry"/> in quad <paramref name="f"/>, oriented the same way: each end is
    /// the entry end's neighbour by a side edge.</summary>
    private static (int S, int T) Across(PolygonMesh mesh, int f, (int S, int T) entry)
    {
        var loop = mesh.Faces[f].Loop;
        var i = loop.ToList().IndexOf(entry.S);
        if (loop[(i + 1) % 4] == entry.T)
            return (loop[(i + 3) % 4], loop[(i + 2) % 4]); // s, t, u, w: sides t-u and w-s
        return (loop[(i + 1) % 4], loop[(i + 2) % 4]);     // t, s, x, y: sides s-x and t-y
    }
}
