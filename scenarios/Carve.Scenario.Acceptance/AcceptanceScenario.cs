using System.Text;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Orcward;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Acceptance;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class AcceptanceExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, AcceptanceScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The frame step of T-49's acceptance run. A fresh agent's session left a round folder (<c>CARVE_ACCEPTANCE_RUN</c>) with
/// <c>character.carve</c> and <c>game/Content/models/character.glb</c>. This opens the saved model and draws it with <c>render</c>
/// and the exported file with <c>render_glb</c>, at rest and at three times of every clip it has, from the front and the side,
/// and measures each pair under D-6's rule as the Orcward scenario does (<see cref="Comparison"/>, linked): a pixel differs when its luma
/// differs past 4 of 255 (the person's ruling of 2026-11-17; past 8 in R, G or B before), a frame fails past 0.5 % of its pixels or with a connected patch of differing pixels past 50. Each frame also has a
/// positive check: the character covers at least 2 % of the frame (a camera that looked away would show background), and the
/// render of the front does not pass against the exported file's view from the back (so the comparison tells views apart).
/// Results go to <c>acceptance/differences.tsv</c> and a triptych of each pair to <c>acceptance/compare/</c> in the round folder,
/// which <c>tests/Carve.AcceptanceLoad.Tests</c> then reads along with the loads.
/// </summary>
public sealed class AcceptanceScenario : IScenario
{
    public async Task RunAsync(ScenarioDriver driver)
    {
        var round = Environment.GetEnvironmentVariable("CARVE_ACCEPTANCE_RUN") ?? throw new InvalidOperationException("CARVE_ACCEPTANCE_RUN is unset");
        var carveFile = Path.Combine(round, "character.carve");
        var glb = Path.Combine(round, "game", "Content", "models", "character.glb");
        var outDir = Path.Combine(round, "acceptance");
        if (Directory.Exists(outDir))
        {
            Directory.Delete(outDir, recursive: true);
        }

        Directory.CreateDirectory(Path.Combine(outDir, "compare"));

        await CallTextAsync(driver, "open", new { path = carveFile });
        var clips = (await CallJsonAsync(driver, "get_clips", new { })).GetProperty("clips").EnumerateArray()
            .Select(c => (Name: c.GetProperty("name").GetString()!, Duration: c.GetProperty("duration").GetSingle())).ToList();

        var results = new List<Comparison.Result>();
        var views = new[] { ("front", "+z"), ("side", "+x") };
        var poses = new List<(string Name, string? Clip, float Time)> { ("rest", null, 0) };
        foreach (var (name, duration) in clips)
        {
            // 0, a third and two thirds of the clip: a key time and two places that are probably between keys.
            foreach (var f in new[] { 0f, 1f / 3, 2f / 3 })
            {
                poses.Add(($"{name.ToLowerInvariant()}-{f * duration:0.000}", name, f * duration));
            }
        }

        foreach (var (poseName, clip, time) in poses)
        {
            foreach (var (viewName, view) in views)
            {
                object a = clip is null ? new { view } : new { view, clip, time };
                object b = clip is null ? new { path = glb, view } : new { path = glb, view, clip, time };
                var viaRender = await driver.CaptureAsync($"render-{poseName}-{viewName}", a);
                var viaGlb = await driver.CaptureToolAsync("render_glb", $"glb-{poseName}-{viewName}", b);
                var r = Comparison.Measure($"{poseName}-{viewName}", "good", viaRender, viaGlb);
                results.Add(r);
                Comparison.WriteTriptych(Path.Combine(outDir, "compare", $"{poseName}-{viewName}.png"), viaRender, viaGlb);
                if (r.Fails)
                {
                    driver.Fail($"{r.Name}: render_glb differs from render, {r.Differing} px ({r.Percent:0.000} %), largest patch {r.LargestPatch} px");
                }

                if (r.Foreground * 50 < r.Total)
                {
                    driver.Fail($"{r.Name}: the character covers {r.Foreground} px, under 2 % of the frame");
                }
            }
        }

        // The comparison must tell views apart: the render from the front against the file seen from the back must fail.
        var front = await driver.CaptureAsync("render-rest-front-control", new { view = "+z" });
        var back = await driver.CaptureToolAsync("render_glb", "glb-rest-back-control", new { path = glb, view = "-z" });
        var control = Comparison.Measure("rest-front-vs-back", "control", front, back);
        results.Add(control);
        if (!control.Fails)
        {
            driver.Fail($"the comparison does not tell the front from the back: {control.Differing} px, largest patch {control.LargestPatch} px");
        }

        var tsv = new StringBuilder("name\tkind\tdiffering\ttotal\tpercent\tpatch\tfails\tforeground\n");
        foreach (var r in results)
        {
            tsv.Append($"{r.Name}\t{r.Kind}\t{r.Differing}\t{r.Total}\t{r.Percent:0.000}\t{r.LargestPatch}\t{(r.Fails ? "fails" : "passes")}\t{r.Foreground}\n");
        }

        await File.WriteAllTextAsync(Path.Combine(outDir, "differences.tsv"), tsv.ToString());
    }
}
