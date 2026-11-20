using System.Globalization;
using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Uvs;
using StbImageSharp;

namespace Carve.Scenario.Bakes;

/// <summary>A PNG as read back from disk: RGBA8, row-major, top row first.</summary>
internal sealed record Texels(int Width, int Height, byte[] Rgba)
{
    public static Texels Read(string path)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        return new Texels(image.Width, image.Height, image.Data);
    }

    public (int R, int G, int B) At(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (Rgba[i], Rgba[i + 1], Rgba[i + 2]);
    }
}

/// <summary>
/// The checks the stages share. A failed check is added to <see cref="Failures"/> and the run goes on, so one run names every
/// failure. Every expected pixel is worked here from <c>get_mesh</c>'s geometry, UVs and groups and render's framing rule
/// (<see cref="CameraPixels"/>), never read from the server.
/// </summary>
internal static class BakesChecks
{
    public static readonly List<string> Failures = [];

    /// <summary>
    /// <c>CARVE_BAKES_BREAK</c>: <c>swap</c> bakes the base colour with <c>steel</c> and <c>red</c> swapped (the checks still expect
    /// the right colours); <c>faint</c> bakes <c>steel</c> a dimmer grey and <c>red</c> a duller red, under the ruled steel margin
    /// and red lead but still grey and still red-leading; <c>mirror</c> works every render sample through a camera mirrored left to right.
    /// </summary>
    public static string? Break { get; } = Environment.GetEnvironmentVariable("CARVE_BAKES_BREAK");

    public static void Note(string message) => Failures.Add(message);

    /// <summary>Rec. 709 luma of the sRGB bytes, as D-6's frame rule reads a pixel.</summary>
    public static double Luma((int R, int G, int B) c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

    /// <summary>How far red leads the larger of green and blue; above 0 is red-dominant.</summary>
    public static int RedLead((int R, int G, int B) c) => c.R - Math.Max(c.G, c.B);

    public static (int R, int G, int B) Hex(string hex) =>
        (int.Parse(hex[1..3], NumberStyles.HexNumber), int.Parse(hex[3..5], NumberStyles.HexNumber), int.Parse(hex[5..7], NumberStyles.HexNumber));

    private static bool Near((int R, int G, int B) a, (int R, int G, int B) b, int by) =>
        Math.Abs(a.R - b.R) <= by && Math.Abs(a.G - b.G) <= by && Math.Abs(a.B - b.B) <= by;

    private static (int R, int G, int B) Rgb(ScenarioFrame frame, int x, int y)
    {
        var p = frame.Pixel(x, y);
        return (p.R, p.G, p.B);
    }

    // ---- The uvs look with an image under it: T-121's block rule. ----

    /// <summary>
    /// T-121's rule for <c>render mode=uvs image=</c>. The window is the unit square grown by 8/7, so at frame size S the square is
    /// 7S/8 pixels from S/16; frame pixel x has its centre at u = (x + 0.5 - S/16) / (7S/8). The look samples nearest with no mips,
    /// so the pixel shows texel <c>floor(u * width)</c> as stored inside an island (the faces' UV polygons) and each channel
    /// <c>(c + 1) / 2</c> outside, within 2. A pixel is taken only when its centre lies a quarter to three quarters into its texel on
    /// each axis and 4 pixels or more from every face edge, whose lines are drawn over the image (T-121's placement).
    /// <para>
    /// Inside an island the frame is also held to <paramref name="worked"/>, the face's colour worked from its groups, so a bake of
    /// the wrong colours fails even though the frame shows the PNG faithfully. Returns the counts (inside, outside).
    /// </para>
    /// </summary>
    public static (int Inside, int Outside) CheckUvImage(string what, ScenarioFrame frame, SceneMesh mesh, Texels png, Func<int, (int R, int G, int B)> worked)
    {
        var size = frame.Width;
        double origin = size / 16.0, side = size * 7 / 8.0;
        Vector2 Pixel(Vector2 uv) => new((float)(origin + uv.X * side), (float)(origin + uv.Y * side));
        var polygons = mesh.Uvs.ToDictionary(e => e.Key, e => e.Value.Select(Pixel).ToArray());
        var boxes = polygons.ToDictionary(e => e.Key, e => (Min: e.Value.Aggregate(Vector2.Min), Max: e.Value.Aggregate(Vector2.Max)));
        // The face edges, and the unit square's border, which the look also draws over the image.
        var square = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }.Select(Pixel).ToArray();
        var segments = polygons.Values.Append(square).SelectMany(l => l.Select((c, i) => (A: c, B: l[(i + 1) % l.Length]))).ToArray();
        var kinds = new Dictionary<string, int>();

        var (inside, outside, wrong) = (0, 0, 0);
        double Frac(double t) => t - Math.Floor(t);
        for (var y = (int)origin; y < origin + side; y += 2)
        {
            for (var x = (int)origin; x < origin + side; x += 2)
            {
                var (u, v) = ((x + 0.5 - origin) / side, (y + 0.5 - origin) / side);
                if (u is <= 0 or >= 1 || v is <= 0 or >= 1) continue;
                if (Frac(u * png.Width) is < 0.25 or > 0.75 || Frac(v * png.Height) is < 0.25 or > 0.75) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f);
                if (!Clear(p, segments, 4)) continue;
                var face = -1;
                foreach (var (f, box) in boxes)
                {
                    if (p.X < box.Min.X || p.X > box.Max.X || p.Y < box.Min.Y || p.Y > box.Max.Y) continue;
                    if (Contains(polygons[f], p)) { face = f; break; }
                }

                var texel = png.At((int)Math.Floor(u * png.Width), (int)Math.Floor(v * png.Height));
                var got = Rgb(frame, x, y);
                string? problem = null, kind = null;
                if (face >= 0)
                {
                    inside++;
                    var want = worked(face);
                    if (!Near(got, texel, 2)) (problem, kind) = ($"inside face {face}, the frame shows {got} but the PNG's texel is {texel}", "inside-png");
                    else if (!Near(got, want, 2)) (problem, kind) = ($"inside face {face}, the frame shows {got} but the face's colour, worked from its groups, is {want}", "inside-worked");
                }
                else
                {
                    outside++;
                    var half = ((texel.R + 1) / 2, (texel.G + 1) / 2, (texel.B + 1) / 2);
                    if (!Near(got, half, 2)) (problem, kind) = ($"outside every island, the frame shows {got} but half the PNG's texel {texel} is {half}", "outside");
                }

                if (problem is null) continue;
                kinds[kind!] = kinds.GetValueOrDefault(kind!) + 1;
                if (++wrong <= 8) Note($"{what}: pixel ({x}, {y}): {problem}");
            }
        }

        if (wrong > 8) Note($"{what}: {wrong} pixels in all break the block rule ({string.Join(", ", kinds.Select(k => $"{k.Value} {k.Key}"))})");
        if (inside < 2000 || outside < 500) Note($"{what}: only {inside} pixels inside islands and {outside} outside could be placed");
        return (inside, outside);
    }

    private static bool Clear(Vector2 p, (Vector2 A, Vector2 B)[] segments, float clear)
    {
        foreach (var (a, b) in segments)
        {
            if (p.X < MathF.Min(a.X, b.X) - clear || p.X > MathF.Max(a.X, b.X) + clear || p.Y < MathF.Min(a.Y, b.Y) - clear || p.Y > MathF.Max(a.Y, b.Y) + clear) continue;
            if (Geo.DistanceToSegment(p, a, b) < clear) return false;
        }

        return true;
    }

    private static bool Contains(Vector2[] polygon, Vector2 p)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }

        return inside;
    }

    /// <summary>
    /// A crop of a frame scaled up by <paramref name="scale"/> (nearest), written as a PNG: a closer look cut from a frame already
    /// taken, not a new look.
    /// </summary>
    public static void WriteCrop(ScenarioFrame frame, string path, int x0, int y0, int side, int scale)
    {
        x0 = Math.Clamp(x0, 0, frame.Width - side);
        y0 = Math.Clamp(y0, 0, frame.Height - side);
        var n = side * scale;
        var rgba = new byte[n * n * 4];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var p = frame.Pixel(x0 + x / scale, y0 + y / scale);
                var i = (y * n + x) * 4;
                (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (p.R, p.G, p.B, 255);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Carve.SampleTextures.Png.Encode(rgba, n, n));
    }

    // ---- Render frames: the pixel at points of the faces, by group. ----

    /// <summary>One render view: the frame's name, the direction (from the point looked at toward the eye), the zoom and target.</summary>
    /// <param name="Mirror">Whether the x-mirror check applies: not in a view along x, where a point and its mirror share a line of sight.</param>
    public sealed record View(string Name, Vector3 Direction, int Size, float Zoom = 1, Vector3? Target = null, bool Mirror = true)
    {
        public object Args => Target is { } t
            ? new { direction = new[] { Direction.X, Direction.Y, Direction.Z }, zoom = Zoom, target = new[] { t.X, t.Y, t.Z }, size = Size, mode = "shaded" }
            : new { direction = new[] { Direction.X, Direction.Y, Direction.Z }, size = Size, mode = "shaded" };
    }

    /// <summary>One sampled point: the face, its group, which point of it (centre or corner n), the pixel and what it shows.</summary>
    public sealed record Sample(string View, int Face, string Group, string Point, int X, int Y, (int R, int G, int B) Shown)
    {
        public double Luma => BakesChecks.Luma(Shown);

        public int RedLead => BakesChecks.RedLead(Shown);
    }

    /// <summary>
    /// The faces' sample points in a view: each face turned toward the eye (its unit normal's cosine with the way to the eye at
    /// least <see cref="Facing"/>), at its centre and 0.7 of the way from the centre to each corner (M-3: near the corners as well),
    /// each point in sight of the eye by the ray test, at the pixel render's framing rule puts it.
    /// </summary>
    public static List<Sample> SampleFaces(ScenarioFrame frame, View view, Scene scene, SceneMesh mesh, IReadOnlyDictionary<int, string> groupOf)
    {
        var (min, max) = (scene.Min, scene.Max);
        var project = CameraPixels.Perspective(min, max, view.Direction, view.Size, view.Zoom, view.Target);
        var eye = CameraPixels.Eye(min, max, view.Direction, view.Target);
        var unproject = CameraPixels.Unproject(min, max, view.Direction, view.Size, view.Zoom, view.Target);
        var samples = new List<Sample>();
        for (var f = 0; f < mesh.Faces.Length; f++)
        {
            // The pixel shows face f alone: the rays through it and 1.5 pixels to each side and corner all meet f's plane inside its
            // loop and in sight of the eye, so no edge, silhouette or other face is within reach of it.
            bool Solid(int x, int y)
            {
                var n = mesh.Normal(f);
                var corner = mesh.Vertices[mesh.Faces[f][0]];
                foreach (var dy in new[] { -1.5f, 0, 1.5f })
                {
                    foreach (var dx in new[] { -1.5f, 0, 1.5f })
                    {
                        var (o, d) = unproject(x + dx, y + dy);
                        var across = Vector3.Dot(n, d);
                        if (MathF.Abs(across) < 1e-9f) return false;
                        var hit = o + d * (Vector3.Dot(n, corner - o) / across);
                        if (!Geo.Inside(mesh.Vertices, mesh.Faces[f], n, hit) || !Geo.InSight(scene, mesh, f, eye, hit)) return false;
                    }
                }

                return true;
            }

            var centre = mesh.Centre(f);
            var normal = Vector3.Normalize(mesh.Normal(f));
            if (Vector3.Dot(normal, Vector3.Normalize(eye - centre)) < Facing) continue;
            var points = new List<(string Name, Vector3 At)> { ("centre", centre) };
            points.AddRange(mesh.Faces[f].Select((v, i) => ($"corner{i}", centre + 0.7f * (mesh.Vertices[v] - centre))));
            foreach (var (name, at) in points)
            {
                if (!Geo.InSight(scene, mesh, f, eye, at)) continue;
                var (x, y) = CameraPixels.Holding(project(at));
                if (!Solid(x, y)) continue;
                if (Break == "mirror") x = view.Size - 1 - x;
                if (x < 2 || y < 2 || x >= view.Size - 2 || y >= view.Size - 2) continue;
                samples.Add(new Sample(view.Name, f, groupOf[f], name, x, y, Rgb(frame, x, y)));
            }
        }

        return samples;
    }

    /// <summary>The least cosine between a sampled face's normal and the way to the eye: a face seen at more than 60 degrees is left out.</summary>
    public const float Facing = 0.5f;

    /// <summary>
    /// Where the x-mirror of a point (-x, y, z) lands in a view, and what is shown there: the wrench is near-symmetric in x and its red
    /// box is not (M-2), so a mirrored or wrong camera shows red there.
    /// </summary>
    public static (int X, int Y, (int R, int G, int B) Shown)? Mirrored(ScenarioFrame frame, View view, Scene scene, Vector3 at)
    {
        var project = CameraPixels.Perspective(scene.Min, scene.Max, view.Direction, view.Size, view.Zoom, view.Target);
        var (x, y) = CameraPixels.Holding(project(at with { X = -at.X }));
        if (Break == "mirror") x = view.Size - 1 - x;
        if (x < 0 || y < 0 || x >= view.Size || y >= view.Size) return null;
        return (x, y, Rgb(frame, x, y));
    }
}
