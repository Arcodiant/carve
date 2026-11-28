using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>clear (M-34, T-231): empties the model as one undo step and starts a new journal.</summary>
public sealed class ClearTool(ModelStore store)
{
    public const string Example = "{}";

    public const string Text =
        "Empties the model: every mesh, part, material, skeleton, clip and morph target goes, and the model is as a fresh carve's. "
        + "It is one step, so undo straight after it brings the model back as it was (undone is \"clear\"). Clearing a model that is already empty is still a step. "
        + "It also starts again the list of calls save_script writes: a script saved after a clear starts with clear(), and undo of the clear brings the earlier list back. "
        + "Takes only verbosity; any other argument is a tool error. Returns {meshes:[]}, as undo does for a model with no meshes ({\"ok\":true} at silent). "
        + "Worked example, on define_mesh's unit cube (returns {\"meshes\":[]}):\n"
        + ExampleMarker + Example;

    public static McpServerTool Create() => MeshToolSupport.Create<ClearTool>("clear", Text, WithVerbosity(null));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("clear", context, "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        if (errors.Count > 0) return Failed("clear", errors);
        store.Clear("clear");
        return Quiet(Ok(new Dictionary<string, object> { ["meshes"] = System.Array.Empty<object>() }), level);
    }
}
