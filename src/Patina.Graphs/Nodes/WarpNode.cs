using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>warp</c>: input <c>in</c> (greyscale or colour) read at texels displaced by <c>by</c>, bilinear and wrapping
/// (so it tiles). <c>by</c> colour: R and G are the u and v offsets about 0.5, a channel at 0 or 1 displacing by
/// <c>amount</c> of the image (the read lies at the output's texel plus the offset: R above 0.5 reads further
/// right, G above 0.5 further down). <c>by</c> greyscale: the offset along <c>angle</c> degrees (0 along u, 90 down v),
/// 0.5 displacing nothing. <c>amount</c> 0..0.25 of the image width. The output is as colour as <c>in</c>.
/// <para>The rectangle of <c>in</c> depends on <c>by</c> (T-153): it is the bounding box of the four texels each output
/// texel's bilinear read touches, worked from <c>by</c>'s actual offsets, not widened by the whole amount, and in
/// unwrapped coordinates, so a tile whose reads cross the image's edge asks for a box running past it (the read wraps).
/// <see cref="InputRect"/> for <c>in</c> stays the bound on that box: the output widened by the largest displacement,
/// <c>amount</c> times the size rounded up, and 2.</para>
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

    public override IReadOnlyList<string> DependsOn(string port, NodeParameters parameters) => port == "in" ? ["by"] : [];

    /// <summary>
    /// For <c>in</c>: the texels the tile's bilinear reads touch, columns <c>min ix</c> to <c>max ix + 1</c> and rows
    /// <c>min iy</c> to <c>max iy + 1</c>, worked by <see cref="Source"/> as <see cref="Evaluate"/> works them.
    /// </summary>
    public override IReadOnlyList<TexelRect> InputRects(string port, InputRequest request)
    {
        if (port != "in") return base.InputRects(port, request);
        var amount = request.Parameters.Float("amount");
        var (cos, sin) = Direction(request.Parameters);
        var by = request.Input("by")!;
        var byColour = by.Kind == PortKind.Colour;
        var o = request.Output;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        var step = byColour ? 3 : 1;
        for (var y = o.Y; y < o.Bottom; y++)
        {
            // by is read pointwise, so a row of the tile lies in one of its tiles; a grey by's R and G are its value
            var row = by.Row(y, o.X, o.Width);
            for (var x = o.X; x < o.Right; x++)
            {
                var j = (x - o.X) * step;
                var (ix, iy, _, _) = Source(x, y, row[j], byColour ? row[j + 1] : row[j], byColour, amount, cos, sin, request.Size);
                if (ix < x0) x0 = ix;
                if (ix > x1) x1 = ix;
                if (iy < y0) y0 = iy;
                if (iy > y1) y1 = iy;
            }
        }
        return [new TexelRect(x0, y0, x1 - x0 + 2, y1 - y0 + 2)];
    }

    private static (float Cos, float Sin) Direction(NodeParameters parameters)
    {
        var radians = parameters.Float("angle") * Math.PI / 180.0;
        return ((float)Math.Cos(radians), (float)Math.Sin(radians));
    }

    /// <summary>
    /// Where texel (x, y) reads from, given <c>by</c>'s value there: the top-left texel of its bilinear read and the
    /// weights toward the next column and row. One function for <see cref="Evaluate"/> and <see cref="InputRects"/>, so
    /// the rectangle asked for is the texels read, to the last bit.
    /// </summary>
    public static (int Ix, int Iy, float Tx, float Ty) Source(int x, int y, float r, float g, bool colour, float amount, float cos, float sin, int size)
    {
        var (dx, dy) = Offset(r, g, colour, amount, cos, sin, size);
        // the source position, in texels from the first texel centre: the texel centre is at x + 0.5 in both
        float fx = x + dx, fy = y + dy;
        int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
        return (ix, iy, fx - ix, fy - iy);
    }

    /// <summary>The displacement in texels, (u, v), from <c>by</c>'s value at a texel: its R and G for a colour, or the grey along (cos, sin).</summary>
    public static (float Dx, float Dy) Offset(float r, float g, bool colour, float amount, float cos, float sin, int size)
    {
        var k = 2 * amount * size;
        return colour ? ((r - 0.5f) * k, (g - 0.5f) * k) : ((r - 0.5f) * k * cos, (r - 0.5f) * k * sin);
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var amount = e.Parameters.Float("amount");
        var (cos, sin) = Direction(e.Parameters);
        var input = e.Input("in")!;
        var by = e.Input("by")!;
        var byColour = by.Kind == PortKind.Colour;
        var step = byColour ? 3 : 1;
        var output = e.Output();
        var channels = output.Channels;
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var row = output.Row(y);
            var offsets = by.Row(y, e.Rect.X, e.Rect.Width);
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var j = (x - e.Rect.X) * step;
                var (ix, iy, tx, ty) = Source(x, y, offsets[j], byColour ? offsets[j + 1] : offsets[j], byColour, amount, cos, sin, e.Size);
                input.Bilinear(ix, iy, tx, ty, row.Slice((x - e.Rect.X) * channels, channels));
            }
        }
    }
}
