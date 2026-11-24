using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>
/// <c>help</c> (T-207): the reference of the tools whose description had to be cut to fit Claude Code's limit of 4,096
/// characters. Each entry holds what left its tool's description, in the same words; a tool's result is not cut there.
/// </summary>
public sealed class HelpTool
{
    /// <summary>
    /// The entries, in the order <c>help {}</c> lists them: tool name, what its reference covers, the reference
    /// (<c>Reference</c> on the tool's class). Adding an entry is one line here, and the tool's description ends with
    /// <c>More: help {"tool":"name"} gives what moved.</c>
    /// </summary>
    private static readonly (string Tool, string Covers, string Reference)[] Entries =
    [
        ("render", "caption formats, the normals and checker colour codes, the uvs look, labels, sheets, bones, weights, parts and every error", RenderTool.Reference),
        ("export", "what is written (nodes, materials, UVs), parts and skinned detail, piece and clips modes, the limits file and its format, the result line, the errors and worked examples", ExportTool.Reference),
    ];

    /// <summary>The names of the entries, in the order <c>help {}</c> lists them.</summary>
    public static IReadOnlyList<string> Tools => [.. Entries.Select(e => e.Tool)];

    /// <summary>One entry's reference, for tests that read a sentence or an example that moved there; null when there is none.</summary>
    public static string? Entry(string tool) => Entries.FirstOrDefault(e => e.Tool == tool).Reference;

    public static string Text =>
        "Gives the reference of a tool whose description holds only what it does and how to call it: the detail that was cut to keep each description whole, in the same words, with its worked examples. "
        + "Takes tool (optional): one tool's name for its reference; without it, a list of the entries, one line each, the tool and what its reference covers. "
        + "The entries are " + string.Join(", ", Tools) + ". A tool with no entry, or a name that is no tool, is a tool error listing the entries. "
        + "Reads nothing of the model, needs no mesh, changes nothing and is not an undo step. "
        + ExampleMarker + """{"tool":"render"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<HelpTool>("help", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["tool"] = new JsonObject { ["description"] = "A tool's name, for its reference; leave it out for the list of entries.", ["type"] = "string" },
        },
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? tool = null)
    {
        var errors = UnknownArguments("help", context, "tool");
        if (!Absent(tool) && tool!.Value.ValueKind != JsonValueKind.String) errors.Add($"tool: expected a string, got {Describe(tool.Value)}");
        if (errors.Count > 0) return Failed("help", errors);
        if (Absent(tool)) return Line(string.Join('\n', Entries.Select(e => $"{e.Tool}: {e.Covers}")));
        var name = tool!.Value.GetString()!;
        if (Entry(name) is not { } reference) return Error($"help: there is no entry for '{name}'; the entries are {string.Join(", ", Tools)}");
        return Line(reference);
    }
}
