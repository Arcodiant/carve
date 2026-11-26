using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Carve.Graphs;

/// <summary>
/// <c>mesh_position</c>: colour, r, g and b the rest position's x, y and z, each 0 to 1 across the mesh's own bounding box (a
/// flat axis reads 0.5). No parameters. Neutral <c>#808080</c> with no mesh.
/// </summary>
public sealed class MeshPositionNode(IMeshMaps? source) : NodeType
{
    public const string TypeName = "mesh_position";

    /// <summary>128 / 255 in each channel, <c>#808080</c>.</summary>
    public const float Neutral = 128f / 255f;

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Colour;

    public override void Evaluate(NodeEvaluation evaluation) =>
        MeshNodes.Copy(evaluation, source?.Position(evaluation.Size), Neutral);
}
