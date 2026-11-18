using System.Numerics;
using System.Text.Json;
using Carve.Scenario.Crate;

namespace Carve.Scenario.Acceptance;

/// <summary>
/// The positive check a mirrored or flipped camera fails (T-49's Notes, RV-52): the character's silhouette at rest, worked out
/// here from the saved meshes' faces through <see cref="CameraPixels.Perspective"/> under <c>render</c>'s framing (the box of
/// every mesh at rest), is compared with each frame's foreground (every pixel not the background). A camera fault that
/// <c>render</c> and <c>render_glb</c> share passes their comparison, but not this.
/// </summary>
internal static class Silhouette
{
    /// <summary>A frame must overlap the prediction this much (intersection over union); a view only tells a mirror or flip apart when the prediction overlaps its own mirror or flip less.</summary>
    public const double Least = 0.9;

    /// <summary>
    /// One mesh as <c>get_mesh</c> gives it: a deleted vertex is null in its slot, and each live face is its loops, the outer
    /// loop first and then each hole (a deleted face is left out; no other face refers to it).
    /// </summary>
    public sealed record Mesh(Vector3?[] Vertices, int[][][] Faces);

    /// <summary>Reads <c>get_mesh</c>'s JSON, keeping its null slots and its holed faces ({loop, holes}) (RV-53).</summary>
    public static Mesh Read(JsonElement mesh)
    {
        static int[] Ids(JsonElement ids) => ids.EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var vertices = mesh.GetProperty("vertices").EnumerateArray().Select(v =>
        {
            if (v.ValueKind == JsonValueKind.Null)
            {
                return (Vector3?)null;
            }

            var at = v.GetProperty("at");
            return new Vector3(at[0].GetSingle(), at[1].GetSingle(), at[2].GetSingle());
        }).ToArray();
        var faces = mesh.GetProperty("faces").EnumerateArray().Where(f => f.ValueKind != JsonValueKind.Null).Select(f =>
            f.TryGetProperty("loop", out var loop)
                ? new[] { Ids(loop) }.Concat(f.GetProperty("holes").EnumerateArray().Select(Ids)).ToArray()
                : new[] { Ids(f.GetProperty("vertices")) }).ToArray();
        return new Mesh(vertices, faces);
    }

    /// <summary>The pixels whose centre lies inside a projected face of any mesh, row by row from the top left.</summary>
    public static bool[] Predict(IReadOnlyList<Mesh> meshes, Vector3 direction, int size)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var v in meshes.SelectMany(m => m.Vertices))
        {
            if (v is { } live)
            {
                min = Vector3.Min(min, live);
                max = Vector3.Max(max, live);
            }
        }

        var project = CameraPixels.Perspective(min, max, direction, size);
        var inside = new bool[size * size];
        foreach (var (vertices, faces) in meshes)
        {
            var at = vertices.Select(v => v is { } live ? project(live) : default).ToArray();
            foreach (var face in faces)
            {
                if (face.Length == 1)
                {
                    // A fan from the first corner: the faces are planar and convex enough for their silhouette.
                    var loop = face[0];
                    for (var k = 1; k + 1 < loop.Length; k++)
                    {
                        Fill(inside, size, at[loop[0]], at[loop[k]], at[loop[k + 1]]);
                    }
                }
                else
                {
                    // A holed face is drawn from its loop and its holes, not its loop alone: a pixel is inside when its centre
                    // crosses the face's edges, outer loop and holes together, an odd number of times.
                    FillHoled(inside, size, face.Select(l => l.Select(i => at[i]).ToArray()).ToArray());
                }
            }
        }

        return inside;
    }

    public static double Overlap(bool[] a, bool[] b)
    {
        var (both, either) = (0, 0);
        for (var i = 0; i < a.Length; i++)
        {
            both += a[i] && b[i] ? 1 : 0;
            either += a[i] || b[i] ? 1 : 0;
        }

        return either == 0 ? 1 : (double)both / either;
    }

    /// <summary>Left for right, as a camera with its X axis reversed would draw it.</summary>
    public static bool[] Mirror(bool[] m, int size) => Remap(m, size, (x, y) => (size - 1 - x, y));

    /// <summary>Top for bottom, as a camera with its Y axis reversed would draw it.</summary>
    public static bool[] Flip(bool[] m, int size) => Remap(m, size, (x, y) => (x, size - 1 - y));

    private static bool[] Remap(bool[] m, int size, Func<int, int, (int X, int Y)> from)
    {
        var r = new bool[m.Length];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var (fx, fy) = from(x, y);
                r[y * size + x] = m[fy * size + fx];
            }
        }

        return r;
    }

    private static void FillHoled(bool[] inside, int size, Vector2[][] loops)
    {
        var outer = loops[0];
        var x0 = Math.Max(0, (int)MathF.Floor(outer.Min(p => p.X)));
        var x1 = Math.Min(size - 1, (int)MathF.Ceiling(outer.Max(p => p.X)));
        var y0 = Math.Max(0, (int)MathF.Floor(outer.Min(p => p.Y)));
        var y1 = Math.Min(size - 1, (int)MathF.Ceiling(outer.Max(p => p.Y)));
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var odd = false;
                foreach (var loop in loops)
                {
                    for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
                    {
                        var (a, b) = (loop[i], loop[j]);
                        if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y))
                        {
                            odd = !odd;
                        }
                    }
                }

                if (odd)
                {
                    inside[y * size + x] = true;
                }
            }
        }
    }

    private static void Fill(bool[] inside, int size, Vector2 a, Vector2 b, Vector2 c)
    {
        var area = Cross(b - a, c - a);
        if (MathF.Abs(area) < 1e-9f)
        {
            return;
        }

        var x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        var x1 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        var y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        var y1 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var (u, v, w) = (Cross(b - a, p - a) / area, Cross(c - b, p - b) / area, Cross(a - c, p - c) / area);
                if (u >= 0 && v >= 0 && w >= 0)
                {
                    inside[y * size + x] = true;
                }
            }
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
