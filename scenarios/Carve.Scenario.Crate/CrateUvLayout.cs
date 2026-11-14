using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Crate.CrateLooks;

namespace Carve.Scenario.Crate;

/// <summary>
/// The <c>uvs</c> look (T-58) on the crate the checker stage leaves (its UVs undone): laid with <c>project_uvs box</c> (ruling 1),
/// one seam marked between a +Z face and a +X face, then the layout at 512, 256 and 1024, with face labels, with one face
/// highlighted, and with one face's UVs moved to u 1..1.5 so the window grows; then undone, and the front byte-equal to before.
/// Every pixel checked is worked by <see cref="UvLayoutPixels"/> from the UVs read back with <c>get_mesh</c>.
/// </summary>
internal static class CrateUvLayout
{
    /// <summary>Pixels kept clear of a line, and the corner and face labels' places.</summary>
    private const int Clear = 4, CornerClear = 44, LabelHalfWidth = 22, LabelHalfHeight = 12;

    private static readonly List<string> Log = [];

    public static async Task RunAsync(ScenarioDriver driver, ScenarioFrame shadedFront)
    {
        Log.Clear();
        var crate = await ReadAsync(driver);
        var boxed = await CallJsonAsync(driver, "project_uvs", new { method = "box" });
        if (boxed.GetProperty("mapped").GetArrayLength() != crate.Faces.Length)
        {
            driver.Fail($"project_uvs box mapped {boxed.GetProperty("mapped").GetArrayLength()} faces, not all {crate.Faces.Length}");
        }

        var (seamA, seamB, front, side) = SeamEdge(driver, crate);
        await CallJsonAsync(driver, "mark_edges", new { edges = new[] { new[] { seamA, seamB } }, seam = true });
        var layout = await UvsAsync(driver, crate);
        var counts = $"{crate.Vertices.Length} vertices, {crate.Faces.Length} faces";

        foreach (var (name, size) in new[] { ("crate-uvs", 512), ("crate-uvs-256", 256), ("crate-uvs-1024", 1024) })
        {
            var frame = await driver.CaptureAsync(name, new { mode = "uvs", size });
            CheckCaption(driver, frame, $"{size}x{size}, uvs mesh, {counts}");
            var pixels = UvLayoutPixels.Of(layout.Values, size);
            Sweep(driver, frame, pixels, layout, highlighted: null, labels: false);
            Origin(driver, frame, pixels, layout);
            Seam(driver, frame, pixels, layout, seamA, seamB, front, side, crate);
        }

        var labelled = await driver.CaptureAsync("crate-uvs-labels", new { mode = "uvs", labels = "faces" });
        CheckCaption(driver, labelled, $"512x512, uvs mesh, labels faces, {counts}");
        Sweep(driver, labelled, UvLayoutPixels.Of(layout.Values, 512), layout, highlighted: null, labels: true);

        // The +Z side's largest face, highlighted solid orange.
        var picked = layout.Keys.Where(f => CrateUvs.SideOf(CrateUvs.Normal(crate, f)) == Side.PlusZ).MaxBy(f => UvLayoutPixels.Area(layout[f]));
        var highlight = await driver.CaptureAsync("crate-uvs-highlight", new { mode = "uvs", highlight = new { faces = new[] { picked } } });
        CheckCaption(driver, highlight, $"512x512, uvs mesh, highlight 1 face, {counts}");
        var orange = Sweep(driver, highlight, UvLayoutPixels.Of(layout.Values, 512), layout, highlighted: picked, labels: false);
        if (orange < 4) driver.Fail($"crate-uvs-highlight: only {orange} orange samples inside face {picked}");

        // One -Y face moved to u 1..1.5: the window grows right, the square's top-left at (32, 106.7) at 512.
        var moved = layout.Keys.Where(f => CrateUvs.SideOf(CrateUvs.Normal(crate, f)) == Side.MinusY && layout[f].Count == 4).MaxBy(f => UvLayoutPixels.Area(layout[f]));
        Vector2[] outside = [new(1, 0), new(1.5f, 0), new(1.5f, 0.5f), new(1, 0.5f)];
        await CallJsonAsync(driver, "set_uvs", new { faces = new[] { new { face = moved, uv = outside.Select(p => new[] { p.X, p.Y }).ToArray() } } });
        var grownLayout = new Dictionary<int, IReadOnlyList<Vector2>>(layout) { [moved] = outside };
        var grown = await driver.CaptureAsync("crate-uvs-grown", new { mode = "uvs" });
        CheckCaption(driver, grown, $"512x512, uvs mesh, {counts}");
        var grownPixels = UvLayoutPixels.Of(grownLayout.Values, 512);
        var corner = grownPixels.PixelOf(Vector2.Zero);
        if (Math.Abs(corner.X - 32) > 0.01 || Math.Abs(corner.Y - 106.667) > 0.01) driver.Fail($"crate-uvs-grown: the square's top-left is worked to {corner}, not (32, 106.7)");
        Sweep(driver, grown, grownPixels, grownLayout, highlighted: null, labels: false);
        Origin(driver, grown, grownPixels, grownLayout);
        var inMoved = grownPixels.PixelOf(new Vector2(1.3125f, 0.3125f)); // a cell centre, column 2 row 2
        Expect(driver, grown, ((int)inMoved.X, (int)inMoved.Y), UvLayoutPixels.Texel(grownPixels.UvAt((int)inMoved.X, (int)inMoved.Y)), $"face {moved} moved to u 1..1.5, bright");

        File.WriteAllLines(Path.Combine(driver.RunFolder, "uv-layout-samples.txt"), Log);
        for (var undo = 0; undo < 3; undo++) await CallJsonAsync(driver, "undo", new { }); // set_uvs, mark_edges, project_uvs
        var after = await driver.CaptureAsync("crate-front-after-uvs", new { view = "front" });
        driver.AssertBytesEqual("crate front, after the layout's UVs and seam were undone", shadedFront.Pixels, after.Pixels);
    }

    /// <summary>Each face's corner UVs as <c>get_mesh</c> gives them.</summary>
    private static async Task<Dictionary<int, IReadOnlyList<Vector2>>> UvsAsync(ScenarioDriver driver, Shape crate)
    {
        var mesh = await CallJsonAsync(driver, "get_mesh", new { });
        var layout = new Dictionary<int, IReadOnlyList<Vector2>>();
        foreach (var entry in mesh.GetProperty("uvs").EnumerateArray())
        {
            var face = entry.GetProperty("face").GetInt32();
            var uv = entry.GetProperty("uv").EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToArray();
            if (uv.Length != crate.Faces[face].Length) driver.Fail($"face {face} has {uv.Length} UVs for {crate.Faces[face].Length} corners (a hole?)");
            layout[face] = uv;
        }

        return layout;
    }

    /// <summary>An edge of a +Z face that a +X face shares: a seam between two islands of the box layout.</summary>
    private static (int A, int B, int Front, int Side) SeamEdge(ScenarioDriver driver, Shape crate)
    {
        for (var f = 0; f < crate.Faces.Length; f++)
        {
            if (CrateUvs.SideOf(CrateUvs.Normal(crate, f)) != Side.PlusZ) continue;
            var loop = crate.Faces[f];
            for (var i = 0; i < loop.Length; i++)
            {
                var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
                for (var g = 0; g < crate.Faces.Length; g++)
                {
                    if (CrateUvs.SideOf(CrateUvs.Normal(crate, g)) != Side.PlusX) continue;
                    var other = crate.Faces[g];
                    if (Enumerable.Range(0, other.Length).Any(j => other[j] == b && other[(j + 1) % other.Length] == a)) return (a, b, f, g);
                }
            }
        }

        driver.Fail("no edge of the crate joins a +Z face to a +X face");
        return default;
    }

    /// <summary>
    /// Every 8th pixel (a quarter of that at 256) clear of every line and label: inside a face, its texel's byte (orange if it is the
    /// highlighted face alone); inside none, the dimmed byte. Returns how many orange samples there were.
    /// </summary>
    private static int Sweep(ScenarioDriver driver, ScenarioFrame frame, UvLayoutPixels pixels, Dictionary<int, IReadOnlyList<Vector2>> layout, int? highlighted, bool labels)
    {
        var step = Math.Max(4, pixels.Size / 64);
        var (bright, dim, orange) = (0, 0, 0);
        Vector2[] square = [Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY];
        var corners = square.Select(pixels.PixelOf).ToArray();
        var marks = labels ? layout.Values.Select(l => pixels.PixelOf(UvLayoutPixels.LabelPoint(l))).ToArray() : [];
        var segments = layout.Values.SelectMany(l => l.Select((p, i) => (p, l[(i + 1) % l.Count]))).Concat(square.Select((p, i) => (p, square[(i + 1) % 4]))).ToArray();
        for (var y = step / 2; y < pixels.Size; y += step)
        for (var x = step / 2; x < pixels.Size; x += step)
        {
            if (corners.Any(c => Math.Abs(c.X - x) < CornerClear && Math.Abs(c.Y - y) < CornerClear)) continue;
            if (marks.Any(m => Math.Abs(m.X - x) < LabelHalfWidth && Math.Abs(m.Y - y) < LabelHalfHeight)) continue;
            var uv = pixels.UvAt(x, y);
            if (segments.Any(s => UvLayoutPixels.Distance(uv, s.Item1, s.Item2) * pixels.Scale < Clear)) continue;
            var inside = layout.Where(e => UvLayoutPixels.Inside(e.Value, uv)).Select(e => e.Key).ToList();
            var texel = UvLayoutPixels.Texel(uv);
            if (highlighted is { } h && inside.Contains(h))
            {
                if (inside.Count > 1) continue;
                Expect(driver, frame, (x, y), UvLayoutPixels.Orange, $"face {h} highlighted, solid orange");
                orange++;
            }
            else if (inside.Count > 0)
            {
                Expect(driver, frame, (x, y), texel, $"inside face {inside[0]}, bright: the texel at ({uv.X:0.000}, {uv.Y:0.000})");
                bright++;
            }
            else
            {
                Expect(driver, frame, (x, y), UvLayoutPixels.Dimmed(texel), $"no face, dim: the texel at ({uv.X:0.000}, {uv.Y:0.000}) at 40%");
                dim++;
            }
        }

        if (bright < 20 || dim < 20) driver.Fail($"{frame.Name}: only {bright} bright and {dim} dim samples");
        Log.Add($"{frame.Name}\tsweep every {step} px\t{bright} bright\t{dim} dim\t{orange} orange");
        return orange;
    }

    /// <summary>uv (0,0)'s white cell at the square's top-left, and the texel at its v-flip and u-mirror not white.</summary>
    private static void Origin(ScenarioDriver driver, ScenarioFrame frame, UvLayoutPixels pixels, Dictionary<int, IReadOnlyList<Vector2>> layout)
    {
        var at = pixels.PixelOf(new Vector2(1 / 16f, 1 / 16f));
        var (x, y) = ((int)at.X, (int)at.Y);
        var uv = pixels.UvAt(x, y);
        var inside = layout.Values.Any(l => UvLayoutPixels.Inside(l, uv));
        var want = inside ? (255, 255, 255) : UvLayoutPixels.Dimmed((255, 255, 255));
        Expect(driver, frame, (x, y), want, $"uv (0,0)'s white cell, {(inside ? "bright" : "dim")}");
        foreach (var mirror in new[] { new Vector2(1 / 16f, 15 / 16f), new Vector2(15 / 16f, 1 / 16f) })
        {
            var p = pixels.PixelOf(mirror);
            var got = frame.Pixel((int)p.X, (int)p.Y);
            if (Math.Max(got.R, Math.Max(got.G, got.B)) - Math.Min(got.R, Math.Min(got.G, got.B)) <= 8)
            {
                driver.Fail($"{frame.Name}: the cell at uv {mirror}, (0,0)'s mirror, is grey or white at ({(int)p.X}, {(int)p.Y}): {got}");
            }
        }
    }

    /// <summary>The seam yellow at its UV midpoint on each side it divides, and not 6 px off it.</summary>
    private static void Seam(ScenarioDriver driver, ScenarioFrame frame, UvLayoutPixels pixels, Dictionary<int, IReadOnlyList<Vector2>> layout, int a, int b, int front, int side, Shape crate)
    {
        foreach (var face in new[] { front, side })
        {
            var loop = crate.Faces[face];
            var (ia, ib) = (Array.IndexOf(loop, a), Array.IndexOf(loop, b));
            var (ua, ub) = (pixels.PixelOf(layout[face][ia]), pixels.PixelOf(layout[face][ib]));
            var mid = (ua + ub) / 2;
            var across = Vector2.Normalize(new Vector2(-(ub.Y - ua.Y), ub.X - ua.X));
            (int, int, int) Read(Vector2 p) { var q = frame.Pixel((int)p.X, (int)p.Y); return (q.R, q.G, q.B); }
            (int, int, int)[] beside = [Read(mid + across * 4), Read(mid - across * 4)];
            var best = Enumerable.Range(-2, 5).SelectMany(dx => Enumerable.Range(-2, 5).Select(dy => Read(mid + new Vector2(dx, dy))))
                .SelectMany(p => beside.Select(bg => UvLayoutPixels.SeamShare(p, bg))).Max();
            Log.Add($"{frame.Name}\tseam {a}-{b} on face {face}\tat ({mid.X:0.0}, {mid.Y:0.0})\tshare {best:0.00}");
            if (best < 0.4) driver.Fail($"{frame.Name}: the seam {a}-{b} on face {face} at ({mid.X:0.0}, {mid.Y:0.0}) is at most {best:0.00} #FFD800");

            // 6 px off it, toward the face's inside: not the seam.
            var centre = pixels.PixelOf(UvLayoutPixels.LabelPoint(layout[face]));
            var inward = Vector2.Dot(centre - mid, across) > 0 ? across : -across;
            var off = mid + inward * 6;
            var share = UvLayoutPixels.SeamShare(Read(off), UvLayoutPixels.Texel(pixels.UvAt((int)off.X, (int)off.Y)));
            if (share >= 0.25) driver.Fail($"{frame.Name}: 6 px off the seam on face {face}, at ({off.X:0.0}, {off.Y:0.0}), is {share:0.00} #FFD800");
        }
    }

    private static void Expect(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, (int R, int G, int B) want, string what)
    {
        var p = frame.Pixel(at.X, at.Y);
        var got = (p.R, p.G, p.B);
        var ok = UvLayoutPixels.Near(got, want);
        if (!ok || Log.Count < 4000) Log.Add($"{frame.Name}\t{what}\tat {at}\t{got}\texpected {want}\t{(ok ? "ok" : "FAIL")}");
        if (!ok) driver.Fail($"{frame.Name}: {what}: ({at.X}, {at.Y}) is {got}, expected {want} ±{UvLayoutPixels.Tolerance}");
    }
}
