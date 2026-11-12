using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Cuts;

using Rgb = (int R, int G, int B);

/// <summary>
/// T-65, plane_cut: define_mesh's unit cube cut diagonally by the plane x + 2y - z = 1.2 (through [0.5, 0.6, 0.5], normal
/// [1, 2, -1]), keeping below with fill, seen from <c>[0.6, 0.35, 1]</c> and from <c>[-0.6, 0.35, -1]</c>: the cap is a
/// pentagon facing up, the corner above it (vertices 2, 3, 6) gone. Then the same cut with <c>keep: both</c> in the
/// wireframe from <c>[0.6, 0.35, 1]</c>, the cut edges highlighted. Every pixel asked about is worked from get_mesh's
/// geometry by a ray test (<see cref="Solid.FirstHit"/>).
/// </summary>
internal sealed class PlaneCutStage : ICutsStage
{
    private static readonly double[] Front = [0.6, 0.35, 1];
    private static readonly double[] Back = [-0.6, 0.35, -1];
    private static readonly double[] Point = [0.5, 0.6, 0.5];
    private static readonly double[] Normal = [1, 2, -1];

    private static readonly double[][] Vertices =
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];

    private static readonly int[][] Faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    /// <summary>The plane's height over (x, z): x + 2y - z = 1.2.</summary>
    private static float PlaneY(float x, float z) => (1.2f - x + z) / 2;

    private static readonly float[] Steps = [0.12f, 0.37f, 0.63f, 0.88f];

    /// <summary>A 4 x 4 grid on each of the cube's six planes, six points outside it, and the points of a grid over x and z on the cut plane inside the cube.</summary>
    private static readonly (string Name, Vector3 At)[] Probes =
    [
        .. Grid("the back (z = 0)", (u, v) => new Vector3(u, v, 0)),
        .. Grid("the front (z = 1)", (u, v) => new Vector3(u, v, 1)),
        .. Grid("the bottom (y = 0)", (u, v) => new Vector3(u, 0, v)),
        .. Grid("the top (y = 1)", (u, v) => new Vector3(u, 1, v)),
        .. Grid("the left (x = 0)", (u, v) => new Vector3(0, u, v)),
        .. Grid("the right (x = 1)", (u, v) => new Vector3(1, u, v)),
        .. new[] { -0.15f, 1.15f }.SelectMany(x => new[] { -0.15f, 0.5f, 1.15f }.Select(y => ("outside the cube", new Vector3(x, y, 0.5f)))),
        .. Grid("the cut", (x, z) => new Vector3(x, PlaneY(x, z), z)).Where(p => p.Item2.Y is > 0.05f and < 0.95f),
    ];

    private static IEnumerable<(string, Vector3)> Grid(string name, Func<float, float, Vector3> at) =>
        Steps.SelectMany(u => Steps.Select(v => (name, at(u, v))));

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. Keeping below with fill: a closed solid, the corner above the plane gone and the cut capped.
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        var below = await CallJsonAsync(driver, "plane_cut", new { point = Point, normal = Normal, keep = "below" });
        Summary("plane_cut keep below", below, 10, 7, true);
        SameList("plane_cut keep below: split faces", ["0", "1", "3", "4", "5"], Ids(below.GetProperty("split").EnumerateArray().Select(s => s.GetProperty("face"))));
        SameList("plane_cut keep below: deleted.vertices", ["2", "3", "6"], Ids(below.GetProperty("deleted").GetProperty("vertices").EnumerateArray()));
        var created = below.GetProperty("created").GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()).ToArray();
        var cap = created.Length == 0 ? -1 : created.Max();
        var solid = await Solid.ReadAsync(driver);
        Equal("the cap's corners", 5, cap < 0 ? 0 : solid.Faces[cap].Loop.Length);
        foreach (var (name, direction, caption) in new[]
        {
            ("plane-cut-below-front", Front, "512x512, direction (0.6, 0.35, 1), 10 vertices, 7 faces"),
            ("plane-cut-below-back", Back, "512x512, direction (-0.6, 0.35, -1), 10 vertices, 7 faces"),
        })
        {
            var camera = new Camera(Vector3.Zero, Vector3.One, ToVector(direction), 512);
            var frame = await driver.CaptureAsync(name, new { direction });
            Caption(frame, caption);
            Expect(frame, camera, solid, "the cut", cap, 8);
            Look(frame, camera, solid, Shades(frame, camera, solid), [], "the cube cut below the plane");
        }

        // 2. Keeping both: only split, the cut edges highlighted in the wireframe.
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        var both = await CallJsonAsync(driver, "plane_cut", new { point = Point, normal = Normal });
        Summary("plane_cut keep both", both, 13, 11, true);
        Equal("plane_cut keep both: deleted faces", 0, both.GetProperty("deleted").GetProperty("faces").GetArrayLength());
        var whole = await Solid.ReadAsync(driver);
        var n = Vector3.Normalize(ToVector(Normal));
        var p0 = ToVector(Point);
        bool OnPlane(int v) => MathF.Abs(Vector3.Dot(whole.Vertices[v] - p0, n)) < 1e-4f;
        var edges = Enumerable.Range(0, whole.Faces.Length).Where(f => !whole.IsDeleted(f))
            .SelectMany(f => whole.Faces[f].Loop.Select((v, k) => (A: v, B: whole.Faces[f].Loop[(k + 1) % whole.Faces[f].Loop.Length])))
            .Select(e => (Lo: Math.Min(e.A, e.B), Hi: Math.Max(e.A, e.B))).Distinct().OrderBy(e => e).ToArray();
        var cut = edges.Where(e => OnPlane(e.Lo) && OnPlane(e.Hi)).ToArray();
        Equal("the cut edges of keep both", 5, cut.Length);

        var front = new Camera(Vector3.Zero, Vector3.One, ToVector(Front), 512);
        var wire = await driver.CaptureAsync("plane-cut-both-wireframe", new
        {
            direction = Front,
            mode = "wireframe",
            highlight = new { edges = cut.Select(e => new[] { e.Lo, e.Hi }).ToArray() },
        });
        Caption(wire, "512x512, direction (0.6, 0.35, 1), wireframe, highlight 5 edges, 13 vertices, 11 faces");
        var lines = edges.Select(e => (front.Project(whole.Vertices[e.Lo]), front.Project(whole.Vertices[e.Hi]))).ToArray();
        Look(wire, front, whole, _ => Fill, lines, "the cube split by the plane, in the wireframe");

        // The edges on the faces turned to the camera (front, top, right) are in sight: the cut ones orange, the others not.
        var dir = Vector3.Normalize(ToVector(Front));
        var facing = Enumerable.Range(0, whole.Faces.Length).Where(f => !whole.IsDeleted(f) && Vector3.Dot(whole.Normal(f), dir) > 0.2f).ToArray();
        var seen = facing.SelectMany(f => whole.Faces[f].Loop.Select((v, k) => (A: v, B: whole.Faces[f].Loop[(k + 1) % whole.Faces[f].Loop.Length])))
            .Select(e => (Lo: Math.Min(e.A, e.B), Hi: Math.Max(e.A, e.B))).Distinct().OrderBy(e => e).ToArray();
        var seenCut = seen.Where(cut.Contains).ToArray();
        Equal("the cut edges in sight", 3, seenCut.Length);
        foreach (var (lo, hi) in seen)
        {
            var (a, b) = (whole.Vertices[lo], whole.Vertices[hi]);
            var (from, to) = (front.Project(Vector3.Lerp(a, b, 0.2f)), front.Project(Vector3.Lerp(a, b, 0.8f)));
            if (cut.Contains((lo, hi)))
            {
                Line(wire, from, to, Orange, $"the cut edge {lo}-{hi}");
            }
            else
            {
                NotLine(wire, from, to, Orange, $"the uncut edge {lo}-{hi}");
            }
        }
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
