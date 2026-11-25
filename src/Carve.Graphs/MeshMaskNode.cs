using Carve.Core.Baking;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Carve.Graphs;

/// <summary>
/// <c>mesh_mask</c>: grey, 1 on the faces of a group or material and 0 elsewhere (<c>Bake.Masks</c>). Exactly one of the
/// parameters <c>group</c> and <c>material</c>, a name as carve has it. Neutral 1 with no mesh. A name the mesh does not have
/// throws <see cref="MeshMaskException"/>, listing its groups and materials.
/// </summary>
public sealed class MeshMaskNode(IMeshMaps? source) : NodeType
{
    public const string TypeName = "mesh_mask";

    public const float Neutral = 1f;

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Text("group"), ParamSpec.Text("material")];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    /// <summary>Which mask the node names; throws <see cref="MeshParameterException"/> when it names both or neither.</summary>
    public static (MaskSource Source, string Name) Selection(string nodeId, NodeParameters parameters)
    {
        if (NotOne(parameters) is { } wrong) throw new MeshParameterException($"nodes['{nodeId}']: {wrong}");
        return parameters.Has("group") ? (MaskSource.Group, parameters.Text("group")) : (MaskSource.Material, parameters.Text("material"));
    }

    /// <summary>Both or neither named is a graph fault at validation (RV-82), as it is the bake's refusal.</summary>
    public override IEnumerable<(string? Parameter, string Message)> CheckParameters(NodeParameters parameters) =>
        NotOne(parameters) is { } wrong ? [(null, wrong)] : [];

    private static string? NotOne(NodeParameters parameters)
    {
        bool group = parameters.Has("group"), material = parameters.Has("material");
        return group != material ? null : $"give exactly one of params 'group' and 'material'{(group ? ", not both" : "")}";
    }

    public override void Evaluate(NodeEvaluation evaluation)
    {
        var (from, name) = Selection(evaluation.NodeId, evaluation.Parameters);
        MeshNodes.Copy(evaluation, source?.Mask(evaluation.Size, from, name), Neutral);
    }
}
