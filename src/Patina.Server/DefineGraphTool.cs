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

    /// <summary>The same graph with out's input misspelt: accepted, and the fault named.</summary>
    public const string WorkedFaulty =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"wood":{"type":"colour","params":{"colour":"#aa7040"}},"tone":{"type":"noise","params":{"cells":[8,1],"interpolation":"step"}},"base":{"type":"blend","inputs":{"a":"wood","b":"tone"},"params":{"mode":"multiply"}},"out":{"type":"output","inputs":{"in":"bsae"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedFaultyResult =
        "define_graph: the open graph is now 4 nodes, size 256, seed 0; 1 fault:\n- node 'out' input 'in': names 'bsae', which is not a node; the nodes are base, out, tone, wood";

    /// <summary>A second worked example: a running-bond layout of bricks, its height as the base colour (a node with several outputs is named "id.port").</summary>
    public const string WorkedTiles =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"layout":{"type":"tiles","params":{"count":[4,8],"stagger":0.5,"gap":0.1,"bevel":0.1}},"out":{"type":"output","inputs":{"in":"layout.height"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedTilesResult = "define_graph: the open graph is now 2 nodes, size 256, seed 0; no faults: it evaluates";

    public static string Text(NodeRegistry registry) =>
        "Replaces the open texture graph with a whole new one, given as graph: the body of a .texgraph file, as JSON. "
        + "patina holds one graph per session. define_graph clears the snapshot stack (push_snapshot) and the graph's path (save gives it one), "
        + "and returns the graph's faults: none means it evaluates. A graph with faults is still taken, so edit_graph can fix it, but save refuses it until it has none. "
        + "A graph is {\"version\": 1, \"size\": S, \"seed\": N, \"nodes\": {id: node}}: version is 1; size is the size the images are evaluated at, "
        + TextureGraph.SizesText + "; seed, 0 to 4294967295, is the graph's seed, which every noise adds its own seed to, so the same graph gives the same pixels on every run. "
        + "Ids and port names are lower case, [a-z][a-z0-9_]*. A node is {\"type\": T, \"inputs\": {port: source}, \"params\": {name: value}}, inputs and params left out when empty; "
        + "a source is the id of another node (\"id.port\" for a type with several outputs); a parameter left out takes its default. "
        + "A value is a number, a string, or an array of numbers. All of it is image-relative and tiles: an image tiled across a surface shows no seam. "
        + "The images are the output nodes': one per role (base_colour, normal, roughness, occlusion), and a graph needs at least one. "
        + "A fault names the node and the input port or parameter it is on, and says what would be right (the types, the ids, the range): "
        + "an unknown type, a source naming no node, a cycle, a greyscale/colour mismatch, a required input not connected, an unknown parameter or one out of range, two outputs with one role. "
        + "The node types:\n"
        + NodeCatalogue.Text(registry)
        + "A graph that is not a .texgraph body (a missing or unknown key, a value of the wrong kind, a version other than 1, a bad size or seed) is refused, naming what is wrong, and the open graph is unchanged. "
        + "Returns one line: the open graph's nodes, size and seed, then its faults, one a line.\n"
        + "Worked example, a tone per stave (eight staves across u, one random grey each) on a wood colour:\n"
        + "define_graph " + Worked + "\n"
        + "returns: " + WorkedResult + "\n"
        + "The same with out's input misspelt as \"bsae\" is taken, and returns:\n" + WorkedFaultyResult + "\n"
        + "Brick layout, tiles (4 bricks across, 8 rows, every second row shifted half a brick) with its height output as the base colour:\n"
        + "define_graph " + WorkedTiles + "\n"
        + "returns: " + WorkedTilesResult + "\n"
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
            return Line($"define_graph: the open graph is now {Describe(read.Graph)}; {Faults(read.Faults)}");
        }
    }
}
