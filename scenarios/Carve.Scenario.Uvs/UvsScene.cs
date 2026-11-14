using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Uvs;

/// <summary>One mesh as <c>get_mesh</c> gives it: vertices, face loops, the UVs of the mapped faces, and the seams.</summary>
internal sealed record SceneMesh(string Name, Vector3[] Vertices, int[][] Faces, Dictionary<int, Vector2[]> Uvs, (int A, int B)[] Seams, JsonElement Raw)
{
    /// <summary>A face's Newell normal, not normalised.</summary>
    public Vector3 Normal(int face) => Geo.Normal(Vertices, Faces[face]);

    public Vector3 Centre(int face) => Faces[face].Aggregate(Vector3.Zero, (sum, v) => sum + Vertices[v]) / Faces[face].Length;

    public Vector3 BoundsMin => Vertices.Aggregate(Vector3.Min);

    public Vector3 BoundsMax => Vertices.Aggregate(Vector3.Max);

    /// <summary>The four properties that make a mesh's content (never its name): what the barrel is compared on.</summary>
    public string Content => string.Join("|", new[] { "vertices", "faces", "uvs", "seams" }.Select(k => Raw.TryGetProperty(k, out var p) ? p.GetRawText() : "-"));
}

/// <summary>Every mesh of the model as the scenario read it, and the bounds the camera frames (all of them together).</summary>
internal sealed class Scene(SceneMesh[] meshes)
{
    public SceneMesh[] Meshes { get; } = meshes;

    public Vector3 Min => Meshes.Select(m => m.BoundsMin).Aggregate(Vector3.Min);

    public Vector3 Max => Meshes.Select(m => m.BoundsMax).Aggregate(Vector3.Max);

    public SceneMesh this[string name] => Meshes.Single(m => m.Name == name);

    public static async Task<Scene> ReadAsync(ScenarioDriver driver, params string[] names)
    {
        var meshes = new List<SceneMesh>();
        foreach (var name in names) meshes.Add(await ReadOneAsync(driver, name));
        return new Scene([.. meshes]);
    }

    public static async Task<SceneMesh> ReadOneAsync(ScenarioDriver driver, string name)
    {
        var mesh = await CallJsonAsync(driver, "get_mesh", new { mesh = name });
        var vertices = mesh.GetProperty("vertices").EnumerateArray().Select(v => new Vector3(v.GetProperty("at")[0].GetSingle(), v.GetProperty("at")[1].GetSingle(), v.GetProperty("at")[2].GetSingle())).ToArray();
        var faces = mesh.GetProperty("faces").EnumerateArray().Select(f => f.GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
        var uvs = new Dictionary<int, Vector2[]>();
        if (mesh.TryGetProperty("uvs", out var list))
        {
            foreach (var entry in list.EnumerateArray())
            {
                uvs[entry.GetProperty("face").GetInt32()] = entry.GetProperty("uv").EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToArray();
            }
        }

        var seams = mesh.TryGetProperty("seams", out var s) ? s.EnumerateArray().Select(p => (p[0].GetInt32(), p[1].GetInt32())).ToArray() : [];
        return new SceneMesh(name, vertices, faces, uvs, seams, mesh);
    }

    /// <summary>The islands: faces joined across every edge that is not a seam.</summary>
    public static int Islands(SceneMesh mesh)
    {
        var parent = Enumerable.Range(0, mesh.Faces.Length).ToArray();
        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        var seam = mesh.Seams.Select(p => (Math.Min(p.A, p.B), Math.Max(p.A, p.B))).ToHashSet();
        var owner = new Dictionary<(int, int), int>();
        for (var f = 0; f < mesh.Faces.Length; f++)
        {
            var loop = mesh.Faces[f];
            for (var i = 0; i < loop.Length; i++)
            {
                var e = (Math.Min(loop[i], loop[(i + 1) % loop.Length]), Math.Max(loop[i], loop[(i + 1) % loop.Length]));
                if (seam.Contains(e)) continue;
                if (owner.TryGetValue(e, out var g)) parent[Find(f)] = Find(g); else owner[e] = f;
            }
        }

        return Enumerable.Range(0, mesh.Faces.Length).Select(Find).Distinct().Count();
    }
}

internal static class Geo
{
    public static Vector3 Normal(Vector3[] v, int[] loop)
    {
        var n = Vector3.Zero;
        for (var i = 0; i < loop.Length; i++)
        {
            Vector3 a = v[loop[i]], b = v[loop[(i + 1) % loop.Length]];
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }

        return n;
    }

    /// <summary>The 3D area of a planar face: half its Newell normal's length.</summary>
    public static double Area(SceneMesh m, int face) => m.Normal(face).Length() / 2;

    /// <summary>Whether a point of a face's plane lies inside its loop (even-odd, on the two axes the normal leaves).</summary>
    public static bool Inside(Vector3[] v, int[] loop, Vector3 normal, Vector3 p)
    {
        var a = MathF.Abs(normal.X) >= MathF.Abs(normal.Y) && MathF.Abs(normal.X) >= MathF.Abs(normal.Z) ? 0 : MathF.Abs(normal.Y) >= MathF.Abs(normal.Z) ? 1 : 2;
        Vector2 Drop(Vector3 q) => a switch { 0 => new(q.Y, q.Z), 1 => new(q.X, q.Z), _ => new(q.X, q.Y) };
        var (pp, inside) = (Drop(p), false);
        for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
        {
            Vector2 pi = Drop(v[loop[i]]), pj = Drop(v[loop[j]]);
            if ((pi.Y > pp.Y) != (pj.Y > pp.Y) && pp.X < (pj.X - pi.X) * (pp.Y - pi.Y) / (pj.Y - pi.Y) + pi.X) inside = !inside;
        }

        return inside;
    }

    public static (float U, float V, float W) Barycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
        var den = d00 * d11 - d01 * d01;
        var v = (d11 * d20 - d01 * d21) / den;
        var w = (d00 * d21 - d01 * d20) / den;
        return (1 - v - w, v, w);
    }

    /// <summary>Whether nothing but <paramref name="self"/> crosses the segment from the eye to <paramref name="p"/>, over every face of every mesh.</summary>
    public static bool InSight(Scene scene, SceneMesh selfMesh, int self, Vector3 eye, Vector3 p)
    {
        var d = p - eye;
        foreach (var m in scene.Meshes)
        {
            for (var g = 0; g < m.Faces.Length; g++)
            {
                if (ReferenceEquals(m, selfMesh) && g == self) continue;
                var n = m.Normal(g);
                var across = Vector3.Dot(n, d);
                if (MathF.Abs(across) < 1e-9f) continue;
                var t = Vector3.Dot(n, m.Vertices[m.Faces[g][0]] - eye) / across;
                if (t <= 1e-4f || t >= 1 - 1e-4f) continue;
                if (Inside(m.Vertices, m.Faces[g], n, eye + d * t)) return false;
            }
        }

        return true;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-9f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }
}
