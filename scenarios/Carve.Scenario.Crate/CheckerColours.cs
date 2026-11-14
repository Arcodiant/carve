namespace Carve.Scenario.Crate;

/// <summary>
/// Reading the checker look's pixels (T-57), apart from the crate so that the server's tests can link it. Light, the
/// tonemap and sRGB set a lit pixel's value, so a pixel is classified, never matched: by the nearest of the checker's
/// eight column hues, by light against dark on one face, and as white or magenta. The hues repeat
/// <c>CheckerTexture.Hues</c> (a unit test holds them equal), since a scenario cannot see the server.
/// </summary>
internal static class CheckerColours
{
    /// <summary>The eight column hues, #RRGGBB in sRGB, column 0 (u = 0) first.</summary>
    public static readonly uint[] Hues = [0xE04040, 0xE08A30, 0xD8C830, 0x50B050, 0x40B0C0, 0x4070E0, 0x6A50D0, 0xB050C0];

    /// <summary>The hue angle of an sRGB pixel in degrees, 0 to 360.</summary>
    public static double Hue(int r, int g, int b)
    {
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        if (max == min) return 0;
        double d = max - min;
        var h = max == r ? (g - b) / d : max == g ? 2 + (b - r) / d : 4 + (r - g) / d;
        return (h * 60 + 360) % 360;
    }

    /// <summary>The column whose hue is nearest the pixel's, by angle round the circle.</summary>
    public static int NearestColumn(int r, int g, int b)
    {
        var h = Hue(r, g, b);
        var best = 0;
        var bestGap = double.MaxValue;
        for (var c = 0; c < Hues.Length; c++)
        {
            var hue = Hues[c];
            var gap = Math.Abs(h - Hue((int)(hue >> 16 & 0xFF), (int)(hue >> 8 & 0xFF), (int)(hue & 0xFF)));
            gap = Math.Min(gap, 360 - gap);
            if (gap < bestGap) (best, bestGap) = (c, gap);
        }

        return best;
    }

    /// <summary>A pixel's brightest channel: a light cell's exceeds its dark neighbour's on one face.</summary>
    public static int Value(int r, int g, int b) => Math.Max(r, Math.Max(g, b));

    /// <summary>The white cell's largest channel spread, set from the probe frame and approved by the person (ruling 10, 2026-11-13).</summary>
    public const int WhiteSpread = 24;

    /// <summary>The white cell's least channel, likewise.</summary>
    public const int WhiteLeast = 120;

    /// <summary>White: every channel close (spread at most <see cref="WhiteSpread"/>) and at least <see cref="WhiteLeast"/>.</summary>
    public static bool IsWhite(int r, int g, int b) =>
        Value(r, g, b) - Math.Min(r, Math.Min(g, b)) <= WhiteSpread && Math.Min(r, Math.Min(g, b)) >= WhiteLeast;

    /// <summary>Magenta's greatest green, its greatest red-blue difference and its least red and blue above green, likewise.</summary>
    public const int MagentaGreen = 30, MagentaRedBlue = 24, MagentaAboveGreen = 100;

    /// <summary>Magenta: green near 0, red and blue close and well above it.</summary>
    public static bool IsMagenta(int r, int g, int b) =>
        g <= MagentaGreen && Math.Abs(r - b) <= MagentaRedBlue && Math.Min(r, b) - g >= MagentaAboveGreen;

    /// <summary>
    /// Whether a pixel shows the checker cell a sample expects: magenta where the face is unmapped, white at cell (0, 0),
    /// else column <paramref name="column"/>'s hue; with what was expected, in words, and the pixel's nearest column.
    /// </summary>
    public static (bool Ok, string Expected, int Nearest) Judge(int r, int g, int b, bool mapped, int column, int row)
    {
        var (white, magenta, nearest) = (IsWhite(r, g, b), IsMagenta(r, g, b), NearestColumn(r, g, b));
        var (ok, expected) = !mapped ? (magenta, "magenta (unmapped)")
            : (column, row) == (0, 0) ? (white, "white (uv 0,0)")
            : (!white && !magenta && nearest == column, $"column {column}'s hue");
        return (ok, expected, nearest);
    }
}
