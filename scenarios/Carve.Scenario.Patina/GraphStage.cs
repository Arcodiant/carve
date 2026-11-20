using System.Text.Json;
using Carve.Debug;

namespace Carve.Scenario.Patina;

/// <summary>
/// T-113's stage, the graph tools end to end on the real patina: define a small graph (a tone per stave on a wood
/// colour), edit it (a grain multiplied in), read it back, push, break it with an edit and see a breaking edit refused,
/// pop back to the pushed graph byte for byte, save, open again and compare by content, then open a file with a planted
/// bad input id, see the fault named by node and port, and fix it through edit_graph.
/// </summary>
internal sealed class GraphStage : IPatinaStage
{
    private const string Tone =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"wood":{"type":"colour","params":{"colour":"#aa7040"}},"tone":{"type":"noise","params":{"cells":[8,1],"interpolation":"step"}},"base":{"type":"blend","inputs":{"a":"wood","b":"tone"},"params":{"mode":"multiply"}},"out":{"type":"output","inputs":{"in":"base"},"params":{"role":"base_colour"}}}}}""";

    private const string Grain =
        """{"operations":[{"op":"set_node","node":"grain","type":"noise","params":{"cells":[64,2],"octaves":2}},{"op":"set_node","node":"grained","type":"blend","inputs":{"a":"base","b":"grain"},"params":{"mode":"multiply","opacity":0.3}},{"op":"connect","node":"out","port":"in","from":"grained"}]}""";

    /// <summary>The planted file: out reads "bsae", a node that is not there.</summary>
    private const string Planted =
        "{\n  \"version\": 1,\n  \"size\": 256,\n  \"seed\": 0,\n  \"nodes\": {\n"
        + "    \"wood\": { \"type\": \"colour\", \"params\": { \"colour\": \"#aa7040\" } },\n"
        + "    \"out\": { \"type\": \"output\", \"inputs\": { \"in\": \"bsae\" }, \"params\": { \"role\": \"base_colour\" } }\n"
        + "  }\n}\n";

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var tools = (await driver.ListToolsAsync()).Select(t => t.Name).ToList();
        checks.Check(tools.SequenceEqual(["define_graph", "edit_graph", "get_graph", "save", "open", "push_snapshot", "pop_snapshot", "export", "swatch", "check"]),
            "graph: tools/list is " + string.Join(", ", tools));

        var defined = await checks.CallAsync("define_graph", Tone);
        checks.Check(defined == "define_graph: the open graph is now 4 nodes, size 256, seed 0; no faults: it evaluates", "graph: define_graph said " + defined);

        var edited = await checks.CallAsync("edit_graph", Grain);
        checks.Check(edited.EndsWith("The graph is 6 nodes, size 256, seed 0; no faults: it evaluates", StringComparison.Ordinal), "graph: edit_graph said " + edited);

        var read = await checks.GraphAsync();
        checks.Check(read.Contains("\"grain\": { \"type\": \"noise\", \"params\": { \"cells\": [64, 2], \"octaves\": 2 } }", StringComparison.Ordinal)
            && read.Contains("\"out\": { \"type\": \"output\", \"inputs\": { \"in\": \"grained\" }", StringComparison.Ordinal), "graph: get_graph gave\n" + read);

        // Push, change the look, try an edit that would break the graph (refused, the fault named), and pop back.
        var pushed = await checks.CallAsync("push_snapshot", """{"label":"grained"}""");
        checks.Check(pushed == "push_snapshot: pushed 'grained'; depth 1", "graph: push_snapshot said " + pushed);
        await checks.CallAsync("edit_graph", """{"operations":[{"op":"set_params","node":"wood","params":{"colour":"#00ff00"}},{"op":"set_params","node":"tone","params":{"interpolation":"smooth"}}]}""");
        checks.Check(await checks.GraphAsync() != read, "graph: the edit after push changed nothing");
        var broken = await checks.CallAsync("edit_graph", """{"operations":[{"op":"remove_node","node":"tone"}]}""", refused: true);
        checks.Check(broken.StartsWith("edit_graph: nothing changed: the operations would leave 1 new fault:\n- node 'base' input 'b': names 'tone'", StringComparison.Ordinal), "graph: the breaking edit said " + broken);
        var popped = await checks.CallAsync("pop_snapshot", "{}");
        checks.Check(popped.StartsWith("pop_snapshot: restored 'grained'; depth 0.", StringComparison.Ordinal), "graph: pop_snapshot said " + popped);
        checks.Check(await checks.GraphAsync() == read, "graph: pop_snapshot did not restore the pushed graph byte for byte");
        await checks.CallAsync("pop_snapshot", "{}", refused: true);

        // Save, replace the graph, open the file again: the same graph by content, and the file is what get_graph shows.
        var path = Path.Combine(driver.RunFolder, "graph.texgraph");
        var saved = await checks.CallAsync("save", JsonSerializer.Serialize(new { path = Path.Combine(driver.RunFolder, "graph") }));
        checks.Check(saved == $"save: saved graph.texgraph to {path}: 6 nodes, size 256, seed 0", "graph: save said " + saved);
        checks.Check(File.Exists(path) && File.ReadAllText(path) == read + "\n", "graph: the saved file is not the graph get_graph gave");
        await checks.CallAsync("define_graph", """{"graph":{"version":1,"size":32,"seed":0,"nodes":{}}}""");
        var opened = await checks.CallAsync("open", JsonSerializer.Serialize(new { path }));
        checks.Check(opened == $"open: opened graph.texgraph from {path}: 6 nodes, size 256, seed 0; no faults: it evaluates", "graph: open said " + opened);
        checks.Check(await checks.GraphAsync() == read, "graph: the opened graph is not the saved one by content");

        // The planted bad input id: the file opens, the fault names node and port, and edit_graph fixes it.
        var planted = Path.Combine(driver.RunFolder, "planted.texgraph");
        File.WriteAllText(planted, Planted);
        var faulty = await checks.CallAsync("open", JsonSerializer.Serialize(new { path = planted }));
        checks.Check(faulty.Contains("; 1 fault:\n- node 'out' input 'in': names 'bsae', which is not a node; the nodes are out, wood", StringComparison.Ordinal), "graph: the planted file opened with " + faulty);
        await checks.CallAsync("save", JsonSerializer.Serialize(new { path = planted }), refused: true);
        var fixedUp = await checks.CallAsync("edit_graph", """{"operations":[{"op":"connect","node":"out","port":"in","from":"wood"}]}""");
        checks.Check(fixedUp.EndsWith("no faults: it evaluates", StringComparison.Ordinal), "graph: fixing the planted input said " + fixedUp);
    }
}
