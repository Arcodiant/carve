using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>fbm</c>: fractal noise, greyscale in 0..1. The sum of <c>octaves</c> (1 to 8) of a base noise, <c>basis</c>
/// <c>value</c> (as <c>noise</c>, smoothstep) or <c>gradient</c> (Perlin-style: hashed unit gradients, quintic fade),
/// on a lattice of <c>cells</c> [x, y] across the image, the cells doubling each octave (whole numbers, so it tiles).
/// Each octave weighs <c>gain</c> (0..1) times the one before; the sum is divided by the weights' sum. <c>style</c>
/// <c>plain</c> sums the octaves' n, <c>ridged</c> sums 1 - |2n - 1| (sharp bright creases) and <c>billow</c> sums
/// |2n - 1| (sharp dark creases), each octave on its own. <c>seed</c> is an offset on the graph's seed.
/// </summary>
public sealed class FbmNode : NodeType
{
    public const string TypeName = "fbm";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.IntPair("cells", 1, 1024, 4, 4),
        ParamSpec.Int("octaves", 1, 8, 4),
        ParamSpec.Float("gain", 0, 1, 0.5),
        ParamSpec.Enum("basis", "value", "value", "gradient"),
        ParamSpec.Enum("style", "plain", "plain", "ridged", "billow"),
        ParamSpec.Int("seed", 0, 1_000_000, 0),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    private const float Half = 0.70710678f;

    // eight unit gradients: the axes and the diagonals
    private static readonly (float X, float Y)[] Gradients = [(1, 0), (-1, 0), (0, 1), (0, -1), (Half, Half), (-Half, Half), (Half, -Half), (-Half, -Half)];

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

    /// <summary>One octave of Perlin-style gradient noise at texel (x, y), 0..1 (the raw -0.7071..0.7071 stretched to it), the lattice wrapping.</summary>
    public static float GradientOctave(int x, int y, int size, int cellsX, int cellsY, uint seed)
    {
        float px = (x + 0.5f) * cellsX / size, py = (y + 0.5f) * cellsY / size;
        int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py);
        float fx = px - ix, fy = py - iy;
        float Corner(int dx, int dy)
        {
            var g = Gradients[NoiseNode.Hash(Tile.Wrap(ix + dx, cellsX), Tile.Wrap(iy + dy, cellsY), seed) & 7];
            return g.X * (fx - dx) + g.Y * (fy - dy);
        }
        float u = Fade(fx), v = Fade(fy);
        var top = Corner(0, 0) + (Corner(1, 0) - Corner(0, 0)) * u;
        var bottom = Corner(0, 1) + (Corner(1, 1) - Corner(0, 1)) * u;
        var n = top + (bottom - top) * v;
        return Math.Clamp(n * Half + 0.5f, 0f, 1f);
    }

    /// <summary>The style applied to one octave's n in 0..1.</summary>
    public static float Style(string style, float n) => style switch
    {
        "ridged" => 1 - MathF.Abs(2 * n - 1),
        "billow" => MathF.Abs(2 * n - 1),
        _ => n,
    };

    /// <summary>The fractal value at texel (x, y), 0..1.</summary>
    public static float Value(int x, int y, int size, int cellsX, int cellsY, int octaves, float gain, bool gradient, string style, uint graphSeed, int nodeSeed)
    {
        float sum = 0, total = 0, weight = 1;
        for (var o = 0; o < octaves; o++)
        {
            var seed = NoiseNode.OctaveSeed(graphSeed, nodeSeed, o);
            var n = gradient
                ? GradientOctave(x, y, size, cellsX << o, cellsY << o, seed)
                : NoiseNode.Octave(x, y, size, cellsX << o, cellsY << o, true, seed);
            sum += weight * Style(style, n);
            total += weight;
            weight *= gain;
        }
        return sum / total;
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var (cx, cy) = e.Parameters.IntPair("cells");
        int octaves = e.Parameters.Int("octaves"), nodeSeed = e.Parameters.Int("seed");
        var gain = e.Parameters.Float("gain");
        var gradient = e.Parameters.Text("basis") == "gradient";
        var style = e.Parameters.Text("style");
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var row = output.Row(y);
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
                row[x - e.Rect.X] = Value(x, y, e.Size, cx, cy, octaves, gain, gradient, style, e.Seed, nodeSeed);
        }
    }
}
