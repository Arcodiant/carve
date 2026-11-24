using Carve.Scenario.Uvs;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Weathered;

/// <summary>The barrel's faces as <c>samples/barrel.carve</c> numbers them, and what stage 1 makes of them.</summary>
internal static class Barrel
{
    public static readonly int[] Hoops = [.. Enumerable.Range(0, 8), .. Enumerable.Range(24, 8)];
    public static readonly int[] Staves = [.. Enumerable.Range(8, 16)];
    public static readonly int[] Caps = [32, 33];
    public const float Raise = 0.015f;

    /// <summary>
    /// The staves' band of the square: projected as M-7 does, so each of the eight facets is one plank (u = k/8 to (k+1)/8). The projection
    /// keeps the staves' aspect (v spans 0.324 of the square at full width), so the band is just that tall and the iron gets the rest.
    /// </summary>
    public static readonly float[] StaveBand = [0, 0, 1, 0.34f];

    /// <summary>The rest (hoop tops, their walls, the caps) unwrapped below the band.</summary>
    public static readonly float[] RestBand = [0, 0.34f, 1, 1];

    /// <summary>Every face but the staves, once extrude has added the hoops' walls.</summary>
    public static int[] Rest(int faceCount) => [.. Enumerable.Range(0, faceCount).Where(f => !Staves.Contains(f))];

    public static string Graph(WeatheredRun run, string name) => run.InRun(Path.Combine("graphs", name + ".texgraph"));

    public static (string Name, object Args)[] Materials(WeatheredRun run)
    {
        object G(string graph, string output) => new { graph = Graph(run, graph), output, size = WeatheredRun.BakeSize };
        string T(string file) => run.InRun(Path.Combine("textures", file));
        return
        [
            ("weathered-iron", new { name = "weathered-iron", base_colour_texture = G("weathered-iron", "base_colour"), metallic_roughness_texture = G("weathered-iron", "roughness"), metallic = 1.0, roughness = 1.0 }),
            ("weathered-staves", new { name = "weathered-staves", base_colour_texture = G("weathered-staves", "base_colour"), metallic_roughness_texture = G("weathered-staves", "roughness"), normal_texture = G("weathered-staves", "normal"), occlusion_texture = G("weathered-staves", "occlusion") }),
            ("end-grain", new { name = "end-grain", base_colour_texture = T("end-grain-base.png"), normal_texture = T("end-grain-normal.png") }),
        ];
    }
}

/// <summary>Stage 1: <c>samples/barrel.carve</c> opened, its hoops raised, the UVs of the research's layout, the groups and the three materials, <c>check</c> silent.</summary>
internal sealed class BuildStage : IWeatheredStage
{
    public async Task RunAsync(WeatheredRun run)
    {
        var driver = run.Driver;
        foreach (var file in new[] { "graphs/weathered-iron.texgraph", "graphs/weathered-staves.texgraph", "graphs/planks.texgraph", "textures/end-grain-base.png", "textures/end-grain-normal.png" })
        {
            var to = run.InRun(file);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(Path.Combine(run.Here, file), to, true);
        }

        await CallJsonAsync(driver, "open", new { path = Path.Combine(run.Here, "barrel.carve") });
        await CallJsonAsync(driver, "extrude", new { faces = Barrel.Hoops, distance = Barrel.Raise });
        var faceCount = (await Scene.ReadOneAsync(driver, "mesh")).Faces.Length;
        await CallJsonAsync(driver, "project_uvs", new { method = "cylindrical", faces = Barrel.Staves, into = Barrel.StaveBand });
        await CallJsonAsync(driver, "unwrap", new { faces = Barrel.Rest(faceCount), into = Barrel.RestBand });
        await CallJsonAsync(driver, "define_group", new { name = "hoops", faces = Barrel.Rest(faceCount).Where(f => !Barrel.Caps.Contains(f)).ToArray() });
        await CallJsonAsync(driver, "define_group", new { name = "staves", faces = Barrel.Staves });

        foreach (var (name, args) in Barrel.Materials(run))
        {
            var defined = await CallJsonAsync(driver, "define_material", args);
            // A graph that reads the mesh is reported as such (it bakes per mesh); anything else is a fault of the build.
            var others = defined.TryGetProperty("warnings", out var w) ? w.EnumerateArray().Select(e => e.GetString()!).Where(e => !e.Contains("the graph reads the mesh, so it is baked onto each mesh", StringComparison.Ordinal)).ToArray() : [];
            if (others.Length != 0) run.Note($"define_material {name}: warnings {string.Join("; ", others)}");
        }

        var iron = Barrel.Rest(faceCount).Where(f => !Barrel.Caps.Contains(f)).ToArray();
        foreach (var (material, faces) in new[] { ("weathered-iron", iron), ("weathered-staves", Barrel.Staves), ("end-grain", Barrel.Caps) })
        {
            var set = await CallJsonAsync(driver, "set_material", new { material, faces });
            if (set.TryGetProperty("warnings", out var w) && w.GetArrayLength() != 0) run.Note($"set_material {material}: warnings {w}");
        }
        var check = await CallJsonAsync(driver, "check", new { });
        File.WriteAllText(run.InRun("build-check.json"), check.GetRawText());
        if (check.GetProperty("warnings").GetArrayLength() != 0) run.Note($"check: the weathered barrel has warnings: {check.GetProperty("warnings")}");
    }
}
