using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;
using static Carve.Scenario.Cuts.CutsChecks;

namespace Carve.Scenario.Cuts;

using Rgb = (int R, int G, int B);

/// <summary>
/// T-68, bridge and merge_faces. A slab 2 x 2 x 0.2 defined with a square hole in its front face and the matching hole
/// in its back face, the hole's inner faces left out, then bridged into a tunnel: closed, seen shaded from
/// <c>[0.6, 0.35, 1]</c> (the two tunnel walls facing the camera drawn, the background through the rest) and
/// orthographic from <c>[0, 0, 1]</c> (the walls edge-on, the background through the whole hole). Then a plate of four
/// quads merged into one, in the wireframe from <c>[0.6, 0.35, 1]</c>: the outline drawn, the dissolved inner edges not.
/// Every pixel asked about is worked from get_mesh's geometry by a ray test (<see cref="Solid.FirstHit"/>).
/// </summary>
internal sealed class MergeBridgeStage : ICutsStage
{
    private const float Front = 0.1f;
    private static readonly double[] Direction = [0.6, 0.35, 1];

    private static readonly double[][] SlabVertices =
    [
        [-1, -1, 0.1], [1, -1, 0.1], [1, 1, 0.1], [-1, 1, 0.1], // 0-3 the front's outline
        [-0.5, -0.5, 0.1], [0.5, -0.5, 0.1], [0.5, 0.5, 0.1], [-0.5, 0.5, 0.1], // 4-7 the front's hole
        [-1, -1, -0.1], [1, -1, -0.1], [1, 1, -0.1], [-1, 1, -0.1], // 8-11 the back's outline
        [-0.5, -0.5, -0.1], [0.5, -0.5, -0.1], [0.5, 0.5, -0.1], [-0.5, 0.5, -0.1], // 12-15 the back's hole
    ];

    private static readonly object[] SlabFaces =
    [
        new { loop = new[] { 0, 1, 2, 3 }, holes = new[] { new[] { 4, 7, 6, 5 } } }, // the front, its hole clockwise seen from +Z
        new { loop = new[] { 8, 11, 10, 9 }, holes = new[] { new[] { 12, 13, 14, 15 } } }, // the back, its hole clockwise seen from -Z
        new[] { 1, 0, 8, 9 }, new[] { 2, 1, 9, 10 }, new[] { 3, 2, 10, 11 }, new[] { 0, 3, 11, 8 },
    ];

    /// <summary>The slab's points asked about, on the front's plane unless named otherwise.</summary>
    private static readonly (string Name, Vector3 At)[] SlabProbes =
    [
        ("the hole's centre", new Vector3(0, 0, Front)),
        .. new[] { -0.3f, 0, 0.3f }.Select(t => ("inside the hole, by its -x wall", new Vector3(-0.46f, t, 0.05f))),
        .. new[] { -0.3f, 0, 0.3f }.Select(t => ("inside the hole, by its -y wall", new Vector3(t, -0.48f, 0.05f))),
        .. new[] { -0.3f, 0, 0.3f }.Select(t => ("inside the hole, by its +x side", new Vector3(0.45f, t, Front))),
        .. new[] { -0.3f, 0, 0.3f }.Select(t => ("inside the hole, by its +y side", new Vector3(t, 0.45f, Front))),
        .. Signs().Select(s => ("the slab, by its corner", new Vector3(0.9f * s.X, 0.9f * s.Y, Front))),
        .. Signs().Select(s => ("the slab, by the hole's corner", new Vector3(0.6f * s.X, 0.6f * s.Y, Front))),
        .. Sides().Select(s => ("the slab, at its side's middle", new Vector3(0.75f * s.X, 0.75f * s.Y, Front))),
        .. Signs().Select(s => ("outside the slab, by its corner", new Vector3(1.1f * s.X, 1.1f * s.Y, Front))),
    ];

    private static readonly double[][] PlateVertices =
    [
        [-1, -1, 0], [0, -1, 0], [1, -1, 0], [-1, 0, 0], [0, 0, 0], [1, 0, 0], [-1, 1, 0], [0, 1, 0], [1, 1, 0],
    ];

    private static readonly int[][] PlateFaces = [[0, 1, 4, 3], [1, 2, 5, 4], [3, 4, 7, 6], [4, 5, 8, 7]];

    private static readonly (string Name, Vector3 At)[] PlateProbes =
    [
        .. Signs().Select(s => ("the plate, by its corner", new Vector3(0.75f * s.X, 0.75f * s.Y, 0))),
        .. Signs().Select(s => ("the plate, near its centre", new Vector3(0.3f * s.X, 0.3f * s.Y, 0))),
        .. Signs().Select(s => ("outside the plate, by its corner", new Vector3(1.15f * s.X, 1.15f * s.Y, 0))),
        .. Sides().Select(s => ("outside the plate, by its side", new Vector3(1.15f * s.X, 1.15f * s.Y, 0))),
    ];

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The slab with a hole through front and back, open, then bridged into a closed tunnel.
        var open = await CallJsonAsync(driver, "define_mesh", new { vertices = SlabVertices, faces = SlabFaces });
        Summary("define_mesh, the holed slab", open, 16, 6, false);
        var bridged = await CallJsonAsync(driver, "bridge", new { loops = new[] { new[] { 4, 5, 6, 7 }, new[] { 12, 13, 14, 15 } } });
        Summary("bridge", bridged, 16, 10, true);
        SameList("bridge: created.faces", ["6", "7", "8", "9"], bridged.GetProperty("created").GetProperty("faces").EnumerateArray().Select(f => $"{f.GetInt32()}"));
        var check = await CallJsonAsync(driver, "check", new { });
        Equal("check after bridge: problems", 0, check.GetProperty("problems").GetArrayLength());
        var slab = await Solid.ReadAsync(driver);
        SameList("get_mesh: the first tunnel wall", ["4", "5", "13", "12"], slab.Faces[6].Loop.Select(v => $"{v}"));

        var (min, max) = (new Vector3(-1, -1, -Front), new Vector3(1, 1, Front));
        var camera = new Camera(min, max, ToVector(Direction), 512);
        var shaded = await driver.CaptureAsync("bridge-slab", new { direction = Direction });
        Caption(shaded, "512x512, direction (0.6, 0.35, 1), 16 vertices, 10 faces");
        Expect(shaded, camera, slab, SlabProbes, "the hole's centre", -1);
        Expect(shaded, camera, slab, SlabProbes, "inside the hole, by its -x wall", 9);
        Expect(shaded, camera, slab, SlabProbes, "inside the hole, by its -y wall", 6);
        Expect(shaded, camera, slab, SlabProbes, "the slab, by its corner", 0);
        var shades = new Dictionary<int, Rgb>
        {
            [0] = Shade(shaded, camera, slab, SlabProbes, "the slab, by its corner", 0),
            [6] = Shade(shaded, camera, slab, SlabProbes, "inside the hole, by its -y wall", 6),
            [9] = Shade(shaded, camera, slab, SlabProbes, "inside the hole, by its -x wall", 9),
        };
        Look(shaded, camera, slab, SlabProbes, f => shades.TryGetValue(f, out var c) ? c : null, [], "the bridged slab", 20);

        var straight = new Camera(min, max, Vector3.UnitZ, 512, orthographic: true);
        var ortho = await driver.CaptureAsync("bridge-slab-ortho", new { direction = new[] { 0, 0, 1 }, projection = "orthographic" });
        Caption(ortho, "512x512, direction (0, 0, 1), orthographic, 16 vertices, 10 faces");
        foreach (var name in new[] { "the hole's centre", "inside the hole, by its -x wall", "inside the hole, by its -y wall", "inside the hole, by its +x side", "inside the hole, by its +y side" })
            Expect(ortho, straight, slab, SlabProbes, name, -1);
        Expect(ortho, straight, slab, SlabProbes, "the slab, by its corner", 0);
        var front = Shade(ortho, straight, slab, SlabProbes, "the slab, by its corner", 0);
        Look(ortho, straight, slab, SlabProbes, f => f == 0 ? front : null, [], "the bridged slab, orthographic", 20);

        // 2. A plate of four quads merged into one: the centre vertex goes, the outline keeps its collinear vertices.
        await CallJsonAsync(driver, "define_mesh", new { vertices = PlateVertices, faces = PlateFaces });
        var merged = await CallJsonAsync(driver, "merge_faces", new { faces = new[] { 0, 1, 2, 3 } });
        Summary("merge_faces", merged, 8, 1, false);
        SameList("merge_faces: deleted.vertices", ["4"], merged.GetProperty("deleted").GetProperty("vertices").EnumerateArray().Select(v => $"{v.GetInt32()}"));
        SameList("merge_faces: deleted.faces", ["1", "2", "3"], merged.GetProperty("deleted").GetProperty("faces").EnumerateArray().Select(v => $"{v.GetInt32()}"));
        var plate = await Solid.ReadAsync(driver);
        SameList("get_mesh: the merged face", ["0", "1", "2", "5", "8", "7", "6", "3"], plate.Faces[0].Loop.Select(v => $"{v}"));
        SameList("get_mesh: the deleted faces", ["1", "2", "3"], Enumerable.Range(0, plate.Faces.Length).Where(plate.IsDeleted).Select(f => $"{f}"));

        var flat = new Camera(new Vector3(-1, -1, 0), new Vector3(1, 1, 0), ToVector(Direction), 512);
        var wire = await driver.CaptureAsync("merge-plate-wireframe", new { direction = Direction, mode = "wireframe" });
        Caption(wire, "512x512, direction (0.6, 0.35, 1), wireframe, 8 vertices, 1 faces");
        Expect(wire, flat, plate, PlateProbes, "the plate, by its corner", 0);
        Expect(wire, flat, plate, PlateProbes, "the plate, near its centre", 0);
        var outline = plate.Faces[0].Loop;
        var lines = outline.Select((v, k) => (flat.Project(plate.Vertices[v]), flat.Project(plate.Vertices[outline[(k + 1) % outline.Length]]))).ToArray();
        Look(wire, flat, plate, PlateProbes, _ => Fill, lines, "the merged plate's wireframe", 16);
        for (var k = 0; k < outline.Length; k++)
        {
            var (lo, hi) = (outline[k], outline[(k + 1) % outline.Length]);
            var (a, b) = (plate.Vertices[lo], plate.Vertices[hi]);
            for (var part = 0; part < 4; part++)
            {
                var (from, to) = (0.1f + 0.2f * part, 0.3f + 0.2f * part);
                Line(wire, flat.Project(Vector3.Lerp(a, b, from)), flat.Project(Vector3.Lerp(a, b, to)), Edge,
                    $"the outline's edge {lo}-{hi}, {from:0.0} to {to:0.0} along it");
            }
        }

        // The dissolved edges are not drawn: along each of 1-4, 3-4, 5-4 and 7-4 the plate is its fill.
        var centre = Vector3.Zero;
        foreach (var other in new[] { 1, 3, 5, 7 })
            foreach (var t in new[] { 0.3f, 0.5f, 0.7f })
            {
                var (x, y) = flat.Pixel(Vector3.Lerp(centre, ToVector(PlateVertices[other]), t));
                Colour(wire, (x, y), Fill, Exact, $"the dissolved edge 4-{other}, {t:0.0} along it");
            }
    }

    /// <summary>The ray test itself must give <paramref name="face"/> at every probe named <paramref name="name"/>, so no probe the checks rest on is left out as uncertain.</summary>
    private static void Expect(ScenarioFrame frame, Camera camera, Solid solid, (string Name, Vector3 At)[] probes, string name, int face)
    {
        foreach (var (_, at) in probes.Where(p => p.Name == name))
        {
            var (x, y) = camera.Pixel(at);
            Equal($"{frame.Name}: what the geometry shows at {name} {at} (pixel ({x}, {y}))", face, solid.Seen(camera, frame.Width, x, y));
        }
    }

    /// <summary>A face's shade, read at the first probe named <paramref name="name"/> that surely shows it, not the background.</summary>
    private static Rgb Shade(ScenarioFrame frame, Camera camera, Solid solid, (string Name, Vector3 At)[] probes, string name, int face)
    {
        foreach (var (_, at) in probes.Where(p => p.Name == name))
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
    private static void Look(ScenarioFrame frame, Camera camera, Solid solid, (string Name, Vector3 At)[] probes, Func<int, Rgb?> colourOf,
        (Vector2 From, Vector2 To)[] lines, string what, int least)
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

        if (tested < least)
        {
            Note($"{frame.Name}: only {tested} probes were surely a face or the background, fewer than {least}");
        }
    }
}
