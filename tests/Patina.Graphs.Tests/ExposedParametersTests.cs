using System.Collections.Immutable;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// T-139: a graph's exposed parameters. The file's <c>parameters</c> (version 2) round trip; every fault in validation's
/// words; the range intersection worked by hand; overrides equal to the same values set in the graph; the bricks' three
/// parameters keeping its PNGs' bytes, and its grey variant equal to the committed PNGs; and a table of broken copies of
/// the machinery, each caught by a check named for it.
/// </summary>
public class ExposedParametersTests
{
    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Carve.slnx");
    }

    private static string BricksPath => Path.Combine(Root(), "samples", "graphs", "bricks.texgraph");

    private static TextureGraph Bricks()
    {
        var read = TexGraphFile.ReadFile(BricksPath);
        Assert.Null(read.Error);
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    /// <summary>The grey variant, as tools/Patina.SampleGraphs lists it.</summary>
    private static readonly Dictionary<string, ParamValue> Grey = new() { ["mortar"] = ParamValue.Of(0.08), ["tint"] = ParamValue.Of("#7a7a70") };

    private static readonly OutputRole[] FourRoles = [OutputRole.BaseColour, OutputRole.Normal, OutputRole.Roughness, OutputRole.Occlusion];

    /// <summary>A small graph: a tiles node and a levels node, one output, and the given parameters.</summary>
    private static TextureGraph Small(params (string Name, GraphParameter Parameter)[] parameters)
    {
        var g = TextureGraph.Empty(64, 3)
            .WithNode("layout", GraphNode.Of("tiles"))
            .WithNode("tone", GraphNode.Of("levels", [new("in", "layout.mask")]))
            .WithNode("paint", GraphNode.Of("colour"))
            .WithNode("mix", GraphNode.Of("blend", [new("a", "paint"), new("b", "tone")], [new("mode", ParamValue.Of("multiply"))]))
            .WithNode("out", GraphNode.Of("output", [new("in", "mix")]));
        foreach (var (name, p) in parameters) g = g.WithParameter(name, p);
        return g;
    }

    private static string Faults(TextureGraph g) => string.Join("\n", GraphValidator.Validate(g).Select(f => f.Message));

    [Fact]
    public void A_graph_without_parameters_is_version_1_as_before()
    {
        var g = Small();
        var text = TexGraphFile.Write(g);
        Assert.StartsWith("{\n  \"version\": 1,\n  \"size\": 64,\n  \"seed\": 3,\n  \"nodes\": {\n", text);
        Assert.Equal(text, TexGraphFile.Write(g, TexGraphFormat.Version1));
        // An empty parameters object in a version 2 file reads as none, and is written as version 1.
        var v2 = text.Replace("\"version\": 1,", "\"version\": 2,").Replace("  \"nodes\"", "  \"parameters\": {},\n  \"nodes\"");
        Assert.Equal(text, TexGraphFile.Write(TexGraphFile.Read(v2).Graph!));
    }

    [Fact]
    public void Version_2_is_bit_exact_and_a_driven_parameter_is_left_out_of_its_node()
    {
        var g = Small(("mortar", new GraphParameter(ParamValue.Of(0.08), ["layout.gap", "tone.out_low"], 0, 0.2)), ("paint_colour", GraphParameter.Of(ParamValue.Of("#7a7a70"), "paint.colour")))
            .WithNode("layout", GraphNode.Of("tiles", parameters: [new("gap", ParamValue.Of(0.3)), new("bevel", ParamValue.Of(0.05))]));
        var text = TexGraphFile.Write(g);
        Assert.Equal(
            "{\n  \"version\": 2,\n  \"size\": 64,\n  \"seed\": 3,\n  \"parameters\": {\n"
            + "    \"mortar\": { \"value\": 0.08, \"drives\": [\"layout.gap\", \"tone.out_low\"], \"min\": 0, \"max\": 0.2 },\n"
            + "    \"paint_colour\": { \"value\": \"#7a7a70\", \"drives\": [\"paint.colour\"] }\n  },\n  \"nodes\": {\n"
            + "    \"layout\": { \"type\": \"tiles\", \"params\": { \"bevel\": 0.05 } },\n", text[..text.IndexOf("    \"mix\"", StringComparison.Ordinal)]);
        var read = TexGraphFile.Read(text);
        Assert.Null(read.Error);
        Assert.Empty(read.Faults);
        Assert.Equal(text, TexGraphFile.Write(read.Graph!));
        // get_graph's form shows the driver, and reads back to the same graph.
        var shown = TexGraphFile.WriteShowingDriven(g);
        Assert.Contains("\"layout\": { \"type\": \"tiles\", \"params\": { \"bevel\": 0.05, \"gap\": { \"driven_by\": \"mortar\" } } }", shown);
        Assert.Contains("\"paint\": { \"type\": \"colour\", \"params\": { \"colour\": { \"driven_by\": \"paint_colour\" } } }", shown);
        Assert.Equal(text, TexGraphFile.Write(TexGraphFile.Read(shown).Graph!));
        Assert.Equal("node 'layout' parameter 'bevel': is driven_by 'mortar', but no parameter 'mortar' drives layout.bevel",
            TexGraphFile.Read(shown.Replace("\"bevel\": 0.05", "\"bevel\": { \"driven_by\": \"mortar\" }")).Error!.Message);
        // A version 1 file may not hold parameters.
        Assert.Equal("'parameters' needs version 2, but the file is version 1", TexGraphFile.Read(text.Replace("\"version\": 2", "\"version\": 1")).Error!.Message);
    }

    [Fact]
    public void The_bricks_round_trip_bit_exact_and_expose_mortar_tint_and_jitter()
    {
        var text = File.ReadAllText(BricksPath).Replace("\r\n", "\n");
        var g = Bricks();
        Assert.Equal(text, TexGraphFile.Write(g));
        Assert.Equal(["jitter", "mortar", "tint"], g.Parameters.Keys);
        var exposed = GraphParameters.Of(g);
        Assert.Equal("a number from 0 to 0.45", exposed["mortar"].Range.Describe());
        Assert.Equal("a number from 0 to 1", exposed["jitter"].Range.Describe());
        Assert.Equal(ParamKind.Colour, exposed["tint"].Kind);
    }

    /// <summary>Every fault, each naming its parameter in validation's words.</summary>
    public static TheoryData<string, string> FaultCases() => new()
    {
        { "unknown node", "parameters['p']: drives 'brick.gap', but there is no node 'brick'; the nodes are layout, mix, out, paint, tone" },
        { "unknown parameter", "parameters['p']: drives 'layout.width', but 'layout', a node of type tiles, has no parameter 'width'; its parameters are count, stagger, gap, bevel, jitter, orientation" },
        { "not a drive", "parameters['p']: drives 'layout', which is not \"<node>.<param>\"" },
        { "no drives", "parameters['p']: drives nothing; drives is a list of the node parameters it sets, each \"<node>.<param>\"" },
        { "kind mismatch", "parameters['p']: drives 'layout.gap', a number, and 'paint.colour', a colour; the parameters it drives are all of one kind" },
        { "a list", "parameters['p']: drives 'layout.count', two whole numbers, and 'layout.gap', a number; the parameters it drives are all of one kind" },
        { "empty range", "parameters['p']: the parameters it drives have no value in common: layout.gap is a number from 0 to 0.45; and it gives min 0.5" },
        { "out of range", "parameters['p']: is 0.5, but must be a number from 0 to 0.45" },
        { "driven twice", "parameters['q']: drives 'layout.gap', which parameters['p'] drives already; a node parameter has one driver" },
        { "driven twice by one", "parameters['p']: drives 'layout.gap' twice; name each node parameter once" },
        { "min on a colour", "parameters['p']: min and max narrow a number's range, but it drives a colour" },
        { "bad name", "parameters['P']: a parameter's name is [a-z][a-z0-9_]* (lower case letters, digits and _, starting with a letter)" },
        { "enum no common choice", "parameters['p']: is \"diagonal\", but must be one of rows, columns" },
    };

    private static TextureGraph FaultGraph(string name) => name switch
    {
        "unknown node" => Small(("p", GraphParameter.Of(ParamValue.Of(0.1), "brick.gap"))),
        "unknown parameter" => Small(("p", GraphParameter.Of(ParamValue.Of(0.1), "layout.width"))),
        "not a drive" => Small(("p", GraphParameter.Of(ParamValue.Of(0.1), "layout"))),
        "no drives" => Small(("p", GraphParameter.Of(ParamValue.Of(0.1)))),
        "kind mismatch" => Small(("p", GraphParameter.Of(ParamValue.Of(0.1), "layout.gap", "paint.colour"))),
        "a list" => Small(("p", GraphParameter.Of(ParamValue.Of(4, 4), "layout.count", "layout.gap"))),
        "empty range" => Small(("p", new GraphParameter(ParamValue.Of(0.1), ["layout.gap"], 0.5))),
        "out of range" => Small(("p", GraphParameter.Of(ParamValue.Of(0.5), "layout.gap", "layout.jitter"))),
        "driven twice" => Small(("p", GraphParameter.Of(ParamValue.Of(0.1), "layout.gap")), ("q", GraphParameter.Of(ParamValue.Of(0.1), "layout.gap"))),
        "driven twice by one" => Small(("p", GraphParameter.Of(ParamValue.Of(0.1), "layout.gap", "layout.gap"))),
        "min on a colour" => Small(("p", new GraphParameter(ParamValue.Of("#808080"), ["paint.colour"], 0))),
        "bad name" => Small(("P", GraphParameter.Of(ParamValue.Of(0.1), "layout.gap"))),
        _ => Small(("p", GraphParameter.Of(ParamValue.Of("diagonal"), "layout.orientation"))),
    };

    [Theory]
    [MemberData(nameof(FaultCases))]
    public void Every_fault_names_its_parameter(string name, string message)
    {
        var faults = GraphValidator.Validate(FaultGraph(name));
        var fault = Assert.Single(faults);
        Assert.Equal(message, fault.Message);
        Assert.Equal(message[12..message.IndexOf('\'', 12)], fault.GraphParameter);
        Assert.Null(fault.Node);
    }

    [Fact]
    public void A_list_parameter_cannot_be_driven()
    {
        var g = TextureGraph.Empty(64).WithNode("c", GraphNode.Of("colour")).WithNode("out", GraphNode.Of("output", [new("in", "c")], [new("role", ParamValue.Of("normal"))]))
            .WithNode("name_out", GraphNode.Of("output", [new("in", "c")], [new("name", ParamValue.Of("edge"))]))
            .WithParameter("p", GraphParameter.Of(ParamValue.Of("edge"), "name_out.name"));
        Assert.Equal("parameters['p']: drives 'name_out.name', a name, which a graph parameter cannot drive; it drives a whole number, a number, a choice, two whole numbers or a colour", Faults(g));
    }

    /// <summary>A node carrying both list kinds, for the person's ruling (2026-11-20): a list stays a fault when driven.</summary>
    private sealed class ListsNode : NodeType
    {
        public override string Name => "lists";
        public override IReadOnlyList<InputPort> Inputs { get; } = [];
        public override IReadOnlyList<ParamSpec> Parameters { get; } =
            [ParamSpec.Floats("stops", 2, 8, 0, 1, 0, 1), ParamSpec.Colours("colours", 0, 4, "#000000", "#ffffff")];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;
        public override void Evaluate(NodeEvaluation e) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("l.stops", "a list of numbers")]
    [InlineData("l.colours", "a list of colours")]
    public void A_floats_or_colours_parameter_cannot_be_driven(string drive, string kind)
    {
        var registry = NodeRegistry.BuiltIn.With(new ListsNode());
        var value = drive == "l.stops" ? ParamValue.Of([0.0, 1.0]) : ParamValue.OfTexts(["#000000", "#ffffff"]);
        var g = TextureGraph.Empty(64).WithNode("l", GraphNode.Of("lists")).WithParameter("p", GraphParameter.Of(value, drive));
        var faults = string.Join(Environment.NewLine, GraphValidator.Validate(g, registry).Select(f => f.Message));
        Assert.Contains($"parameters['p']: drives '{drive}', {kind}, which a graph parameter cannot drive;", faults);
    }

    [Fact]
    public void The_narrowest_range_is_the_intersection_worked_by_hand()
    {
        // tiles' gap is a float 0..0.45 and its jitter a float 0..1: together 0..0.45.
        var g = Small(("p", GraphParameter.Of(ParamValue.Of(0.2), "layout.gap", "layout.jitter")));
        Assert.Empty(GraphValidator.Validate(g));
        var range = GraphParameters.Of(g)["p"].Range;
        Assert.Equal((0, 0.45), (range.Min, range.Max));
        // levels' out_low is wider (its own range); min and max narrow the intersection further.
        var narrowed = Small(("p", new GraphParameter(ParamValue.Of(0.2), ["layout.gap", "tone.out_low"], 0.1, 0.3)));
        Assert.Equal("a number from 0.1 to 0.3", GraphParameters.Of(narrowed)["p"].Range.Describe());
        // An enum takes the choices the driven parameters share; whole numbers round inward.
        var count = Small(("p", new GraphParameter(ParamValue.Of(4, 4), ["layout.count"], 1.5, 8.5)));
        Assert.Equal("two whole numbers [x, y], each from 2 to 8", GraphParameters.Of(count)["p"].Range.Describe());
    }

    [Fact]
    public void A_driven_parameter_takes_the_value_and_its_own_is_ignored()
    {
        var own = Small().WithNode("layout", GraphNode.Of("tiles", parameters: [new("gap", ParamValue.Of(0.2))]));
        var driven = Small(("mortar", GraphParameter.Of(ParamValue.Of(0.2), "layout.gap")))
            .WithNode("layout", GraphNode.Of("tiles", parameters: [new("gap", ParamValue.Of(7.0))])); // out of tiles' range, but ignored
        Assert.Empty(GraphValidator.Validate(driven));
        Assert.Equal(Rgba(own, OutputRole.BaseColour), Rgba(driven, OutputRole.BaseColour));
    }

    [Fact]
    public void An_override_equals_the_same_value_set_in_the_graph_byte_for_byte()
    {
        var g = Bricks();
        var overridden = GraphParameters.WithOverrides(g, Grey);
        Assert.True(overridden.Ok, overridden.Error);
        var set = g.WithParameter("mortar", g.Parameters["mortar"] with { Value = ParamValue.Of(0.08) })
            .WithParameter("tint", g.Parameters["tint"] with { Value = ParamValue.Of("#7a7a70") });
        Assert.Equal(TexGraphFile.Write(set), TexGraphFile.Write(overridden.Graph!));
        foreach (var role in FourRoles) Assert.Equal(Rgba(set, role), Rgba(overridden.Graph!, role));
        Assert.Equal(TexGraphFile.Write(Bricks()), TexGraphFile.Write(g)); // the graph is not changed
        Assert.Equal("parameters['mortar']: is 0.5, but must be a number from 0 to 0.45", GraphParameters.WithOverrides(g, new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(0.5) }).Error);
        Assert.Equal("parameters['wear']: the graph has no such parameter; its parameters are jitter, mortar, tint", GraphParameters.WithOverrides(g, new Dictionary<string, ParamValue> { ["wear"] = ParamValue.Of(0.5) }).Error);
    }

    [Fact]
    public void The_bricks_keep_their_bytes_and_the_grey_variant_equals_its_committed_pngs()
    {
        var g = Bricks();
        // The bricks as T-136 wrote them, with the values in the nodes: the same texels.
        var v1 = g with { Parameters = g.Parameters.Clear() };
        v1 = v1.WithNode("layout", v1.Nodes["layout"].WithParam("gap", ParamValue.Of(0.1)).WithParam("jitter", ParamValue.Of(0.25)))
            .WithNode("clay", v1.Nodes["clay"].WithParam("colour", ParamValue.Of("#a8452f")));
        Assert.StartsWith("{\n  \"version\": 1,", TexGraphFile.Write(v1));
        var grey = GraphParameters.WithOverrides(g, Grey).Graph!;
        foreach (var role in FourRoles)
        {
            Assert.Equal(Rgba(v1, role), Rgba(g, role));
            Assert.Equal(Committed(Path.Combine("bricks"), role), Rgba(g, role));
            Assert.Equal(Committed(Path.Combine("bricks", "variants", "grey"), role), Rgba(grey, role));
        }
        Assert.NotEqual(Rgba(g, OutputRole.BaseColour), Rgba(grey, OutputRole.BaseColour));
    }

    /// <summary>
    /// Broken copies of the machinery, made here and never in the product, and the check that catches each: an override
    /// not substituted into the nodes it drives (the variant's committed PNGs), and a range that is not the narrowest (the
    /// out-of-range fault).
    /// </summary>
    [Fact]
    public void Broken_copies_are_caught()
    {
        var g = Bricks();
        // 1. Not substituted: the overrides are dropped, so the nodes keep the graph's values.
        Func<TextureGraph, IReadOnlyDictionary<string, ParamValue>, TextureGraph> notSubstituted = (graph, _) => graph;
        Assert.False(VariantMatches(notSubstituted(g, Grey)), "the variant check passes a graph whose overrides were not substituted");
        Assert.True(VariantMatches(GraphParameters.WithOverrides(g, Grey).Graph!));

        // 2. Not the narrowest range: the widest of the driven ranges (gap 0..0.45 and jitter 0..1 taken as 0..1) passes 0.5.
        static string? WidestRangeCheck(TextureGraph graph, string name, ParamValue value)
        {
            var specs = graph.Parameters[name].Drives.Select(d => { GraphParameter.TryParseDrive(d, out var n, out var p); return NodeRegistry.BuiltIn.Find(graph.Nodes[n].Type)!.Parameter(p)!; });
            var wide = ParamSpec.Float(name, specs.Min(s => s.Min), specs.Max(s => s.Max), 0);
            return wide.Check(value);
        }
        var both = Small(("p", GraphParameter.Of(ParamValue.Of(0.5), "layout.gap", "layout.jitter")));
        Assert.Null(WidestRangeCheck(both, "p", ParamValue.Of(0.5))); // the broken copy lets it through
        Assert.Equal("parameters['p']: is 0.5, but must be a number from 0 to 0.45", Faults(both)); // the fault catches it

        bool VariantMatches(TextureGraph variant) => FourRoles.All(r => Rgba(variant, r).SequenceEqual(Committed(Path.Combine("bricks", "variants", "grey"), r)));
    }

    /// <summary>RV-76: a parameter whose value is a list of strings (edit_graph's <c>["#000000", "#ffffff"]</c>) is written, reads back and is faulted by kind, as a list of numbers is; a mixed list stays a load error.</summary>
    [Fact]
    public void A_parameter_whose_value_is_a_list_of_strings_reads_back_and_is_faulted_by_kind()
    {
        var g = Small(("p", GraphParameter.Of(ParamValue.OfTexts(["#000000", "#ffffff"]), "paint.colour")));
        var text = TexGraphFile.Write(g);
        Assert.Contains("\"value\": [\"#000000\", \"#ffffff\"]", text);
        var read = TexGraphFile.Read(text);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Equal(text, TexGraphFile.Write(read.Graph!));
        var numbers = TexGraphFile.Read(TexGraphFile.Write(Small(("p", GraphParameter.Of(ParamValue.Of(0.0, 1.0), "paint.colour"))))).Graph!;
        Assert.NotEmpty(GraphValidator.Validate(numbers));
        var faults = GraphValidator.Validate(read.Graph!);
        Assert.Contains(faults, f => f.Message.StartsWith("parameters['p']", StringComparison.Ordinal));
        Assert.Equal(text, TexGraphFile.Read(TexGraphFile.WriteShowingDriven(g)).Graph is { } shown ? TexGraphFile.Write(shown) : null);
        var mixed = TexGraphFile.Read(text.Replace("[\"#000000\", \"#ffffff\"]", "[\"#000000\", 1]"));
        Assert.False(mixed.Loaded);
        Assert.Contains("all numbers or all strings", mixed.Error!.ToString());
    }

    /// <summary>RV-76: version 1 has no parameters, so writing a graph that has them in it is refused, not written with them and the node values they drive dropped.</summary>
    [Fact]
    public void Writing_a_graph_with_parameters_as_version_1_is_refused()
    {
        var g = Small(("c", GraphParameter.Of(ParamValue.Of("#123456"), "paint.colour")));
        var e = Assert.Throws<ArgumentException>(() => TexGraphFile.Write(g, TexGraphFormat.Version1));
        Assert.Contains("exposed parameters (c)", e.Message);
        Assert.StartsWith("{\n  \"version\": 2,", TexGraphFile.Write(g));
    }

    private static byte[] Rgba(TextureGraph g, OutputRole role) => OutputImage.Rgba8(new GraphEvaluator(g), role, g.Size);

    private static byte[] Committed(string folder, OutputRole role) =>
        ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(Root(), "samples", "graphs", folder, OutputRoles.Name(role) + ".png")), ColorComponents.RedGreenBlueAlpha).Data;
}
