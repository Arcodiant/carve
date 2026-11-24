using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Orcward;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Weathered;

/// <summary>
/// Stage 5: saved as <c>barrel-weathered.carve</c> in the run folder (the sample <c>samples/barrel-weathered.carve</c>, which the load tests hold it
/// to: version 10, every graph path <c>graphs/…</c>), reopened with <c>get_mesh</c> the same and stage 2's 512 frames byte-identical, exported
/// into <c>game/Content/models/</c>, and <c>render_glb</c> of it against <c>render</c> front and three-quarter under D-6's rule, with the
/// control that must fail it: the three-quarter <c>render_glb</c> against the front.
/// </summary>
internal sealed class SavedStage : IWeatheredStage
{
    public async Task RunAsync(WeatheredRun run)
    {
        var driver = run.Driver;
        var before = await Scene.ReadOneAsync(driver, "mesh");
        var carve = run.InRun("barrel-weathered.carve");
        await CallTextAsync(driver, "save", new { path = carve });

        var saved = JsonDocument.Parse(File.ReadAllText(carve)).RootElement;
        if (!saved.TryGetProperty("version", out var version) || version.GetInt32() != 10) run.Note($"save: the file's version is {(saved.TryGetProperty("version", out var v) ? v.ToString() : "absent")}, not 10");
        var graphs = Graphs(saved).ToArray();
        run.Log.Add($"save: graph paths {string.Join(", ", graphs.Distinct())}");
        if (graphs.Length == 0 || graphs.Any(g => !g.StartsWith("graphs/", StringComparison.Ordinal))) run.Note($"save: graph paths [{string.Join(", ", graphs)}], not all graphs/…");

        await CallJsonAsync(driver, "open", new { path = carve });
        var reopened = await Scene.ReadOneAsync(driver, "mesh");
        if (reopened.Raw.GetRawText() != before.Raw.GetRawText()) run.Note("get_mesh after save and open differs from before");
        var prefix = $"w{WeatheredRun.BakeSize}-";
        foreach (var name in run.Shots.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            var again = await driver.CaptureAsync(name + "-reopened", run.Shots[name]);
            RebakeStage.Same(run, $"{name}, after save and open", run.Frames[name], again);
        }

        var content = run.InRun(Path.Combine("game", "Content", "models"));
        Directory.CreateDirectory(content);
        var glb = Path.Combine(content, "barrel-weathered.glb");
        var exported = await CallTextAsync(driver, "export", new { path = glb });
        await File.WriteAllTextAsync(run.InRun("export-result.txt"), exported);
        if (exported.Contains("warning", StringComparison.OrdinalIgnoreCase) || !exported.Contains("barrel-weathered.glb", StringComparison.Ordinal)) run.Note($"export: expected a clean report of barrel-weathered.glb, got: {exported}");

        var rows = new List<Comparison.Result>();
        var glbFrames = new Dictionary<string, ScenarioFrame>();
        foreach (var (view, direction) in new[] { ("front", SeenStage.Front), ("three-quarter", SeenStage.ThreeQuarter) })
        {
            var name = prefix + view;
            var frame = glbFrames[view] = await driver.CaptureToolAsync("render_glb", name + "-glb", new { path = glb, direction = new[] { direction.X, direction.Y, direction.Z }, size = WeatheredSets.Shaded });
            rows.Add(Comparison.Measure(name, "good", run.Frames[name], frame));
            Comparison.WriteTriptych(run.InRun(Path.Combine("compare", $"good-{view}.png")), run.Frames[name], frame);
        }

        rows.Add(Comparison.Measure("three-quarter-glb-against-front", "control", run.Frames[prefix + "front"], glbFrames["three-quarter"]));
        foreach (var r in rows) run.Log.Add($"render_glb {r.Kind} {r.Name}: {r.Differing} of {r.Total} differ, largest patch {r.LargestPatch}, fails {r.Fails}");
        foreach (var r in rows.Where(r => r.Kind == "good" && r.Fails)) run.Note($"{r.Name}: render_glb differs from render in {r.Differing} of {r.Total} pixels, largest patch {r.LargestPatch} (D-6's rule: 0.5 % or a patch of {Comparison.PatchLimit})");
        foreach (var r in rows.Where(r => r.Kind == "control" && !r.Fails)) run.Note($"the control {r.Name} passes the frame rule, so the rule cannot tell two views apart");
    }

    /// <summary>Every <c>graph</c> string anywhere in the saved file.</summary>
    private static IEnumerable<string> Graphs(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (p.Name == "graph" && p.Value.ValueKind == JsonValueKind.String) yield return p.Value.GetString()!;
                foreach (var g in Graphs(p.Value)) yield return g;
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in e.EnumerateArray().SelectMany(Graphs)) yield return g;
        }
    }
}
