using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>transform</c>: input <c>in</c> (greyscale or colour) repeated, scaled, turned, mirrored and moved, applied in that
/// order, each about the image centre: <c>tile</c> [x, y] whole repeats 1 to 16; <c>scale</c> 0.25 to 4, the image's size
/// (2 doubles it, so a half shows; 0.5 halves it, so it shows twice across); <c>rotation</c> in degrees, clockwise as
/// seen (v runs down); <c>mirror</c> <c>none</c>, <c>u</c> (left for right), <c>v</c> (top for bottom) or <c>both</c>;
/// <c>offset</c> [u, v] in fractions of the image, wrapping (a positive u moves the image right). Read <c>bilinear</c>
/// (between the four texel centres about the point) or <c>nearest</c> (the texel the point falls in), wrapping. The
/// output is as colour as <c>in</c>.
/// <para>
/// Each output texel centre is mapped back through the inverse of the five steps to a point in the input, so a texel
/// reads only what lies under it. The input rectangle is the bounding box of the output rectangle's four corners mapped
/// back the same way, and 2 texels (the bilinear read's second texel and rounding). A turned or shrunk tile reads only
/// the few source tiles under it; a strong minify (tile 16 at scale 0.25) reads the whole input for every tile.
/// </para>
/// <para>
/// The result tiles when the rotation is a whole number of quarter turns and the scale is 1 or 1 / n (0.5, 1/3, 0.25:
/// n repeats). A whole-number scale above 1 zooms in and does not tile (the person's ruling, 2026-11-20), and
/// any other rotation or scale generally does not tile, which is the agent's choice and check's seam rule reports. A
/// quarter turn's sine and cosine are exact, so a quarter turn moves texels without blending them; any other angle's
/// are worked by <see cref="ShapeNode.SinCos"/>'s series, so the result is the same on every machine.
/// </para>
/// </summary>
public sealed class TransformNode : NodeType
{
    public const string TypeName = "transform";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Floats("offset", 2, 2, -1, 1, 0, 0),
        ParamSpec.IntPair("tile", 1, 16, 1, 1),
        ParamSpec.Enum("mirror", "none", "none", "u", "v", "both"),
        ParamSpec.Float("rotation", -360, 360, 0),
        ParamSpec.Float("scale", 0.25, 4, 1),
        ParamSpec.Enum("filter", "bilinear", "bilinear", "nearest"),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => inputs["in"];

    /// <summary>The inverse of the five steps, for one image size: an output point to the input point it reads, in texels.</summary>
    public readonly record struct Mapping(double Ox, double Oy, bool MirrorU, bool MirrorV, double Sin, double Cos, double Scale, int TileX, int TileY, int Size)
    {
        public static Mapping Of(NodeParameters p, int size)
        {
            var offset = p.Floats("offset");
            var mirror = p.Text("mirror");
            var (sin, cos) = SinCos(p.Float("rotation"));
            var (tx, ty) = p.IntPair("tile");
            return new(offset[0], offset[1], mirror is "u" or "both", mirror is "v" or "both", sin, cos, p.Float("scale"), tx, ty, size);
        }

        /// <summary>The input point read for output point (<paramref name="px"/>, <paramref name="py"/>), in texels (a texel's centre at x + 0.5), unwrapped.</summary>
        public (double X, double Y) Back(double px, double py)
        {
            double n = Size, c = n / 2;
            // offset
            double qx = px - Ox * n, qy = py - Oy * n;
            // mirror, about the centre
            if (MirrorU) qx = n - qx;
            if (MirrorV) qy = n - qy;
            // rotation: the forward turn is clockwise as seen, (dx, dy) -> (cos dx - sin dy, sin dx + cos dy); this is its inverse
            double dx = qx - c, dy = qy - c;
            double rx = Cos * dx + Sin * dy, ry = -Sin * dx + Cos * dy;
            // scale, about the centre
            qx = c + rx / Scale; qy = c + ry / Scale;
            // tile: the image at 1 / tile its size, repeated (the read wraps)
            return (qx * TileX, qy * TileY);
        }
    }

    /// <summary>The sine and cosine of <paramref name="degrees"/>: exact for a whole number of quarter turns, otherwise <see cref="ShapeNode.SinCos"/>.</summary>
    public static (double Sin, double Cos) SinCos(double degrees)
    {
        if (degrees % 90 == 0)
            return (((int)(degrees / 90) % 4 + 4) % 4) switch { 0 => (0, 1), 1 => (1, 0), 2 => (0, -1), _ => (-1, 0) };
        return ShapeNode.SinCos(degrees * Math.PI / 180);
    }

    public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size)
    {
        var m = Mapping.Of(parameters, size);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (cx, cy) in new[] { (output.X, output.Y), (output.Right, output.Y), (output.X, output.Bottom), (output.Right, output.Bottom) })
        {
            var (x, y) = m.Back(cx, cy);
            minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        // a box the image's size or more across is the whole axis (the evaluator clamps it); at most 16 repeats of a
        // quarter-scale image turned, a few hundred times the size, so the corners fit a float to well under a texel
        return TexelRect.Bounding((float)minX, (float)minY, (float)maxX, (float)maxY, margin: 2);
    }

    public static float Bilinear(float a, float b, float c, float d, float fx, float fy)
    {
        var top = a + (b - a) * fx;
        var bottom = c + (d - c) * fx;
        return top + (bottom - top) * fy;
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var m = Mapping.Of(e.Parameters, e.Size);
        var nearest = e.Parameters.Text("filter") == "nearest";
        var input = e.Input("in")!;
        var output = e.Output();
        var channels = output.Channels;
        var grey = input.Kind == PortKind.Grey;
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var row = output.Row(y);
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var (sx, sy) = m.Back(x + 0.5, y + 0.5);
                var i = (x - e.Rect.X) * channels;
                if (nearest)
                {
                    int nx = (int)Math.Floor(sx), ny = (int)Math.Floor(sy);
                    if (grey) row[i] = input.Grey(nx, ny);
                    else (row[i], row[i + 1], row[i + 2]) = input.Colour(nx, ny);
                    continue;
                }
                // between the four texel centres about the point: texel ix's centre is at ix + 0.5
                double fx = sx - 0.5, fy = sy - 0.5;
                int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
                float tx = (float)(fx - ix), ty = (float)(fy - iy);
                if (grey)
                {
                    row[i] = Bilinear(input.Grey(ix, iy), input.Grey(ix + 1, iy), input.Grey(ix, iy + 1), input.Grey(ix + 1, iy + 1), tx, ty);
                    continue;
                }
                var (r0, g0, b0) = input.Colour(ix, iy);
                var (r1, g1, b1) = input.Colour(ix + 1, iy);
                var (r2, g2, b2) = input.Colour(ix, iy + 1);
                var (r3, g3, b3) = input.Colour(ix + 1, iy + 1);
                row[i] = Bilinear(r0, r1, r2, r3, tx, ty);
                row[i + 1] = Bilinear(g0, g1, g2, g3, tx, ty);
                row[i + 2] = Bilinear(b0, b1, b2, b3, tx, ty);
            }
        }
    }
}
