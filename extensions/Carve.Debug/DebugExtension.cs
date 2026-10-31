using System.Globalization;
using System.IO.Pipelines;
using Carve.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;

namespace Carve.Debug;

/// <summary>
/// Runs the shipped product unattended: the transport swapped for an in-memory pipe with the SDK's
/// client on the far end, a fixed clock and seed, and a run folder. List it first in
/// <c>Carve:Extensions</c>, with one scenario after it.
/// </summary>
public sealed class DebugExtension : ICarveExtension
{
    private const string SeedKey = "Carve:Debug:Seed";
    private const string RunFolderKey = "Carve:Debug:RunFolder";

    private readonly Pipe _toServer = new();
    private readonly Pipe _fromServer = new();

    public void ConfigureHost(CarveHostBuilderContext context)
    {
        context.Mcp.WithStreamServerTransport(_toServer.Reader.AsStream(), _fromServer.Writer.AsStream());

        // Defaults, so a person can change either on the command line. The engine ignores pacing
        // on the fixed clock, and warns if it is left on.
        context.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Empyrean:Time:FrameDeltaTime"] = (1.0 / 60.0).ToString("R", CultureInfo.InvariantCulture),
            ["Empyrean:Time:PaceToFixedStep"] = "false",
        });

        context.Services.AddSingleton(provider => new SeededRandom(
            int.Parse(provider.GetRequiredService<IConfiguration>()[SeedKey] ?? "1", CultureInfo.InvariantCulture)));
    }

    public void OnStarted(IServiceProvider services)
    {
        var scenarios = services.GetServices<IScenario>().ToList();
        if (scenarios is not [var scenario])
        {
            throw new InvalidOperationException(
                $"the debug extension needs exactly one scenario listed after it; there are {scenarios.Count}.");
        }

        var name = NameOf(scenario);
        var random = services.GetRequiredService<SeededRandom>();
        var folder = RunFolder.Create(services.GetRequiredService<IConfiguration>()[RunFolderKey], name);
        var log = new RunLog(folder.Log, name, random.Seed);
        Console.Error.WriteLine($"carve-debug: run folder {folder.Path}");

        var lifetime = services.GetRequiredService<IHostApplicationLifetime>();
        _ = Task.Run(() => RunAsync(scenario, folder, log, lifetime));
    }

    private static string NameOf(IScenario scenario) =>
        (scenario.GetType().Assembly.GetName().Name ?? "scenario").Split('.')[^1].ToLowerInvariant();

    private async Task RunAsync(IScenario scenario, RunFolder folder, RunLog log, IHostApplicationLifetime lifetime)
    {
        // The outcome is settled before the client goes: closing it ends the server's session,
        // which stops the host, and the process may exit from there.
        try
        {
            var transport = new PipeClientTransport(_fromServer.Reader.AsStream(), _toServer.Writer.AsStream());
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: lifetime.ApplicationStopping);
            var driver = new ScenarioDriver(client, folder, log, lifetime.ApplicationStopping);
            Conclude(log, await RunScenarioAsync(scenario, driver));
        }
        catch (Exception e)
        {
            Conclude(log, e.Message);
        }

        lifetime.StopApplication();
    }

    private static async Task<string?> RunScenarioAsync(IScenario scenario, ScenarioDriver driver)
    {
        try
        {
            await scenario.RunAsync(driver);
            return driver.Failure;
        }
        catch (Exception e)
        {
            return driver.Failure ?? e.ToString();
        }
    }

    private static void Conclude(RunLog log, string? failure)
    {
        log.Finish(failure);
        if (failure is not null)
        {
            Console.Error.WriteLine($"carve-debug: scenario failed: {failure}");
            Environment.ExitCode = 1;
        }
    }
}
