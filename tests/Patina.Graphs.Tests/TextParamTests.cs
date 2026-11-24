using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary><see cref="ParamKind.Text"/> (T-161): free text reads, writes and reads again unchanged; empty text is refused.</summary>
[Trait("Tier", "unit")]
public class TextParamTests
{
    private sealed class LabelNode : NodeType
    {
        public override string Name => "label";
        public override IReadOnlyList<InputPort> Inputs { get; } = [];
        public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Text("text")];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;
        public override void Evaluate(NodeEvaluation e) => throw new NotSupportedException();
    }

    private static readonly NodeRegistry Registry = NodeRegistry.BuiltIn.With(new LabelNode());

    private static string File(string json) =>
        $$"""{ "version": 1, "size": 64, "seed": 1, "nodes": { "l": { "type": "label", "params": { "text": {{json}} } } } }""";

    [Fact]
    public void Text_with_a_space_a_quote_and_a_non_ascii_letter_round_trips()
    {
        const string text = "steel \"jaw\" Ærø";
        var first = TexGraphFile.Read(File("\"steel \\\"jaw\\\" Ærø\""), Registry);
        Assert.True(first.Graph is not null, string.Join("; ", first.Faults.Select(f => f.ToString())));
        Assert.Equal(text, first.Graph!.Nodes["l"].Params["text"].Text);

        var written = TexGraphFile.Write(first.Graph);
        var second = TexGraphFile.Read(written, Registry);
        Assert.True(second.Graph is not null, string.Join("; ", second.Faults.Select(f => f.ToString())));
        Assert.Equal(text, second.Graph!.Nodes["l"].Params["text"].Text);
        Assert.Equal(written, TexGraphFile.Write(second.Graph));
    }

    [Fact]
    public void Empty_text_and_a_number_are_refused()
    {
        foreach (var json in new[] { "\"\"", "3" })
        {
            var read = TexGraphFile.Read(File(json), Registry);
            Assert.Contains(read.Faults, f => f.ToString().Contains("text, not empty"));
        }
    }
}
