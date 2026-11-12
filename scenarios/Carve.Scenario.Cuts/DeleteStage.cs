using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Cuts;

using Rgb = (int R, int G, int B);

/// <summary>
/// T-64, delete: define_mesh's unit cube with its top face deleted, seen from <c>[0.6, 0.35, 1]</c>, so the inside of
/// the far walls is culled and the opening shows the background; then the same cube with its top corner vertex 6
/// (1, 1, 1) deleted, which takes its three faces (front, top, right), from the same direction in the wireframe. Every
/// pixel asked about is worked from get_mesh's geometry, tombstones and all, by a ray test (<see cref="Solid.FirstHit"/>).
/// </summary>
internal sealed class DeleteStage : ICutsStage
{
    private static readonly double[] Direction = [0.6, 0.35, 1];

    private static readonly double[][] Vertices =
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];

    private static readonly int[][] Faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    /// <summary>A 4 x 4 grid of points on each of the cube's six planes, named by the plane.</summary>
    private static readonly (string Name, Vector3 At)[] Probes =
    [
        .. Grid("the back (z = 0)", (u, v) => new Vector3(u, v, 0)),
        .. Grid("the front (z = 1)", (u, v) => new Vector3(u, v, 1)),
        .. Grid("the bottom (y = 0)", (u, v) => new Vector3(u, 0, v)),
        .. Grid("the top (y = 1)", (u, v) => new Vector3(u, 1, v)),
        .. Grid("the left (x = 0)", (u, v) => new Vector3(0, u, v)),
        .. Grid("the right (x = 1)", (u, v) => new Vector3(1, u, v)),
        .. new[] { -0.15f, 1.15f }.SelectMany(x => new[] { -0.15f, 0.5f, 1.15f }.Select(y => ("outside the cube", new Vector3(x, y, 0.5f)))),
    ];

    private static IEnumerable<(string, Vector3)> Grid(string name, Func<float, float, Vector3> at) =>
        new[] { 0.12f, 0.37f, 0.63f, 0.88f }.SelectMany(u => new[] { 0.12f, 0.37f, 0.63f, 0.88f }.Select(v => (name, at(u, v))));

    public async Task RunAsync(ScenarioDriver driver)
    {
        var camera = new Camera(Vector3.Zero, Vector3.One, ToVector(Direction), 512);

        // 1. The top deleted: an open box. Through the opening, the back, left and bottom face away and are culled.
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        var topless = await CallJsonAsync(driver, "delete", new { faces = new[] { 3 } });
        Summary("delete faces [3]", topless, 8, 5, false);
        Deleted("delete faces [3]", topless, [], ["3"]);
        await Nulls(driver, "get_mesh after delete faces [3]", 8, [], 6, ["3"]);
        var open = await Solid.ReadAsync(driver);
        var shaded = await driver.CaptureAsync("delete-top", new { direction = Direction });
        Caption(shaded, "512x512, direction (0.6, 0.35, 1), 8 vertices, 5 faces");
        Expect(shaded, camera, open, "the top (y = 1)", -1, 12);
        Expect(shaded, camera, open, "the front (z = 1)", 1, 12);
        Expect(shaded, camera, open, "the right (x = 1)", 5, 12);
        var shades = new Dictionary<int, Rgb> { [1] = Shade(shaded, camera, open, "the front (z = 1)", 1), [5] = Shade(shaded, camera, open, "the right (x = 1)", 5) };
        Look(shaded, camera, open, f => shades.TryGetValue(f, out var c) ? c : null, [], "the cube with its top deleted");

        // 2. Vertex 6 deleted: it takes faces 1, 3 and 5, and no other vertex (each is still on a live face). The three
        // faces left all face away and are culled, so the wireframe shows their edges over the background.
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        var cornerless = await CallJsonAsync(driver, "delete", new { vertices = new[] { 6 } });
        Summary("delete vertices [6]", cornerless, 7, 3, false);
        Deleted("delete vertices [6]", cornerless, ["6"], ["1", "3", "5"]);
        await Nulls(driver, "get_mesh after delete vertices [6]", 8, ["6"], 6, ["1", "3", "5"]);
        var corner = await Solid.ReadAsync(driver);
        var wire = await driver.CaptureAsync("delete-corner-wireframe", new { direction = Direction, mode = "wireframe" });
        Caption(wire, "512x512, direction (0.6, 0.35, 1), wireframe, 7 vertices, 3 faces");
        var live = Enumerable.Range(0, corner.Faces.Length).Where(f => !corner.IsDeleted(f)).ToArray();
        var edges = live.SelectMany(f => corner.Faces[f].Loop.Select((v, k) => (A: v, B: corner.Faces[f].Loop[(k + 1) % corner.Faces[f].Loop.Length])))
            .Select(e => (Lo: Math.Min(e.A, e.B), Hi: Math.Max(e.A, e.B))).Distinct().OrderBy(e => e).ToArray();
        Equal("the cornerless cube's edges", 9, edges.Length);
        Expect(wire, camera, corner, "the top (y = 1)", -1, 12);
        Expect(wire, camera, corner, "the front (z = 1)", -1, 12);
        Expect(wire, camera, corner, "the right (x = 1)", -1, 12);
        var lines = edges.Select(e => (camera.Project(corner.Vertices[e.Lo]), camera.Project(corner.Vertices[e.Hi]))).ToArray();
        Look(wire, camera, corner, _ => Fill, lines, "the cornerless cube's wireframe");
        foreach (var (lo, hi) in edges)
        {
            var (a, b) = (corner.Vertices[lo], corner.Vertices[hi]);
            for (var part = 0; part < 4; part++)
            {
                var (from, to) = (0.1f + 0.2f * part, 0.3f + 0.2f * part);
                Drawn(wire, camera.Project(Vector3.Lerp(a, b, from)), camera.Project(Vector3.Lerp(a, b, to)),
                    $"the edge {lo}-{hi}, {from:0.0} to {to:0.0} along it");
            }
        }

        // No line where vertex 6's edges were: the middles of 2-6, 5-6 and 6-7 are the background.
        foreach (var other in new[] { 2, 5, 7 })
        {
            var (x, y) = camera.Pixel(Vector3.Lerp(Vector3.One, corner.Vertices[other], 0.5f));
            Background(wire, (x, y), $"the middle of the deleted edge {other}-6");
        }
    }

    /// <summary>
    /// A line over the background, which here often falls across two pixels and fills neither (a near-vertical edge at
    /// x = 219.2 reads 210 and 133 side by side), so <see cref="Checks.Line"/>'s one pixel within 10 of the line's colour
    /// does not fit. Instead every one of 17 points along the stretch must have, within one pixel, a grey (channels within
    /// 3 of each other) at least half way from the background to <see cref="Checks.Edge"/>: the line is there all along it.
    /// </summary>
    private static void Drawn(ScenarioFrame frame, Vector2 from, Vector2 to, string what)
    {
        var half = (48 + Edge.R) / 2;
        for (var step = 0; step <= 16; step++)
        {
            var p = Vector2.Lerp(from, to, step / 16f);
            var (x, y) = ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));
            var found = false;
            for (var dy = -1; dy <= 1 && !found; dy++)
            for (var dx = -1; dx <= 1 && !found; dx++)
            {
                var c = frame.Pixel(x + dx, y + dy);
                found = c.R >= half && Math.Abs(c.R - c.G) <= 3 && Math.Abs(c.G - c.B) <= 3;
            }

            if (!found)
            {
                Note($"{frame.Name}: {what}: no line pixel within one of ({x}, {y}), {step}/16 along {from} to {to}");
                return;
            }
        }
    }

    private static Vector3 ToVector(double[] d) => new((float)d[0], (float)d[1], (float)d[2]);

    private static void Summary(string what, JsonElement result, int vertices, int faces, bool closed)
    {
        Equal($"{what}: vertices", vertices, result.GetProperty("vertices").GetInt32());
        Equal($"{what}: faces", faces, result.GetProperty("faces").GetInt32());
        Equal($"{what}: closed", closed, result.GetProperty("closed").GetBoolean());
    }

    /// <summary>get_mesh keeps every slot, and writes null in exactly the deleted ones.</summary>
    private static async Task Nulls(ScenarioDriver driver, string what, int vertexSlots, string[] vertices, int faceSlots, string[] faces)
    {
        var mesh = JsonDocument.Parse(await CallTextAsync(driver, "get_mesh", new { })).RootElement;
        foreach (var (key, slots, expected) in new[] { ("vertices", vertexSlots, vertices), ("faces", faceSlots, faces) })
        {
            var all = mesh.GetProperty(key).EnumerateArray().ToArray();
            Equal($"{what}: {key} slots", slots, all.Length);
            SameList($"{what}: the null {key}", expected, Enumerable.Range(0, all.Length).Where(i => all[i].ValueKind == JsonValueKind.Null).Select(i => $"{i}"));
        }
    }

    private static void Deleted(string what, JsonElement result, string[] vertices, string[] faces)
    {
        var deleted = result.GetProperty("deleted");
        SameList($"{what}: deleted.vertices", vertices, deleted.GetProperty("vertices").EnumerateArray().Select(v => $"{v.GetInt32()}"));
        SameList($"{what}: deleted.faces", faces, deleted.GetProperty("faces").EnumerateArray().Select(f => $"{f.GetInt32()}"));
    }

    /// <summary>
    /// The ray test must give <paramref name="face"/> at every probe named <paramref name="name"/> it is sure of, and be
    /// sure of at least <paramref name="least"/> of them, so the checks do not rest on probes left out as uncertain.
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

    /// <summary>A face's shade, read at the first probe on <paramref name="name"/> that surely shows it, not the background.</summary>
    private static Rgb Shade(ScenarioFrame frame, Camera camera, Solid solid, string name, int face)
    {
        foreach (var (_, at) in Probes.Where(p => p.Name == name))
        {
            var (x, y) = camera.Pixel(at);
            if (solid.Seen(camera, frame.Width, x, y) == face)
            {
                NotBackground(frame, (x, y), $"face {face}'s shade, at {name} {at}");
                var p = frame.Pixel(x, y);
                return (p.R, p.G, p.B);
            }
        }

        Note($"{frame.Name}: no probe on {name} surely shows face {face}");
        return (0, 0, 0);
    }

    /// <summary>
    /// Every probe the camera surely shows as a face or the background, and more than 4 pixels from a drawn line: the
    /// background where the ray meets nothing, else <paramref name="colourOf"/> the face it meets, or at least the mesh.
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
            }
            else if (colourOf(seen) is { } colour)
            {
                Colour(frame, (x, y), colour, Exact, $"{what}: {name} {at}, face {seen}");
            }
            else
            {
                NotBackground(frame, (x, y), $"{what}: {name} {at}, face {seen}");
            }
        }

        if (tested < 40)
        {
            Note($"{frame.Name}: only {tested} probes were surely a face or the background");
        }
    }
}
