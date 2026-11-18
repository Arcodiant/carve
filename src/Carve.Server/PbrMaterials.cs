using System.Numerics;
using Carve.Core;
using Empyrean;
using CarveMaterial = Carve.Core.Material;
using Material = Empyrean.Material;

namespace Carve.Server;

/// <summary>
/// The engine material of a Carve material (M-7, T-78): the one function, <see cref="Build"/>, that makes the
/// <see cref="Material.Pbr"/> the engine's glTF loader makes of the same material exported (T-76), so <c>render</c> draws a
/// material face as the game would. An instance, made and used on the frame thread, holds each texture and material it made
/// until <see cref="Keep"/> is told the model no longer uses it (RV-47).
/// </summary>
/// <remarks>
/// Textures are uploaded as the loader uploads a model's images with <c>generateMipmaps: true</c> (ruling 1, 2026-11-16):
/// decoded to RGBA8, base colour and emissive as <see cref="TextureFormat.Rgba8Srgb"/>, the other maps as
/// <see cref="TextureFormat.Rgba8Unorm"/>, with a full mip chain and <see cref="Sampler"/>. They are owned textures, released
/// as <see cref="IGraphicsResourceManager"/> and <see cref="Material"/> require: each material is held by an
/// <see cref="IAssetHolder"/> of its own, so its material-table entry is reclaimed when the holder is disposed, and a texture
/// is destroyed only after every material holding it has gone.
/// </remarks>
internal sealed class PbrMaterials(IGraphicsResourceManager resources, ITextureLoader loader, IAssetResidency? residency = null)
{
    /// <summary>The sampler the engine gives a texture loaded with <c>generateMipmaps: true</c> and no sampler: trilinear, repeat, anisotropy 1.</summary>
    public static readonly SamplerDescription Sampler = TextureUpload.Sampler;

    // A PNG used both as colour and as data is two textures, keyed by its bytes' SHA-256 and the colour space.
    private readonly Dictionary<(string Hash, bool Srgb), Texture> _textures = [];
    // Keyed by every field, the textures' bytes included (a Carve material's equality); the holder is null without residency.
    // With the texture keys it samples, so a release need not hash the PNGs again.
    private readonly Dictionary<CarveMaterial, (Material Made, IAssetHolder? Holder, (string Hash, bool Srgb)[] Samples)> _materials = [];

    /// <summary>How many textures are held now.</summary>
    internal int TextureCount => _textures.Count;

    /// <summary>How many engine materials are held now.</summary>
    internal int MaterialCount => _materials.Count;

    /// <summary>The engine material of <paramref name="material"/>, made once per distinct material while it is kept.</summary>
    public Material Of(CarveMaterial material)
    {
        if (_materials.TryGetValue(material, out var held)) return held.Made;
        var samples = new List<(string Hash, bool Srgb)>();
        var made = Build(material, (t, srgb) =>
        {
            samples.Add((GlbExport.HashOf(t), srgb));
            return Texture(t, srgb);
        });
        IAssetHolder? holder = null;
        if (residency is not null)
        {
            holder = residency.CreateHolder($"carve material '{material.Name}'");
            holder.Hold(made);
        }
        _materials[material] = (made, holder, samples.ToArray());
        return made;
    }

    /// <summary>
    /// Releases every material not in <paramref name="used"/> (the model's materials and those the drawn mesh still holds),
    /// its holder first, then every texture no kept material samples. Call it once nothing draws the released materials.
    /// </summary>
    public void Keep(IEnumerable<CarveMaterial> used)
    {
        var keep = used.ToHashSet();
        foreach (var gone in _materials.Keys.Where(m => !keep.Contains(m)).ToArray())
        {
            _materials[gone].Holder?.Dispose();
            _materials.Remove(gone);
        }
        var sampled = _materials.Values.SelectMany(m => m.Samples).ToHashSet();
        foreach (var key in _textures.Keys.Where(k => !sampled.Contains(k)).ToArray())
        {
            resources.Destroy(_textures[key]);
            _textures.Remove(key);
        }
    }

    /// <summary>The texture of <paramref name="texture"/>'s PNG in the colour space asked, uploaded once.</summary>
    public Texture Texture(MaterialTexture texture, bool srgb)
    {
        var key = (GlbExport.HashOf(texture), srgb);
        if (_textures.TryGetValue(key, out var made)) return made;
        var image = loader.DecodeCpu(texture.Png.ToArray());
        made = TextureUpload.Create(resources, image.Pixels, image.Width, image.Height, srgb);
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
