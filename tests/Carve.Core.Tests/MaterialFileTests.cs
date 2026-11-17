using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// Carve file version 6 (T-74): materials, faces' materials and texture paths relative to the file, read back by content;
/// and every model without a material written as before, byte for byte.
/// </summary>
[Trait("Tier", "unit")]
public sealed class MaterialFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "carve-t74-" + Guid.NewGuid().ToString("N"));

    public MaterialFileTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>A PNG written under the test folder, and the texture read from it.</summary>
    private MaterialTexture Png(string relative, uint width, uint height, byte fill = 0)
    {
        var path = Path.GetFullPath(Path.Combine(_root, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, TestPngs.Png(width, height, fill: fill));
        return MaterialTexture.Load(path, out var problem) ?? throw new InvalidOperationException(problem);
    }

    private static PolygonMesh Box() => TestMeshes.Cube().WithStyle(
        FaceStyle.None.WithMaterial([1, 4], "wood").WithColour([2], "#C8503C").WithMaterial([0], "steel").WithGroup("lid", [1]));

    private Model Textured(string folder)
    {
        var wood = new Material("wood", Roughness: 0.8f, BaseColourTexture: Png($"{folder}/wood.png", 4, 2),
            NormalTexture: Png($"{folder}/tex/wood-normal.png", 8, 8, fill: 1));
        var steel = new Material("steel", BaseColour: "#c0c0c8", Metallic: 1f, Roughness: 0.25f, Emissive: "#100000", Occlusion: 0.5f,
            DoubleSided: true, MetallicRoughnessTexture: Png($"{folder}/tex/steel-mr.png", 16, 16, fill: 2),
            EmissiveTexture: Png($"{folder}/tex/steel-mr.png", 16, 16, fill: 2), OcclusionTexture: Png($"{folder}/tex/sub/ao.png", 2, 2));
        return Model.Empty.WithMaterial(wood).WithMaterial(steel).WithMaterial(new Material("unused")).With("box", Box());
    }

    [Fact]
    public void A_version_6_file_is_written_as_the_format_says()
    {
        var folder = Path.Combine(_root, "a");
        var text = CarveFile.Write(Textured("a"), folder);
        Assert.StartsWith("""
            {
              "carve": 6,
              "materials": [
                {"name": "wood", "roughness": 0.8, "base_colour_texture": "wood.png", "normal_texture": "tex/wood-normal.png"},
                {"name": "steel", "base_colour": "#c0c0c8", "metallic": 1, "roughness": 0.25, "emissive": "#100000", "occlusion": 0.5, "double_sided": true, "metallic_roughness_texture": "tex/steel-mr.png", "emissive_texture": "tex/steel-mr.png", "occlusion_texture": "tex/sub/ao.png"},
                {"name": "unused"}
              ],
              "meshes": [
            """.ReplaceLineEndings("\n"), text);
        Assert.Contains("""
                  "colours": [
                    {"colour": "#C8503C", "faces": [2]}
                  ],
                  "materials": [
                    {"material": "steel", "faces": [0]},
                    {"material": "wood", "faces": [1, 4]}
                  ],
                  "groups": [
            """.ReplaceLineEndings("\n"), text);
    }

    [Fact]
    public void A_model_round_trips_by_content_with_textures_beside_the_file_and_in_subfolders()
    {
        var folder = Path.Combine(_root, "a");
        var model = Textured("a");
        var text = CarveFile.Write(model, folder);
        var read = CarveFile.Read(text, folder);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(6, read.Version);
        Assert.Equal(model.Materials, read.Model!.Materials); // names, factors, absolute paths and every byte
        Assert.Equal(model.Find("box")!.Style!.Materials, read.Model.Find("box")!.Style!.Materials);
        Assert.Equal(text, CarveFile.Write(read.Model, folder));
    }

    [Fact]
    public void Saved_to_another_folder_the_paths_are_relative_to_it()
    {
        var model = Textured("a");
        var elsewhere = Path.Combine(_root, "b", "c");
        Directory.CreateDirectory(elsewhere);
        var text = CarveFile.Write(model, elsewhere);
        Assert.Contains("\"base_colour_texture\": \"../../a/wood.png\"", text);
        Assert.Contains("\"occlusion_texture\": \"../../a/tex/sub/ao.png\"", text);
        var read = CarveFile.Read(text, elsewhere);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(model.Materials, read.Model!.Materials);
    }

    [Fact]
    public void A_missing_texture_names_the_path_the_file_gave_and_the_one_tried()
    {
        var folder = Path.Combine(_root, "a");
        var text = CarveFile.Write(Textured("a"), folder);
        File.Delete(Path.Combine(folder, "tex", "wood-normal.png"));
        var read = CarveFile.Read(text, folder);
        Assert.False(read.Succeeded);
        var error = Assert.Single(read.Errors);
        Assert.Equal("materials[0].normal_texture", error.Element);
        Assert.StartsWith($"cannot read 'tex/wood-normal.png' ({Path.Combine(folder, "tex", "wood-normal.png")}): ", error.Message);
    }

    [Fact]
    public void A_texture_that_is_not_a_PNG_is_refused_at_its_field()
    {
        var folder = Path.Combine(_root, "a");
        var text = CarveFile.Write(Textured("a"), folder);
        File.WriteAllBytes(Path.Combine(folder, "wood.png"), TestPngs.Png(5000, 4));
        var error = Assert.Single(CarveFile.Read(text, folder).Errors);
        Assert.Equal("materials[0].base_colour_texture", error.Element);
        Assert.EndsWith("the PNG is 5000 x 4; a texture is 1 to 4096 pixels on each side", error.Message);
    }

    [Fact]
    public void Without_the_folder_a_texture_is_refused_both_ways()
    {
        var text = CarveFile.Write(Textured("a"), Path.Combine(_root, "a"));
        Assert.Contains(CarveFile.Read(text).Errors, e => e.Element == "materials[0].base_colour_texture"
            && e.Message == "a texture needs the file's folder, since its path is relative to it");
        Assert.Throws<ArgumentException>(() => CarveFile.Write(Textured("a")));
    }

    [Fact]
    public void A_texture_on_another_drive_refuses_the_save_naming_it()
    {
        var drive = char.ToUpperInvariant(_root[0]) == 'Q' ? 'R' : 'Q';
        var path = $@"{drive}:\textures\wood.png";
        var model = Model.Empty.WithMaterial(new Material("wood", BaseColourTexture: MaterialTexture.FromPng(path, TestPngs.Png(4, 4), out _)));
        var problem = CarveFile.TextureProblem(model, _root);
        Assert.NotNull(problem);
        Assert.StartsWith($"materials[0].base_colour_texture: the texture '{path}' of the material 'wood' has no path relative to", problem);
        Assert.Equal(problem, Assert.Throws<ArgumentException>(() => CarveFile.Write(model, _root)).Message.Split(" (Parameter")[0]);
    }

    [Fact]
    public void Every_field_at_its_default_writes_only_the_name_and_an_unused_material_is_still_version_6()
    {
        var model = Model.Empty.WithMaterial(new Material("plain")).With("cube", TestMeshes.Cube());
        var text = CarveFile.Write(model);
        Assert.Contains("\"carve\": 6,\n  \"materials\": [\n    {\"name\": \"plain\"}\n  ],\n", text);
        Assert.DoesNotContain("\"material\":", text); // no face has one, so the mesh key is left out
        var read = CarveFile.Read(text);
        Assert.Equal([new Material("plain")], read.Model!.Materials);
        Assert.Equal(text, CarveFile.Write(read.Model));
    }

    [Fact]
    public void Hex_is_kept_as_given_and_negative_zero_is_not_a_default()
    {
        var m = new Material("m", BaseColour: "#ffffff", Metallic: -0f);
        var text = CarveFile.Write(Model.Empty.WithMaterial(m).With("cube", TestMeshes.Cube()));
        Assert.Contains("{\"name\": \"m\", \"base_colour\": \"#ffffff\", \"metallic\": -0}", text);
        Assert.Equal(m, CarveFile.Read(text).Model!.Materials[0]);
    }

    private static string WithMaterials(string materials, string uses, string colours = "[]") => $$"""
        {"carve": 6, "materials": {{materials}}, "meshes": [{"name": "m", "vertices": [[0, 0, 0], [1, 0, 0], [0, 1, 0], [1, 1, 0]],
          "faces": [[0, 1, 2], [1, 3, 2]], "colours": {{colours}}, "materials": {{uses}}, "groups": [], "weights": [],
          "shading": "flat", "hard_edges": []}], "skeleton": null, "clips": []}
        """;

    [Fact]
    public void A_faces_undefined_material_is_refused_naming_the_ones_there_are()
    {
        var read = CarveFile.Read(WithMaterials("""[{"name": "wood"}, {"name": "steel"}]""", """[{"material": "glass", "faces": [0]}]"""));
        var error = Assert.Single(read.Errors);
        Assert.Equal(("meshes[0].materials[0].material", "no material named 'glass'; the materials are 'wood', 'steel'"), (error.Element, error.Message));
        error = Assert.Single(CarveFile.Read(WithMaterials("[]", """[{"material": "glass", "faces": [0]}]""")).Errors);
        Assert.Equal("no material named 'glass'; the file defines no materials", error.Message);
    }

    [Fact]
    public void A_face_with_two_materials_or_a_colour_and_a_material_is_refused_at_the_face()
    {
        var twice = CarveFile.Read(WithMaterials("""[{"name": "wood"}, {"name": "steel"}]""",
            """[{"material": "wood", "faces": [0]}, {"material": "steel", "faces": [1, 0]}]"""));
        Assert.Equal([("meshes[0].materials[1].faces[1]", "face 0 is given a material earlier; give each face one material")],
            twice.Errors.Select(e => (e.Element, e.Message)));
        var both = CarveFile.Read(WithMaterials("""[{"name": "wood"}]""", """[{"material": "wood", "faces": [1]}]""",
            """[{"colour": "#C8503C", "faces": [1]}]"""));
        Assert.Equal([("meshes[0].materials[0].faces[0]", "face 1 is given a colour too; a face has a colour or a material, not both")],
            both.Errors.Select(e => (e.Element, e.Message)));
    }

    [Theory]
    [InlineData("""[{"name": "carve"}]""", "materials[0].name")]
    [InlineData("""[{"name": "a:b"}]""", "materials[0].name")]
    [InlineData("""[{"name": ""}]""", "materials[0].name")]
    [InlineData("""[{"name": "w"}, {"name": "w"}]""", "materials[1].name")]
    [InlineData("""[{"roughness": 1}]""", "materials[0].name")]
    [InlineData("""[{"name": "w", "roughness": 1.5}]""", "materials[0].roughness")]
    [InlineData("""[{"name": "w", "metallic": -1}]""", "materials[0].metallic")]
    [InlineData("""[{"name": "w", "occlusion": "1"}]""", "materials[0].occlusion")]
    [InlineData("""[{"name": "w", "base_colour": "red"}]""", "materials[0].base_colour")]
    [InlineData("""[{"name": "w", "emissive": "#00000"}]""", "materials[0].emissive")]
    [InlineData("""[{"name": "w", "double_sided": 1}]""", "materials[0].double_sided")]
    [InlineData("""[{"name": "w", "shine": 1}]""", "materials[0].shine")]
    [InlineData("""[{"name": "w", "normal_texture": 3}]""", "materials[0].normal_texture")]
    [InlineData("""{"name": "w"}""", "materials")]
    public void Every_material_rule_is_checked_on_read(string materials, string element)
    {
        var read = CarveFile.Read(WithMaterials(materials, "[]"));
        Assert.False(read.Succeeded);
        Assert.Equal(element, Assert.Single(read.Errors).Element);
    }

    [Fact]
    public void Materials_in_an_older_version_are_an_unknown_key()
    {
        var text = WithMaterials("""[{"name": "w"}]""", "[]").Replace("\"carve\": 6", "\"carve\": 5");
        Assert.Equal(["materials", "meshes[0].materials"], CarveFile.Read(text).Errors.Select(e => e.Element));
    }

    /// <summary>
    /// The byte-identical guard: every committed Carve file reads at its own version and writes back to its exact
    /// bytes (as git holds them, <c>\n</c> line ends), so no file without a material moves to version 6.
    /// </summary>
    [Theory]
    [InlineData("tests/Carve.Core.Tests/Fixtures/leaky-shed-v1.carve", 1)]
    [InlineData("samples/leaky-shed.carve", 2)]
    [InlineData("samples/gardener.carve", 2)]
    [InlineData("samples/gardener-soft.carve", 3)]
    [InlineData("samples/gatehouse.carve", 4)]
    [InlineData("samples/barrel.carve", 5)]
    public void Every_committed_file_reads_and_writes_unchanged(string path, int version)
    {
        var committed = File.ReadAllText(Path.Combine(RepoRoot(), path)).ReplaceLineEndings("\n");
        var read = CarveFile.Read(committed, Path.GetDirectoryName(Path.Combine(RepoRoot(), path)));
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(version, read.Version);
        Assert.Empty(read.Model!.Materials);
        if (version == 1) return; // version 1 is read only; the model writes as version 2
        var written = Encoding.UTF8.GetBytes(CarveFile.Write(read.Model));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(committed))), Convert.ToHexString(SHA256.HashData(written)));
        Assert.Equal(written, Encoding.UTF8.GetBytes(CarveFile.Write(read.Model, RepoRoot())));
    }

    /// <summary>The byte-identical guard for the one committed version 6 file, whose texture paths are relative to its own folder (RV-47).</summary>
    [Fact]
    public void The_committed_textured_barrel_reads_and_writes_unchanged()
    {
        var path = Path.Combine(RepoRoot(), "samples", "barrel-textured.carve");
        var committed = File.ReadAllText(path).ReplaceLineEndings("\n");
        var folder = Path.GetDirectoryName(path)!;
        var read = CarveFile.Read(committed, folder);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(6, read.Version);
        Assert.NotEmpty(read.Model!.Materials);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(committed))),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CarveFile.Write(read.Model, folder)))));
    }

    /// <summary>Meshes 'a' and 'b', each using its own material; 'other' (b's) has a texture.</summary>
    private string TwoMeshes(string folder)
    {
        var model = Model.Empty.WithMaterial(new Material("used", BaseColour: "#C8503C"))
            .WithMaterial(new Material("other", BaseColourTexture: Png($"{folder}/other.png", 4, 4)))
            .With("a", TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial([0], "used")))
            .With("b", TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial([1], "other")));
        return CarveFile.Write(model, Path.Combine(_root, folder));
    }

    [Fact]
    public void Read_for_one_mesh_reads_only_the_materials_it_uses()
    {
        var folder = Path.Combine(_root, "m");
        var text = TwoMeshes("m");
        File.Delete(Path.Combine(folder, "other.png"));

        var a = CarveFile.Read(text, folder, "a");
        Assert.True(a.Succeeded, string.Join("; ", a.Errors));
        Assert.Equal(["a"], a.Model!.Meshes.Select(m => m.Name));
        Assert.Equal([new Material("used", BaseColour: "#C8503C")], a.Model.Materials);

        // The mesh that uses the missing texture, and the whole file, are still refused at the material's field.
        Assert.Equal("materials[1].base_colour_texture", Assert.Single(CarveFile.Read(text, folder, "b").Errors).Element);
        Assert.Equal("materials[1].base_colour_texture", Assert.Single(CarveFile.Read(text, folder).Errors).Element);
        // A name the file has no mesh of reads the whole file, as before.
        Assert.Equal("materials[1].base_colour_texture", Assert.Single(CarveFile.Read(text, folder, "c").Errors).Element);
    }

    [Fact]
    public void Read_for_one_mesh_with_every_texture_there_is_that_mesh_and_its_materials()
    {
        var folder = Path.Combine(_root, "m");
        var b = CarveFile.Read(TwoMeshes("m"), folder, "b");
        Assert.True(b.Succeeded, string.Join("; ", b.Errors));
        Assert.Equal(["b"], b.Model!.Meshes.Select(m => m.Name));
        Assert.Equal(["other"], b.Model.Materials.Select(m => m.Name));
    }

    /// <summary>Two spellings of one texture path, differing in case, each keep their own path (RV-47); on a case-sensitive system they are two files.</summary>
    [Fact]
    public void Texture_paths_differing_in_case_are_read_each_as_given()
    {
        if (!OperatingSystem.IsWindows()) return; // the one file must answer to both spellings
        var folder = Path.Combine(_root, "c");
        Png("c/tex/a.png", 4, 4);
        var text = WithMaterials("""[{"name": "one", "base_colour_texture": "tex/a.png"}, {"name": "two", "base_colour_texture": "TEX/A.png"}]""", "[]");
        var read = CarveFile.Read(text, folder);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(Path.Combine(folder, "tex", "a.png"), read.Model!.Materials[0].BaseColourTexture!.Path);
        Assert.Equal(Path.Combine(folder, "TEX", "A.png"), read.Model.Materials[1].BaseColourTexture!.Path);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "gatehouse.carve"))) dir = dir.Parent!;
        return dir.FullName;
    }
}
