using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>graph</c> (T-140, format version 2): another <c>.texgraph</c> used as one node. Parameter <c>path</c>, relative to
/// the using graph's file (<see cref="TextureGraph.Folder"/>); its other parameters are the nested graph's exposed
/// parameters by name, its inputs the nested graph's <c>input</c> nodes by name, and its outputs the nested graph's
/// outputs by role or name. It is never evaluated: <see cref="GraphValidator"/> expands it inline, its nodes renamed
/// <c>&lt;node&gt;/&lt;id&gt;</c>, so the evaluator sees only the nodes it already knows.
/// </summary>
public sealed class NestedGraphNode : NodeType
{
    public const string TypeName = "graph";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<string> Outputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.FilePath("path")];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) =>
        throw new InvalidOperationException("a graph node is expanded at validation, not evaluated");

    public override void Evaluate(NodeEvaluation evaluation) =>
        throw new InvalidOperationException("a graph node is expanded at validation, not evaluated");
}
