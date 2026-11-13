using Carve.Core;
using Empyrean;

namespace Carve.Server;

/// <summary>
/// The material <c>render</c> draws the model's own colours with: the engine's PBR material, with the parameters its loader
/// realises from the material <see cref="GlbExport"/> writes (metallic 0, roughness 1, no maps, no emission, single-sided), so
/// the preview shades as the game will (T-82, the person's ruling of 2026-11-12, "render goes PBR").
/// </summary>
internal static class SurfaceMaterial
{
    /// <summary>The material of one colour, an sRGB hex, or the default grey for null: the base colour is its linear form, as exported.</summary>
    public static Material Of(string? colour) => Material.Pbr(
        colour is null ? GlbExport.BaseColor : FaceStyle.ToLinear(colour),
        GlbExport.Metallic,
        GlbExport.Roughness,
        doubleSided: false);
}
