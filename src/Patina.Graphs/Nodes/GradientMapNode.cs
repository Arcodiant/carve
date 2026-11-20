using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>gradient_map</c>: input <c>in</c> (greyscale), a colour out. <c>positions</c> (2 to 8 numbers, 0 to 1, low to high,
/// equal ones allowed: a hard step, the later colour taking the value itself) and <c>colours</c> (as many <c>"#rrggbb"</c>).
/// A value is looked up between the stops either side of it and the colours are mixed linearly in sRGB, as <c>#rrggbb</c> is
/// held; below the first stop it is the first colour, above the last the last. A count mismatch or positions out of order
/// is a fault on the node's input, naming <c>positions</c> and <c>colours</c>.
/// </summary>
public sealed class GradientMapNode : NodeType
{
    public const string TypeName = "gradient_map";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Grey)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Floats("positions", 2, 8, 0, 1, 0, 1),
        ParamSpec.Colours("colours", 2, 8, "#000000", "#ffffff"),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Colour;

    /// <summary>What is wrong with the pair, or null.</summary>
    public static string? Problem(float[] positions, int colours)
    {
        if (positions.Length != colours)
            return $"parameters 'positions' and 'colours' must be the same length, but positions has {positions.Length} and colours has {colours}";
        for (var i = 1; i < positions.Length; i++)
            if (positions[i] < positions[i - 1])
                return $"parameter 'positions' must run from low to high, but stop {i + 1} ({positions[i]:0.###}) is below stop {i} ({positions[i - 1]:0.###}); 'colours' is read in the same order";
        return null;
    }

    public override IEnumerable<(string Port, string Message)> CheckInputKinds(IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters)
    {
        if (Problem(parameters.Floats("positions"), parameters.Colours("colours").Length) is { } problem) yield return ("in", problem);
    }

    /// <summary>The colour (0..1 per channel, sRGB) for <paramref name="v"/>.</summary>
    public static (float R, float G, float B) Map(float v, float[] positions, (float R, float G, float B)[] colours)
    {
        var last = positions.Length - 1;
        if (v < positions[0]) return colours[0];
        if (v >= positions[last]) return colours[last];
        var i = 1;
        while (positions[i] <= v) i++; // the first stop above v; i - 1 is at or below it
        var t = (v - positions[i - 1]) / (positions[i] - positions[i - 1]);
        var a = colours[i - 1]; var b = colours[i];
        return (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var positions = e.Parameters.Floats("positions");
        var colours = e.Parameters.Colours("colours").Select(c => (c.R / 255f, c.G / 255f, c.B / 255f)).ToArray();
        var input = e.Input("in")!;
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var from = input.Row(y, e.Rect.X, e.Rect.Width);
            var to = output.Row(y);
            for (var i = 0; i < from.Length; i++)
            {
                var (r, g, b) = Map(from[i], positions, colours);
                to[i * 3] = r; to[i * 3 + 1] = g; to[i * 3 + 2] = b;
            }
        }
    }
}
