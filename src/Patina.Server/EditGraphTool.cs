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

    /// <summary>The parameters' worked example (T-139), on samples/graphs/bricks.texgraph, whose mortar, tint and jitter are exposed: grey stone bricks.</summary>
    public const string WorkedParameters = """{"operations":[{"op":"set_parameters","values":{"mortar":0.08,"tint":"#7a7a70"}}]}""";

    public const string WorkedParametersResult =
        "edit_graph: 1 operation applied: set parameters mortar 0.08 (was 0.1), tint \"#7a7a70\" (was \"#a8452f\"). The graph is 18 nodes, size 256, seed 21; no faults: it evaluates";

    /// <summary>The other parameter operations' shapes, each run by a test on the bricks.</summary>
    public const string SetParameterExample = """{"op":"set_parameter","name":"variation","value":0.7,"drives":["tone.out_low"],"max":0.9}""";
    public const string RemoveParameterExample = """{"op":"remove_parameter","name":"jitter"}""";

    /// <summary>The list parameters' worked example (T-171), after define_graph's gradient_map one: the middle stop moved and made lighter, in place.</summary>
    public const string WorkedList =
        """{"operations":[{"op":"set_params","node":"tint","params":{"positions":[0,0.4,1],"colours":["#1c2b24","#6b8f55","#b9c9a8"]}}]}""";

    public const string WorkedListResult = "edit_graph: 1 operation applied: set 'tint' positions [0, 0.4, 1], colours [\"#1c2b24\", \"#6b8f55\", \"#b9c9a8\"]. The graph is 4 nodes, size 256, seed 0; no faults: it evaluates";

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
        + "- set_parameter {name, value, drives, min, max}: adds the exposed parameter name, or changes the keys given. drives is a list of node parameters, each \"node.param\", all of one kind (a number, a whole number, a choice, two whole numbers or a colour; not a list); "
        + "the parameter takes the narrowest range of them (the intersection, or an enum's common choices), narrowed again by min and max (numbers; null clears one), and every driven parameter takes its value, its own value in its node left out. "
        + "A new parameter needs value and drives. A node parameter a parameter stops driving keeps the parameter's value as its own.\n"
        + "- remove_parameter {name}: removes it; the parameters it drove keep its value as their own.\n"
        + "- set_parameters {values}: sets several parameters' values by name at once, such as {\"mortar\": 0.08}.\n"
        + "A value out of a parameter's range is a new fault (parameters['mortar']: is 0.5, but must be a number from 0 to 0.45), so the list is refused. get_graph lists the parameters; swatch and export take them for one call without editing the graph.\n"
        + "For example " + SetParamsExample + " gives tone another seed, " + DisconnectExample + " on its own is refused (a blend's b is required, so leaving it open is a new fault), and " + SetExample + " doubles the size and changes the seed. "
        + "A list parameter (a gradient_map's positions or colours) is given whole in the same shape as define_graph reads it, an array of numbers or of strings, and replaces the list; give both lists of a gradient_map in one operation when the count changes. "
        + "Types, ports and parameters are define_graph's. Returns one line: what each operation did, then the graph's nodes, size, seed and faults.\n"
        + "Worked example, after define_graph's worked example: a grain running along v, multiplied in at 0.3:\n"
        + "edit_graph " + Worked + "\n"
        + "returns: " + WorkedResult + "\n"
        + "Then edit_graph " + WorkedRefused + " changes nothing and returns:\n" + WorkedRefusedResult + "\n"
        + "Worked example, after open of samples/graphs/bricks.texgraph (its parameters mortar, tint and jitter drive layout.gap, clay.colour and layout.jitter), grey stone bricks:\n"
        + "edit_graph " + WorkedParameters + "\n"
        + "returns: " + WorkedParametersResult + "\n"
        + "Worked example, after define_graph's mossy gradient_map example: its middle stop moved to 0.4 and made lighter, the other stops as they were:\n"
        + "edit_graph " + WorkedList + "\n"
        + "returns: " + WorkedListResult + "\n"
        + "Then the operation " + SetParameterExample + " exposes tone's out_low as variation, from 0 to 0.9 (tone's own range is wider), and " + RemoveParameterExample + " leaves layout.jitter at 0.25 as its own.";

    public static McpServerTool Create() => Create<EditGraphTool>("edit_graph", Text, new JsonObject
    {
        ["operations"] = new JsonObject
        {
            ["type"] = "array",
            ["description"] = "The operations, applied in order, all or nothing: each an object with op (set_node, remove_node, connect, disconnect, set_params, set, set_parameter, remove_parameter or set_parameters) and its keys.",
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
        ["set_parameter"] = ["name", "value", "drives", "min", "max"],
        ["remove_parameter"] = ["name"],
        ["set_parameters"] = ["values"],
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
    private static (string?, string?, string?, string?) Key(GraphFault fault) => (fault.Node, fault.Port, fault.Parameter, fault.GraphParameter);

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
            if (op == "set_parameters") return SetParameters(operation, ref g, Fail);
            if (op is "set_parameter" or "remove_parameter") return Parameter(op, operation, ref g, Fail);

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

    private static string Names(TextureGraph graph) => graph.Parameters.Count == 0 ? "none" : string.Join(", ", graph.Parameters.Keys);

    private static string? SetParameters(JsonElement operation, ref TextureGraph graph, Func<string, string?> fail)
    {
        if (!operation.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Object)
            return fail("values is missing; give an object of the parameters' names to values, such as {\"mortar\": 0.08}");
        var said = new List<string>();
        foreach (var p in values.EnumerateObject())
        {
            if (!graph.Parameters.TryGetValue(p.Name, out var existing))
                return fail($"there is no parameter '{p.Name}'; the parameters are {Names(graph)}");
            if (ParameterOverrides.Read(p.Value) is not { } value)
                return fail($"parameter '{p.Name}'" + ParameterOverrides.WhyNot(p.Value));
            said.Add($"{p.Name} {value} (was {existing.Value})");
            graph = graph.WithParameter(p.Name, existing with { Value = value });
        }
        return said.Count == 0 ? fail("values is empty; give at least one parameter") : "set parameters " + string.Join(", ", said);
    }

    private static string? Parameter(string op, JsonElement operation, ref TextureGraph graph, Func<string, string?> fail)
    {
        if (!TryString(operation, "name", out var name)) return fail("name is missing; give the parameter's name, as a string");
        var existing = graph.Parameters.GetValueOrDefault(name);
        if (op == "remove_parameter")
        {
            if (existing is null) return fail($"there is no parameter '{name}'; the parameters are {Names(graph)}");
            graph = graph.WithoutParameter(name);
            var kept = Release(ref graph, existing.Drives, existing.Value);
            return $"removed parameter '{name}'" + (kept.Count == 0 ? "" : $"; {string.Join(", ", kept)} keep{(kept.Count == 1 ? "s" : "")} {existing.Value}");
        }

        var said = new List<string>();
        var parameter = existing ?? new GraphParameter(ParamValue.Of(0), []);
        if (operation.TryGetProperty("value", out var valueArg))
        {
            if (ParameterOverrides.Read(valueArg) is not { } value) return fail("value" + ParameterOverrides.WhyNot(valueArg));
            said.Add($"value {value}" + (existing is null ? "" : $" (was {existing.Value})"));
            parameter = parameter with { Value = value };
        }
        else if (existing is null) return fail("value is missing; a new parameter needs value and drives");
        if (operation.TryGetProperty("drives", out var drivesArg))
        {
            if (drivesArg.ValueKind != JsonValueKind.Array || drivesArg.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
                return fail($"drives is {Kind(drivesArg)}; it is an array of node parameters, each \"node.param\", such as [\"layout.gap\"]");
            parameter = parameter with { Drives = [.. drivesArg.EnumerateArray().Select(e => e.GetString()!)] };
            said.Add($"drives {string.Join(", ", parameter.Drives)}" + (existing is null ? "" : $" (was {string.Join(", ", existing.Drives)})"));
        }
        else if (existing is null) return fail("drives is missing; a new parameter needs value and drives");
        foreach (var key in new[] { "min", "max" })
        {
            if (!operation.TryGetProperty(key, out var bound)) continue;
            if (bound.ValueKind == JsonValueKind.Null)
            {
                parameter = key == "min" ? parameter with { Min = null } : parameter with { Max = null };
                said.Add($"{key} cleared");
                continue;
            }
            if (bound.ValueKind != JsonValueKind.Number || !double.IsFinite(bound.GetDouble()))
                return fail($"{key} is {Kind(bound)}; it is a number, or null to clear it");
            var n = bound.GetDouble();
            parameter = key == "min" ? parameter with { Min = n } : parameter with { Max = n };
            said.Add($"{key} {TexGraphFile.FormatNumber(n)}");
        }
        if (said.Count == 0) return fail("give value, drives, min or max to change");

        // What it stops driving keeps the old value as its own; what it starts driving drops its own.
        if (existing is not null) Release(ref graph, [.. existing.Drives.Where(d => !parameter.Drives.Contains(d))], existing.Value);
        foreach (var drive in parameter.Drives)
            if (GraphParameter.TryParseDrive(drive, out var id, out var param) && graph.Nodes.TryGetValue(id, out var node) && node.Params.ContainsKey(param))
                graph = graph.WithNode(id, node with { Params = node.Params.Remove(param) });
        graph = graph.WithParameter(name, parameter);
        return (existing is null ? $"added parameter '{name}' " : $"set parameter '{name}' ") + string.Join(", ", said);
    }

    /// <summary>Gives each of <paramref name="drives"/> that names a node <paramref name="value"/> as its own; the drives so kept.</summary>
    private static List<string> Release(ref TextureGraph graph, IEnumerable<string> drives, ParamValue value)
    {
        var kept = new List<string>();
        foreach (var drive in drives)
            if (GraphParameter.TryParseDrive(drive, out var id, out var param) && graph.Nodes.TryGetValue(id, out var node))
            {
                graph = graph.WithNode(id, node.WithParam(param, value));
                kept.Add(drive);
            }
        return kept;
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
            if (ParameterOverrides.Read(parameter.Value) is not { } read)
                return $"parameter '{parameter.Name}'" + ParameterOverrides.WhyNot(parameter.Value);
            parameters = parameters.SetItem(parameter.Name, read);
        }
        return null;
    }
}
