using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Nodes;

namespace Patina.Server;

/// <summary>What patina's tools share: how a tool is made, its results, the graph's one-line state and its faults, and the path rules.</summary>
public static class ToolSupport
{
    /// <summary>What a tool says when there is no open graph yet.</summary>
    public const string NoGraph = "there is no open graph yet; define_graph or open one first";

    /// <summary>A tool from a class with a <c>Run</c> method, with its own input schema, closed to other arguments.</summary>
    public static McpServerTool Create<T>(string name, string description, JsonObject properties, params string[] required) where T : class
    {
        var tool = McpServerTool.Create(
            typeof(T).GetMethod("Run")!,
            request => ActivatorUtilities.CreateInstance<T>(request.Services!),
            new McpServerToolCreateOptions { Name = name, Description = description });
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Length > 0) schema["required"] = new JsonArray([.. required.Select(r => (JsonNode)r)]);
        schema["additionalProperties"] = false;
        tool.ProtocolTool.InputSchema = JsonSerializer.SerializeToElement(schema);
        return tool;
    }

    public static CallToolResult Line(string text) => new() { Content = [new TextContentBlock { Text = text }] };

    public static CallToolResult Error(string text) => new() { IsError = true, Content = [new TextContentBlock { Text = text }] };

    /// <summary>An argument the tool does not take, as an error naming the ones it does; null when every argument is known.</summary>
    public static CallToolResult? Unknown(string tool, RequestContext<CallToolRequestParams> context, params string[] known)
    {
        var bad = (context.Params?.Arguments?.Keys ?? []).Where(k => !known.Contains(k)).ToList();
        if (bad.Count == 0) return null;
        var takes = known.Length == 0 ? "takes no arguments" : "takes " + string.Join(", ", known);
        return Error($"{tool}: unknown argument {string.Join(", ", bad.Select(b => $"'{b}'"))}; {tool} {takes}");
    }

    /// <summary>A string argument, or null with an error when it is given as something else.</summary>
    public static bool TryText(string tool, string name, JsonElement? value, out string? text, out CallToolResult? error)
    {
        text = null;
        error = null;
        if (value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return true;
        if (value.Value.ValueKind == JsonValueKind.String)
        {
            text = value.Value.GetString();
            return true;
        }
        error = Error($"{tool}: {name} is a string, not {Kind(value.Value)}");
        return false;
    }

    public static string Kind(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "true or false",
        _ => "null",
    };

    /// <summary>The graph in one line: <c>4 nodes, size 256, seed 0</c>.</summary>
    public static string Describe(TextureGraph graph) =>
        $"{Count(graph.Nodes.Count, "node")}, size {graph.Size.ToString(CultureInfo.InvariantCulture)}, seed {graph.Seed.ToString(CultureInfo.InvariantCulture)}";

    /// <summary><c>no faults: it evaluates</c>, or the count and one fault a line, each message as T-110 gives it.</summary>
    public static string Faults(IReadOnlyList<GraphFault> faults) => faults.Count == 0
        ? "no faults: it evaluates"
        : Count(faults.Count, "fault") + ":" + string.Concat(faults.Select(f => "\n- " + f.Message));

    /// <summary>The graph and its faults: <c>4 nodes, size 256, seed 0; no faults: it evaluates</c>.</summary>
    public static string State(TextureGraph graph, NodeRegistry registry) =>
        Describe(graph) + "; " + Faults(GraphValidator.Validate(graph, registry));

    public static string Count(int n, string what) => $"{n.ToString(CultureInfo.InvariantCulture)} {what}{(n == 1 ? "" : "s")}";

    /// <summary>
    /// A .texgraph path: <paramref name="path"/> with <c>.texgraph</c> added unless it already ends so (in any case),
    /// resolved against <paramref name="baseDirectory"/> (the working directory when null). Null, with an error, for an
    /// empty path, one with a character a path may not hold, or one naming a folder.
    /// </summary>
    public static string? Resolve(string tool, string? path, string? baseDirectory, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = $"{tool}: path is empty; give a file path, such as planks.texgraph";
            return null;
        }
        if (path.EndsWith('/') || path.EndsWith('\\'))
        {
            error = $"{tool}: path \"{path}\" names a folder; give a file path, such as planks.texgraph";
            return null;
        }
        if (path.IndexOfAny([.. Path.GetInvalidPathChars(), '*', '?', '"', '<', '>', '|']) is var at and >= 0)
        {
            error = $"{tool}: path \"{path}\" holds '{path[at]}', which a file path may not";
            return null;
        }
        if (!path.EndsWith(TexGraphFile.Extension, StringComparison.OrdinalIgnoreCase)) path += TexGraphFile.Extension;
        try
        {
            return Path.GetFullPath(path, baseDirectory ?? Environment.CurrentDirectory);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"{tool}: path \"{path}\" is not a valid file path: {e.Message}";
            return null;
        }
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="path"/> through a temporary file in the same folder moved into
    /// place, creating missing folders, so a failed write leaves no partial file. False, with an error, when it fails.
    /// </summary>
    public static bool Write(string tool, string path, string text, out bool replaced, out string? error)
    {
        replaced = File.Exists(path);
        error = null;
        var temporary = path + ".writing";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temporary); } catch (IOException) { }
            error = $"{tool}: could not write {path}: {e.Message}";
            return false;
        }
    }
}
