using System.Numerics;

namespace Carve.Scenario.Crate;

/// <summary>
/// Predicting the <c>uvs</c> look's pixels (T-58) from the UVs alone, apart from the crate so that other suites can link it.
/// The window rule is worked here again, never read from the server: the unit square grown to take in every UV given, kept
/// square about the bounds' centre, then widened by 8/7, so the margin is a sixteenth of the window each side; u runs right
/// and v down. A face's pixel is its texel's byte (unlit, exact), the ground's the dimmed byte, a highlighted face solid
/// orange; the tolerance is the person's (ruling 13, round 1): ±2 a channel.
/// </summary>
internal sealed record UvLayoutPixels(double CentreU, double CentreV, double Side, int Size)
{
    /// <summary>The approved tolerance a channel, from the probe (ruling 13).</summary>
    public const int Tolerance = 2;

    public static UvLayoutPixels Of(IEnumerable<IReadOnlyList<Vector2>> faces, int size)
    {
        double u0 = 0, v0 = 0, u1 = 1, v1 = 1;
        foreach (var p in faces.SelectMany(f => f))
        {
            (u0, v0, u1, v1) = (Math.Min(u0, p.X), Math.Min(v0, p.Y), Math.Max(u1, p.X), Math.Max(v1, p.Y));
        }

        return new UvLayoutPixels((u0 + u1) / 2, (v0 + v1) / 2, Math.Max(u1 - u0, v1 - v0) * 8 / 7, size);
    }

    /// <summary>The UV a pixel centre samples.</summary>
    public Vector2 UvAt(int x, int y) =>
        new((float)(CentreU - Side / 2 + (x + 0.5) * Side / Size), (float)(CentreV - Side / 2 + (y + 0.5) * Side / Size));

    /// <summary>Where a UV lands, in pixels from the top-left.</summary>
    public Vector2 PixelOf(Vector2 uv) =>
        new((float)(((uv.X - CentreU) / Side + 0.5) * Size), (float)(((uv.Y - CentreV) / Side + 0.5) * Size));

    /// <summary>Pixels per UV unit.</summary>
    public double Scale => Size / Side;

    /// <summary>The checker's cell at a UV, repeated: (0,0) white, light cells the column's hue, dark ones 55% of it.</summary>
    public static (int R, int G, int B) Texel(Vector2 uv)
    {
        static int CellOf(double t) => Math.Clamp((int)Math.Floor((t - Math.Floor(t)) * 8), 0, 7);
        var (c, r) = (CellOf(uv.X), CellOf(uv.Y));
        if (c == 0 && r == 0) return (255, 255, 255);
        var hue = CheckerColours.Hues[c];
        static int Dark(uint x) => (int)((x * 55 + 50) / 100);
        return (c + r) % 2 == 0
            ? ((int)(hue >> 16 & 0xFF), (int)(hue >> 8 & 0xFF), (int)(hue & 0xFF))
            : (Dark(hue >> 16 & 0xFF), Dark(hue >> 8 & 0xFF), Dark(hue & 0xFF));
    }

    /// <summary>A byte as the ground shows it: sRGB-encode(decode(c) x 34/255), the ground's vertex colour (40% of the displayed value).</summary>
    public static int Dimmed(int c) => Encode(Decode(c) * 34 / 255);

    public static (int R, int G, int B) Dimmed((int R, int G, int B) c) => (Dimmed(c.R), Dimmed(c.G), Dimmed(c.B));

    /// <summary>The highlight's solid orange as shown: linear (1, 0.55, 0.1) to a byte, then encoded.</summary>
    public static readonly (int R, int G, int B) Orange = (Encode(1), Encode(Math.Round(0.55 * 255) / 255), Encode(Math.Round(0.1 * 255) / 255));

    /// <summary>A seam, #FFD800.</summary>
    public static readonly (int R, int G, int B) Seam = (0xFF, 0xD8, 0x00);

    public static double Decode(int c)
    {
        var x = c / 255.0;
        return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
    }

    public static int Encode(double c) =>
        (int)Math.Round(255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055));

    public static bool Near((int R, int G, int B) got, (int R, int G, int B) want, int by = Tolerance) =>
        Math.Abs(got.R - want.R) <= by && Math.Abs(got.G - want.G) <= by && Math.Abs(got.B - want.B) <= by;

    /// <summary>
    /// How much of a pixel is #FFD800 over <paramref name="bg"/>, fitted in linear light (the target blends linearly and is
    /// encoded after): a line's pixels are partly covered under MSAA. 0 where the fit is more than 0.04 off in a channel.
    /// </summary>
    public static double SeamShare((int R, int G, int B) p, (int R, int G, int B) bg)
    {
        double[] got = [Decode(p.R), Decode(p.G), Decode(p.B)], under = [Decode(bg.R), Decode(bg.G), Decode(bg.B)], seam = [1, Decode(0xD8), 0];
        double num = 0, den = 0;
        for (var c = 0; c < 3; c++)
        {
            num += (got[c] - under[c]) * (seam[c] - under[c]);
            den += (seam[c] - under[c]) * (seam[c] - under[c]);
        }

        if (den < 1e-6) return 0;
        var f = Math.Clamp(num / den, 0, 1);
        return Enumerable.Range(0, 3).All(c => Math.Abs(under[c] + f * (seam[c] - under[c]) - got[c]) <= 0.04) ? f : 0;
    }

    /// <summary>Whether a UV lies inside a loop (even-odd).</summary>
    public static bool Inside(IReadOnlyList<Vector2> loop, Vector2 p)
    {
        var inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            var (a, b) = (loop[i], loop[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }

        return inside;
    }

    /// <summary>The distance in UV from a point to a segment.</summary>
    public static double Distance(Vector2 p, Vector2 a, Vector2 b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var t = dx * dx + dy * dy < 1e-18 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
    }

    /// <summary>The area centroid of a loop, or its corners' mean where it has no area: where a face's id is written.</summary>
    public static Vector2 LabelPoint(IReadOnlyList<Vector2> loop)
    {
        double area = 0, x = 0, y = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            Vector2 a = loop[i], b = loop[(i + 1) % loop.Count];
            var cross = (double)a.X * b.Y - (double)b.X * a.Y;
            (area, x, y) = (area + cross, x + (a.X + b.X) * cross, y + (a.Y + b.Y) * cross);
        }

        return Math.Abs(area) < 1e-12 ? loop.Aggregate(Vector2.Zero, (s, p) => s + p) / loop.Count : new Vector2((float)(x / (3 * area)), (float)(y / (3 * area)));
    }

    /// <summary>The unsigned area of a loop.</summary>
    public static double Area(IReadOnlyList<Vector2> loop) =>
        Math.Abs(Enumerable.Range(0, loop.Count).Sum(i => (double)loop[i].X * loop[(i + 1) % loop.Count].Y - (double)loop[(i + 1) % loop.Count].X * loop[i].Y)) / 2;
}
