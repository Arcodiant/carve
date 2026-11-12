using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Cuts;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class CutsExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, CutsScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The cuts, one stage after another in the order of <see cref="CutsStages.All"/>. Each stage starts from its own
/// <c>define_mesh</c>, so stages share no ids. A failed check is noted and the run goes on; the run fails at its end
/// with every failure of every stage.
/// </summary>
public sealed class CutsScenario : IScenario
{
    public async Task RunAsync(ScenarioDriver driver)
    {
        foreach (var stage in CutsStages.All)
        {
            await stage.RunAsync(driver);
        }

        if (Failures.Count > 0)
        {
            driver.Fail($"{Failures.Count} check(s) failed:\n" + string.Join("\n", Failures.Select(f => "  " + f)));
        }
    }
}
