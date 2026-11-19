using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Patina.Graphs.Tests;

/// <summary>Node types made outside the package, registered as M-29's mesh nodes will be.</summary>
public static class TestNodes
{
    public static readonly NodeRegistry Registry = NodeRegistry.BuiltIn.With(new HashNode(), new BlurNode(), new SplitNode());

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
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
            for (var x = 0; x < e.Size; x++)
                output.SetGrey(x, y, gain * TestNodes.HashValue(x, Band.Wrap(y, e.Size), e.Seed, offset));
    }
}

/// <summary><c>blur</c>: a 5 x 5 box mean, radius 2, wrapping; greyscale or colour in, the same out.</summary>
public sealed class BlurNode : NodeType
{
    public const int R = 2;
    public override string Name => "blur";
    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];
    public override IReadOnlyList<ParamSpec> Parameters { get; } = [];
    public override int Radius(NodeParameters parameters, int size) => R;

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => inputs["in"];

    public override void Evaluate(NodeEvaluation e)
    {
        var input = e.Input("in")!;
        var output = e.Output();
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
            for (var x = 0; x < e.Size; x++)
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
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
            for (var x = 0; x < e.Size; x++)
            {
                var c = input.Colour(x, y);
                e.Output("r").SetGrey(x, y, c.R);
                e.Output("g").SetGrey(x, y, c.G);
            }
    }
}
