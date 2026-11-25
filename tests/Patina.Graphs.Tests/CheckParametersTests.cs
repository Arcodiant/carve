using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// <see cref="NodeType.CheckParameters"/> (RV-82) through <see cref="GraphValidator"/> alone, on a test type: a refusal no
/// range can say is a fault naming the node and the parameter, asked only once every parameter is in its range.
/// </summary>
[Trait("Tier", "unit")]
public class CheckParametersTests
{
    /// <summary>A grey source whose <c>a</c> and <c>b</c> may not be equal, or both 0 (that one is the node's as a whole).</summary>
    private sealed class PairNode(bool check) : NodeType
    {
        public override string Name => "pair";
        public override IReadOnlyList<InputPort> Inputs { get; } = [];
        public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Float("a", 0, 1, 0.5), ParamSpec.Float("b", 0, 1, 0.25)];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

        public override IEnumerable<(string? Parameter, string Message)> CheckParameters(NodeParameters parameters)
        {
            if (!check) yield break;
            var (a, b) = (parameters.Value("a").Number, parameters.Value("b").Number);
            if (a == 0 && b == 0) yield return (null, "a and b are both 0");
            else if (a == b) yield return ("b", "equals a");
        }

        public override void Evaluate(NodeEvaluation e) => throw new InvalidOperationException("never reached");
    }

    private static readonly NodeRegistry Lax = NodeRegistry.BuiltIn.With(new PairNode(check: false));
    private static readonly NodeRegistry Strict = NodeRegistry.BuiltIn.With(new PairNode(check: true));

    /// <summary>Read with no check, so the graph exists; then validated alone with it.</summary>
    private static IReadOnlyList<GraphFault> Faults(string parameters)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": 16, "seed": 1, "nodes": { "p": { "type": "pair", "params": { {{parameters}} } }, "o": { "type": "output", "inputs": { "in": "p" }, "params": { "role": "occlusion" } } } }""", Lax);
        Assert.True(read.Graph is not null, $"{read.Error} {string.Join("; ", read.Faults)}");
        return GraphValidator.Validate(read.Graph!, Strict);
    }

    [Fact]
    public void A_parameter_the_node_refuses_is_a_fault_naming_the_node_and_the_parameter()
    {
        var fault = Assert.Single(Faults("\"a\": 0.5, \"b\": 0.5"));
        Assert.Equal(("p", "b", "node 'p' parameter 'b': equals a"), (fault.Node, fault.Parameter, fault.Message));
    }

    [Fact]
    public void A_refusal_of_the_whole_node_names_the_node_alone()
    {
        var fault = Assert.Single(Faults("\"a\": 0, \"b\": 0"));
        Assert.Equal(("p", null, "node 'p': a and b are both 0"), (fault.Node, fault.Parameter, fault.Message));
    }

    [Fact]
    public void Parameters_it_accepts_give_no_fault() => Assert.Empty(Faults("\"a\": 0.5, \"b\": 0.25"));

    [Fact]
    public void It_is_not_asked_while_a_parameter_is_out_of_its_range()
    {
        // Out of range, the graph is not read even laxly, so this one goes through the read's own validation.
        var read = TexGraphFile.Read("""{ "version": 1, "size": 16, "seed": 1, "nodes": { "p": { "type": "pair", "params": { "a": 2, "b": 2 } }, "o": { "type": "output", "inputs": { "in": "p" }, "params": { "role": "occlusion" } } } }""", Strict);
        Assert.Null(read.Error);
        var faults = read.Faults;
        Assert.NotEmpty(faults);
        Assert.All(faults, f => Assert.DoesNotContain("equals", f.Message));
    }
}
