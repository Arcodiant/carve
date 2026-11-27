using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>image</c> (T-240, format version 2): a PNG read into the graph. Parameter <c>path</c>, relative to the graph's file
/// (<see cref="TextureGraph.Folder"/>, as a <c>graph</c> node's is; no extension added), and <c>kind</c>: <c>colour</c>
/// (R, G, B / 255), <c>grey</c> (R / 255; carve's bakes write grey as R = G = B) or <c>normal</c> (each of R, G, B as
/// 2c / 255 - 1, renormalised to unit length: the form <c>height_to_normal</c> outputs). Alpha is not read.
/// <para>The image is laid over the graph's 0..1 square whatever its size: texel (x, y) of a size-n graph samples the
/// image bilinear, wrapping, at its centre ((x + 0.5) / n, (y + 0.5) / n), so an image of the graph's size is read texel
/// for texel exactly, the node tiles, and it is the same at every tile size; a non-square image is stretched. The file
/// is read and decoded by <see cref="GraphValidator"/>, so a missing or unreadable one is a fault on <c>path</c>.</para>
/// </summary>
public sealed class ImageNode : NodeType
{
    public const string TypeName = "image";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
        [ParamSpec.FilePath("path", "a .png file"), ParamSpec.Enum("kind", "colour", "colour", "grey", "normal")];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) =>
        parameters.Text("kind") == "grey" ? PortKind.Grey : PortKind.Colour;

    /// <summary>
    /// Texel (<paramref name="x"/>, <paramref name="y"/>) of a <paramref name="size"/> graph read from <paramref name="image"/>
    /// as <paramref name="kind"/>: R, G, B (grey: R three times; normal: the unit vector).
    /// </summary>
    public static (float R, float G, float B) Texel(PngImage image, string kind, int x, int y, int size)
    {
        var (x0, fx) = Axis(x, size, image.Width);
        var (y0, fy) = Axis(y, size, image.Height);
        int x1 = Tile.Wrap(x0 + 1, image.Width), y1 = Tile.Wrap(y0 + 1, image.Height);
        x0 = Tile.Wrap(x0, image.Width);
        y0 = Tile.Wrap(y0, image.Height);
        var rgba = image.Rgba;
        float At(int c)
        {
            float a = rgba[(y0 * image.Width + x0) * 4 + c], b = rgba[(y0 * image.Width + x1) * 4 + c];
            float d = rgba[(y1 * image.Width + x0) * 4 + c], e = rgba[(y1 * image.Width + x1) * 4 + c];
            var top = a + (b - a) * fx;
            var bottom = d + (e - d) * fx;
            return top + (bottom - top) * fy;
        }
        if (kind == "grey") { var v = At(0) / 255f; return (v, v, v); }
        if (kind == "colour") return (At(0) / 255f, At(1) / 255f, At(2) / 255f);
        float nx = At(0) * (2f / 255f) - 1, ny = At(1) * (2f / 255f) - 1, nz = At(2) * (2f / 255f) - 1;
        var length = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        // A texel blended between opposite normals can come out of no length; it is read as flat.
        return length < 1e-6f ? (0, 0, 1) : (nx / length, ny / length, nz / length);
    }

    /// <summary>
    /// Where texel <paramref name="t"/>'s centre falls on an image <paramref name="n"/> texels across: the texel at or
    /// before it (not yet wrapped) and the fraction past it, worked in integers so that n = size gives (t, 0) exactly.
    /// </summary>
    private static (int Index, float Fraction) Axis(int t, int size, int n)
    {
        // (t + 0.5) / size * n - 0.5 = ((2t + 1) n - size) / 2 size
        var numerator = (2L * t + 1) * n - size;
        var denominator = 2L * size;
        var index = numerator / denominator;
        if (numerator % denominator < 0) index--;
        return ((int)index, (float)(numerator - index * denominator) / denominator);
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var image = e.Parameters.Image ?? throw new InvalidOperationException($"image '{e.NodeId}' has no image: it is read at validation");
        var kind = e.Parameters.Text("kind");
        var output = e.Output();
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            for (var x = e.Rect.X; x < e.Rect.Right; x++)
            {
                var (r, g, b) = Texel(image, kind, x, y, e.Size);
                if (kind == "grey") output.SetGrey(x, y, r);
                else output.SetColour(x, y, r, g, b);
            }
    }
}
