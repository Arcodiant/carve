using System.Numerics;

namespace Carve.Core;

/// <summary>A triangle of a triangulated face; the winding follows the face and <see cref="Face"/> says which face it came from.</summary>
public readonly record struct MeshTriangle(int A, int B, int C, int Face);

/// <summary>Ear-clipping triangulation in the plane of a face's Newell normal. A derived view for render and export; never stored.</summary>
public static class Triangulator
{
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

    internal static bool TryTriangulate(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face, out List<(int A, int B, int C)> triangles)
    {
        triangles = [];
        if (face.Count == 3)
        {
            triangles.Add((face[0], face[1], face[2]));
            return true;
        }

        var normal = PolygonMesh.Normalize(PolygonMesh.NewellVector(verts, face));
        if (normal == Vector3.Zero) return false;

        // Basis (u, v) with u x v = normal, so a face wound CCW about its normal is CCW in 2D.
        var helper = MathF.Abs(normal.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var u = Vector3.Normalize(Vector3.Cross(helper, normal));
        var v = Vector3.Cross(normal, u);

        var size = PolygonMesh.FaceSize(verts, face);
        var eps = 1e-9f * size * size;
        var pts = face.Select(i => new Vector2(Vector3.Dot(verts[i], u), Vector3.Dot(verts[i], v))).ToArray();
        var remaining = Enumerable.Range(0, face.Count).ToList();

        while (remaining.Count > 3)
        {
            var clipped = false;
            for (var r = 0; r < remaining.Count; r++)
            {
                var ia = remaining[(r + remaining.Count - 1) % remaining.Count];
                var ib = remaining[r];
                var ic = remaining[(r + 1) % remaining.Count];
                if (Cross(pts[ia], pts[ib], pts[ic]) <= eps) continue;
                if (ContainsOther(pts, remaining, ia, ib, ic, eps)) continue;

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
                if (MathF.Abs(Cross(pts[ia], pts[remaining[r]], pts[ic])) <= eps) { collinear = r; break; }
            }
            if (collinear < 0) return false;
            remaining.RemoveAt(collinear);
        }

        var a = remaining[0];
        var b = remaining[1];
        var c = remaining[2];
        if (Cross(pts[a], pts[b], pts[c]) <= eps) return false;
        triangles.Add((face[a], face[b], face[c]));
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        var ab = b - a;
        var ac = c - a;
        return ab.X * ac.Y - ab.Y * ac.X;
    }

    private static bool ContainsOther(Vector2[] pts, List<int> remaining, int ia, int ib, int ic, float eps)
    {
        foreach (var i in remaining)
        {
            if (i == ia || i == ib || i == ic) continue;
            var p = pts[i];
            if (p == pts[ia] || p == pts[ib] || p == pts[ic]) continue;
            if (Cross(pts[ia], pts[ib], p) >= -eps && Cross(pts[ib], pts[ic], p) >= -eps && Cross(pts[ic], pts[ia], p) >= -eps)
                return true;
        }
        return false;
    }
}
