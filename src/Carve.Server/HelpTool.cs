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
        ("save", "the file format with the unit cube's file, the part tree and its example, every version's detail, the errors and the missing-texture warning", SaveTool.Reference),
        ("open", "the version 1 detail, the part tree and its example, every error, and which file to keep working in", OpenTool.Reference),
        ("bake", "each map's channels and rules, the surface entries, the curvature block, the result fields and every error", BakeTool.Reference),
        ("select", "the float detail, the stale-selection rule and every error", SelectTool.Reference),
        ("define_material", "how the game's shading reads each field, the glow and occlusion rules, and each texture's colour space and channels", DefineMaterialTool.Reference),
        ("check", "the thresholds, the morph-target and UV-warning rules, every warning's full wording, the envelope warnings, the notes and the stats", CheckTool.Reference),
        ("get_mesh", "where vertex and face ids come from, the worked examples on the unit cube, with its output, and reading a morph target", GetMeshTool.Reference),
        ("unwrap", "what each field of the result means, the stretch and flipped counts and the full list", UnwrapTool.Reference),
    ];

    /// <summary>
    /// The topics (T-199), reference text no one tool owns, in the order <c>help {}</c> lists them after the entries: name, what
    /// it covers, the text. Adding a topic is one line here; a description names it with <c>help {"topic":"name"}</c>.
    /// </summary>
    private static readonly (string Topic, string Covers, string Text)[] Topics =
    [
        ("warnings", "every warning of a mesh, its fix, how warnings are grouped and the verbosity levels", WarningsTopic.Text),
    ];

    /// <summary>The names of the topics, in the order <c>help {}</c> lists them.</summary>
    public static IReadOnlyList<string> TopicNames => [.. Topics.Select(t => t.Topic)];

    /// <summary>One topic's text, for tests; null when there is none.</summary>
    public static string? Topic(string topic) => Topics.FirstOrDefault(t => t.Topic == topic).Text;

    /// <summary>The names of the entries, in the order <c>help {}</c> lists them.</summary>
    public static IReadOnlyList<string> Tools => [.. Entries.Select(e => e.Tool)];

    /// <summary>One entry's reference, for tests that read a sentence or an example that moved there; null when there is none.</summary>
    public static string? Entry(string tool) => Entries.FirstOrDefault(e => e.Tool == tool).Reference;

    public static string Text =>
        "Gives the reference of a tool whose description holds only what it does and how to call it: the detail that was cut to keep each description whole, in the same words, with its worked examples. "
        + "Takes tool (optional): one tool's name for its reference; or topic (optional): the name of reference no one tool owns; without either, a list of the entries then the topics, one line each, the name and what it covers. "
        + "The entries are " + string.Join(", ", Tools) + "; the topics are " + string.Join(", ", TopicNames) + ". A tool with no entry, a name that is no tool, an unknown topic, or both arguments, is a tool error listing them. "
        + "Reads nothing of the model, needs no mesh, changes nothing and is not an undo step. "
        + ExampleMarker + """{"tool":"render"}""" + "\n" + ExampleMarker + """{"topic":"warnings"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<HelpTool>("help", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["tool"] = new JsonObject { ["description"] = "A tool's name, for its reference; leave it out for the list of entries.", ["type"] = "string" },
            ["topic"] = new JsonObject { ["description"] = "A topic's name, for its text; not with tool.", ["type"] = "string" },
        },
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? tool = null, JsonElement? topic = null)
    {
        var errors = UnknownArguments("help", context, "tool", "topic");
        if (!Absent(tool) && tool!.Value.ValueKind != JsonValueKind.String) errors.Add($"tool: expected a string, got {Describe(tool.Value)}");
        if (!Absent(topic) && topic!.Value.ValueKind != JsonValueKind.String) errors.Add($"topic: expected a string, got {Describe(topic.Value)}; the topics are {string.Join(", ", TopicNames)}");
        if (errors.Count > 0) return Failed("help", errors);
        if (!Absent(tool) && !Absent(topic)) return Error($"help: give tool or topic, not both; the entries are {string.Join(", ", Tools)}; the topics are {string.Join(", ", TopicNames)}");
        if (!Absent(topic))
        {
            var topicName = topic!.Value.GetString()!;
            return Topic(topicName) is { } text ? Line(text) : Error($"help: there is no topic '{topicName}'; the topics are {string.Join(", ", TopicNames)}");
        }
        if (Absent(tool)) return Line(string.Join('\n', Entries.Select(e => $"{e.Tool}: {e.Covers}").Concat(Topics.Select(t => $"topic {t.Topic}: {t.Covers}"))));
        var name = tool!.Value.GetString()!;
        if (Entry(name) is not { } reference) return Error($"help: there is no entry for '{name}'; the entries are {string.Join(", ", Tools)}");
        return Line(reference);
    }
}
