using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Cuts;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;
using static Carve.Scenario.Cuts.CutsChecks;

namespace Carve.Scenario.Gatehouse;

using Rgb = (int R, int G, int B);

/// <summary>
/// The frames of each stage and their checks. Every pixel asked about is worked from get_mesh's geometry by the Cuts
/// scenario's ray test (<see cref="Solid"/>, with T-67's <c>SeenNarrow</c> for a face only a few pixels wide), at the probes of
/// <see cref="GatehousePlan"/>: the middle of every face and a point near each of its corners. The door is predicted by the
/// Crate scenario's colour rule (<see cref="ColourRule"/>) from the front face of the same normal, at <see cref="Checks.Exact"/>,
/// and every grey face from the first grey face of the same normal in the frame (the same rule, colour / grey = 1): so the
/// cornice's front, end walls and top are held to the slab's front, ends and top, and a cornice drawn flush fails.
/// The pixel checks are the existing ones; there is none of this task's own design.
/// </summary>
internal sealed class GatehouseFrames(ScenarioDriver driver)
{
    private const int Tile = 256;
    private const int Gutter = 2;

    /// <summary>For the wireframe stages: the stage's new edges, and how many of them are on faces toward the camera. Worked in Core.</summary>
    private static readonly Dictionary<int, (int Added, int InSight)> Wires = new() { [2] = (16, 10), [4] = (7, 5), [6] = (10, 5) };

    private static readonly string[] Views = ["+x", "-x", "+y", "-y", "+z", "-z"];

    private static readonly double[] Front = [0.6, 0.35, 1];
    private static readonly double[] Back = [-0.6, 0.35, -1];

    public async Task StageAsync(int stage, string name, Solid? previous, bool wire = false, int doorFace = -1, bool last = false)
    {
        var solid = await Solid.ReadAsync(driver);
        var (min, max) = GatehousePlan.Bounds(solid);
        var counts = Counts(solid);
        ScenarioFrame? plainFront = null;
        foreach (var (view, direction, text) in new[] { ("front", Front, "0.6, 0.35, 1"), ("back", Back, "-0.6, 0.35, -1") })
        {
            var frame = await driver.CaptureAsync($"gate-{stage}-{name}-{view}", new { direction });
            Caption(frame, $"512x512, direction ({text}), {counts}");
            var camera = new Camera(min, max, ToVector(direction), GatehousePlan.Size);
            Look(frame, solid, camera, GatehousePlan.Probes(solid), stage, view, doorFace, []);
            if (view == "front")
            {
                plainFront = frame;
            }
        }

        if (wire && previous is not null)
        {
            await WireAsync(stage, name, solid, previous, min, max, counts);
        }

        if (last)
        {
            await StraightAsync(stage, name, solid, min, max, counts, doorFace);
            await LabelsAsync(solid, min, max, counts, plainFront!);
            await SheetAsync(solid, min, max, counts, doorFace);
        }
    }

    /// <summary>The new edges highlighted: those on faces toward the camera are orange, the others there are not.</summary>
    private async Task WireAsync(int stage, string name, Solid solid, Solid previous, Vector3 min, Vector3 max, string counts)
    {
        var fresh = GatehousePlan.NewEdges(previous, solid);
        var camera = new Camera(min, max, ToVector(Front), GatehousePlan.Size);
        var frame = await driver.CaptureAsync($"gate-{stage}-{name}-wire", new
        {
            direction = Front,
            mode = "wireframe",
            highlight = new { edges = fresh.Select(e => new[] { e.Lo, e.Hi }).ToArray() },
        });
        Caption(frame, $"512x512, direction (0.6, 0.35, 1), wireframe, highlight {fresh.Length} edges, {counts}");
        var (added, inSight) = Wires[stage];
        Equal($"{frame.Name}: the stage's new edges", added, fresh.Length);
        var seen = GatehousePlan.InSight(solid, camera, ToVector(Front));
        Equal($"{frame.Name}: the new edges in sight", inSight, seen.Count(fresh.Contains));
        var lines = solid.Edges().Select(e => (camera.Project(solid.Vertices[e.Lo]), camera.Project(solid.Vertices[e.Hi]))).ToArray();
        Look(frame, solid, camera, GatehousePlan.Probes(solid), stage, "wire", -1, lines);
        foreach (var (lo, hi) in seen)
        {
            var (a, b) = (solid.Vertices[lo], solid.Vertices[hi]);
            var (from, to) = (camera.Project(Vector3.Lerp(a, b, 0.2f)), camera.Project(Vector3.Lerp(a, b, 0.8f)));
            if (fresh.Contains((lo, hi)))
            {
                Line(frame, from, to, Orange, $"the new edge {lo}-{hi}");
            }
            else
            {
                NotLine(frame, from, to, Orange, $"the old edge {lo}-{hi}");
            }
        }
    }

    /// <summary>Straight on and orthographic: the background seen through the window.</summary>
    private async Task StraightAsync(int stage, string name, Solid solid, Vector3 min, Vector3 max, string counts, int doorFace)
    {
        var frame = await driver.CaptureAsync($"gate-{stage}-{name}-window-ortho", new { direction = new[] { 0, 0, 1 }, projection = "orthographic" });
        Caption(frame, $"512x512, direction (0, 0, 1), orthographic, {counts}");
        var camera = new Camera(min, max, Vector3.UnitZ, GatehousePlan.Size, orthographic: true);
        Look(frame, solid, camera, GatehousePlan.Probes(solid).Concat(GatehousePlan.WindowBackground()), stage, "straight", doorFace, []);
    }

    /// <summary>The window's reveal faces labelled: each one the camera surely sees has its id in white at its middle, on the backing.</summary>
    private async Task LabelsAsync(Solid solid, Vector3 min, Vector3 max, string counts, ScenarioFrame plain)
    {
        var frame = await driver.CaptureAsync("gate-7-reveal-labels", new { direction = Front, labels = "faces" });
        Caption(frame, $"512x512, direction (0.6, 0.35, 1), labels faces, {counts}");
        Size(frame, 512, 512);
        Corners(frame);
        var camera = new Camera(min, max, ToVector(Front), GatehousePlan.Size);
        Equal("the reveal faces", 5, GatehousePlan.Reveal(solid).Length);
        var labelled = 0;
        foreach (var face in GatehousePlan.RevealSeen(solid, camera))
        {
            var middle = GatehousePlan.Probes(solid).First(p => p.Face == face && p.Name == "middle");
            labelled++;
            var at = camera.Project(middle.At);
            RequireWhite(frame, IdBox(at, face, centred: true), $"reveal face {face}'s label");
            var (x, y) = camera.Pixel(middle.At);
            var (shade, seen) = (plain.Pixel(x, y), frame.Pixel(x, y));
            if (IsWhite(seen))
            {
                Note($"{frame.Name}: reveal face {face}'s middle at ({x}, {y}) is a letter's pixel, so it says nothing of the backing");
            }
            else if (Math.Abs(seen.G - Backed(shade.G)) > 10)
            {
                Note($"{frame.Name}: reveal face {face}'s middle at ({x}, {y}) is {seen} over {shade}: the backing should give {Backed(shade.G)}");
            }
        }

        Equal($"{frame.Name}: reveal faces the camera surely sees", GatehousePlan.RevealsSeenFromFront, labelled);
    }

    private static void RequireWhite(ScenarioFrame frame, (int X, int Y, int Width, int Height) box, string what)
    {
        if (!WhiteIn(frame, box))
        {
            Note($"{frame.Name}: no white pixel in {what}'s box {box}");
        }
    }

    /// <summary>The six-view sheet: each tile is its single view; the horizontal views are asked by the ray test too.</summary>
    private async Task SheetAsync(Solid solid, Vector3 min, Vector3 max, string counts, int doorFace)
    {
        var sheet = await driver.CaptureAsync("gate-7-sheet", new { views = Views, projection = "orthographic", size = Tile, labels = "none" });
        Caption(sheet, $"3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, {counts}");
        Size(sheet, 3 * Tile + 2 * Gutter, 2 * Tile + Gutter);
        var background = sheet.Pixel(Tile - 1, Tile - 1);
        foreach (var (x, y) in new[] { (256, 100), (257, 100), (514, 100), (515, 100), (100, 256), (100, 257), (600, 257) })
        {
            if (sheet.Pixel(x, y) != background)
            {
                Note($"{sheet.Name}: the gutter at ({x}, {y}) is {sheet.Pixel(x, y)}, expected the background {background}");
            }
        }

        for (var tile = 0; tile < Views.Length; tile++)
        {
            var (left, top) = (tile % 3 * (Tile + Gutter), tile / 3 * (Tile + Gutter));
            NotBackground(sheet, (left + Tile / 2, top + Tile / 2), $"the centre of tile {Views[tile]}");
            RequireWhite(sheet, (left + 4, top + 4, 16, 16), $"tile {Views[tile]}'s caption");
            var single = await driver.CaptureAsync($"gate-7-axis-{(Views[tile][0] == '+' ? "plus" : "minus")}{Views[tile][1]}-ortho-256", new { view = Views[tile], projection = "orthographic", size = Tile });
            Tiled(sheet, left, top, single, Views[tile].Length, Views[tile]);
            var direction = Views[tile] switch
            {
                "+x" => Vector3.UnitX, "-x" => -Vector3.UnitX, "+y" => Vector3.UnitY, "-y" => -Vector3.UnitY, "+z" => Vector3.UnitZ, _ => -Vector3.UnitZ,
            };
            var camera = new Camera(min, max, direction, Tile, orthographic: true, up: Views[tile][1] is 'y' ? -Vector3.UnitZ : null);
            Look(single, solid, camera, GatehousePlan.Probes(solid), 7, Views[tile], doorFace, [], Tile);
            if (Views[tile] == "+y")
            {
                FromAbove(single, solid, camera);
            }
        }
    }

    /// <summary>
    /// From above, the cornice's top (+Y) has no face of its normal (the slab's top is cut away), so, as T-67 rules for the
    /// pocket's slanted walls, each sure probe on it must differ from the roof's shade by more than <see cref="Checks.Exact"/>
    /// in some channel. From below the cornice's underside and the slab's bottom share a normal, which <see cref="Look"/>
    /// holds to one shade; together the two fail a +y and -y swapped, or a view along Y with the wrong up.
    /// </summary>
    private static void FromAbove(ScenarioFrame frame, Solid solid, Camera camera)
    {
        var live = Enumerable.Range(0, solid.Faces.Length).Where(f => !solid.IsDeleted(f)).ToArray();
        var cornice = live.Single(f => solid.Normal(f).Y > 0.9999f && solid.Faces[f].Loop.All(v => solid.Vertices[v].Z > 0.15f - 1e-4f));
        var roof = live.Where(f => solid.Normal(f) is { Y: > 0.5f and < 0.99f }).ToArray();
        Equal($"{frame.Name}: the roof's faces", 2, roof.Length);
        var probes = GatehousePlan.Probes(solid).Select(p => (p, at: camera.Pixel(p.At), seen: p.Narrow ? solid.SeenNarrow(camera, Tile, camera.Pixel(p.At).X, camera.Pixel(p.At).Y) : solid.Seen(camera, Tile, camera.Pixel(p.At).X, camera.Pixel(p.At).Y))).ToArray();
        var roofShades = probes.Where(t => roof.Contains(t.seen)).Select(t => Pixel(frame, t.at.X, t.at.Y)).ToArray();
        var sure = 0;
        foreach (var (probe, (x, y), seen) in probes.Where(t => t.p.Face == cornice && t.seen != -2))
        {
            sure++;
            Equal($"{frame.Name}: what the geometry shows at the cornice's top {probe.Name} {probe.At} (pixel ({x}, {y}))", cornice, seen);
            var p = Pixel(frame, x, y);
            foreach (var r in roofShades.Where(r => Math.Abs(p.R - r.R) <= Exact && Math.Abs(p.G - r.G) <= Exact && Math.Abs(p.B - r.B) <= Exact))
            {
                Note($"{frame.Name}: the cornice's top {probe.Name} at ({x}, {y}) is {p}, within {Exact} of the roof's {r} in every channel: not the top seen from above");
            }
        }

        if (sure < 1 || roofShades.Length == 0)
        {
            Note($"{frame.Name}: {sure} sure probes on the cornice's top and {roofShades.Length} on the roof; the check needs one of each");
        }
    }

    /// <summary>The tile equals the single view's frame everywhere but the caption's corner.</summary>
    private static void Tiled(ScenarioFrame sheet, int left, int top, ScenarioFrame single, int letters, string what)
    {
        var (right, bottom) = (6 + 8 * letters, 22);
        for (var y = 0; y < Tile; y++)
        for (var x = 0; x < Tile; x++)
        {
            if ((y >= bottom || x >= right) && sheet.Pixel(left + x, top + y) != single.Pixel(x, y))
            {
                Note($"{sheet.Name}: tile {what} at ({x}, {y}) is {sheet.Pixel(left + x, top + y)}; {single.Name} there is {single.Pixel(x, y)}");
                return;
            }
        }
    }

    /// <summary>
    /// Every probe the ray test is sure of, and more than 4 pixels from a drawn line: the background where the ray meets nothing,
    /// else a face, never the background, in the shade of the first sure probe on a grey face of the same normal (T-67's rule;
    /// the door's is predicted from the front face's, and is its own when the front is not sure).
    /// The faces shown and, in a shaded frame, the number of sure probes are the ones worked in Core (<see cref="GatehousePlan.Expected"/>).
    /// </summary>
    private static void Look(ScenarioFrame frame, Solid solid, Camera camera, IEnumerable<Probe> probes, int stage, string view, int doorFace,
        (Vector2 From, Vector2 To)[] lines, int size = GatehousePlan.Size)
    {
        if (size == GatehousePlan.Size)
        {
            Size(frame, size, size);
            Corners(frame);
        }

        var shades = new List<(Vector3 Normal, int Face, Rgb Shade)>();
        var shown = new SortedSet<int>();
        var tested = 0;
        var all = probes.Select(p =>
        {
            var (x, y) = camera.Pixel(p.At);
            var seen = p.Narrow ? solid.SeenNarrow(camera, size, x, y) : solid.Seen(camera, size, x, y);
            return (p, x, y, seen);
        }).Where(t => t.seen != -2 && !lines.Any(l => DistanceToSegment(new Vector2(t.x + 0.5f, t.y + 0.5f), l.From, l.To) < 4)).ToArray();
        var predicted = doorFace >= 0 && all.FirstOrDefault(t => t.seen == 1) is { seen: 1 } reference
            ? Predict(Pixel(frame, reference.x, reference.y), GatehouseScenario.DoorBrown)
            : ((Rgb?)null);
        foreach (var (probe, x, y, seen) in all)
        {
            tested++;
            if (probe.Face == -1)
            {
                Equal($"{frame.Name}: what the geometry shows at {probe.Name} {probe.At} (pixel ({x}, {y}))", -1, seen);
            }

            if (seen == -1)
            {
                Background(frame, (x, y), $"{frame.Name}: {probe.Name} {probe.At}, which a ray meets nowhere");
                continue;
            }

            shown.Add(seen);
            NotBackground(frame, (x, y), $"{frame.Name}: {probe.Name} of face {probe.Face} {probe.At}, face {seen}");
            if (view == "wire")
            {
                Colour(frame, (x, y), Fill, Exact, $"{frame.Name}: {probe.Name} of face {probe.Face}, face {seen}, the wireframe's fill");
                continue;
            }

            if (seen == doorFace && predicted is { } door)
            {
                Colour(frame, (x, y), door, Exact, $"{frame.Name}: {probe.Name} of face {probe.Face}, the door, predicted {door}");
            }
            else if (Shade(solid, shades, seen, doorFace) is { } shade)
            {
                Colour(frame, (x, y), shade.Shade, Exact, $"{frame.Name}: {probe.Name} of face {probe.Face}, face {seen}, predicted from face {shade.Face} of the same normal");
            }
            else
            {
                shades.Add((solid.Normal(seen), seen, Pixel(frame, x, y)));
            }
        }

        if (lines.Length == 0 && size == GatehousePlan.Size && GatehousePlan.Expected.TryGetValue((stage, view), out var expected))
        {
            // Every probe in the window's hole must be sure: one the ray test is not sure of is a failure, not a probe left out.
            var window = all.Count(t => t.p.Face == -1);
            Equal($"{frame.Name}: sure probes in the window's hole", probes.Count(p => p.Face == -1), window);
            Equal($"{frame.Name}: sure probes", expected.Sure + window, tested);
            Equal($"{frame.Name}: faces shown", expected.Faces, string.Join(" ", shown));
        }
        else if (tested < 10)
        {
            Note($"{frame.Name}: only {tested} probes were surely a face or the background");
        }
    }

    /// <summary>The shade already read for a grey face of <paramref name="face"/>'s normal; the coloured door only from itself.</summary>
    private static (Vector3 Normal, int Face, Rgb Shade)? Shade(Solid solid, List<(Vector3 Normal, int Face, Rgb Shade)> shades, int face, int doorFace)
    {
        var normal = solid.Normal(face);
        foreach (var s in shades)
        {
            if (face == doorFace || s.Face == doorFace ? s.Face == face : Vector3.Dot(s.Normal, normal) > 0.9999f)
            {
                return s;
            }
        }

        return null;
    }

    private static Rgb Pixel(ScenarioFrame frame, int x, int y)
    {
        var p = frame.Pixel(x, y);
        return (p.R, p.G, p.B);
    }

    private static string Counts(Solid solid) =>
        $"{solid.Faces.Where((f, i) => !solid.IsDeleted(i)).SelectMany(f => f.Loop.Concat(f.Holes.SelectMany(h => h))).Distinct().Count()} vertices, {Enumerable.Range(0, solid.Faces.Length).Count(f => !solid.IsDeleted(f))} faces";
}
