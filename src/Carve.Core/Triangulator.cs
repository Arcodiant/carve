using System.Numerics;

namespace Carve.Core;

/// <summary>A triangle of a triangulated face; the winding follows the face and <see cref="Face"/> says which face it came from.</summary>
public readonly record struct MeshTriangle(int A, int B, int C, int Face);

/// <summary>Ear-clipping triangulation in the plane of a face's Newell normal. A derived view for render and export; never stored.</summary>
public static class Triangulator
{
    /// <summary>
    /// A corner is flat, and a point is on a line, when the sine of the angle it makes is at most this. It is judged
    /// against the local edges (or, for a point and a line, the face size), not against the area of the whole face,
    /// so a face of very many short edges keeps all its vertices.
    /// </summary>
    public const double CollinearTolerance = 1e-9;

    /// <summary>Triangles of every face, in face order.</summary>
    public static IReadOnlyList<MeshTriangle> Triangulate(PolygonMesh mesh)
    {
        var result = new List<MeshTriangle>();
        for (var f = 0; f < mesh.Faces.Count; f++)
            result.AddRange(TriangulateFace(mesh, f));
        return result;
    }

    /// <summary>
    /// Triangles of one face. A validated mesh's faces always triangulate. A plain face's loop is ear-clipped; a holed
    /// face's holes are first bridged into its outer loop (<see cref="Bridge"/>) and the one outline is ear-clipped.
    /// </summary>
    public static IReadOnlyList<MeshTriangle> TriangulateFace(PolygonMesh mesh, int face)
    {
        var f = mesh.Faces[face];
        IReadOnlyList<int> outline = f.HasHoles
            ? Bridge(mesh.Vertices, f)?.Ids ?? throw new InvalidOperationException($"faces[{face}] cannot be bridged")
            : f.Loop;
        if (!TryTriangulate(mesh.Vertices, outline, out var tris))
            throw new InvalidOperationException($"faces[{face}] cannot be triangulated");
        return tris.Select(t => new MeshTriangle(t.A, t.B, t.C, face)).ToArray();
    }

    /// <summary>The face's vertices in 2D, in the plane of its Newell normal, counter-clockwise about that normal; null when it has no normal.</summary>
    private static (double X, double Y)[]? Project(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face)
    {
        var normal = PlaneNormal(verts, face);
        if (normal == (0, 0, 0)) return null;
        return ProjectWith(verts, face, normal, verts[face[0]]);
    }

    /// <summary>The vertices <paramref name="ids"/> in 2D, in the plane through <paramref name="o"/> of unit <paramref name="normal"/>.</summary>
    private static (double X, double Y)[] ProjectWith(IReadOnlyList<Vector3> verts, IReadOnlyList<int> ids,
        (double X, double Y, double Z) normal, Vector3 o)
    {
        // Basis (u, v) with u x v = normal, so a face wound CCW about its normal is CCW in 2D.
        var helper = Math.Abs(normal.X) < 0.9 ? (1.0, 0.0, 0.0) : (0.0, 1.0, 0.0);
        var u = PolygonMesh.Unit(Cross3(helper, normal));
        var v = Cross3(normal, u);
        return ids.Select(i =>
        {
            double dx = (double)verts[i].X - o.X, dy = (double)verts[i].Y - o.Y, dz = (double)verts[i].Z - o.Z;
            return (dx * u.X + dy * u.Y + dz * u.Z, dx * v.X + dy * v.Y + dz * v.Z);
        }).ToArray();
    }

    /// <summary>
    /// The unit normal of a face: Newell's, or where the signed area cancels (a bow tie has none) the plane its
    /// corners span, so a crossing can still be found. Zero when the corners are collinear.
    /// </summary>
    private static (double X, double Y, double Z) PlaneNormal(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face)
    {
        var size = PolygonMesh.FaceSize(verts, face);
        var newell = PolygonMesh.NewellVector(verts, face);
        if (PolygonMesh.HasArea(newell, size)) return PolygonMesh.Unit(newell);

        var o = verts[face[0]];
        (double X, double Y, double Z) From(int i) => ((double)verts[i].X - o.X, (double)verts[i].Y - o.Y, (double)verts[i].Z - o.Z);

        var far = From(face.MaxBy(i => PolygonMesh.Length(From(i))));
        var spanned = face.Select(i => Cross3(far, From(i))).MaxBy(PolygonMesh.Length);
        return PolygonMesh.HasArea(spanned, size) ? PolygonMesh.Unit(spanned) : (0, 0, 0);
    }

    private static (double X, double Y, double Z) Cross3((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    internal static bool TryTriangulate(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face, out List<(int A, int B, int C)> triangles)
    {
        triangles = [];
        if (face.Count == 3)
        {
            triangles.Add((face[0], face[1], face[2]));
            return true;
        }

        var pts = Project(verts, face);
        if (pts is null) return false;
        var size = PolygonMesh.FaceSize(verts, face);
        var remaining = Enumerable.Range(0, face.Count).ToList();

        while (remaining.Count > 3)
        {
            var clipped = false;
            for (var r = 0; r < remaining.Count; r++)
            {
                var ia = remaining[(r + remaining.Count - 1) % remaining.Count];
                var ib = remaining[r];
                var ic = remaining[(r + 1) % remaining.Count];
                if (!IsConvex(pts[ia], pts[ib], pts[ic])) continue;
                if (ContainsOther(pts, remaining, ia, ib, ic, size)) continue;

                triangles.Add((face[ia], face[ib], face[ic]));
                remaining.RemoveAt(r);
                clipped = true;
                break;
            }
            if (clipped) continue;

            // No ear: drop a collinear vertex (it adds no area), else the outline is unclippable.
            var collinear = -1;
            for (var r = 0; r < remaining.Count; r++)
            {
                var ia = remaining[(r + remaining.Count - 1) % remaining.Count];
                var ic = remaining[(r + 1) % remaining.Count];
                if (IsStraightThrough(pts[ia], pts[remaining[r]], pts[ic])) { collinear = r; break; }
            }
            if (collinear < 0) return false;
            remaining.RemoveAt(collinear);
        }

        var a = remaining[0];
        var b = remaining[1];
        var c = remaining[2];
        if (!IsConvex(pts[a], pts[b], pts[c])) return false;
        triangles.Add((face[a], face[b], face[c]));
        return true;
    }

    /// <summary>
    /// The first fault of a face's outline in the plane of its normal, as the position of each edge's first vertex
    /// (edge k runs from face[k] to face[k+1]): two non-adjacent edges that properly cross, else two edges that fold
    /// back on each other (adjacent edges that turn back 180 degrees, or collinear edges that overlap along a length).
    /// Edges that only touch (a vertex on another edge) are neither, and nor is a bridge, the way a hole is written
    /// in one outline: two edges that are the same two points walked in opposite directions. Each end of a bridge is
    /// a vertex on both sides, so no vertex lies part way along an edge it turned back over, and none is lost.
    /// Which way the loop past a bridge winds is not judged here: <see cref="CoversTwice"/> refuses the face whose
    /// hole winds the same way as its outline, after clipping. Null when the outline has neither fault.
    /// </summary>
    internal static (int First, int Second, bool Fold)? FindCrossing(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face)
    {
        if (face.Count < 4) return null;
        var pts = Project(verts, face);
        if (pts is null) return null;
        var size = PolygonMesh.FaceSize(verts, face);
        var n = pts.Length;

        for (var i = 0; i < n; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;
                var c = pts[j];
                var d = pts[(j + 1) % n];
                if (Disjoint(a, b, c, d)) continue;
                if (Side(c, d, a, size) * Side(c, d, b, size) < 0 && Side(a, b, c, size) * Side(a, b, d, size) < 0)
                    return (i, j, false);
            }
        }

        for (var i = 0; i < n; i++)
        {
            // Adjacent edges i-1 and i meet at i: a fold when both leave that vertex the same way.
            if (IsFlat(pts[(i + n - 1) % n], pts[i], pts[(i + 1) % n]) && Dot(pts[(i + n - 1) % n], pts[i], pts[(i + 1) % n]) > 0)
                return ((i + n - 1) % n, i, true);
        }

        for (var i = 0; i < n; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            var length = Distance(a, b);
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;
                var c = pts[j];
                var d = pts[(j + 1) % n];
                if (Disjoint(a, b, c, d)) continue;
                if (Side(a, b, c, size) != 0 || Side(a, b, d, size) != 0 || length == 0) continue;
                if (Distance(a, d) <= CollinearTolerance * size && Distance(b, c) <= CollinearTolerance * size) continue;
                // Both ends of c-d are on the line a-b: the overlap along it, as a length.
                var tc = ((c.X - a.X) * (b.X - a.X) + (c.Y - a.Y) * (b.Y - a.Y)) / length;
                var td = ((d.X - a.X) * (b.X - a.X) + (d.Y - a.Y) * (b.Y - a.Y)) / length;
                var overlap = Math.Min(length, Math.Max(tc, td)) - Math.Max(0, Math.Min(tc, td));
                if (overlap > CollinearTolerance * size) return (i, j, true);
            }
        }
        return null;
    }

    /// <summary>
    /// Whether a face's triangles cover some of its area twice. Clipping an ear a-b-c off an outline splits it, as a
    /// closed path, into the ear and the rest, so at every point the triangles covering it add up to the outline's
    /// winding number there; and every ear turns left, so each counts +1. The winding number is at most 1
    /// everywhere, the face a region covered once (a hole joined by a bridge wound opposite to the outline goes to
    /// 0), exactly when no two triangles overlap in their interiors. Two triangles share no interior when an edge
    /// of one has all three corners of the other on or right of its line ("on" as in <see cref="FindCrossing"/>),
    /// so triangles that meet along an edge or at a point are apart.
    /// </summary>
    internal static bool CoversTwice(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face, IReadOnlyList<(int A, int B, int C)> triangles)
    {
        if (triangles.Count < 2) return false;
        var pts = Project(verts, face);
        if (pts is null) return false;
        var size = PolygonMesh.FaceSize(verts, face);
        var at = new Dictionary<int, (double X, double Y)>();
        for (var k = 0; k < face.Count; k++) at[face[k]] = pts[k];

        var tris = triangles
            .Select(t => (A: at[t.A], B: at[t.B], C: at[t.C]))
            .Select(t => (t.A, t.B, t.C, MinX: Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)), MaxX: Math.Max(t.A.X, Math.Max(t.B.X, t.C.X))))
            .OrderBy(t => t.MinX)
            .ToArray();

        // Sweep along x: only triangles whose x ranges meet can overlap.
        for (var i = 0; i < tris.Length; i++)
        {
            for (var j = i + 1; j < tris.Length && tris[j].MinX <= tris[i].MaxX; j++)
            {
                var s = tris[i];
                var t = tris[j];
                if (!Apart(s.A, s.B, s.C, t.A, t.B, t.C, size) && !Apart(t.A, t.B, t.C, s.A, s.B, s.C, size)) return true;
            }
        }
        return false;
    }

    /// <summary>Some edge of the left-turning triangle a-b-c has p, q and r all on or right of its line.</summary>
    private static bool Apart(
        (double X, double Y) a, (double X, double Y) b, (double X, double Y) c,
        (double X, double Y) p, (double X, double Y) q, (double X, double Y) r, double size)
    {
        bool Separates((double X, double Y) e0, (double X, double Y) e1)
            => Side(e0, e1, p, size) <= 0 && Side(e0, e1, q, size) <= 0 && Side(e0, e1, r, size) <= 0;
        return Separates(a, b) || Separates(b, c) || Separates(c, a);
    }

    private static bool Disjoint((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
        => Math.Max(a.X, b.X) < Math.Min(c.X, d.X) || Math.Max(c.X, d.X) < Math.Min(a.X, b.X)
        || Math.Max(a.Y, b.Y) < Math.Min(c.Y, d.Y) || Math.Max(c.Y, d.Y) < Math.Min(a.Y, b.Y);

    /// <summary>Which side of the line a-b the point p is on (1 left, -1 right), 0 when it is within the tolerance of the line.</summary>
    private static int Side((double X, double Y) a, (double X, double Y) b, (double X, double Y) p, double size)
    {
        var cross = Cross(a, b, p);
        return Math.Abs(cross) <= CollinearTolerance * size * Distance(a, b) ? 0 : Math.Sign(cross);
    }

    private static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static double Distance((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    /// <summary>A left turn at b that is not flat, judged against the lengths of the two edges.</summary>
    private static bool IsConvex((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
        => Cross(a, b, c) > CollinearTolerance * Distance(a, b) * Distance(b, c);

    private static bool IsFlat((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
        => Math.Abs(Cross(a, b, c)) <= CollinearTolerance * Distance(a, b) * Distance(b, c);

    /// <summary>A flat corner at b whose neighbours lie on opposite sides of it (not a fold, where they lie on the same side).</summary>
    private static bool IsStraightThrough((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
        => IsFlat(a, b, c) && Dot(a, b, c) < 0;

    /// <summary>Dot product of b-&gt;a and b-&gt;c: positive when the two edges leave b the same way.</summary>
    private static double Dot((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
        => (a.X - b.X) * (c.X - b.X) + (a.Y - b.Y) * (c.Y - b.Y);

    private static bool ContainsOther((double X, double Y)[] pts, List<int> remaining, int ia, int ib, int ic, double size)
    {
        foreach (var i in remaining)
        {
            if (i == ia || i == ib || i == ic) continue;
            var p = pts[i];
            if (p == pts[ia] || p == pts[ib] || p == pts[ic]) continue;
            if (Inside(pts[ia], pts[ib], p, size) && Inside(pts[ib], pts[ic], p, size) && Inside(pts[ic], pts[ia], p, size))
                return true;
        }
        return false;
    }

    /// <summary>p is left of a-b, or within the tolerance (of the face size) of the line on its right.</summary>
    private static bool Inside((double X, double Y) a, (double X, double Y) b, (double X, double Y) p, double size)
        => Cross(a, b, p) >= -CollinearTolerance * size * Distance(a, b);

    /// <summary>
    /// A holed face as one outline: <see cref="Ids"/> the vertex ids in order, and <see cref="Corners"/> the face corner
    /// (an index into <see cref="Face.Corners"/>) each position came from. A bridge vertex appears twice.
    /// </summary>
    internal sealed record Bridged(int[] Ids, int[] Corners);

    /// <summary>
    /// Each hole of <paramref name="face"/> (its <c>faces[<paramref name="faceId"/>]</c>) refused for its winding (not
    /// clockwise against the outer loop, seen from outside), for not lying strictly inside the outer loop, or for
    /// meeting another hole. "Meeting" counts touching: a point within <see cref="CollinearTolerance"/> of the face size
    /// of another loop's edge, the "on" of <see cref="FindCrossing"/>. Assumes the loops' indices are valid and the outer
    /// loop has an area.
    /// </summary>
    internal static List<MeshError> HoleProblems(IReadOnlyList<Vector3> verts, Face face, int faceId)
    {
        var errors = new List<MeshError>();
        var normal = PlaneNormal(verts, face.Loop);
        var o = verts[face.Loop[0]];
        var size = PolygonMesh.FaceSize(verts, face.Loop);
        var outer = ProjectWith(verts, face.Loop, normal, o);
        var holes = face.Holes.Select(h => ProjectWith(verts, h, normal, o)).ToArray();
        var ok = new bool[holes.Length];
        for (var k = 0; k < holes.Length; k++)
        {
            var name = Face.LoopName(faceId, k);
            if (SignedArea(holes[k]) > 0)
                errors.Add(new MeshError(name,
                    "hole winds the same way as the face's outer loop: seen from outside, a hole winds clockwise"));
            else if (LoopsMeet(outer, holes[k], size) || !InsidePolygon(outer, holes[k][0]))
                errors.Add(new MeshError(name,
                    $"hole is not strictly inside the outer loop of faces[{faceId}]: it crosses, touches or lies outside it"));
            else
                ok[k] = true;
        }

        for (var k = 0; k < holes.Length; k++)
        {
            for (var j = 0; j < k && ok[k]; j++)
            {
                if (!ok[j]) continue;
                if (LoopsMeet(holes[j], holes[k], size) || InsidePolygon(holes[j], holes[k][0]) || InsidePolygon(holes[k], holes[j][0]))
                    errors.Add(new MeshError(Face.LoopName(faceId, k),
                        $"hole overlaps or touches {Face.LoopName(faceId, j)}: the holes of a face must lie apart"));
            }
        }
        return errors;
    }

    /// <summary>
    /// The holes of <paramref name="face"/> bridged into its outer loop, the standard way: holes are taken by their
    /// rightmost vertex (in the plane of the outer loop), rightmost first, and each such vertex is joined to the nearest
    /// vertex of the outline so far that it sees: the join lies inside that vertex's corner and meets no edge of the
    /// outline or of any hole left. The outline then runs to the join, round the hole from its rightmost vertex and back
    /// to it, and back along the join. Null when some hole sees no vertex. Plain faces are not bridged.
    /// </summary>
    internal static Bridged? Bridge(IReadOnlyList<Vector3> verts, Face face)
    {
        var normal = PlaneNormal(verts, face.Loop);
        if (normal == (0, 0, 0)) return null;
        var size = PolygonMesh.FaceSize(verts, face.Loop);
        var pts = ProjectWith(verts, face.Corners, normal, verts[face.Loop[0]]);
        var combined = Enumerable.Range(0, face.Loop.Count).ToList();

        var holeCorners = Enumerable.Range(0, face.Holes.Count)
            .Select(h => Enumerable.Range(0, face.Holes[h].Count).Select(i => face.CornerAt(h, i)).ToArray())
            .ToArray();
        var order = Enumerable.Range(0, holeCorners.Length)
            .OrderByDescending(h => holeCorners[h].Max(c => pts[c].X)).ThenBy(h => h).ToList();
        var left = new HashSet<int>(order);

        foreach (var h in order)
        {
            var cs = holeCorners[h];
            var m = 0;
            for (var i = 1; i < cs.Length; i++) if (pts[cs[i]].X > pts[cs[m]].X) m = i;
            var mc = cs[m];
            var at = pts[mc];

            var best = -1;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < combined.Count; i++)
            {
                var p = pts[combined[i]];
                var d = Distance(at, p);
                if (d >= bestDistance) continue;
                var prev = pts[combined[(i + combined.Count - 1) % combined.Count]];
                var next = pts[combined[(i + 1) % combined.Count]];
                if (!InCone(prev, p, next, at)) continue;
                if (Blocked(at, p, size, combined, pts, skipAt: p)) continue;
                if (left.Any(other => Blocked(at, p, size, holeCorners[other], pts, skipAt: at))) continue;
                best = i;
                bestDistance = d;
            }
            if (best < 0) return null;

            var splice = new List<int>(cs.Length + 2);
            for (var i = 0; i <= cs.Length; i++) splice.Add(cs[(m + i) % cs.Length]);
            splice.Add(combined[best]);
            combined.InsertRange(best + 1, splice);
            left.Remove(h);
        }
        return new Bridged(combined.Select(c => face.Corners[c]).ToArray(), [.. combined]);
    }

    /// <summary>Whether the join a-b meets an edge of the closed loop <paramref name="loop"/> (corner indices into <paramref name="pts"/>), leaving out the edges that end at <paramref name="skipAt"/>.</summary>
    private static bool Blocked((double X, double Y) a, (double X, double Y) b, double size, IReadOnlyList<int> loop,
        (double X, double Y)[] pts, (double X, double Y) skipAt)
    {
        for (var k = 0; k < loop.Count; k++)
        {
            var c = pts[loop[k]];
            var d = pts[loop[(k + 1) % loop.Count]];
            if (c == skipAt || d == skipAt) continue;
            if (SegmentsMeet(a, b, c, d, size)) return true;
        }
        return false;
    }

    /// <summary>Whether p lies strictly inside the corner at b of a loop running a, b, c counter-clockwise.</summary>
    private static bool InCone((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) p)
        => Cross(a, b, c) > 0
            ? Cross(a, b, p) > 0 && Cross(b, c, p) > 0
            : !(Cross(a, b, p) <= 0 && Cross(b, c, p) <= 0);

    /// <summary>Twice-signed area of a 2D loop: positive counter-clockwise.</summary>
    private static double SignedArea((double X, double Y)[] loop)
    {
        double area = 0;
        for (var k = 0; k < loop.Length; k++)
        {
            var a = loop[k];
            var b = loop[(k + 1) % loop.Length];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area;
    }

    /// <summary>Whether some edge of one loop meets (crosses or touches) some edge of the other.</summary>
    private static bool LoopsMeet((double X, double Y)[] p, (double X, double Y)[] q, double size)
    {
        for (var i = 0; i < p.Length; i++)
            for (var j = 0; j < q.Length; j++)
                if (SegmentsMeet(p[i], p[(i + 1) % p.Length], q[j], q[(j + 1) % q.Length], size)) return true;
        return false;
    }

    /// <summary>Whether segments a-b and c-d cross or touch, "on" within the tolerance <see cref="Side"/> uses.</summary>
    private static bool SegmentsMeet((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d, double size)
    {
        var d1 = Side(a, b, c, size);
        var d2 = Side(a, b, d, size);
        var d3 = Side(c, d, a, size);
        var d4 = Side(c, d, b, size);
        if (d1 == 0 && d2 == 0 && d3 == 0 && d4 == 0) return !Disjoint(a, b, c, d);
        return d1 * d2 <= 0 && d3 * d4 <= 0;
    }

    /// <summary>Whether p is inside the loop by the even-odd rule; for a point on no edge of the loop.</summary>
    private static bool InsidePolygon((double X, double Y)[] loop, (double X, double Y) p)
    {
        var inside = false;
        for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
        {
            var a = loop[i];
            var b = loop[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
