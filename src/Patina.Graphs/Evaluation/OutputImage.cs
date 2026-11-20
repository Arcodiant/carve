using Patina.Graphs.Imaging;

namespace Patina.Graphs.Evaluation;

/// <summary>One role's image at one size: its RGBA8 bytes, packed by <see cref="OutputPacking"/>, and its PNG bytes.</summary>
public sealed record OutputImage(OutputRole Role, int Size, byte[] Rgba, byte[] Png)
{
    /// <summary>Evaluates the role's output tile by tile into its RGBA8 image, then encodes the PNG.</summary>
    public static OutputImage Make(GraphEvaluator evaluator, OutputRole role, int size, int? tileSize = null, long? cacheBytes = null)
    {
        var rgba = Rgba8(evaluator, role, size, tileSize, cacheBytes);
        return new(role, size, rgba, Imaging.Png.Encode(rgba, size, size));
    }

    /// <summary>The role's RGBA8 image alone, with no PNG.</summary>
    public static byte[] Rgba8(GraphEvaluator evaluator, OutputRole role, int size, int? tileSize = null, long? cacheBytes = null)
    {
        var rgba = new byte[(long)size * size * 4];
        evaluator.Evaluate(role, size, tile => OutputPacking.PackTile(role, tile, rgba, size), tileSize, cacheBytes);
        return rgba;
    }
}
