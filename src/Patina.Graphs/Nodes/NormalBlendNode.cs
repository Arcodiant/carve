using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>normal_blend</c> (T-224): a detail normal laid over a base normal by reoriented normal mapping (Barré-Brisson and
/// Hill), the detail rotated into the base's frame, so strong detail on a tilted base (a bevel) keeps its tilt. Inputs
/// <c>base</c> and <c>detail</c>, colour tangent-space normals in -1..1 as <c>height_to_normal</c> and an <c>image</c>
/// of kind <c>normal</c> give them; parameter <c>strength</c> 0..1 moves the detail toward (0, 0, 1) and renormalises
/// it, so 0 gives the base. Colour out, renormalised to unit length.
/// </summary>
public sealed class NormalBlendNode : NodeType
{
    public const string TypeName = "normal_blend";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("base", PortKind.Colour), new("detail", PortKind.Colour)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Float("strength", 0, 1, 1)];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Colour;

    public static (float X, float Y, float Z) Blend((float X, float Y, float Z) b, (float X, float Y, float Z) d, float strength)
    {
        // The detail moved toward (0, 0, 1) by strength. r below is linear in u, so the renormalisation at the end
        // also renormalises this lerp.
        float ux = -d.X * strength, uy = -d.Y * strength, uz = 1f + (d.Z - 1f) * strength;
        // RNM needs a unit base: one short of it (a mix, a blur or a fractional shift of normals) turns the detail too far.
        b = Unit(b.X, b.Y, b.Z);
        // RNM: t = base + (0, 0, 1), u = detail with x and y negated, r = t * dot(t, u) / t.z - u.
        float tx = b.X, ty = b.Y, tz = b.Z + 1f;
        if (tz <= 1e-6f) return b; // a base facing straight into the surface has no frame
        var k = (tx * ux + ty * uy + tz * uz) / tz;
        return Unit(tx * k - ux, ty * k - uy, tz * k - uz);
    }

    private static (float X, float Y, float Z) Unit(float x, float y, float z)
    {
        var length = MathF.Sqrt(x * x + y * y + z * z);
        return length < 1e-12f ? (0f, 0f, 1f) : (x / length, y / length, z / length);
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var strength = e.Parameters.Float("strength");
        var b = e.Input("base")!;
        var d = e.Input("detail")!;
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var n = Blend(b.Colour(x, y), d.Colour(x, y), strength);
                output.SetColour(x, y, n.X, n.Y, n.Z);
            }
    }
}
