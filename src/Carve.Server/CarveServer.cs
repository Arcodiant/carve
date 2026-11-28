using Microsoft.Extensions.DependencyInjection;

namespace Carve.Server;

/// <summary>
/// The tools carve serves and the services they share, registered in one place so that <c>Program.cs</c> and the unit
/// harness in <c>tests/Carve.Server.Tests</c> (T-87) serve the same <c>McpServerTool</c> instances. The engine, the
/// transport and extensions are <c>Program.cs</c>'s; nothing here starts a thread or touches a device.
/// </summary>
public static class CarveServer
{
    /// <summary>
    /// Registers the model store, the engine status, the tool folder and every tool. <paramref name="baseDirectory"/> is
    /// what save, open, export and define_material resolve a relative path against; null, as carve itself passes, is
    /// the working directory at the time of the call.
    /// </summary>
    public static void AddTools(IServiceCollection services, IMcpServerBuilder mcp, string? baseDirectory = null)
    {
        services.AddSingleton(RenderTool.Create);
        services.AddSingleton(RenderGlbTool.Create);
        mcp.WithTools([DefineMeshTool.Create(), DefinePartTool.Create(), GetPartsTool.Create(), KeepLevelTool.Create(), MoveTool.Create(), ExtrudeTool.Create(), SubdivideTool.Create(), DisplaceTool.Create(), TessellateTool.Create(), DeleteTool.Create(), MergeFacesTool.Create(), BridgeTool.Create(), PlaneCutTool.Create(), LoopCutTool.Create(), KnifeTool.Create(), GetMeshTool.Create(), UndoTool.Create(), ClearTool.Create(), ExportTool.Create(), SaveTool.Create(), OpenTool.Create(), CheckTool.Create(), SelectTool.Create(), DefineGroupTool.Create(), SetColourTool.Create(), DefineMaterialTool.Create(), SetMaterialTool.Create(), SetShadingTool.Create(), MarkEdgesTool.Create(), SetUvsTool.Create(), UnwrapTool.Create(), ProjectUvsTool.Create(), DefineSkeletonTool.Create(), GetSkeletonTool.Create(), SetWeightsTool.Create(), GetWeightsTool.Create(), WeighTool.Create(), DefineClipTool.Create(), DeleteClipTool.Create(), GetClipsTool.Create(), DefineTargetTool.Create(), DeleteTargetTool.Create(), BakeTool.Create(), HelpTool.Create()]);

        services.AddSingleton<EngineStatus>();
        services.AddSingleton<ModelStore>();
        services.AddSingleton<RenderQueue>();
        services.AddSingleton(new GraphBakes());
        services.AddSingleton(new ToolFolder(baseDirectory));
    }
}

/// <summary>What a tool resolves a relative file path against: <see cref="Directory"/>, or carve's working directory when null.</summary>
public sealed record ToolFolder(string? Directory);
