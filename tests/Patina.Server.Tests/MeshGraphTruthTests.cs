using System.Text.Json;
using Carve.Core.Baking;
using Carve.Graphs;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Server.Tests;

/// <summary>
/// RV-82: patina tells the truth about a mesh-reading graph. A mesh node's bad parameter is a fault in every tool, and
/// one an exposed parameter's value gives is refused naming the node; a graph nesting a mesh-reading graph carries the
/// note, the nested node named as expanded, in every answer and every swatch view's text; and the mesh_curvature
/// caption's two claims hold.
/// </summary>
[Trait("Tier", "unit")]
public class MeshGraphTruthTests
{
    private static string Graph(string node, int version = 1, string parameters = "") =>
        "{\"version\":" + version + ",\"size\":32,\"seed\":0," + parameters + "\"nodes\":{\"m\":" + node
        + ",\"o\":{\"type\":\"output\",\"inputs\":{\"in\":\"m\"},\"params\":{\"name\":\"m\"}}}}";

    private static string Define(string graph) => "{\"graph\":" + graph + "}";

    [Theory]
    [InlineData("""{"type":"mesh_mask"}""")]
    [InlineData("""{"type":"mesh_mask","params":{"group":"a","material":"b"}}""")]
    [InlineData("""{"type":"mesh_curvature","params":{"width":0}}""")]
    [InlineData("""{"type":"mesh_occlusion","params":{"reach":0}}""")]
    public async Task A_bad_mesh_parameter_is_a_fault_in_define_edit_open_and_save(string node)
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        var defined = await patina.Ok("define_graph", Define(Graph(node)));
        Assert.Contains("1 fault:\n- node 'm'", defined, StringComparison.Ordinal);
        Assert.DoesNotContain("it evaluates", defined, StringComparison.Ordinal);
        Assert.Contains("node 'm'", await patina.Refused("save", """{"path":"bad"}"""), StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(folder.Path, "bad.texgraph"), Graph(node));
        var opened = Results.TextOf(await patina.CallToolAsync("open", """{"path":"bad"}""", TestContext.Current.CancellationToken));
        Assert.Contains("node 'm'", opened, StringComparison.Ordinal);
        Assert.DoesNotContain("it evaluates", opened, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_graph_answers_a_width_of_0_as_a_fault()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", NodeCatalogue.WorkedMesh);
        var edited = await patina.Refused("edit_graph", """{"operations":[{"op":"set_params","node":"edges","params":{"width":0}}]}""");
        Assert.StartsWith("edit_graph: nothing changed: the operations would leave 1 new fault:", edited, StringComparison.Ordinal);
        Assert.Contains("node 'edges' parameter 'width'", edited, StringComparison.Ordinal);
    }

    /// <summary>The probe that still reached evaluation after T-217: an exposed parameter's value of 0, inside its range, driving a width.</summary>
    [Theory]
    [InlineData("export", """{"folder":"out","parameters":{"w":0}}""")]
    [InlineData("swatch", """{"view":"flat","parameters":{"w":0}}""")]
    [InlineData("check", """{"parameters":{"w":0}}""")]
    public async Task A_parameter_value_a_mesh_node_refuses_is_answered_naming_the_node(string tool, string arguments)
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        const string drive = "\"parameters\":{\"w\":{\"value\":0.02,\"drives\":[\"m.width\"]}},";
        Assert.EndsWith("no faults: it evaluates", await patina.Ok("define_graph",
            Define(Graph("""{"type":"mesh_curvature","params":{"width":0.02}}""", 2, drive))), StringComparison.Ordinal);
        var refused = await patina.Refused(tool, arguments);
        Assert.StartsWith(tool + ": ", refused, StringComparison.Ordinal);
        Assert.Contains("node 'm' parameter 'width'", refused, StringComparison.Ordinal);
    }

    private const string Note = "mesh inputs neutral: g/edges (mesh_curvature); the real maps come from the mesh when a carve material names the graph";

    private static void WriteInner(string folder) =>
        File.WriteAllText(Path.Combine(folder, "inner.texgraph"),
            """{"version":1,"size":32,"seed":0,"nodes":{"edges":{"type":"mesh_curvature","params":{"width":0.02}},"o":{"type":"output","inputs":{"in":"edges"},"params":{"name":"edge"}}}}""");

    private const string Outer = """{"graph":{"version":2,"size":32,"seed":0,"nodes":{"g":{"type":"graph","params":{"path":"inner"}},"o":{"type":"output","inputs":{"in":"g.edge"},"params":{"name":"edge"}}}}}""";

    [Fact]
    public async Task A_graph_nesting_a_mesh_graph_says_so_in_every_answer()
    {
        using var folder = new TempFolder();
        WriteInner(folder.Path);
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("define_graph", Outer);
        Assert.Contains("\n" + Note + "\n", await patina.Ok("get_graph", "{}"), StringComparison.Ordinal);
        Assert.EndsWith("\n" + Note, await patina.Ok("save", """{"path":"outer"}"""), StringComparison.Ordinal);
        Assert.EndsWith("\n" + Note, await patina.Ok("open", """{"path":"outer"}"""), StringComparison.Ordinal);
        Assert.EndsWith("\n" + Note, await patina.Ok("export", """{"folder":"out"}"""), StringComparison.Ordinal);
        var report = JsonDocument.Parse(await patina.Ok("check", "{}")).RootElement;
        Assert.Contains(Note, report.GetProperty("notes").EnumerateArray().Select(n => n.GetString()));
        // The person's ruling (2026-11-25): one-value stays, its fix the true reason, through the nested file too.
        var problem = Assert.Single(report.GetProperty("problems").EnumerateArray());
        Assert.Equal(("one-value", "edge"), (problem.GetProperty("check").GetString(), problem.GetProperty("output").GetString()));
        Assert.EndsWith(CheckTool.NeutralFix, problem.GetProperty("message").GetString(), StringComparison.Ordinal);

        // Two levels down, each graph node in the name.
        File.WriteAllText(Path.Combine(folder.Path, "outer2.texgraph"),
            """{"version":2,"size":32,"seed":0,"nodes":{"h":{"type":"graph","params":{"path":"outer"}},"o":{"type":"output","inputs":{"in":"h.edge"},"params":{"name":"edge"}}}}""");
        var read = TexGraphFile.ReadFile(Path.Combine(folder.Path, "outer2.texgraph"), Program.Registry);
        Assert.StartsWith("mesh inputs neutral: h/g/edges (mesh_curvature);", ToolSupport.MeshNote(read.Graph!, Program.Registry), StringComparison.Ordinal);
    }

    /// <summary>The ruling decided by what an output reads, not by the graph or the output's name: a mesh-reading output's one-value gives the true reason, a flat output beside it that reads no mesh node keeps the old advice.</summary>
    [Fact]
    public async Task Check_gives_one_value_the_neutral_reason_only_for_an_output_that_reads_a_mesh_node()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", """{"graph":{"version":1,"size":32,"seed":0,"nodes":{"edges":{"type":"mesh_curvature","params":{"width":0.02}},"n":{"type":"fbm","params":{"seed":3}},"w":{"type":"levels","inputs":{"in":"n"},"params":{"in_low":0,"in_high":0}},"oe":{"type":"output","inputs":{"in":"edges"},"params":{"name":"edge"}},"ow":{"type":"output","inputs":{"in":"w"},"params":{"name":"wear"}}}}}""");
        var report = JsonDocument.Parse(await patina.Ok("check", "{}")).RootElement;
        var messages = report.GetProperty("problems").EnumerateArray()
            .ToDictionary(p => (p.GetProperty("check").GetString(), p.GetProperty("output").GetString()), p => p.GetProperty("message").GetString()!);
        Assert.Equal(2, messages.Count);
        Assert.EndsWith(CheckTool.NeutralFix, messages[("one-value", "edge")], StringComparison.Ordinal);
        Assert.Contains("widen its levels", messages[("one-value", "wear")], StringComparison.Ordinal);
        Assert.DoesNotContain("neutral", messages[("one-value", "wear")], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"view":"flat"}""")]
    [InlineData("""{"view":"tiled"}""")]
    [InlineData("""{"view":"sphere"}""")]
    [InlineData("""{"view":"cube"}""")]
    [InlineData("""{"view":"sheet"}""")]
    public async Task Every_swatch_view_says_the_mesh_inputs_are_neutral(string arguments)
    {
        using var folder = new TempFolder();
        WriteInner(folder.Path);
        await using var patina = await PatinaHarness.StartAsync(folder.Path, blankEngine: true);
        await patina.Ok("define_graph", Outer);
        var result = await patina.CallToolAsync("swatch", arguments, TestContext.Current.CancellationToken);
        if (Results.IsError(result)) Assert.Fail($"swatch {arguments} failed: {Results.TextOf(result)}");
        var caption = result.GetProperty("content")[1].GetProperty("text").GetString()!;
        Assert.EndsWith("; " + Note, caption, StringComparison.Ordinal);
        if (arguments.Contains("sheet", StringComparison.Ordinal))
            Assert.EndsWith("   mesh inputs neutral", patina.LastLabels![0], StringComparison.Ordinal);
    }

    /// <summary>A curvature band down the middle column of real curvature: peak 1 at x = size / 2, falling to the flat 0.5 over 4 texels each side.</summary>
    private sealed class Band : IMeshMaps
    {
        public BakeImage Curvature(int size, double width)
        {
            var image = new BakeImage(size, size, 1);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    image[x, y] = 0.5f + 0.5f * Math.Max(0, 1 - Math.Abs(x - size / 2) / 4f);
            return image;
        }

        public BakeImage Occlusion(int size, int rays, float reach) => throw new NotSupportedException();
        public BakeImage Mask(int size, MaskSource source, string name) => throw new NotSupportedException();
        public BakeImage Position(int size) => throw new NotSupportedException();
    }

    [Fact]
    public void The_mesh_curvature_example_is_the_base_colour_on_the_stand_in_and_wears_in_runs_along_a_real_edge()
    {
        const int size = 256;
        var text = JsonDocument.Parse(NodeCatalogue.WorkedMesh).RootElement.GetProperty("graph").GetRawText();
        byte[] Base(NodeRegistry registry) => OutputImage.Rgba8(new GraphEvaluator(TexGraphFile.Read(text, registry).Graph!, registry), OutputRole.BaseColour, size);
        bool Worn(byte[] rgba, int x, int y) => rgba.AsSpan((y * size + x) * 4, 3).SequenceCompareTo(new byte[] { 0x30, 0x32, 0x36 }) != 0;

        // On the neutral stand-in the mask is 0, so every texel is the iron, #303236.
        var neutral = Base(Program.Registry);
        Assert.All(Enumerable.Range(0, size * size), i => Assert.False(Worn(neutral, i % size, i / size), $"texel {i} is worn"));

        // On a real edge the wear comes and goes along it: some centre rows worn, some not, in more than one run; none off the band.
        var band = Base(MeshNodes.Register(NodeRegistry.BuiltIn, new Band()));
        var centre = Enumerable.Range(0, size).Select(y => Worn(band, size / 2, y)).ToList();
        Assert.Contains(true, centre);
        Assert.Contains(false, centre);
        Assert.True(centre.Zip(centre.Skip(1)).Count(p => !p.First && p.Second) >= 2, "the wear is one unbroken run along the edge");
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                if (Math.Abs(x - size / 2) > 4) Assert.False(Worn(band, x, y), $"({x}, {y}) is worn off the band");
    }
}
