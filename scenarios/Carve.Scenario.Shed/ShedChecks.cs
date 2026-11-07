using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Shed;

using Rgb = (int R, int G, int B);

/// <summary>
/// The checks every frame uses. A failed check is added to <see cref="Failures"/> and the run goes on, so one run
/// reports everything that is wrong; the scenario fails at its end with all of them.
/// </summary>
internal static class Checks
{
    /// <summary>The flat colours of the looks are within this of the sRGB value; a line pixel partly covers its neighbours.</summary>
    public const int Exact = 2;

    /// <summary>An engine text or dot pixel reads this or more on every channel; nothing under a backing does.</summary>
    public const int White = 250;

    public static readonly List<string> Failures = [];

    public static readonly Rgb Fill = Srgb(0.09, 0.095, 0.10);
    public static readonly Rgb Edge = Srgb(0.85, 0.85, 0.85);
    public static readonly Rgb Orange = Srgb(1, 0.55, 0.1);

    public static void Note(string message) => Failures.Add(message);

    /// <summary>How a linear colour reads in a frame, each channel 0 to 255.</summary>
    public static Rgb Srgb(double r, double g, double b)
    {
        static int Encode(double c) =>
            (int)Math.Round(255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055));
        return (Encode(r), Encode(g), Encode(b));
    }

    /// <summary>A face's unlit colour in normals mode: the sRGB of ((n + 1) / 2).</summary>
    public static Rgb NormalColour(Vector3 n) => Srgb((n.X + 1) / 2, (n.Y + 1) / 2, (n.Z + 1) / 2);

    public static bool IsBackground((byte R, byte G, byte B, byte A) p) =>
        p.R is >= 47 and <= 49 && p.G is >= 51 and <= 53 && p.B is >= 55 and <= 57;

    public static bool IsWhite((byte R, byte G, byte B, byte A) p) => p.R >= White && p.G >= White && p.B >= White;

    public static async Task<string> CallTextAsync(ScenarioDriver driver, string tool, object arguments)
    {
        var result = await driver.CallAsync(tool, arguments);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError is true)
        {
            driver.Fail($"{tool}: {text}");
        }

        return text;
    }

    public static async Task<JsonElement> CallJsonAsync(ScenarioDriver driver, string tool, object arguments) =>
        JsonDocument.Parse(await CallTextAsync(driver, tool, arguments)).RootElement.Clone();

    /// <summary>A call that must be a tool error whose text names each of <paramref name="mentions"/>. Returns the text.</summary>
    public static async Task<string> ExpectToolErrorAsync(ScenarioDriver driver, string tool, object arguments, params string[] mentions)
    {
        var result = await driver.CallAsync(tool, arguments);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError is not true)
        {
            Note($"{tool} {JsonSerializer.Serialize(arguments)} was not a tool error: {text}");
        }

        foreach (var m in mentions.Where(m => !text.Contains(m, StringComparison.Ordinal)))
        {
            Note($"{tool} {JsonSerializer.Serialize(arguments)}: the error should name '{m}': {text}");
        }

        return text;
    }

    public static void Equal<T>(string what, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            Note($"{what}: expected {expected}, got {actual}");
        }
    }

    public static void SameList(string what, IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var (e, a) = (expected.ToArray(), actual.ToArray());
        if (!e.SequenceEqual(a))
        {
            Note($"{what}: expected {e.Length} [{string.Join(" | ", e)}], got {a.Length} [{string.Join(" | ", a)}]");
        }
    }

    public static void Caption(ScenarioFrame frame, string expected) => Equal($"{frame.Name}: the caption", expected, frame.Caption);

    public static void Size(ScenarioFrame frame, int width, int height)
    {
        if (frame.Width != width || frame.Height != height)
        {
            Note($"{frame.Name}: the PNG is {frame.Width} x {frame.Height}, expected {width} x {height}");
        }
    }

    public static void Corners(ScenarioFrame frame)
    {
        var (w, h) = (frame.Width - 1, frame.Height - 1);
        foreach (var (x, y) in new[] { (0, 0), (w, 0), (0, h), (w, h) })
        {
            if (!IsBackground(frame.Pixel(x, y)))
            {
                Note($"{frame.Name}: a corner at ({x}, {y}) is {frame.Pixel(x, y)}, not the background");
            }
        }
    }

    public static void Colour(ScenarioFrame frame, (int X, int Y) at, Rgb colour, int within, string what)
    {
        var p = frame.Pixel(at.X, at.Y);
        if (Math.Abs(p.R - colour.R) > within || Math.Abs(p.G - colour.G) > within || Math.Abs(p.B - colour.B) > within)
        {
            Note($"{frame.Name}: {what} at ({at.X}, {at.Y}) is {p}, expected {colour} within {within}");
        }
    }

    public static void Background(ScenarioFrame frame, (int X, int Y) at, string what)
    {
        if (!IsBackground(frame.Pixel(at.X, at.Y)))
        {
            Note($"{frame.Name}: {what} at ({at.X}, {at.Y}) is {frame.Pixel(at.X, at.Y)}, expected the background");
        }
    }

    public static void NotBackground(ScenarioFrame frame, (int X, int Y) at, string what)
    {
        if (IsBackground(frame.Pixel(at.X, at.Y)))
        {
            Note($"{frame.Name}: {what} at ({at.X}, {at.Y}) is the background, expected the mesh");
        }
    }

    /// <summary>
    /// A line is one pixel wide and rarely centred on a pixel, so of the pixels touching the projected stretch of
    /// it, the closest in colour must be the line's, within 10/255.
    /// </summary>
    public static void Line(ScenarioFrame frame, Vector2 from, Vector2 to, Rgb colour, string what)
    {
        const int Steps = 16;
        var best = (Off: int.MaxValue, X: 0, Y: 0, P: default((byte R, byte G, byte B, byte A)));
        for (var step = 0; step <= Steps; step++)
        {
            var p0 = Vector2.Lerp(from, to, (float)step / Steps);
            var (x, y) = ((int)MathF.Floor(p0.X), (int)MathF.Floor(p0.Y));
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var p = frame.Pixel(x + dx, y + dy);
                var off = Math.Max(Math.Abs(p.R - colour.R), Math.Max(Math.Abs(p.G - colour.G), Math.Abs(p.B - colour.B)));
                if (off < best.Off)
                {
                    best = (off, x + dx, y + dy, p);
                }
            }
        }

        if (best.Off > 10)
        {
            Note($"{frame.Name}: {what}, {from} to {to}: the closest pixel to {colour} is {best.P} at ({best.X}, {best.Y})");
        }
    }

    /// <summary>Of a face's samples, those whose pixel the camera surely shows as a face or the background: tried against <paramref name="expect"/>.</summary>
    public static int Samples(ScenarioFrame frame, Camera camera, Shape shape, (int X, int Y) origin, int size, Func<int, Rgb?> expectFace, bool background, string what, IEnumerable<(Vector2 From, Vector2 To)>? avoid = null)
    {
        var avoided = avoid?.ToArray() ?? [];
        var tested = 0;
        for (var face = 0; face < shape.Faces.Length; face++)
        foreach (var point in shape.Samples(face))
        {
            var (x, y) = camera.Pixel(point);
            if (y < 24 && size < 400)
            {
                continue; // a sheet tile's caption
            }

            var seen = camera.Seen(shape, x, y);
            if (seen == -2 || avoided.Any(s => DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), s.From, s.To) < 4))
            {
                continue;
            }

            tested++;
            var at = (origin.X + x, origin.Y + y);
            if (seen == -1)
            {
                if (background && !IsBackground(frame.Pixel(at.Item1, at.Item2)))
                {
                    Note($"{frame.Name}: {what}: face {face} at {point} (pixel {at}) should show the background, is {frame.Pixel(at.Item1, at.Item2)}");
                }
            }
            else if (expectFace(seen) is { } colour)
            {
                Colour(frame, at, colour, Exact, $"{what}: face {face}'s sample {point} seen as face {seen}");
            }
            else if (!background)
            {
                continue;
            }
            else
            {
                NotBackground(frame, at, $"{what}: face {face}'s sample {point} seen as face {seen}");
            }
        }

        return tested;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>The cell of an id: centred on a point for a face, from a top left for a vertex, on whole pixels as the text is.</summary>
    public static (int X, int Y, int Width, int Height) IdBox(Vector2 at, int id, bool centred)
    {
        var width = 8 * id.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        var from = centred ? at - new Vector2(width / 2f, 8) : at;
        return ((int)MathF.Round(from.X), (int)MathF.Round(from.Y), width, 16);
    }

    /// <summary>A shade under a 0.6 black backing, which blends in linear light.</summary>
    public static int Backed(int shade)
    {
        var c = shade / 255.0;
        var linear = (c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4)) * 0.4;
        return (int)Math.Round(255 * (linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055));
    }

    public static bool WhiteIn(ScenarioFrame frame, (int X, int Y, int Width, int Height) box)
    {
        for (var y = box.Y; y < box.Y + box.Height; y++)
        for (var x = box.X; x < box.X + box.Width; x++)
        {
            if (IsWhite(frame.Pixel(x, y)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A line stands off its faces (<c>LineLift</c>, about one pixel here), so it is drawn that much nearer the eye than the
    /// edge itself projects. At three columns, past the chimney, along the straight edge from <paramref name="a"/> to <paramref name="b"/>, the
    /// brightness-weighted centre of the line's rows must lie between 0.5 and 1.7 pixels above where the edge projects.
    /// </summary>
    public static void LineStandsOff(ScenarioFrame frame, Camera camera, Vector3 a, Vector3 b, string what)
    {
        var (pa, pb) = (camera.Project(a), camera.Project(b));
        foreach (var t in new[] { 0.55f, 0.7f, 0.85f })
        {
            var x = (int)MathF.Floor(Vector2.Lerp(pa, pb, t).X);
            var y0 = pa.Y + (pb.Y - pa.Y) * ((x + 0.5f - pa.X) / (pb.X - pa.X));
            double sum = 0, weighted = 0;
            for (var row = (int)MathF.Floor(y0) - 4; row <= (int)MathF.Floor(y0) + 3; row++)
            {
                var w = frame.Pixel(x, row).G - 53;
                if (w > 20)
                {
                    sum += w;
                    weighted += w * (row + 0.5);
                }
            }

            var shift = sum == 0 ? double.NaN : y0 - weighted / sum;
            if (!(shift is >= 0.5 and <= 1.7))
            {
                Note($"{frame.Name}: {what} at column {x}: the line is {shift:0.00} pixels above where the edge projects ({y0:0.00}), expected 0.5 to 1.7: it should stand off its faces");
            }
        }
    }
}
