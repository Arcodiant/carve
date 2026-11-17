using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Crate;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Uvs;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class UvsExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, UvsScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The barrel of T-53 (the one source of its calls: vertices, faces and seams), as <c>TestMeshes.Barrel</c> and <c>BarrelSeams</c> build
/// it: eight sides, five rings at y 0, 0.1, 0.5, 0.9 and 1, radii 0.36, 0.38, 0.42, 0.38 and 0.36; ring i's vertex k is id 8i + k at
/// angle 2 pi k / 8, at (r cos a, y, -r sin a) rounded to 6 decimals.
/// </summary>
internal static class BarrelSpec
{
    public static readonly double[] Heights = [0, 0.1, 0.5, 0.9, 1.0];
    public static readonly double[] Radii = [0.36, 0.38, 0.42, 0.38, 0.36];

    public static double[][] Vertices { get; } = Enumerable.Range(0, 5).SelectMany(i => Enumerable.Range(0, 8).Select(k =>
    {
        var a = 2 * Math.PI * k / 8;
        return new[] { Math.Round(Radii[i] * Math.Cos(a), 6), Heights[i], Math.Round(-Radii[i] * Math.Sin(a), 6) };
    })).ToArray();

    public static int[][] Faces { get; } =
    [
        .. Enumerable.Range(0, 4).SelectMany(i => Enumerable.Range(0, 8).Select(k => new[] { 8 * i + k, 8 * i + (k + 1) % 8, 8 * (i + 1) + (k + 1) % 8, 8 * (i + 1) + k })),
        [7, 6, 5, 4, 3, 2, 1, 0],
        [32, 33, 34, 35, 36, 37, 38, 39],
    ];

    /// <summary>The four vertical edges at -Z and both cap rings.</summary>
    public static int[][] Seams { get; } =
    [
        [2, 10], [10, 18], [18, 26], [26, 34],
        .. Enumerable.Range(0, 8).Select(k => new[] { k, (k + 1) % 8 }),
        .. Enumerable.Range(0, 8).Select(k => new[] { 32 + k, 32 + (k + 1) % 8 }),
    ];

    /// <summary>Ring 2's vertex k moved outward, along its own radius, by <paramref name="by"/> metres.</summary>
    public static double[] Outward(int k, double by)
    {
        var a = 2 * Math.PI * k / 8;
        return [Math.Round(by * Math.Cos(a), 6), 0, Math.Round(-by * Math.Sin(a), 6)];
    }
}

/// <summary>
/// M-6 through the real tools: the barrel seamed and unwrapped, the crate box-projected and moved 3 m aside (render frames every mesh,
/// so each frame zooms on one), both seen in both UV looks, a fault made and found twice, then saved, reopened and exported.
/// </summary>
public sealed class UvsScenario : IScenario
{
    private static readonly Vector3 Front = CrateLooks.Front, Back = -CrateLooks.Front;
    private const int Size = 512, Tile = 256, Gutter = 2;

    private static readonly (string Name, string View, Vector3 Direction, int Columns, bool Pairs)[] BarrelViews =
    [
        ("front", "front", Front, 3, true), ("back", "back", Back, 3, true), ("minus-z", "-z", -Vector3.UnitZ, 2, false), ("plus-y", "+y", Vector3.UnitY, 3, true),
    ];

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The barrel: define_mesh, the seams, unwrap with the defaults; three islands, no UV warning.
        CheckCounts(driver, "define_mesh", await CallJsonAsync(driver, "define_mesh", new { name = "Barrel", vertices = BarrelSpec.Vertices, faces = BarrelSpec.Faces }), 40, 34);
        await CallJsonAsync(driver, "mark_edges", new { edges = BarrelSpec.Seams, seam = true, mesh = "Barrel" });
        var unwrapped = await CallJsonAsync(driver, "unwrap", new { mesh = "Barrel" });
        var side = unwrapped.GetProperty("islands").EnumerateArray().Single(i => i.GetProperty("faces").GetInt32() == 32);
        var baseline = side.GetProperty("stretch").GetDouble();
        var barrel0 = await Scene.ReadOneAsync(driver, "Barrel");
        if (unwrapped.GetProperty("islands").GetArrayLength() != 3 || Scene.Islands(barrel0) != 3 || barrel0.Uvs.Count != 34)
        {
            driver.Fail($"unwrap: expected three islands and 34 mapped faces, got {unwrapped.GetProperty("islands").GetArrayLength()} islands, {Scene.Islands(barrel0)} by the seams, {barrel0.Uvs.Count} mapped");
        }

        var check = await CallJsonAsync(driver, "check", new { mesh = "Barrel" });
        var uvWarnings = check.GetProperty("warnings").EnumerateArray().Select(w => w.GetString() ?? "").Where(w => w.Contains("UV")).ToArray();
        if (uvWarnings.Length != 0) driver.Fail($"check: the unwrapped barrel has UV warnings: {string.Join("; ", uvWarnings)}");

        // 2. The crate beside it: the shared 12 calls on a mesh named Crate, one move of every vertex 3 m along +X, then box.
        await BuildAsync(driver, "Crate");
        await EditAsync(driver, "move", new { vertices = Enumerable.Range(0, 72).ToArray(), by = new[] { 3.0, 0, 0 } }, 72, 70, "Crate");
        var boxed = await CallJsonAsync(driver, "project_uvs", new { method = "box", mesh = "Crate" });
        if (boxed.GetProperty("mapped").GetArrayLength() != 70) driver.Fail($"project_uvs box mapped {boxed.GetProperty("mapped").GetArrayLength()} of the crate's 70 faces");
        var scene = await Scene.ReadAsync(driver, "Barrel", "Crate");
        var (barrel, crate) = (scene["Barrel"], scene["Crate"]);

        // 3. The barrel in the checker look: four views, a 2 x 3 sheet with face labels, and a side face highlighted.
        var frames = new Dictionary<string, ScenarioFrame>();
        var front = await CheckerAsync(driver, scene, barrel, "barrel-checker-front", "front", Front, 3, true);
        frames[front.Frame.Name] = front.Frame;
        foreach (var (name, view, direction, columns, pairs) in BarrelViews.Skip(1))
        {
            var shot = await CheckerAsync(driver, scene, barrel, $"barrel-checker-{name}", view, direction, columns, pairs);
            frames[shot.Frame.Name] = shot.Frame;
        }

        await SheetAsync(driver, scene, barrel);
        var picked = front.Samples.Where(s => s.Face < 32).GroupBy(s => s.Face).OrderByDescending(g => g.Count()).First().Key;
        var lit = await driver.CaptureAsync("barrel-checker-highlight", new { view = "front", zoom = (double)front.Cam.Zoom, target = Array3(front.Cam.Target), size = Size, mode = "checker", mesh = "Barrel", highlight = new { faces = new[] { picked } } });
        CheckCaption(driver, lit, "highlight 1 face");
        UvsChecker.Tint(driver, front.Frame, lit, front.Samples, picked);

        // 4. The barrel's layout at 256, 512 and 1024, with face labels, and the same face highlighted.
        var layout = barrel.Uvs.ToDictionary(e => e.Key, e => (IReadOnlyList<Vector2>)e.Value);
        var shape = new CrateLooks.Shape(barrel.Vertices, barrel.Faces);
        foreach (var size in new[] { 256, 512, 1024 })
        {
            var frame = await driver.CaptureAsync($"barrel-uvs-{size}", new { mode = "uvs", size, mesh = "Barrel" });
            frames[frame.Name] = frame;
            CheckCaption(driver, frame, $"{size}x{size}, uvs Barrel, 40 vertices, 34 faces");
            var pixels = UvLayoutPixels.Of(layout.Values, size);
            CrateUvLayout.Sweep(driver, frame, pixels, layout, null, false);
            CrateUvLayout.Origin(driver, frame, pixels, layout);
            Seams(driver, frame, pixels, layout, barrel, shape);
        }

        var labelled = await driver.CaptureAsync("barrel-uvs-labels", new { mode = "uvs", labels = "faces", mesh = "Barrel" });
        CheckCaption(driver, labelled, "512x512, uvs Barrel, labels faces, 40 vertices, 34 faces");
        CrateUvLayout.Sweep(driver, labelled, UvLayoutPixels.Of(layout.Values, Size), layout, null, true);
        var highlighted = await driver.CaptureAsync("barrel-uvs-highlight", new { mode = "uvs", highlight = new { faces = new[] { picked } }, mesh = "Barrel" });
        CheckCaption(driver, highlighted, "512x512, uvs Barrel, highlight 1 face, 40 vertices, 34 faces");
        var orange = CrateUvLayout.Sweep(driver, highlighted, UvLayoutPixels.Of(layout.Values, Size), layout, picked, false);
        if (orange < 4) driver.Fail($"barrel-uvs-highlight: only {orange} orange samples inside face {picked}");

        // 5. The crate: its checker front and back, and its layout.
        var crateShots = new List<ScenarioFrame>();
        foreach (var (name, view, direction) in new[] { ("front", "front", Front), ("back", "back", Back) })
        {
            var shot = await CheckerAsync(driver, scene, crate, $"crate-checker-{name}", view, direction, 3, true);
            frames[shot.Frame.Name] = shot.Frame;
        }

        var crateLayout = crate.Uvs.ToDictionary(e => e.Key, e => (IReadOnlyList<Vector2>)e.Value);
        var crateUvs = await driver.CaptureAsync("crate-uvs", new { mode = "uvs", mesh = "Crate" });
        frames[crateUvs.Name] = crateUvs;
        CheckCaption(driver, crateUvs, "512x512, uvs Crate, 72 vertices, 70 faces");
        var cratePixels = UvLayoutPixels.Of(crateLayout.Values, Size);
        CrateUvLayout.Sweep(driver, crateUvs, cratePixels, crateLayout, null, false);
        CrateUvLayout.Origin(driver, crateUvs, cratePixels, crateLayout);

        // 6. A fault made and found, twice. Each is undone to the barrel as it was, and its frame is the frame from before.
        await MovedRingAsync(driver, barrel, baseline, front.Frame);
        await ExtrudedCapAsync(driver, front.Frame);

        // 7. Save both meshes, open them again: the same meshes, the same frames. Then the barrel against the sample, and the export.
        var work = Path.Combine(driver.RunFolder, "work");
        Directory.CreateDirectory(work);
        var carve = Path.Combine(work, "uvs.carve");
        await CallTextAsync(driver, "save", new { path = carve });
        await CallJsonAsync(driver, "open", new { path = carve });
        var reopened = await Scene.ReadAsync(driver, "Barrel", "Crate");
        foreach (var (was, now) in scene.Meshes.Zip(reopened.Meshes))
        {
            if (was.Raw.GetRawText() != now.Raw.GetRawText()) driver.Fail($"{was.Name}: get_mesh after save and open differs from before");
        }

        await AgainAsync(driver, frames, "barrel-checker-front", new { view = "front", zoom = (double)front.Cam.Zoom, target = Array3(front.Cam.Target), size = Size, mode = "checker" });
        await AgainAsync(driver, frames, "barrel-uvs-512", new { mode = "uvs", size = 512, mesh = "Barrel" }, "barrel-uvs-512");
        await AgainAsync(driver, frames, "crate-uvs", new { mode = "uvs", mesh = "Crate" });
        var crateFront = await CrateFrontArgsAsync(driver, scene, crate);
        await AgainAsync(driver, frames, "crate-checker-front", crateFront);

        var sample = Path.Combine(Path.GetDirectoryName(typeof(UvsScenario).Assembly.Location) ?? AppContext.BaseDirectory, "barrel.carve");
        await CallJsonAsync(driver, "open", new { path = sample });
        var fromSample = await Scene.ReadOneAsync(driver, "mesh");
        if (fromSample.Content != reopened["Barrel"].Content) driver.Fail("the saved Barrel's vertices, faces, UVs and seams differ from samples/barrel.carve's mesh");
        await CallJsonAsync(driver, "open", new { path = carve });

        var content = Path.Combine(driver.RunFolder, "content", "models");
        Directory.CreateDirectory(content);
        var exported = await CallTextAsync(driver, "export", new { path = Path.Combine(content, "barrel.glb") });
        if (exported.Contains("warning", StringComparison.OrdinalIgnoreCase) || !exported.Contains("barrel.glb")) driver.Fail($"export: expected a clean report of barrel.glb, got: {exported}");
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);

        File.WriteAllLines(Path.Combine(driver.RunFolder, "checker-samples.txt"), UvsChecker.Log);
        File.WriteAllLines(Path.Combine(driver.RunFolder, "uv-layout-samples.txt"), CrateUvLayout.Log);
    }

    private static double[] Array3(Vector3 v) => [v.X, v.Y, v.Z];

    /// <summary>One checker frame of <paramref name="mesh"/>, zoomed on it, every sample checked and its needs met.</summary>
    private static async Task<(ScenarioFrame Frame, List<Sample> Samples, Cam Cam)> CheckerAsync(ScenarioDriver driver, Scene scene, SceneMesh mesh, string frameName, string view, Vector3 direction, int columns, bool pairs, int magenta = 0)
    {
        var cam = new Cam(view, direction, Size, UvsChecker.Fit(scene, mesh, direction, Size), (mesh.BoundsMin + mesh.BoundsMax) / 2);
        var frame = await driver.CaptureAsync(frameName, new { view, zoom = (double)cam.Zoom, target = Array3(cam.Target), size = Size, mode = "checker" });
        CheckCaption(driver, frame, $"{Size}x{Size}, view {view}");
        var samples = UvsChecker.Sweep(driver, frame, scene, mesh, cam, view);
        UvsChecker.Need(driver, frame, view, scene, mesh, cam, samples, columns, pairs, magenta);
        return (frame, samples, cam);
    }

    private static async Task<object> CrateFrontArgsAsync(ScenarioDriver driver, Scene scene, SceneMesh crate)
    {
        await Task.CompletedTask;
        var zoom = UvsChecker.Fit(scene, crate, Front, Size);
        return new { view = "front", zoom = (double)zoom, target = Array3((crate.BoundsMin + crate.BoundsMax) / 2), size = Size, mode = "checker" };
    }

    /// <summary>The 2 x 3 sheet with face labels: six views of the barrel, each tile worked as its own camera.</summary>
    private static async Task SheetAsync(ScenarioDriver driver, Scene scene, SceneMesh barrel)
    {
        (string View, Vector3 Direction)[] views = [("front", Front), ("back", Back), ("-z", -Vector3.UnitZ), ("+y", Vector3.UnitY), ("-x", -Vector3.UnitX), ("-y", -Vector3.UnitY)];
        var cams = views.Select((v, k) => new Cam(v.View, v.Direction, Tile, UvsChecker.Fit(scene, barrel, v.Direction, Tile), (barrel.BoundsMin + barrel.BoundsMax) / 2, k % 2 * (Tile + Gutter), k / 2 * (Tile + Gutter), Tile, true)).ToArray();
        var sheet = await driver.CaptureAsync("barrel-checker-sheet", new { views = cams.Select(c => c.Entry).ToArray(), columns = 2, size = Tile, mode = "checker", labels = "faces", mesh = "Barrel" });
        CheckCaption(driver, sheet, "2x3 tiles of 256x256");
        foreach (var cam in cams)
        {
            var samples = UvsChecker.Sweep(driver, sheet, scene, barrel, cam, $"sheet tile {cam.View}");
            UvsChecker.Need(driver, sheet, $"sheet tile {cam.View}", scene, barrel, cam, samples, 1, false);
        }
    }

    /// <summary>Every seam's UV segments yellow on each face it divides.</summary>
    private static void Seams(ScenarioDriver driver, ScenarioFrame frame, UvLayoutPixels pixels, Dictionary<int, IReadOnlyList<Vector2>> layout, SceneMesh barrel, CrateLooks.Shape shape)
    {
        foreach (var (a, b) in barrel.Seams)
        {
            var faces = Enumerable.Range(0, barrel.Faces.Length).Where(f => Joins(barrel.Faces[f], a, b)).ToArray();
            if (faces.Length != 2) driver.Fail($"seam {a}-{b} divides {faces.Length} faces, not two");
            CrateUvLayout.Seam(driver, frame, pixels, layout, a, b, faces[0], faces[1], shape, clearPx: 7);
        }
    }

    private static bool Joins(int[] loop, int a, int b) =>
        Enumerable.Range(0, loop.Length).Any(i => (loop[i] == a && loop[(i + 1) % loop.Length] == b) || (loop[i] == b && loop[(i + 1) % loop.Length] == a));

    /// <summary>
    /// Ring 2 moved outward a vertex at a time: the checker frame shows the old UVs stretched over the bigger faces (each sample still
    /// worked from the moved geometry, and the faces' texture density down), and unwrap's stretch rises. Then undone.
    /// </summary>
    private static async Task MovedRingAsync(ScenarioDriver driver, SceneMesh before, double baseline, ScenarioFrame frontBefore)
    {
        for (var k = 0; k < 8; k++)
        {
            await CallJsonAsync(driver, "move", new { vertices = new[] { 16 + k }, by = BarrelSpec.Outward(k, 0.15), mesh = "Barrel" });
        }

        var scene = await Scene.ReadAsync(driver, "Barrel", "Crate");
        var barrel = scene["Barrel"];
        var density = (SceneMesh m) => Enumerable.Range(8, 16).Sum(f => Geo.Area(m, f)) / Enumerable.Range(8, 16).Sum(f => UvLayoutPixels.Area(m.Uvs[f]));
        var (was, now) = (density(before), density(barrel));
        if (now < was * 1.15) driver.Fail($"the moved ring's faces carry {now:0.000} m2 per UV area, not 15% above the {was:0.000} before: the cells are not stretched");
        UvsChecker.Log.Add($"moved ring\tdensity m2 per UV unit2\tbefore {was:0.0000}\tafter {now:0.0000}\tratio {now / was:0.000}");
        var shot = await CheckerAsync(driver, scene, barrel, "barrel-moved-checker-front", "front", Front, 3, true);

        var again = await CallJsonAsync(driver, "unwrap", new { mesh = "Barrel" });
        var stretch = again.GetProperty("islands").EnumerateArray().Single(i => i.GetProperty("faces").GetInt32() == 32).GetProperty("stretch").GetDouble();
        UvsChecker.Log.Add($"moved ring\tunwrap stretch of the side\tbefore {baseline:0.0000}\tafter {stretch:0.0000}");
        if (stretch <= baseline + 0.01) driver.Fail($"unwrap's stretch of the side is {stretch:0.0000} on the moved barrel, not above {baseline:0.0000}");

        for (var undo = 0; undo < 9; undo++) await CallJsonAsync(driver, "undo", new { });
        var restored = await Scene.ReadOneAsync(driver, "Barrel");
        if (restored.Raw.GetRawText() != before.Raw.GetRawText()) driver.Fail("the barrel after unwrap and the moves were undone differs from before");
        var after = await driver.CaptureAsync("barrel-checker-front-after-moves", new { view = "front", zoom = (double)UvsChecker.Fit(await Scene.ReadAsync(driver, "Barrel", "Crate"), restored, Front, Size), target = Array3((restored.BoundsMin + restored.BoundsMax) / 2), size = Size, mode = "checker" });
        driver.AssertBytesEqual("barrel checker front, after the moves were undone", frontBefore.Pixels, after.Pixels);
        _ = shot;
    }

    /// <summary>The top cap extruded: its eight new walls are magenta, check lists them, export warns of them. Then undone.</summary>
    private static async Task ExtrudedCapAsync(ScenarioDriver driver, ScenarioFrame frontBefore)
    {
        var before = await Scene.ReadOneAsync(driver, "Barrel");
        await EditAsync(driver, "extrude", new { faces = new[] { 33 }, distance = 0.2 }, 48, 42, "Barrel");
        var scene = await Scene.ReadAsync(driver, "Barrel", "Crate");
        var barrel = scene["Barrel"];
        if (barrel.Uvs.Count != 34 || barrel.Uvs.Keys.Any(f => f >= 34)) driver.Fail($"extrude: the cap keeps its UVs and the walls have none; mapped faces are {string.Join(",", barrel.Uvs.Keys)}");
        var shot = await CheckerAsync(driver, scene, barrel, "barrel-extruded-checker-front", "front", Front, 2, true, magenta: 12);

        var check = await CallJsonAsync(driver, "check", new { mesh = "Barrel" });
        var listed = check.GetProperty("warnings").EnumerateArray().Select(w => w.GetString() ?? "").Where(w => w.Contains("no UVs")).OrderBy(w => w).ToArray();
        var expected = Enumerable.Range(34, 8).Select(f => $"faces[{f}]: no UVs").OrderBy(w => w).ToArray();
        if (listed.Length != 8 || !listed.Zip(expected).All(p => p.First.StartsWith(p.Second))) driver.Fail($"check: expected eight 'no UVs' warnings for faces 34 to 41, got: {string.Join("; ", listed)}");

        var work = Path.Combine(driver.RunFolder, "work");
        Directory.CreateDirectory(work);
        var exported = await CallTextAsync(driver, "export", new { path = Path.Combine(work, "barrel-extruded.glb") });
        if (!exported.Contains("Barrel: 8 faces have no UVs; their corners are written as (0, 0)") || exported.Contains("Crate:")) driver.Fail($"export: expected one warning, that Barrel has 8 faces without UVs, got: {exported}");

        await CallJsonAsync(driver, "undo", new { });
        var restored = await Scene.ReadOneAsync(driver, "Barrel");
        if (restored.Raw.GetRawText() != before.Raw.GetRawText()) driver.Fail("the barrel after the extrude was undone differs from before");
        var after = await driver.CaptureAsync("barrel-checker-front-after-extrude", new { view = "front", zoom = UvsChecker.Fit(await Scene.ReadAsync(driver, "Barrel", "Crate"), restored, Front, Size), target = Array3((restored.BoundsMin + restored.BoundsMax) / 2), size = Size, mode = "checker" });
        driver.AssertBytesEqual("barrel checker front, after the extrude was undone", frontBefore.Pixels, after.Pixels);
    }

    private static async Task AgainAsync(ScenarioDriver driver, Dictionary<string, ScenarioFrame> frames, string name, object args, string? key = null)
    {
        var first = frames[key ?? name];
        var again = await driver.CaptureAsync($"{name}-reopened", args);
        driver.AssertBytesEqual($"{name}, after save and open", first.Pixels, again.Pixels);
    }

    private static void CheckCaption(ScenarioDriver driver, ScenarioFrame frame, string part)
    {
        if (frame.Caption?.Contains(part) != true) driver.Fail($"{frame.Name}: the caption is '{frame.Caption}', expected it to contain '{part}'");
    }
}
