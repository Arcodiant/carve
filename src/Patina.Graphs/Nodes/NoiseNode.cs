using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>noise</c>: greyscale value noise in 0..1 on a lattice of <c>cells</c> [x, y] cells across the image (so it tiles).
/// <c>interpolation</c> <c>smooth</c> (smoothstep between lattice values, as T-77) or <c>step</c> (one value per cell);
/// <c>octaves</c> 1 to 4, each doubling the cells and halving the weight, normalised to 0..1; <c>seed</c> an offset on the
/// graph's seed. Sampled at texel centres.
/// </summary>
public sealed class NoiseNode : NodeType
{
    public const string TypeName = "noise";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.IntPair("cells", 1, 4096, 8, 8),
        ParamSpec.Enum("interpolation", "smooth", "smooth", "step"),
        ParamSpec.Int("octaves", 1, 4, 1),
        ParamSpec.Int("seed", 0, 1_000_000, 0),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    /// <summary>T-77's integer hash (<c>TextureSet.Hash</c> in tools/Carve.SampleTextures/TextureSet.cs), copied.</summary>
    public static uint Hash(int x, int y, uint seed)
    {
        var h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA6Bu ^ seed * 0xC2B2AE35u;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return h;
    }

    /// <summary>The lattice value at a cell, 0..1, the cell wrapping so the noise tiles.</summary>
    public static float Lattice(int ix, int iy, int cellsX, int cellsY, uint seed) =>
        (Hash(((ix % cellsX) + cellsX) % cellsX, ((iy % cellsY) + cellsY) % cellsY, seed) & 0xFFFF) / 65535f;

    /// <summary>The seed an octave hashes with: the graph's, the node's, and a step per octave.</summary>
    public static uint OctaveSeed(uint graphSeed, int nodeSeed, int octave) => graphSeed + (uint)nodeSeed + (uint)octave * 7919u;

    /// <summary>One octave's value at texel (x, y) of an image of <paramref name="size"/>.</summary>
    public static float Octave(int x, int y, int size, int cellsX, int cellsY, bool smooth, uint seed)
    {
        float px = (x + 0.5f) * cellsX / size, py = (Band.Wrap(y, size) + 0.5f) * cellsY / size;
        int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py);
        if (!smooth) return Lattice(ix, iy, cellsX, cellsY, seed);
        float fx = px - ix, fy = py - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        var a = Lattice(ix, iy, cellsX, cellsY, seed);
        var b = Lattice(ix + 1, iy, cellsX, cellsY, seed);
        var c = Lattice(ix, iy + 1, cellsX, cellsY, seed);
        var d = Lattice(ix + 1, iy + 1, cellsX, cellsY, seed);
        var top = a + (b - a) * fx;
        var bottom = c + (d - c) * fx;
        return top + (bottom - top) * fy;
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var (cx, cy) = e.Parameters.IntPair("cells");
        var smooth = e.Parameters.Text("interpolation") == "smooth";
        var octaves = e.Parameters.Int("octaves");
        var nodeSeed = e.Parameters.Int("seed");
        var output = e.Output();
        var total = 0f;
        for (var o = 0; o < octaves; o++) total += 1f / (1 << o);
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
        {
            var row = output.Row(y);
            for (var x = 0; x < e.Size; x++)
            {
                float sum = 0;
                for (var o = 0; o < octaves; o++)
                    sum += Octave(x, y, e.Size, cx << o, cy << o, smooth, OctaveSeed(e.Seed, nodeSeed, o)) / (1 << o);
                row[x] = sum / total;
            }
        }
    }
}
