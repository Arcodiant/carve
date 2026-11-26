using System.Text.Json;
using Patina.Graphs;
using Patina.Graphs.Checks;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using StbImageSharp;

namespace Patina.Server.Tests;

/// <summary>
/// T-162: patina knows the four mesh nodes on their neutral stand-ins and says so in every answer that shows pixels; the
/// graphs it holds without a mesh node answer as before; and the two weathered sample graphs equal their committed PNGs,
/// pass check but for the two outputs the stand-ins empty, and show the planks' own staves when the mesh reads neutral.
/// </summary>
[Trait("Tier", "unit")]
public class MeshNodeToolTests
{
    private static string Sample(string relative) => Path.Combine(Paths.RepoRoot, "samples", "graphs", relative);

    private static TextureGraph Load(string name)
    {
        var read = TexGraphFile.ReadFile(Sample(name + ".texgraph"), Program.Registry);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static byte[] Committed(string name, string file)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(Sample($"{name}/{file}")), ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((256, 256), (image.Width, image.Height));
        return image.Data;
    }

    [Fact]
    public async Task Every_tool_takes_a_mesh_graph_and_says_the_inputs_are_neutral()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        Assert.Equal(NodeCatalogue.Evaluates(9), await patina.Ok("define_graph", NodeCatalogue.WorkedMesh));
        const string note = "mesh inputs neutral: edges (mesh_curvature); the real maps come from the mesh when a carve material names the graph";

        Assert.Contains("\n" + note + "\n", await patina.Ok("get_graph", "{}"), StringComparison.Ordinal);
        await patina.Ok("edit_graph", """{"operations":[{"op":"set_params","node":"edges","params":{"width":0.03}}]}""");
        Assert.Contains("0.03", await patina.GraphText());
        await patina.Ok("save", """{"path":"iron"}""");
        Assert.EndsWith("\n" + note, await patina.Ok("open", """{"path":"iron"}"""), StringComparison.Ordinal);
        Assert.EndsWith("\n" + note, await patina.Ok("export", """{"folder":"out","size":32}"""), StringComparison.Ordinal);
        var report = JsonDocument.Parse(await patina.Ok("check", "{}")).RootElement;
        Assert.Contains(note, report.GetProperty("notes").EnumerateArray().Select(n => n.GetString()));
    }

    [Fact]
    public async Task A_graph_without_a_mesh_node_answers_as_before()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        var opened = await patina.Ok("open", JsonSerializer.Serialize(new { path = Sample("planks.texgraph") }));
        Assert.DoesNotContain("mesh", opened, StringComparison.Ordinal);
        Assert.DoesNotContain("mesh", await patina.Ok("get_graph", "{}"), StringComparison.Ordinal);
        Assert.DoesNotContain("mesh inputs", await patina.Ok("check", "{}"), StringComparison.Ordinal);
        Assert.Equal("", ToolSupport.MeshNote(Load("planks")));
    }

    [Fact]
    public void The_mesh_nodes_are_in_the_index_and_each_has_its_catalogue_entry()
    {
        var text = DefineGraphTool.Text(Program.Registry);
        foreach (var name in Carve.Graphs.MeshNodes.TypeNames)
        {
            Assert.Contains(name + " (", text, StringComparison.Ordinal);
            Assert.Contains("In patina a stand-in", NodeCatalogue.Entry(Program.Registry, name), StringComparison.Ordinal);
        }
    }

    /// <summary>Each sample graph with a node type <see cref="NodeRegistry.BuiltIn"/> does not know: the files it writes and the one mask the stand-ins empty.</summary>
    private static readonly Dictionary<string, (string[] Files, string Mask)> Expected = new(StringComparer.Ordinal)
    {
        ["weathered-iron"] = (["base_colour.png", "roughness.png", "wear.png"], "wear"),
        ["weathered-staves"] = (["base_colour.png", "normal.png", "roughness.png", "occlusion.png", "grime.png"], "grime"),
    };

    /// <summary>
    /// Every top-level sample graph with a node type <see cref="NodeRegistry.BuiltIn"/> does not know: the graphs
    /// Patina.Graphs.Tests' <c>CheckSamplesTests</c> skips by the same rule (RV-82), so a new one is held here or there.
    /// </summary>
    public static TheoryData<string> MeshSamples() => [.. MeshSampleNames()];

    private static IEnumerable<string> MeshSampleNames() =>
        Directory.GetFiles(Path.Combine(Paths.RepoRoot, "samples", "graphs"), "*.texgraph").Order(StringComparer.Ordinal)
            .Where(file => !JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("nodes").EnumerateObject()
                .All(n => NodeRegistry.BuiltIn.Find(n.Value.GetProperty("type").GetString()!) is not null))
            .Select(Path.GetFileNameWithoutExtension)!;

    [Fact]
    public void The_mesh_samples_are_the_two_weathered_graphs() =>
        Assert.Equal(Expected.Keys.Order(StringComparer.Ordinal), MeshSampleNames());

    [Theory]
    [MemberData(nameof(MeshSamples))]
    public void The_committed_pngs_equal_the_graph(string name)
    {
        var files = Expected[name].Files;
        var graph = Load(name);
        var evaluator = new GraphEvaluator(graph, Program.Registry);
        var written = evaluator.Outputs.Where(OutputRoles.HasOwnFile).Select(OutputRoles.FileOf).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(files.Order(StringComparer.Ordinal), written);
        foreach (var output in evaluator.Outputs.Where(OutputRoles.HasOwnFile))
            Assert.True(OutputImage.FileRgba8(evaluator, output, graph.Size).AsSpan().SequenceEqual(Committed(name, OutputRoles.FileOf(output))),
                $"{name}/{OutputRoles.FileOf(output)} differs from the graph; run tools/Patina.SampleGraphs");
    }

    [Theory]
    [MemberData(nameof(MeshSamples))]
    public void Check_reports_the_one_empty_mask_and_nothing_else(string name)
    {
        var mask = Expected[name].Mask;
        var graph = Load(name);
        var set = CheckSet.OfGraph(new GraphEvaluator(graph, Program.Registry), graph.Size);
        var problems = CheckRules.Run(set);
        var only = Assert.Single(problems);
        Assert.Equal(("one-value", mask), (only.Check, only.Output));
        // Neutral inputs leave the mask 0 on every texel.
        Assert.All(Committed(name, mask + ".png").Chunk(4), p => Assert.Equal(0, p[0]));
    }

    /// <summary>The person's ruling (2026-11-25): check's answer keeps the one problem, its fix the true reason rather than "widen its levels".</summary>
    [Theory]
    [MemberData(nameof(MeshSamples))]
    public async Task Check_tells_the_empty_mask_it_is_flat_for_the_neutral_inputs(string name)
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        await patina.Ok("open", JsonSerializer.Serialize(new { path = Sample(name + ".texgraph") }));
        var report = JsonDocument.Parse(await patina.Ok("check", "{}")).RootElement;
        Assert.False(report.GetProperty("ok").GetBoolean());
        var only = Assert.Single(report.GetProperty("problems").EnumerateArray());
        Assert.Equal(("one-value", Expected[name].Mask), (only.GetProperty("check").GetString(), only.GetProperty("output").GetString()));
        Assert.EndsWith(CheckTool.NeutralFix, only.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("widen", only.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Neutral_weathered_staves_show_the_planks_own_maps_byte_for_byte()
    {
        foreach (var file in new[] { "base_colour.png", "roughness.png", "normal.png", "occlusion.png" })
            Assert.True(Committed("weathered-staves", file).AsSpan().SequenceEqual(Committed("planks", file)), file);
    }

    [Theory]
    [InlineData("weathered-iron", "wear_amount")]
    [InlineData("weathered-staves", "grime_amount")]
    public void The_exposed_amount_is_a_number_from_zero_to_one(string name, string amount) =>
        Assert.Contains($"- {amount}: a number, 1, from 0 to 1; drives base.opacity, rough.opacity", GetGraphTool.ParameterLines(Load(name), Program.Registry), StringComparison.Ordinal);
}
