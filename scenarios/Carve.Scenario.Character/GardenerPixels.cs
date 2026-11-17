using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Crate;

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

    /// <summary>
    /// As <see cref="ExpectGroup"/>, but against the palette lit as a face of world <paramref name="normal"/> is lit along
    /// <paramref name="direction"/> (<see cref="ColourRule.Lit"/>, M-7's sky included), not the bare colours. The sky's light is
    /// mostly blue, so it turns a dark colour toward another's direction: bare, the boots at rest read as skin (0.996 against
    /// 0.985); lit, as boots (1.000 against 0.986). The person's ruling of 2026-11-17 ("Accept all", M-19 row 6).
    /// </summary>
    public static void ExpectGroup(ScenarioDriver driver, ScenarioFrame frame, int x, int y, string group, string what, Vector3 normal, Vector3 direction)
    {
        var (found, dot) = ("outside the image", 0f);
        if (x >= 0 && y >= 0 && x < frame.Width && y < frame.Height)
        {
            var p = frame.Pixel(x, y);
            var n = Vector3.Normalize(new Vector3(Srgb(p.R), Srgb(p.G), Srgb(p.B)));
            (found, dot) = GardenerSpec.Palette
                .Select(c => (c.Group, Lit: ColourRule.Lit(c.Colour, normal, direction)))
                .Select(c => (c.Group, Linear: Vector3.Normalize(new Vector3((float)c.Lit[0], (float)c.Lit[1], (float)c.Lit[2]))))
                .Append(Palette[^1])
                .Select(c => (c.Group, Dot: Vector3.Dot(c.Linear, n)))
                .MaxBy(c => c.Dot);
        }

        if (found != group || dot < 0.98f)
        {
            driver.Fail($"{frame.Name}: {what} at ({x}, {y}) reads as {found} (match {dot:0.000}, against the palette lit at {normal}) with pixel {(x >= 0 && y >= 0 && x < frame.Width && y < frame.Height ? frame.Pixel(x, y).ToString() : "-")}, expected {group}");
        }
    }

    /// <summary>
    /// The rest frame from +Z: the centre of one front face of each colour's group, projected from the mesh, reads as that
    /// colour, against the palette lit by the sky at the face's own normal (the person's ruling of 2026-11-17, M-19 row 6).
    /// Shared by the character and soft scenarios, whose rest checks are the same.
    /// </summary>
    public static void ExpectRestColours(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, GardenerSpec.MeshSpec body, GardenerSpec.MeshSpec hat)
    {
        var targets = new (GardenerSpec.MeshSpec Mesh, string Group, Vector3 Near)[]
        {
            (body, "shirt", new Vector3(0, 0.85f, 0.3f)), (body, "trousers", new Vector3(0, 0.57f, 0.3f)), (body, "skin", new Vector3(0.07f, 1.27f, 0.3f)),
            (body, "boots", new Vector3(0.09f, 0.05f, 0.4f)), (body, "can", new Vector3(-0.435f, 0.39f, 0.30f)),
            (hat, "straw", new Vector3(0, 1.485f, 0.3f)), (hat, "band", new Vector3(0, 1.43f, 0.3f)),
        };
        foreach (var (mesh, group, near) in targets)
        {
            var face = mesh.NearestFace(group, near);
            var c = mesh.Centroid(face);
            var (x, y) = CameraPixels.Holding(project(c));
            ExpectGroup(driver, frame, x, y, group, $"the {group} face at {c}", mesh.Normal(face), Vector3.UnitZ);
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
