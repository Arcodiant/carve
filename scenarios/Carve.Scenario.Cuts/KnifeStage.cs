using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;
using static Carve.Scenario.Cuts.CutsChecks;

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
        .. Grid("the front (z = 0.2)", Steps, (u, v) => new Vector3(u, v, 0.2f)),
        .. Grid("the top (y = 2)", Steps, (u, v) => new Vector3(u, 2, v / 10)),
        .. Grid("the right (x = 2)", Steps, (u, v) => new Vector3(2, u, v / 10)),
        .. new[] { 0.4f, 0.5f, 0.6f }.SelectMany(x => new[] { 0.2f, 0.5f, 0.8f }.Select(y => ("the door", new Vector3(x, y, 0.2f)))),
        .. new[] { 1.3f, 1.45f, 1.6f }.SelectMany(x => new[] { 1.05f, 1.2f, 1.35f }.Select(y => ("the recess", new Vector3(x, y, 0.15f)))),
        .. new[] { -0.3f, 2.3f }.SelectMany(x => new[] { -0.3f, 1f, 2.3f }.Select(y => ("outside the slab", new Vector3(x, y, 0.1f)))),
    ];

    /// <summary>Points on the pocket's three walls in sight, halfway down (z = 0.175): the bottom one (y = 0.9, facing +Y) and
    /// the two slanted ones on the left, each a third, a half and two thirds along.</summary>
    private static readonly (string Name, Vector3 At)[] Walls =
    [
        .. Along("the bottom wall", new Vector2(1.3f, 0.9f), new Vector2(1.6f, 0.9f)),
        .. Along("the lower left wall", new Vector2(1.1f, 1.2f), new Vector2(1.3f, 0.9f)),
        .. Along("the upper left wall", new Vector2(1.3f, 1.5f), new Vector2(1.1f, 1.2f)),
    ];

    private static IEnumerable<(string, Vector3)> Along(string name, Vector2 a, Vector2 b) =>
        new[] { 1 / 3f, 0.5f, 2 / 3f }.Select(t => (name, new Vector3(Vector2.Lerp(a, b, t), 0.175f)));

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
        var edges = cut.Edges();
        var paths = new[] { (Hexagon, true), (Door, false) }
            .SelectMany(p => Enumerable.Range(0, p.Item2 ? p.Item1.Length : p.Item1.Length - 1)
                .Select(k => (ToVector(p.Item1[k]), ToVector(p.Item1[(k + 1) % p.Item1.Length]))))
            .ToArray();
        bool Knifed((int Lo, int Hi) e)
        {
            var mid = (cut.Vertices[e.Lo] + cut.Vertices[e.Hi]) / 2;
            return paths.Any(s => Solid.SegmentDistance(mid, s.Item1, s.Item2) < 1e-4f);
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
        Look(wire, camera, cut, Probes, _ => Fill, lines, "the knifed slab, in the wireframe");

        // The edges on the faces turned to the camera (front, top, right) are in sight: the knifed ones orange, the others not.
        var dir = Vector3.Normalize(ToVector(Front));
        var facing = Enumerable.Range(0, cut.Faces.Length).Where(f => !cut.IsDeleted(f) && Vector3.Dot(cut.Normal(f), dir) > 0.2f).ToArray();
        var seen = cut.Edges(facing);
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
        Expect(shaded, camera, pocket, Probes, "the recess", 6, 5);
        Look(shaded, camera, pocket, Probes, Shades(shaded, camera, pocket), [], "the slab with the hexagon recessed");
        Predicted(shaded, camera, pocket, doorPiece);
    }

    /// <summary>
    /// The person's rulings on shade prediction (2026-11-12). A coloured face's pixel is predicted by the Crate scenario's
    /// rule (<see cref="ColourRule"/>) from a grey face of the same normal in the same frame, at <see cref="Checks.Exact"/>:
    /// the door piece and the pocket floor from the front (face 1), the pocket's bottom wall (+Y, blue as the extrude
    /// leaves it) from the slab's top (face 3). The two slanted walls have no face of the same normal, so each of their
    /// probes must differ from the pocket floor's shade by more than <see cref="Checks.Exact"/> in at least one channel.
    /// </summary>
    private static void Predicted(ScenarioFrame frame, Camera camera, Solid solid, int doorPiece)
    {
        Equal("the door piece is not the front", true, doorPiece != 1);
        var front = Reference(frame, camera, solid, 1, false, "the front");
        var top = Reference(frame, camera, solid, 3, true, "the top");

        // The extrude's walls round the hexagon, each found by its normal: +Y, and the two facing +X (lower and upper).
        int Wall(Func<Vector3, bool> facing, string what)
        {
            var walls = Enumerable.Range(0, solid.Faces.Length).Where(f => !solid.IsDeleted(f) && f != 6
                && solid.Faces[f].Loop.All(v => solid.Vertices[v] is var p && p.Z is >= 0.15f - 1e-5f and <= 0.2f + 1e-5f
                    && p.X is >= 1.1f - 1e-5f and <= 1.8f + 1e-5f && p.Y is >= 0.9f - 1e-5f and <= 1.5f + 1e-5f)
                && facing(solid.Normal(f))).ToArray();
            Equal($"{what}: one face", 1, walls.Length);
            return walls.Length == 1 ? walls[0] : -1;
        }

        var bottom = Wall(n => n.Y > 0.99f, "the bottom wall");
        var lowerLeft = Wall(n => n.X > 0.5f && n.Y > 0.3f, "the lower left wall");
        var upperLeft = Wall(n => n.X > 0.5f && n.Y < -0.3f, "the upper left wall");
        if (front is not { } f || top is not { } t)
        {
            return;
        }

        var floor = Predict(f, HexagonBlue, solid.Normal(1), camera.Direction);
        Coloured(frame, camera, solid, Probes.Where(p => p.Name == "the door"), doorPiece, Predict(f, DoorBrown, solid.Normal(1), camera.Direction), "the door piece, brown, from the front", false);
        Coloured(frame, camera, solid, Probes.Where(p => p.Name == "the recess"), 6, floor, "the pocket floor, blue, from the front", false);
        Coloured(frame, camera, solid, Walls.Where(p => p.Name == "the bottom wall"), bottom, Predict(t, HexagonBlue, solid.Normal(3), camera.Direction), "the bottom wall, blue, from the top", true);
        foreach (var (wall, name) in new[] { (lowerLeft, "the lower left wall"), (upperLeft, "the upper left wall") })
        {
            var sure = 0;
            foreach (var (_, at) in Walls.Where(p => p.Name == name))
            {
                var (x, y) = camera.Pixel(at);
                if (Shows(frame, camera, solid, x, y, wall, true, $"{name} {at}") is not { } p)
                {
                    continue;
                }

                sure++;
                if (Math.Abs(p.R - floor.R) <= Exact && Math.Abs(p.G - floor.G) <= Exact && Math.Abs(p.B - floor.B) <= Exact)
                {
                    Note($"{frame.Name}: {name} {at} at ({x}, {y}) is {p}, within {Exact} of the pocket floor's {floor} in every channel: not shaded as a wall");
                }
            }

            Least(frame, name, sure, 1);
        }
    }

    /// <summary>The grey pixel of <paramref name="face"/> at the first probe that surely shows it.</summary>
    private static Rgb? Reference(ScenarioFrame frame, Camera camera, Solid solid, int face, bool narrow, string what)
    {
        foreach (var (_, at) in Probes)
        {
            var (x, y) = camera.Pixel(at);
            if ((narrow ? solid.SeenNarrow(camera, frame.Width, x, y) : solid.Seen(camera, frame.Width, x, y)) == face)
            {
                var p = frame.Pixel(x, y);
                return (p.R, p.G, p.B);
            }
        }

        Note($"{frame.Name}: no probe surely shows {what} (face {face}), the reference for a coloured face");
        return null;
    }

    /// <summary>Each probe the ray test is sure of must show <paramref name="face"/>, in <paramref name="colour"/> within <see cref="Checks.Exact"/>.</summary>
    private static void Coloured(ScenarioFrame frame, Camera camera, Solid solid, IEnumerable<(string Name, Vector3 At)> probes, int face, Rgb colour, string what, bool narrow)
    {
        var sure = 0;
        foreach (var (_, at) in probes)
        {
            var (x, y) = camera.Pixel(at);
            if (Shows(frame, camera, solid, x, y, face, narrow, $"{what} {at}") is null)
            {
                continue;
            }

            sure++;
            Colour(frame, (x, y), colour, Exact, $"{what} {at}, predicted {colour}");
        }

        Least(frame, what, sure, 1);
    }

    /// <summary>
    /// The pixel, if the ray test is sure of it; it must then show <paramref name="face"/>. A face only a few pixels across
    /// (the slab's top, a pocket wall) is never sure by <see cref="Solid.Seen"/>, so for it <paramref name="narrow"/> asks
    /// <see cref="Solid.SeenNarrow"/>.
    /// </summary>
    private static Rgb? Shows(ScenarioFrame frame, Camera camera, Solid solid, int x, int y, int face, bool narrow, string what)
    {
        var seen = narrow ? solid.SeenNarrow(camera, frame.Width, x, y) : solid.Seen(camera, frame.Width, x, y);
        if (seen == -2)
        {
            return null;
        }

        Equal($"{frame.Name}: what the geometry shows at {what} (pixel ({x}, {y}))", face, seen);
        var p = frame.Pixel(x, y);
        return (p.R, p.G, p.B);
    }

    private static void Least(ScenarioFrame frame, string what, int sure, int least)
    {
        if (sure < least)
        {
            Note($"{frame.Name}: only {sure} probes on {what} were sure, fewer than {least}");
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
}
