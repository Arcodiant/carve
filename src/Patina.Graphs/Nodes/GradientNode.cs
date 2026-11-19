using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>gradient</c>: greyscale ramp along <c>axis</c> <c>u</c> or <c>v</c>, <c>repeat</c> times across the image. <c>shape</c>
/// <c>linear</c> is 0 to 1 across each repeat; <c>triangle</c> is 0 at each repeat's edges and 1 in its middle. Sampled at texel centres.
/// </summary>
public sealed class GradientNode : NodeType
{
    public const string TypeName = "gradient";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Enum("axis", "u", "u", "v"),
        ParamSpec.Int("repeat", 1, 4096, 1),
        ParamSpec.Enum("shape", "linear", "linear", "triangle"),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    public static float Value(int texel, int size, int repeat, bool triangle)
    {
        var t = (texel + 0.5f) * repeat / size;
        var f = t - MathF.Floor(t);
        return triangle ? 1 - MathF.Abs(2 * f - 1) : f;
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var alongU = e.Parameters.Text("axis") == "u";
        var repeat = e.Parameters.Int("repeat");
        var triangle = e.Parameters.Text("shape") == "triangle";
        var output = e.Output();
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
        {
            var row = output.Row(y);
            if (alongU)
                for (var x = 0; x < e.Size; x++) row[x] = Value(x, e.Size, repeat, triangle);
            else
                row.Fill(Value(Band.Wrap(y, e.Size), e.Size, repeat, triangle));
        }
    }
}
