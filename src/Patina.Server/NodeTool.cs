using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs.Nodes;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

/// <summary>
/// <c>nodes</c> (T-170): the node catalogue, one type's entry or every type's, with a worked example for each. It holds
/// what define_graph's description could not: Claude Code cuts a tool description at 4,096 characters, and a tool's
/// result is not cut there.
/// </summary>
public sealed class NodeTool(NodeRegistry registry)
{
    /// <summary>The worked example: one type's entry.</summary>
    public const string Worked = """{"type":"stripes"}""";

    public const string Text =
        "Lists patina's node types, the parts of a texture graph: for each, what it does, its inputs (optional ones marked), its outputs when it has several (a source names one as \"id.port\"), "
        + "every parameter with its kind, range and default, and a worked define_graph example, run as written by patina's tests. "
        + "Takes type (optional): one type's name for its entry alone; without it, every type's, in the order define_graph's description lists them. Changes nothing, and needs no open graph. "
        + "Refused: a type patina has not (the error lists the types). Read it before writing a graph: define_graph's description carries only the types' names.\n"
        + "Worked example: nodes " + Worked + " returns:\n";

    /// <summary>The description: <see cref="Text"/>, then the worked example's result as nodes gives it.</summary>
    public static string Description(NodeRegistry registry) => Text + NodeCatalogue.Entry(registry, "stripes").TrimEnd('\n');

    public static McpServerTool Create(NodeRegistry registry) => Create<NodeTool>("nodes", Description(registry), new JsonObject
    {
        ["type"] = new JsonObject { ["type"] = "string", ["description"] = "One node type's name, such as tiles; leave it out for every type." },
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? type = null)
    {
        if (Unknown("nodes", context, "type") is { } unknown) return unknown;
        if (!TryText("nodes", "type", type, out var name, out var error)) return error!;
        if (name is null) return Line($"nodes: {registry.Names.Count()} types\n" + NodeCatalogue.Text(registry).TrimEnd('\n'));
        if (registry.Find(name) is null)
            return Error($"nodes: there is no node type '{name}'; the types are {string.Join(", ", registry.Names)}");
        return Line(NodeCatalogue.Entry(registry, name).TrimEnd('\n'));
    }
}
