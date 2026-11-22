using System.Collections.Immutable;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs;

/// <summary>
/// Everything wrong with a graph, all at once: each fault names its node and its port or parameter. In node id order:
/// a bad id, an unknown type (listing the types), an unknown parameter (listing the type's) or a wrong value (giving
/// the range), an input port the type has not (listing its inputs), a source that is not a node (listing the ids) or
/// names a port that node has not, a required input missing; then each cycle (naming the nodes on it), each input
/// given the wrong kind (greyscale where colour is wanted, or the other way), an output given both a role and a name, a
/// name that is a role's or the reserved metallic_roughness, two outputs with one role or one name, a metallic output with no roughness to be packed with,
/// and no output at all; then each exposed parameter's faults (T-139), in name order, each naming it
/// (<c>parameters['mortar']: ...</c>, <see cref="GraphFault.GraphParameter"/>): a bad name, no drives, a drive that is not
/// <c>"node.param"</c> or names no node or no parameter, a node parameter driven twice, a kind that cannot be driven (a
/// list or a name), two kinds in one parameter, min or max on a kind that is not a number, no range in common, and a
/// value out of the range. A node parameter a parameter drives takes its value, and its own value in the node is not
/// checked. Every input whose source is a node of a known type is checked for kinds, even when another input of its node
/// is at fault or its node is on a cycle; a fault does not cascade: an input whose source is missing, of unknown type or on a cycle is not kind-checked,
/// nor is anything its kind would decide (a port whose kind it could change, and what that port feeds).
/// </summary>
public static class GraphValidator
{
    /// <summary>How deep graphs used as nodes may nest: a graph using one using one ... 8 files down.</summary>
    public const int MaxDepth = 8;

    /// <summary>
    /// A cap on what nesting takes in (RV-76, RV-77): the graph's own nodes count toward it, but it is checked only as each
    /// nested file is expanded or read for a drive's range, all levels down, so a graph of its own nodes alone is never
    /// held to it. A file using another many times at each level would otherwise expand exponentially and hang where a
    /// fault is wanted. A graph node whose expansion would pass it is a fault on its <c>path</c>; a drive whose read would,
    /// a fault on its parameter.
    /// </summary>
    public const int MaxNodes = 4096;

    public static IReadOnlyList<GraphFault> Validate(TextureGraph graph, NodeRegistry? registry = null) =>
        Analyse(graph, registry ?? NodeRegistry.BuiltIn).Faults;

    /// <summary>
    /// Each <c>graph</c> node (T-140) as validation resolved it, in id order: its file, the nested graph's exposed
    /// parameters, inputs and outputs, and the nested files read for it, all levels down; get_graph and open show them.
    /// </summary>
    public static IReadOnlyList<NestedGraphUse> Uses(TextureGraph graph, NodeRegistry? registry = null) =>
        [.. Analyse(graph, registry ?? NodeRegistry.BuiltIn).Uses.OrderBy(u => u.Key, StringComparer.Ordinal).Select(u => u.Value.Public)];

    /// <summary>The absolute path a <c>graph</c> node's <paramref name="path"/> names: against <see cref="TextureGraph.Folder"/> (the working directory when null), <c>.texgraph</c> added when missing; null for a path that is not one, or is not relative (<see cref="ParamSpec.IsRelativePath"/>).</summary>
    public static string? ResolvePath(TextureGraph graph, string path)
    {
        if (!ParamSpec.IsRelativePath(path)) return null;
        var text = path.EndsWith(TexGraphFile.Extension, StringComparison.OrdinalIgnoreCase) ? path : path + TexGraphFile.Extension;
        try
        {
            return Path.GetFullPath(Path.Combine(graph.Folder ?? Directory.GetCurrentDirectory(), text));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    internal static GraphAnalysis Analyse(TextureGraph graph, NodeRegistry registry) => Analyse(graph, registry, [], 0, new NodeBudget(graph.Nodes.Count));

    /// <summary>
    /// The nodes taken in so far by one validation, all levels down, against <see cref="MaxNodes"/>: each nested file
    /// expanded, and each nested file read for a drive's range (RV-77), so neither path can grow past it. A file read for
    /// a drive is read once a validation for each chain of files above it (<see cref="NestedParameters"/>).
    /// </summary>
    internal sealed class NodeBudget(int taken)
    {
        public int Taken { get; set; } = taken;

        /// <summary>The nested files read for drives, by the chain of files above and the file: what <see cref="NestedParameters"/> gave.</summary>
        public Dictionary<string, NestedParameterSet?> Read { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static GraphAnalysis Analyse(TextureGraph graph, NodeRegistry registry, IReadOnlyList<string> chain, int depth, NodeBudget budget)
    {
        var a = new GraphAnalysis();
        var ids = string.Join(", ", graph.Nodes.Keys);
        // The exposed parameters first: what they drive takes their value, and its own value in the node is not checked.
        var exposed = GraphParameters.Analyse(graph, registry, chain, budget);

        // Each graph node expanded inline (T-140): the nodes analysed are the graph's own and, for each graph node, the
        // nested graph's nodes renamed <node>/<id>. A graph node is never one of them; a source naming it resolves to the
        // nested output's own source.
        var nodes = new SortedDictionary<string, GraphNode>(StringComparer.Ordinal);
        var useFaults = new Dictionary<string, List<GraphFault>>(StringComparer.Ordinal);
        var wiredHere = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, node) in graph.Nodes)
        {
            // The graph's own node with an id expansion also makes (an outer "g/c" beside graph node "g" whose file has "c",
            // RV-76) stays the graph's own, so its id is checked as one, and the nested node is not put over it.
            if (registry.Find(node.Type) is not NestedGraphNode) { nodes[id] = node; continue; }
            var faults = useFaults[id] = [];
            a.Uses[id] = Expand(id, node, graph, registry, chain, depth, budget, faults, nodes, a, wiredHere, exposed);
        }

        foreach (var id in nodes.Keys.Concat(useFaults.Keys).Order(StringComparer.Ordinal))
        {
            if (useFaults.TryGetValue(id, out var fromUse)) { a.Faults.AddRange(fromUse); continue; }
            var node = nodes[id];
            var isExpanded = a.Expanded.Contains(id) && !graph.Nodes.ContainsKey(id);
            if (!(isExpanded ? GraphNames.IsValidPath(id) : GraphNames.IsValid(id)))
                a.Fault(id, null, null, $"node '{id}': an id is {GraphNames.Pattern} (lower case letters, digits and _, starting with a letter)");
            var type = registry.Find(node.Type);
            if (type is null)
            {
                a.Fault(id, null, null, $"node '{id}': unknown type '{node.Type}'; the types are {string.Join(", ", registry.Names)}");
                continue;
            }
            var values = new Dictionary<string, ParamValue>(node.Params, StringComparer.Ordinal);
            foreach (var spec in type.Parameters)
                if (exposed.DrivenBy.ContainsKey((id, spec.Name)))
                {
                    values.Remove(spec.Name);
                    if (exposed.Values.TryGetValue((id, spec.Name), out var drivenValue)) values[spec.Name] = drivenValue;
                }
            foreach (var (name, value) in node.Params)
            {
                if (exposed.DrivenBy.ContainsKey((id, name))) continue;
                var spec = type.Parameter(name);
                if (spec is null)
                    a.Fault(id, null, name, $"node '{id}' parameter '{name}': {type.Name} has no such parameter; its parameters are {List(type.Parameters.Select(p => p.Name))}");
                else if (spec.Check(value) is { } wrong)
                    a.Fault(id, null, name, $"node '{id}' parameter '{name}' {wrong}");
            }
            var resolved = new ResolvedNode(id, type, new NodeParameters(type, values), values);
            if (type is InputNode && !isExpanded) InputFaults(a, id, node, resolved.Parameters);
            resolved.Connected.UnionWith(node.Inputs.Keys);
            var faultsBefore = a.Faults.Count;
            var sourceOfUnknownType = false;
            foreach (var (port, source) in node.Inputs)
            {
                if (type.Input(port) is null)
                {
                    a.Fault(id, port, null, $"node '{id}' input '{port}': {type.Name} has no such input; its inputs are {List(type.Inputs.Select(p => p.Name))}");
                    continue;
                }
                // An expanded node's sources are expanded ids; a source a file gives (this graph's own nodes, and what a
                // graph node's input was given) is a plain one.
                var plain = !isExpanded || wiredHere.Contains(id);
                string from;
                string? fromPort;
                if (!(plain ? GraphNames.TryParseSource(source, out from, out fromPort) : GraphNames.TryParsePathSource(source, out from, out fromPort)))
                {
                    a.Fault(id, port, null, $"node '{id}' input '{port}': '{source}' is not a source; a source is \"<id>\" or \"<id>.<port>\"");
                    continue;
                }
                if (plain && a.Uses.TryGetValue(from, out var use))
                {
                    if (use.Failed) { sourceOfUnknownType = true; continue; } // its own faults say why
                    if (fromPort is null)
                    {
                        if (use.Outputs.Count == 1) fromPort = use.Outputs.Keys.Single();
                        else
                        {
                            a.Fault(id, port, null, $"node '{id}' input '{port}': names '{from}', a graph node using {use.FileName}, which has several outputs, so name one: {List(use.Order.Select(o => from + "." + o))}");
                            continue;
                        }
                    }
                    if (!use.Outputs.TryGetValue(fromPort, out var inner))
                    {
                        a.Fault(id, port, null, $"node '{id}' input '{port}': names '{source}', but {use.FileName} has no output '{fromPort}'; its outputs are {List(use.Order.Select(o => from + "." + o))}");
                        continue;
                    }
                    resolved.Sources[port] = inner;
                    continue;
                }
                if (!nodes.TryGetValue(from, out var target) || (plain && a.Expanded.Contains(from)))
                {
                    a.Fault(id, port, null, $"node '{id}' input '{port}': names '{from}', which is not a node; the nodes are {ids}");
                    continue;
                }
                var targetType = registry.Find(target.Type);
                if (targetType is null) { sourceOfUnknownType = true; continue; } // its own fault says so, and this input is not kind-checked
                if (fromPort is null)
                {
                    if (targetType.Outputs.Count == 1) fromPort = targetType.Outputs[0];
                    else
                    {
                        a.Fault(id, port, null, targetType.Outputs.Count == 0
                            ? $"node '{id}' input '{port}': names '{from}', a node of type {targetType.Name}, which has no output port"
                            : $"node '{id}' input '{port}': names '{from}', which has several output ports, so name one: {List(targetType.Outputs.Select(o => from + "." + o))}");
                        continue;
                    }
                }
                else if (!targetType.Outputs.Contains(fromPort))
                {
                    a.Fault(id, port, null, targetType.Outputs.Count == 0
                        ? $"node '{id}' input '{port}': names '{source}', but '{from}', a node of type {targetType.Name}, has no output port"
                        : $"node '{id}' input '{port}': names '{source}', but '{from}' has no port '{fromPort}'; its output ports are {List(targetType.Outputs)}");
                    continue;
                }
                resolved.Sources[port] = (from, fromPort);
            }
            foreach (var input in type.Inputs.Where(p => p.Required && !node.Inputs.ContainsKey(p.Name)))
                a.Fault(id, input.Name, null, $"node '{id}' input '{input.Name}': {type.Name} needs it connected, and it is not");
            resolved.InputsAtFault = sourceOfUnknownType || a.Faults.Skip(faultsBefore).Any(f => f.Port is not null);
            a.Nodes[id] = resolved;
        }

        Cycles(a);
        Kinds(a);
        Outputs(a, nodes);
        InputNames(a);
        a.Faults.AddRange(exposed.Faults);
        // A fault on what a graph node's input was given is named on the graph node's input (wall's in), not on the
        // nested input node's port.
        for (var i = 0; i < a.Faults.Count; i++)
            if (a.Faults[i] is { Node: { } n, Port: InputNode.ValuePort } f && a.PassThrough.TryGetValue(n, out var at))
            {
                var head = $"node '{n}' input '{InputNode.ValuePort}':";
                var rest = f.Message.StartsWith(head, StringComparison.Ordinal) ? f.Message[head.Length..] : " " + f.Message;
                a.Faults[i] = f with { Node = at.Node, Port = at.Input, Message = $"node '{at.Node}' input '{at.Input}':{rest} ({at.FileName}'s input '{at.Input}')" };
            }
        return a;
    }

    /// <summary>An input node's own faults: no name, a default of the other kind, a file wiring its value port, and no default (which only stops the graph being evaluated on its own).</summary>
    private static void InputFaults(GraphAnalysis a, string id, GraphNode node, NodeParameters parameters)
    {
        if (!node.Params.ContainsKey("name"))
            a.Fault(id, null, "name", $"node '{id}' parameter 'name': an input needs a name, {GraphNames.Pattern}: the input port a graph node using this graph gives it");
        if (InputNode.CheckDefault(parameters) is { } wrong)
            a.Fault(id, null, "default", $"node '{id}' parameter 'default' {wrong}");
        var port = InputNode.PortName(parameters) ?? id;
        if (node.Inputs.ContainsKey(InputNode.ValuePort))
            a.Fault(id, InputNode.ValuePort, null, $"node '{id}' input '{InputNode.ValuePort}': an input node takes nothing in its own file; a graph node using this graph gives it, as its input '{port}'");
        else if (!node.Params.ContainsKey("default"))
            a.Faults.Add(new(id, null, "default", $"node '{id}' parameter 'default': input '{port}' has no default, so the graph cannot be evaluated on its own; give it a default, or use the graph as a graph node that connects '{port}'") { OnItsOwn = true });
    }

    /// <summary>Two input nodes of one name.</summary>
    private static void InputNames(GraphAnalysis a)
    {
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in a.Nodes.Values.Where(n => n.Type is InputNode && !a.Expanded.Contains(n.Id)).OrderBy(n => n.Id, StringComparer.Ordinal))
            if (InputNode.PortName(node.Parameters) is { } name && !byName.TryAdd(name, node.Id))
                a.Fault(node.Id, null, "name", $"node '{node.Id}' parameter 'name': {name} is already the name of input '{byName[name]}'; a graph has one input per name");
    }

    /// <summary>
    /// One graph node expanded: its file resolved and read, the values set on it checked against the nested graph's
    /// parameters and set through <see cref="GraphParameters.WithOverrides"/>, the nested graph analysed with the chain of
    /// files above it, and its nodes added to <paramref name="nodes"/> as <c>&lt;id&gt;/&lt;nested id&gt;</c> with their
    /// sources resolved and driven parameters set. Faults go to <paramref name="faults"/>; a use with any is
    /// <see cref="GraphUse.Failed"/>, and nothing of it is added.
    /// </summary>
    private static GraphUse Expand(string id, GraphNode node, TextureGraph graph, NodeRegistry registry, IReadOnlyList<string> chain, int depth, NodeBudget budget,
        List<GraphFault> faults, SortedDictionary<string, GraphNode> nodes, GraphAnalysis a, HashSet<string> wiredHere, ParameterAnalysis exposed)
    {
        var use = new GraphUse(id);
        void Fault(string? port, string? parameter, string message) => faults.Add(new(id, port, parameter, message));
        if (!GraphNames.IsValid(id))
            Fault(null, null, $"node '{id}': an id is {GraphNames.Pattern} (lower case letters, digits and _, starting with a letter)");
        if (!node.Params.TryGetValue("path", out var pathValue))
        {
            Fault(null, "path", $"node '{id}' parameter 'path': a graph node needs path, the .texgraph it uses, relative to this graph's file");
            return use;
        }
        var spec = ParamSpec.FilePath("path");
        var full = spec.Check(pathValue) is null ? ResolvePath(graph, pathValue.Text!) : null;
        if (full is null)
        {
            Fault(null, "path", $"node '{id}' parameter 'path' {spec.Check(pathValue) ?? $"is {pathValue}, which is not a path"}");
            return use;
        }
        use.File = full;
        var fileName = use.FileName;
        var at = chain.ToList().FindIndex(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));
        if (at >= 0)
        {
            Fault(null, "path", $"node '{id}' parameter 'path': uses {fileName}, a cycle through files: {string.Join(" -> ", chain.Skip(at).Append(full).Select(Path.GetFileName))} (each file uses the next as a node)");
            return use;
        }
        if (depth >= MaxDepth)
        {
            Fault(null, "path", $"node '{id}' parameter 'path': uses {fileName}, nesting deeper than {MaxDepth} files ({string.Join(" -> ", chain.Append(full).Select(Path.GetFileName))})");
            return use;
        }
        if (!File.Exists(full))
        {
            Fault(null, "path", $"node '{id}' parameter 'path': {Missing(full)}");
            return use;
        }
        string text;
        try
        {
            text = File.ReadAllText(full);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Fault(null, "path", $"node '{id}' parameter 'path': could not read {full}: {e.Message}");
            return use;
        }
        a.Files.Add(full);
        use.Files.Add(full);
        var read = TexGraphFile.Parse(text, null, Path.GetDirectoryName(full));
        if (read.Graph is not { } nested)
        {
            Fault(null, "path", $"node '{id}' parameter 'path': {full} is not a graph patina reads: {read.Error}");
            return use;
        }

        // The values set on the node: the nested graph's exposed parameters by name, each checked alone so every one at fault is named.
        // A value an outer parameter drives ("wall.mortar") is that parameter's, already checked against this range by
        // GraphParameters (its own value in the node is ignored); one at fault leaves the nested graph's own value.
        var overrides = node.Params.Where(p => p.Key != "path" && !exposed.DrivenBy.ContainsKey((id, p.Key))).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var (name, value) in overrides.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var where = $"nodes['{id}'].params['{name}']";
            if (!nested.Parameters.ContainsKey(name))
            {
                Fault(null, name, $"{where}: {fileName} has no such parameter; its parameters are {(nested.Parameters.Count == 0 ? "none" : string.Join(", ", nested.Parameters.Keys))}");
                continue;
            }
            var one = GraphParameters.WithOverrides(nested, new Dictionary<string, ParamValue> { [name] = value }, registry, chain: [.. chain, full], budget: budget);
            if (one.Error is { } error)
            {
                var head = GraphParameters.Where(name) + ": ";
                Fault(null, name, $"{where}: {(error.StartsWith(head, StringComparison.Ordinal) ? error[head.Length..] : error)} ({fileName}, parameter '{name}')");
            }
        }
        if (faults.Count > 0) return use;
        foreach (var ((driven, name), value) in exposed.Values)
            if (driven == id) overrides[name] = value;
        var set = GraphParameters.WithOverrides(nested, overrides, registry, chain: [.. chain, full], budget: budget).Graph!;

        if (budget.Taken + set.Nodes.Count > MaxNodes)
        {
            Fault(null, "path", $"node '{id}' parameter 'path': uses {fileName}, which takes the nodes validated past {MaxNodes} (the graph's own and each nested file's each time it is used, all levels down)");
            return use;
        }
        budget.Taken += set.Nodes.Count;
        var inner = Analyse(set, registry, [.. chain, full], depth + 1, budget);
        use.Files.AddRange(inner.Files);
        a.Files.AddRange(inner.Files);
        use.Parameters = GraphParameters.Analyse(set, registry, [.. chain, full], budget).Exposed;
        foreach (var f in inner.Faults.Where(f => !f.OnItsOwn))
        {
            var path = f.Node is null ? id : $"{id}/{f.Node}";
            var message = NestedMessage(id, f);
            faults.Add(f with { Node = path, Message = f.File is null ? $"{message} ({fileName})" : message, File = f.File ?? full });
        }

        // The nested graph's ports: its own input nodes (not those of graphs it uses) and its outputs.
        var inputs = inner.Nodes.Values.Where(n => n.Type is InputNode && !inner.Expanded.Contains(n.Id) && InputNode.PortName(n.Parameters) is not null)
            .GroupBy(n => InputNode.PortName(n.Parameters)!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        use.Inputs = [.. inputs.OrderBy(i => i.Key, StringComparer.Ordinal).Select(i => new NestedGraphInput(i.Key, InputNode.KindOf(i.Value.Parameters), i.Value.Parameters.Has("default")))];
        use.Order = [.. OutputRoles.Order(inner.Outputs.Keys)];
        if (faults.Count > 0) return use;
        foreach (var (port, _) in node.Inputs.Where(p => !inputs.ContainsKey(p.Key)))
            Fault(port, null, $"node '{id}' input '{port}': {fileName} has no such input; its inputs are {List(use.Inputs.Select(i => $"{i.Name} ({PortKinds.Name(i.Kind)})"))}");
        foreach (var input in use.Inputs.Where(i => !i.HasDefault && !node.Inputs.ContainsKey(i.Name)))
            Fault(input.Name, null, $"node '{id}' input '{input.Name}': {fileName}'s input '{input.Name}' has no default, so a graph node using it must connect it, and it is not");
        if (faults.Count > 0) return use;

        // An outer graph node with an id this expansion makes ("g/c" a graph node beside "g", RV-77) has no node of its own to
        // stand in the nested node's place, so what named that node would name nothing: the use is left unexpanded, as a use
        // at fault is, and the outer node's id fault is the one said.
        if (inner.Flat().Any(n => graph.Nodes.TryGetValue($"{id}/{n.Id}", out var outer) && registry.Find(outer.Type) is NestedGraphNode)) return use;
        foreach (var (nestedId, flat) in inner.Flat())
        {
            var newId = $"{id}/{nestedId}";
            if (graph.Nodes.ContainsKey(newId)) continue; // the graph's own node of that id is kept, and faulted for its id
            var flatInputs = flat.Inputs.Select(p => KeyValuePair.Create(p.Key, $"{id}/{p.Value}")).ToList();
            if (inner.Nodes[nestedId].Type is InputNode && !inner.Expanded.Contains(nestedId)
                && InputNode.PortName(inner.Nodes[nestedId].Parameters) is { } name && node.Inputs.TryGetValue(name, out var given))
            {
                flatInputs.Add(KeyValuePair.Create(InputNode.ValuePort, given));
                wiredHere.Add(newId);
                a.PassThrough[newId] = (id, name, fileName);
            }
            nodes[newId] = flat with { Inputs = ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, flatInputs) };
            a.Expanded.Add(newId);
        }
        foreach (var output in use.Order)
        {
            var (from, port) = inner.Nodes[inner.Outputs[output]].Sources["in"];
            use.Outputs[output] = ($"{id}/{from}", port);
        }
        use.Failed = false;
        return use;
    }

    /// <summary>A missing nested file, as open's missing file (and carve's missing texture, T-81) gives it: the path and the .texgraph files in its folder.</summary>
    /// <summary>
    /// A nested graph's fault named from the level above, in the form the graph node's own faults take (the person's
    /// ruling, 2026-11-20): <c>node 'layout' parameter 'gap' is ...</c> becomes <c>nodes['wall/layout'].params['gap']: is ...</c>,
    /// an input <c>nodes['wall/x'].inputs['in']: ...</c>, a node <c>nodes['wall/x']: ...</c>, and one already in this form
    /// (from a level further down) gains the prefix; a fault on no node (a nested parameter's) is put under the graph node.
    /// </summary>
    private static string NestedMessage(string id, GraphFault f)
    {
        if (f.Node is null) return $"nodes['{id}']: {f.Message}";
        var path = $"{id}/{f.Node}";
        var m = f.Message;
        string Rest(string head) => m[head.Length..].TrimStart(':', ' ');
        if (f.Parameter is { } p && m.StartsWith($"node '{f.Node}' parameter '{p}'", StringComparison.Ordinal))
            return $"nodes['{path}'].params['{p}']: {Rest($"node '{f.Node}' parameter '{p}'")}";
        if (f.Port is { } q && m.StartsWith($"node '{f.Node}' input '{q}'", StringComparison.Ordinal))
            return $"nodes['{path}'].inputs['{q}']: {Rest($"node '{f.Node}' input '{q}'")}";
        if (m.StartsWith($"node '{f.Node}'", StringComparison.Ordinal))
            return $"nodes['{path}']: {Rest($"node '{f.Node}'")}";
        if (m.StartsWith("nodes['", StringComparison.Ordinal))
            return "nodes['" + id + "/" + m["nodes['".Length..];
        return $"nodes['{path}']: {m}";
    }

    /// <summary>
    /// What a <c>graph</c> node's parameters are, for an outer parameter driving one (<c>"wall.mortar"</c>): the nested
    /// file's name, its parameters, and those not at fault with their ranges. Null when the file cannot be read, is in
    /// <paramref name="chain"/> or is too deep; the graph node's own faults say why.
    /// </summary>
    internal static NestedParameterSet? NestedParameters(TextureGraph graph, GraphNode node, NodeRegistry registry, IReadOnlyList<string> chain, NodeBudget budget)
    {
        if (chain.Count >= MaxDepth || !node.Params.TryGetValue("path", out var pathValue) || ParamSpec.FilePath("path").Check(pathValue) is not null) return null;
        if (ResolvePath(graph, pathValue.Text!) is not { } full || chain.Contains(full, StringComparer.OrdinalIgnoreCase)) return null;
        // Read once a validation for each chain (RV-77): a file driven many times at each level would otherwise be read
        // and analysed again for each drive, all levels down, which is exponential however few nodes it expands to.
        var key = string.Join("|", chain.Append(full));
        if (budget.Read.TryGetValue(key, out var known)) return known;
        var set = Read(full);
        if (set is { OverBudget: false }) budget.Read[key] = set;
        else if (set is null) budget.Read[key] = null;
        return set;

        NestedParameterSet? Read(string full)
        {
            if (!File.Exists(full)) return null;
            string text;
            try
            {
                text = File.ReadAllText(full);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
            if (TexGraphFile.Parse(text, null, Path.GetDirectoryName(full)).Graph is not { } nested) return null;
            var name = Path.GetFileName(full);
            var cost = Math.Max(1, nested.Nodes.Count);
            if (budget.Taken + cost > MaxNodes) return new(name, [.. nested.Parameters.Keys], new Dictionary<string, ExposedParameter>(), OverBudget: true);
            budget.Taken += cost;
            return new(name, [.. nested.Parameters.Keys], GraphParameters.Analyse(nested, registry, [.. chain, full], budget).Exposed);
        }
    }

    private static string Missing(string full)
    {
        const int limit = 20;
        var where = Path.GetDirectoryName(full)!;
        var head = $"no file at {full}; ";
        if (!Directory.Exists(where)) return head + $"the folder {where} does not exist";
        List<string> names;
        try
        {
            names = [.. Directory.GetFiles(where, "*" + TexGraphFile.Extension).Select(f => Path.GetFileName(f)!).Order(StringComparer.Ordinal)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return head + $"the folder {where} could not be listed: {e.Message}";
        }
        if (names.Count == 0) return head + $"there are no .texgraph files in {where}";
        var shown = string.Join(", ", names.Take(limit)) + (names.Count > limit ? $" and {names.Count - limit} more" : "");
        return head + $"the .texgraph files in {where} ({names.Count}) are: {shown}";
    }

    private static void Cycles(GraphAnalysis a)
    {
        // Tarjan's strongly connected components over node -> source edges.
        var index = new Dictionary<string, int>();
        var low = new Dictionary<string, int>();
        var stack = new Stack<string>();
        var onStack = new HashSet<string>();
        var components = new List<List<string>>();

        void Visit(string v)
        {
            index[v] = low[v] = index.Count;
            stack.Push(v); onStack.Add(v);
            foreach (var (w, _) in a.Nodes[v].Sources.Values)
            {
                if (!a.Nodes.ContainsKey(w)) continue;
                if (!index.ContainsKey(w)) { Visit(w); low[v] = Math.Min(low[v], low[w]); }
                else if (onStack.Contains(w)) low[v] = Math.Min(low[v], index[w]);
            }
            if (low[v] != index[v]) return;
            var component = new List<string>();
            string x;
            do { x = stack.Pop(); onStack.Remove(x); component.Add(x); } while (x != v);
            components.Add(component);
        }

        foreach (var id in a.Nodes.Keys.Order(StringComparer.Ordinal))
            if (!index.ContainsKey(id)) Visit(id);

        foreach (var component in components)
        {
            var members = component.ToHashSet();
            var start = component.Min(StringComparer.Ordinal)!;
            if (component.Count == 1 && !a.Nodes[start].Sources.Values.Any(s => s.Node == start)) continue;
            a.Cyclic.UnionWith(members);
            // The shortest way round from start back to start, inputs in port order.
            var previous = new Dictionary<string, (string Node, string Port)>();
            var queue = new Queue<string>([start]);
            var found = false;
            while (queue.Count > 0 && !found)
            {
                var v = queue.Dequeue();
                foreach (var (port, (w, _)) in a.Nodes[v].Sources.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!members.Contains(w)) continue;
                    if (w == start) { previous[start] = (v, port); found = true; break; }
                    if (previous.ContainsKey(w)) continue;
                    previous[w] = (v, port);
                    queue.Enqueue(w);
                }
            }
            // Walk back from start: start, the node taking start, ..., start again; then read it forwards.
            var path = new List<string> { start };
            string firstPort;
            for (var at = previous[start]; ; at = previous[at.Node])
            {
                path.Add(at.Node);
                if (at.Node == start) { firstPort = at.Port; break; }
            }
            path.Reverse();
            var forwards = path;
            a.Fault(start, firstPort, null, $"node '{start}' input '{firstPort}': is on a cycle, {string.Join(" -> ", forwards)} (each node takes the next as an input)");
        }
    }

    private static void Kinds(GraphAnalysis a)
    {
        var done = new HashSet<string>();

        void Visit(string id)
        {
            if (!done.Add(id)) return;
            var node = a.Nodes[id];
            foreach (var (source, _) in node.Sources.Values)
                if (a.Nodes.ContainsKey(source) && !a.Cyclic.Contains(source)) Visit(source);
            var cyclic = a.Cyclic.Contains(id);

            // What each input carries where its source's kind is known; an input that is at fault, or whose source's kind
            // is not known, could carry either, so every way it could go is tried and only what holds for all of them is said.
            // A node on a cycle is checked the same way: an input from the cycle is unknown, so only what its inputs from off
            // the cycle settle is said, and its own outputs are given no kind (the cycle's inputs would decide them).
            var inputs = new Dictionary<string, PortKind>();
            var unknown = new List<string>();
            foreach (var input in node.Type.Inputs)
            {
                // A source may name no resolved node: a use's output resolving to an outer node of an id the expansion also
                // makes, faulted and left out for its type (RV-78). Its kind is not known, as an input at fault's is not.
                if (node.Sources.TryGetValue(input.Name, out var from) && !a.Cyclic.Contains(from.Node)
                    && a.Nodes.TryGetValue(from.Node, out var giver) && giver.OutputKinds.TryGetValue(from.Port, out var gives))
                    inputs[input.Name] = gives;
                else if (node.Connected.Contains(input.Name) || input.Required)
                    unknown.Add(input.Name);
            }
            foreach (var (port, gives) in inputs)
            {
                var takes = node.Type.Input(port)!.Kind;
                if (takes != PortKind.Any && takes != gives)
                    a.Fault(id, port, null, $"node '{id}' input '{port}': takes {PortKinds.Name(takes)}, but '{node.Sources[port].Node}' gives {PortKinds.Name(gives)}");
            }

            var ways = Ways(inputs, unknown);
            List<(string Port, string Message)>? always = null;
            foreach (var way in ways)
            {
                var found = node.Type.CheckInputKinds(way, node.Parameters).Where(f => inputs.ContainsKey(f.Port)).ToList();
                always = always is null ? found : [.. always.Where(found.Contains)];
            }
            foreach (var (port, message) in always!)
                a.Fault(id, port, null, $"node '{id}' input '{port}': {message}");
            if (cyclic) return;
            foreach (var port in node.Type.Outputs)
            {
                var kinds = ways.Select(way => node.Type.OutputKind(port, way, node.Parameters)).Distinct().ToList();
                if (kinds.Contains(PortKind.Any)) throw new InvalidOperationException($"type '{node.Type.Name}' gives port '{port}' no kind");
                if (kinds.Count == 1) node.OutputKinds[port] = kinds[0];
            }
            if (unknown.Count == 0 && !node.InputsAtFault) a.Order.Add(id);
        }

        foreach (var id in a.Nodes.Keys.Order(StringComparer.Ordinal)) Visit(id);
    }

    /// <summary>The inputs as they are, with each unknown one given each kind it could carry: one way when none is unknown.</summary>
    private static List<Dictionary<string, PortKind>> Ways(Dictionary<string, PortKind> inputs, List<string> unknown)
    {
        var ways = new List<Dictionary<string, PortKind>> { inputs };
        foreach (var port in unknown)
            ways = [.. ways.SelectMany(w => new[] { PortKind.Grey, PortKind.Colour }.Select(k => new Dictionary<string, PortKind>(w) { [port] = k }))];
        return ways;
    }

    private static void Outputs(GraphAnalysis a, IReadOnlyDictionary<string, GraphNode> nodes)
    {
        // Each output's role's name or its own name, to the output's id.
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in a.Nodes.Values.Where(n => n.Type is OutputNode).OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            // A role or name already at fault is not counted against another output's: no cascade.
            if (a.Faults.Any(f => f.Node == node.Id && f.Parameter is "role" or "name")) continue;
            var given = nodes[node.Id].Params;
            if (given.ContainsKey("role") && given.ContainsKey("name"))
            {
                a.Fault(node.Id, null, "name", $"node '{node.Id}' parameter 'name': an output has a role or a name, not both; drop role to name an extra image, or name to give a role");
                continue;
            }
            if (OutputNode.GivenName(node.Parameters) is { } name)
            {
                if (OutputRoles.TryParse(name, out _))
                    a.Fault(node.Id, null, "name", $"node '{node.Id}' parameter 'name': {name} is a role; give it as role, or name the output something else");
                else if (name == Checks.CheckSet.PackedRole)
                    a.Fault(node.Id, null, "name", $"node '{node.Id}' parameter 'name': {name} is reserved for the packed image export writes as roughness.png (check's role for it); name the output something else");
                else if (byName.TryGetValue(name, out var other))
                    a.Fault(node.Id, null, "name", $"node '{node.Id}' parameter 'name': {name} is already the name of output '{other}'; a graph has one output per name");
                else byName[name] = node.Id;
                continue;
            }
            var role = OutputRoles.Name(OutputNode.Role(node.Parameters));
            if (byName.TryGetValue(role, out var first))
                a.Fault(node.Id, null, "role", $"node '{node.Id}' parameter 'role': {role} is already the role of output '{first}'; a graph has one output per role");
            else byName[role] = node.Id;
        }
        if (byName.TryGetValue(OutputRoles.Name(OutputRole.Metallic), out var metallic) && !byName.ContainsKey(OutputRoles.Name(OutputRole.Roughness)))
            a.Fault(metallic, null, "role", $"node '{metallic}' parameter 'role': metallic is packed with roughness, so the graph needs a roughness output too (glTF's metallic-roughness image, roughness.png: R 255, G roughness, B metallic)");
        if (!a.Nodes.Values.Any(n => n.Type is OutputNode))
            a.Fault(null, null, null, $"the graph has no output: add an output node with a role ({string.Join(", ", OutputRoles.Names)})");
        a.Outputs = byName;
    }

    private static string List(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count == 0 ? "none" : string.Join(", ", list);
    }
}

/// <summary>What validation learns on the way: each node's type, parameters and sources, what each port carries, and an order that puts every node after its inputs.</summary>
internal sealed class GraphAnalysis
{
    public List<GraphFault> Faults { get; } = [];
    public Dictionary<string, ResolvedNode> Nodes { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Cyclic { get; } = new(StringComparer.Ordinal);
    public List<string> Order { get; } = [];
    /// <summary>Each output by its role's name or its own name, to its node's id.</summary>
    public Dictionary<string, string> Outputs { get; set; } = new(StringComparer.Ordinal);

    public void Fault(string? node, string? port, string? parameter, string message) => Faults.Add(new(node, port, parameter, message));

    /// <summary>Each graph node by id (T-140), resolved or failed.</summary>
    public Dictionary<string, GraphUse> Uses { get; } = new(StringComparer.Ordinal);

    /// <summary>The ids expansion made, <c>&lt;node&gt;/&lt;id&gt;</c>.</summary>
    public HashSet<string> Expanded { get; } = new(StringComparer.Ordinal);

    /// <summary>Each nested input node a graph node's input was wired to: the graph node, the input's name, the nested file's name.</summary>
    public Dictionary<string, (string Node, string Input, string FileName)> PassThrough { get; } = new(StringComparer.Ordinal);

    /// <summary>Every nested file read, all levels down, in the order read.</summary>
    public List<string> Files { get; } = [];

    /// <summary>
    /// The graph as plain nodes for a graph using it to take in (T-140): every node but the outputs, its sources resolved
    /// (<c>"id.port"</c>, graph nodes already expanded) and its driven parameters set to their values. Only for a graph with no faults.
    /// </summary>
    public IEnumerable<(string Id, GraphNode Node)> Flat() =>
        Nodes.Values.Where(n => n.Type is not OutputNode).OrderBy(n => n.Id, StringComparer.Ordinal).Select(n => (n.Id, GraphNode.Of(n.Type.Name,
            n.Sources.Select(s => KeyValuePair.Create(s.Key, $"{s.Value.Node}.{s.Value.Port}")),
            n.Values)));
}

/// <summary>A graph node as validation resolved it (T-140).</summary>
internal sealed class GraphUse(string id)
{
    public string Id { get; } = id;
    public string? File { get; set; }
    public string FileName => File is null ? "the file" : Path.GetFileName(File);
    public bool Failed { get; set; } = true;
    public IReadOnlyDictionary<string, ExposedParameter> Parameters { get; set; } = new Dictionary<string, ExposedParameter>();
    public IReadOnlyList<NestedGraphInput> Inputs { get; set; } = [];
    /// <summary>The nested graph's outputs, in sheet order.</summary>
    public IReadOnlyList<string> Order { get; set; } = [];
    /// <summary>Each output to the expanded node and port it comes from; empty for a failed use.</summary>
    public Dictionary<string, (string Node, string Port)> Outputs { get; } = new(StringComparer.Ordinal);
    public List<string> Files { get; } = [];

    public NestedGraphUse Public => new(Id, File, !Failed, Parameters, Inputs, Order, Files);
}

internal sealed class ResolvedNode(string id, NodeType type, NodeParameters parameters, IReadOnlyDictionary<string, ParamValue> values)
{
    /// <summary>The parameter values the node is evaluated with, driven ones set.</summary>
    public IReadOnlyDictionary<string, ParamValue> Values { get; } = values;

    public string Id { get; } = id;
    public NodeType Type { get; } = type;
    public NodeParameters Parameters { get; } = parameters;
    public Dictionary<string, (string Node, string Port)> Sources { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, PortKind> OutputKinds { get; } = new(StringComparer.Ordinal);

    /// <summary>The input ports the graph connects, whether or not their source resolved.</summary>
    public HashSet<string> Connected { get; } = new(StringComparer.Ordinal);

    /// <summary>An input of this node is at fault (a bad port, source or a required one missing) or names a node of unknown type, so the node is not put in the evaluation order.</summary>
    public bool InputsAtFault { get; set; }
}

/// <summary>A nested graph's input port (T-140): its name, kind, and whether it has a default (one without must be connected).</summary>
public sealed record NestedGraphInput(string Name, PortKind Kind, bool HasDefault);

/// <summary>
/// A <c>graph</c> node as validation resolved it (T-140), for get_graph: the file it names (null when its path is not one),
/// whether it expanded (false: its faults say why), the nested graph's exposed parameters with the values set on the node,
/// its inputs and its outputs (sheet order), and every nested file read for it, all levels down.
/// </summary>
public sealed record NestedGraphUse(string Node, string? File, bool Expanded, IReadOnlyDictionary<string, ExposedParameter> Parameters,
    IReadOnlyList<NestedGraphInput> Inputs, IReadOnlyList<string> Outputs, IReadOnlyList<string> Files);

/// <summary>A nested graph's parameters as an outer parameter driving its graph node reads them: see <see cref="GraphValidator.NestedParameters"/>.
/// <c>OverBudget</c>: reading it would take the nodes validated past <see cref="GraphValidator.MaxNodes"/>, so it was not analysed.</summary>
internal sealed record NestedParameterSet(string FileName, IReadOnlyList<string> Declared, IReadOnlyDictionary<string, ExposedParameter> Exposed, bool OverBudget = false);
