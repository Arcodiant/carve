using Carve.Extensibility;
using Carve.Server;
using Empyrean;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

// Lowest first: the product's defaults, then whatever extensions add, then the environment and
// the command line, which stay on top so an extension's value is a default and never forced.
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Empyrean:Time:FixedDeltaTime"] = "0.05",
    ["Empyrean:Time:PaceToFixedStep"] = "true",
});
builder.Configuration.Sources.Add(new EnvironmentVariablesConfigurationSource());
builder.Configuration.Sources.Add(new CommandLineConfigurationSource { Args = args });

// Stdout belongs to MCP.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.AddProvider(new FileLoggerProvider(
    Path.Combine(Path.GetTempPath(), $"carve-{Environment.ProcessId}.log")));

IReadOnlyList<ICarveExtension> extensions;
try
{
    extensions = ExtensionLoader.Load(builder.Configuration);
}
catch (ExtensionLoadException e)
{
    Console.Error.WriteLine($"carve: {e.Message}");
    return 1;
}

var services = builder.Services;
var product = typeof(Program).Assembly;
var mcp = services.AddMcpServer(o =>
    o.ServerInfo = new Implementation { Name = "carve", Version = product.GetName().Version?.ToString() ?? "0" });

// After the SDK has gathered the tools, so that tools/list answers even when there are none.
services.PostConfigure<McpServerOptions>(o => o.ToolCollection ??= []);

if (!RunHooks(extensions, nameof(ICarveExtension.ConfigureHost),
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

services.AddSingleton(RenderTool.Create);
mcp.WithTools([DefineMeshTool.Create(), MoveTool.Create(), ExtrudeTool.Create(), GetMeshTool.Create(), UndoTool.Create(), ExportTool.Create(), SaveTool.Create(), OpenTool.Create(), CheckTool.Create(), SelectTool.Create(), DefineGroupTool.Create(), SetColourTool.Create(), SetShadingTool.Create(), MarkEdgesTool.Create(), DefineSkeletonTool.Create(), GetSkeletonTool.Create(), SetWeightsTool.Create(), DefineClipTool.Create(), DeleteClipTool.Create()]);

services.AddSingleton<EngineStatus>();
services.AddSingleton<ModelStore>();
services.AddSingleton<RenderQueue>();
services.AddSingleton<RenderComposition>();
services.UseRenderPipeline<RenderComposition>();
services.AddSystem<RenderSystem>();
services.AddHostedService<EngineThread>();

using var host = builder.Build();
await host.StartAsync();

if (!RunHooks(extensions, nameof(ICarveExtension.OnStarted), e => e.OnStarted(host.Services)))
{
    await host.StopAsync();
    return 1;
}

await host.WaitForShutdownAsync();
return Environment.ExitCode; // An extension reports a failed run here, then stops the host.

static bool RunHooks(IEnumerable<ICarveExtension> extensions, string hook, Action<ICarveExtension> run)
{
    foreach (var extension in extensions)
    {
        try
        {
            run(extension);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"carve: extension {extension.GetType().FullName} failed in {hook}: {e.Message}");
            return false;
        }
    }

    return true;
}
