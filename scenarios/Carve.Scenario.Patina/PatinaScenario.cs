using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Patina;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class PatinaExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, PatinaScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// patina's stages, one after another in the order of <see cref="PatinaStages.All"/>. A failed check is noted and the
/// run goes on; the run fails at its end with every failure of every stage.
/// </summary>
public sealed class PatinaScenario(IConfiguration configuration) : IScenario
{
    public async Task RunAsync(ScenarioDriver driver)
    {
        var checks = new PatinaChecks(driver);
        foreach (var stage in PatinaStages.For(configuration[PatinaStages.SelectionKey]))
        {
            await stage.RunAsync(driver, checks);
        }

        if (checks.Failures.Count > 0)
        {
            driver.Fail($"{checks.Failures.Count} check(s) failed:\n" + string.Join("\n", checks.Failures.Select(f => "  " + f)));
        }
    }
}

/// <summary>Calls that note what went wrong and go on, for the stages.</summary>
public sealed class PatinaChecks(ScenarioDriver driver)
{
    public List<string> Failures { get; } = [];

    public void Check(bool holds, string what)
    {
        if (!holds) Failures.Add(what);
    }

    /// <summary>Calls <paramref name="tool"/> with <paramref name="arguments"/> (JSON); its text, noting a failure when it errs and should not, or should and does not.</summary>
    public async Task<string> CallAsync(string tool, string arguments, bool refused = false)
    {
        var result = await driver.CallAsync(tool, JsonDocument.Parse(arguments).RootElement.Clone());
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        if ((result.IsError ?? false) != refused)
            Failures.Add($"{tool} {arguments}: {(refused ? "was not refused" : "failed")}: {text}");
        return text;
    }

    /// <summary>The open graph as a file, from get_graph (everything after its head line).</summary>
    public async Task<string> GraphAsync()
    {
        var text = await CallAsync("get_graph", "{}");
        return text[(text.IndexOf('\n') + 1)..];
    }
}
