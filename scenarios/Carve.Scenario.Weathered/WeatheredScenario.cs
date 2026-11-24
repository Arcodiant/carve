using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carve.Scenario.Weathered;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class WeatheredExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, WeatheredScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// T-167 through the real tools: the weathered barrel, one stage after another in the order of <see cref="WeatheredStages.All"/>. A failed
/// check is noted and the run goes on; the run fails at its end with every failure. <c>CARVE_WEATHERED_BREAK</c> makes it wrong on purpose
/// (<see cref="WeatheredRun.Break"/>), and it must then fail.
/// </summary>
public sealed class WeatheredScenario(IConfiguration configuration) : IScenario
{
    public async Task RunAsync(ScenarioDriver driver)
    {
        var run = new WeatheredRun(driver);
        try
        {
            foreach (var stage in WeatheredStages.For(configuration[WeatheredStages.SelectionKey]))
            {
                await stage.RunAsync(run);
            }
        }
        finally
        {
            // Every failure noted, counted, even when a check of M-7's failed the driver first and stopped the run (the mirror break's end grain).
            File.WriteAllLines(run.InRun("sets.txt"), run.Log);
            File.WriteAllLines(run.InRun("failures.txt"), [$"{run.Failures.Count} check(s) failed", .. run.Failures]);
        }

        if (run.Failures.Count > 0)
        {
            driver.Fail($"{run.Failures.Count} check(s) failed:\n" + string.Join("\n", run.Failures.Select(f => "  " + f)));
        }
    }
}

/// <summary>What the stages share: the driver, the run folder's files, and the failures noted so far.</summary>
internal sealed class WeatheredRun(ScenarioDriver driver)
{
    /// <summary>
    /// The break table's switch: <c>swap</c> (the two graphs' materials exchanged), <c>neutral</c> (the texel checks read a neutral PNG),
    /// <c>mirror</c> (the checks' camera mirrored top to bottom), <c>stale</c> (stage 3 reads stage 2's frame after the <c>move</c>) or
    /// <c>no-rebake</c> (stage 3 expects nothing baked after the graph edit) make the scenario wrong on purpose, and it must then fail.
    /// </summary>
    public static readonly string? Break = Environment.GetEnvironmentVariable("CARVE_WEATHERED_BREAK") is { Length: > 0 } b ? b : null;

    public const int BakeSize = 512;

    public ScenarioDriver Driver { get; } = driver;

    public List<string> Failures { get; } = [];

    /// <summary>The folder the scenario's own files were copied with it into: the barrel, the end grain, the graphs.</summary>
    public string Here { get; } = Path.GetDirectoryName(typeof(WeatheredRun).Assembly.Location) ?? AppContext.BaseDirectory;

    /// <summary>A file of the run folder, the graphs copied there so a stage can edit them.</summary>
    public string InRun(string file) => Path.Combine(Driver.RunFolder, file);

    public void Note(string failure) => Failures.Add(failure);

    /// <summary>The frames of stage 2 (and later), by name, for the stages after it to compare with.</summary>
    public Dictionary<string, ScenarioFrame> Frames { get; } = [];

    /// <summary>The arguments of <c>render</c> each of <see cref="Frames"/> was drawn with, so stage 5 can draw them again.</summary>
    public Dictionary<string, object> Shots { get; } = [];

    /// <summary>A frame drawn and kept, with its arguments, under its name.</summary>
    public async Task<ScenarioFrame> CaptureAsync(string name, object args)
    {
        Shots[name] = args;
        return Frames[name] = await Driver.CaptureAsync(name, args);
    }

    /// <summary>The numbers every check measured, written to <c>sets.txt</c> at the end.</summary>
    public List<string> Log { get; } = [];

    /// <summary>The ruled thresholds (the person, 2026-11-24): a - b of set means at least (or at most) the margin, noted when not.</summary>
    public void AtLeast(string what, double a, double b, double margin)
    {
        Log.Add($"{what}: {WeatheredSets.F(a)} - {WeatheredSets.F(b)} = {WeatheredSets.F(a - b)} (at least {margin})");
        if (!(a - b >= margin)) Note($"{what}: {WeatheredSets.F(a)} - {WeatheredSets.F(b)} = {WeatheredSets.F(a - b)}, under the ruled {margin}");
    }

    public void AtMost(string what, double a, double b, double margin)
    {
        Log.Add($"{what}: {WeatheredSets.F(a)} - {WeatheredSets.F(b)} = {WeatheredSets.F(a - b)} (at most {margin})");
        if (!(a - b <= margin)) Note($"{what}: {WeatheredSets.F(a)} - {WeatheredSets.F(b)} = {WeatheredSets.F(a - b)}, over the ruled {margin}");
    }

    /// <summary>Runs a check of M-7's that fails the driver, noting its failure and going on (the driver's first failure stays sticky).</summary>
    public void Try(Action check)
    {
        try { check(); }
        catch (ScenarioFailedException e) { Note(e.Message); }
    }
}
