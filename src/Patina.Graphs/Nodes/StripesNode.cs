using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>stripes</c>: greyscale stripes, 1 on a stripe and 0 between. <c>count</c> whole stripes across the image; <c>axis</c> <c>u</c>
/// (stripes stand upright and vary along u), <c>v</c> (they lie across) or <c>diagonal</c> (they run along the image's diagonal, with
/// <c>count</c> stripes along each axis, so they tile); <c>width</c> 0..1 of a period; <c>softness</c> 0..0.5 of a period, the
/// smoothstep's reach each side of each edge, so the ramp, centred on the edge, is 2 x softness wide (RV-76). A stripe is centred on each multiple of 1/count (the first at the image's edge), so
/// a set of half the count at width 0.5 covers every second stripe of a set of the full count. Sampled at texel centres.
/// </summary>
public sealed class StripesNode : NodeType
{
    public const string TypeName = "stripes";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Int("count", 1, 4096, 8),
        ParamSpec.Enum("axis", "u", "u", "v", "diagonal"),
        ParamSpec.Float("width", 0, 1, 0.5),
        ParamSpec.Float("softness", 0, 0.5, 0),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    /// <summary>The stripe's value at phase <paramref name="t"/> (0..1 of a period, the stripe centred on 0).</summary>
    public static float Value(double t, double width, double softness)
    {
        var d = Math.Min(t, 1 - t);               // distance from the nearest stripe centre, in periods
        var half = width / 2;
        if (softness <= 0) return d <= half ? 1 : 0;
        var s = Math.Clamp((d - (half - softness)) / (2 * softness), 0, 1);
        return (float)(1 - s * s * (3 - 2 * s));
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var count = e.Parameters.Int("count");
        var axis = e.Parameters.Text("axis");
        double width = e.Parameters.Float("width"), softness = e.Parameters.Float("softness");
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var row = output.Row(y);
            double v = (Tile.Wrap(y, e.Size) + 0.5) / e.Size;
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var u = (Tile.Wrap(x, e.Size) + 0.5) / e.Size;
                var c = axis == "u" ? u : axis == "v" ? v : u + v;
                var t = c * count;
                row[x - e.Rect.X] = Value(t - Math.Floor(t), width, softness);
            }
        }
    }
}
