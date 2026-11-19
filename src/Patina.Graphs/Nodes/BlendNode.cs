using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>blend</c>: inputs <c>a</c> and <c>b</c>, optional greyscale <c>mask</c>; <c>mode</c> <c>mix</c>, <c>multiply</c>, <c>add</c>,
/// <c>subtract</c>, <c>min</c> or <c>max</c>; <c>opacity</c> 0..1. The result is <c>a</c> moved toward <c>mode(a, b)</c> by
/// <c>opacity * mask</c> (mask 1 where there is none), so in <c>mix</c> the mask takes <c>a</c> where it is 0 and <c>b</c> where it is 1.
/// Colour if either of a and b is colour (a greyscale input is spread to grey), else greyscale. Not clamped: the output packing clamps.
/// </summary>
public sealed class BlendNode : NodeType
{
    public const string TypeName = "blend";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } =
    [
        new("a", PortKind.Any),
        new("b", PortKind.Any),
        new("mask", PortKind.Grey, Required: false),
    ];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Enum("mode", "mix", "mix", "multiply", "add", "subtract", "min", "max"),
        ParamSpec.Float("opacity", 0, 1, 1),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) =>
        inputs["a"] == PortKind.Colour || inputs["b"] == PortKind.Colour ? PortKind.Colour : PortKind.Grey;

    public static float Combine(string mode, float a, float b) => mode switch
    {
        "multiply" => a * b,
        "add" => a + b,
        "subtract" => a - b,
        "min" => MathF.Min(a, b),
        "max" => MathF.Max(a, b),
        _ => b,
    };

    /// <summary><paramref name="a"/> moved toward <c>mode(a, b)</c> by <paramref name="k"/>, the opacity times the mask.</summary>
    public static float Apply(string mode, float a, float b, float k) => a + (Combine(mode, a, b) - a) * k;

    public override void Evaluate(NodeEvaluation e)
    {
        var mode = e.Parameters.Text("mode");
        var opacity = e.Parameters.Float("opacity");
        Band a = e.Input("a")!, b = e.Input("b")!;
        var mask = e.Input("mask");
        var output = e.Output();
        var colour = output.Kind == PortKind.Colour;
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
            for (var x = 0; x < e.Size; x++)
            {
                var k = opacity * (mask?.Grey(x, y) ?? 1f);
                if (!colour)
                {
                    float ga = a.Grey(x, y);
                    output.SetGrey(x, y, Apply(mode, ga, b.Grey(x, y), k));
                    continue;
                }
                var ca = a.Colour(x, y);
                var cb = b.Colour(x, y);
                output.SetColour(x, y,
                    Apply(mode, ca.R, cb.R, k),
                    Apply(mode, ca.G, cb.G, k),
                    Apply(mode, ca.B, cb.B, k));
            }
    }
}
