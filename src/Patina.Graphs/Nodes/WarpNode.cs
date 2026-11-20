using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>warp</c>: input <c>in</c> (greyscale or colour) read at texels displaced by <c>by</c>, bilinear and wrapping
/// (so it tiles). <c>by</c> colour: R and G are the u and v offsets about 0.5, a channel at 0 or 1 displacing by
/// <c>amount</c> of the image (the read lies at the output's texel plus the offset: R above 0.5 reads further
/// right, G above 0.5 further down). <c>by</c> greyscale: the offset along <c>angle</c> degrees (0 along u, 90 down v),
/// 0.5 displacing nothing. <c>amount</c> 0..0.25 of the image width. The output is as colour as <c>in</c>. The input
/// rectangle is the output's widened by the largest displacement, <c>amount</c> times the size rounded up, and 2.
/// </summary>
public sealed class WarpNode : NodeType
{
    public const string TypeName = "warp";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any), new("by", PortKind.Any)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Float("amount", 0, 0.25, 0.05),
        ParamSpec.Float("angle", 0, 360, 0),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => inputs["in"];

    /// <summary>The widest displacement in texels, rounded up, and the margin of the bilinear read.</summary>
    public static int Reach(float amount, int size) => (int)MathF.Ceiling(amount * size) + 2;

    public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) =>
        port == "in" ? output.Inflate(Reach(parameters.Float("amount"), size)) : output;

    /// <summary>The displacement in texels, (u, v), from <c>by</c>'s value at a texel: its R and G for a colour, or the grey along (cos, sin).</summary>
    public static (float Dx, float Dy) Offset(float r, float g, bool colour, float amount, float cos, float sin, int size)
    {
        var k = 2 * amount * size;
        return colour ? ((r - 0.5f) * k, (g - 0.5f) * k) : ((r - 0.5f) * k * cos, (r - 0.5f) * k * sin);
    }

    public static float Bilinear(float a, float b, float c, float d, float fx, float fy)
    {
        var top = a + (b - a) * fx;
        var bottom = c + (d - c) * fx;
        return top + (bottom - top) * fy;
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var amount = e.Parameters.Float("amount");
        var radians = e.Parameters.Float("angle") * Math.PI / 180.0;
        float cos = (float)Math.Cos(radians), sin = (float)Math.Sin(radians);
        var input = e.Input("in")!;
        var by = e.Input("by")!;
        var byColour = by.Kind == PortKind.Colour;
        var output = e.Output();
        var channels = output.Channels;
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var row = output.Row(y);
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var (r, g, _) = by.Colour(x, y);
                var (dx, dy) = Offset(r, g, byColour, amount, cos, sin, e.Size);
                // the source position, in texels from the first texel centre: the texel centre is at x + 0.5 in both
                float fx = x + dx, fy = y + dy;
                int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
                float tx = fx - ix, ty = fy - iy;
                var (r0, g0, b0) = input.Colour(ix, iy);
                var (r1, g1, b1) = input.Colour(ix + 1, iy);
                var (r2, g2, b2) = input.Colour(ix, iy + 1);
                var (r3, g3, b3) = input.Colour(ix + 1, iy + 1);
                var i = (x - e.Rect.X) * channels;
                row[i] = Bilinear(r0, r1, r2, r3, tx, ty);
                if (channels == 3)
                {
                    row[i + 1] = Bilinear(g0, g1, g2, g3, tx, ty);
                    row[i + 2] = Bilinear(b0, b1, b2, b3, tx, ty);
                }
            }
        }
    }
}
