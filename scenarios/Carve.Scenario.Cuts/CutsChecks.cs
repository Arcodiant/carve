using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;
using Rgb = (int R, int G, int B);

namespace Carve.Scenario.Cuts;

/// <summary>
/// The helpers the cut stages share, and the gatehouse with them (it links this file): reading a tool's result, laying
/// out probes, and the probe checks a stage runs against its own <see cref="Solid"/>. Each was written once per stage
/// first; the checks are unchanged.
/// </summary>
internal static class CutsChecks
{
    public static Vector3 ToVector(double[] d) => new((float)d[0], (float)d[1], (float)d[2]);

    public static IEnumerable<string> Ids(IEnumerable<JsonElement> ids) => ids.Select(i => $"{i.GetInt32()}");

    /// <summary>An edit's summary: its vertex and face counts and whether the mesh is closed.</summary>
    public static void Summary(string what, JsonElement result, int vertices, int faces, bool closed)
    {
        Equal($"{what}: vertices", vertices, result.GetProperty("vertices").GetInt32());
        Equal($"{what}: faces", faces, result.GetProperty("faces").GetInt32());
        Equal($"{what}: closed", closed, result.GetProperty("closed").GetBoolean());
    }

    /// <summary>One probe named <paramref name="name"/> at each pair of <paramref name="steps"/>, placed by <paramref name="at"/>.</summary>
    public static IEnumerable<(string, Vector3)> Grid(string name, float[] steps, Func<float, float, Vector3> at) =>
        steps.SelectMany(u => steps.Select(v => (name, at(u, v))));

    /// <summary>The signs of a square's four corners, counter-clockwise from (-, -).</summary>
    public static (float X, float Y)[] Signs() => [(-1, -1), (1, -1), (1, 1), (-1, 1)];

    /// <summary>The directions of a square's four sides, in the order of <see cref="Signs"/>.</summary>
    public static (float X, float Y)[] Sides() => [(0, -1), (1, 0), (0, 1), (-1, 0)];

    /// <summary>
    /// The pixel of a face coloured <paramref name="hex"/> from the grey pixel of a face of the same <paramref name="normal"/>
    /// in the same frame along <paramref name="direction"/>: T-82's diffuse plus the PBR specular (<see cref="ColourRule"/>).
    /// </summary>
    public static Rgb Predict(Rgb grey, string hex, Vector3 normal, Vector3 direction)
    {
        var lit = ColourRule.Predict([ColourRule.Linear(grey.R), ColourRule.Linear(grey.G), ColourRule.Linear(grey.B)], hex, normal, direction);
        return Srgb(lit[0], lit[1], lit[2]);
    }

    /// <summary>The pixel of a face coloured <paramref name="hex"/> straight from T-82's light model (<see cref="ColourRule.Lit"/>).</summary>
    public static Rgb Lit(string hex, Vector3 normal, Vector3 direction)
    {
        var lit = ColourRule.Lit(hex, normal, direction);
        return Srgb(lit[0], lit[1], lit[2]);
    }

    /// <summary>
    /// The ray test must give <paramref name="face"/> at every probe named <paramref name="name"/> it is sure of, and be
    /// sure of at least <paramref name="least"/> of them.
    /// </summary>
    public static void Expect(ScenarioFrame frame, Camera camera, Solid solid, (string Name, Vector3 At)[] probes, string name, int face, int least)
    {
        var sure = 0;
        foreach (var (_, at) in probes.Where(p => p.Name == name))
        {
            var (x, y) = camera.Pixel(at);
            var seen = solid.Seen(camera, frame.Width, x, y);
            if (seen == -2)
            {
                continue;
            }

            sure++;
            Equal($"{frame.Name}: what the geometry shows at {name} {at} (pixel ({x}, {y}))", face, seen);
        }

        if (sure < least)
        {
            Note($"{frame.Name}: only {sure} probes on {name} were sure, fewer than {least}");
        }
    }

    /// <summary>
    /// Every probe the ray test is sure of, and that is more than 4 pixels from every one of <paramref name="lines"/>, must
    /// be the background where a ray meets nothing, and otherwise not the background and in the face's colour from
    /// <paramref name="colourOf"/>, when it gives one.
    /// </summary>
    public static void Look(ScenarioFrame frame, Camera camera, Solid solid, (string Name, Vector3 At)[] probes, Func<int, Rgb?> colourOf, (Vector2 From, Vector2 To)[] lines, string what)
    {
        Size(frame, 512, 512);
        Corners(frame);
        var tested = 0;
        foreach (var (name, at) in probes)
        {
            var (x, y) = camera.Pixel(at);
            var seen = solid.Seen(camera, frame.Width, x, y);
            if (seen == -2 || lines.Any(l => DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), l.From, l.To) < 4))
            {
                continue;
            }

            tested++;
            if (seen == -1)
            {
                Background(frame, (x, y), $"{what}: {name} {at}, which a ray meets nowhere");
                continue;
            }

            NotBackground(frame, (x, y), $"{what}: {name} {at}, face {seen}");
            if (colourOf(seen) is { } colour)
            {
                Colour(frame, (x, y), colour, Exact, $"{what}: {name} {at}, face {seen}");
            }
        }

        if (tested < 40)
        {
            Note($"{frame.Name}: only {tested} probes were surely a face or the background");
        }
    }
}
