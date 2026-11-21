using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Nodes;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

public sealed class DefineGraphTool(GraphStore store, NodeRegistry registry)
{
    /// <summary>The worked example: a tone per stave (eight across u) on a wood colour. Tests run it as written.</summary>
    public const string Worked =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"wood":{"type":"colour","params":{"colour":"#aa7040"}},"tone":{"type":"noise","params":{"cells":[8,1],"interpolation":"step"}},"base":{"type":"blend","inputs":{"a":"wood","b":"tone"},"params":{"mode":"multiply"}},"out":{"type":"output","inputs":{"in":"base"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedResult = "define_graph: the open graph is now 4 nodes, size 256, seed 0; no faults: it evaluates";

    /// <summary>T-134's metal plate: a grey plate, metallic from a gradient (packed into roughness.png's B), and a named output <c>edge</c>; export's and swatch's examples use it too.</summary>
    public const string WorkedMetal =
        """{"graph":{"version":1,"size":64,"seed":0,"nodes":{"plate":{"type":"colour","params":{"colour":"#c8c8c8"}},"ramp":{"type":"gradient"},"rough":{"type":"levels","inputs":{"in":"ramp"},"params":{"out_low":0.7,"out_high":0.3}},"band":{"type":"gradient","params":{"axis":"v","shape":"triangle"}},"out_base":{"type":"output","inputs":{"in":"plate"}},"out_rough":{"type":"output","inputs":{"in":"rough"},"params":{"role":"roughness"}},"out_metal":{"type":"output","inputs":{"in":"ramp"},"params":{"role":"metallic"}},"out_edge":{"type":"output","inputs":{"in":"band"},"params":{"name":"edge"}}}}}""";

    public const string WorkedMetalResult = "define_graph: the open graph is now 8 nodes, size 64, seed 0; no faults: it evaluates";

    /// <summary>The same graph with out's input misspelt: accepted, and the fault named.</summary>
    public const string WorkedFaulty =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"wood":{"type":"colour","params":{"colour":"#aa7040"}},"tone":{"type":"noise","params":{"cells":[8,1],"interpolation":"step"}},"base":{"type":"blend","inputs":{"a":"wood","b":"tone"},"params":{"mode":"multiply"}},"out":{"type":"output","inputs":{"in":"bsae"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedFaultyResult =
        "define_graph: the open graph is now 4 nodes, size 256, seed 0; 1 fault:\n- node 'out' input 'in': names 'bsae', which is not a node; the nodes are base, out, tone, wood";

    /// <summary>A second worked example: a running-bond layout of bricks, its height as the base colour (a node with several outputs is named "id.port").</summary>
    public const string WorkedTiles =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"layout":{"type":"tiles","params":{"count":[4,8],"stagger":0.5,"gap":0.1,"bevel":0.1}},"out":{"type":"output","inputs":{"in":"layout.height"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedTilesResult = "define_graph: the open graph is now 2 nodes, size 256, seed 0; no faults: it evaluates";
    /// <summary>A second worked example: a tone per stone, from worley's random on a stone colour. Tests run it as written.</summary>
    public const string WorkedStones =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"stones":{"type":"worley","params":{"cells":[6,6],"jitter":0.85}},"stone":{"type":"colour","params":{"colour":"#8c857a"}},"base":{"type":"blend","inputs":{"a":"stone","b":"stones.random"},"params":{"mode":"multiply"}},"out":{"type":"output","inputs":{"in":"base"},"params":{"role":"base_colour"}}}}}""";

    /// <summary>A worked example of blur and gradient_map: value noise softened by a blur, then coloured by a map of three stops. Tests run it as written.</summary>
    public const string WorkedMap =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"cloud":{"type":"fbm","params":{"cells":[4,4],"octaves":4}},"soft":{"type":"blur","inputs":{"in":"cloud"},"params":{"radius":0.02,"quality":"smooth"}},"tint":{"type":"gradient_map","inputs":{"in":"soft"},"params":{"positions":[0,0.5,1],"colours":["#1c2b24","#4a6b52","#b9c9a8"]}},"out":{"type":"output","inputs":{"in":"tint"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedMapResult = "define_graph: the open graph is now 4 nodes, size 256, seed 0; no faults: it evaluates";

    /// <summary>T-140's worked example: bricks.texgraph (beside the graph, or in the tool folder for a graph never saved) used as one node, its mortar set by name. Tests run it as written, with the sample copied beside it.</summary>
    public const string WorkedNested =
        """{"graph":{"version":2,"size":256,"seed":0,"nodes":{"wall":{"type":"graph","params":{"path":"bricks.texgraph","mortar":0.06}},"out":{"type":"output","inputs":{"in":"wall.base_colour"}},"rough":{"type":"output","inputs":{"in":"wall.roughness"},"params":{"role":"roughness"}}}}}""";

    public const string WorkedNestedResult = "define_graph: the open graph is now 3 nodes, size 256, seed 0; no faults: it evaluates";

    /// <summary>The same with mortar a parameter of this graph passed through to the node (the person's ruling, 2026-11-20): its range is bricks' own, 0 to 0.45.</summary>
    public const string WorkedPassThrough =
        """{"graph":{"version":2,"size":256,"seed":0,"parameters":{"mortar":{"value":0.06,"drives":["wall.mortar"]}},"nodes":{"wall":{"type":"graph","params":{"path":"bricks.texgraph"}},"out":{"type":"output","inputs":{"in":"wall.base_colour"}},"rough":{"type":"output","inputs":{"in":"wall.roughness"},"params":{"role":"roughness"}}}}}""";

    /// <summary>A worked example of transform: stripes turned 30 degrees, which does not tile. Tests run it as written.</summary>
    public const string WorkedTurn =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"stripes":{"type":"stripes","params":{"count":8}},"turned":{"type":"transform","inputs":{"in":"stripes"},"params":{"rotation":30}},"out":{"type":"output","inputs":{"in":"turned"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedTurnResult = "define_graph: the open graph is now 3 nodes, size 256, seed 0; no faults: it evaluates";

    public static string Text(NodeRegistry registry) =>
        "Replaces the open texture graph with a whole new one: graph is the body of a .texgraph file, as JSON (patina holds one graph per session). "
        + "It clears the snapshot stack (push_snapshot) and the graph's path (save gives it one). A graph with faults is still taken, so edit_graph can fix it; save refuses it until it has none. "
        + "A graph is {\"version\": 1, \"size\": S, \"seed\": N, \"nodes\": {id: node}}: version 1, or 2 for exposed parameters and input and graph nodes; size is the size the images are evaluated at, "
        + TextureGraph.SizesText + "; seed, 0 to 4294967295, is added to every noise's own seed, so the same graph gives the same pixels on every run. "
        + "Ids and port names are [a-z][a-z0-9_]*. A node is {\"type\": T, \"inputs\": {port: source}, \"params\": {name: value}}, inputs and params left out when empty; "
        + "a source is another node's id (\"id.port\" for a type with several outputs); a parameter left out takes its default. "
        + "A value is a number, a string, or a list of numbers or of colours ([0, 0.5, 1], [\"#202020\", \"#c0a080\"]). Everything is image-relative and tiles. "
        + "The images are the output nodes', at least one: one per role (base_colour, normal, roughness, metallic, occlusion, height), or a name ([a-z][a-z0-9_]*) in place of a role for an extra image <name>.png, such as a mask. "
        + "metallic is packed into roughness.png's blue channel, so it needs a roughness output too. "
        + "A fault names the node and the port or parameter it is on, and what would be right (the types, the ids, the range). "
        + "The node types (nodes gives each one's inputs, outputs, parameters with ranges and defaults, and a worked example; nodes {\"type\": \"tiles\"} gives one): "
        + NodeCatalogue.Index(registry) + ".\n"
        + "A graph that is not a .texgraph body (a missing or unknown key, a value of the wrong kind, a version other than 1 or 2, a bad size or seed) is refused, naming what is wrong, and the open graph is unchanged. "
        + "Returns one line: the open graph's nodes, size and seed, then its faults, one a line.\n"
        + "Worked example, a tone per stave (eight staves across u, one random grey each) on a wood colour:\n"
        + "define_graph " + Worked + "\n"
        + "returns: " + WorkedResult + "\n"
        + "The same with out's input misspelt as \"bsae\" is taken, and returns:\n" + WorkedFaultyResult + "\n"
        + "Next: edit_graph to change it, get_graph to read it back, save to keep it.";

    public static McpServerTool Create(NodeRegistry registry) => Create<DefineGraphTool>("define_graph", Text(registry), new JsonObject
    {
        ["graph"] = new JsonObject
        {
            ["type"] = "object",
            ["description"] = "The whole graph, as a .texgraph file holds it: {\"version\": 1, \"size\": 256, \"seed\": 0, \"nodes\": {id: {\"type\": ..., \"inputs\": {...}, \"params\": {...}}}}.",
        },
    }, "graph");

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? graph = null)
    {
        if (Unknown("define_graph", context, "graph") is { } unknown) return unknown;
        if (graph is not { ValueKind: JsonValueKind.Object } body)
            return Error("define_graph: graph is missing; give the whole graph as a JSON object, {\"version\": 1, \"size\": 256, \"seed\": 0, \"nodes\": {...}}"
                + (graph is { } other ? $", not {Kind(other)}" : ""));

        var read = TexGraphFile.Read(body.GetRawText(), registry);
        if (read.Graph is null) return Error($"define_graph: graph is not a .texgraph body: {read.Error!.Message}; the open graph is unchanged");

        lock (store.Gate)
        {
            store.Replace(read.Graph, null);
            // Validated again once placed: a graph node's path resolves against the tool folder the store gives a graph never saved (T-140).
            var placed = store.Graph!;
            return Line($"define_graph: the open graph is now {Describe(placed)}; {Faults(GraphValidator.Validate(placed, registry))}");
        }
    }
}
