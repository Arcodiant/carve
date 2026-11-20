using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs;

/// <summary>
/// Everything wrong with a graph, all at once: each fault names its node and its port or parameter. In node id order:
/// a bad id, an unknown type (listing the types), an unknown parameter (listing the type's) or a wrong value (giving
/// the range), an input port the type has not (listing its inputs), a source that is not a node (listing the ids) or
/// names a port that node has not, a required input missing; then each cycle (naming the nodes on it), each input
/// given the wrong kind (greyscale where colour is wanted, or the other way), an output given both a role and a name, a
/// name that is a role's, two outputs with one role or one name, a metallic output with no roughness to be packed with,
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
    public static IReadOnlyList<GraphFault> Validate(TextureGraph graph, NodeRegistry? registry = null) =>
        Analyse(graph, registry ?? NodeRegistry.BuiltIn).Faults;

    internal static GraphAnalysis Analyse(TextureGraph graph, NodeRegistry registry)
    {
        var a = new GraphAnalysis();
        var ids = string.Join(", ", graph.Nodes.Keys);
        // The exposed parameters first: what they drive takes their value, and its own value in the node is not checked.
        var exposed = GraphParameters.Analyse(graph, registry);

        foreach (var (id, node) in graph.Nodes)
        {
            if (!GraphNames.IsValid(id))
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
            var resolved = new ResolvedNode(id, type, new NodeParameters(type, values));
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
                if (!GraphNames.TryParseSource(source, out var from, out var fromPort))
                {
                    a.Fault(id, port, null, $"node '{id}' input '{port}': '{source}' is not a source; a source is \"<id>\" or \"<id>.<port>\"");
                    continue;
                }
                if (!graph.Nodes.TryGetValue(from, out var target))
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
        Outputs(a, graph);
        a.Faults.AddRange(exposed.Faults);
        return a;
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
                if (node.Sources.TryGetValue(input.Name, out var from) && !a.Cyclic.Contains(from.Node) && a.Nodes[from.Node].OutputKinds.TryGetValue(from.Port, out var gives))
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

    private static void Outputs(GraphAnalysis a, TextureGraph graph)
    {
        // Each output's role's name or its own name, to the output's id.
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in a.Nodes.Values.Where(n => n.Type is OutputNode).OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            // A role or name already at fault is not counted against another output's: no cascade.
            if (a.Faults.Any(f => f.Node == node.Id && f.Parameter is "role" or "name")) continue;
            var given = graph.Nodes[node.Id].Params;
            if (given.ContainsKey("role") && given.ContainsKey("name"))
            {
                a.Fault(node.Id, null, "name", $"node '{node.Id}' parameter 'name': an output has a role or a name, not both; drop role to name an extra image, or name to give a role");
                continue;
            }
            if (OutputNode.GivenName(node.Parameters) is { } name)
            {
                if (OutputRoles.TryParse(name, out _))
                    a.Fault(node.Id, null, "name", $"node '{node.Id}' parameter 'name': {name} is a role; give it as role, or name the output something else");
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
}

internal sealed class ResolvedNode(string id, NodeType type, NodeParameters parameters)
{
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
