using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Carve.Graphs;

/// <summary>
/// <c>mesh_curvature</c>: grey, 0.5 on flat surface, toward 1 at convex edges and toward 0 at concave ones
/// (<c>Bake.Curvature</c>). Parameter <c>width</c>, how far from an edge it reaches, in <b>metres on the mesh</b> (not a
/// fraction of the image, so the picture is the same at every size), above 0 to 1, default 0.01. Neutral 0.5 with no mesh.
/// </summary>
public sealed class MeshCurvatureNode(IMeshMaps? source) : NodeType
{
    public const string TypeName = "mesh_curvature";

    public const float Neutral = 0.5f;

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Float("width", 0, 1, 0.01)];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    public override void Evaluate(NodeEvaluation evaluation)
    {
        var width = MeshNodes.Positive(evaluation, "width", "metres");
        MeshNodes.Copy(evaluation, source?.Curvature(evaluation.Size, width), Neutral);
    }
}
