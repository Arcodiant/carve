using System.Globalization;
using System.Numerics;
using Carve.Core;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using CarveMaterial = Carve.Core.Material;
using System.Security.Cryptography;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;

namespace Carve.Server;

/// <summary>
/// The mesh as a self-contained binary glTF: one scene, one node with an identity transform and one mesh, both named
/// for the file; one indexed triangle primitive of <c>POSITION</c> and <c>NORMAL</c> alone, from
/// <see cref="Shading"/>, with every position the mesh's own float (no scale, axis change or recentring) and every
/// normal its corner's: its face's on a flat mesh, so every edge is hard. glTF winds counter-clockwise from outside, as Carve does, so the
/// triangles are written as they are. One matte material. The same mesh always writes the same bytes.
/// </summary>
public static class GlbExport
{
    public const string MaterialName = "carve";

    /// <summary>The colour <c>render</c> draws the surface in: <c>RenderSystem</c> takes its surface colour from here, so the game sees what Carve shows.</summary>
    public static readonly Vector4 BaseColor = new(0.72f, 0.72f, 0.70f, 1f);

    /// <summary>Set explicitly: glTF's default material is fully metallic.</summary>
    public const float Metallic = 0f;

    public const float Roughness = 1f;

    /// <summary>The materials a mesh exports as, in the order their primitives are written: the grey one first when any face has no colour.</summary>
    public static string[] MaterialNames(PolygonMesh mesh) => MaterialNames(mesh, []);

    /// <summary>As above for a mesh whose faces may use <paramref name="materials"/>: the colours' primitives, then each material in order of first use by face.</summary>
    public static string[] MaterialNames(PolygonMesh mesh, IReadOnlyList<CarveMaterial> materials) =>
        [.. Primitives(mesh, Shading.Triangles(mesh), materials).Select(m => m.Name)];

    /// <summary>One primitive of the static file: its material's name, its colour (null: the grey one), and its Carve material when it has one.</summary>
    internal sealed record Primitive(string Name, string? Colour, CarveMaterial? Material);

    /// <summary>The key a face's primitive is found by: a material's name, a colour's hex, or "" for the grey one.</summary>
    internal static string KeyOf(PolygonMesh? mesh, int face) =>
        mesh?.Style?.MaterialOf(face) is { } material ? "m" + material : "c" + (ColourOf(mesh, face) ?? "");

    private static List<Primitive> Primitives(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles, IReadOnlyList<CarveMaterial> materials)
    {
        var colours = triangles.Where(t => mesh?.Style?.MaterialOf(t.Face) is null).Select(t => ColourOf(mesh, t.Face)).Distinct().ToList();
        var result = colours.OrderBy(c => c is null ? 0 : 1)
            .Select(c => new Primitive(c is null ? MaterialName : $"{MaterialName} {c}", c, null)).ToList();
        foreach (var name in triangles.Select(t => mesh?.Style?.MaterialOf(t.Face)).Where(n => n is not null).Distinct())
            result.Add(new Primitive(name!, null, materials.FirstOrDefault(m => m.Name == name)
                ?? throw new ArgumentException($"a face uses the material '{name}', which is not defined", nameof(materials))));
        return result;
    }

    /// <summary>The Carve materials the faces of <paramref name="meshes"/> use, in model order, for the texture rules.</summary>
    internal static IReadOnlyList<CarveMaterial> UsedBy(IEnumerable<PolygonMesh> meshes, IReadOnlyList<CarveMaterial> materials)
    {
        var used = meshes.SelectMany(Model.MaterialsUsedBy).ToHashSet();
        return [.. materials.Where(m => used.Contains(m.Name))];
    }

    /// <summary>True when any material in <paramref name="used"/> has a texture, so <c>TEXCOORD_0</c> is written.</summary>
    internal static bool Textured(IEnumerable<CarveMaterial> used) => used.Any(m => m.HasTexture);

    /// <summary>Each distinct set of PNG bytes once, however many materials and channels use it.</summary>
    internal sealed class Images
    {
        private readonly Dictionary<string, ImageBuilder> _byHash = [];

        public ImageBuilder Of(MaterialTexture texture)
        {
            var key = HashOf(texture);
            if (!_byHash.TryGetValue(key, out var image))
                _byHash[key] = image = ImageBuilder.From(new MemoryImage(texture.Png.ToArray()));
            return image;
        }
    }

    /// <summary>The SHA-256 of a texture's PNG bytes, in hex: the key export and render share a PNG by.</summary>
    internal static string HashOf(MaterialTexture texture) => Convert.ToHexString(SHA256.HashData(texture.Png.AsSpan()));

    /// <summary>
    /// The glTF metallic-roughness material of a Carve material: base colour factor (linear, alpha 1), metallic and roughness
    /// factors always, emissive factor (linear) only when not black, <c>doubleSided</c>, and each texture embedded as its
    /// PNG bytes; the occlusion strength goes with the occlusion texture only. No sampler and no alpha fields are written.
    /// </summary>
    internal static MaterialBuilder Material(CarveMaterial m, Images images)
    {
        var builder = new MaterialBuilder(m.Name)
            .WithMetallicRoughnessShader()
            .WithBaseColor(FaceStyle.ToLinear(m.BaseColour))
            .WithMetallicRoughness((float)m.Metallic, (float)m.Roughness)
            .WithDoubleSide(m.DoubleSided);
        var emissive = FaceStyle.ToLinear(m.Emissive);
        if (emissive.X != 0f || emissive.Y != 0f || emissive.Z != 0f) builder.WithEmissive(new Vector3(emissive.X, emissive.Y, emissive.Z));
        if (m.BaseColourTexture is { } baseColour) builder.WithChannelImage(KnownChannel.BaseColor, images.Of(baseColour));
        if (m.MetallicRoughnessTexture is { } metallicRoughness) builder.WithChannelImage(KnownChannel.MetallicRoughness, images.Of(metallicRoughness));
        if (m.NormalTexture is { } normal) builder.WithChannelImage(KnownChannel.Normal, images.Of(normal));
        if (m.EmissiveTexture is { } emissiveTexture) builder.WithChannelImage(KnownChannel.Emissive, images.Of(emissiveTexture));
        if (m.OcclusionTexture is { } occlusion)
        {
            builder.WithChannelImage(KnownChannel.Occlusion, images.Of(occlusion));
            builder.UseChannel(KnownChannel.Occlusion).Parameters[KnownProperty.OcclusionStrength] = m.Occlusion;
        }
        return builder;
    }

    /// <summary>A face's colour as an upper-case hex, or null for an uncoloured face or no mesh.</summary>
    internal static string? ColourOf(PolygonMesh? mesh, int face) => mesh?.Style?.ColourOf(face)?.ToUpperInvariant();

    /// <summary>The opaque matte material of one colour (D-6): metallic 0, roughness 1, single-sided; no colour is the default grey.</summary>
    internal static MaterialBuilder Material(string name, string? colour) => new MaterialBuilder(name)
        .WithMetallicRoughnessShader()
        .WithBaseColor(colour is null ? BaseColor : FaceStyle.ToLinear(colour))
        .WithMetallicRoughness(Metallic, Roughness)
        .WithDoubleSide(false);

    /// <summary>One opaque matte material per colour, metallic 0 and roughness 1, its base colour the linear form of the sRGB hex (D-6).</summary>
    public static byte[] Write(IReadOnlyList<ShadedTriangle> triangles, string name) => Write(null, triangles, name);

    /// <summary>True when any live face of <paramref name="mesh"/> has UVs: only then is <c>TEXCOORD_0</c> written.</summary>
    internal static bool HasUvs(PolygonMesh? mesh) => mesh?.Uvs is { } uvs && mesh.LiveFaces.Any(uvs.IsMapped);

    /// <summary>The UV of corner <paramref name="vertex"/> of <paramref name="face"/>: its own, or (0, 0) for an unmapped face.</summary>
    internal static Vector2 UvAt(PolygonMesh mesh, int face, int vertex) =>
        mesh.Uvs?.Of(face) is { } uv ? uv[mesh.Faces[face].CornerOf(vertex)] : Vector2.Zero;

    /// <summary>
    /// The warning for each piece with faces that have no UVs while the export carries UVs, or none when no piece has any.
    /// The text is <c>&lt;mesh&gt;: N faces have no UVs; their corners are written as (0, 0)</c>, then the first face ids.
    /// A face whose material has a texture is left to <see cref="MaterialUvWarnings"/>, which names it with its material.
    /// </summary>
    public static IReadOnlyList<string> UvWarnings(Model model)
    {
        if (!model.Meshes.Any(m => HasUvs(m.Mesh))) return [];
        var textured = TexturedNames(model);
        var warnings = new List<string>();
        foreach (var (name, mesh) in model.Meshes)
        {
            var bare = mesh.LiveFaces.Where(f => mesh.Uvs?.IsMapped(f) != true && !(mesh.Style?.MaterialOf(f) is { } m && textured.Contains(m))).ToList();
            if (bare.Count == 0) continue;
            var shown = string.Join(", ", bare.Take(UvWarningIds));
            warnings.Add(bare.Count == 1
                ? $"{name}: 1 face has no UVs; its corners are written as (0, 0) (face {shown})"
                : $"{name}: {bare.Count.ToString(CultureInfo.InvariantCulture)} faces have no UVs; their corners are written as (0, 0) (faces {shown}"
                    + (bare.Count > UvWarningIds ? $" and {(bare.Count - UvWarningIds).ToString(CultureInfo.InvariantCulture)} more)" : ")"));
        }
        return warnings;
    }

    private static HashSet<string> TexturedNames(Model model) =>
        [.. model.Materials.Where(m => m.HasTexture).Select(m => m.Name)];

    /// <summary>
    /// One warning per textured material that faces use without UVs, in model order:
    /// <c>material 'wood' has a texture, but N faces using it have no UVs; their corners are written as (0, 0) (box faces 1, 2)</c>,
    /// the faces per mesh, the first few of each. The same rule as <c>check</c>'s, and these faces are not named again by <see cref="UvWarnings"/>.
    /// </summary>
    public static IReadOnlyList<string> MaterialUvWarnings(Model model)
    {
        var warnings = new List<string>();
        foreach (var material in model.Materials.Where(m => m.HasTexture))
        {
            var perMesh = model.Meshes
                .Select(m => (m.Name, Faces: m.Mesh.LiveFaces.Where(f => m.Mesh.Style?.MaterialOf(f) == material.Name && m.Mesh.Uvs?.IsMapped(f) != true).ToList()))
                .Where(m => m.Faces.Count > 0).ToList();
            if (perMesh.Count == 0) continue;
            var total = perMesh.Sum(m => m.Faces.Count);
            var where = string.Join("; ", perMesh.Select(m =>
                $"{m.Name} {(m.Faces.Count == 1 ? "face" : "faces")} {string.Join(", ", m.Faces.Take(UvWarningIds))}"
                + (m.Faces.Count > UvWarningIds ? $" and {(m.Faces.Count - UvWarningIds).ToString(CultureInfo.InvariantCulture)} more" : "")));
            warnings.Add(total == 1
                ? $"material '{material.Name}' has a texture, but 1 face using it has no UVs; its corners are written as (0, 0) ({where})"
                : $"material '{material.Name}' has a texture, but {total.ToString(CultureInfo.InvariantCulture)} faces using it have no UVs; their corners are written as (0, 0) ({where})");
        }
        return warnings;
    }

    private const int UvWarningIds = 5;

    /// <summary>As above, with the colours of <paramref name="mesh"/>.</summary>
    public static byte[] Write(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles, string name) => Write(mesh, triangles, name, []);

    /// <summary>
    /// As above, with the faces' materials given by <paramref name="materials"/> (the model's): one primitive per material
    /// after the colours', in order of first use by face, <c>TEXCOORD_0</c> written when any used material has a texture.
    /// </summary>
    public static byte[] Write(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles, string name, IReadOnlyList<CarveMaterial> materials)
    {
        var uvs = HasUvs(mesh) || Textured(UsedBy(mesh is null ? [] : [mesh], materials));
        var built = Build(mesh, triangles, name, materials, uvs, new Images(), null);
        var scene = new SceneBuilder();
        scene.AddRigidMesh(built.Mesh, Matrix4x4.Identity).WithName(name);
        var root = scene.ToGltf2();
        built.Targets?.Write(root, root.LogicalMeshes[0], built.Tags!);
        return root.WriteGLB().ToArray();
    }

    /// <summary>One glTF mesh as it is built, with its morph targets' writer and each primitive's vertex tags when the mesh has targets.</summary>
    internal sealed record Built(IMeshBuilder<MaterialBuilder> Mesh, GlbTargets? Targets, IReadOnlyList<IReadOnlyList<int>>? Tags);

    /// <summary>
    /// The glTF mesh of <paramref name="mesh"/>'s <paramref name="triangles"/>, named <paramref name="name"/>: a primitive per
    /// colour and material, <c>TEXCOORD_0</c> when <paramref name="uvs"/>, the mesh's own morph targets tagged on its corners.
    /// <paramref name="shared"/> (T-95), when given, holds the materials by primitive key across the meshes of one file, so a
    /// colour or material used by several meshes is one glTF material.
    /// </summary>
    internal static Built Build(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles, string name, IReadOnlyList<CarveMaterial> materials,
        bool uvs, Images images, Dictionary<string, MaterialBuilder>? shared)
    {
        if (mesh?.Targets is { } named && GlbTargets.For([.. named.Names]) is { } targets)
        {
            var piece = targets.Of(mesh);
            return uvs
                ? Build<Tagged<VertexTexture1>>(mesh, triangles, name, materials,
                    (m, i, v, n) => new(new VertexTexture1(UvAt(m!, i, v)), targets.Tag(piece, i, v, n)), true, targets, images, shared)
                : Build<Tagged<VertexEmpty>>(mesh, triangles, name, materials,
                    (_, i, v, n) => new(default, targets.Tag(piece, i, v, n)), false, targets, images, shared);
        }
        return uvs
            ? Build<VertexTexture1>(mesh, triangles, name, materials, (m, i, v, _) => new VertexTexture1(UvAt(m!, i, v)), true, null, images, shared)
            : Build<VertexEmpty>(mesh, triangles, name, materials, (_, _, _, _) => default, false, null, images, shared);
    }

    private static Built Build<TvM>(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles, string name, IReadOnlyList<CarveMaterial> materials,
        Func<PolygonMesh?, int, int, Vector3, TvM> texture, bool uvs, GlbTargets? targets, Images images, Dictionary<string, MaterialBuilder>? shared)
        where TvM : struct, IVertexMaterial
    {
        var mesh2 = new MeshBuilder<VertexPositionNormal, TvM, VertexEmpty>(name);
        var primitives = new Dictionary<string, PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, TvM, VertexEmpty>>();
        foreach (var p in Primitives(mesh, triangles, materials))
        {
            var key = p.Material is null ? "c" + (p.Colour ?? "") : "m" + p.Name;
            MaterialBuilder? material = null;
            if (shared is null || !shared.TryGetValue(key, out material))
            {
                material = p.Material is null ? Material(p.Name, p.Colour) : Material(p.Material, images);
                if (shared is not null) shared[key] = material;
            }
            primitives[key] = mesh2.UsePrimitive(material);
        }

        // Shading.Triangles is in Triangulator order, so the ids of a triangle's vertices (and so its corners) are read from there.
        // A target's corners are tagged with their deltas, so they need the vertex ids too.
        var cornered = uvs || targets is not null;
        var ids = cornered ? Triangulator.Triangulate(mesh!) : null;
        for (var i = 0; i < triangles.Count; i++)
        {
            var t = triangles[i];
            var primitive = primitives[KeyOf(mesh, t.Face)];
            var id = ids?[i] ?? default;
            primitive.AddTriangle(
                new VertexBuilder<VertexPositionNormal, TvM, VertexEmpty>(new VertexPositionNormal(t.A, t.NormalA), cornered ? texture(mesh, t.Face, id.A, t.NormalA) : default),
                new VertexBuilder<VertexPositionNormal, TvM, VertexEmpty>(new VertexPositionNormal(t.B, t.NormalB), cornered ? texture(mesh, t.Face, id.B, t.NormalB) : default),
                new VertexBuilder<VertexPositionNormal, TvM, VertexEmpty>(new VertexPositionNormal(t.C, t.NormalC), cornered ? texture(mesh, t.Face, id.C, t.NormalC) : default));
        }

        return new Built(mesh2, targets, targets is null ? null : GlbTargets.TagsOf(mesh2.Primitives));
    }
}
