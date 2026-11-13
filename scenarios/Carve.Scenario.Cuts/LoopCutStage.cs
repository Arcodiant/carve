using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;
using static Carve.Scenario.Cuts.CutsChecks;

namespace Carve.Scenario.Cuts;

using Rgb = (int R, int G, int B);

/// <summary>
/// T-66, loop_cut: a 1 x 2 x 1 box (corner at the origin) with two loops at 0.45 and 0.55 of its vertical edge 0-3, round
/// its four sides, seen in the wireframe from <c>[0.6, 0.35, 1]</c> with the loop edges highlighted. Then the band's front
/// face extruded out by 0.1, shaded from the same direction. Every pixel asked about is worked from get_mesh's geometry by
/// a ray test (<see cref="Solid.FirstHit"/>), with the checks the other Cuts stages use.
/// </summary>
internal sealed class LoopCutStage : ICutsStage
{
    private static readonly double[] Front = [0.6, 0.35, 1];

    private static readonly double[][] Vertices =
        [[0, 0, 0], [1, 0, 0], [1, 2, 0], [0, 2, 0], [0, 0, 1], [1, 0, 1], [1, 2, 1], [0, 2, 1]];

    private static readonly int[][] Faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    private static readonly float[] Steps = [0.12f, 0.37f, 0.63f, 0.88f];

    /// <summary>A 4 x 4 grid on each of the box's six planes, and six points outside it.</summary>
    private static readonly (string Name, Vector3 At)[] BoxProbes =
    [
        .. Grid("the back (z = 0)", Steps, (u, v) => new Vector3(u, 2 * v, 0)),
        .. Grid("the front (z = 1)", Steps, (u, v) => new Vector3(u, 2 * v, 1)),
        .. Grid("the bottom (y = 0)", Steps, (u, v) => new Vector3(u, 0, v)),
        .. Grid("the top (y = 2)", Steps, (u, v) => new Vector3(u, 2, v)),
        .. Grid("the left (x = 0)", Steps, (u, v) => new Vector3(0, 2 * u, v)),
        .. Grid("the right (x = 1)", Steps, (u, v) => new Vector3(1, 2 * u, v)),
        .. new[] { -0.15f, 1.15f }.SelectMany(x => new[] { -0.15f, 1f, 2.15f }.Select(y => ("outside the box", new Vector3(x, y, 0.5f)))),
    ];

    /// <summary>The extruded band (y 0.9 to 1.1, out to z = 1.1): a grid on its front, and on its top and right walls.</summary>
    private static readonly (string Name, Vector3 At)[] BandProbes =
    [
        .. Steps.SelectMany(u => Steps.Select(v => ("the band's front", new Vector3(u, 0.9f + 0.2f * v, 1.1f)))),
        .. Steps.SelectMany(u => new[] { 0.3f, 0.5f, 0.7f }.Select(v => ("the band's top", new Vector3(u, 1.1f, 1 + 0.1f * v)))),
        .. Steps.SelectMany(u => new[] { 0.3f, 0.5f, 0.7f }.Select(v => ("the band's right", new Vector3(1, 0.9f + 0.2f * u, 1 + 0.1f * v)))),
    ];

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. Two loops round the sides, the new edges highlighted in the wireframe.
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        var cut = await CallJsonAsync(driver, "loop_cut", new { edge = new[] { 0, 3 }, at = new[] { 0.45, 0.55 } });
        Summary("loop_cut", cut, 16, 14, true);
        SameList("loop_cut: split faces", ["0", "1", "4", "5"], Ids(cut.GetProperty("split").EnumerateArray().Select(s => s.GetProperty("face"))));
        Equal("loop_cut: every split face in three pieces", true, cut.GetProperty("split").EnumerateArray().All(s => s.GetProperty("pieces").GetArrayLength() == 3));
        var made = cut.GetProperty("created").GetProperty("vertices").EnumerateArray().Select(v => v.GetInt32()).ToHashSet();
        Equal("loop_cut: new vertices", 8, made.Count);

        var whole = await Solid.ReadAsync(driver);
        SameList("loop_cut: the heights of the new vertices", ["0.9", "0.9", "0.9", "0.9", "1.1", "1.1", "1.1", "1.1"],
            made.Select(v => whole.Vertices[v].Y).Order().Select(y => y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)));
        var edges = AllEdges(whole);
        // A loop edge joins two new vertices at one height; the new vertices up a box edge are at different heights.
        var loops = edges.Where(e => made.Contains(e.Lo) && made.Contains(e.Hi) && MathF.Abs(whole.Vertices[e.Lo].Y - whole.Vertices[e.Hi].Y) < 1e-4f).ToArray();
        Equal("the loop edges", 8, loops.Length);

        var camera = new Camera(Vector3.Zero, new Vector3(1, 2, 1), ToVector(Front), 512);
        var wire = await driver.CaptureAsync("loop-cut-wireframe", new
        {
            direction = Front,
            mode = "wireframe",
            highlight = new { edges = loops.Select(e => new[] { e.Lo, e.Hi }).ToArray() },
        });
        Caption(wire, "512x512, direction (0.6, 0.35, 1), wireframe, highlight 8 edges, 16 vertices, 14 faces");
        var lines = edges.Select(e => (camera.Project(whole.Vertices[e.Lo]), camera.Project(whole.Vertices[e.Hi]))).ToArray();
        Look(wire, camera, whole, BoxProbes, _ => Fill, lines, "the box with two loops, in the wireframe");

        // The edges on the faces turned to the camera are in sight: the loop edges orange, the others not.
        var dir = Vector3.Normalize(ToVector(Front));
        var facing = Enumerable.Range(0, whole.Faces.Length).Where(f => !whole.IsDeleted(f) && Vector3.Dot(whole.Normal(f), dir) > 0.2f).ToArray();
        var seen = AllEdges(whole, facing);
        Equal("the loop edges in sight", 4, seen.Count(loops.Contains));
        foreach (var (lo, hi) in seen)
        {
            var (a, b) = (whole.Vertices[lo], whole.Vertices[hi]);
            var (from, to) = (camera.Project(Vector3.Lerp(a, b, 0.2f)), camera.Project(Vector3.Lerp(a, b, 0.8f)));
            if (loops.Contains((lo, hi)))
            {
                Line(wire, from, to, Orange, $"the loop edge {lo}-{hi}");
            }
            else
            {
                NotLine(wire, from, to, Orange, $"the other edge {lo}-{hi}");
            }
        }

        // 2. The band's front face (the piece of face 1 between the loops) extruded out by 0.1, shaded.
        var band = Enumerable.Range(0, whole.Faces.Length).Single(f => !whole.IsDeleted(f) && whole.Normal(f).Z > 0.9f
            && whole.Faces[f].Loop.All(v => whole.Vertices[v].Z == 1 && whole.Vertices[v].Y is > 0.85f and < 1.15f));
        var extruded = await CallJsonAsync(driver, "extrude", new { faces = new[] { band }, distance = 0.1 });
        Summary("extrude the band", extruded, 20, 18, true);
        var solid = await Solid.ReadAsync(driver);
        SameList("the band's front face stands out to", ["1.1", "1.1", "1.1", "1.1"],
            solid.Faces[band].Loop.Select(v => solid.Vertices[v].Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)));
        var camera2 = new Camera(Vector3.Zero, new Vector3(1, 2, 1.1f), ToVector(Front), 512);
        var shaded = await driver.CaptureAsync("loop-cut-band-extruded", new { direction = Front });
        Caption(shaded, "512x512, direction (0.6, 0.35, 1), 20 vertices, 18 faces");
        var probes = BoxProbes.Concat(BandProbes).ToArray();
        Expect(shaded, camera2, solid, probes, "the band's front", band, 8);
        Look(shaded, camera2, solid, probes, Shades(shaded, camera2, solid, probes), [], "the box with its band extruded");
        Predicted(shaded, camera2, solid, band);
    }

    /// <summary>
    /// The person's rulings on shade prediction (2026-11-12, T-67's <c>knife-slab-recess</c>): a face's pixel is predicted by
    /// the Crate scenario's rule (<see cref="Crate.ColourRule"/>) from a grey face of the same normal in the same frame, at
    /// <see cref="Checks.Exact"/>; for a grey face colour / grey is 1, so it is that face's grey. The band's front is
    /// predicted from the box's front, its top wall (+Y) from the box's top (y = 2) and its right wall (+X) from the box's
    /// right (x = 1). The walls are a few pixels across, so they are asked by <see cref="Solid.SeenNarrow"/>, and at least
    /// one probe on each must be sure. A band drawn flush, or with no walls, shows the box's front where the walls should be.
    /// </summary>
    private static void Predicted(ScenarioFrame frame, Camera camera, Solid solid, int band)
    {
        int Wall(Func<Vector3, bool> facing, string what)
        {
            var walls = Enumerable.Range(0, solid.Faces.Length).Where(f => !solid.IsDeleted(f) && f != band
                && solid.Faces[f].Loop.All(v => solid.Vertices[v] is var p && p.Z is >= 1 - 1e-5f and <= 1.1f + 1e-5f && p.Y is >= 0.9f - 1e-5f and <= 1.1f + 1e-5f)
                && facing(solid.Normal(f))).ToArray();
            Equal($"{what}: one face", 1, walls.Length);
            return walls.Length == 1 ? walls[0] : -1;
        }

        var top = Wall(n => n.Y > 0.99f, "the band's top wall");
        var right = Wall(n => n.X > 0.99f, "the band's right wall");
        foreach (var (name, face, normal, from, narrow) in new[]
        {
            ("the band's front", band, Vector3.UnitZ, "the front (z = 1)", false),
            ("the band's top", top, Vector3.UnitY, "the top (y = 2)", true),
            ("the band's right", right, Vector3.UnitX, "the right (x = 1)", true),
        })
        {
            if (Reference(frame, camera, solid, from, normal) is not { } grey)
            {
                continue;
            }

            var sure = 0;
            foreach (var (_, at) in BandProbes.Where(p => p.Name == name))
            {
                var (x, y) = camera.Pixel(at);
                var seen = narrow ? solid.SeenNarrow(camera, frame.Width, x, y) : solid.Seen(camera, frame.Width, x, y);
                if (seen == -2)
                {
                    continue;
                }

                sure++;
                Equal($"{frame.Name}: what the geometry shows at {name} {at} (pixel ({x}, {y}))", face, seen);
                Colour(frame, (x, y), grey, Exact, $"{frame.Name}: {name} {at}, predicted {grey} from {from}");
            }

            if (sure < 1)
            {
                Note($"{frame.Name}: no probe on {name} was sure");
            }
        }
    }

    /// <summary>The grey pixel at the first probe named <paramref name="name"/> that surely shows a face of <paramref name="normal"/>, not the band's.</summary>
    private static Rgb? Reference(ScenarioFrame frame, Camera camera, Solid solid, string name, Vector3 normal)
    {
        foreach (var (_, at) in BoxProbes.Where(p => p.Name == name))
        {
            var (x, y) = camera.Pixel(at);
            var seen = solid.Seen(camera, frame.Width, x, y);
            if (seen >= 0 && Vector3.Dot(solid.Normal(seen), normal) > 0.99f)
            {
                var p = frame.Pixel(x, y);
                return (p.R, p.G, p.B);
            }
        }

        Note($"{frame.Name}: no probe on {name} surely shows a face facing {normal}, the reference for the band");
        return null;
    }

    private static (int Lo, int Hi)[] AllEdges(Solid solid, IEnumerable<int>? only = null) =>
        (only ?? Enumerable.Range(0, solid.Faces.Length).Where(f => !solid.IsDeleted(f)))
            .SelectMany(f => solid.Faces[f].Loop.Select((v, k) => (A: v, B: solid.Faces[f].Loop[(k + 1) % solid.Faces[f].Loop.Length])))
            .Select(e => (Lo: Math.Min(e.A, e.B), Hi: Math.Max(e.A, e.B))).Distinct().OrderBy(e => e).ToArray();

    /// <summary>Each face's shade, read at the first probe that surely shows it.</summary>
    private static Func<int, Rgb?> Shades(ScenarioFrame frame, Camera camera, Solid solid, (string Name, Vector3 At)[] all)
    {
        var shades = new Dictionary<int, Rgb>();
        foreach (var (_, at) in all)
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
