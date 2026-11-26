using System.Globalization;
using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Uvs;

namespace Carve.Scenario.Weathered;

/// <summary>A texel of a bake, the point of the mesh it lies on, and the pixel of the 1024 <c>uvs</c> look that shows it.</summary>
internal readonly record struct Texel(int Face, Vector3 P, int Px, int Py, bool Top);

/// <summary>One camera of <c>render</c> as the checks work it: a direction, a zoom about a target, a tile of a sheet.</summary>
internal sealed record Camera(Vector3 Direction, int Size = WeatheredSets.Shaded, float Zoom = 1, Vector3? Target = null, int Left = 0, int Top = 0)
{
    /// <summary>The arguments of <c>render</c> for this camera alone (not a sheet's tile).</summary>
    public object Args() => Target is { } t
        ? new { direction = new[] { Direction.X, Direction.Y, Direction.Z }, zoom = (double)Zoom, target = new[] { t.X, t.Y, t.Z }, size = Size, mode = "shaded" }
        : new { direction = new[] { Direction.X, Direction.Y, Direction.Z }, size = Size, mode = "shaded" };

    /// <summary>
    /// The camera the checks predict from: this one, or under the <c>mirror</c> break the same mirrored top to bottom (the barrel is symmetric
    /// about its axis and top to bottom, so only the caps' end grain and stage 3's one-hoop edit can tell).
    /// </summary>
    public Camera Predicted(Vector3 min, Vector3 max) => WeatheredRun.Break != "mirror" ? this
        : this with { Direction = Direction with { Y = -Direction.Y }, Target = Target is { } t ? t with { Y = min.Y + max.Y - t.Y } : null };
}

/// <summary>
/// The research's texel sets, now the checks' (the Scope's step 2): the texels of a bake at size n whose centres lie inside a face's UVs at least
/// 2 px of the 1024 <c>uvs</c> look from its UV edges, sorted by where on the face their point lies; and the ruled thresholds over them.
/// </summary>
internal static class WeatheredSets
{
    public const int UvFrame = 1024, Shaded = 512;
    public const string Rim = "hoop rim", HoopMiddle = "hoop top middle", Beside = "stave beside hoop", StaveMiddle = "stave middle";
    public static readonly string[] Names = [Rim, HoopMiddle, Beside, StaveMiddle];

    public static double Luma((byte R, byte G, byte B, byte A) p) => 0.2126 * p.R + 0.7152 * p.G + 0.0722 * p.B;

    public static (int X, int Y) UvPixel(Vector2 uv) => ((int)(UvFrame / 16.0 + uv.X * UvFrame * 7 / 8.0), (int)(UvFrame / 16.0 + uv.Y * UvFrame * 7 / 8.0));

    public static Dictionary<string, Texel[]> Of(SceneMesh mesh, int n)
    {
        var staveY = Barrel.Staves.SelectMany(f => mesh.Faces[f]).Select(v => mesh.Vertices[v].Y).ToArray();
        var (y0, y1) = (staveY.Min(), staveY.Max());
        var named = Names.ToDictionary(k => k, _ => new List<Texel>());
        var scale = UvFrame * 7 / 8.0 / n;
        foreach (var f in Barrel.Hoops.Concat(Barrel.Staves))
        {
            if (!mesh.Uvs.TryGetValue(f, out var uv)) continue;
            var loop = mesh.Faces[f];
            var ys = loop.Select(v => mesh.Vertices[v].Y).ToArray();
            var (fy0, fy1) = (ys.Min(), ys.Max());
            var (lo, hi) = (uv.Aggregate(Vector2.Min), uv.Aggregate(Vector2.Max));
            for (var ty = Math.Max(0, (int)(lo.Y * n)); ty <= Math.Min(n - 1, (int)(hi.Y * n)); ty++)
            for (var tx = Math.Max(0, (int)(lo.X * n)); tx <= Math.Min(n - 1, (int)(hi.X * n)); tx++)
            {
                var c = new Vector2((tx + 0.5f) / n, (ty + 0.5f) / n);
                if (Enumerable.Range(0, uv.Length).Any(k => Geo.DistanceToSegment(c, uv[k], uv[(k + 1) % uv.Length]) * n * scale < 2)) continue;
                Vector3? p = null;
                for (var k = 1; k < loop.Length - 1 && p is null; k++)
                {
                    var b = Bary(c, uv[0], uv[k], uv[k + 1]);
                    if (b.X >= 0 && b.Y >= 0 && b.Z >= 0) p = mesh.Vertices[loop[0]] * b.X + mesh.Vertices[loop[k]] * b.Y + mesh.Vertices[loop[k + 1]] * b.Z;
                }

                if (p is not { } q) continue;
                var (px, py) = UvPixel(c);
                var texel = new Texel(f, q, px, py, q.Y > (y0 + y1) / 2);
                if (Barrel.Hoops.Contains(f))
                {
                    var t = (q.Y - fy0) / (fy1 - fy0);
                    if (t < 0.15f || t > 0.85f) named[Rim].Add(texel);
                    else if (t > 0.4f && t < 0.6f) named[HoopMiddle].Add(texel);
                    continue;
                }

                if (Math.Min(q.Y - y0, y1 - q.Y) < 0.02f) named[Beside].Add(texel);
                else if (Math.Abs(q.Y - (y0 + y1) / 2) < 0.03f) named[StaveMiddle].Add(texel);
            }
        }

        return named.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    /// <summary>The value (0 to 1) of each texel of a set in a <c>uvs</c> look of a graph output.</summary>
    public static double[] Values(ScenarioFrame uvs, IEnumerable<Texel> texels) => [.. texels.Select(t => uvs.Pixel(t.Px, t.Py).R / 255.0)];

    /// <summary>
    /// The pixels of a frame (or of a sheet's tile) that show the texels of a set: every third point, facing the camera and in its sight, under
    /// the camera the checks predict from.
    /// </summary>
    public static (Texel Texel, (byte R, byte G, byte B, byte A) Pixel)[] Seen(ScenarioFrame frame, Scene scene, SceneMesh mesh, Camera camera, IEnumerable<Texel> texels)
    {
        var (min, max) = (mesh.BoundsMin, mesh.BoundsMax);
        var c = camera.Predicted(min, max);
        var project = CameraPixels.Perspective(min, max, c.Direction, c.Size, c.Zoom, c.Target);
        var eye = CameraPixels.Eye(min, max, c.Direction, c.Target);
        return [.. texels.Where((t, i) => i % 3 == 0 && Vector3.Dot(mesh.Normal(t.Face), eye - t.P) > 0 && Geo.InSight(scene, mesh, t.Face, eye, t.P))
            .Select(t => (t, CameraPixels.Holding(project(t.P))))
            .Where(e => e.Item2.X >= 0 && e.Item2.Y >= 0 && e.Item2.X < c.Size && e.Item2.Y < c.Size)
            .Select(e => (e.t, frame.Pixel(e.Item2.X + c.Left, e.Item2.Y + c.Top)))];
    }

    public static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

    private static Vector3 Bary(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (MathF.Abs(det) < 1e-12f) return new Vector3(-1);
        var l0 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
        var l1 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
        return new Vector3(l0, l1, 1 - l0 - l1);
    }
}
