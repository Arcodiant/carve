using Patina.Graphs.Imaging;

namespace Patina.Graphs.Evaluation;

/// <summary>One output's file image at one size: its RGBA8 bytes, packed by <see cref="OutputPacking"/>, and its PNG bytes. <see cref="Output"/> is a role's name or a named output's.</summary>
public sealed record OutputImage(string Output, int Size, byte[] Rgba, byte[] Png)
{
    /// <summary>Evaluates the role's file image (<see cref="FileRgba8(GraphEvaluator, string, int, int?, long?)"/>) tile by tile, then encodes the PNG.</summary>
    public static OutputImage Make(GraphEvaluator evaluator, OutputRole role, int size, int? tileSize = null, long? cacheBytes = null) =>
        Make(evaluator, OutputRoles.Name(role), size, tileSize, cacheBytes);

    /// <summary>Evaluates the output's file image tile by tile, then encodes the PNG.</summary>
    public static OutputImage Make(GraphEvaluator evaluator, string output, int size, int? tileSize = null, long? cacheBytes = null)
    {
        var rgba = FileRgba8(evaluator, output, size, tileSize, cacheBytes);
        return new(output, size, rgba, Imaging.Png.Encode(rgba, size, size));
    }

    /// <summary>The role's RGBA8 image alone, with no PNG: <c>roughness</c> with B 0 and <c>metallic</c> grey, as a swatch sheet's column shows them.</summary>
    public static byte[] Rgba8(GraphEvaluator evaluator, OutputRole role, int size, int? tileSize = null, long? cacheBytes = null) =>
        Rgba8(evaluator, OutputRoles.Name(role), size, tileSize, cacheBytes);

    /// <summary>The output's RGBA8 image alone, with no PNG: <c>roughness</c> with B 0 and <c>metallic</c> grey, as a swatch sheet's column shows them.</summary>
    public static byte[] Rgba8(GraphEvaluator evaluator, string output, int size, int? tileSize = null, long? cacheBytes = null)
    {
        var rgba = new byte[(long)size * size * 4];
        var packedAs = OutputRoles.PackedAs(output);
        evaluator.Evaluate(output, size, tile => OutputPacking.PackTile(packedAs, tile, rgba, size), tileSize, cacheBytes);
        return rgba;
    }

    /// <summary>
    /// The RGBA8 image of the output's file, <see cref="OutputRoles.FileOf"/>: as <see cref="Rgba8(GraphEvaluator, string, int, int?, long?)"/>, but
    /// <c>roughness</c> carries the graph's <c>metallic</c> in B (0 without one), the glTF metallic-roughness image. <c>metallic</c> has no file of its own.
    /// </summary>
    public static byte[] FileRgba8(GraphEvaluator evaluator, string output, int size, int? tileSize = null, long? cacheBytes = null)
    {
        if (!OutputRoles.HasOwnFile(output))
            throw new ArgumentException("metallic has no file of its own: it is packed into roughness.png's blue channel", nameof(output));
        var rgba = Rgba8(evaluator, output, size, tileSize, cacheBytes);
        var metallic = OutputRoles.Name(OutputRole.Metallic);
        if (output == OutputRoles.Name(OutputRole.Roughness) && evaluator.Has(metallic))
            evaluator.Evaluate(metallic, size, tile => OutputPacking.PackMetallicTile(tile, rgba, size), tileSize, cacheBytes);
        return rgba;
    }
}
