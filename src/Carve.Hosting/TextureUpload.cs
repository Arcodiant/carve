using Empyrean;

namespace Carve.Hosting;

/// <summary>
/// How a texture reaches the GPU for a material (T-78's ruling 1, moved from carve's <c>PbrMaterials</c> by T-111):
/// tightly packed RGBA8 pixels, sRGB for a colour texture and linear for a data one, sampled linearly and repeating,
/// with mipmaps generated. A server that has pixels already (patina) need not encode a PNG to draw them.
/// </summary>
public static class TextureUpload
{
    /// <summary>Linear filtering and mips, repeat on every axis, no anisotropy, no LOD clamp.</summary>
    public static readonly SamplerDescription Sampler = new(TextureFilter.Linear, TextureFilter.Linear, TextureFilter.Linear,
        TextureAddressMode.Repeat, TextureAddressMode.Repeat, TextureAddressMode.Repeat, 1f, SamplerDescription.NoLodClamp, 0f, 0f);

    /// <summary>The texture of <paramref name="rgba8"/>, <c>width * height * 4</c> bytes, rows top first.</summary>
    /// <param name="srgb">True for colour (base colour, emissive), false for data (metallic-roughness, normal, occlusion).</param>
    public static Texture Create(IGraphicsResourceManager resources, ReadOnlySpan<byte> rgba8, int width, int height, bool srgb) =>
        resources.CreateTexture(rgba8, width, height, srgb ? TextureFormat.Rgba8Srgb : TextureFormat.Rgba8Unorm, Sampler,
            generateMipmaps: true);
}
