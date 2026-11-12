using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Cuts;

using Rgb = (int R, int G, int B);

/// <summary>
/// T-63, the holed plate: a 2 x 2 plate in the XY plane, 0.2 thick, a box whose front face (+Z) has a 1 x 1 square hole,
/// first with the inner square kept as its own face, then redefined without it so the opening shows the background
/// through the back face's culled inside. Every pixel asked about is worked from the geometry by a ray test
/// (<see cref="Solid.FirstHit"/>), at the plate's corners as well as its middles and the hole's.
/// </summary>
internal sealed class HolesStage : ICutsStage
{
    private const float Front = 0.1f;
    private static readonly Vector3 Min = new(-1, -1, -Front);
    private static readonly Vector3 Max = new(1, 1, Front);
    private static readonly double[] Direction = [0.6, 0.35, 1];

    private static readonly double[][] Vertices =
    [
        [-1, -1, -0.1], [1, -1, -0.1], [1, 1, -0.1], [-1, 1, -0.1], // 0-3 the back
        [-1, -1, 0.1], [1, -1, 0.1], [1, 1, 0.1], [-1, 1, 0.1], // 4-7 the front's outer loop
        [-0.5, -0.5, 0.1], [-0.5, 0.5, 0.1], [0.5, 0.5, 0.1], [0.5, -0.5, 0.1], // 8-11 the hole, clockwise seen from +Z
    ];

    private static readonly object FrontFace = new { loop = new[] { 4, 5, 6, 7 }, holes = new[] { new[] { 8, 9, 10, 11 } } };
    private static readonly int[] Inner = [8, 11, 10, 9];
    private static readonly int[][] Box = [[0, 3, 2, 1], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];

    /// <summary>The points asked about, on the front's plane unless on a side.</summary>
    private static readonly (string Name, Vector3 At)[] Probes =
    [
        ("the hole's centre", new Vector3(0, 0, Front)),
        .. Signs().Select(s => ("inside the hole, by its corner", new Vector3(0.4f * s.X, 0.4f * s.Y, Front))),
        .. Sides().Select(s => ("inside the hole, by its side", new Vector3(0.4f * s.X, 0.4f * s.Y, Front))),
        .. Signs().Select(s => ("the plate, by its corner", new Vector3(0.9f * s.X, 0.9f * s.Y, Front))),
        .. Sides().Select(s => ("the plate, at its side's middle", new Vector3(0.75f * s.X, 0.75f * s.Y, Front))),
        .. Signs().Select(s => ("the plate, by the hole's corner", new Vector3(0.6f * s.X, 0.6f * s.Y, Front))),
        .. Sides().SelectMany(s => new[] { -0.8f, 0, 0.8f }.Select(t => ("a side of the plate", new Vector3(s.X == 0 ? t : s.X, s.Y == 0 ? t : s.Y, 0)))),
        .. Signs().Select(s => ("outside the plate, by its corner", new Vector3(1.1f * s.X, 1.1f * s.Y, Front))),
        .. Sides().Select(s => ("outside the plate, by its side", new Vector3(1.12f * s.X, 1.12f * s.Y, Front))),
    ];

    private static (float X, float Y)[] Signs() => [(-1, -1), (1, -1), (1, 1), (-1, 1)];

    private static (float X, float Y)[] Sides() => [(0, -1), (1, 0), (0, 1), (-1, 0)];

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The plate with the inner face: closed, and the hole's centre is the inner face, in the plate's shade.
        var filled = await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = new object[] { FrontFace, Inner }.Concat(Box).ToArray() });
        Summary("define_mesh, the plate with its inner face", filled, 7, true);
        var plate = await Solid.ReadAsync(driver);
        SameList("get_mesh: face 0's loop", ["4", "5", "6", "7"], plate.Faces[0].Loop.Select(v => $"{v}"));
        SameList("get_mesh: face 0's holes", ["8 9 10 11"], plate.Faces[0].Holes.Select(h => string.Join(" ", h)));
        SameList("get_mesh: face 1, the inner face", ["8", "11", "10", "9"], plate.Faces[1].Loop.Select(v => $"{v}"));
        var camera = new Camera(Min, Max, ToVector(Direction), 512);
        var shaded = await driver.CaptureAsync("holes-plate", new { direction = Direction });
        Caption(shaded, "512x512, direction (0.6, 0.35, 1), 12 vertices, 7 faces");
        Expect(shaded, camera, plate, "the hole's centre", 1);
        Expect(shaded, camera, plate, "the plate, by its corner", 0);
        var shade = PlateShade(shaded, camera);
        Look(shaded, camera, plate, f => Facing(plate, f) ? shade : null, [], "the plate with its inner face");

        // 2. The inner face removed: the four hole edges are open, and through the hole is the background.
        var open = await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = new object[] { FrontFace }.Concat(Box).ToArray() });
        Summary("define_mesh, the plate without its inner face", open, 6, false);
        var openEdges = await CallJsonAsync(driver, "select", new { kind = "edges", where = "open" });
        SameList("edges where open: the hole's four", ["8-9", "8-11", "9-10", "10-11"],
            openEdges.GetProperty("edges").EnumerateArray().Select(e => $"{e[0].GetInt32()}-{e[1].GetInt32()}"));
        var holed = await Solid.ReadAsync(driver);
        var through = await driver.CaptureAsync("holes-open", new { direction = Direction });
        Caption(through, "512x512, direction (0.6, 0.35, 1), 12 vertices, 6 faces");
        Opening(through, camera, holed);
        Look(through, camera, holed, f => Facing(holed, f) ? shade : null, [], "the plate without its inner face");

        var straight = new Camera(Min, Max, Vector3.UnitZ, 512, orthographic: true);
        var ortho = await driver.CaptureAsync("holes-open-ortho", new { direction = new[] { 0, 0, 1 }, projection = "orthographic" });
        Caption(ortho, "512x512, direction (0, 0, 1), orthographic, 12 vertices, 6 faces");
        Opening(ortho, straight, holed);
        var orthoShade = PlateShade(ortho, straight);
        Look(ortho, straight, holed, f => Facing(holed, f) ? orthoShade : null, [], "the plate without its inner face, orthographic");

        // 3. The wireframe: the hole's edges and the front's outer edges drawn, the faces filled, the opening background.
        var wire = await driver.CaptureAsync("holes-open-wireframe", new { direction = Direction, mode = "wireframe" });
        Caption(wire, "512x512, direction (0.6, 0.35, 1), wireframe, 12 vertices, 6 faces");
        Opening(wire, camera, holed);
        var lines = holed.Faces.SelectMany(f => new[] { f.Loop }.Concat(f.Holes))
            .SelectMany(loop => loop.Select((v, k) => (camera.Project(holed.Vertices[v]), camera.Project(holed.Vertices[loop[(k + 1) % loop.Length]]))))
            .ToArray();
        Look(wire, camera, holed, _ => Fill, lines, "the wireframe");
        foreach (var (loop, what) in new[] { (holed.Faces[0].Holes.FirstOrDefault() ?? [], "the hole's edge"), (holed.Faces[0].Loop, "the front's outer edge") })
        {
            for (var k = 0; k < loop.Length; k++)
            {
                // In four stretches, each of which must show the line: Line samples 17 points of a stretch, which on a
                // long steep edge can all fall where the line covers two pixels part way and none in full.
                var (a, b) = (holed.Vertices[loop[k]], holed.Vertices[loop[(k + 1) % loop.Length]]);
                for (var part = 0; part < 4; part++)
                {
                    var (from, to) = (0.1f + 0.2f * part, 0.3f + 0.2f * part);
                    Line(wire, camera.Project(Vector3.Lerp(a, b, from)), camera.Project(Vector3.Lerp(a, b, to)), Edge,
                        $"{what} {loop[k]}-{loop[(k + 1) % loop.Length]}, {from:0.0} to {to:0.0} along it");
                }
            }
        }
    }

    private static Vector3 ToVector(double[] d) => new((float)d[0], (float)d[1], (float)d[2]);

    private static bool Facing(Solid solid, int face) => solid.Normal(face).Z > 0.99f;

    private static void Summary(string what, JsonElement result, int faces, bool closed)
    {
        Equal($"{what}: vertices", 12, result.GetProperty("vertices").GetInt32());
        Equal($"{what}: faces", faces, result.GetProperty("faces").GetInt32());
        Equal($"{what}: edges", 16, result.GetProperty("edges").GetInt32());
        Equal($"{what}: closed", closed, result.GetProperty("closed").GetBoolean());
    }

    /// <summary>
    /// The ray test itself must give <paramref name="face"/> at every probe named <paramref name="name"/>, so no probe the
    /// checks rest on is left out as uncertain.
    /// </summary>
    private static void Expect(ScenarioFrame frame, Camera camera, Solid solid, string name, int face)
    {
        foreach (var (_, at) in Probes.Where(p => p.Name == name))
        {
            var (x, y) = camera.Pixel(at);
            Equal($"{frame.Name}: what the geometry shows at {name} {at} (pixel ({x}, {y}))", face, solid.Seen(camera, frame.Width, x, y));
        }
    }

    /// <summary>Without the inner face, the hole's centre and every point inside it by its corners and sides is the background, the plate's corners the front face.</summary>
    private static void Opening(ScenarioFrame frame, Camera camera, Solid solid)
    {
        Expect(frame, camera, solid, "the hole's centre", -1);
        Expect(frame, camera, solid, "inside the hole, by its corner", -1);
        Expect(frame, camera, solid, "inside the hole, by its side", -1);
        Expect(frame, camera, solid, "the plate, by its corner", 0);
    }

    /// <summary>The front's shade, read at the plate's first corner probe, not the background. Every +Z face the frame shows is this within <see cref="Exact"/>.</summary>
    private static Rgb PlateShade(ScenarioFrame frame, Camera camera)
    {
        var (x, y) = camera.Pixel(Probes.First(p => p.Name == "the plate, by its corner").At);
        NotBackground(frame, (x, y), "the plate's shade, by its corner");
        var p = frame.Pixel(x, y);
        return (p.R, p.G, p.B);
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

        if (tested < 20)
        {
            Note($"{frame.Name}: only {tested} probes were surely a face or the background");
        }
    }
}
