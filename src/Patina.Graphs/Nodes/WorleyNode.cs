using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>worley</c>: Worley (cellular) noise. One feature point in each cell of a <c>cells</c> [x, y] lattice across the
/// image, placed by T-77's integer hash, the cells wrapping so it tiles. <c>jitter</c> 0..1 moves a point from its
/// cell's centre (0) toward anywhere in the cell (1). Distances are in cell widths (each axis measured in its own
/// cells), by <c>metric</c> <c>euclidean</c> or <c>manhattan</c>. Outputs, all greyscale: <c>f1</c> (the distance to the
/// nearest point), <c>edge</c> (f2 - f1, the second nearest less the nearest: 0 on the boundary between two cells) and
/// <c>random</c> (one value 0..1 for the whole cell of the nearest point). <c>seed</c> is an offset on the graph's seed.
/// Sampled at texel centres; the nearest two points are searched in the 3 x 3 cells around the texel's, then in each
/// ring of cells further out that could still hold a point nearer than the second (RV-76: at jitter 1 a point two
/// cells away can be the second nearest), so f1 and f2 are the true nearest two.
/// </summary>
public sealed class WorleyNode : NodeType
{
    public const string TypeName = "worley";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<string> Outputs { get; } = ["f1", "edge", "random"];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.IntPair("cells", 1, 1024, 6, 6),
        ParamSpec.Float("jitter", 0, 1, 1),
        ParamSpec.Enum("metric", "euclidean", "euclidean", "manhattan"),
        ParamSpec.Int("seed", 0, 1_000_000, 0),
    ];

    /// <summary>The furthest ring of cells searched; a second-nearest point is always within it (f2 is at most 3 cells, by either metric).</summary>
    public const int MaxRing = 4;

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    /// <summary>The feature point of cell (<paramref name="ci"/>, <paramref name="cj"/>) (the cell wrapping), as an offset 0..1 from the cell's corner at <paramref name="jitter"/>.</summary>
    public static (float X, float Y) Point(int ci, int cj, int cellsX, int cellsY, float jitter, uint seed)
    {
        var h = NoiseNode.Hash(Tile.Wrap(ci, cellsX), Tile.Wrap(cj, cellsY), seed);
        float hx = (h & 0xFFFF) / 65535f, hy = (h >> 16) / 65535f;
        return (0.5f + (hx - 0.5f) * jitter, 0.5f + (hy - 0.5f) * jitter);
    }

    /// <summary>The value of a cell's <c>random</c> output, 0..1: its own hash, apart from the point's.</summary>
    public static float CellRandom(int ci, int cj, int cellsX, int cellsY, uint seed) =>
        (NoiseNode.Hash(Tile.Wrap(ci, cellsX), Tile.Wrap(cj, cellsY), seed ^ 0x5BD1E995u) & 0xFFFF) / 65535f;

    /// <summary>f1, edge and random at texel (x, y) of an image of <paramref name="size"/>.</summary>
    public static (float F1, float Edge, float Random) Sample(int x, int y, int size, int cellsX, int cellsY, float jitter, bool manhattan, uint seed)
    {
        float px = (x + 0.5f) * cellsX / size, py = (y + 0.5f) * cellsY / size;
        int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py);
        float f1 = float.MaxValue, f2 = float.MaxValue;
        int nearI = ix, nearJ = iy;
        void Visit(int i, int j)
        {
            var (ox, oy) = Point(i, j, cellsX, cellsY, jitter, seed);
            float dx = i + ox - px, dy = j + oy - py;
            var d = manhattan ? MathF.Abs(dx) + MathF.Abs(dy) : MathF.Sqrt(dx * dx + dy * dy);
            if (d < f1) { f2 = f1; f1 = d; nearI = i; nearJ = j; }
            else if (d < f2) f2 = d;
        }
        for (var j = iy - 1; j <= iy + 1; j++)
            for (var i = ix - 1; i <= ix + 1; i++)
                Visit(i, j);
        // A point of a cell r rings out lies at least (r - 1) cells past the texel's nearest window edge, plus the
        // (1 - jitter) / 2 it is held from its cell's side; neither metric is shorter than that axis distance.
        var edge = MathF.Min(MathF.Min(px - ix, ix + 1 - px), MathF.Min(py - iy, iy + 1 - py)) + (1 - jitter) / 2;
        for (var r = 2; r <= MaxRing && f2 > edge + (r - 1); r++)
            for (var j = iy - r; j <= iy + r; j++)
                for (var i = ix - r; i <= ix + r; i++)
                    if (Math.Abs(i - ix) == r || Math.Abs(j - iy) == r) Visit(i, j);
        return (f1, f2 - f1, CellRandom(nearI, nearJ, cellsX, cellsY, seed));
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var (cx, cy) = e.Parameters.IntPair("cells");
        var jitter = e.Parameters.Float("jitter");
        var manhattan = e.Parameters.Text("metric") == "manhattan";
        var seed = NoiseNode.OctaveSeed(e.Seed, e.Parameters.Int("seed"), 0);
        Tile f1 = e.Output("f1"), edge = e.Output("edge"), random = e.Output("random");
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            Span<float> a = f1.Row(y), b = edge.Row(y), c = random.Row(y);
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var s = Sample(x, y, e.Size, cx, cy, jitter, manhattan, seed);
                a[x - e.Rect.X] = s.F1; b[x - e.Rect.X] = s.Edge; c[x - e.Rect.X] = s.Random;
            }
        }
    }
}
