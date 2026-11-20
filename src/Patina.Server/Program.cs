using Carve.Hosting;
using Empyrean;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Patina.Graphs.Nodes;
using Patina.Server;

// patina hosts through Carve.Hosting (T-111) as carve does: its own name and tools, the same extension loading, the
// stdio transport unless an extension brings its own. Its one look is swatch's sphere (T-114).
return await EngineHost.RunAsync(args, new EngineHostOptions
{
    Name = "patina",
    Product = typeof(Program).Assembly,
    AddServer = server =>
    {
        Program.AddTools(server.Services, server.Mcp);
        server.Services.AddSingleton<SwatchComposition>();
        server.Services.UseRenderPipeline<SwatchComposition>();
        server.Services.AddSystem<SwatchSystem>();
    },
});

/// <summary>patina's tools, and the services they share, in one place for this file and the unit harness.</summary>
public partial class Program
{
    /// <summary>The tools patina serves, in the order tools/list gives them. A later task adds its tool with one line here.</summary>
    public static McpServerTool[] Tools(NodeRegistry registry) =>
    [
        DefineGraphTool.Create(registry),
        EditGraphTool.Create(),
        GetGraphTool.Create(),
        SaveTool.Create(),
        OpenTool.Create(),
        PushSnapshotTool.Create(),
        PopSnapshotTool.Create(),
        ExportTool.Create(),
        SwatchTool.Create(),
        CheckTool.Create(),
    ];

    /// <summary>
    /// Registers the graph store, the node registry, the tool folder and every tool. <paramref name="baseDirectory"/> is
    /// what save and open resolve a relative path against; null, as patina itself passes, is the working directory.
    /// </summary>
    public static void AddTools(IServiceCollection services, IMcpServerBuilder mcp, string? baseDirectory = null)
    {
        var registry = NodeRegistry.BuiltIn;
        mcp.WithTools(Tools(registry));
        services.AddSingleton(registry);
        services.AddSingleton<GraphStore>();
        services.AddSingleton(new ToolFolder(baseDirectory));
        services.AddSingleton<OffscreenQueue<SwatchRequest>>();
    }
}
