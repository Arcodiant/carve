using Xunit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs.Tests;

/// <summary>
/// T-134: the list parameters (<see cref="ParamKind.Floats"/>, <see cref="ParamKind.Colours"/>) read back and written
/// bit-exact and described in words, and version 2's machinery, on a stand-in field and node type held only here.
/// </summary>
[Trait("Tier", "unit")]
public class ListParamsAndVersionTests
{
    /// <summary>A greyscale constant, its first stop; it carries both list kinds so the validator reads them.</summary>
    private sealed class StopsNode : NodeType
    {
        public override string Name => "stops";
        public override IReadOnlyList<InputPort> Inputs { get; } = [];
        public override IReadOnlyList<ParamSpec> Parameters { get; } =
        [
            ParamSpec.Floats("stops", 2, 8, 0, 1, 0, 1),
            ParamSpec.Colours("colours", 0, 4, "#000000", "#ffffff"),
            ParamSpec.Floats("three", 3, 3, -1, 1, 0, 0, 0),
        ];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;
        public override void Evaluate(NodeEvaluation e)
        {
            var v = e.Parameters.Floats("stops")[0];
            var output = e.Output();
            for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
                for (var x = e.Rect.X; x < e.Rect.Right; x++) output.SetGrey(x, y, v);
        }
    }

    private static readonly NodeRegistry Registry = NodeRegistry.BuiltIn.With(new StopsNode());

    private const string Lists = """
        {
          "version": 1,
          "size": 32,
          "seed": 3,
          "nodes": {
            "o": { "type": "output", "inputs": { "in": "s" }, "params": { "role": "occlusion" } },
            "s": { "type": "stops", "params": { "colours": ["#102030", "#A0b0C0"], "stops": [0.1, 0.30000000000000004, 1, 5E-324] } }
          }
        }

        """;

    [Fact]
    public void Both_list_kinds_read_back_and_write_bit_exact()
    {
        var read = TexGraphFile.Read(Lists.Replace("\r\n", "\n"), Registry);
        Assert.Null(read.Error);
        Assert.Empty(read.Faults);
        var s = read.Graph!.Nodes["s"];
        Assert.Equal(ParamValueKind.Numbers, s.Params["stops"].Kind);
        Assert.Equal([0.1, 0.30000000000000004, 1, double.Epsilon], s.Params["stops"].Numbers);
        Assert.Equal(ParamValueKind.Texts, s.Params["colours"].Kind);
        Assert.Equal(["#102030", "#A0b0C0"], s.Params["colours"].Texts);
        var text = TexGraphFile.Write(read.Graph);
        Assert.Contains("\"params\": { \"colours\": [\"#102030\", \"#A0b0C0\"], \"stops\": [0.1, 0.30000000000000004, 1, 5E-324] }", text);
        Assert.StartsWith("{\n  \"version\": 1,\n", text); // list parameters stay version 1
        Assert.Equal(text, TexGraphFile.Write(TexGraphFile.Read(text, Registry).Graph!));
        Assert.Equal([0x1a, 0x1a, 0x1a, 255], OutputImage.Rgba8(new GraphEvaluator(read.Graph, Registry), OutputRole.Occlusion, 32)[..4]); // 0.1 * 255 = 25.5 -> 26
        var p = new NodeParameters(new StopsNode(), s.Params);
        Assert.Equal([(byte)0x10, (byte)0x20, (byte)0x30], new[] { p.Colours("colours")[0].R, p.Colours("colours")[0].G, p.Colours("colours")[0].B });
        Assert.Equal(2, p.Colours("colours").Length);
    }

    [Fact]
    public void An_empty_list_is_numbers_and_either_kind_takes_it()
    {
        var spec = ParamSpec.Colours("c", 0, 2);
        Assert.Null(spec.Check(ParamValue.Of(Array.Empty<double>())));
        Assert.Equal("[]", ParamValue.Of(Array.Empty<double>()).ToString());
        Assert.NotNull(ParamSpec.Floats("f", 1, 2, 0, 1, 0).Check(ParamValue.Of(Array.Empty<double>())));
    }

    [Theory]
    [InlineData("\"stops\": [0.5]", "node 's' parameter 'stops' is [0.5], but must be a list of 2 to 8 numbers [a, b, ...], each from 0 to 1")]
    [InlineData("\"stops\": [0.5, 2]", "node 's' parameter 'stops' is [0.5, 2], but must be a list of 2 to 8 numbers [a, b, ...], each from 0 to 1")]
    [InlineData("\"stops\": [\"#000000\", \"#ffffff\"]", "node 's' parameter 'stops' is [\"#000000\", \"#ffffff\"], but must be a list of 2 to 8 numbers [a, b, ...], each from 0 to 1")]
    [InlineData("\"colours\": [\"#000000\", \"red\"]", "node 's' parameter 'colours' is [\"#000000\", \"red\"], but must be a list of 0 to 4 colours [\"#rrggbb\", ...]")]
    [InlineData("\"colours\": [0.5]", "node 's' parameter 'colours' is [0.5], but must be a list of 0 to 4 colours [\"#rrggbb\", ...]")]
    [InlineData("\"colours\": [\"#000000\", \"#000000\", \"#000000\", \"#000000\", \"#000000\"]", "node 's' parameter 'colours' is [\"#000000\", \"#000000\", \"#000000\", \"#000000\", \"#000000\"], but must be a list of 0 to 4 colours [\"#rrggbb\", ...]")]
    [InlineData("\"three\": [0, 0]", "node 's' parameter 'three' is [0, 0], but must be a list of 3 numbers [a, b, ...], each from -1 to 1")]
    public void A_list_out_of_range_is_a_fault_giving_the_range_in_words(string param, string message)
    {
        var text = $$"""{ "version": 1, "size": 32, "seed": 0, "nodes": { "s": { "type": "stops", "params": { {{param}} } }, "o": { "type": "output", "inputs": { "in": "s" }, "params": { "role": "height" } } } }""";
        var read = TexGraphFile.Read(text, Registry);
        Assert.Null(read.Error);
        Assert.Equal(message, Assert.Single(read.Faults).Message);
    }

    [Fact]
    public void A_mixed_array_is_a_load_error()
    {
        var read = TexGraphFile.Read("""{ "version": 1, "size": 32, "seed": 0, "nodes": { "s": { "type": "stops", "params": { "stops": [0.5, "#000000"] } } } }""", Registry);
        Assert.Equal("node 's' parameter 'stops': an array is all numbers or all strings", read.Error!.Message);
    }

    // ---- Version 2's machinery, on a stand-in held only in these tests.

    /// <summary>A top-level string field version 2 adds; its value is kept beside the graph read, never in the product.</summary>
    private sealed class StandIn : TexGraphField
    {
        public static readonly ConditionalWeakTable<TextureGraph, string> Values = new();
        public override string Name => "stand_in";
        public override int Version => 2;
        public override bool IsIn(TextureGraph graph) => Values.TryGetValue(graph, out _);
        public override string Write(TextureGraph graph) => JsonSerializer.Serialize(Values.TryGetValue(graph, out var v) ? v : "");
        public override Func<TextureGraph, TextureGraph> Read(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.String) throw new FormatException("a stand-in is a string");
            var value = reader.GetString()!;
            return graph => { Values.AddOrUpdate(graph, value); return graph; };
        }
    }

    private static readonly TexGraphFormat Format = TexGraphFormat.Current.With(new StandIn()).WithNodeType("nested", 2);

    private static TextureGraph Plain() =>
        TextureGraph.Empty(64, 9).WithNode("c", GraphNode.Of("colour")).WithNode("o", GraphNode.Of("output", [new("in", "c")]));

    [Fact]
    public void A_graph_using_nothing_later_is_written_as_version_1_byte_for_byte_as_before()
    {
        var g = Plain();
        Assert.Equal(1, Format.VersionOf(g));
        Assert.Equal(TexGraphFile.Write(g), TexGraphFile.Write(g, Format));
        Assert.StartsWith("{\n  \"version\": 1,\n  \"size\": 64,\n  \"seed\": 9,\n  \"nodes\": {\n", TexGraphFile.Write(g, Format));
    }

    [Fact]
    public void A_graph_with_a_version_2_field_is_written_as_version_2_and_reads_back()
    {
        var g = Plain();
        StandIn.Values.AddOrUpdate(g, "kept");
        var text = TexGraphFile.Write(g, Format);
        Assert.StartsWith("{\n  \"version\": 2,\n  \"size\": 64,\n  \"seed\": 9,\n  \"stand_in\": \"kept\",\n  \"nodes\": {\n", text);
        var read = TexGraphFile.Read(text, null, Format);
        Assert.Null(read.Error);
        Assert.True(StandIn.Values.TryGetValue(read.Graph!, out var value));
        Assert.Equal("kept", value);
        Assert.Equal(text, TexGraphFile.Write(read.Graph!, Format));
        // The same text without the format: the field is unknown.
        Assert.Equal("unknown key 'stand_in': a graph has version, size, seed and nodes", TexGraphFile.Read(text).Error!.Message);
        Assert.Equal("unknown key 'other': a graph has version, size, seed, stand_in and nodes", TexGraphFile.Read(text.Replace("stand_in", "other"), null, Format).Error!.Message);
    }

    [Fact]
    public void A_version_2_node_type_makes_a_version_2_file()
    {
        var g = Plain().WithNode("n", GraphNode.Of("nested"));
        Assert.Equal(2, Format.VersionOf(g));
        var text = TexGraphFile.Write(g, Format);
        Assert.StartsWith("{\n  \"version\": 2,\n", text);
        Assert.Equal(text, TexGraphFile.Write(TexGraphFile.Read(text, null, Format).Graph!, Format));
        var v1 = TexGraphFile.Read(text.Replace("\"version\": 2", "\"version\": 1"), null, Format);
        Assert.Equal("node 'n' of type nested needs version 2, but the file is version 1", v1.Error!.Message);
    }

    [Fact]
    public void A_version_1_file_holding_a_version_2_field_is_a_load_error_at_the_field()
    {
        var read = TexGraphFile.Read("{\n  \"version\": 1,\n  \"size\": 64,\n  \"seed\": 9,\n  \"stand_in\": \"x\",\n  \"nodes\": {}\n}\n", null, Format);
        Assert.Equal((5, 3, "'stand_in' needs version 2, but the file is version 1"), (read.Error!.Line, read.Error.Column, read.Error.Message));
        var wrong = TexGraphFile.Read("{\n  \"version\": 2,\n  \"size\": 64,\n  \"seed\": 9,\n  \"stand_in\": 4,\n  \"nodes\": {}\n}\n", null, Format);
        Assert.Equal((5, 15, "stand_in: a stand-in is a string"), (wrong.Error!.Line, wrong.Error.Column, wrong.Error.Message));
    }

    [Fact]
    public void A_version_2_file_needing_nothing_later_reads_and_is_written_back_as_version_1()
    {
        var text = TexGraphFile.Write(Plain());
        var read = TexGraphFile.Read(text.Replace("\"version\": 1", "\"version\": 2"));
        Assert.Null(read.Error);
        Assert.Equal(text, TexGraphFile.Write(read.Graph!));
    }

    [Fact]
    public void Every_committed_texgraph_is_written_byte_for_byte_as_it_is()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Carve.slnx"))) root = Path.GetDirectoryName(root)!;
        var files = Directory.GetFiles(Path.Combine(root, "samples", "graphs"), "*.texgraph");
        Assert.NotEmpty(files);
        foreach (var path in files)
        {
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            var read = TexGraphFile.Read(text);
            Assert.Null(read.Error);
            Assert.Equal(text, TexGraphFile.Write(read.Graph!));
        }
    }
}
