using System.Reflection;
using Carve.Extensibility;
using Empyrean;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Carve.Hosting;

/// <summary>Who a server is and what it adds to the host. <see cref="EngineHost.RunAsync"/> does the rest.</summary>
public sealed class EngineHostOptions
{
    /// <summary>The engine defaults both servers run with: a fixed 0.05 s step, paced to it.</summary>
    public static IReadOnlyDictionary<string, string?> EngineDefaults { get; } = new Dictionary<string, string?>
    {
        ["Empyrean:Time:FixedDeltaTime"] = "0.05",
        ["Empyrean:Time:PaceToFixedStep"] = "true",
    };

    /// <summary>The MCP server name (<c>serverInfo.name</c>), and the prefix of every line the host writes to stderr.</summary>
    public required string Name { get; init; }

    /// <summary>The server's own assembly: its version is <c>serverInfo.version</c>.</summary>
    public required Assembly Product { get; init; }

    /// <summary>The log file is <c>&lt;temp&gt;/&lt;prefix&gt;-&lt;process id&gt;.log</c>. Null is the name.</summary>
    public string? LogFilePrefix { get; init; }

    /// <summary>The lowest configuration source, below extensions, the environment and the command line.</summary>
    public IReadOnlyDictionary<string, string?> Defaults { get; init; } = EngineDefaults;

    /// <summary>
    /// The server's own registrations: its tools (on <see cref="EngineHostContext.Mcp"/>), services, systems
    /// (<c>AddSystem</c>) and render pipeline (<c>UseRenderPipeline</c>). Called after the extensions' hooks and the
    /// engine's own registrations, before the engine thread.
    /// </summary>
    public required Action<EngineHostContext> AddServer { get; init; }
}

/// <summary>What <see cref="EngineHostOptions.AddServer"/> registers into.</summary>
public sealed class EngineHostContext(IServiceCollection services, IMcpServerBuilder mcp, IConfiguration configuration)
{
    public IServiceCollection Services { get; } = services;

    public IMcpServerBuilder Mcp { get; } = mcp;

    /// <summary>The configuration so far, every hook's additions in it.</summary>
    public IConfiguration Configuration { get; } = configuration;
}

/// <summary>
/// The process both MCP servers are (T-111, moved from carve's <c>Program.cs</c>): configuration layered, logs to stderr
/// and the temp folder, extensions loaded and their hooks run, the engine registered headless, the stdio transport
/// unless an extension supplied one, the server's own registrations, the engine on its own thread, then the
/// <c>OnStarted</c> hooks and a wait for shutdown.
/// </summary>
public static class EngineHost
{
    /// <summary>Runs the server to its end. The result is the process exit code.</summary>
    public static async Task<int> RunAsync(string[] args, EngineHostOptions options)
    {
        var name = options.Name;
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        // Lowest first: the product's defaults, then whatever extensions add, then the environment and
        // the command line, which stay on top so an extension's value is a default and never forced.
        builder.Configuration.AddInMemoryCollection(options.Defaults);
        builder.Configuration.Sources.Add(new EnvironmentVariablesConfigurationSource());
        builder.Configuration.Sources.Add(new CommandLineConfigurationSource { Args = args });

        // Stdout belongs to MCP.
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.AddProvider(new FileLoggerProvider(
            Path.Combine(Path.GetTempPath(), $"{options.LogFilePrefix ?? name}-{Environment.ProcessId}.log"), name));

        IReadOnlyList<ICarveExtension> extensions;
        try
        {
            extensions = ExtensionLoader.Load(builder.Configuration);
        }
        catch (ExtensionLoadException e)
        {
            Console.Error.WriteLine($"{name}: {e.Message}");
            return 1;
        }

        var services = builder.Services;
        var mcp = services.AddMcpServer(o =>
            o.ServerInfo = new Implementation { Name = name, Version = options.Product.GetName().Version?.ToString() ?? "0" });

        // After the SDK has gathered the tools, so that tools/list answers even when there are none.
        services.PostConfigure<McpServerOptions>(o => o.ToolCollection ??= []);

        if (!RunHooks(name, extensions, nameof(ICarveExtension.ConfigureHost),
            e =>
            {
                e.ConfigureHost(new CarveHostBuilderContext(services, builder.Configuration, mcp));

                // Fresh copies on top after each hook, so a later hook reads the person's switch, not an
                // earlier default. Added, not moved: removing a source rebuilds every provider, and a value
                // set directly on the configuration lives in those providers.
                builder.Configuration.Sources.Add(new EnvironmentVariablesConfigurationSource());
                builder.Configuration.Sources.Add(new CommandLineConfigurationSource { Args = args });
            }))
        {
            return 1;
        }

        // After the hooks, so that first-wins engine substitution works.
        services.AddEmpyreanCore();
        services.AddEmpyreanGraphicsHeadlessCore();
        services.AddEmpyreanConfiguration(builder.Configuration);

        if (!services.Any(d => d.ServiceType == typeof(ITransport)))
        {
            mcp.WithStdioServerTransport();
        }

        options.AddServer(new EngineHostContext(services, mcp, builder.Configuration));

        // A server's tools await it; one that registered its own (carve does, for its in-process harness) keeps it.
        services.TryAddSingleton<EngineStatus>();
        services.AddHostedService<EngineThread>();

        using var host = builder.Build();
        await host.StartAsync();

        if (!RunHooks(name, extensions, nameof(ICarveExtension.OnStarted), e => e.OnStarted(host.Services)))
        {
            await host.StopAsync();
            return 1;
        }

        await host.WaitForShutdownAsync();
        return Environment.ExitCode; // An extension reports a failed run here, then stops the host.
    }

    private static bool RunHooks(string name, IEnumerable<ICarveExtension> extensions, string hook, Action<ICarveExtension> run)
    {
        foreach (var extension in extensions)
        {
            try
            {
                run(extension);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{name}: extension {extension.GetType().FullName} failed in {hook}: {e.Message}");
                return false;
            }
        }

        return true;
    }
}
