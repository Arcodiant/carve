using Patina.Graphs.Checks;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Nodes;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// check's rules (T-141) on every committed sample graph, which must come back clean (a sample check flags is a stop,
/// not a change to a rule), on the planted fault graphs, which each raise the one problem they plant, and on M-7's
/// PNGs, whose results are written into the member's file.
/// </summary>
[Trait("Tier", "unit")]
public class CheckSamplesTests(ITestOutputHelper output)
{
    private static string Root => Path.GetDirectoryName(Path.GetDirectoryName(Samples.Folder))!;

    private static TextureGraph Load(string path)
    {
        var read = TexGraphFile.ReadFile(path);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    /// <summary>The graphs that evaluate on their own: every top-level sample, but a graph with an input that has no default (a filter meant to be used as a node).</summary>
    public static TheoryData<string> SampleGraphs()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Samples.Folder, "*.texgraph").Order(StringComparer.Ordinal))
            // The weathered graphs (T-162) read the mesh nodes, which Patina.Graphs does not know; tests/Patina.Server.Tests holds them to check.
            if (!Path.GetFileName(file).StartsWith("weathered-", StringComparison.Ordinal))
                data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    private static IReadOnlyList<CheckProblem> Check(TextureGraph graph, int? size = null)
    {
        var set = CheckSet.OfGraph(new GraphEvaluator(graph), size ?? graph.Size);
        return CheckRules.Run(set);
    }

    [Theory]
    [MemberData(nameof(SampleGraphs))]
    public void Every_committed_sample_graph_checks_clean(string name)
    {
        var graph = Load(Path.Combine(Samples.Folder, name + ".texgraph"));
        var problems = Check(graph);
        output.WriteLine($"{name}: {problems.Count} problems");
        Assert.True(problems.Count == 0, string.Join("\n", problems.Select(p => $"{p.Check} {p.Output}: {p.Message}")));
    }

    [Fact]
    public void The_bricks_variant_checks_clean()
    {
        var grey = GraphParameters.WithOverrides(Load(Path.Combine(Samples.Folder, "bricks.texgraph")),
            new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(0.08), ["tint"] = ParamValue.Of("#7a7a70") }).Graph!;
        Assert.Empty(Check(grey));
    }

    [Theory]
    [InlineData("chrome-in-paint", "metallic-not-binary", "metallic")]
    [InlineData("flat-mask", "one-value", "wear")]
    public void Each_planted_fault_graph_raises_the_one_problem_it_plants(string name, string check, string output)
    {
        var problems = Check(Load(Path.Combine(Samples.Folder, "faults", name + ".texgraph")));
        var one = Assert.Single(problems);
        Assert.Equal((check, output), (one.Check, one.Output));
    }

    [Fact]
    public void M7_textures_are_checked_with_their_roles()
    {
        var folder = Path.Combine(Root, "samples", "textures");
        (string File, string Role)[] files =
        [
            ("end-grain-base.png", "base_colour"), ("end-grain-normal.png", "normal"),
            ("iron-base.png", "base_colour"), ("iron-mr.png", "metallic_roughness"),
            ("rune-emissive.png", "emissive"),
            ("staves-base.png", "base_colour"), ("staves-normal.png", "normal"), ("staves-occlusion.png", "occlusion"),
        ];
        foreach (var group in new[] { new[] { 0, 1 }, [2, 3], [4], [5, 6, 7] })
        {
            var set = CheckSet.OfPngs([.. group.Select(i =>
            {
                var image = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(folder, files[i].File)), ColorComponents.RedGreenBlueAlpha);
                return (files[i].Role, image.Width, image.Height, image.Data);
            })]);
            var problems = CheckRules.Run(set);
            output.WriteLine($"{string.Join(" + ", group.Select(i => files[i].File))}: {(problems.Count == 0 ? "clean" : string.Join(" | ", problems.Select(p => p.Check + " " + p.Output + ": " + p.Message)))}");
        }
    }
}
