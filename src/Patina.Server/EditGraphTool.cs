using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Nodes;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

public sealed class EditGraphTool(GraphStore store, NodeRegistry registry)
{
    /// <summary>The worked example, on define_graph's worked graph: a grain along v multiplied in. Tests run it as written.</summary>
    public const string Worked =
        """{"operations":[{"op":"set_node","node":"grain","type":"noise","params":{"cells":[64,2],"octaves":2}},{"op":"set_node","node":"grained","type":"blend","inputs":{"a":"base","b":"grain"},"params":{"mode":"multiply","opacity":0.3}},{"op":"connect","node":"out","port":"in","from":"grained"}]}""";

    public const string WorkedResult =
        "edit_graph: 3 operations applied: added 'grain' (noise); added 'grained' (blend); connected 'out' input 'in' to 'grained' (was 'base'). The graph is 6 nodes, size 256, seed 0; no faults: it evaluates";

    /// <summary>The refused example after it: removing tone leaves base's input b naming no node.</summary>
    public const string WorkedRefused = """{"operations":[{"op":"remove_node","node":"tone"}]}""";

    public const string WorkedRefusedResult =
        "edit_graph: nothing changed: the operations would leave 1 new fault:\n- node 'base' input 'b': names 'tone', which is not a node; the nodes are base, grain, grained, out, wood";

    /// <summary>The other operations' shapes, each run by a test.</summary>
    public const string SetParamsExample = """{"op":"set_params","node":"tone","params":{"seed":3}}""";
    public const string DisconnectExample = """{"op":"disconnect","node":"grained","port":"b"}""";
    public const string SetExample = """{"op":"set","size":512,"seed":7}""";

    public const string Text =
        "Edits the open graph by a list of operations, applied in order and all or nothing: when any operation cannot be applied, or the list would leave a fault the graph did not have, "
        + "nothing changes and the result says why (the operation by number, or the new faults, each naming its node and port or parameter). "
        + "Faults the graph already had (an opened file's, say) do not stop an edit, so they can be fixed one list at a time; the result lists those left. "
        + "operations is an array of objects, each with op and its own keys:\n"
        + "- set_node {node, type, inputs, params}: adds the node with that id, or replaces it whole (inputs and params optional, as in define_graph).\n"
        + "- remove_node {node}: removes it; disconnect or reconnect what reads it in the same list, or that is a new fault.\n"
        + "- connect {node, port, from}: sets node's input port to read from (an id, or \"id.port\"), replacing what it read before.\n"
        + "- disconnect {node, port}: leaves the input unconnected (a fault unless the port is optional).\n"
        + "- set_params {node, params}: merges params into the node's, replacing those it names and keeping the rest.\n"
        + "- set {size, seed}: the graph's size or seed or both.\n"
        + "For example " + SetParamsExample + " gives tone another seed, " + DisconnectExample + " on its own is refused (a blend's b is required, so leaving it open is a new fault), and " + SetExample + " doubles the size and changes the seed. "
        + "Types, ports and parameters are define_graph's. Returns one line: what each operation did, then the graph's nodes, size, seed and faults.\n"
        + "Worked example, after define_graph's worked example: a grain running along v, multiplied in at 0.3:\n"
        + "edit_graph " + Worked + "\n"
        + "returns: " + WorkedResult + "\n"
        + "Then edit_graph " + WorkedRefused + " changes nothing and returns:\n" + WorkedRefusedResult;

    public static McpServerTool Create() => Create<EditGraphTool>("edit_graph", Text, new JsonObject
    {
        ["operations"] = new JsonObject
        {
            ["type"] = "array",
            ["description"] = "The operations, applied in order, all or nothing: each an object with op (set_node, remove_node, connect, disconnect, set_params or set) and its keys.",
            ["items"] = new JsonObject { ["type"] = "object" },
        },
    }, "operations");

    private static readonly Dictionary<string, string[]> Keys = new(StringComparer.Ordinal)
    {
        ["set_node"] = ["node", "type", "inputs", "params"],
        ["remove_node"] = ["node"],
        ["connect"] = ["node", "port", "from"],
        ["disconnect"] = ["node", "port"],
        ["set_params"] = ["node", "params"],
        ["set"] = ["size", "seed"],
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? operations = null)
    {
        if (Unknown("edit_graph", context, "operations") is { } unknown) return unknown;
        if (operations is not { ValueKind: JsonValueKind.Array } list)
            return Error("edit_graph: operations is missing; give an array of operations, such as [{\"op\": \"set_params\", \"node\": \"tone\", \"params\": {\"seed\": 3}}]"
                + (operations is { } other ? $", not {Kind(other)}" : ""));
        if (list.GetArrayLength() == 0) return Error("edit_graph: operations is empty; give at least one");

        lock (store.Gate)
        {
            if (store.Graph is not { } before) return Error("edit_graph: " + NoGraph);

            var graph = before;
            var done = new List<string>();
            var number = 0;
            foreach (var operation in list.EnumerateArray())
            {
                number++;
                var said = Apply(operation, ref graph, out var problem);
                if (said is null) return Error($"edit_graph: nothing changed: operation {number.ToString(CultureInfo.InvariantCulture)} {problem}");
                done.Add(said);
            }

            var had = GraphValidator.Validate(before, registry).Select(Key).ToHashSet();
            var faults = GraphValidator.Validate(graph, registry);
            var added = faults.Where(f => !had.Contains(Key(f))).ToList();
            if (added.Count > 0)
                return Error($"edit_graph: nothing changed: the operations would leave {Count(added.Count, "new fault")}:" + string.Concat(added.Select(f => "\n- " + f.Message)));

            store.Edit(graph);
            return Line($"edit_graph: {Count(done.Count, "operation")} applied: {string.Join("; ", done)}. The graph is {Describe(graph)}; {Faults(faults)}");
        }
    }

    /// <summary>A fault's place, which a new fault does not share with an old one: its node, port and parameter (the message may name ids an edit added).</summary>
    private static (string?, string?, string?) Key(GraphFault fault) => (fault.Node, fault.Port, fault.Parameter);

    /// <summary>Applies one operation to <paramref name="graph"/>: what it did, or null with <paramref name="problem"/> (starting "(op): ").</summary>
    private static string? Apply(JsonElement operation, ref TextureGraph graph, out string problem)
    {
        problem = "";
        if (operation.ValueKind != JsonValueKind.Object)
        {
            problem = $"is {Kind(operation)}; an operation is an object with op, such as {{\"op\": \"remove_node\", \"node\": \"grain\"}}";
            return null;
        }
        if (!operation.TryGetProperty("op", out var opValue) || opValue.ValueKind != JsonValueKind.String || !Keys.TryGetValue(opValue.GetString()!, out var keys))
        {
            problem = (operation.TryGetProperty("op", out var given) ? $"has op {given.GetRawText()}" : "has no op")
                + "; op is one of " + string.Join(", ", Keys.Keys);
            return null;
        }
        var op = opValue.GetString()!;
        string? failure = null;
        string? Fail(string message)
        {
            failure = $"({op}): {message}";
            return null;
        }

        var g = graph;
        string? result = Run();
        graph = g;
        problem = failure ?? "";
        return result;

        string? Run()
        {
            foreach (var property in operation.EnumerateObject())
                if (property.Name != "op" && !keys.Contains(property.Name))
                    return Fail($"unknown key '{property.Name}'; {op} takes {string.Join(", ", keys)}");

            if (op == "set") return Set(operation, ref g, Fail);

            if (!TryString(operation, "node", out var id)) return Fail("node is missing; give the node's id, as a string");
            var existing = g.Nodes.GetValueOrDefault(id);
            if (op != "set_node" && existing is null) return Fail($"there is no node '{id}'; the nodes are {Ids(g)}");

            switch (op)
            {
                case "set_node":
                {
                    if (!TryString(operation, "type", out var type)) return Fail("type is missing; give the node's type, as a string");
                    var inputs = ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal);
                    if (operation.TryGetProperty("inputs", out var inputsValue) && Inputs(inputsValue, ref inputs) is { } inputProblem) return Fail(inputProblem);
                    var parameters = ImmutableSortedDictionary.Create<string, ParamValue>(StringComparer.Ordinal);
                    if (operation.TryGetProperty("params", out var paramsValue) && Params(paramsValue, ref parameters) is { } paramProblem) return Fail(paramProblem);
                    g = g.WithNode(id, new GraphNode(type, inputs, parameters));
                    return existing is null ? $"added '{id}' ({type})" : $"replaced '{id}' ({type})";
                }
                case "remove_node":
                    g = g.WithoutNode(id);
                    return $"removed '{id}' ({existing!.Type})";
                case "connect":
                {
                    if (!TryString(operation, "port", out var port)) return Fail("port is missing; give the input port, as a string");
                    if (!TryString(operation, "from", out var from)) return Fail("from is missing; give the source, an id or \"id.port\", as a string");
                    var was = existing!.Inputs.GetValueOrDefault(port);
                    g = g.WithNode(id, existing.WithInput(port, from));
                    return $"connected '{id}' input '{port}' to '{from}'" + (was is null ? "" : $" (was '{was}')");
                }
                case "disconnect":
                {
                    if (!TryString(operation, "port", out var port)) return Fail("port is missing; give the input port, as a string");
                    if (!existing!.Inputs.TryGetValue(port, out var was))
                        return Fail($"node '{id}' input '{port}' is not connected; its connected inputs are "
                            + (existing.Inputs.Count == 0 ? "none" : string.Join(", ", existing.Inputs.Keys)));
                    g = g.WithNode(id, existing with { Inputs = existing.Inputs.Remove(port) });
                    return $"disconnected '{id}' input '{port}' (was '{was}')";
                }
                default: // set_params
                {
                    if (!operation.TryGetProperty("params", out var paramsValue)) return Fail("params is missing; give the parameters to set, as an object");
                    var parameters = existing!.Params;
                    if (Params(paramsValue, ref parameters) is { } paramProblem) return Fail(paramProblem);
                    g = g.WithNode(id, existing with { Params = parameters });
                    var named = paramsValue.EnumerateObject().Select(p => $"{p.Name} {parameters[p.Name]}");
                    return $"set '{id}' {string.Join(", ", named)}";
                }
            }
        }
    }

    private static string? Set(JsonElement operation, ref TextureGraph graph, Func<string, string?> fail)
    {
        var said = new List<string>();
        var inv = CultureInfo.InvariantCulture;
        if (operation.TryGetProperty("size", out var sizeValue))
        {
            if (sizeValue.ValueKind != JsonValueKind.Number || !sizeValue.TryGetInt32(out var size) || !TextureGraph.IsValidSize(size))
                return fail($"size {sizeValue.GetRawText()} is not {TextureGraph.SizesText}");
            said.Add($"size {size.ToString(inv)} (was {graph.Size.ToString(inv)})");
            graph = graph with { Size = size };
        }
        if (operation.TryGetProperty("seed", out var seedValue))
        {
            if (seedValue.ValueKind != JsonValueKind.Number || !seedValue.TryGetUInt32(out var seed))
                return fail($"seed {seedValue.GetRawText()} is not a whole number from 0 to 4294967295");
            said.Add($"seed {seed.ToString(inv)} (was {graph.Seed.ToString(inv)})");
            graph = graph with { Seed = seed };
        }
        return said.Count == 0 ? fail("give size, seed or both") : "set " + string.Join(", ", said);
    }

    private static bool TryString(JsonElement operation, string key, out string text)
    {
        text = "";
        if (!operation.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String) return false;
        text = value.GetString()!;
        return true;
    }

    private static string Ids(TextureGraph graph) => graph.Nodes.Count == 0 ? "none" : string.Join(", ", graph.Nodes.Keys);

    /// <summary>Reads an inputs object into <paramref name="inputs"/>; what is wrong with it, or null.</summary>
    private static string? Inputs(JsonElement value, ref ImmutableSortedDictionary<string, string> inputs)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return $"inputs is {Kind(value)}; it is an object of port: source, such as {{\"a\": \"wood\"}}";
        foreach (var port in value.EnumerateObject())
        {
            if (port.Value.ValueKind != JsonValueKind.String)
                return $"input '{port.Name}' is {Kind(port.Value)}; a source is a string, an id or \"id.port\"";
            inputs = inputs.SetItem(port.Name, port.Value.GetString()!);
        }
        return null;
    }

    /// <summary>Merges a params object into <paramref name="parameters"/>; what is wrong with it, or null.</summary>
    private static string? Params(JsonElement value, ref ImmutableSortedDictionary<string, ParamValue> parameters)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return $"params is {Kind(value)}; it is an object of name: value, such as {{\"seed\": 3}}";
        foreach (var parameter in value.EnumerateObject())
        {
            var v = parameter.Value;
            ParamValue? read = v.ValueKind switch
            {
                JsonValueKind.Number when double.IsFinite(v.GetDouble()) => ParamValue.Of(v.GetDouble()),
                JsonValueKind.String => ParamValue.Of(v.GetString()!),
                JsonValueKind.Array when v.EnumerateArray().All(e => e.ValueKind == JsonValueKind.Number) =>
                    ParamValue.Of([.. v.EnumerateArray().Select(e => e.GetDouble())]),
                _ => null,
            };
            if (read is null) return $"parameter '{parameter.Name}' is {Kind(v)}; a value is a number, a string or an array of numbers";
            parameters = parameters.SetItem(parameter.Name, read);
        }
        return null;
    }
}
