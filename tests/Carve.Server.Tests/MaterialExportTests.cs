extern alias carve;

using GameLimits = carve::Carve.Server.GameLimits;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;
using Material = Carve.Core.Material;
using Model = Carve.Core.Model;
using Skeleton = Carve.Core.Skeleton;

namespace Carve.Server.Tests;

/// <summary>
/// T-76: materials in the export, written as glTF metallic-roughness materials and read back through the engine's own
/// parser (<c>IModelGeometryLoader.Parse</c>): every field, each texture's bytes, a shared texture embedded once,
/// no parse warnings, primitive order and names, static and skinned.
/// </summary>
[Trait("Tier", "unit")]
public class MaterialExportTests
{
    /// <summary>A real PNG of 2 x 2 pixels, its pixels from <paramref name="seed"/>.</summary>
    internal static byte[] Png(byte seed)
    {
        var raw = new List<byte>();
        for (var y = 0; y < 2; y++)
        {
            raw.Add(0);
            for (var x = 0; x < 2; x++) raw.AddRange([(byte)(seed + x), (byte)((seed * 3) + y), seed, 255]);
        }
        return TestPng.Write([0, 0, 0, 2, 0, 0, 0, 2, 8, 6, 0, 0, 0], [.. raw]);
    }

    internal static MaterialTexture Texture(string name, byte seed) =>
        MaterialTexture.FromPng(Path.Combine(Path.GetTempPath(), "carve-t76", name), Png(seed), out var problem) ?? throw new InvalidOperationException(problem);

    internal static readonly Material Wood = new("wood", "#C8A06E", 0.25f, 0.6f, "#201000", 0.8f, true,
        Texture("wood.png", 10), Texture("wood-mr.png", 20), Texture("wood-n.png", 30), Texture("wood-e.png", 40), Texture("wood-ao.png", 50));

    internal static readonly Material Steel = new("steel", "#9090A0", 1f, 0.3f,
        BaseColourTexture: Texture("shared.png", 70), OcclusionTexture: Texture("steel-ao.png", 80));

    private static JsonElement Json(byte[] glb)
    {
        Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
        return JsonDocument.Parse(glb.AsMemory(20, BitConverter.ToInt32(glb, 12))).RootElement;
    }

    private static ParsedModel Parse(byte[] glb) => GlbExportTests.Parser.Parse(glb, ModelFormat.Gltf);

    private static string[] PrimitiveNames(byte[] glb)
    {
        var json = Json(glb);
        var materials = json.GetProperty("materials");
        return [.. json.GetProperty("meshes")[0].GetProperty("primitives").EnumerateArray()
            .Select(p => materials[p.GetProperty("material").GetInt32()].GetProperty("name").GetString()!)];
    }

    private static JsonElement MaterialJson(byte[] glb, string name) =>
        Json(glb).GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == name);

    private static PolygonMesh Mapped(PolygonMesh mesh, params int[] faces)
    {
        var uvs = MeshUvs.None;
        foreach (var f in faces)
            uvs = uvs.WithFace(f, [.. mesh.Faces[f].Corners.Select((_, k) => new Vector2((f * 4 + k) / 32f + 0.01f, 1f - ((f * 4 + k) / 37f)))]);
        return mesh.WithUvs(uvs);
    }

    /// <summary>The cube: faces 0 and 1 red, 2 and 3 'wood', 4 'steel', 5 uncoloured; UVs on every face.</summary>
    private static Model StaticModel(params Material[] materials)
    {
        var mesh = Mapped(TestMeshes.Cube(), 0, 1, 2, 3, 4, 5);
        var model = Model.Of("cube", mesh);
        foreach (var m in materials) model = model.WithMaterial(m);
        return model.With("cube", mesh.WithStyle(FaceStyle.None.WithColour([0, 1], "#C8503C").WithMaterial([2, 3], "wood").WithMaterial([4], "steel")));
    }

    private static byte[] Static(Model model, string name = "cube")
    {
        var mesh = model.Combined!;
        return GlbExport.Write(mesh, Shading.Triangles(mesh), name, model.Materials);
    }

    [Fact]
    public void A_static_model_writes_colours_first_then_its_materials_in_order_of_first_use()
    {
        var model = StaticModel(Steel, Wood);
        var glb = Static(model);
        var parsed = Parse(glb);
        Assert.Empty(parsed.Warnings);
        Assert.Equal(["carve", "carve #C8503C", "wood", "steel"], PrimitiveNames(glb));
        Assert.Equal(["carve", "carve #C8503C", "wood", "steel"], GlbExport.MaterialNames(model.Combined!, model.Materials));
        Assert.Equal(4, parsed.SubmeshMaterials.Length);
        // A cube face is two triangles: one uncoloured, two red, two wood, one steel.
        Assert.Equal([6, 12, 12, 6], parsed.MeshData.Submeshes!.Select(r => r.IndexCount));
    }

    [Fact]
    public void Every_field_and_texture_is_read_back_by_the_engines_parser()
    {
        var parsed = Parse(Static(StaticModel(Steel, Wood)));
        var wood = parsed.SubmeshMaterials[2]!;
        Assert.Equal(FaceStyle.ToLinear("#C8A06E"), wood.BaseColor);
        Assert.True(wood.Pbr);
        Assert.Equal(0.25f, wood.Metallic);
        Assert.Equal(0.6f, wood.Roughness);
        var emissive = FaceStyle.ToLinear("#201000");
        Assert.Equal(new Vector3(emissive.X, emissive.Y, emissive.Z), wood.EmissiveColor);
        Assert.Equal(0.8f, wood.Occlusion);
        Assert.True(wood.DoubleSided);
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.BaseColourTexture!).Png.ToArray(), wood.TextureBytes);
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.MetallicRoughnessTexture!).Png.ToArray(), wood.MetallicRoughnessTexture!.Bytes);
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.NormalTexture!).Png.ToArray(), wood.NormalTexture!.Bytes);
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.EmissiveTexture!).Png.ToArray(), wood.EmissiveTexture!.Bytes);
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.OcclusionTexture!).Png.ToArray(), wood.OcclusionTexture!.Bytes);

        var steel = parsed.SubmeshMaterials[3]!;
        Assert.Equal(1f, steel.Metallic);
        Assert.Equal(0.3f, steel.Roughness);
        Assert.False(steel.DoubleSided);
        Assert.Null(steel.MetallicRoughnessTexture);
        Assert.Null(steel.NormalTexture);
        Assert.Null(steel.EmissiveTexture);
    }

    [Fact]
    public void A_material_at_its_defaults_is_matte_white_single_sided_with_no_emissive_or_occlusion_written()
    {
        var glb = Static(StaticModel(Steel, new Material("wood")));
        var wood = Parse(glb).SubmeshMaterials[2]!;
        Assert.Equal(new Vector4(1, 1, 1, 1), wood.BaseColor);
        Assert.Equal(0f, wood.Metallic);
        Assert.Equal(1f, wood.Roughness);
        Assert.Equal(Vector3.Zero, wood.EmissiveColor);
        Assert.False(wood.DoubleSided);
        Assert.Null(wood.TextureBytes);
        var material = MaterialJson(glb, "wood");
        Assert.False(material.TryGetProperty("emissiveFactor", out _));
        Assert.False(material.TryGetProperty("occlusionTexture", out _));
        Assert.Equal(0.0, material.GetProperty("pbrMetallicRoughness").GetProperty("metallicFactor").GetDouble()); // written: glTF's default is 1
    }

    /// <summary>What export's description says of the factors (RV-47): each is left out when it equals glTF's default 1.</summary>
    [Fact]
    public void Metallic_and_roughness_factors_are_left_out_where_they_equal_glTFs_default_of_1()
    {
        var glb = Static(StaticModel(Steel with { Roughness = 1f }, new Material("wood")));
        var wood = MaterialJson(glb, "wood").GetProperty("pbrMetallicRoughness");
        Assert.Equal(0.0, wood.GetProperty("metallicFactor").GetDouble());
        Assert.False(wood.TryGetProperty("roughnessFactor", out _));
        var metal = MaterialJson(glb, "steel").GetProperty("pbrMetallicRoughness");
        Assert.False(metal.TryGetProperty("metallicFactor", out _));
        Assert.False(metal.TryGetProperty("roughnessFactor", out _));
        var parsed = Parse(glb).SubmeshMaterials[3]!; // carve, carve #C8503C, wood, steel
        Assert.Equal((1f, 1f), (parsed.Metallic, parsed.Roughness));
    }

    [Fact]
    public void Each_corners_UV_is_written_as_Carve_holds_it_so_a_texture_lands_unflipped()
    {
        var model = StaticModel(Steel, Wood);
        var mesh = model.Combined!;
        var expected = new HashSet<(Vector3, Vector2)>();
        foreach (var f in mesh.LiveFaces)
            for (var k = 0; k < mesh.Faces[f].Corners.Count; k++) expected.Add((mesh.Vertices[mesh.Faces[f].Corners[k]], mesh.Uvs!.Of(f)![k]));
        var vertices = Parse(Static(model)).MeshData.Vertices;
        Assert.NotEmpty(vertices);
        Assert.All(vertices, v => Assert.Contains((v.Position, v.TexCoord), expected));
    }

    [Fact]
    public void An_emissive_texture_with_a_black_factor_writes_no_factor_and_the_loader_reads_it_as_it_would_glTF()
    {
        var glb = Static(StaticModel(Steel, Wood with { Emissive = "#000000" }));
        Assert.False(MaterialJson(glb, "wood").TryGetProperty("emissiveFactor", out _));
        var wood = Parse(glb).SubmeshMaterials[2]!;
        Assert.Equal(Vector3.Zero, wood.EmissiveColor); // glTF multiplies the texture by the factor, so it glows only when the factor is set
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.EmissiveTexture!).Png.ToArray(), wood.EmissiveTexture!.Bytes);
    }

    [Fact]
    public void A_texture_shared_by_two_materials_is_embedded_once()
    {
        var model = StaticModel(Steel, Wood with { BaseColourTexture = Steel.BaseColourTexture });
        var glb = Static(model);
        // wood: five textures, one of them the shared one; steel: the shared one and its occlusion map.
        Assert.Equal(6, Json(glb).GetProperty("images").GetArrayLength());
        var parsed = Parse(glb);
        Assert.Same(parsed.SubmeshMaterials[2]!.TextureBytes, parsed.SubmeshMaterials[3]!.TextureBytes);
        Assert.Equal(((Carve.Core.MaterialTexture)Steel.BaseColourTexture!).Png.ToArray(), parsed.SubmeshMaterials[3]!.TextureBytes);
    }

    [Fact]
    public void No_alpha_fields_no_sampler_and_the_occlusion_strength_only_with_its_texture()
    {
        var glb = Static(StaticModel(Steel with { Occlusion = 0.5f, OcclusionTexture = null }, Wood));
        var json = Json(glb);
        Assert.False(json.TryGetProperty("samplers", out _));
        Assert.DoesNotContain("alphaMode", json.GetRawText());
        Assert.DoesNotContain("alphaCutoff", json.GetRawText());
        Assert.False(MaterialJson(glb, "steel").TryGetProperty("occlusionTexture", out _));
        Assert.Equal(0.8, MaterialJson(glb, "wood").GetProperty("occlusionTexture").GetProperty("strength").GetDouble(), 6);
    }

    [Fact]
    public void A_textured_material_writes_TEXCOORD_0_even_when_no_face_has_UVs_and_an_untextured_one_does_not()
    {
        static Model Bare(Material m) =>
            Model.Of("cube", TestMeshes.Cube()).WithMaterial(m).With("cube", TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial([0], m.Name)));
        var textured = Static(Bare(Wood));
        Assert.Contains("TEXCOORD_0", Json(textured).GetRawText());
        Assert.Empty(Parse(textured).Warnings);
        Assert.DoesNotContain("TEXCOORD_0", Json(Static(Bare(new Material("plain", "#FF0000")))).GetRawText());
    }

    [Fact]
    public void A_textured_face_without_UVs_is_warned_of_by_its_material_and_not_again_as_a_bare_face()
    {
        var mesh = Mapped(TestMeshes.Cube(), 0);
        var model = Model.Of("cube", mesh).WithMaterial(Wood).With("cube", mesh.WithStyle(FaceStyle.None.WithMaterial([1, 2], "wood")));
        Assert.Equal(["material 'wood' has a texture, but 2 faces using it have no UVs; their corners are written as (0, 0) (cube faces 1, 2)"],
            GlbExport.MaterialUvWarnings(model));
        Assert.Equal(["cube: 3 faces have no UVs; their corners are written as (0, 0) (faces 3, 4, 5)"], GlbExport.UvWarnings(model));
        Assert.Empty(GlbExport.MaterialUvWarnings(StaticModel(Steel, Wood)));
    }

    private static Skeleton Rig() => Skeleton.Create([new BoneSpec("Hips", null, new Vector3(0, 1, 0), Quaternion.Identity)]).Skeleton!;

    private static PolygonMesh Weighted(PolygonMesh mesh)
    {
        var weights = VertexWeights.None;
        for (var v = 0; v < mesh.Vertices.Count; v++) weights = weights.With([v], [new Influence(0, 1)]);
        return mesh.WithWeights(weights);
    }

    /// <summary>Pieces 'body' (a red face, two wood, the rest uncoloured) and 'coat' (a steel face, a wood one, the rest uncoloured).</summary>
    private static Model Skinned(params Material[] materials)
    {
        var body = Weighted(TestMeshes.Cube()).WithStyle(FaceStyle.None.WithColour([0], "#C8503C").WithMaterial([1, 2], "wood"));
        var coat = Weighted(TestMeshes.Cube()).WithStyle(FaceStyle.None.WithMaterial([0], "steel").WithMaterial([1], "wood"));
        var model = Model.Of("body", TestMeshes.Cube()).WithSkeleton(Rig());
        foreach (var m in materials) model = model.WithMaterial(m);
        return model.With("body", body).With("coat", coat);
    }

    [Fact]
    public void A_skinned_model_writes_primitives_per_piece_and_a_material_shared_between_pieces_is_one_primitive()
    {
        var glb = SkinnedExport.Write(Skinned(Steel, Wood), "hero");
        var parsed = Parse(glb.Bytes);
        Assert.Empty(parsed.Warnings);
        Assert.Equal(["body:c8503c", "body", "wood", "coat", "steel"], PrimitiveNames(glb.Bytes));
        Assert.Equal(["body:c8503c", "body", "wood", "coat", "steel"], glb.Materials);
        Assert.Equal(5, Json(glb.Bytes).GetProperty("materials").GetArrayLength());
        Assert.Equal(5, glb.Primitives); // the coat's wood faces join the body's wood primitive
        Assert.Equal(5, parsed.SubmeshMaterials.Length);
        var wood = parsed.SubmeshMaterials.Select(m => m!).First(m => m.Metallic == 0.25f);
        Assert.Equal(FaceStyle.ToLinear("#C8A06E"), wood.BaseColor);
        Assert.Equal(0.8f, wood.Occlusion);
        Assert.True(wood.DoubleSided);
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.OcclusionTexture!).Png.ToArray(), wood.OcclusionTexture!.Bytes);
        Assert.Equal(((Carve.Core.MaterialTexture)Wood.BaseColourTexture!).Png.ToArray(), wood.TextureBytes);
        Assert.Contains("TEXCOORD_0", Json(glb.Bytes).GetRawText()); // no face has UVs, yet a used material has a texture
    }

    [Fact]
    public void A_material_named_like_a_mesh_renames_that_pieces_uncoloured_primitive_only_when_they_clash()
    {
        var plain = Weighted(TestMeshes.Cube());
        var clash = Model.Of("wood", plain).WithSkeleton(Rig()).WithMaterial(Wood)
            .With("wood", plain.WithStyle(FaceStyle.None.WithMaterial([0], "wood")));
        var glb = SkinnedExport.Write(clash, "hero");
        Assert.Equal(["wood:uncoloured", "wood"], glb.Materials);
        Assert.Equal(["wood:uncoloured", "wood"], PrimitiveNames(glb.Bytes));
        Assert.Empty(Parse(glb.Bytes).Warnings);

        // The same names with no material of that name in use: the piece keeps its bare name.
        var fine = Model.Of("wood", plain).WithSkeleton(Rig()).WithMaterial(Wood).With("wood", plain);
        Assert.Equal(["wood"], SkinnedExport.Write(fine, "hero").Materials);
    }

    [Fact]
    public void Clips_mode_writes_no_materials()
    {
        var rig = Rig();
        var walk = RigTestHelpers.MakeClip("walk", 1f, rig,
            new BoneChannelsSpec("Hips", null, new ChannelSpec<Vector3>("STEP", [new(0.5f, new Vector3(0, 1.1f, 0))])));
        var json = Json(ClipsExport.Write(Skinned(Steel, Wood).WithClips([walk]), "hero"));
        Assert.False(json.TryGetProperty("materials", out _));
        Assert.False(json.TryGetProperty("images", out _));
    }

    [Fact]
    public void Two_exports_of_one_model_are_byte_identical()
    {
        Assert.Equal(Static(StaticModel(Steel, Wood)), Static(StaticModel(Steel, Wood)));
        Assert.Equal(SkinnedExport.Write(Skinned(Steel, Wood), "hero").Bytes, SkinnedExport.Write(Skinned(Steel, Wood), "hero").Bytes);
    }

    [Fact]
    public void The_submesh_warning_counts_material_primitives_and_names_the_materials()
    {
        var model = StaticModel(Steel, Wood);
        var warning = Assert.Single(new GameLimits(null, 2, null, null).Warnings(model, 12, GlbExport.MaterialNames(model.Combined!, model.Materials).Length));
        Assert.Equal("4 submeshes, 1 colours and 2 materials, above the game's max_submeshes 2; share colours and materials between pieces where you can", warning);
    }
}
