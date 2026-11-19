using System.Numerics;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Materials;
using Carve.Scenario.Uvs;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Planks;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class PlanksExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, PlanksScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>The barrel's materials, their faces and their textures: M-7's four, the staves and the rune stave bound to the planks graph's PNGs.</summary>
internal static class PlanksSpec
{
    /// <summary>Files copied into the run folder, relative to it: the planks' four maps, and M-7's for iron, end grain and the rune.</summary>
    public static readonly string[] Files =
    [
        "graphs/planks/base_colour.png", "graphs/planks/normal.png", "graphs/planks/occlusion.png", "graphs/planks/roughness.png",
        "textures/iron-base.png", "textures/iron-mr.png", "textures/end-grain-base.png", "textures/end-grain-normal.png", "textures/rune-emissive.png",
        "textures/staves-base.png",
    ];

    /// <summary>The break table's switch: <c>t77-base</c> (T-77's staves for the planks' base colour), <c>roughness-base</c> (the roughness map as base colour) or <c>mirror</c> (u mirrored on the side) make the scenario wrong on purpose, and it must then fail.</summary>
    public static readonly string? Break = Environment.GetEnvironmentVariable("CARVE_PLANKS_BREAK") is { Length: > 0 } b ? b : null;

    public static (string Name, object Args)[] Materials(string root)
    {
        string T(string file) => Path.Combine(root, file);
        var baseColour = Break switch { "t77-base" => T("textures/staves-base.png"), "roughness-base" => T("graphs/planks/roughness.png"), _ => T("graphs/planks/base_colour.png") };
        return
        [
            ("staves", new { name = "staves", base_colour_texture = baseColour, normal_texture = T("graphs/planks/normal.png"), occlusion_texture = T("graphs/planks/occlusion.png"), metallic_roughness_texture = T("graphs/planks/roughness.png") }),
            ("iron", new { name = "iron", base_colour_texture = T("textures/iron-base.png"), metallic_roughness_texture = T("textures/iron-mr.png"), metallic = 1.0, roughness = 1.0 }),
            ("end-grain", new { name = "end-grain", base_colour_texture = T("textures/end-grain-base.png"), normal_texture = T("textures/end-grain-normal.png") }),
            ("rune-stave", new { name = "rune-stave", base_colour_texture = baseColour, normal_texture = T("graphs/planks/normal.png"), occlusion_texture = T("graphs/planks/occlusion.png"), metallic_roughness_texture = T("graphs/planks/roughness.png"), emissive_texture = T("textures/rune-emissive.png"), emissive = "#FF8020" }),
        ];
    }

    public static readonly int[] Hoops = [.. Enumerable.Range(0, 8), .. Enumerable.Range(24, 8)];
    public static readonly int[] Rune = [14, 22];
    public static readonly int[] Staves = [.. Enumerable.Range(8, 16).Where(f => !Rune.Contains(f))];
    public static readonly int[] Caps = [32, 33];

    public static (string Material, int[] Faces)[] Assignments => [("iron", Hoops), ("rune-stave", Rune), ("staves", Staves), ("end-grain", Caps)];
}

/// <summary>
/// T-116 through the real tools: M-7's barrel given UVs and four materials, the staves' maps made by the planks graph, seen in five frames with
/// every pixel worked from the geometry, the UVs and the texels, then saved as <c>barrel-planks.carve</c>, reopened and exported.
/// </summary>
public sealed class PlanksScenario : IScenario
{
    private static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f), Back = -Front;
    private const int Size = 512, Tile = 256, Gutter = 2;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var here = Path.GetDirectoryName(typeof(PlanksScenario).Assembly.Location) ?? AppContext.BaseDirectory;
        if (PlankTexels.GapProblem() is { } problem) driver.Fail($"the planks' base colour: {problem}");
        foreach (var file in PlanksSpec.Files)
        {
            var to = Path.Combine(driver.RunFolder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(Path.Combine(here, file), to, true);
        }

        // 1. The barrel, its UVs and its materials, as M-7 gives them.
        await CallJsonAsync(driver, "open", new { path = Path.Combine(here, "barrel.carve") });
        await CallJsonAsync(driver, "project_uvs", new { method = "cylindrical", faces = Enumerable.Range(0, 32).ToArray(), into = new[] { 0, 0, 1, 1 } });
        await CallJsonAsync(driver, "project_uvs", new { method = "planar", view = "+y", faces = new[] { 33 }, into = new[] { 0f, 0f, 0.5f, 1f } });
        await CallJsonAsync(driver, "project_uvs", new { method = "planar", view = "-y", faces = new[] { 32 }, into = new[] { 0.5f, 0f, 1f, 1f } });
        foreach (var (name, args) in PlanksSpec.Materials(driver.RunFolder))
        {
            var defined = await CallJsonAsync(driver, "define_material", args);
            if (defined.GetProperty("warnings").GetArrayLength() != 0) driver.Fail($"define_material {name}: warnings {defined.GetProperty("warnings")}");
        }

        foreach (var (material, assigned) in PlanksSpec.Assignments)
        {
            var set = await CallJsonAsync(driver, "set_material", new { material, faces = assigned });
            if (set.TryGetProperty("warnings", out var w) && w.GetArrayLength() != 0) driver.Fail($"set_material {material}: warnings {w}");
        }

        var check = await CallJsonAsync(driver, "check", new { });
        if (check.GetProperty("warnings").GetArrayLength() != 0) driver.Fail($"check: the barrel has warnings: {check.GetProperty("warnings")}");

        var barrel = await Scene.ReadOneAsync(driver, "mesh");
        var materials = FaceMaterials(driver, barrel);
        if (barrel.Faces.Length != 34 || materials.Count != 34) driver.Fail($"expected 34 faces, all with a material; got {barrel.Faces.Length} faces and {materials.Count} with a material");
        // The mirror break: the checks predict from u mirrored on the side (1 - u on the 32 cylindrical faces), as if project_uvs had
        // written it the other way round; the frames are drawn as the tools made them.
        Vector2[]? Predicted(int f, Vector2[] uv) => PlanksSpec.Break == "mirror" && f < 32 ? [.. uv.Select(t => new Vector2(1 - t.X, t.Y))] : uv;
        var faces = Enumerable.Range(0, barrel.Faces.Length).Select(f => new FaceGeo(f, barrel.Faces[f].Select(v => barrel.Vertices[v]).ToArray(), barrel.Uvs.TryGetValue(f, out var uv) ? Predicted(f, uv) : null)).ToArray();
        var (min, max) = (barrel.BoundsMin, barrel.BoundsMax);
        var none = new HashSet<int>();
        var frames = new Dictionary<string, ScenarioFrame>();

        // 2. Front, back and top.
        var front = frames["planks-front"] = await driver.CaptureAsync("planks-front", new { view = "front", size = Size, mode = "shaded" });
        var frontPool = new Pool(driver, front, new Shot(faces, min, max, Front, Size), "front", materials, none);
        frontPool.RuneGlows();
        frontPool.IronAndRust();
        frontPool.Wood();
        PlanksChecks.Tone(driver, frontPool, Front, "front");

        var back = frames["planks-back"] = await driver.CaptureAsync("planks-back", new { view = "back", size = Size, mode = "shaded" });
        var backPool = new Pool(driver, back, new Shot(faces, min, max, Back, Size), "back", materials, none);
        backPool.NoRune();
        backPool.IronAndRust();
        backPool.Wood();
        PlanksChecks.Tone(driver, backPool, Back, "back");

        var top = frames["planks-top"] = await driver.CaptureAsync("planks-top", new { view = "+y", size = Size, mode = "shaded" });
        new Pool(driver, top, new Shot(faces, min, max, Vector3.UnitY, Size), "top", materials, none).Rings(33);

        // 3. Close on the boundary of two staves facing the front most, head on, so the gap is wide enough for a P2 footprint.
        var (target, direction) = GapView(faces, min, max);
        var gap = frames["planks-gap"] = await driver.CaptureAsync("planks-gap", new { direction = new[] { direction.X, direction.Y, direction.Z }, zoom = 4.0, target = new[] { target.X, target.Y, target.Z }, size = Size, mode = "shaded" });
        var gapShot = new Shot(faces, min, max, direction, Size, 4, target);
        var gapPool = new Pool(driver, gap, gapShot, "gap close-up", materials, none);
        gapPool.Wood();
        PlanksChecks.Gap(driver, gap, gapShot, gapPool, materials, "gap close-up");
        PlanksChecks.Tone(driver, gapPool, direction, "gap close-up");

        // 4. The 2 x 2 sheet, each tile its own camera.
        (string View, Vector3 Direction)[] views = [("front", Front), ("back", Back), ("+y", Vector3.UnitY), ("-x", -Vector3.UnitX)];
        var sheet = frames["planks-sheet"] = await driver.CaptureAsync("planks-sheet", new { views = views.Select(v => v.View).ToArray(), columns = 2, size = Tile, mode = "shaded" });
        for (var k = 0; k < views.Length; k++)
        {
            var shot = new Shot(faces, min, max, views[k].Direction, Tile, 1, null, k % 2 * (Tile + Gutter), k / 2 * (Tile + Gutter));
            var tile = new Pool(driver, sheet, shot, $"sheet tile {views[k].View}", materials, none);
            switch (views[k].View)
            {
                case "front": tile.RuneGlows(3); tile.IronAndRust(); break;
                case "back": tile.NoRune(); tile.IronAndRust(); break;
                case "+y": tile.Rings(33); break;
                default: tile.Wood(); tile.IronAndRust(); break;
            }
        }

        // 5. Save, reopen: the same mesh and the same frames. The saved file is the sample.
        var carve = Path.Combine(driver.RunFolder, "barrel-planks.carve");
        await CallTextAsync(driver, "save", new { path = carve });
        await CallJsonAsync(driver, "open", new { path = carve });
        var reopened = await Scene.ReadOneAsync(driver, "mesh");
        if (reopened.Raw.GetRawText() != barrel.Raw.GetRawText()) driver.Fail("get_mesh after save and open differs from before");
        foreach (var (name, args) in new (string, object)[] { ("planks-front", new { view = "front", size = Size, mode = "shaded" }), ("planks-gap", new { direction = new[] { direction.X, direction.Y, direction.Z }, zoom = 4.0, target = new[] { target.X, target.Y, target.Z }, size = Size, mode = "shaded" }) })
        {
            var again = await driver.CaptureAsync($"{name}-reopened", args);
            driver.AssertBytesEqual($"{name}, after save and open", frames[name].Pixels, again.Pixels);
        }

        // 6. Export.
        var content = Path.Combine(driver.RunFolder, "game", "Content", "models");
        Directory.CreateDirectory(content);
        var exported = await CallTextAsync(driver, "export", new { path = Path.Combine(content, "barrel-planks.glb") });
        if (exported.Contains("warning", StringComparison.OrdinalIgnoreCase) || !exported.Contains("barrel-planks.glb")) driver.Fail($"export: expected a clean report of barrel-planks.glb, got: {exported}");
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);
        File.WriteAllLines(Path.Combine(driver.RunFolder, "pixel-samples.txt"), Pool.Log);
    }

    /// <summary>
    /// The shared edge of two neighbouring stave columns (faces 8 + k and 9 + k, none of them the rune's) that faces the front camera most, and
    /// the direction to look at it head on (outward from the barrel's axis, a little above).
    /// </summary>
    private static (Vector3 Target, Vector3 Direction) GapView(FaceGeo[] faces, Vector3 min, Vector3 max)
    {
        var axis = new Vector2((min.X + max.X) / 2, (min.Z + max.Z) / 2);
        var toFront = Vector2.Normalize(new Vector2(Front.X, Front.Z));
        (Vector3 Target, Vector3 Direction, float Score)? best = null;
        foreach (var k in Enumerable.Range(0, 7).Where(k => !PlanksSpec.Rune.Contains(8 + k) && !PlanksSpec.Rune.Contains(9 + k)))
        {
            var shared = faces[8 + k].Loop.Where(v => faces[9 + k].Loop.Any(w => Vector3.Distance(v, w) < 1e-5f)).ToArray();
            if (shared.Length != 2) continue;
            var mid = (shared[0] + shared[1]) / 2;
            var outward = Vector2.Normalize(new Vector2(mid.X, mid.Z) - axis);
            var score = Vector2.Dot(outward, toFront);
            if (best is null || score > best.Value.Score) best = (mid, Vector3.Normalize(new Vector3(outward.X, 0.25f, outward.Y)), score);
        }

        return best is { } b ? (b.Target, b.Direction) : throw new InvalidOperationException("no pair of neighbouring stave faces shares an edge");
    }

    private static Dictionary<int, string> FaceMaterials(ScenarioDriver driver, SceneMesh mesh)
    {
        var result = new Dictionary<int, string>();
        if (!mesh.Raw.TryGetProperty("face_materials", out var list)) driver.Fail("get_mesh reports no face_materials");
        foreach (var entry in list.EnumerateArray())
        {
            foreach (var f in entry.GetProperty("faces").EnumerateArray()) result[f.GetInt32()] = entry.GetProperty("material").GetString()!;
        }

        foreach (var (material, assigned) in PlanksSpec.Assignments)
        {
            foreach (var f in assigned)
            {
                if (result.GetValueOrDefault(f) != material) driver.Fail($"get_mesh: face {f} has material '{result.GetValueOrDefault(f)}', expected '{material}'");
            }
        }

        return result;
    }
}
