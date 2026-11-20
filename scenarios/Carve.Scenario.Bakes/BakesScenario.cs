using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;

namespace Carve.Scenario.Bakes;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class BakesExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, BakesScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The bakes, one stage after another in the order of <see cref="BakesStages.All"/>. A failed check is noted and the run goes on;
/// the run fails at its end with every failure of every stage. <c>CARVE_BAKES_BREAK</c> makes it wrong on purpose
/// (<see cref="BakesChecks.Break"/>), and it must then fail.
/// </summary>
public sealed class BakesScenario : IScenario
{
    public async Task RunAsync(ScenarioDriver driver)
    {
        foreach (var stage in BakesStages.All)
        {
            await stage.RunAsync(driver);
        }

        if (BakesChecks.Failures.Count > 0)
        {
            driver.Fail($"{BakesChecks.Failures.Count} check(s) failed:\n" + string.Join("\n", BakesChecks.Failures.Select(f => "  " + f)));
        }
    }
}
