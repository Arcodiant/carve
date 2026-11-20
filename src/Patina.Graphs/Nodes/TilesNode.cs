using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>tiles</c>: a layout of tiles or bricks. <c>count</c> [x, y] tiles across the image; <c>stagger</c> 0..1, the shift of every
/// second row in tiles (0.5 is running bond); <c>gap</c> 0..0.45 of a tile's width (the image's width over <c>count</c> x); <c>bevel</c>
/// 0..0.5 of a tile's width, the ramp inside each tile's edge; <c>jitter</c> 0..1, each tile's size and place moved by the hash but
/// kept inside its slot (so a tile never reaches a neighbour's gap); <c>orientation</c> <c>rows</c> (rows run along u, every second
/// row shifted along u) or <c>columns</c> (columns run along v, every second column shifted along v). Outputs: <c>mask</c> (1 on a
/// tile, 0 in the gap), <c>height</c> (0 in the gap, rising over the bevel to 1), <c>random</c> (one value 0..1 for a tile) and
/// <c>uv</c> (colour: red and green are the texel's place in its tile, 0..1, clamped in the gap, blue 0). Sampled at texel centres,
/// worked from the texel alone, so it is the same in any tile of any size. A gap is held to 90% of a tile's extent along either axis.
/// </summary>
public sealed class TilesNode : NodeType
{
    public const string TypeName = "tiles";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<string> Outputs { get; } = ["mask", "height", "random", "uv"];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.IntPair("count", 1, 256, 4, 4),
        ParamSpec.Float("stagger", 0, 1, 0),
        ParamSpec.Float("gap", 0, 0.45, 0.1),
        ParamSpec.Float("bevel", 0, 0.5, 0.1),
        ParamSpec.Float("jitter", 0, 1, 0),
        ParamSpec.Enum("orientation", "rows", "rows", "columns"),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) =>
        port == "uv" ? PortKind.Colour : PortKind.Grey;

    /// <summary>What one texel is: inside its tile or in the gap, its height, its tile's random value, and its place in the tile.</summary>
    public readonly record struct Texel(bool Inside, float Height, float Random, float U, float V);

    public readonly record struct Layout(int CountX, int CountY, double Stagger, double Gap, double Bevel, double Jitter, bool Columns, uint Seed)
    {
        public static Layout Of(NodeParameters p, uint seed)
        {
            var (cx, cy) = p.IntPair("count");
            return new Layout(cx, cy, p.Float("stagger"), p.Float("gap"), p.Float("bevel"), p.Float("jitter"), p.Text("orientation") == "columns", seed);
        }

        private static float Hash01(int i, int j, uint seed, int k) => (NoiseNode.Hash(i, j, seed + (uint)k * 7919u) & 0xFFFF) / 65535f;

        /// <summary>The texel at (x, y) of an image of <paramref name="size"/>, at its centre.</summary>
        public Texel At(int x, int y, int size)
        {
            double u = (Tile.Wrap(x, size) + 0.5) / size, v = (Tile.Wrap(y, size) + 0.5) / size;
            // a runs along the rows (u for rows, v for columns); b across them.
            double a = Columns ? v : u, b = Columns ? u : v;
            int na = Columns ? CountY : CountX, nb = Columns ? CountX : CountY;
            double sa = 1.0 / na, sb = 1.0 / nb;
            var j = Math.Min((int)Math.Floor(b * nb), nb - 1);
            var p = a * na - ((j & 1) == 1 ? Stagger : 0);
            var i = (int)Math.Floor(p);
            double fa = p - i, fb = b * nb - j;
            i = Tile.Wrap(i, na);

            var gap = Gap / CountX;
            double ga = Math.Min(gap, 0.9 * sa), gb = Math.Min(gap, 0.9 * sb);
            double la = sa - ga, lb = sb - gb;
            var tla = la * (1 - 0.6 * Jitter * Hash01(i, j, Seed, 1));
            var tlb = lb * (1 - 0.6 * Jitter * Hash01(i, j, Seed, 2));
            var a0 = ga / 2 + (la - tla) * Hash01(i, j, Seed, 3) * Jitter;
            var b0 = gb / 2 + (lb - tlb) * Hash01(i, j, Seed, 4) * Jitter;
            double ia = fa * sa, ib = fb * sb;
            var d = Math.Min(Math.Min(ia - a0, a0 + tla - ia), Math.Min(ib - b0, b0 + tlb - ib));
            var bevel = Bevel / CountX;
            var height = d < 0 ? 0 : bevel > 0 ? Math.Min(1, d / bevel) : 1;
            var ta = Math.Clamp((ia - a0) / tla, 0, 1);
            var tb = Math.Clamp((ib - b0) / tlb, 0, 1);
            return new Texel(d >= 0, (float)height, Hash01(i, j, Seed, 5), (float)(Columns ? tb : ta), (float)(Columns ? ta : tb));
        }
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var layout = Layout.Of(e.Parameters, e.Seed);
        Tile mask = e.Output("mask"), height = e.Output("height"), random = e.Output("random"), uv = e.Output("uv");
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var t = layout.At(x, y, e.Size);
                mask.SetGrey(x, y, t.Inside ? 1 : 0);
                height.SetGrey(x, y, t.Height);
                random.SetGrey(x, y, t.Random);
                uv.SetColour(x, y, t.U, t.V, 0);
            }
    }
}
