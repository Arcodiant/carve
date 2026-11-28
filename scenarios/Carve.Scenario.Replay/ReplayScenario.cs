using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;

namespace Carve.Scenario.Replay;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class ReplayExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, ReplayScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// D-8's invariant, An exported script replays to the same model, on small models built for it: the stages of
/// <see cref="ReplayStages.All"/> in order. A failed check is noted and the run goes on; the run fails at its end with all of them.
/// </summary>
public sealed class ReplayScenario : IScenario
{
    public async Task RunAsync(ScenarioDriver driver)
    {
        foreach (var stage in ReplayStages.All)
        {
            await stage.RunAsync(driver);
        }

        if (Replay.Failures.Count > 0)
        {
            driver.Fail($"{Replay.Failures.Count} check(s) failed:\n" + string.Join("\n", Replay.Failures.Select(f => "  " + f)));
        }
    }
}
