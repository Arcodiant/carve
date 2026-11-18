using System.Text.Json;
using Empyrean;

namespace Carve.Server.Tests;

/// <summary>T-76: <c>export</c> through the real server of a cube with a textured material, loaded back through the engine's parser.</summary>
[Trait("Tier", "unit")]
public sealed class MaterialExportToolTests() : FileToolTests("carve-export-material")
{
    [Fact]
    public async Task A_textured_cube_exports_with_its_material_and_the_loader_reads_every_field_back()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var png = MaterialExportTests.Png(33);
        File.WriteAllBytes(Path.Combine(Root, "wood.png"), png);
        var rough = MaterialExportTests.Png(44);
        File.WriteAllBytes(Path.Combine(Root, "wood-mr.png"), rough);

        Ok(await carve.CallToolAsync("define_material", """{"name":"wood","base_colour":"#C8A06E","metallic":0.25,"roughness":0.6,"emissive":"#201000","double_sided":true,"base_colour_texture":"wood.png","metallic_roughness_texture":"wood-mr.png"}"""));
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]},{"face":2,"uv":[[0,0],[1,0],[1,1],[0,1]]}]}"""));
        File.WriteAllBytes(Path.Combine(Root, "glow.png"), MaterialExportTests.Png(55));
        Ok(await carve.CallToolAsync("define_material", """{"name":"glow","emissive_texture":"glow.png"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[0,1,2],"material":"wood"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[3],"material":"glow"}"""));

        var line = Ok(await carve.CallToolAsync("export", """{"path":"out/wood.glb"}"""));
        Assert.DoesNotContain('\n', line);
        Assert.Contains("3 materials 'carve', 'wood', 'glow'", line);
        Assert.Contains("material 'wood' has a texture, but 1 face using it has no UVs; its corners are written as (0, 0) (mesh face 1)", line);

        var parsed = GlbExportTests.Parser.Parse(File.ReadAllBytes(Path.Combine(Root, "out", "wood.glb")), ModelFormat.Gltf);
        Assert.Empty(parsed.Warnings);
        var wood = parsed.SubmeshMaterials[1]!;
        Assert.Equal(Carve.Core.FaceStyle.ToLinear("#C8A06E"), wood.BaseColor);
        Assert.Equal(0.25f, wood.Metallic);
        Assert.Equal(0.6f, wood.Roughness);
        Assert.True(wood.DoubleSided);
        Assert.Equal(png, wood.TextureBytes);
        Assert.Equal(rough, wood.MetallicRoughnessTexture!.Bytes);
        // An emissive texture with no emissive colour given: T-75 makes the colour white, so the factor reads back non-zero.
        Assert.NotEqual(System.Numerics.Vector3.Zero, parsed.SubmeshMaterials[2]!.EmissiveColor);
        Assert.NotNull(parsed.SubmeshMaterials[2]!.EmissiveTexture);
        Assert.Equal(3, parsed.SubmeshMaterials.Length);
    }
}
