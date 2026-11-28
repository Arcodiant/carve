using System.Numerics;
using static Carve.Core.PlaneGeometry;

namespace Carve.Core;

/// <summary>
/// <c>tessellate</c> (T-237): the selected faces cut into triangles on their own planes, none with an edge longer than
/// <c>max_edge</c>, moving no existing vertex.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Edges.</b> Every edge of a selected face (outer loop and holes) is cut into the fewest equal pieces no longer
/// than <c>max_edge</c>, by its own length. The new points go into every face on that edge, so an unselected neighbour
/// takes them into its loop, in order, and is not otherwise changed: no T-junction, and the mesh stays closed.</item>
/// <item><b>Inside.</b> An equilateral lattice at <c>max_edge</c> spacing in the face's plane, each point kept only inside
/// the face and at least half the spacing clear of its loops. The face's loop points and the lattice are triangulated by a
/// constrained Delaunay triangulation (<see cref="ConstrainedDelaunay"/>) with every loop edge a constraint, so holes stay
/// holes; then any inner edge longer than <c>max_edge</c> (in the strip by the loops) is split at its midpoint until none
/// is. A new inner point lies on the face's own triangles (<see cref="Triangulator"/>), blended from the three corners of
/// the one it falls in.</item>
/// <item><b>Ids.</b> Edge points are appended first, edge by edge in the order the selected faces reach them, from the
/// lower vertex id; then each face's inner points, face by face. Each selected face keeps its id on the triangle at its
/// first corner; its other triangles are appended, face by face. <c>split</c> lists each face that became more than one
/// triangle.</item>
/// <item>Every piece takes its face's colour, material and groups; corner values (UVs) are found by vertex from the face
/// it was, and a split hard edge or seam stays one along its pieces (<see cref="EditProvenance"/>).</item>
/// </list>
/// </remarks>
public static class Tessellation
{
    /// <summary>The most new vertices one call may add (the person's ruling, 2026-11-27); a call predicted over it is refused.</summary>
    public const int Budget = 200_000;

    /// <summary>
    /// The new vertices a call is predicted to add, before any work: each edge's points plus each face's area over the
    /// lattice's cell (max_edge squared times sqrt 3 / 2). In doubles, so a tiny max_edge cannot overflow.
    /// </summary>
    public static double Predicted(PolygonMesh mesh, IReadOnlyList<int> faces, double maxEdge)
    {
        var edges = new HashSet<(int, int)>();
        double count = 0;
        foreach (var f in faces)
        {
            count += mesh.FaceArea(f) / (maxEdge * maxEdge * Math.Sqrt(3) / 2);
            foreach (var loop in mesh.Faces[f].Holes.Prepend(mesh.Faces[f].Loop))
                for (var i = 0; i < loop.Count; i++)
                {
                    var (a, b) = (loop[i], loop[(i + 1) % loop.Count]);
                    if (edges.Add(a < b ? (a, b) : (b, a)))
                        count += Math.Max(0, Math.Ceiling(Length(D(mesh.Vertices[b]) - D(mesh.Vertices[a])) / maxEdge - 1e-9) - 1);
                }
        }
        return count;
    }

    public static EditResult Tessellate(PolygonMesh mesh, double maxEdge, IReadOnlyList<int>? faces = null)
    {
        var errors = new List<MeshError>();
        if (!double.IsFinite(maxEdge) || maxEdge <= 0)
            errors.Add(new MeshError("max_edge", MeshEdits.Invariant($"{maxEdge} is not above 0")));
        if (faces is not null) errors.AddRange(MeshEdits.UnknownElements(mesh, [], [], faces));
        if (errors.Count > 0) return EditResult.Refused(errors);
        var selected = (faces ?? mesh.LiveFaces.ToList()).Distinct().Order().ToList();
        if (selected.Count == 0) return EditResult.Refused([new MeshError("faces", "list at least one face")]);
        var predicted = Predicted(mesh, selected, maxEdge);
        if (predicted > Budget)
            return EditResult.Refused([new MeshError("max_edge", MeshEdits.Invariant(
                $"{maxEdge:G6} would add about {predicted:N0} new vertices, over the budget of {Budget:N0}; give a larger max_edge or fewer faces"))]);

        var verts = mesh.Vertices;
        var added = new List<Vector3>();
        var stencils = new List<Stencil>();
        Vector3 At(int v) => v < verts.Count ? verts[v] : added[v - verts.Count];

        // 1. Every edge of a selected face, cut into equal pieces by its own length.
        var chains = new Dictionary<(int Lo, int Hi), List<int>>();
        foreach (var f in selected)
            foreach (var loop in mesh.Faces[f].Holes.Prepend(mesh.Faces[f].Loop))
                for (var i = 0; i < loop.Count; i++)
                {
                    var (a, b) = (loop[i], loop[(i + 1) % loop.Count]);
                    var key = a < b ? (a, b) : (b, a);
                    if (chains.ContainsKey(key)) continue;
                    var length = Length(D(verts[key.Item2]) - D(verts[key.Item1]));
                    var pieces = (int)Math.Ceiling(length / maxEdge - 1e-9);
                    var chain = new List<int>();
                    for (var k = 1; k < pieces; k++)
                    {
                        var t = (double)k / pieces;
                        chain.Add(verts.Count + added.Count);
                        stencils.Add(Stencil.Lerp(key.Item1, key.Item2, t));
                        added.Add(Vector3.Lerp(verts[key.Item1], verts[key.Item2], (float)t));
                    }
                    if (chain.Count > 0) chains[key] = chain;
                }

        // 2. Every live face takes the new points on its edges into its loops.
        var reshaped = new SortedDictionary<int, Face>();
        foreach (var f in mesh.LiveFaces)
        {
            var face = mesh.Faces[f];
            var changed = false;
            var loop = FaceSplit.Insert(face.Loop, chains, ref changed);
            var holes = face.Holes.Select(h => FaceSplit.Insert(h, chains, ref changed)).ToArray();
            if (changed) reshaped[f] = new Face(loop, holes);
        }

        // 3. Each selected face, triangulated with its lattice.
        var newFaces = new List<Face>();
        var made = new List<(int Face, int[] Added)>();
        foreach (var f in selected)
        {
            var original = mesh.Faces[f];
            var face = reshaped.GetValueOrDefault(f) ?? original;
            List<(int A, int B, int C)> triangles;
            try { triangles = Triangulate(mesh, f, face, maxEdge, At, added, stencils); }
            catch (InvalidOperationException e)
            {
                errors.Add(new MeshError($"faces[{f}]", "cannot be tessellated: " + e.Message));
                continue;
            }
            var first = triangles.FindIndex(t => t.A == original.Loop[0] || t.B == original.Loop[0] || t.C == original.Loop[0]);
            if (first > 0) (triangles[0], triangles[first]) = (triangles[first], triangles[0]);
            reshaped[f] = new Face([triangles[0].A, triangles[0].B, triangles[0].C]);
            var ids = new int[triangles.Count - 1];
            for (var i = 1; i < triangles.Count; i++)
            {
                ids[i - 1] = mesh.Faces.Count + newFaces.Count;
                newFaces.Add(new Face([triangles[i].A, triangles[i].B, triangles[i].C]));
            }
            made.Add((f, ids));
        }
        if (errors.Count > 0) return EditResult.Refused(errors);

        var result = mesh.Edit(reshapedFaces: reshaped, addedVertices: added, addedFaces: newFaces);
        if (!result.Succeeded) return EditResult.Refused(result.Errors);

        var provenance = new EditProvenance();
        for (var k = 0; k < stencils.Count; k++) provenance.Vertex(verts.Count + k, VertexOrigin.Blend(stencils[k]));
        foreach (var f in reshaped.Keys) provenance.Face(f, new FaceOrigin(f, CornerOriginKind.Vertex));
        foreach (var (f, pieceIds) in made)
            foreach (var piece in pieceIds) provenance.Face(piece, new FaceOrigin(f, CornerOriginKind.Vertex));
        foreach (var ((lo, hi), chain) in chains) provenance.Split(lo, hi, chain);
        var split = made.Where(m => m.Added.Length > 0).Select(m => new SplitFace(m.Face, [m.Face, .. m.Added])).ToList();
        var edit = EditResult.From(mesh, result, [], Enumerable.Range(verts.Count, added.Count).ToArray(),
            Enumerable.Range(mesh.Faces.Count, newFaces.Count).ToArray(), [], provenance: provenance);
        return edit.WithSplit(split);
    }

    /// <summary>
    /// Face <paramref name="f"/>, with the edge points already in its loops (<paramref name="face"/>), as triangles by vertex
    /// id, counter-clockwise seen from outside; its new inner points are appended to <paramref name="added"/>.
    /// </summary>
    private static List<(int A, int B, int C)> Triangulate(PolygonMesh mesh, int f, Face face, double maxEdge,
        Func<int, Vector3> at, List<Vector3> added, List<Stencil> stencils)
    {
        var original = mesh.Faces[f];
        var normal = PolygonMesh.Unit(PolygonMesh.NewellVector(mesh.Vertices, original.Loop));
        var (u, w) = Basis(normal);
        var origin = D(mesh.Vertices[original.Loop[0]]);
        Vector2D Q(Vector3D x) => new(Dot(x - origin, u), Dot(x - origin, w));
        Vector2D Of(int v) => Q(D(at(v)));

        var loops = face.Holes.Prepend(face.Loop).ToArray();
        var outer = loops[0].Select(Of).ToArray();
        var (minX, minY, maxX, maxY) = (outer.Min(p => p.X), outer.Min(p => p.Y), outer.Max(p => p.X), outer.Max(p => p.Y));
        var cdt = new ConstrainedDelaunay(minX, minY, maxX, maxY);
        var ids = new List<int> { -1, -1, -1 }; // the triangulation's point index to vertex id; -1 a new inner point
        var index = new Dictionary<int, int>();
        foreach (var loop in loops)
            foreach (var v in loop)
            {
                index[v] = cdt.Add(Of(v));
                ids.Add(v);
            }

        foreach (var p in Lattice(loops.Select(l => l.Select(Of).ToArray()).ToArray(), maxEdge))
        {
            cdt.Add(p);
            ids.Add(-1);
        }

        foreach (var loop in loops)
            for (var i = 0; i < loop.Count; i++) cdt.Constrain(index[loop[i]], index[loop[(i + 1) % loop.Count]]);
        cdt.Legalize();
        cdt.Classify();
        cdt.Refine(maxEdge);

        // Each new inner point, on the face's own triangles: blended from the corners of the one it lies in.
        var own = Triangulator.TriangulateFace(mesh, f).Select(t => (t.A, t.B, t.C, P: Q(D(mesh.Vertices[t.A])), R: Q(D(mesh.Vertices[t.B])), S: Q(D(mesh.Vertices[t.C])))).ToArray();
        var vertexOf = new int[cdt.PointCount];
        for (var i = 3; i < cdt.PointCount; i++)
        {
            if (i < ids.Count && ids[i] >= 0) { vertexOf[i] = ids[i]; continue; }
            var p = cdt.Point(i);
            var (best, weights) = (-1, (0.0, 0.0, 0.0));
            var bestMin = double.NegativeInfinity;
            for (var k = 0; k < own.Length; k++)
            {
                var tri = own[k];
                var area = Orient(tri.P, tri.R, tri.S);
                if (area == 0) continue;
                var (wa, wb) = (Orient(p, tri.R, tri.S) / area, Orient(tri.P, p, tri.S) / area);
                var wc = 1 - wa - wb;
                var least = Math.Min(wa, Math.Min(wb, wc));
                if (least > bestMin) (best, bestMin, weights) = (k, least, (wa, wb, wc));
            }
            var (ta, tb, tc) = (own[best].A, own[best].B, own[best].C);
            var stencil = Stencil.Of([(ta, weights.Item1), (tb, weights.Item2), (tc, weights.Item3)]);
            vertexOf[i] = mesh.Vertices.Count + added.Count;
            stencils.Add(stencil);
            var position = D(mesh.Vertices[ta]) * weights.Item1 + D(mesh.Vertices[tb]) * weights.Item2 + D(mesh.Vertices[tc]) * weights.Item3;
            added.Add(new Vector3((float)position.X, (float)position.Y, (float)position.Z));
        }
        return cdt.Inside().Select(t => (vertexOf[t.A], vertexOf[t.B], vertexOf[t.C])).ToList();
    }

    /// <summary>
    /// The equilateral lattice at <paramref name="maxEdge"/> over a face's projected loops (<paramref name="loops"/>[0] the
    /// outer), each point kept only inside the face and at least half the spacing clear of every loop segment, in row order.
    /// RV-100: the same points as testing <see cref="PlaneGeometry.Inside"/> and every segment per point, found in about
    /// linear time: a row's loop crossings are worked once, and the segments are bucketed in a grid of half the spacing.
    /// </summary>
    internal static List<Vector2D> Lattice(Vector2D[][] loops, double maxEdge)
    {
        var outer = loops[0];
        var (minX, minY, maxX, maxY) = (outer.Min(p => p.X), outer.Min(p => p.Y), outer.Max(p => p.X), outer.Max(p => p.Y));
        var clear = maxEdge / 2;
        static Vector3D Flat(Vector2D p) => new(p.X, p.Y, 0);
        (int, int) Cell(double x, double y) => ((int)Math.Floor((x - minX) / clear), (int)Math.Floor((y - minY) / clear));
        var grid = new Dictionary<(int, int), List<(Vector3D A, Vector3D B)>>();
        foreach (var loop in loops)
            for (var i = 0; i < loop.Length; i++)
            {
                var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
                var (lo, hi) = (Cell(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), Cell(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
                for (var cx = lo.Item1; cx <= hi.Item1; cx++)
                    for (var cy = lo.Item2; cy <= hi.Item2; cy++)
                    {
                        if (!grid.TryGetValue((cx, cy), out var list)) grid[(cx, cy)] = list = [];
                        list.Add((Flat(a), Flat(b)));
                    }
            }

        var kept = new List<Vector2D>();
        var crossings = loops.Select(_ => new List<double>()).ToArray();
        // A point is in a loop when an odd number of the row's crossings lie to its right, as PlaneGeometry.InPolygon counts.
        static bool In(List<double> row, double x) => row.Count(c => x < c) % 2 == 1;
        var rowStep = maxEdge * Math.Sqrt(3) / 2;
        for (var j = 0; minY + j * rowStep < maxY; j++)
        {
            var y = minY + j * rowStep;
            for (var l = 0; l < loops.Length; l++)
            {
                crossings[l].Clear();
                var loop = loops[l];
                for (var i = 0; i < loop.Length; i++)
                {
                    var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
                    if ((a.Y > y) != (b.Y > y)) crossings[l].Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
            }
            for (var x = minX + (j % 2) * maxEdge / 2; x < maxX; x += maxEdge)
            {
                if (!In(crossings[0], x) || crossings.Skip(1).Any(c => In(c, x))) continue;
                var p = new Vector2D(x, y);
                // A segment nearer than `clear` overlaps the 3 x 3 cells round p; one more ring covers rounding at the cell edges.
                var (px, py) = Cell(x, y);
                var near = false;
                for (var cx = px - 2; cx <= px + 2 && !near; cx++)
                    for (var cy = py - 2; cy <= py + 2 && !near; cy++)
                        near = grid.TryGetValue((cx, cy), out var list) && list.Any(s => SegmentDistance(Flat(p), s.A, s.B) < clear);
                if (!near) kept.Add(p);
            }
        }
        return kept;
    }
}
