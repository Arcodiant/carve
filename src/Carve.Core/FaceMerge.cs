namespace Carve.Core;

/// <summary>
/// <c>merge_faces</c> (T-68): connected faces dissolved into one. The lowest listed id survives, reshaped to the union's
/// outline, with a hole wherever the union encloses one (T-63); the other listed faces are tombstoned (T-64), and so is
/// every vertex of the listed faces that no live face uses afterwards. A vertex still on the outline stays, collinear or
/// not: no vertex is dissolved from an outline.
/// </summary>
/// <remarks>
/// <para>The outline is the listed faces' directed edges (outer loops and holes, each its own way) less every edge two
/// listed faces share, which the merge dissolves. The edges left are chained into loops; seen along the listed faces'
/// summed area normal, the one loop winding counter-clockwise is the outer loop and every clockwise one a hole.</para>
/// <para>The outer loop starts at the first corner of the survivor's own outer loop that is still on it (else at its
/// lowest vertex id); each hole starts at its lowest vertex id, and the holes are in order of that id.</para>
/// <para>The survivor keeps its colour and groups. A listed face of another colour (no colour counts as one) is a
/// warning naming it, never an error. A hard mark on a dissolved edge goes with the edge. A non-planar result is the
/// report's non-planar warning, as define_mesh's is.</para>
/// </remarks>
public static class FaceMerge
{
    public const string TooFewMessage = "merge_faces needs at least two faces to merge";

    /// <summary>
    /// Merges <paramref name="faces"/> into the lowest listed id. Refused: fewer than two faces, an id that names no live
    /// face or is listed twice, faces that are not connected by shared edges, two listed faces running a shared edge the
    /// same way (mixed winding), and a result that is not one face (faces touching at a vertex only, a closed shell, two
    /// outlines). The result is also refused by <see cref="PolygonMesh.Edit"/>'s validation, as define_mesh would be.
    /// </summary>
    /// <param name="warnings">The fold warnings, for each listed face whose normal is 90 degrees or more from the merged face's, then the colour warnings (<c>faces[n]: ...</c>); empty when neither applies.</param>
    public static EditResult Merge(PolygonMesh mesh, IEnumerable<int> faces, out IReadOnlyList<string> warnings)
    {
        warnings = [];
        var list = faces.ToArray();
        if (list.Length < 2)
            return EditResult.Refused([new MeshError("faces", TooFewMessage + MeshEdits.Invariant($" (got {list.Length})"))]);

        var errors = MeshEdits.UnknownElements(mesh, [], [], list);
        var first = new Dictionary<int, int>();
        for (var i = 0; i < list.Length; i++)
        {
            if (first.TryGetValue(list[i], out var at))
                errors.Add(new MeshError(MeshEdits.Invariant($"faces[{list[i]}]"),
                    $"the {MeshEdits.Ordinal(i)} listed face is listed {MeshEdits.Ordinal(at)} too: each face is merged once"));
            else
                first[list[i]] = i;
        }
        if (errors.Count > 0) return EditResult.Refused(errors);

        var listed = new SortedSet<int>(list);
        var survivor = listed.Min;

        // Shared edges: two listed faces running an edge opposite ways dissolve it and are connected by it.
        var dissolved = new HashSet<(int Lo, int Hi)>();
        var links = listed.ToDictionary(f => f, _ => new List<int>());
        foreach (var edge in mesh.Topology.Edges)
        {
            var uses = edge.Faces.Where(u => listed.Contains(u.Face)).ToArray();
            if (uses.Length < 2) continue;
            var clash = uses.SelectMany((u, i) => uses.Skip(i + 1).Where(w => w.LoToHi == u.LoToHi)
                .Select(w => (A: Math.Min(u.Face, w.Face), B: Math.Max(u.Face, w.Face)))).ToArray();
            if (clash.Length > 0)
            {
                var pair = clash.Min();
                errors.Add(new MeshError(edge.Name, MeshEdits.Invariant(
                    $"faces[{pair.A}] and faces[{pair.B}] both run this edge the same way (mixed winding), so they cannot be merged")));
                continue;
            }
            if (uses.Length > 2)
            {
                errors.Add(new MeshError(edge.Name, MeshEdits.Invariant(
                    $"{uses.Length} listed faces share this edge, so the merged faces would not be one face")));
                continue;
            }
            dissolved.Add((edge.Lo, edge.Hi));
            links[uses[0].Face].Add(uses[1].Face);
            links[uses[1].Face].Add(uses[0].Face);
        }
        if (errors.Count > 0) return EditResult.Refused(errors);

        // Connected: every listed face reached from the survivor through shared edges.
        var reached = new HashSet<int> { survivor };
        var queue = new Queue<int>([survivor]);
        while (queue.Count > 0)
            foreach (var linked in links[queue.Dequeue()])
                if (reached.Add(linked)) queue.Enqueue(linked);
        foreach (var f in listed.Where(f => !reached.Contains(f)))
            errors.Add(new MeshError(MeshEdits.Invariant($"faces[{f}]"), MeshEdits.Invariant(
                $"shares no edge, directly or through other listed faces, with faces[{survivor}]: the listed faces must be connected by shared edges")));
        if (errors.Count > 0) return EditResult.Refused(errors);

        // The outline: every listed face's directed edges but the dissolved ones, chained from each vertex to the next.
        var next = new Dictionary<int, List<int>>();
        foreach (var f in listed)
        {
            var face = mesh.Faces[f];
            for (var hole = -1; hole < face.Holes.Count; hole++)
            {
                var loop = face.LoopAt(hole);
                for (var i = 0; i < loop.Count; i++)
                {
                    int a = loop[i], b = loop[(i + 1) % loop.Count];
                    if (dissolved.Contains((Math.Min(a, b), Math.Max(a, b)))) continue;
                    if (!next.TryGetValue(a, out var to)) next[a] = to = [];
                    to.Add(b);
                }
            }
        }

        if (next.Count == 0)
            return NotOne("the listed faces close a shell, so no outline is left");
        if (next.Where(n => n.Value.Count > 1).Select(n => n.Key).Order().FirstOrDefault(-1) is var pinch and >= 0)
            return NotOne(MeshEdits.Invariant($"the outline passes through vertices[{pinch}] twice, so the faces touch there at a vertex only"));

        var loops = new List<List<int>>();
        var seen = new HashSet<int>();
        foreach (var start in next.Keys.Order())
        {
            if (seen.Contains(start)) continue;
            var loop = new List<int>();
            for (var v = start; seen.Add(v); v = next[v][0])
            {
                loop.Add(v);
                if (!next.ContainsKey(next[v][0])) return NotOne("the outline does not close");
            }
            loops.Add(loop);
        }

        // Outer and holes, by winding along the listed faces' summed area normal.
        (double X, double Y, double Z) normal = (0, 0, 0);
        foreach (var f in listed)
        {
            var n = PolygonMesh.NewellVector(mesh.Vertices, mesh.Faces[f].Loop);
            normal = (normal.X + n.X, normal.Y + n.Y, normal.Z + n.Z);
        }
        var outer = new List<List<int>>();
        var holes = new List<List<int>>();
        foreach (var loop in loops)
        {
            var n = PolygonMesh.NewellVector(mesh.Vertices, loop);
            (n.X * normal.X + n.Y * normal.Y + n.Z * normal.Z > 0 ? outer : holes).Add(loop);
        }
        if (outer.Count != 1)
            return NotOne(outer.Count == 0
                ? "no loop of the outline winds as an outer loop"
                : MeshEdits.Invariant($"the outline is {outer.Count} separate loops, so the result would be {outer.Count} faces"));

        var own = mesh.Faces[survivor].Loop;
        var outline = outer[0];
        var begin = own.Select(v => outline.IndexOf(v)).FirstOrDefault(i => i >= 0, -1);
        var merged = new Face(
            Rotated(outline, begin >= 0 ? begin : outline.IndexOf(outline.Min())),
            holes.Select(h => Rotated(h, h.IndexOf(h.Min()))).OrderBy(h => h[0]).ToList());

        // Vertices of the listed faces that no live face uses afterwards go; a stray no face used before stays.
        var used = new HashSet<int>(merged.Corners);
        foreach (var f in mesh.LiveFaces)
            if (!listed.Contains(f)) used.UnionWith(mesh.Faces[f].Corners);
        var gone = listed.SelectMany(f => mesh.Faces[f].Corners).Where(v => !used.Contains(v)).Distinct().Order().ToArray();
        var others = listed.Where(f => f != survivor).ToArray();

        var result = mesh.Edit(reshapedFaces: [KeyValuePair.Create(survivor, merged)], deletedVertices: gone, deletedFaces: others);
        var edit = EditResult.From(mesh, result, [], [], [], [], new DeletedElements(gone, others));
        if (edit.Succeeded) warnings = [.. FoldWarnings(mesh, listed, survivor, PolygonMesh.NewellVector(mesh.Vertices, merged.Loop)), .. ColourWarnings(mesh, survivor, others)];
        return edit;

        static EditResult NotOne(string why) =>
            EditResult.Refused([new MeshError("faces", "the merged faces would not be one face: " + why)]);
    }

    private static List<int> Rotated(List<int> loop, int start) => [.. loop.Skip(start), .. loop.Take(start)];

    // A merged face folds over a listed face whose normal turns 90 degrees or more from the merged face's (ruled 2026-11-13:
    // merge and warn). The outline can be planar all the same, as when every face of a cube but one is merged.
    private static IEnumerable<string> FoldWarnings(PolygonMesh mesh, IEnumerable<int> listed, int survivor, (double X, double Y, double Z) m)
    {
        var ml = Math.Sqrt(m.X * m.X + m.Y * m.Y + m.Z * m.Z);
        foreach (var f in listed.Order())
        {
            var n = PolygonMesh.NewellVector(mesh.Vertices, mesh.Faces[f].Loop);
            var nl = Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
            if (ml == 0 || nl == 0 || (n.X * m.X + n.Y * m.Y + n.Z * m.Z) / (nl * ml) < 1e-6)
                yield return MeshEdits.Invariant($"faces[{f}]: its normal is 90 degrees or more from the merged faces[{survivor}]'s, so the merged face folds back over it");
        }
    }

    private static IReadOnlyList<string> ColourWarnings(PolygonMesh mesh, int survivor, IEnumerable<int> others)
    {
        var keep = mesh.Style?.ColourOf(survivor);
        return others.Where(f => mesh.Style?.ColourOf(f) != keep)
            .Select(f => MeshEdits.Invariant($"faces[{f}]: merged with colour {mesh.Style?.ColourOf(f) ?? "none"}, and the merged face keeps faces[{survivor}]'s colour, {keep ?? "none"}"))
            .ToArray();
    }
}
