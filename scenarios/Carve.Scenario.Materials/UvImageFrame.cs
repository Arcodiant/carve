using System.Globalization;
using System.Numerics;
using Carve.Debug;
using Carve.SampleTextures;
using Carve.Scenario.Uvs;

namespace Carve.Scenario.Materials;

/// <summary>
/// T-121: the barrel in the <c>uvs</c> look with <c>staves-base.png</c> under its layout, one frame at 1024 after M-7's, with every
/// pixel worked from the texels and the layout rule rather than read from the server.
/// <para>
/// The rule. The window is the unit square grown by 8/7 (<c>UvWindow.Growth</c>), so at size 1024 the square is 896 pixels from x = 64:
/// frame pixel x has its centre at u = (x + 0.5 - 64) / 896, and one texel of the 256-texel image is 3.5 pixels. The engine samples this
/// look nearest with no mips (<c>RenderSystem.MakeUvImage</c>, probed by <c>UvImageLookTests</c>), so the pixel shows texel
/// <c>floor(u * 256)</c>, as stored inside an island and as <c>(c + 1) / 2</c> in the padding outside every island. The barrel's
/// islands do not fill the unit square, so a sample is either inside an island or in the padding, and the check needs ten or more of
/// each. A texel is under 4 frame pixels, so a check takes only pixels whose centre lies 0.25 to
/// 0.75 of a texel into it (a quarter texel, nearly a pixel, from the neighbour) and 4 pixels or more from every face edge, whose white
/// lines are drawn over the image.
/// </para>
/// <para>
/// T-77's staves are 32 texels wide: the gap is the texels at x mod 32 = 31 and 0 (colour <c>(28, 18, 11)</c>), so at u = k/8 for k = 1 to 7
/// it is 7 pixels wide, and the face edge's line lies over its middle. The pixels 2 to 3 pixels either side of the line are gap colour.
/// </para>
/// </summary>
internal static class UvImageFrame
{
    public const int Size = 1024;
    private const double Origin = Size / 16.0, Side = Size * 7 / 8.0;
    private const string Image = "staves-base.png";

    /// <summary>The gap texels' colour, from the generator.</summary>
    private static readonly (int R, int G, int B) Gap = (28, 18, 11);

    public static async Task<ScenarioFrame> CheckAsync(ScenarioDriver driver, string textures, SceneMesh barrel)
    {
        var path = Path.Combine(textures, Image);
        var frame = await driver.CaptureAsync("barrel-uvs-image", new { mode = "uvs", image = path, size = Size });
        var want = $"{Size}x{Size}, uvs mesh, image {Image} 256 x 256, ";
        if (frame.Caption is null || !frame.Caption.StartsWith(want, StringComparison.Ordinal)) driver.Fail($"barrel-uvs-image: the caption is '{frame.Caption}', expected it to start '{want}'");

        var rgba = TextureSet.Build().First(t => t.Name == Image).Rgba;
        var loops = barrel.Uvs.Values.Select(l => l.Select(Pixel).ToArray()).ToArray();
        var segments = barrel.Uvs.Values.SelectMany(Loop).ToArray();
        // The islands are the faces' polygons: full inside, half brightness (each channel (c + 1) / 2) outside, in the unit square.
        bool Inside(double x, double y) => loops.Any(l => Contains(l, x, y));
        (int R, int G, int B) Shown((int R, int G, int B) c, bool inside) => inside ? c : ((c.R + 1) / 2, (c.G + 1) / 2, (c.B + 1) / 2);
        var (islands, padding) = (0, 0);
        var log = new List<string> { "kind u v x y texel got want" };

        // Stave interiors: each stave at seven heights, the stored texel inside an island, half of it outside.
        var interior = 0;
        for (var k = 0; k < 8; k++)
        {
            foreach (var v in new[] { 0.1, 0.2, 0.36, 0.5, 0.64, 0.8, 0.9 })
            {
                if (Find((k + 0.5) / 8, v, segments, 4, texelMargin: true) is not { } at) continue;
                var texel = (X: (int)Math.Floor(U(at.X) * TextureSet.Size), Y: (int)Math.Floor(U(at.Y) * TextureSet.Size));
                var i = (texel.Y * TextureSet.Size + texel.X) * 4;
                var inside = Inside(at.X + 0.5, at.Y + 0.5);
                var expected = Shown((rgba[i] + 0, rgba[i + 1] + 0, rgba[i + 2] + 0), inside);
                var got = Rgb(frame, at.X, at.Y);
                log.Add(Line(inside ? "stave-island" : "stave-padding", k, v, at, texel, got, expected));
                if (!Near(got, expected, 2)) driver.Fail($"barrel-uvs-image: stave {k} at pixel ({at.X}, {at.Y}), texel {texel}, {(inside ? "inside an island" : "in the padding")}: the frame shows {got}, expected {expected}");
                interior++;
                if (inside) islands++; else padding++;
            }
        }

        if (interior < 30 || islands < 10 || padding < 10) driver.Fail($"barrel-uvs-image: only {interior} stave samples could be placed, {islands} inside islands and {padding} in the padding");

        // The gaps: 2 to 3 pixels either side of the edge line at u = k/8, in the gap colour (all 84 read exact in the run, so the tolerance is the stave check's 2).
        var gaps = 0;
        for (var k = 1; k < 8; k++)
        {
            var edge = Origin + Side * k / 8;
            foreach (var v in new[] { 0.1, 0.2, 0.36, 0.5, 0.64, 0.8, 0.9 })
            {
                foreach (var dx in new[] { 2, -3 })
                {
                    var x = (int)Math.Floor(edge) + dx;
                    var y = (int)Math.Round(Origin + Side * v - 0.5);
                    if (Clearance(x + 0.5, y + 0.5, segments) < 2) continue;
                    var got = Rgb(frame, x, y);
                    var shown = Shown(Gap, Inside(x + 0.5, y + 0.5));
                    log.Add(Line("gap", k, v, (x, y), (-1, -1), got, shown));
                    if (!Near(got, shown, 2)) driver.Fail($"barrel-uvs-image: the gap at u = {k}/8, pixel ({x}, {y}) is {got}, expected {shown}");
                    gaps++;
                }
            }
        }

        if (gaps < 40) driver.Fail($"barrel-uvs-image: only {gaps} gap samples could be placed");
        System.IO.File.WriteAllLines(Path.Combine(driver.RunFolder, "uv-image-samples.txt"), log);
        return frame;
    }

    /// <summary>Whether a point is inside a convex or simple polygon, by the crossing rule.</summary>
    private static bool Contains(Vector2[] polygon, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }

        return inside;
    }

    private static double U(int pixel) => (pixel + 0.5 - Origin) / Side;

    private static (int R, int G, int B) Rgb(ScenarioFrame frame, int x, int y)
    {
        var p = frame.Pixel(x, y);
        return (p.R, p.G, p.B);
    }

    private static bool Near((int R, int G, int B) got, (int R, int G, int B) want, int by) =>
        Math.Abs(got.R - want.R) <= by && Math.Abs(got.G - want.G) <= by && Math.Abs(got.B - want.B) <= by;

    private static string Line(string kind, int k, double v, (int X, int Y) at, (int X, int Y) texel, (int, int, int) got, (int, int, int) want) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind} {k} {v} {at.X} {at.Y} {texel.X},{texel.Y} {got} {want}");

    /// <summary>A face's edges in pixels from the top left, as the layout draws them.</summary>
    private static IEnumerable<(Vector2 A, Vector2 B)> Loop(Vector2[] corners) =>
        corners.Select((c, i) => (Pixel(c), Pixel(corners[(i + 1) % corners.Length])));

    private static Vector2 Pixel(Vector2 uv) => new((float)(Origin + uv.X * Side), (float)(Origin + uv.Y * Side));

    /// <summary>The distance from a point to the nearest face edge, in pixels.</summary>
    private static double Clearance(double x, double y, (Vector2 A, Vector2 B)[] segments)
    {
        var p = new Vector2((float)x, (float)y);
        var best = double.MaxValue;
        foreach (var (a, b) in segments)
        {
            var ab = b - a;
            var t = ab == Vector2.Zero ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            best = Math.Min(best, Vector2.Distance(p, a + ab * t));
        }

        return best;
    }

    /// <summary>
    /// A pixel near (u, v) whose centre is a quarter to three quarters into its texel on each axis and at least <paramref name="clear"/>
    /// pixels from every edge; null when none of the 7 x 7 pixels there is.
    /// </summary>
    private static (int X, int Y)? Find(double u, double v, (Vector2 A, Vector2 B)[] segments, double clear, bool texelMargin)
    {
        var (cx, cy) = ((int)Math.Round(Origin + Side * u - 0.5), (int)Math.Round(Origin + Side * v - 0.5));
        foreach (var d in Enumerable.Range(0, 49).Select(i => (X: i % 7 - 3, Y: i / 7 - 3)).OrderBy(d => d.X * d.X + d.Y * d.Y))
        {
            var (x, y) = (cx + d.X, cy + d.Y);
            double Frac(int pixel) => U(pixel) * TextureSet.Size - Math.Floor(U(pixel) * TextureSet.Size);
            if (texelMargin && (Frac(x) is < 0.25 or > 0.75 || Frac(y) is < 0.25 or > 0.75)) continue;
            if (Clearance(x + 0.5, y + 0.5, segments) >= clear) return (x, y);
        }

        return null;
    }
}
