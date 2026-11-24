using System.Numerics;
using System.Text;
using System.Text.Json;
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
/// At rest, both draws of the front and the side must also overlap the silhouette worked out from the saved meshes
/// (<see cref="Silhouette"/>) by 0.9 or more, which a camera fault <c>render</c> and <c>render_glb</c> share would fail, and at least one
/// view's silhouette must overlap its own mirror, and one its own flip, by less (else this check could not see the fault).
/// Results go to <c>acceptance/differences.tsv</c> (the silhouette overlaps to <c>acceptance/silhouettes.tsv</c>) and a triptych of each pair to <c>acceptance/compare/</c> in the round folder,
/// which <c>tests/Carve.AcceptanceLoad.Tests</c> then reads along with the loads.
/// </summary>
public sealed class AcceptanceScenario : IScenario
{
    private const int Size = 512;

    public async Task RunAsync(ScenarioDriver driver)
    {
        if (Environment.GetEnvironmentVariable("CARVE_WEATHERING_RUN") is { Length: > 0 } weathering)
        {
            await WeatheringStep.RunAsync(driver, weathering);
            return;
        }

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

        // The meshes, named by the saved file, read through get_mesh: what the silhouette at rest is worked from.
        // A deleted vertex or face is null in its slot, and a holed face is {loop, holes} (RV-53): Silhouette.Read keeps both.
        var meshes = new List<Silhouette.Mesh>();
        foreach (var m in JsonDocument.Parse(await File.ReadAllTextAsync(carveFile)).RootElement.GetProperty("meshes").EnumerateArray())
        {
            meshes.Add(Silhouette.Read(await CallJsonAsync(driver, "get_mesh", new { mesh = m.GetProperty("name").GetString()! })));
        }

        var silhouettes = new StringBuilder("name\toverlap\tfails\n");
        var predicted = new Dictionary<string, bool[]>();
        foreach (var (viewName, direction) in new[] { ("front", Vector3.UnitZ), ("side", Vector3.UnitX) })
        {
            var p = Silhouette.Predict(meshes, direction, Size);
            predicted[viewName] = p;
            foreach (var (kind, turned) in new[] { ("mirror", Silhouette.Mirror(p, Size)), ("flip", Silhouette.Flip(p, Size)) })
            {
                var o = Silhouette.Overlap(p, turned);
                silhouettes.Append($"predicted-{viewName}-vs-{kind}\t{o:0.000}\t{(o < Silhouette.Least ? "differs" : "same")}\n");
            }
        }

        foreach (var kind in new[] { "mirror", "flip" })
        {
            Func<bool[], bool[]> turn = kind == "mirror" ? p => Silhouette.Mirror(p, Size) : p => Silhouette.Flip(p, Size);
            if (predicted.Values.All(p => Silhouette.Overlap(p, turn(p)) >= Silhouette.Least))
            {
                driver.Fail($"the character's silhouette is the same under a {kind} from the front and the side, so the silhouette check cannot see a {kind}ed camera");
            }
        }

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
                if (clip is null)
                {
                    foreach (var frame in new[] { viaRender, viaGlb })
                    {
                        var o = Silhouette.Overlap(Foreground(frame), predicted[viewName]);
                        silhouettes.Append($"{frame.Name}\t{o:0.000}\t{(o < Silhouette.Least ? "fails" : "passes")}\n");
                        if (o < Silhouette.Least)
                        {
                            driver.Fail($"{frame.Name}: the character overlaps its silhouette worked from the meshes by {o:0.000}, under {Silhouette.Least} (a mirrored, flipped or misplaced camera)");
                        }
                    }
                }

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
        await File.WriteAllTextAsync(Path.Combine(outDir, "silhouettes.tsv"), silhouettes.ToString());
    }

    /// <summary>The frame's pixels that are not the background (kept here, so <see cref="Silhouette"/> needs no scenario types and a unit test can link it).</summary>
    private static bool[] Foreground(ScenarioFrame frame)
    {
        var mask = new bool[frame.Width * frame.Height];
        for (var i = 0; i < mask.Length; i++)
        {
            var p = frame.Pixels.AsSpan(i * 4, 4);
            mask[i] = !IsBackground((p[0], p[1], p[2], p[3]));
        }

        return mask;
    }
}
