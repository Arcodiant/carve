using System.Numerics;
using Carve.Debug;
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

    /// <summary>The pixels whose centre lies inside a projected face of any mesh, row by row from the top left.</summary>
    public static bool[] Predict(IReadOnlyList<(Vector3[] Vertices, int[][] Faces)> meshes, Vector3 direction, int size)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var v in meshes.SelectMany(m => m.Vertices))
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        var project = CameraPixels.Perspective(min, max, direction, size);
        var inside = new bool[size * size];
        foreach (var (vertices, faces) in meshes)
        {
            var at = vertices.Select(project).ToArray();
            foreach (var face in faces)
            {
                // A fan from the first corner: the faces are planar and convex enough for their silhouette.
                for (var k = 1; k + 1 < face.Length; k++)
                {
                    Fill(inside, size, at[face[0]], at[face[k]], at[face[k + 1]]);
                }
            }
        }

        return inside;
    }

    /// <summary>The frame's pixels that are not the background.</summary>
    public static bool[] Foreground(ScenarioFrame frame)
    {
        var mask = new bool[frame.Width * frame.Height];
        for (var i = 0; i < mask.Length; i++)
        {
            var p = frame.Pixels.AsSpan(i * 4, 4);
            mask[i] = !CrateBuild.IsBackground((p[0], p[1], p[2], p[3]));
        }

        return mask;
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
