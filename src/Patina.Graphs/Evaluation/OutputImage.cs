using Patina.Graphs.Imaging;

namespace Patina.Graphs.Evaluation;

/// <summary>One role's image at one size: its RGBA8 bytes, packed by <see cref="OutputPacking"/>, and its PNG bytes.</summary>
public sealed record OutputImage(OutputRole Role, int Size, byte[] Rgba, byte[] Png)
{
    /// <summary>Evaluates the role's output band by band into its RGBA8 image, then encodes the PNG.</summary>
    public static OutputImage Make(GraphEvaluator evaluator, OutputRole role, int size, int? bandRows = null)
    {
        var rgba = Rgba8(evaluator, role, size, bandRows);
        return new(role, size, rgba, Imaging.Png.Encode(rgba, size, size));
    }

    /// <summary>The role's RGBA8 image alone, with no PNG.</summary>
    public static byte[] Rgba8(GraphEvaluator evaluator, OutputRole role, int size, int? bandRows = null)
    {
        var rgba = new byte[(long)size * size * 4];
        evaluator.Evaluate(role, size, (band, y0, rows) =>
        {
            for (var y = y0; y < y0 + rows; y++)
                OutputPacking.PackRow(role, band, y, rgba.AsSpan(y * size * 4, size * 4));
        }, bandRows);
        return rgba;
    }
}
