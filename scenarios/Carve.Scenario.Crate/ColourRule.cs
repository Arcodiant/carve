using System.Numerics;

namespace Carve.Scenario.Crate;

/// <summary>
/// The Crate scenario's colour rule (T-30), apart from the crate so that other scenarios can link it: a lit surface's
/// pixel, in linear light, is its colour times one light factor per channel, so the pixel of a surface coloured
/// <c>#RRGGBB</c> is the pixel of the grey surface (0.72, 0.72, 0.70) under the same light times colour / grey in each
/// channel. Each scenario judges the prediction at its own tolerance.
/// </summary>
internal static class ColourRule
{
    /// <summary>The surface of a face with no colour (the product's default grey).</summary>
    public static readonly Vector3 Grey = new(0.72f, 0.72f, 0.70f);

    /// <summary>Colour / grey for each channel, the colour's channels in linear light.</summary>
    public static double[] Scale(string hex)
    {
        var (r, g, b) = (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
        var albedo = new Vector3((float)Linear(r), (float)Linear(g), (float)Linear(b));
        return [albedo.X / Grey.X, albedo.Y / Grey.Y, albedo.Z / Grey.Z];
    }

    /// <summary>An 8 bit sRGB channel in linear light.</summary>
    public static double Linear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
