using System.Numerics;
using Carve.Core;
using Empyrean;
using CarveMaterial = Carve.Core.Material;
using Material = Empyrean.Material;

namespace Carve.Server;

/// <summary>
/// The engine material of a Carve material (M-7, T-78): the one function, <see cref="Build"/>, that makes the
/// <see cref="Material.Pbr"/> the engine's glTF loader makes of the same material exported (T-76), so <c>render</c> draws a
/// material face as the game would. An instance, made and used on the frame thread, holds every texture and material it made
/// for the process, as the checker texture is held.
/// </summary>
/// <remarks>
/// Textures are uploaded as the loader uploads a model's images with <c>generateMipmaps: true</c> (ruling 1, 2026-11-16):
/// decoded to RGBA8, base colour and emissive as <see cref="TextureFormat.Rgba8Srgb"/>, the other maps as
/// <see cref="TextureFormat.Rgba8Unorm"/>, with a full mip chain and <see cref="Sampler"/>. They are owned textures that Carve
/// could destroy; it never does. Were they ever released, the materials holding them would go first (the engine's
/// <see cref="Material"/> lifetime rule).
/// </remarks>
internal sealed class PbrMaterials(IGraphicsResourceManager resources, ITextureLoader loader)
{
    /// <summary>The sampler the engine gives a texture loaded with <c>generateMipmaps: true</c> and no sampler: trilinear, repeat, anisotropy 1.</summary>
    public static readonly SamplerDescription Sampler = new(TextureFilter.Linear, TextureFilter.Linear, TextureFilter.Linear,
        TextureAddressMode.Repeat, TextureAddressMode.Repeat, TextureAddressMode.Repeat, 1f, SamplerDescription.NoLodClamp, 0f, 0f);

    // A PNG used both as colour and as data is two textures, keyed by its bytes' SHA-256 and the colour space.
    private readonly Dictionary<(string Hash, bool Srgb), Texture> _textures = [];
    // Keyed by every field, the textures' bytes included (a Carve material's equality).
    private readonly Dictionary<CarveMaterial, Material> _materials = [];

    /// <summary>The engine material of <paramref name="material"/>, made once per distinct material.</summary>
    public Material Of(CarveMaterial material)
    {
        if (!_materials.TryGetValue(material, out var made))
            _materials[material] = made = Build(material, Texture);
        return made;
    }

    /// <summary>The texture of <paramref name="texture"/>'s PNG in the colour space asked, uploaded once.</summary>
    public Texture Texture(MaterialTexture texture, bool srgb)
    {
        var key = (GlbExport.HashOf(texture), srgb);
        if (_textures.TryGetValue(key, out var made)) return made;
        var image = loader.DecodeCpu(texture.Png.ToArray());
        made = resources.CreateTexture(image.Pixels, image.Width, image.Height,
            srgb ? TextureFormat.Rgba8Srgb : TextureFormat.Rgba8Unorm, Sampler, generateMipmaps: true);
        _textures[key] = made;
        return made;
    }

    /// <summary>
    /// The <see cref="Material.Pbr"/> of <paramref name="m"/>: base colour <see cref="FaceStyle.ToLinear"/> with alpha 1, as
    /// export writes it; metallic and roughness; the five textures from <paramref name="texture"/> (base colour and emissive
    /// sRGB, the rest linear); the emissive colour linear; occlusion strength only with an occlusion map, else 1, since glTF
    /// carries the strength on the map alone (ruling 4); and double-sidedness.
    /// </summary>
    internal static Material Build(CarveMaterial m, Func<MaterialTexture, bool, Texture> texture)
    {
        Texture? Of(MaterialTexture? t, bool srgb) => t is null ? null : texture(t, srgb);
        var emissive = FaceStyle.ToLinear(m.Emissive);
        return Material.Pbr(FaceStyle.ToLinear(m.BaseColour), m.Metallic, m.Roughness,
            Of(m.BaseColourTexture, true)!, Of(m.MetallicRoughnessTexture, false)!, Of(m.NormalTexture, false)!,
            Of(m.EmissiveTexture, true)!, Of(m.OcclusionTexture, false)!,
            new Vector3(emissive.X, emissive.Y, emissive.Z), m.OcclusionTexture is null ? 1f : m.Occlusion, m.DoubleSided);
    }
}
