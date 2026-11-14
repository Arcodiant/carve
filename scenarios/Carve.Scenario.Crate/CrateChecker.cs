using System.Numerics;
using Carve.Debug;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Crate.CrateLooks;

namespace Carve.Scenario.Crate;

/// <summary>The six sides a crate face is laid by: its normal's dominant axis and sign.</summary>
internal enum Side { PlusX, MinusX, PlusY, MinusY, PlusZ, MinusZ }

/// <summary>
/// The crate's UVs (T-57, and T-58 lays them again): each face by its normal's dominant axis, over the crate's bounds
/// <see cref="CrateLooks.CrateMin"/> to <see cref="CrateLooks.CrateMax"/>, so every face of one side shares one checker,
/// laid to read unmirrored from outside with v down (glTF axes): +Z u = (x - x0)/sx, v = (y1 - y)/sy; -Z u = (x1 - x)/sx;
/// +X u = (z1 - z)/sz; -X u = (z - z0)/sz, each with the same v; +Y u = (x - x0)/sx, v = (z - z0)/sz (screen up is -Z
/// from above); -Y u = (x - x0)/sx, v = (z1 - z)/sz.
/// </summary>
internal static class CrateUvs
{
    private static readonly Vector3 Low = CrateMin, Size = CrateMax - CrateMin;

    /// <summary>A face's Newell normal, not normalised.</summary>
    public static Vector3 Normal(Shape shape, int face)
    {
        var n = Vector3.Zero;
        var loop = shape.Faces[face];
        for (var i = 0; i < loop.Length; i++)
        {
            Vector3 a = shape.Vertices[loop[i]], b = shape.Vertices[loop[(i + 1) % loop.Length]];
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }

        return n;
    }

    public static Side SideOf(Vector3 n)
    {
        var a = Vector3.Abs(n);
        return a.X >= a.Y && a.X >= a.Z ? (n.X > 0 ? Side.PlusX : Side.MinusX)
            : a.Y >= a.Z ? (n.Y > 0 ? Side.PlusY : Side.MinusY)
            : n.Z > 0 ? Side.PlusZ : Side.MinusZ;
    }

    /// <summary>The UV of a point on a face of <paramref name="side"/>.</summary>
    public static Vector2 Uv(Side side, Vector3 p)
    {
        var t = (p - Low) / Size;
        return side switch
        {
            Side.PlusZ => new(t.X, 1 - t.Y),
            Side.MinusZ => new(1 - t.X, 1 - t.Y),
            Side.PlusX => new(1 - t.Z, 1 - t.Y),
            Side.MinusX => new(t.Z, 1 - t.Y),
            Side.PlusY => new(t.X, t.Z),
            _ => new(t.X, 1 - t.Z),
        };
    }

    /// <summary>The point of <paramref name="side"/>'s plane at <paramref name="plane"/> (its coordinate along the side's axis) with UV <paramref name="uv"/>.</summary>
    public static Vector3 At(Side side, Vector2 uv, float plane)
    {
        var t = side switch
        {
            Side.PlusZ => new Vector3(uv.X, 1 - uv.Y, 0),
            Side.MinusZ => new Vector3(1 - uv.X, 1 - uv.Y, 0),
            Side.PlusX => new Vector3(0, 1 - uv.Y, 1 - uv.X),
            Side.MinusX => new Vector3(0, 1 - uv.Y, uv.X),
            Side.PlusY => new Vector3(uv.X, 0, uv.Y),
            _ => new Vector3(uv.X, 0, 1 - uv.Y),
        };
        var p = Low + t * Size;
        return side is Side.PlusX or Side.MinusX ? p with { X = plane } : side is Side.PlusY or Side.MinusY ? p with { Y = plane } : p with { Z = plane };
    }

    /// <summary>The <c>set_uvs</c> faces: every face but <paramref name="unmapped"/>, one UV per corner.</summary>
    public static object[] Lay(Shape shape, params int[] unmapped) =>
        Enumerable.Range(0, shape.Faces.Length).Where(f => !unmapped.Contains(f)).Select(f =>
        {
            var side = SideOf(Normal(shape, f));
            return (object)new { face = f, uv = shape.Faces[f].Select(v => Uv(side, shape.Vertices[v])).Select(uv => new[] { uv.X, uv.Y }).ToArray() };
        }).ToArray();
}

/// <summary>
/// The checker look on the crate (T-57): every face laid by <see cref="CrateUvs"/> but the lid panel, left unmapped, and
/// seen from the front, the back, above, as a 2 x 2 sheet and at 256 and 1024. Every check is worked from the geometry:
/// each cell centre of each face is projected by <see cref="CameraPixels"/>, kept only where a ray test finds it in
/// sight with a ring 3.5 pixels round it on the same surface, and then classified (white at
/// uv (0,0), magenta on the unmapped face, else its column's hue), with a light cell brighter than the dark one below it.
/// The cells each frame must show are named, white's mirrors among them, so a mirrored or flipped camera, a u-mirrored
/// texture or a v-flipped upload fails. Undone at the end, the crate's front frame byte-equal to the first.
/// </summary>
internal static class CrateChecker
{
    private const int Tile = 256, Gutter = 2;
    private static readonly Vector3 Back = -Front;

    private sealed record Sample(int Face, Side Side, int Column, int Row, (int X, int Y) Pixel);

    private sealed record View(string Name, Vector3 Direction, int Size, int Left = 0, int Top = 0, int CaptionWidth = 0);

    /// <summary>Every cell checked, one line each, kept in the run folder as <c>checker-samples.txt</c> for review.</summary>
    private static readonly List<string> Log = [];

    public static async Task RunAsync(ScenarioDriver driver, ScenarioFrame shadedFront)
    {
        Log.Clear();
        await BuildAsync(driver);
        var crate = await ReadAsync(driver);
        var (low, high) = (crate.Vertices.Aggregate(Vector3.Min), crate.Vertices.Aggregate(Vector3.Max));
        if (Vector3.Distance(low, CrateMin) > 1e-4f || Vector3.Distance(high, CrateMax) > 1e-4f)
        {
            driver.Fail($"the crate's bounds are {low} to {high}, not {CrateMin} to {CrateMax}");
        }

        var laid = await CallJsonAsync(driver, "set_uvs", new { faces = CrateUvs.Lay(crate, TopFace) });
        if (laid.GetProperty("set").GetInt32() != crate.Faces.Length - 1 || laid.GetProperty("unmapped").GetInt32() != 1)
        {
            driver.Fail($"set_uvs on the crate reported {laid}: expected {crate.Faces.Length - 1} set and 1 unmapped");
        }

        var counts = $"{crate.Vertices.Length} vertices, {crate.Faces.Length} faces";
        foreach (var (name, view, size) in new[] { ("front", "front", Size), ("back", "back", Size), ("plusy", "+y", Size), ("front-256", "front", Tile), ("front-1024", "front", 1024) })
        {
            var frame = await driver.CaptureAsync($"crate-checker-{name}", new { view, size, mode = "checker" });
            CheckCaption(driver, frame, $"{size}x{size}, view {view}, checker, {counts}");
            var direction = view switch { "front" => Front, "back" => Back, _ => Vector3.UnitY };
            var samples = Sweep(driver, frame, crate, new View(name, direction, size));
            Require(driver, frame, name, samples, view switch
            {
                "front" => [.. Corners(Side.PlusZ), .. Row(Side.PlusZ, 0)],
                "back" => [.. Corners(Side.MinusZ), .. Row(Side.MinusZ, 0)],
                _ => Corners(Side.PlusY),
            }, unmappedInSight: view != "back");
        }

        string[] views = ["front", "back", "+x", "+y"];
        var sheet = await driver.CaptureAsync("crate-checker-sheet", new { views, columns = 2, size = Tile, mode = "checker" });
        CheckCaption(driver, sheet, $"2x2 tiles of {Tile}x{Tile}, checker, views front, back, +x, +y, {counts}");
        for (var k = 0; k < views.Length; k++)
        {
            var direction = views[k] switch { "front" => Front, "back" => Back, "+x" => Vector3.UnitX, _ => Vector3.UnitY };
            var side = views[k] switch { "front" => Side.PlusZ, "back" => Side.MinusZ, "+x" => Side.PlusX, _ => Side.PlusY };
            var tile = new View($"sheet tile {views[k]}", direction, Tile, k % 2 * (Tile + Gutter), k / 2 * (Tile + Gutter), 8 + 8 * views[k].Length + 4);
            var samples = Sweep(driver, sheet, crate, tile);
            // The lid panel is in sight from the front and above; from below-behind and level with +X it is not.
            Require(driver, sheet, tile.Name, samples, Corners(side), unmappedInSight: views[k] is "front" or "+y");
        }

        File.WriteAllLines(Path.Combine(driver.RunFolder, "checker-samples.txt"), Log);
        var undone = await CallJsonAsync(driver, "undo", new { });
        var after = await driver.CaptureAsync("crate-front-after-checker", new { view = "front" });
        driver.AssertBytesEqual($"crate front, after the checker's UVs were undone ({undone})", shadedFront.Pixels, after.Pixels);
    }

    /// <summary>uv (0,0)'s cell, and its u-mirror and v-flip, where white must not be.</summary>
    private static (Side, int, int)[] Corners(Side side) => [(side, 0, 0), (side, 7, 0), (side, 0, 7)];

    private static IEnumerable<(Side, int, int)> Row(Side side, int row) => Enumerable.Range(0, 8).Select(c => (side, c, row));

    /// <summary>Every cell named is seen and classified, a light cell is above a darker one somewhere, and the unmapped panel is seen where it should be.</summary>
    private static void Require(ScenarioDriver driver, ScenarioFrame frame, string what, List<Sample> samples, IEnumerable<(Side Side, int Column, int Row)> cells, bool unmappedInSight)
    {
        foreach (var (side, c, r) in cells)
        {
            if (!samples.Any(s => s.Side == side && s.Column == c && s.Row == r && s.Face != TopFace))
            {
                driver.Fail($"{frame.Name}: {what}: no cell ({c}, {r}) of the {side} side is in clear sight");
            }
        }

        if (unmappedInSight != samples.Any(s => s.Face == TopFace))
        {
            driver.Fail($"{frame.Name}: {what}: the unmapped lid panel is {(unmappedInSight ? "not" : "unexpectedly")} in clear sight");
        }

        var pairs = 0;
        foreach (var light in samples.Where(s => s.Face != TopFace && (s.Column + s.Row) % 2 == 0 && (s.Column, s.Row) != (0, 0)))
        {
            if (samples.FirstOrDefault(s => s.Face == light.Face && s.Column == light.Column && s.Row == light.Row + 1) is not { } dark) continue;
            pairs++;
            var (lp, dp) = (frame.Pixel(light.Pixel.X, light.Pixel.Y), frame.Pixel(dark.Pixel.X, dark.Pixel.Y));
            if (CheckerColours.Value(lp.R, lp.G, lp.B) <= CheckerColours.Value(dp.R, dp.G, dp.B))
            {
                driver.Fail($"{frame.Name}: {what}: face {light.Face}'s light cell ({light.Column}, {light.Row}) at {light.Pixel} is {lp}, no brighter than the dark cell below it at {dark.Pixel}, {dp}");
            }
        }

        if (pairs == 0)
        {
            driver.Fail($"{frame.Name}: {what}: no light cell above a dark one on one face is in clear sight");
        }
    }

    /// <summary>Each face's cell centres in clear sight in <paramref name="view"/>, each checked as it is found.</summary>
    private static List<Sample> Sweep(ScenarioDriver driver, ScenarioFrame frame, Shape crate, View view)
    {
        var pixel = Perspective(CrateMin, CrateMax, view.Direction, view.Size);
        var eye = Eye(CrateMin, CrateMax, view.Direction);
        var samples = new List<Sample>();
        for (var f = 0; f < crate.Faces.Length; f++)
        {
            var normal = CrateUvs.Normal(crate, f);
            if (normal.LengthSquared() < 1e-12f) continue;
            var side = CrateUvs.SideOf(normal);
            var corner = crate.Vertices[crate.Faces[f][0]];
            var plane = side is Side.PlusX or Side.MinusX ? corner.X : side is Side.PlusY or Side.MinusY ? corner.Y : corner.Z;
            var (a, b) = InPlane(side);
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
            {
                var at = CrateUvs.At(side, new Vector2((c + 0.5f) / 8, (r + 0.5f) / 8), plane);
                if (Vector3.Dot(normal, eye - at) <= 0 || !Inside(crate, f, at) || !InSight(crate, f, eye, at)) continue;

                // The point is its cell's centre (frac(8u) = frac(8v) = 0.5). A ring 3.5 pixels round it, in sight, on the face or one
                // beside it in the same plane and side: the checker draws no edge between those, and their UVs run on
                // as one (the frame's mitred bands meet on the diagonal through the +Z side's white cell).
                var centre = pixel(at);
                var perUnit = Enumerable.Range(0, 8).Select(k => Ring(a, b, k)).Min(d => Vector2.Distance(pixel(at + d * 0.01f), centre) / 0.01f);
                var radius = 3.5f / perUnit;
                if (!Enumerable.Range(0, 8).Select(k => at + Ring(a, b, k) * radius).All(p => OnSurface(crate, side, plane, p) && InSight(crate, f, eye, p))) continue;

                var (x, y) = Holding(centre);
                if (x < 4 || y < 4 || x >= view.Size - 4 || y >= view.Size - 4 || (x < view.CaptionWidth && y < 24)) continue;
                var sample = new Sample(f, side, c, r, (x + view.Left, y + view.Top));
                samples.Add(sample);
                Check(driver, frame, view.Name, sample);
            }
        }

        return samples;
    }

    private static void Check(ScenarioDriver driver, ScenarioFrame frame, string what, Sample s)
    {
        var p = frame.Pixel(s.Pixel.X, s.Pixel.Y);
        var (ok, expected, column) = CheckerColours.Judge(p.R, p.G, p.B, s.Face != TopFace, s.Column, s.Row);
        Log.Add($"{frame.Name}	{what}	face {s.Face}	{s.Side}	cell ({s.Column}, {s.Row})	at {s.Pixel}	{p}	expected {expected}	{(ok ? "ok" : "FAIL")}");
        if (!ok)
        {
            driver.Fail($"{frame.Name}: {what}: face {s.Face} ({s.Side}) cell ({s.Column}, {s.Row}) at {s.Pixel} is {p}, expected {expected}; nearest column {column}");
        }
    }

    /// <summary>The side's in-plane axes, unit length.</summary>
    private static (Vector3 A, Vector3 B) InPlane(Side side) => side switch
    {
        Side.PlusX or Side.MinusX => (Vector3.UnitY, Vector3.UnitZ),
        Side.PlusY or Side.MinusY => (Vector3.UnitX, Vector3.UnitZ),
        _ => (Vector3.UnitX, Vector3.UnitY),
    };

    private static Vector3 Ring(Vector3 a, Vector3 b, int k) => a * MathF.Cos(k * MathF.PI / 4) + b * MathF.Sin(k * MathF.PI / 4);

    /// <summary>Whether a point lies on a face of <paramref name="side"/> in the plane at <paramref name="plane"/>.</summary>
    private static bool OnSurface(Shape crate, Side side, float plane, Vector3 p) =>
        Enumerable.Range(0, crate.Faces.Length).Any(g =>
        {
            var n = CrateUvs.Normal(crate, g);
            if (n.LengthSquared() < 1e-12f || CrateUvs.SideOf(n) != side) return false;
            var corner = crate.Vertices[crate.Faces[g][0]];
            var at = side is Side.PlusX or Side.MinusX ? corner.X : side is Side.PlusY or Side.MinusY ? corner.Y : corner.Z;
            return MathF.Abs(at - plane) < 1e-5f && Inside(crate, g, p);
        });

    /// <summary>Whether a point of a face's plane lies inside its loop, by the even-odd rule on the plane's own axes.</summary>
    private static bool Inside(Shape crate, int face, Vector3 p)
    {
        var (a, b) = InPlane(CrateUvs.SideOf(CrateUvs.Normal(crate, face)));
        var loop = crate.Faces[face];
        var inside = false;
        for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
        {
            var (pi, pj) = (crate.Vertices[loop[i]], crate.Vertices[loop[j]]);
            var (yi, yj, py) = (Vector3.Dot(pi, b), Vector3.Dot(pj, b), Vector3.Dot(p, b));
            if ((yi > py) == (yj > py)) continue;
            var x = Vector3.Dot(pi, a) + (py - yi) / (yj - yi) * (Vector3.Dot(pj, a) - Vector3.Dot(pi, a));
            if (Vector3.Dot(p, a) < x) inside = !inside;
        }

        return inside;
    }

    /// <summary>Whether nothing of the crate but <paramref name="face"/> crosses the segment from the eye to <paramref name="p"/>.</summary>
    private static bool InSight(Shape crate, int face, Vector3 eye, Vector3 p)
    {
        var d = p - eye;
        for (var g = 0; g < crate.Faces.Length; g++)
        {
            if (g == face) continue;
            var n = CrateUvs.Normal(crate, g);
            var across = Vector3.Dot(n, d);
            if (MathF.Abs(across) < 1e-9f) continue;
            var t = Vector3.Dot(n, crate.Vertices[crate.Faces[g][0]] - eye) / across;
            if (t <= 1e-4f || t >= 1 - 1e-4f) continue;
            if (Inside(crate, g, eye + d * t)) return false;
        }

        return true;
    }
}
