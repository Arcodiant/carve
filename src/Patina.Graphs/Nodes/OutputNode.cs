using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>output</c>: one of the graph's images. Input <c>in</c>; parameter <c>role</c>, <c>base_colour</c> (default),
/// <c>normal</c>, <c>roughness</c>, <c>metallic</c>, <c>occlusion</c> or <c>height</c>; or, in place of a role, a
/// <c>name</c> (<c>[a-z][a-z0-9_]*</c>, not a role's nor <c>metallic_roughness</c>, check's packed image), an extra image written as <c>&lt;name&gt;.png</c>, greyscale or
/// colour (a nested graph's extra port in T-140, a mask M-30 layers with). A <c>normal</c> takes colour (the vector's
/// components), a <c>roughness</c>, <c>metallic</c>, <c>occlusion</c> or <c>height</c> greyscale, a <c>base_colour</c> or
/// a named output either. It has no output port: the evaluator packs what reaches <c>in</c> by <see cref="OutputPacking"/>.
/// </summary>
public sealed class OutputNode : NodeType
{
    public const string TypeName = "output";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];

    public override IReadOnlyList<string> Outputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Enum("role", "base_colour", [.. OutputRoles.Names]), ParamSpec.Identifier("name")];

    /// <summary>The output's name when it has one in place of a role; null for a role's output.</summary>
    public static string? GivenName(NodeParameters parameters) => parameters.Has("name") ? parameters.Text("name") : null;

    public static OutputRole Role(NodeParameters parameters)
    {
        OutputRoles.TryParse(parameters.Text("role"), out var role);
        return role;
    }

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) =>
        throw new InvalidOperationException("the output node has no output port");

    public override IEnumerable<(string Port, string Message)> CheckInputKinds(IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters)
    {
        if (GivenName(parameters) is not null) yield break; // a named output takes either
        var role = Role(parameters);
        var takes = OutputRoles.Takes(role);
        if (takes != PortKind.Any && inputs.TryGetValue("in", out var gives) && gives != takes)
            yield return ("in", $"role {OutputRoles.Name(role)} takes {PortKinds.Name(takes)}, but it is given {PortKinds.Name(gives)}");
    }

    public override void Evaluate(NodeEvaluation evaluation) =>
        throw new InvalidOperationException("the output node is packed by the evaluator, not evaluated");
}
