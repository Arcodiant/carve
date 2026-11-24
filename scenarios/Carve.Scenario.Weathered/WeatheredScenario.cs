using Carve.Debug;
using Carve.Extensibility;
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
public sealed class WeatheredScenario : IScenario
{
    public async Task RunAsync(ScenarioDriver driver)
    {
        var run = new WeatheredRun(driver);
        foreach (var stage in WeatheredStages.All)
        {
            await stage.RunAsync(run);
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
    /// <summary>The break table's switch (Scope's breaks); none until stage 2 is written.</summary>
    public static readonly string? Break = Environment.GetEnvironmentVariable("CARVE_WEATHERED_BREAK") is { Length: > 0 } b ? b : null;

    public const int BakeSize = 512;

    public ScenarioDriver Driver { get; } = driver;

    public List<string> Failures { get; } = [];

    /// <summary>The folder the scenario's own files were copied with it into: the barrel, the end grain, the graphs.</summary>
    public string Here { get; } = Path.GetDirectoryName(typeof(WeatheredRun).Assembly.Location) ?? AppContext.BaseDirectory;

    /// <summary>A file of the run folder, the graphs copied there so a stage can edit them.</summary>
    public string InRun(string file) => Path.Combine(Driver.RunFolder, file);

    public void Note(string failure) => Failures.Add(failure);
}
