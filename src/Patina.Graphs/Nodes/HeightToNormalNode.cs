using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>height_to_normal</c>: input <c>height</c> (greyscale); parameter <c>strength</c>, the slope per image width at height 1
/// (a height rising 1 across the whole image width has slope <c>strength</c>). Central differences over radius 1 with
/// wrap-around, glTF tangent space (red right, green up with image rows running down, blue out of the surface), unit
/// length. Colour out holding the vector's components in -1..1; the output packing encodes them.
/// </summary>
public sealed class HeightToNormalNode : NodeType
{
    public const string TypeName = "height_to_normal";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("height", PortKind.Grey)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } = [ParamSpec.Float("strength", 0, 1000, 1)];

    public override int Radius(NodeParameters parameters, int size) => 1;

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Colour;

    public static (float X, float Y, float Z) Normal(float left, float right, float up, float down, int size, float strength)
    {
        // Slope per image width: a difference per texel times the texels across. Height rising to the right tilts the
        // normal left (-x); rising down the image (increasing row) tilts it up the image (+y, green up).
        var sx = (right - left) * 0.5f * size * strength;
        var sy = (down - up) * 0.5f * size * strength;
        var nx = -sx; var ny = sy;
        var inv = 1f / MathF.Sqrt(nx * nx + ny * ny + 1f);
        return (nx * inv, ny * inv, inv);
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var strength = e.Parameters.Float("strength");
        var h = e.Input("height")!;
        var output = e.Output();
        for (var y = e.Y0; y < e.Y0 + e.Rows; y++)
            for (var x = 0; x < e.Size; x++)
            {
                var n = Normal(h.Grey(x - 1, y), h.Grey(x + 1, y), h.Grey(x, y - 1), h.Grey(x, y + 1), e.Size, strength);
                output.SetColour(x, y, n.X, n.Y, n.Z);
            }
    }
}
