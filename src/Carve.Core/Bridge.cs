using System.Numerics;

namespace Carve.Core;

/// <summary>
/// <c>bridge</c> (T-68): two open loops of equal count joined by a ring of quads, so a hole can go through a wall. No
/// vertex is made, so no weights are; each new quad takes the colour and groups of the face on the first loop's edge it
/// comes from, as extrude's walls take theirs.
/// </summary>
/// <remarks>
/// <para><b>Loops.</b> Each loop is a closed cycle of vertex ids, every edge of it (the last vertex back to the first
/// among them) an open edge, used by exactly one live face.</para>
/// <para><b>Pairing.</b> First-loop vertex <c>i</c> is paired with second-loop vertex <c>(r + d * i) mod n</c>, the
/// second loop taken as listed. Every rotation <c>r</c> and both directions <c>d</c> are tried, in the order
/// <c>d = +1</c> for <c>r = 0 .. n-1</c> and then <c>d = -1</c> for <c>r = 0 .. n-1</c>, and the first pairing whose total
/// length (the sum of the distances between paired vertices) is least is taken: a later pairing replaces it only when
/// strictly shorter. So the rotation and direction in which the agent lists either loop do not change the result.</para>
/// <para><b>Faces.</b> Quad <c>i</c> joins pair <c>i</c> to pair <c>i + 1</c> (the last to the first), appended in the
/// first loop's listed order. Each quad runs its edge on the first loop opposite to the existing face there, so the
/// surface closes; its edge on the second loop must then run opposite to the face there too, and when it does not (the
/// two loops' faces wind so that no quad agrees with both, or a loop's faces wind both ways round it) the bridge is
/// refused.</para>
/// </remarks>
public static class Bridge
{
    /// <summary>The pairing the least total length gives: second-loop index <c>(Rotation + Direction * i) mod n</c> for first-loop index <c>i</c>.</summary>
    public readonly record struct Pairing(int Rotation, int Direction);

    /// <summary>
    /// Bridges <paramref name="first"/> and <paramref name="second"/>. Refused: a loop that is not a cycle of open edges
    /// (fewer than three vertices, a vertex repeated or not live, two neighbours with no edge or an edge not open), loops
    /// of unequal count, a vertex in both loops, and loops whose faces wind so that no quad agrees with both.
    /// </summary>
    public static EditResult Join(PolygonMesh mesh, IReadOnlyList<int> first, IReadOnlyList<int> second)
    {
        var errors = new List<MeshError>();
        LoopProblems(mesh, "loops[0]", first, errors);
        LoopProblems(mesh, "loops[1]", second, errors);
        if (first.Count != second.Count)
            errors.Add(new MeshError("loops", MeshEdits.Invariant(
                $"the loops have {first.Count} and {second.Count} vertices: bridge joins loops of the same count, pair by pair")));
        foreach (var v in first.Intersect(second).Order())
            errors.Add(new MeshError(MeshEdits.Invariant($"vertices[{v}]"), "is in both loops: the two loops must share no vertex"));
        if (errors.Count > 0) return EditResult.Refused(errors);

        var n = first.Count;
        var pairing = Pair(mesh.Vertices, first, second);
        var partner = Enumerable.Range(0, n).Select(i => second[Mod(pairing.Rotation + pairing.Direction * i, n)]).ToArray();

        var quads = new List<Face>();
        // Each quad takes the colour and groups of the face on its first-loop edge, and starts unmapped (T-71).
        var provenance = new EditProvenance();
        var firstRuns = OpenUse(mesh, first[0], first[1 % n]).Runs;
        for (var i = 0; i < n; i++)
        {
            int a0 = first[i], a1 = first[(i + 1) % n], b0 = partner[i], b1 = partner[(i + 1) % n];
            var (aFace, aRuns) = OpenUse(mesh, a0, a1);
            if (aRuns != firstRuns)
            {
                errors.Add(new MeshError(MeshEdits.Invariant($"loops[0][{i}]"), MeshEdits.Invariant(
                    $"the faces round the first loop wind both ways (edges[{Math.Min(a0, a1)}-{Math.Max(a0, a1)}] against its first edge), so no ring of quads agrees with them all")));
                continue;
            }
            // The quad runs a1 -> a0 when the face there runs a0 -> a1, and a0 -> a1 otherwise; its second-loop edge
            // then runs b0 -> b1 or b1 -> b0, and the face there must run it the other way.
            var quad = aRuns ? new Face([a1, a0, b0, b1]) : new Face([a0, a1, b1, b0]);
            var (_, bRuns) = OpenUse(mesh, b0, b1);
            if (bRuns == aRuns)
            {
                errors.Add(new MeshError(MeshEdits.Invariant($"edges[{Math.Min(b0, b1)}-{Math.Max(b0, b1)}]"), MeshEdits.Invariant(
                    $"paired with edges[{Math.Min(a0, a1)}-{Math.Max(a0, a1)}]: the faces on the two edges wind so that no quad between them agrees with both, so the bridge could not close the surface")));
                continue;
            }
            provenance.Face(mesh.Faces.Count + quads.Count, new FaceOrigin(aFace, CornerOriginKind.None));
            quads.Add(quad);
        }
        if (errors.Count > 0) return EditResult.Refused(errors);

        var result = mesh.Edit(addedFaces: quads);
        return EditResult.From(mesh, result, [], [], Enumerable.Range(mesh.Faces.Count, n).ToArray(), [], provenance: provenance);
    }

    /// <summary>The least-total-length pairing of <paramref name="second"/> to <paramref name="first"/>, in the stated order of trial (see <see cref="Bridge"/>).</summary>
    public static Pairing Pair(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> first, IReadOnlyList<int> second)
    {
        var n = first.Count;
        var best = new Pairing(0, 1);
        var least = double.PositiveInfinity;
        foreach (var direction in new[] { 1, -1 })
            for (var rotation = 0; rotation < n; rotation++)
            {
                var total = 0.0;
                for (var i = 0; i < n; i++)
                    total += Vector3.Distance(vertices[first[i]], vertices[second[Mod(rotation + direction * i, n)]]);
                if (total < least)
                {
                    least = total;
                    best = new Pairing(rotation, direction);
                }
            }
        return best;
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    /// <summary>The one face on the open edge a-b, and whether it runs the edge from a to b.</summary>
    private static (int Face, bool Runs) OpenUse(PolygonMesh mesh, int a, int b)
    {
        mesh.Topology.TryGetEdge(a, b, out var edge);
        var use = edge!.Faces[0];
        return (use.Face, use.LoToHi == (a < b));
    }

    private static void LoopProblems(PolygonMesh mesh, string name, IReadOnlyList<int> loop, List<MeshError> errors)
    {
        if (loop.Count < 3)
        {
            errors.Add(new MeshError(name, MeshEdits.Invariant($"has {loop.Count} vertices: a loop is a cycle of at least 3 vertices joined by open edges")));
            return;
        }
        var bad = false;
        var seen = new Dictionary<int, int>();
        for (var i = 0; i < loop.Count; i++)
        {
            var v = loop[i];
            if (mesh.VertexProblem(v) is { } problem)
            {
                errors.Add(new MeshError(MeshEdits.Invariant($"{name}[{i}]"), MeshEdits.Invariant($"vertex {v}: {problem}")));
                bad = true;
            }
            else if (seen.TryGetValue(v, out var at))
            {
                errors.Add(new MeshError(MeshEdits.Invariant($"{name}[{i}]"), MeshEdits.Invariant($"vertex {v} repeats {name}[{at}]: a loop passes each vertex once")));
                bad = true;
            }
            else
                seen[v] = i;
        }
        if (bad) return;

        for (var i = 0; i < loop.Count; i++)
        {
            int a = loop[i], b = loop[(i + 1) % loop.Count];
            if (!mesh.Topology.TryGetEdge(a, b, out var edge))
                errors.Add(new MeshError(MeshEdits.Invariant($"{name}[{i}]"), MeshEdits.Invariant(
                    $"vertices {a} and {b} are not joined by an edge, so the loop is not a cycle of the mesh's edges")));
            else if (edge.Faces.Count != 1)
                errors.Add(new MeshError(MeshEdits.Invariant($"{name}[{i}]"), MeshEdits.Invariant(
                    $"{edge.Name} is used by {edge.Faces.Count} faces, not open: every edge of a loop must be open (one face)")));
        }
    }
}
