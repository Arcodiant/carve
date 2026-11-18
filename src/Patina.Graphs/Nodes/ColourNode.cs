using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary><c>colour</c>: a constant sRGB colour, parameter <c>colour</c> <c>"#rrggbb"</c> (default <c>#808080</c>), giving a colour image.</summary>
public sealed class ColourNode : NodeType
{
    public const string TypeName = "colour";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Colour("colour", "#808080")];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Colour;

    public override void Evaluate(NodeEvaluation evaluation)
    {
        var (r, g, b) = evaluation.Parameters.Colour("colour");
        float fr = r / 255f, fg = g / 255f, fb = b / 255f;
        var output = evaluation.Output();
        for (var y = evaluation.Y0; y < evaluation.Y0 + evaluation.Rows; y++)
        {
            var row = output.Row(y);
            for (var i = 0; i < row.Length; i += 3)
            {
                row[i] = fr; row[i + 1] = fg; row[i + 2] = fb;
            }
        }
    }
}
