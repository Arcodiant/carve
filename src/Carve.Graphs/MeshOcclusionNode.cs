using Carve.Core.Baking;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Carve.Graphs;

/// <summary>
/// <c>mesh_occlusion</c>: grey, 1 open and toward 0 where other surface is near (<c>Bake.Occlusion</c>, against every mesh of
/// the model at rest). Parameters <c>rays</c> 1 to 1024, default 32, and <c>reach</c>, how far a ray looks, in <b>metres on
/// the mesh</b> (not a fraction of the image, so the picture is the same at every size), above 0 to 10, default 0.1.
/// Neutral 1 with no mesh.
/// </summary>
public sealed class MeshOcclusionNode(IMeshMaps? source) : NodeType
{
    public const string TypeName = "mesh_occlusion";

    public const float Neutral = 1f;

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
        [ParamSpec.Int("rays", 1, Bake.MaxRays, Bake.DefaultRays), ParamSpec.Float("reach", 0, 10, 0.1)];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    public override void Evaluate(NodeEvaluation evaluation)
    {
        var reach = (float)MeshNodes.Positive(evaluation, "reach", "metres");
        MeshNodes.Copy(evaluation, source?.Occlusion(evaluation.Size, evaluation.Parameters.Int("rays"), reach), Neutral);
    }
}
