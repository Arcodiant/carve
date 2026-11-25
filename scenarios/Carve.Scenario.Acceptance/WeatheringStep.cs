using System.Text;
using Carve.Debug;
using Carve.Scenario.Orcward;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Acceptance;

/// <summary>
/// T-168's frame step, run when <c>CARVE_WEATHERING_RUN</c> names a round folder: <c>wrench-weathered.carve</c> drawn with <c>render</c> and
/// <c>game/Content/models/wrench.glb</c> with <c>render_glb</c>, front and three-quarter, each pair measured under D-6's frame rule
/// (<see cref="Comparison"/>), and a front-against-back control that must fail. Writes <c>acceptance/differences.tsv</c> and <c>compare/</c>.
/// </summary>
internal static class WeatheringStep
{
    private const int Size = 512;

    public static async Task RunAsync(ScenarioDriver driver, string round)
    {
        var glb = Path.Combine(round, "game", "Content", "models", "wrench.glb");
        var outDir = Path.Combine(round, "acceptance");
        if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        Directory.CreateDirectory(Path.Combine(outDir, "compare"));

        await CallTextAsync(driver, "open", new { path = Path.Combine(round, "wrench-weathered.carve") });
        var results = new List<Comparison.Result>();
        foreach (var (name, direction) in new[] { ("front", new[] { 0.0, 0.0, 1.0 }), ("three-quarter", new[] { 0.6, 0.45, 0.66 }) })
        {
            var viaRender = await driver.CaptureAsync($"render-{name}", new { direction, size = Size, mode = "shaded" });
            var viaGlb = await driver.CaptureToolAsync("render_glb", $"glb-{name}", new { path = glb, direction, size = Size });
            var r = Comparison.Measure(name, "good", viaRender, viaGlb);
            results.Add(r);
            Comparison.WriteTriptych(Path.Combine(outDir, "compare", $"{name}.png"), viaRender, viaGlb);
            if (r.Fails) driver.Fail($"{r.Name}: render_glb differs from render, {r.Differing} px ({r.Percent:0.000} %), largest patch {r.LargestPatch} px");
            if (r.Foreground * 50 < r.Total) driver.Fail($"{r.Name}: the wrench covers {r.Foreground} px, under 2 % of the frame");
        }

        var front = await driver.CaptureAsync("render-front-control", new { direction = new[] { 0.0, 0.0, 1.0 }, size = Size, mode = "shaded" });
        var back = await driver.CaptureToolAsync("render_glb", "glb-back-control", new { path = glb, direction = new[] { 0.0, 0.0, -1.0 }, size = Size });
        var control = Comparison.Measure("front-vs-back", "control", front, back);
        results.Add(control);
        if (!control.Fails) driver.Fail($"the comparison does not tell the front from the back: {control.Differing} px, largest patch {control.LargestPatch} px");

        var tsv = new StringBuilder("name\tkind\tdiffering\ttotal\tpercent\tpatch\tfails\tforeground\n");
        foreach (var r in results)
            tsv.Append($"{r.Name}\t{r.Kind}\t{r.Differing}\t{r.Total}\t{r.Percent:0.000}\t{r.LargestPatch}\t{(r.Fails ? "fails" : "passes")}\t{r.Foreground}\n");
        await File.WriteAllTextAsync(Path.Combine(outDir, "differences.tsv"), tsv.ToString());
    }
}
