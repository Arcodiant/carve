using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>levels</c>: input <c>in</c>, greyscale or colour, the same out. Each channel is mapped from <c>in_low</c>..<c>in_high</c>
/// to 0..1 (clamped), raised to 1 / <c>gamma</c> (above 1 brightens), then mapped to <c>out_low</c>..<c>out_high</c>; an
/// <c>out_low</c> above <c>out_high</c> inverts. Defaults 0, 1, 0, 1, 1. When <c>in_high</c> equals <c>in_low</c> the map is a step at it.
/// </summary>
public sealed class LevelsNode : NodeType
{
    public const string TypeName = "levels";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Float("in_low", 0, 1, 0),
        ParamSpec.Float("in_high", 0, 1, 1),
        ParamSpec.Float("out_low", 0, 2, 0),
        ParamSpec.Float("out_high", 0, 2, 1),
        ParamSpec.Float("gamma", 0.1, 10, 1),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => inputs["in"];

    public static float Map(float v, float inLow, float inHigh, float outLow, float outHigh, float gamma)
    {
        var t = inHigh == inLow ? (v >= inLow ? 1f : 0f) : Math.Clamp((v - inLow) / (inHigh - inLow), 0f, 1f);
        if (gamma != 1) t = (float)DeterministicMath.Pow(t, 1.0 / gamma);
        return outLow + (outHigh - outLow) * t;
    }

    public override void Evaluate(NodeEvaluation e)
    {
        float lo = e.Parameters.Float("in_low"), hi = e.Parameters.Float("in_high");
        float olo = e.Parameters.Float("out_low"), ohi = e.Parameters.Float("out_high"), gamma = e.Parameters.Float("gamma");
        var input = e.Input("in")!;
        var output = e.Output();
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
        {
            var from = input.Row(y);
            var to = output.Row(y);
            for (var i = 0; i < to.Length; i++) to[i] = Map(from[i], lo, hi, olo, ohi, gamma);
        }
    }
}
