using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Nodes;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

public sealed class GetGraphTool(GraphStore store, NodeRegistry registry)
{
    /// <summary>What get_graph returns after define_graph's worked example, before any save: its head line, then the file.</summary>
    public const string WorkedHead = "get_graph: 4 nodes, size 256, seed 0; no path (save gives it one); no faults: it evaluates";

    public const string Text =
        "Reads back the open graph: a line with its nodes, size, seed, path (the file save last wrote or open read, or no path) and faults, one a line, each naming its node and port or parameter; "
        + "then the graph exactly as save would write it, a .texgraph file: version, size, seed, then one node a line in id order, inputs and params in name order, the parameters the graph gives "
        + "(a default it leaves out is not written). Give that JSON back to define_graph as graph to start again from it. Takes nothing, and changes nothing. "
        + "A graph with exposed parameters (version 2, set with edit_graph's set_parameter) lists them between the two, a line each: its name, kind, value, range (the narrowest of the node parameters it drives) and what it drives; "
        + "and in the file each driven node parameter shows {\"driven_by\": \"<name>\"} where its own value would be, which save leaves out and define_graph reads back. "
        + "A graph using others as graph nodes (T-140) lists each after those, a line each: the file its path resolved to (against the graph's own file's folder, or the tool folder for a graph never saved, which the head line says), "
        + "the nested graph's parameters with the values set on the node and their ranges, its inputs (name and kind, and whether it has a default) and its outputs as the node's ports (wall.base_colour). "
        + "For samples/graphs/bricks.texgraph the lines are:\n" + WorkedBricksParameters + "\n"
        + "and its layout node reads \"gap\": { \"driven_by\": \"mortar\" }.\n"
        + "Worked example, after define_graph's worked example, get_graph {} returns:\n"
        + WorkedHead + "\n"
        + "{\n  \"version\": 1,\n  \"size\": 256,\n  \"seed\": 0,\n  \"nodes\": {\n"
        + "    \"base\": { \"type\": \"blend\", \"inputs\": { \"a\": \"wood\", \"b\": \"tone\" }, \"params\": { \"mode\": \"multiply\" } },\n"
        + "    \"out\": { \"type\": \"output\", \"inputs\": { \"in\": \"base\" }, \"params\": { \"role\": \"base_colour\" } },\n"
        + "    \"tone\": { \"type\": \"noise\", \"params\": { \"cells\": [8, 1], \"interpolation\": \"step\" } },\n"
        + "    \"wood\": { \"type\": \"colour\", \"params\": { \"colour\": \"#aa7040\" } }\n"
        + "  }\n}";

    public static McpServerTool Create() => Create<GetGraphTool>("get_graph", Text, new JsonObject());

    /// <summary>What get_graph lists for the bricks' parameters (samples/graphs/bricks.texgraph) after its head line, before the file.</summary>
    public const string WorkedBricksParameters =
        "parameters (3):\n"
        + "- jitter: a number, 0.25, from 0 to 1; drives layout.jitter\n"
        + "- mortar: a number, 0.1, from 0 to 0.45; drives layout.gap\n"
        + "- tint: a colour, \"#a8452f\", any colour \"#rrggbb\"; drives clay.colour";

    /// <summary>A line for each exposed parameter, its kind, value, range and drives, under a count line; nothing for a graph without. A parameter at fault says so.</summary>
    public static string ParameterLines(TextureGraph graph, NodeRegistry registry)
    {
        if (graph.Parameters.Count == 0) return "";
        var exposed = GraphParameters.Of(graph, registry);
        var lines = graph.Parameters.Select(p => exposed.TryGetValue(p.Key, out var e)
            ? $"- {p.Key}: {e.KindText}, {e.Value}, {Range(e)}; drives {string.Join(", ", e.Drives)}"
            : $"- {p.Key}: at fault (see the faults), {p.Value.Value}; drives {(p.Value.Drives.Length == 0 ? "nothing" : string.Join(", ", p.Value.Drives))}");
        return $"parameters ({graph.Parameters.Count.ToString(CultureInfo.InvariantCulture)}):\n" + string.Join("\n", lines) + "\n";
    }

    /// <summary>A line for each graph node (T-140): its resolved file, the nested graph's parameters, inputs and outputs; nothing for a graph without.</summary>
    public static string UseLines(TextureGraph graph, NodeRegistry registry, bool neverSaved)
    {
        var uses = GraphValidator.Uses(graph, registry);
        if (uses.Count == 0) return "";
        var against = neverSaved ? $"the tool folder {graph.Folder} (the graph has never been saved)" : $"the graph's own folder {graph.Folder}";
        var lines = uses.Select(u =>
        {
            var head = $"- {u.Node}: {u.File ?? "no file (see the faults)"}";
            if (!u.Expanded) return head + "; at fault (see the faults)";
            var parameters = u.Parameters.Count == 0 ? "none" : string.Join("; ", u.Parameters.Values.Select(e => $"{e.Name} {e.KindText}, {e.Value}, {Range(e)}"));
            var inputs = u.Inputs.Count == 0 ? "none" : string.Join(", ", u.Inputs.Select(i => $"{i.Name} ({Patina.Graphs.Imaging.PortKinds.Name(i.Kind)}{(i.HasDefault ? ", has a default" : ", must be connected")})"));
            return $"{head}; parameters: {parameters}; inputs: {inputs}; outputs: {string.Join(", ", u.Outputs.Select(o => u.Node + "." + o))}";
        });
        return $"graph nodes ({uses.Count.ToString(CultureInfo.InvariantCulture)}), paths resolved against {against}:\n" + string.Join("\n", lines) + "\n";
    }

    private static string Range(ExposedParameter e) => e.Kind switch
    {
        ParamKind.Float or ParamKind.Int => $"from {TexGraphFile.FormatNumber(e.Range.Min)} to {TexGraphFile.FormatNumber(e.Range.Max)}",
        ParamKind.IntPair => $"each from {TexGraphFile.FormatNumber(e.Range.Min)} to {TexGraphFile.FormatNumber(e.Range.Max)}",
        ParamKind.Enum => "one of " + string.Join(", ", e.Range.Choices),
        _ => "any colour \"#rrggbb\"",
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        if (Unknown("get_graph", context) is { } unknown) return unknown;
        lock (store.Gate)
        {
            if (store.Graph is not { } graph) return Error("get_graph: " + NoGraph);
            var path = store.Path is { } p ? "path " + p : "no path (save gives it one)";
            return Line($"get_graph: {Describe(graph)}; {path}; {Faults(GraphValidator.Validate(graph, registry))}\n{ParameterLines(graph, registry)}{UseLines(graph, registry, store.Path is null)}{TexGraphFile.WriteShowingDriven(graph).TrimEnd('\n')}");
        }
    }
}

public sealed class SaveTool(GraphStore store, NodeRegistry registry, ToolFolder folder)
{
    public const string Worked = """{"path":"planks"}""";

    /// <summary>save's result for the worked example, with &lt;folder&gt; for the working directory.</summary>
    public const string WorkedResult = "save: saved planks.texgraph to <folder>/planks.texgraph: 6 nodes, size 256, seed 0";

    public const string Text =
        "Writes the open graph to a .texgraph file, so the work outlives this session; open reads it back. Takes path: .texgraph is added when the path does not end in it; "
        + "a relative path resolves against patina's working directory, and the result gives the absolute path; missing folders are created; an existing file is replaced, and the result says (replaced). "
        + "The file is the graph as get_graph shows it: versioned JSON, keys in one order, one node a line, so the same graph always gives the same bytes and a diff shows what changed. "
        + "The path becomes the graph's path. Refused, writing nothing, while the graph has a fault: a saved graph always evaluates, but for an input node with no default, which a graph using this one as a node gives. Only the open graph is written, never a file it uses as a graph node. The error lists the faults, each naming its node and port or parameter; fix them with edit_graph. "
        + "Also refused: no open graph, an empty path, one naming a folder or holding a character a path may not, and a write that fails (naming the path and the system's message; no partial file is left). "
        + "save changes nothing in the graph or the snapshot stack, which is never saved.\n"
        + "Worked example, after define_graph's and edit_graph's worked examples (put patina's working directory for <folder>):\n"
        + "save " + Worked + "\nreturns: " + WorkedResult;

    public static McpServerTool Create() => Create<SaveTool>("save", Text, new JsonObject
    {
        ["path"] = new JsonObject { ["type"] = "string", ["description"] = "The file to write, absolute or relative to patina's working directory; .texgraph is added if missing." },
    }, "path");

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null)
    {
        if (Unknown("save", context, "path") is { } unknown) return unknown;
        if (!TryText("save", "path", path, out var text, out var bad)) return bad!;
        lock (store.Gate)
        {
            if (store.Graph is not { } graph) return Error("save: " + NoGraph);
            var target = Resolve("save", text, folder.Directory, out var pathError);
            if (target is null) return Error(pathError!);
            // A fault that only stops the graph evaluating on its own (an input with no default, T-140) does not stop the save: a graph using it as a node gives the input.
            var faults = GraphValidator.Validate(graph, registry).Where(f => !f.OnItsOwn).ToList();
            if (faults.Count > 0)
                return Error($"save: refused: the graph has {Faults(faults)}\nA saved graph evaluates; fix them with edit_graph, then save again. Nothing was written.");
            if (!Write("save", target, TexGraphFile.Write(graph), out var replaced, out var writeError)) return Error(writeError!);
            store.Saved(target);
            return Line($"save: saved {Path.GetFileName(target)} to {target}{(replaced ? " (replaced)" : "")}: {Describe(graph)}");
        }
    }
}

public sealed class OpenTool(GraphStore store, NodeRegistry registry, ToolFolder folder)
{
    /// <summary>How many file names a missing file's error lists before "and N more".</summary>
    public const int ListLimit = 20;

    public const string Worked = """{"path":"planks.texgraph"}""";
    public const string WorkedResult = "open: opened planks.texgraph from <folder>/planks.texgraph: 6 nodes, size 256, seed 0; no faults: it evaluates";
    public const string WorkedMissing = """{"path":"plank"}""";
    public const string WorkedMissingResult = "open: no file at <folder>/plank.texgraph; the .texgraph files in <folder> (1) are: planks.texgraph";

    public const string Text =
        "Replaces the open graph with the one in a .texgraph file (save writes them). Takes path: .texgraph is added when the path does not end in it, and a relative path resolves against patina's working directory. "
        + "The file's path becomes the graph's path, and the snapshot stack is cleared. A graph node's path resolves against the file's own folder; the result names each nested file it read. "
        + "A graph whose references are wrong (an input naming no node, an unknown type or parameter, a value out of range, a cycle) still opens, and the result lists its faults one a line, "
        + "each naming its node and port or parameter, so edit_graph can fix them; save refuses it until they are fixed. "
        + "Refused, leaving the open graph as it was: a missing file (the error lists the .texgraph files in its folder, so a misspelt name shows the right one); "
        + "a file that is not a graph (not JSON, a missing or unknown key, a version other than 1, a bad size or seed), giving the line and column; an empty path, or one naming a folder.\n"
        + "Worked example, after save's worked example (put patina's working directory for <folder>):\n"
        + "open " + Worked + "\nreturns: " + WorkedResult + "\n"
        + "open " + WorkedMissing + " is refused: " + WorkedMissingResult;

    public static McpServerTool Create() => Create<OpenTool>("open", Text, new JsonObject
    {
        ["path"] = new JsonObject { ["type"] = "string", ["description"] = "The .texgraph file to read, absolute or relative to patina's working directory; .texgraph is added if missing." },
    }, "path");

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null)
    {
        if (Unknown("open", context, "path") is { } unknown) return unknown;
        if (!TryText("open", "path", path, out var text, out var bad)) return bad!;
        var target = Resolve("open", text, folder.Directory, out var pathError);
        if (target is null) return Error(pathError!);
        if (!File.Exists(target)) return Error(Missing(target));

        string content;
        try
        {
            content = File.ReadAllText(target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Error($"open: could not read {target}: {e.Message}");
        }

        var read = TexGraphFile.Read(content, registry, null, Path.GetDirectoryName(target));
        if (read.Graph is null)
            return Error($"open: {target} is not a graph patina reads: {read.Error}; the open graph is unchanged");
        lock (store.Gate)
        {
            store.Replace(read.Graph, target);
            var faults = read.Faults.Count == 0
                ? Faults(read.Faults)
                : Faults(read.Faults) + "\nIt is open: fix them with edit_graph (save refuses it until then).";
            var nested = GraphValidator.Uses(store.Graph!, registry).SelectMany(u => u.Files).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var files = nested.Count == 0 ? "" : $"\nread {nested.Count.ToString(CultureInfo.InvariantCulture)} nested file{(nested.Count == 1 ? "" : "s")} for its graph nodes: {string.Join(", ", nested)}";
            return Line($"open: opened {Path.GetFileName(target)} from {target}: {Describe(read.Graph)}; {faults}{files}");
        }
    }

    /// <summary>The missing-file error, as carve's open gives it (T-81): the resolved path and the .texgraph files in its folder.</summary>
    private static string Missing(string target)
    {
        var where = Path.GetDirectoryName(target)!;
        var head = $"open: no file at {target}; ";
        if (!Directory.Exists(where)) return head + $"the folder {where} does not exist";
        List<string> names;
        try
        {
            names = Directory.EnumerateFiles(where)
                .Select(f => Path.GetFileName(f))
                .Where(n => n.EndsWith(TexGraphFile.Extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return head + $"the folder {where} could not be listed: {e.Message}";
        }
        var inv = CultureInfo.InvariantCulture;
        if (names.Count == 0) return head + $"there are no {TexGraphFile.Extension} files in {where}";
        var rest = names.Count > ListLimit ? $", and {(names.Count - ListLimit).ToString(inv)} more" : "";
        return head + $"the {TexGraphFile.Extension} files in {where} ({names.Count.ToString(inv)}) are: {string.Join(", ", names.Take(ListLimit))}{rest}";
    }
}

public sealed class PushSnapshotTool(GraphStore store)
{
    public const string Worked = """{"label":"before grain"}""";
    public const string WorkedResult = "push_snapshot: pushed 'before grain'; depth 1";

    public const string Text =
        "Copies the open graph onto a stack of snapshots, to try a change and come back: push, edit freely, then pop_snapshot restores the graph exactly as it was pushed. "
        + "Takes label, optional, a name for the snapshot that pop_snapshot gives back. Returns the stack's depth. "
        + "The stack lives in this session only: it is never saved, and define_graph and open clear it. The open graph and its path are unchanged. Refused when there is no open graph.\n"
        + "Worked example, after define_graph's worked example:\n"
        + "push_snapshot " + Worked + "\nreturns: " + WorkedResult + "\n"
        + "then edit_graph's worked example adds the grain, and pop_snapshot {} takes the graph back to before it.";

    public static McpServerTool Create() => Create<PushSnapshotTool>("push_snapshot", Text, new JsonObject
    {
        ["label"] = new JsonObject { ["type"] = "string", ["description"] = "Optional: a name for the snapshot, given back by pop_snapshot." },
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? label = null)
    {
        if (Unknown("push_snapshot", context, "label") is { } unknown) return unknown;
        if (!TryText("push_snapshot", "label", label, out var text, out var bad)) return bad!;
        lock (store.Gate)
        {
            if (store.Graph is null) return Error("push_snapshot: " + NoGraph);
            var depth = store.Push(text);
            return Line($"push_snapshot: pushed {Label(text)}; depth {depth.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    public static string Label(string? label) => label is null ? "a snapshot with no label" : $"'{label}'";
}

public sealed class PopSnapshotTool(GraphStore store, NodeRegistry registry)
{
    public const string WorkedResult = "pop_snapshot: restored 'before grain'; depth 0. The graph is 4 nodes, size 256, seed 0; no faults: it evaluates";

    public const string Empty = "pop_snapshot: the snapshot stack is empty; push_snapshot first, before the change to come back from";

    public const string Text =
        "Restores the graph on top of the snapshot stack (push_snapshot) as the open graph, byte for byte as it was pushed, and removes it from the stack. "
        + "Takes nothing. Returns the snapshot's label, the new depth, and the restored graph's nodes, size, seed and faults. The graph's path is kept; nothing is written to a file. "
        + "On an empty stack it is an error that says so, and changes nothing.\n"
        + "Worked example, after push_snapshot's: edit_graph's worked example adds the grain (6 nodes), then pop_snapshot {} returns:\n"
        + WorkedResult + "\n"
        + "and pop_snapshot {} again is refused: " + Empty;

    public static McpServerTool Create() => Create<PopSnapshotTool>("pop_snapshot", Text, new JsonObject());

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        if (Unknown("pop_snapshot", context) is { } unknown) return unknown;
        lock (store.Gate)
        {
            if (store.Pop() is not { } top) return Error(Empty);
            return Line($"pop_snapshot: restored {PushSnapshotTool.Label(top.Label)}; depth {store.Depth.ToString(CultureInfo.InvariantCulture)}. The graph is {State(top.Graph, registry)}");
        }
    }
}
