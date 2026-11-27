using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;
using static Carve.Scenario.Cuts.CutsChecks;

namespace Carve.Scenario.Cuts;

/// <summary>
/// T-237, tessellate: a 2 x 2 x 0.2 slab whose front face (z = 0.2) is holed by a 0.6 x 0.6 socket 0.1 deep (four walls
/// and a floor), the front in group <c>wear</c>, tessellated at <c>max_edge</c> 0.25 and seen in the wireframe from
/// <c>[0.6, 0.35, 1]</c>. From get_mesh: the front's pieces are triangles on z = 0.2 no longer than 0.25 with no piece in
/// the socket, no other face is split and no vertex has moved, and the faces round the front took its border points.
/// Every pixel asked about is worked from that geometry by a ray test that knows holes (<see cref="Solid"/>): each piece's
/// centre and a grid on the faces in sight filled, the socket's floor through the hole, the background round the slab,
/// and every edge of every piece drawn.
/// </summary>
internal sealed class TessellateStage : ICutsStage
{
    private static readonly double[] Front = [0.6, 0.35, 1];

    private const float MaxEdge = 0.25f;

    /// <summary>
    /// The edge colour over the fill at 3 of 4 samples, in linear: at some slopes (upright, 45 degrees, 2:1, flat) 4x MSAA
    /// never gives a 1-pixel line all four, so its brightest pixel is this blend; the person chose to accept it here (T-237).
    /// </summary>
    private static readonly (int R, int G, int B) ThreeQuarters = Srgb(0.75 * 0.85 + 0.25 * 0.09, 0.75 * 0.85 + 0.25 * 0.095, 0.75 * 0.85 + 0.25 * 0.10);

    /// <summary>The box, then the socket's rim on the front (clockwise seen from the front, as a hole) and its floor ring.</summary>
    private static readonly double[][] Vertices =
    [
        [0, 0, 0], [2, 0, 0], [2, 2, 0], [0, 2, 0], [0, 0, 0.2], [2, 0, 0.2], [2, 2, 0.2], [0, 2, 0.2],
        [0.7, 0.7, 0.2], [0.7, 1.3, 0.2], [1.3, 1.3, 0.2], [1.3, 0.7, 0.2],
        [0.7, 0.7, 0.1], [0.7, 1.3, 0.1], [1.3, 1.3, 0.1], [1.3, 0.7, 0.1],
    ];

    private static readonly object[] Faces =
    [
        new[] { 0, 3, 2, 1 }, new { loop = new[] { 4, 5, 6, 7 }, holes = new[] { new[] { 8, 9, 10, 11 } } },
        new[] { 0, 1, 5, 4 }, new[] { 3, 7, 6, 2 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 },
        new[] { 12, 15, 14, 13 },
        new[] { 9, 8, 12, 13 }, new[] { 10, 9, 13, 14 }, new[] { 11, 10, 14, 15 }, new[] { 8, 11, 15, 12 },
    ];

    private static readonly Vector3 Low = Vector3.Zero;
    private static readonly Vector3 High = new(2, 2, 0.2f);

    private static readonly float[] Steps = [0.13f, 0.38f, 0.63f, 0.88f, 1.12f, 1.37f, 1.62f, 1.87f];

    private static readonly float[] Other = [0.25f, 0.5f, 0.75f, 1.25f, 1.5f, 1.75f];

    private static readonly (string Name, Vector3 At)[] Probes =
    [
        .. Grid("the front (z = 0.2)", Steps, (u, v) => new Vector3(u, v, 0.2f)),
        .. Grid("the top (y = 2)", Steps, (u, v) => new Vector3(u, 2, v / 10)),
        .. Grid("the right (x = 2)", Steps, (u, v) => new Vector3(2, u, v / 10)),
        .. new[] { 0.85f, 1f, 1.15f }.SelectMany(x => new[] { 0.85f, 1f, 1.15f }.Select(y => ("the socket's floor", new Vector3(x, y, 0.1f)))),
        .. new[] { -0.3f, 2.3f }.SelectMany(x => new[] { -0.3f, 1f, 2.3f }.Select(y => ("outside the slab", new Vector3(x, y, 0.1f)))),
    ];

    private static void True(string what, bool holds)
    {
        if (!holds) Note(what + ": does not hold");
    }

    public async Task RunAsync(ScenarioDriver driver)
    {
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        await CallJsonAsync(driver, "define_group", new { name = "wear", faces = new[] { 1 } });
        var before = await Solid.ReadAsync(driver);

        var result = await CallJsonAsync(driver, "tessellate", new { max_edge = MaxEdge, group = "wear" });
        Equal("tessellate: closed", true, result.GetProperty("closed").GetBoolean());
        Equal("tessellate: warnings", 0, result.GetProperty("warnings").GetArrayLength());
        var split = result.GetProperty("split").EnumerateArray().ToArray();
        SameList("tessellate: the faces split", ["1"], Ids(split.Select(s => s.GetProperty("face"))));
        var pieces = split[0].GetProperty("pieces").EnumerateArray().Select(p => p.GetInt32()).ToArray();

        var cut = await Solid.ReadAsync(driver);
        Equal("tessellate: the faces", before.Faces.Length + pieces.Length - 1, cut.Faces.Length);
        for (var v = 0; v < before.Vertices.Length; v++) Equal($"tessellate: vertex {v} unmoved", before.Vertices[v], cut.Vertices[v]);
        foreach (var f in pieces)
        {
            var loop = cut.Faces[f].Loop;
            Equal($"tessellate: faces[{f}] corners", 3, loop.Length);
            Equal($"tessellate: faces[{f}] holes", 0, cut.Faces[f].Holes.Length);
            var p = loop.Select(v => cut.Vertices[v]).ToArray();
            foreach (var q in p) True($"tessellate: faces[{f}] on the front's plane", Math.Abs(q.Z - 0.2f) < 1e-5f);
            for (var k = 0; k < 3; k++)
                True($"tessellate: faces[{f}] edge {k} no longer than {MaxEdge}", Vector3.Distance(p[k], p[(k + 1) % 3]) <= MaxEdge * 1.0001f);
            var c = (p[0] + p[1] + p[2]) / 3;
            True($"tessellate: faces[{f}] outside the socket", !(c.X > 0.7f && c.X < 1.3f && c.Y > 0.7f && c.Y < 1.3f));
        }
        // Every face but the front keeps its corners in order, gaining only points on its edges.
        for (var f = 0; f < before.Faces.Length; f++)
        {
            if (f == 1) continue;
            SameList($"tessellate: faces[{f}] corners kept", before.Faces[f].Loop.Select(v => $"{v}"),
                cut.Faces[f].Loop.Where(v => v < before.Vertices.Length).Select(v => $"{v}"));
        }
        // The four sides and the four socket walls each took 2 / 0.25 - 1 = 7 or 0.6 / 0.25 -> 2 points on the front's edge.
        foreach (var (f, gained) in new[] { (2, 7), (3, 7), (4, 7), (5, 7), (7, 2), (8, 2), (9, 2), (10, 2) })
            Equal($"tessellate: faces[{f}] points gained", gained, cut.Faces[f].Loop.Length - before.Faces[f].Loop.Length);

        var camera = new Camera(Low, High, ToVector(Front), 512);
        var wire = await driver.CaptureAsync("tessellate-slab-wireframe", new { direction = Front, mode = "wireframe" });
        Caption(wire, $"512x512, direction (0.6, 0.35, 1), wireframe, {cut.Vertices.Length} vertices, {cut.Faces.Length} faces");
        var edges = cut.Edges();
        var lines = edges.Select(e => (camera.Project(cut.Vertices[e.Lo]), camera.Project(cut.Vertices[e.Hi]))).ToArray();
        var centres = pieces.Select(f => ($"the centre of piece {f}", cut.Faces[f].Loop.Aggregate(Vector3.Zero, (s, v) => s + cut.Vertices[v]) / 3));
        Look(wire, camera, cut, [.. Probes, .. centres], _ => Fill, lines, "the tessellated slab, in the wireframe");

        // Every edge of the pieces lies on the front, in front of everything else: each is drawn along its middle.
        foreach (var (lo, hi) in cut.Edges(pieces))
        {
            var (a, b) = (cut.Vertices[lo], cut.Vertices[hi]);
            var (from, to) = (camera.Project(Vector3.Lerp(a, b, 0.3f)), camera.Project(Vector3.Lerp(a, b, 0.7f)));
            var (full, part) = (Closest(wire, from, to, Edge), Closest(wire, from, to, ThreeQuarters));
            if (full.Off > 10 && part.Off > 10)
                Note($"{wire.Name}: the piece edge {lo}-{hi}, {from} to {to}: the closest pixel to {Edge} or {ThreeQuarters} is {full.P} at ({full.X}, {full.Y})");
        }

        // T-238: the front's group displaced with pin_border, along z so the bumps show from the front: the faces outside it
        // are held where they were, and the front's own border points with them.
        var worn = await CallJsonAsync(driver, "displace", new { source = new { noise = new { seed = 3, scale = 0.4, octaves = 3 } }, amount = 0.12, axis = new[] { 0, 0, 1 }, group = "wear", pin_border = true });
        Equal("displace: closed", true, worn.GetProperty("closed").GetBoolean());
        var after = await Solid.ReadAsync(driver);
        var inside = pieces.ToHashSet();
        var held = Enumerable.Range(0, cut.Faces.Length).Where(f => !inside.Contains(f)).SelectMany(f => cut.Faces[f].Loop).ToHashSet();
        var free = Enumerable.Range(0, cut.Vertices.Length).Where(v => !held.Contains(v)).ToArray();
        foreach (var v in held) Equal($"displace: border vertex {v} unmoved", cut.Vertices[v], after.Vertices[v]);
        Equal("displace: vertices moved", free.Count(v => after.Vertices[v] != cut.Vertices[v]), worn.GetProperty("moved").GetProperty("count").GetInt32());
        True("displace: some inside vertex moved", free.Any(v => after.Vertices[v] != cut.Vertices[v]));
        True("displace: largest within amount / 2", worn.GetProperty("largest").GetDouble() is > 0 and <= 0.0601);
        for (var f = 0; f < cut.Faces.Length; f++)
            if (!inside.Contains(f)) SameList($"displace: faces[{f}] corners kept", cut.Faces[f].Loop.Select(v => $"{v}"), after.Faces[f].Loop.Select(v => $"{v}"));

        var wornWire = await driver.CaptureAsync("tessellate-slab-worn", new { direction = Front, mode = "wireframe" });
        Caption(wornWire, $"512x512, direction (0.6, 0.35, 1), wireframe, {after.Vertices.Length} vertices, {after.Faces.Length} faces");
        Size(wornWire, 512, 512);
        Corners(wornWire);
        // The faces round the front are unmoved, so their probes still hold; the front's own are not asked about.
        Look(wornWire, camera, after, [.. Probes.Where(p => !p.Name.StartsWith("the front") && !p.Name.StartsWith("the socket")),
            .. Grid("the top (y = 2)", Other, (u, v) => new Vector3(u, 2, v / 10)), .. Grid("the right (x = 2)", Other, (u, v) => new Vector3(2, u, v / 10))], _ => Fill, [], "the worn slab, the faces round the front unmoved");
    }
}
