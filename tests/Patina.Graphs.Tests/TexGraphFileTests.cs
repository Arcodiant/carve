using Xunit;

namespace Patina.Graphs.Tests;

[Trait("Tier", "unit")]
public class TexGraphFileTests
{
    // Every field: version, size, seed, nodes; a node with type only, with inputs, with params of each JSON kind
    // (a whole number, a fraction, a string, an array), an "id.port" source, and a string needing an escape.
    private const string EveryField =
        "{\n" +
        "  \"version\": 1,\n" +
        "  \"size\": 512,\n" +
        "  \"seed\": 4294967295,\n" +
        "  \"nodes\": {\n" +
        "    \"blurred\": { \"type\": \"blur\", \"inputs\": { \"in\": \"noise_1\" } },\n" +
        "    \"noise_1\": { \"type\": \"hash\", \"params\": { \"cells\": [8, 1], \"gain\": 0.1, \"offset\": 3 } },\n" +
        "    \"out\": { \"type\": \"output\", \"inputs\": { \"in\": \"blurred\" }, \"params\": { \"role\": \"occlusion\" } },\n" +
        "    \"paint\": { \"type\": \"colour\", \"params\": { \"colour\": \"#aa7040\" } },\n" +
        "    \"parts\": { \"type\": \"split\", \"inputs\": { \"in\": \"paint\" } },\n" +
        "    \"red\": { \"type\": \"output\", \"inputs\": { \"in\": \"parts.r\" }, \"params\": { \"role\": \"roughness\" } },\n" +
        "    \"z\": { \"type\": \"colour\", \"params\": { \"note\": \"a \\\"quoted\\\" word\" } }\n" +
        "  }\n" +
        "}\n";

    [Fact]
    public void EveryFieldRoundTripsByteForByte()
    {
        var read = TexGraphFile.Read(EveryField, TestNodes.Registry);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Equal(EveryField, TexGraphFile.Write(read.Graph!));
        var again = TexGraphFile.Read(TexGraphFile.Write(read.Graph!), TestNodes.Registry);
        Assert.Equal(EveryField, TexGraphFile.Write(again.Graph!));

        var g = read.Graph!;
        Assert.Equal(512, g.Size);
        Assert.Equal(uint.MaxValue, g.Seed);
        Assert.Equal("parts.r", g.Nodes["red"].Inputs["in"]);
        Assert.Equal(ParamValue.Of(8, 1), g.Nodes["noise_1"].Params["cells"]);
        Assert.Equal(ParamValue.Of(0.1), g.Nodes["noise_1"].Params["gain"]);
        Assert.Equal("a \"quoted\" word", g.Nodes["z"].Params["note"].Text);
        // The planted unknown parameter is a fault, and the graph loaded all the same.
        Assert.Equal([("z", "note")], read.Faults.Select(f => (f.Node, f.Parameter)));
    }

    [Fact]
    public void WritingSortsNodesInputsAndParamsAndNormalisesSpacing()
    {
        const string loose = """{"nodes":{"b":{"params":{"role":"normal"},"type":"output","inputs":{"in":"a"}},"a":{"type":"colour"}},"seed":7,"size":256,"version":1}""";
        var text = TexGraphFile.Write(TexGraphFile.Read(loose).Graph!);
        Assert.Equal(
            "{\n  \"version\": 1,\n  \"size\": 256,\n  \"seed\": 7,\n  \"nodes\": {\n" +
            "    \"a\": { \"type\": \"colour\" },\n" +
            "    \"b\": { \"type\": \"output\", \"inputs\": { \"in\": \"a\" }, \"params\": { \"role\": \"normal\" } }\n" +
            "  }\n}\n", text);
    }

    [Fact]
    public void AGraphMadeInCodeRoundTrips()
    {
        var g = TextureGraph.Empty(1024, 7)
            .WithNode("wood", GraphNode.Of("colour").WithParam("colour", ParamValue.Of("#aa7040")))
            .WithNode("out", GraphNode.Of("output").WithInput("in", "wood").WithParam("role", ParamValue.Of("base_colour")));
        var text = TexGraphFile.Write(g);
        Assert.Equal(text, TexGraphFile.Write(TexGraphFile.Read(text).Graph!));
        Assert.Equal("{\n  \"version\": 1,\n  \"size\": 1024,\n  \"seed\": 7,\n  \"nodes\": {}\n}\n", TexGraphFile.Write(TextureGraph.Empty(1024, 7)));
    }

    [Theory]
    [InlineData("{ \"version\": 1, \"size\": 256, \"seed\": 7, \"nodes\": { ", 1, "not JSON")]
    [InlineData("{\n  \"version\": 2,\n  \"size\": 256, \"seed\": 7, \"nodes\": {} }", 2, "version 2 is not known")]
    [InlineData("{\n  \"version\": 1,\n  \"size\": 300, \"seed\": 7, \"nodes\": {} }", 3, "power of two from 32 to 8192")]
    [InlineData("{ \"version\": 1, \"size\": 16384, \"seed\": 7, \"nodes\": {} }", 1, "power of two")]
    [InlineData("{ \"version\": 1, \"size\": 256, \"seed\": -1, \"nodes\": {} }", 1, "seed is a whole number from 0 to 4294967295")]
    [InlineData("{ \"version\": 1, \"size\": 256, \"seed\": 7, \"nodes\": {}, \"colour\": 1 }", 1, "unknown key 'colour'")]
    [InlineData("{ \"version\": 1, \"size\": 256, \"seed\": 7 }", 1, "no nodes")]
    [InlineData("{ \"version\": 1, \"size\": 256, \"seed\": 7, \"nodes\": {\n \"a\": { \"type\": \"colour\" },\n \"a\": { \"type\": \"colour\" } } }", 3, "'a' is given twice")]
    [InlineData("{ \"version\": 1, \"size\": 256, \"seed\": 7, \"nodes\": { \"a\": { \"inputs\": {} } } }", 1, "node 'a' has no type")]
    [InlineData("{ \"version\": 1, \"size\": 256, \"seed\": 7, \"nodes\": { \"a\": { \"type\": \"colour\", \"params\": { \"colour\": true } } } }", 1, "node 'a' parameter 'colour'")]
    public void AFileItCannotTakeIsALoadErrorWithItsPosition(string text, int line, string says)
    {
        var read = TexGraphFile.Read(text);
        Assert.False(read.Loaded);
        Assert.NotNull(read.Error);
        Assert.Contains(says, read.Error!.Message);
        Assert.Equal(line, read.Error.Line);
        Assert.True(read.Error.Column >= 1);
    }

    [Fact]
    public void ALoadErrorGivesTheColumn()
    {
        var read = TexGraphFile.Read("{\n  \"version\": 1,\n  \"size\": 300,\n  \"seed\": 7, \"nodes\": {} }");
        Assert.Equal((3, 11), (read.Error!.Line, read.Error.Column)); // the 300
    }

    [Fact]
    public void WriteListsNodesInOrdinalOrderWhateverTheDictionarysComparer()
    {
        // The default comparer is the culture's, which puts "a_" before "a0"; ordinal puts "a0" first.
        var colour = GraphNode.Of("colour");
        var inputs = System.Collections.Immutable.ImmutableSortedDictionary.Create<string, string>().Add("b_", "x").Add("b0", "x");
        var nodes = System.Collections.Immutable.ImmutableSortedDictionary.Create<string, GraphNode>()
            .Add("a_", colour).Add("a0", colour with { Inputs = inputs });
        var text = TexGraphFile.Write(new TextureGraph(256, 0, nodes));
        Assert.True(text.IndexOf("\"a0\"", StringComparison.Ordinal) < text.IndexOf("\"a_\"", StringComparison.Ordinal), text);
        Assert.True(text.IndexOf("\"b0\"", StringComparison.Ordinal) < text.IndexOf("\"b_\"", StringComparison.Ordinal), text);
        Assert.Equal(TexGraphFile.Write(TexGraphFile.Read(text).Graph!), text);
    }
}
