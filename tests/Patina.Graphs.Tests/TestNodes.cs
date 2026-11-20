using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs.Tests;

/// <summary>Node types made outside the package, registered as M-29's mesh nodes will be.</summary>
public static class TestNodes
{
    public static readonly NodeRegistry Registry = NodeRegistry.BuiltIn.With(new HashNode(), new BlurNode(), new SplitNode());

    /// <summary><see cref="Registry"/> and the nodes that read away from their texels, <c>rotate</c> and <c>peek</c>, kept apart so the type lists other tests hold stay as they are.</summary>
    public static readonly NodeRegistry Reaching = Registry.With(new RotateNode(), new PeekNode());

    public static uint Hash(int x, int y, uint seed)
    {
        var h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA6Bu ^ seed * 0xC2B2AE35u;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return h;
    }

    /// <summary>The hash node's value at an image texel.</summary>
    public static float HashValue(int x, int y, uint seed, int offset) => (Hash(x, y, seed + (uint)offset) & 0xFFFF) / 65535f;
}

/// <summary><c>hash</c>: a greyscale value per texel from an integer hash, pointwise. Parameters of three kinds, for the round trip.</summary>
public sealed class HashNode : NodeType
{
    public override string Name => "hash";
    public override IReadOnlyList<InputPort> Inputs { get; } = [];
    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Int("offset", 0, 1000, 0),
        ParamSpec.IntPair("cells", 1, 4096, 1, 1),
        ParamSpec.Float("gain", 0, 2, 1),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    public override void Evaluate(NodeEvaluation e)
    {
        var offset = e.Parameters.Int("offset");
        var gain = e.Parameters.Float("gain");
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
                output.SetGrey(x, y, gain * TestNodes.HashValue(x, Tile.Wrap(y, e.Size), e.Seed, offset));
    }
}

/// <summary><c>blur</c>: a 5 x 5 box mean, radius 2, wrapping; greyscale or colour in, the same out.</summary>
public sealed class BlurNode : NodeType
{
    public const int R = 2;
    public override string Name => "blur";
    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];
    public override IReadOnlyList<ParamSpec> Parameters { get; } = [];
    public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) => output.Inflate(R);

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => inputs["in"];

    public override void Evaluate(NodeEvaluation e)
    {
        var input = e.Input("in")!;
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                float r = 0, g = 0, b = 0;
                for (var dy = -R; dy <= R; dy++)
                    for (var dx = -R; dx <= R; dx++)
                    {
                        var c = input.Colour(x + dx, y + dy);
                        r += c.R; g += c.G; b += c.B;
                    }
                if (output.Kind == PortKind.Grey) output.SetGrey(x, y, r / 25f);
                else output.SetColour(x, y, r / 25f, g / 25f, b / 25f);
            }
    }
}

/// <summary><c>split</c>: a colour in, two greyscale ports out, <c>r</c> and <c>g</c>, for the <c>"id.port"</c> grammar.</summary>
public sealed class SplitNode : NodeType
{
    public override string Name => "split";
    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Colour)];
    public override IReadOnlyList<string> Outputs { get; } = ["r", "g"];
    public override IReadOnlyList<ParamSpec> Parameters { get; } = [];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    public override void Evaluate(NodeEvaluation e)
    {
        var input = e.Input("in")!;
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var c = input.Colour(x, y);
                e.Output("r").SetGrey(x, y, c.R);
                e.Output("g").SetGrey(x, y, c.G);
            }
    }
}

/// <summary>
/// <c>rotate</c>: its greyscale input turned by <c>degrees</c> about the image centre and moved by half the image, nearest
/// texel: a node reading far from the texel it writes. It asks for the bounding box of its tile's corners mapped back.
/// </summary>
public sealed class RotateNode : NodeType
{
    public override string Name => "rotate";
    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Grey)];
    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Float("degrees", -360, 360, 0)];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    /// <summary>The cosine and sine of the turn, worked once from the parameter.</summary>
    public static (float Cos, float Sin) Turn(float degrees) => (MathF.Cos(degrees * MathF.PI / 180f), MathF.Sin(degrees * MathF.PI / 180f));

    /// <summary>Where output point (px, py) reads its input, in texel units, unwrapped.</summary>
    public static (float X, float Y) Source(float px, float py, (float Cos, float Sin) turn, int size)
    {
        float dx = px - size * 0.5f, dy = py - size * 0.5f;
        return (turn.Cos * dx + turn.Sin * dy + size, -turn.Sin * dx + turn.Cos * dy + size);
    }

    public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size)
    {
        var turn = Turn(parameters.Float("degrees"));
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var (cx, cy) in new[] { (output.X, output.Y), (output.Right, output.Y), (output.X, output.Bottom), (output.Right, output.Bottom) })
        {
            var (sx, sy) = Source(cx, cy, turn, size);
            minX = MathF.Min(minX, sx); maxX = MathF.Max(maxX, sx);
            minY = MathF.Min(minY, sy); maxY = MathF.Max(maxY, sy);
        }
        return TexelRect.Bounding(minX, minY, maxX, maxY, margin: 2);
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var turn = Turn(e.Parameters.Float("degrees"));
        var input = e.Input("in")!;
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var (sx, sy) = Source(x + 0.5f, y + 0.5f, turn, e.Size);
                output.SetGrey(x, y, input.Grey((int)MathF.Floor(sx), (int)MathF.Floor(sy)));
            }
    }
}

/// <summary><c>peek</c>: reads the texel to the right of each one while asking only for its own tile, to show the evaluator refuses it.</summary>
public sealed class PeekNode : NodeType
{
    public override string Name => "peek";
    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Grey)];
    public override IReadOnlyList<ParamSpec> Parameters { get; } = [];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    public override void Evaluate(NodeEvaluation e)
    {
        var input = e.Input("in")!;
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++) output.SetGrey(x, y, input.Grey(x + 1, y));
    }
}
