using System.Numerics;
using Carve.Debug;

namespace Carve.Scenario.Character;

/// <summary>
/// Reads a frame's pixel as one of the gardener's colour groups (or the background), for the character and soft scenarios:
/// a lit pixel is the surface colour times one light factor, so in linear light its direction is the colour's.
/// </summary>
internal static class GardenerPixels
{
    private static readonly (string Group, Vector3 Linear)[] Palette =
    [
        .. GardenerSpec.Palette.Select(p => (p.Group, Vector3.Normalize(Linear(p.Colour)))),
        ("background", Vector3.Normalize(new Vector3(Srgb(48), Srgb(52), Srgb(56)))),
    ];

    private static float Srgb(int v)
    {
        var c = v / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    private static Vector3 Linear(string hex) =>
        new(Srgb(Convert.ToInt32(hex.Substring(1, 2), 16)), Srgb(Convert.ToInt32(hex.Substring(3, 2), 16)), Srgb(Convert.ToInt32(hex.Substring(5, 2), 16)));

    /// <summary>The group whose colour is nearest in direction, and how near (the cosine).</summary>
    public static (string Group, float Dot) Classify(ScenarioFrame frame, int x, int y)
    {
        if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height)
        {
            return ("outside the image", 0);
        }

        var p = frame.Pixel(x, y);
        var v = new Vector3(Srgb(p.R), Srgb(p.G), Srgb(p.B));
        if (v.LengthSquared() < 1e-12f)
        {
            return ("black", 0);
        }

        var n = Vector3.Normalize(v);
        return Palette.Select(c => (c.Group, Dot: Vector3.Dot(c.Linear, n))).MaxBy(c => c.Dot);
    }

    public static void ExpectGroup(ScenarioDriver driver, ScenarioFrame frame, int x, int y, string group, string what)
    {
        var (found, dot) = Classify(frame, x, y);
        if (found != group || dot < 0.98f)
        {
            driver.Fail($"{frame.Name}: {what} at ({x}, {y}) reads as {found} (match {dot:0.000}) with pixel {(x >= 0 && y >= 0 && x < frame.Width && y < frame.Height ? frame.Pixel(x, y).ToString() : "-")}, expected {group}");
        }
    }

    public static void ExpectNot(ScenarioDriver driver, ScenarioFrame frame, int x, int y, string group, string what)
    {
        if (Classify(frame, x, y).Group == group)
        {
            driver.Fail($"{frame.Name}: {what} at ({x}, {y}) reads as {group}, which it must not");
        }
    }
}
