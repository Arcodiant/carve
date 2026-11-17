using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Crate;
using Carve.Scenario.Uvs;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Materials;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class MaterialsExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, MaterialsScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>The barrel's materials, their faces and their UVs: the one source for the scenario's calls.</summary>
internal static class MaterialsSpec
{
    public static readonly string[] Textures =
        ["staves-base.png", "staves-normal.png", "staves-occlusion.png", "iron-base.png", "iron-mr.png", "end-grain-base.png", "end-grain-normal.png", "rune-emissive.png"];

    public static readonly int[] Hoops = [.. Enumerable.Range(0, 8), .. Enumerable.Range(24, 8)];
    /// <summary>The break table's switch (T-80): <c>vflip</c>, <c>rune-column</c>, <c>metallic</c> or <c>mirror</c> make the scenario wrong on purpose, and it must then fail; unset it is the scenario.</summary>
    public static readonly string? Break = Environment.GetEnvironmentVariable("CARVE_MATERIALS_BREAK") is { Length: > 0 } b ? b : null;

    public static readonly int[] Rune = Break == "rune-column" ? [13, 21] : [14, 22];
    public static readonly int[] Coloured = [15, 23];
    public static readonly int[] Staves = [.. Enumerable.Range(8, 16).Where(f => !Rune.Contains(f))];
    public static readonly int[] Caps = [32, 33];

    /// <summary>The four <c>define_material</c> calls, texture paths in <paramref name="textures"/>.</summary>
    public static (string Name, object Args)[] Materials(string textures)
    {
        string T(string file) => Path.Combine(textures, file);
        return
        [
            ("staves", Break == "metallic"
                ? new { name = "staves", base_colour_texture = T("staves-base.png"), normal_texture = T("staves-normal.png"), occlusion_texture = T("staves-occlusion.png"), metallic = (double?)1.0 }
                : new { name = "staves", base_colour_texture = T("staves-base.png"), normal_texture = T("staves-normal.png"), occlusion_texture = T("staves-occlusion.png"), metallic = (double?)null }),
            ("iron", new { name = "iron", base_colour_texture = T("iron-base.png"), metallic_roughness_texture = T("iron-mr.png"), metallic = 1.0, roughness = 1.0 }),
            ("end-grain", new { name = "end-grain", base_colour_texture = T("end-grain-base.png"), normal_texture = T("end-grain-normal.png") }),
            ("rune-stave", new { name = "rune-stave", base_colour_texture = T("staves-base.png"), normal_texture = T("staves-normal.png"), occlusion_texture = T("staves-occlusion.png"), emissive_texture = T("rune-emissive.png"), emissive = "#FF8020" }),
        ];
    }

    public static (string Material, int[] Faces)[] Assignments => [("iron", Hoops), ("rune-stave", Rune), ("staves", Staves), ("end-grain", Caps)];
}

/// <summary>
/// M-7 through the real tools: the barrel (M-6's sample) given UVs, four materials and their textures, seen in nine frames with every pixel worked
/// from the geometry, the UVs and the textures, then saved, reopened and exported for the viewer.
/// </summary>
public sealed class MaterialsScenario : IScenario
{
    private static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f), Back = -Front;
    private const int Size = 512, Tile = 256, Gutter = 2;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var here = Path.GetDirectoryName(typeof(MaterialsScenario).Assembly.Location) ?? AppContext.BaseDirectory;
        var textures = Path.Combine(driver.RunFolder, "textures");
        Directory.CreateDirectory(textures);
        foreach (var file in MaterialsSpec.Textures) File.Copy(Path.Combine(here, "textures", file), Path.Combine(textures, file), true);
        if (MaterialsSpec.Break == "vflip") FlipV(textures, "rune-emissive.png", "iron-mr.png");

        // 1. The barrel, its UVs and its materials.
        await CallJsonAsync(driver, "open", new { path = Path.Combine(here, "barrel.carve") });
        await CallJsonAsync(driver, "project_uvs", new { method = "cylindrical", faces = Enumerable.Range(0, 32).ToArray(), into = new[] { 0, 0, 1, 1 } });
        await CallJsonAsync(driver, "project_uvs", new { method = "planar", view = "+y", faces = new[] { 33 }, into = new[] { 0f, 0f, 0.5f, 1f } });
        await CallJsonAsync(driver, "project_uvs", new { method = "planar", view = "-y", faces = new[] { 32 }, into = new[] { 0.5f, 0f, 1f, 1f } });
        foreach (var (name, args) in MaterialsSpec.Materials(textures))
        {
            var defined = await CallJsonAsync(driver, "define_material", args);
            if (defined.GetProperty("warnings").GetArrayLength() != 0) driver.Fail($"define_material {name}: warnings {defined.GetProperty("warnings")}");
        }

        foreach (var (material, assigned) in MaterialsSpec.Assignments)
        {
            var set = await CallJsonAsync(driver, "set_material", new { material, faces = assigned });
            if (set.TryGetProperty("warnings", out var w) && w.GetArrayLength() != 0) driver.Fail($"set_material {material}: warnings {w}");
        }

        var check = await CallJsonAsync(driver, "check", new { });
        if (check.GetProperty("warnings").GetArrayLength() != 0) driver.Fail($"check: the textured barrel has warnings: {check.GetProperty("warnings")}");

        var barrel = await Scene.ReadOneAsync(driver, "mesh");
        var materials = FaceMaterials(driver, barrel);
        if (barrel.Faces.Length != 34 || materials.Count != 34) driver.Fail($"expected 34 faces, all with a material; got {barrel.Faces.Length} faces and {materials.Count} with a material");
        var faces = Enumerable.Range(0, barrel.Faces.Length).Select(f => new FaceGeo(f, barrel.Faces[f].Select(v => barrel.Vertices[v]).ToArray(), barrel.Uvs.TryGetValue(f, out var uv) ? uv : null)).ToArray();
        var (min, max) = (barrel.BoundsMin, barrel.BoundsMax);
        var none = new HashSet<int>();
        var frames = new Dictionary<string, ScenarioFrame>();

        // The UV rectangle of the rune and the cap's centre, from get_mesh, against the generator's.
        CheckUvs(driver, barrel);

        // 2. Frames 1 to 3: front, back, top.
        var front = await CaptureAsync(driver, frames, "barrel-front", new { view = "front", size = Size, mode = "shaded" });
        var frontShot = new Shot(faces, min, max, MaterialsSpec.Break == "mirror" ? new Vector3(-Front.X, Front.Y, Front.Z) : Front, Size);
        var frontPool = new Pool(driver, front, frontShot, "front", materials, none);
        frontPool.RuneGlows();
        frontPool.IronAndRust();
        frontPool.Wood();

        var back = await CaptureAsync(driver, frames, "barrel-back", new { view = "back", size = Size, mode = "shaded" });
        var backShot = new Shot(faces, min, max, Back, Size);
        foreach (var f in MaterialsSpec.Rune)
        {
            if (Vector3.Dot(faces[f].Normal, backShot.Eye - faces[f].Centre) > 0) driver.Fail($"face {f} (the rune's) faces the eye of back");
        }

        var backPool = new Pool(driver, back, backShot, "back", materials, none);
        backPool.NoRune();
        backPool.IronAndRust();
        backPool.Wood();

        var top = await CaptureAsync(driver, frames, "barrel-top", new { view = "+y", size = Size, mode = "shaded" });
        var topPool = new Pool(driver, top, new Shot(faces, min, max, Vector3.UnitY, Size), "top", materials, none);
        topPool.Rings(33);

        // The bottom cap (face 32) is in sight in back and the sheet's back tile, but there a pixel spans 1.5 to 2.4 texels and the rings' light band
        // is 5 wide (period 9): no footprint is all one class (P2). Straight under, as top is straight over, a pixel spans under 1, so it resolves.
        var bottom = await CaptureAsync(driver, frames, "barrel-bottom", new { view = "-y", size = Size, mode = "shaded" });
        var bottomPool = new Pool(driver, bottom, new Shot(faces, min, max, -Vector3.UnitY, Size), "bottom", materials, none);
        bottomPool.Rings(32);

        // 3. Frame 4: close on the top hoop (face 30, the one most head-on to front).
        var target = faces[30].Centre;
        var hoop = await CaptureAsync(driver, frames, "barrel-hoop-close", new { view = "front", zoom = 3.0, target = new[] { target.X, target.Y, target.Z }, size = Size, mode = "shaded" });
        var hoopPool = new Pool(driver, hoop, new Shot(faces, min, max, Front, Size, 3, target), "hoop-close", materials, none);
        hoopPool.IronAndRust();
        hoopPool.Wood();

        // 4. Frame 5: the 2 x 2 sheet, each tile its own camera.
        (string View, Vector3 Direction)[] views = [("front", Front), ("back", Back), ("+y", Vector3.UnitY), ("-x", -Vector3.UnitX)];
        var sheet = await CaptureAsync(driver, frames, "barrel-sheet", new { views = views.Select(v => v.View).ToArray(), columns = 2, size = Tile, mode = "shaded" });
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

        // 5. Frame 6: the front at 256 and 1024.
        foreach (var size in new[] { 256, 1024 })
        {
            var frame = await CaptureAsync(driver, frames, $"barrel-front-{size}", new { view = "front", size, mode = "shaded" });
            var pool = new Pool(driver, frame, new Shot(faces, min, max, Front, size), $"front {size}", materials, none);
            pool.RuneGlows(size < 512 ? 3 : 8);
            pool.IronAndRust();
            pool.Wood();
        }

        // 6. Frame 7: two faces of column 7 coloured beside the material faces; locality, then undone.
        await CallJsonAsync(driver, "set_colour", new { faces = MaterialsSpec.Coloured, colour = "#C8503C" });
        var mixed = await CaptureAsync(driver, frames, "barrel-mixed", new { view = "front", size = Size, mode = "shaded" });
        var mixedPool = new Pool(driver, mixed, frontShot, "mixed", materials.Where(e => !MaterialsSpec.Coloured.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value), MaterialsSpec.Coloured.ToHashSet());
        mixedPool.Coloured(MaterialsSpec.Coloured);
        mixedPool.RuneGlows();
        foreach (var s in mixedPool.All.Where(s => s.Look != Look.Colour))
        {
            var (a, b) = (frontPool.Pixel(s), mixedPool.Pixel(s));
            if (a != b) driver.Fail($"barrel-mixed: material face {s.Hit.Face.Face} at ({s.Hit.X}, {s.Hit.Y}) is {b}, but {a} in barrel-front: only other faces changed");
        }

        await CallJsonAsync(driver, "set_colour", new { faces = MaterialsSpec.Rune, colour = "#2060C0" });
        var changed = await CaptureAsync(driver, frames, "barrel-mixed-other", new { view = "front", size = Size, mode = "shaded" });
        var changedPool = new Pool(driver, changed, frontShot, "mixed, rune faces recoloured", materials.Where(e => !MaterialsSpec.Coloured.Contains(e.Key) && !MaterialsSpec.Rune.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value), MaterialsSpec.Coloured.ToHashSet());
        var same = 0;
        foreach (var s in mixedPool.Of(Look.Colour))
        {
            if (mixedPool.Pixel(s) != changedPool.Pixel(s)) driver.Fail($"barrel-mixed: colour face {s.Hit.Face.Face} at ({s.Hit.X}, {s.Hit.Y}) changed when only the rune faces were recoloured");
            same++;
        }

        if (same < 8) driver.Fail($"only {same} colour samples compared");
        await CallJsonAsync(driver, "undo", new { });
        await CallJsonAsync(driver, "undo", new { });
        driver.AssertBytesEqual("barrel-front, after set_colour and undo", front.Pixels, (await driver.CaptureAsync("barrel-front-undone", new { view = "front", size = Size, mode = "shaded" })).Pixels);

        // 7. Save, reopen: the same mesh and the same frames. The saved file is the sample.
        var carve = Path.Combine(driver.RunFolder, "barrel-textured.carve");
        await CallTextAsync(driver, "save", new { path = carve });
        await CallJsonAsync(driver, "open", new { path = carve });
        var reopened = await Scene.ReadOneAsync(driver, "mesh");
        if (reopened.Raw.GetRawText() != barrel.Raw.GetRawText()) driver.Fail("get_mesh after save and open differs from before");
        foreach (var (name, args) in new (string, object)[] { ("barrel-front", new { view = "front", size = Size, mode = "shaded" }), ("barrel-top", new { view = "+y", size = Size, mode = "shaded" }) })
        {
            var again = await driver.CaptureAsync($"{name}-reopened", args);
            driver.AssertBytesEqual($"{name}, after save and open", frames[name].Pixels, again.Pixels);
        }

        // 8. Export for the viewer, and the samples its frames are checked on.
        var content = Path.Combine(driver.RunFolder, "game", "Content", "models");
        Directory.CreateDirectory(content);
        var exported = await CallTextAsync(driver, "export", new { path = Path.Combine(content, "barrel.glb") });
        if (exported.Contains("warning", StringComparison.OrdinalIgnoreCase) || !exported.Contains("barrel.glb")) driver.Fail($"export: expected a clean report of barrel.glb, got: {exported}");
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);
        WriteViewerSamples(driver, faces, min, max, materials);
        File.WriteAllLines(Path.Combine(driver.RunFolder, "pixel-samples.txt"), Pool.Log);
    }

    /// <summary>The break table's "uploaded v-flipped": these textures are rewritten with their rows reversed.</summary>
    private static void FlipV(string folder, params string[] files)
    {
        var all = Carve.SampleTextures.TextureSet.Build().ToDictionary(t => t.Name, t => t.Rgba);
        foreach (var file in files)
        {
            var (rgba, n) = (all[file], Carve.SampleTextures.TextureSet.Size);
            var flipped = new byte[rgba.Length];
            for (var y = 0; y < n; y++) Array.Copy(rgba, y * n * 4, flipped, (n - 1 - y) * n * 4, n * 4);
            File.WriteAllBytes(Path.Combine(folder, file), Carve.SampleTextures.Png.Encode(flipped, n, n));
        }
    }

    private static async Task<ScenarioFrame> CaptureAsync(ScenarioDriver driver, Dictionary<string, ScenarioFrame> frames, string name, object args) => frames[name] = await driver.CaptureAsync(name, args);

    private static Dictionary<int, string> FaceMaterials(ScenarioDriver driver, SceneMesh mesh)
    {
        var result = new Dictionary<int, string>();
        if (!mesh.Raw.TryGetProperty("face_materials", out var list)) driver.Fail("get_mesh reports no face_materials");
        foreach (var entry in list.EnumerateArray())
        {
            foreach (var f in entry.GetProperty("faces").EnumerateArray()) result[f.GetInt32()] = entry.GetProperty("material").GetString()!;
        }

        foreach (var (material, faces) in MaterialsSpec.Assignments)
        {
            foreach (var f in faces)
            {
                if (result.GetValueOrDefault(f) != material) driver.Fail($"get_mesh: face {f} has material '{result.GetValueOrDefault(f)}', expected '{material}'");
            }
        }

        return result;
    }

    /// <summary>The rune faces' UV rectangle and the cap's centre, as get_mesh gives them, against the textures' layout.</summary>
    private static void CheckUvs(ScenarioDriver driver, SceneMesh barrel)
    {
        var (u0, v0, u1, v1) = Carve.SampleTextures.TextureSet.RuneRect;
        var uv = MaterialsSpec.Rune.SelectMany(f => barrel.Uvs[f]).ToArray();
        float[] got = [uv.Min(p => p.X), uv.Min(p => p.Y), uv.Max(p => p.X), uv.Max(p => p.Y)];
        double[] want = [u0, v0, u1, v1];
        if (got.Zip(want).Any(p => Math.Abs(p.First - p.Second) > 0.0002)) driver.Fail($"the rune faces' UV rectangle is [{string.Join(", ", got)}], the rune texture's is [{string.Join(", ", want)}]");
        foreach (var (face, want128) in new[] { (33, new Vector2(64, 128)), (32, new Vector2(192, 128)) })
        {
            var centre = barrel.Uvs[face].Aggregate(Vector2.Zero, (s, p) => s + p) / barrel.Uvs[face].Length * Shot.TextureSize;
            if (Vector2.Distance(centre, want128) > 1) driver.Fail($"face {face}'s centre has UV texel {centre}, not its disc's centre {want128}");
        }
    }

    /// <summary>
    /// The P2 samples of the viewer's two frames (512 x 512, the same lens and framing as render, +Y up), by class: where the checks of
    /// <c>Carve.MaterialsLoad.Tests</c> read the viewer's pixels.
    /// </summary>
    private static void WriteViewerSamples(ScenarioDriver driver, FaceGeo[] faces, Vector3 min, Vector3 max, Dictionary<int, string> materials)
    {
        var lines = new List<string>();
        foreach (var (view, direction) in new[] { ("viewer-front", new Vector3(0, 0.5f, 2.5f)), ("viewer-angle", new Vector3(1.6f, 1.2f, 2.2f)) })
        {
            var shot = new Shot(faces, min, max, direction, Size);
            foreach (var hit in shot.Sites(0, 0))
            {
                if (!materials.TryGetValue(hit.Face.Face, out var name) || Texels.For(name) is not { } classify || Shot.Class(hit.Texel, hit.Half, classify) is not { } look) continue;
                var (flipped, mirrored) = Pool.Flips(hit, look, classify);
                lines.Add($"{view}\t{hit.X}\t{hit.Y}\t{look}\t{hit.Face.Face}\t{flipped}\t{mirrored}");
            }
        }

        File.WriteAllLines(Path.Combine(driver.RunFolder, "viewer-samples.tsv"), lines);
    }
}
