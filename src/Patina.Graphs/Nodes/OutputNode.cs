using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>output</c>: one of the graph's images. Input <c>in</c>; parameter <c>role</c>, <c>base_colour</c> (default),
/// <c>normal</c>, <c>roughness</c> or <c>occlusion</c>. A <c>normal</c> takes colour (the vector's components), a
/// <c>roughness</c> or <c>occlusion</c> greyscale, a <c>base_colour</c> either. It has no output port: the evaluator
/// packs what reaches <c>in</c> by <see cref="OutputPacking"/>.
/// </summary>
public sealed class OutputNode : NodeType
{
    public const string TypeName = "output";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];

    public override IReadOnlyList<string> Outputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Enum("role", "base_colour", [.. OutputRoles.Names])];

    public static OutputRole Role(NodeParameters parameters)
    {
        OutputRoles.TryParse(parameters.Text("role"), out var role);
        return role;
    }

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) =>
        throw new InvalidOperationException("the output node has no output port");

    public override IEnumerable<(string Port, string Message)> CheckInputKinds(IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters)
    {
        var role = Role(parameters);
        var takes = OutputRoles.Takes(role);
        if (takes != PortKind.Any && inputs.TryGetValue("in", out var gives) && gives != takes)
            yield return ("in", $"role {OutputRoles.Name(role)} takes {PortKinds.Name(takes)}, but it is given {PortKinds.Name(gives)}");
    }

    public override void Evaluate(NodeEvaluation evaluation) =>
        throw new InvalidOperationException("the output node is packed by the evaluator, not evaluated");
}
