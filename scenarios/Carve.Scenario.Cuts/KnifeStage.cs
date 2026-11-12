using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Cuts;

using Rgb = (int R, int G, int B);

/// <summary>
/// T-67, knife: a 2 x 2 x 0.2 slab with a closed hexagon knifed into its front face (z = 0.2), which leaves the front
/// holed and the hexagon an inner face, and an open door path up from the front's bottom edge, which splits it. Seen in the
/// wireframe from <c>[0.6, 0.35, 1]</c> with the knifed edges highlighted; then the hexagon's inner face recessed by 0.05
/// with <c>extrude</c>, shaded from the same direction. Every pixel asked about is worked from get_mesh's geometry by a ray
/// test (<see cref="Solid.FirstHit"/>).
/// </summary>
internal sealed class KnifeStage : ICutsStage
{
    private static readonly double[] Front = [0.6, 0.35, 1];

    private static readonly double[][] Vertices =
        [[0, 0, 0], [2, 0, 0], [2, 2, 0], [0, 2, 0], [0, 0, 0.2], [2, 0, 0.2], [2, 2, 0.2], [0, 2, 0.2]];

    private static readonly int[][] Faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    /// <summary>The hexagon, counter-clockwise seen from the front.</summary>
    private static readonly double[][] Hexagon =
        [[1.3, 0.9, 0.2], [1.6, 0.9, 0.2], [1.8, 1.2, 0.2], [1.6, 1.5, 0.2], [1.3, 1.5, 0.2], [1.1, 1.2, 0.2]];

    /// <summary>The door, up from the bottom edge and back down to it.</summary>
    private static readonly double[][] Door = [[0.3, 0, 0.2], [0.3, 1, 0.2], [0.7, 1, 0.2], [0.7, 0, 0.2]];

    /// <summary>The gatehouse's door brown, for the door piece.</summary>
    private const string DoorBrown = "#6b4423";

    /// <summary>The hexagon's inner face.</summary>
    private const string HexagonBlue = "#4a7a9b";

    private static readonly Vector3 Low = Vector3.Zero;
    private static readonly Vector3 High = new(2, 2, 0.2f);

    private static readonly float[] Steps = [0.24f, 0.74f, 1.26f, 1.76f];

    /// <summary>A 4 x 4 grid on the front, the top and the right, points inside the door and the hexagon (on the front's
    /// plane and on the recess's), and six points outside the slab.</summary>
    private static readonly (string Name, Vector3 At)[] Probes =
    [
        .. Grid("the front (z = 0.2)", (u, v) => new Vector3(u, v, 0.2f)),
        .. Grid("the top (y = 2)", (u, v) => new Vector3(u, 2, v / 10)),
        .. Grid("the right (x = 2)", (u, v) => new Vector3(2, u, v / 10)),
        .. new[] { 0.4f, 0.5f, 0.6f }.SelectMany(x => new[] { 0.2f, 0.5f, 0.8f }.Select(y => ("the door", new Vector3(x, y, 0.2f)))),
        .. new[] { 1.3f, 1.45f, 1.6f }.SelectMany(x => new[] { 1.05f, 1.2f, 1.35f }.Select(y => ("the recess", new Vector3(x, y, 0.15f)))),
        .. new[] { -0.3f, 2.3f }.SelectMany(x => new[] { -0.3f, 1f, 2.3f }.Select(y => ("outside the slab", new Vector3(x, y, 0.1f)))),
    ];

    private static IEnumerable<(string, Vector3)> Grid(string name, Func<float, float, Vector3> at) =>
        Steps.SelectMany(u => Steps.Select(v => (name, at(u, v))));

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The hexagon, closed: the front keeps its id with a hole, the inner face appended.
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        var hexagon = await CallJsonAsync(driver, "knife", new { points = Hexagon, closed = true });
        Summary("knife the hexagon", hexagon, 14, 7, true);
        SameList("knife the hexagon: created faces", ["6"], Ids(hexagon.GetProperty("created").GetProperty("faces").EnumerateArray()));
        var holed = await Solid.ReadAsync(driver);
        Equal("knife the hexagon: the front's holes", 1, holed.Faces[1].Holes.Length);

        // 2. The door, open, from the bottom edge: the front split, the hole in the piece around it.
        var door = await CallJsonAsync(driver, "knife", new { points = Door });
        Summary("knife the door", door, 18, 8, true);
        var cut = await Solid.ReadAsync(driver);
        var edges = Edges(cut);
        var paths = new[] { (Hexagon, true), (Door, false) }
            .SelectMany(p => Enumerable.Range(0, p.Item2 ? p.Item1.Length : p.Item1.Length - 1)
                .Select(k => (ToVector(p.Item1[k]), ToVector(p.Item1[(k + 1) % p.Item1.Length]))))
            .ToArray();
        bool Knifed((int Lo, int Hi) e)
        {
            var mid = (cut.Vertices[e.Lo] + cut.Vertices[e.Hi]) / 2;
            return paths.Any(s => DistanceToSegment(mid, s.Item1, s.Item2) < 1e-4f);
        }

        var knifed = edges.Where(Knifed).ToArray();
        Equal("the knifed edges", 9, knifed.Length);

        var camera = new Camera(Low, High, ToVector(Front), 512);
        var wire = await driver.CaptureAsync("knife-slab-wireframe", new
        {
            direction = Front,
            mode = "wireframe",
            highlight = new { edges = knifed.Select(e => new[] { e.Lo, e.Hi }).ToArray() },
        });
        Caption(wire, "512x512, direction (0.6, 0.35, 1), wireframe, highlight 9 edges, 18 vertices, 8 faces");
        var lines = edges.Select(e => (camera.Project(cut.Vertices[e.Lo]), camera.Project(cut.Vertices[e.Hi]))).ToArray();
        Look(wire, camera, cut, _ => Fill, lines, "the knifed slab, in the wireframe");

        // The edges on the faces turned to the camera (front, top, right) are in sight: the knifed ones orange, the others not.
        var dir = Vector3.Normalize(ToVector(Front));
        var facing = Enumerable.Range(0, cut.Faces.Length).Where(f => !cut.IsDeleted(f) && Vector3.Dot(cut.Normal(f), dir) > 0.2f).ToArray();
        var seen = Edges(cut, facing);
        Equal("the knifed edges in sight", 9, seen.Count(knifed.Contains));
        foreach (var (lo, hi) in seen)
        {
            var (a, b) = (cut.Vertices[lo], cut.Vertices[hi]);
            var (from, to) = (camera.Project(Vector3.Lerp(a, b, 0.2f)), camera.Project(Vector3.Lerp(a, b, 0.8f)));
            if (knifed.Contains((lo, hi)))
            {
                Line(wire, from, to, Orange, $"the knifed edge {lo}-{hi}");
            }
            else
            {
                NotLine(wire, from, to, Orange, $"the unknifed edge {lo}-{hi}");
            }
        }

        // 3. The faces the knife cut out coloured (the person's ruling, 2026-11-12), before the recess so that the pocket's
        // floor carries the colour: the door piece the gatehouse's door brown, the hexagon's inner face blue.
        var doorPiece = Enumerable.Range(0, cut.Faces.Length).Single(f => !cut.IsDeleted(f) && cut.Faces[f].Loop.All(v =>
            cut.Vertices[v] is var p && p.X is >= 0.3f - 1e-5f and <= 0.7f + 1e-5f && p.Y <= 1 + 1e-5f && MathF.Abs(p.Z - 0.2f) < 1e-5f));
        await CallJsonAsync(driver, "set_colour", new { faces = new[] { doorPiece }, colour = DoorBrown });
        await CallJsonAsync(driver, "set_colour", new { faces = new[] { 6 }, colour = HexagonBlue });

        // 4. The hexagon's inner face recessed by 0.05, shaded: a pocket in the front.
        var recess = await CallJsonAsync(driver, "extrude", new { faces = new[] { 6 }, distance = -0.05 });
        Summary("extrude the hexagon by -0.05", recess, 24, 14, true);
        var pocket = await Solid.ReadAsync(driver);
        Equal("the recessed hexagon at z = 0.15", true, pocket.Faces[6].Loop.All(v => MathF.Abs(pocket.Vertices[v].Z - 0.15f) < 1e-5f));
        var shaded = await driver.CaptureAsync("knife-slab-recess", new { direction = Front });
        Caption(shaded, "512x512, direction (0.6, 0.35, 1), 24 vertices, 14 faces");
        Expect(shaded, camera, pocket, "the recess", 6, 5);
        Look(shaded, camera, pocket, Shades(shaded, camera, pocket), [], "the slab with the hexagon recessed");
    }

    private static (int Lo, int Hi)[] Edges(Solid solid, IEnumerable<int>? faces = null) =>
        (faces ?? Enumerable.Range(0, solid.Faces.Length)).Where(f => !solid.IsDeleted(f))
            .SelectMany(f => solid.Faces[f].Holes.Prepend(solid.Faces[f].Loop))
            .SelectMany(l => l.Select((v, k) => (A: v, B: l[(k + 1) % l.Length])))
            .Select(e => (Lo: Math.Min(e.A, e.B), Hi: Math.Max(e.A, e.B))).Distinct().OrderBy(e => e).ToArray();

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var t = Math.Clamp(Vector3.Dot(p - a, b - a) / Vector3.Dot(b - a, b - a), 0, 1);
        return Vector3.Distance(p, a + (t * (b - a)));
    }

    private static IEnumerable<string> Ids(IEnumerable<JsonElement> ids) => ids.Select(i => $"{i.GetInt32()}");

    private static Vector3 ToVector(double[] d) => new((float)d[0], (float)d[1], (float)d[2]);

    private static void Summary(string what, JsonElement result, int vertices, int faces, bool closed)
    {
        Equal($"{what}: vertices", vertices, result.GetProperty("vertices").GetInt32());
        Equal($"{what}: faces", faces, result.GetProperty("faces").GetInt32());
        Equal($"{what}: closed", closed, result.GetProperty("closed").GetBoolean());
    }

    /// <summary>
    /// The ray test must give <paramref name="face"/> at every probe named <paramref name="name"/> it is sure of, and be
    /// sure of at least <paramref name="least"/> of them.
    /// </summary>
    private static void Expect(ScenarioFrame frame, Camera camera, Solid solid, string name, int face, int least)
    {
        var sure = 0;
        foreach (var (_, at) in Probes.Where(p => p.Name == name))
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

    /// <summary>Each face's shade, read at the first probe that surely shows it.</summary>
    private static Func<int, Rgb?> Shades(ScenarioFrame frame, Camera camera, Solid solid)
    {
        var shades = new Dictionary<int, Rgb>();
        foreach (var (_, at) in Probes)
        {
            var (x, y) = camera.Pixel(at);
            var seen = solid.Seen(camera, frame.Width, x, y);
            if (seen >= 0 && !shades.ContainsKey(seen))
            {
                var p = frame.Pixel(x, y);
                shades[seen] = (p.R, p.G, p.B);
            }
        }

        return f => shades.TryGetValue(f, out var c) ? c : null;
    }

    /// <summary>
    /// Every probe the camera surely shows as a face or the background, and more than 4 pixels from a drawn line: the
    /// background where the ray meets nothing, else <paramref name="colourOf"/> the face it meets, and never the background.
    /// </summary>
    private static void Look(ScenarioFrame frame, Camera camera, Solid solid, Func<int, Rgb?> colourOf, (Vector2 From, Vector2 To)[] lines, string what)
    {
        Size(frame, 512, 512);
        Corners(frame);
        var tested = 0;
        foreach (var (name, at) in Probes)
        {
            var (x, y) = camera.Pixel(at);
            var seen = solid.Seen(camera, frame.Width, x, y);
            if (seen == -2 || lines.Any(l => Checks.DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), l.From, l.To) < 4))
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
