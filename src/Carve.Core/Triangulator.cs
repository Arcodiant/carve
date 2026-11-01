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

    /// <summary>Triangles of one face. A validated mesh's faces always triangulate.</summary>
    public static IReadOnlyList<MeshTriangle> TriangulateFace(PolygonMesh mesh, int face)
    {
        if (!TryTriangulate(mesh.Vertices, mesh.Faces[face], out var tris))
            throw new InvalidOperationException($"faces[{face}] cannot be triangulated");
        return tris.Select(t => new MeshTriangle(t.A, t.B, t.C, face)).ToArray();
    }

    /// <summary>The face's vertices in 2D, in the plane of its Newell normal, counter-clockwise about that normal; null when it has no normal.</summary>
    private static (double X, double Y)[]? Project(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face)
    {
        var normal = PlaneNormal(verts, face);
        if (normal == (0, 0, 0)) return null;

        // Basis (u, v) with u x v = normal, so a face wound CCW about its normal is CCW in 2D.
        var helper = Math.Abs(normal.X) < 0.9 ? (1.0, 0.0, 0.0) : (0.0, 1.0, 0.0);
        var u = PolygonMesh.Unit(Cross3(helper, normal));
        var v = Cross3(normal, u);
        var o = verts[face[0]];
        return face.Select(i =>
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
    /// Null when the outline has neither fault.
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
}
