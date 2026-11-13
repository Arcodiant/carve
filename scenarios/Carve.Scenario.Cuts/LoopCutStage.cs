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
        .. Steps.SelectMany(u => new[] { 0.3f, 0.7f }.Select(v => ("the band's top", new Vector3(u, 1.1f, 1 + 0.1f * v)))),
        .. Steps.SelectMany(u => new[] { 0.3f, 0.7f }.Select(v => ("the band's right", new Vector3(1, 0.9f + 0.2f * u, 1 + 0.1f * v)))),
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
