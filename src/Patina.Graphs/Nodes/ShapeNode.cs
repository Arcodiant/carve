using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>shape</c>: one shape in each cell of <c>count</c> [x, y] cells, greyscale, 1 inside. <c>kind</c> <c>circle</c>, <c>square</c>,
/// <c>ring</c> (inner radius half the outer), <c>diamond</c> or <c>polygon</c> with <c>sides</c> 3 to 12 (a vertex pointing up at
/// rotation 0); <c>size</c> 0..1, the shape's width as a part of the cell's shorter side (a circle's diameter, a square's side, a
/// polygon's circumscribed diameter), the shape centred in the cell; <c>softness</c> 0..1, the edge's smoothstep ramp running
/// inward over that part of the shape's depth (of a ring's thickness); <c>rotation</c> in degrees, applied inside the cell so the
/// image still tiles. Shapes are round in a cell that is not square. Sampled at texel centres; the rotation's sine and cosine are
/// worked by series of basic operations, so the result is the same on every machine.
/// </summary>
public sealed class ShapeNode : NodeType
{
    public const string TypeName = "shape";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.IntPair("count", 1, 256, 4, 4),
        ParamSpec.Enum("kind", "circle", "circle", "square", "ring", "diamond", "polygon"),
        ParamSpec.Int("sides", 3, 12, 6),
        ParamSpec.Float("size", 0, 1, 0.6),
        ParamSpec.Float("softness", 0, 1, 0),
        ParamSpec.Float("rotation", -360, 360, 0),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    /// <summary>Sine and cosine of an angle in radians by Taylor series after reduction to -pi..pi, in basic operations only.</summary>
    public static (double Sin, double Cos) SinCos(double a)
    {
        const double Pi = 3.14159265358979323846, TwoPi = 2 * Pi;
        a -= TwoPi * Math.Floor((a + Pi) / TwoPi);
        double a2 = a * a, s = 0, c = 0, ts = a, tc = 1;
        for (var k = 0; k < 14; k++)
        {
            s += ts; c += tc;
            ts *= -a2 / ((2 * k + 2) * (2 * k + 3));
            tc *= -a2 / ((2 * k + 1) * (2 * k + 2));
        }
        return (s, c);
    }

    private static double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }

    public override void Evaluate(NodeEvaluation e)
    {
        var (cx, cy) = e.Parameters.IntPair("count");
        var kind = e.Parameters.Text("kind");
        var sides = e.Parameters.Int("sides");
        double size = e.Parameters.Float("size"), softness = e.Parameters.Float("softness");
        var (sinR, cosR) = SinCos(-e.Parameters.Float("rotation") * Math.PI / 180);
        double cw = 1.0 / cx, ch = 1.0 / cy, m = Math.Min(cw, ch), radius = size / 2;
        var normals = new (double X, double Y)[sides];
        for (var k = 0; k < sides; k++)
        {
            var (s, c) = SinCos(-Math.PI / 2 + Math.PI / sides + k * 2 * Math.PI / sides);
            normals[k] = (c, s);
        }
        var apothem = radius * SinCos(Math.PI / sides).Cos;
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var row = output.Row(y);
            double v = (Tile.Wrap(y, e.Size) + 0.5) / e.Size * cy;
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                double u = (Tile.Wrap(x, e.Size) + 0.5) / e.Size * cx;
                double px = (u - Math.Floor(u) - 0.5) * cw / m, py = (v - Math.Floor(v) - 0.5) * ch / m;
                double qx = px * cosR - py * sinR, qy = px * sinR + py * cosR;
                double d, depth = radius;           // d: distance outside the shape's edge; depth: how far inside the edge runs
                switch (kind)
                {
                    case "circle": d = Math.Sqrt(qx * qx + qy * qy) - radius; break;
                    case "square": d = Math.Max(Math.Abs(qx), Math.Abs(qy)) - radius; break;
                    case "diamond": d = Math.Abs(qx) + Math.Abs(qy) - radius; depth = radius * 0.7071067811865476; break;
                    case "ring":
                        d = Math.Abs(Math.Sqrt(qx * qx + qy * qy) - 0.75 * radius) - 0.25 * radius; depth = 0.25 * radius; break;
                    default:
                        d = double.NegativeInfinity;
                        foreach (var (nx, ny) in normals) d = Math.Max(d, qx * nx + qy * ny);
                        d -= apothem; depth = apothem; break;
                }
                row[x - e.Rect.X] = (float)(d > 0 ? 0 : softness <= 0 ? 1 : Smooth(-d / (softness * depth)));
            }
        }
    }
}
