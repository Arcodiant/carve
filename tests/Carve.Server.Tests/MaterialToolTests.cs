using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>define_material, set_material and their read-back through the real server (T-75).</summary>
[Trait("Tier", "regression")]
public class MaterialToolTests : FileToolTests
{
    public MaterialToolTests() : base("material")
    {
    }

    private static string Fixture { get; } = Path.Combine(Paths.RepoRoot, "tests", "Carve.Server.Tests", "Fixtures", "swatch.png");

    /// <summary>The fixture PNG copied to <paramref name="relative"/> under the working folder; the absolute path.</summary>
    private string Texture(string relative)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(Fixture, path, overwrite: true);
        return path;
    }

    private static JsonElement Body(string text) => JsonDocument.Parse(text).RootElement;

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    [Fact]
    public async Task The_two_descriptions_examples_define_and_assign_on_the_cube_and_get_mesh_and_check_read_it_back()
    {
        Texture("textures/crate.png");
        var (carve, tools) = await StartAsync();
        using var scope = carve;

        var defined = Body(Ok(await carve.CallToolAsync("define_material", Examples(tools, "define_material").Single())));
        Assert.False(defined.GetProperty("replaced").GetBoolean());
        var texture = defined.GetProperty("material").GetProperty("base_colour_texture");
        Assert.Equal(Path.Combine(Root, "textures", "crate.png"), texture.GetProperty("path").GetString());
        Assert.Equal(4, texture.GetProperty("width").GetInt32());

        // Box-mapped faces 2 and 3, as set_material's own example says, then the material on them.
        Ok(await carve.CallToolAsync("project_uvs", """{"method":"box","faces":[2,3]}"""));
        var set = Body(Ok(await carve.CallToolAsync("set_material", Examples(tools, "set_material").Single())));
        Assert.Equal([2, 3], set.GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()));
        Assert.False(set.TryGetProperty("warnings", out _));

        var mesh = Body(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        var materials = mesh.GetProperty("materials").EnumerateArray().ToArray();
        Assert.Single(materials);
        Assert.Equal("crate", materials[0].GetProperty("name").GetString());
        Assert.Equal(0.8, materials[0].GetProperty("roughness").GetDouble(), 6);
        Assert.Equal(4, materials[0].GetProperty("base_colour_texture").GetProperty("height").GetInt32());
        Assert.Equal(JsonValueKind.Null, materials[0].GetProperty("normal_texture").ValueKind);
        var onFaces = mesh.GetProperty("face_materials").EnumerateArray().Single();
        Assert.Equal("crate", onFaces.GetProperty("material").GetString());
        Assert.Equal([2, 3], onFaces.GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()));
        Assert.Empty(mesh.GetProperty("colours").EnumerateArray());

        // Faces 2 and 3 are mapped: no warning for them; the other four are the generic unmapped ones.
        var warnings = Strings(Body(Ok(await carve.CallToolAsync("check", "{}"))).GetProperty("warnings"));
        Assert.DoesNotContain(warnings, w => w.StartsWith("faces[2]:") || w.StartsWith("faces[3]:"));
        Assert.Equal(4, warnings.Count(w => w.EndsWith("no UVs, so the face samples the texture's corner")));
    }

    [Fact]
    public async Task Check_names_the_material_of_a_textured_face_without_UVs_once_per_face_replacing_the_generic_warning()
    {
        Texture("a.png");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"wood","base_colour_texture":"a.png"}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"paint","base_colour":"#3C78C8"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"wood","faces":[1,4]}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"paint","faces":[0]}"""));

        // No UVs anywhere: only the textured faces are warned of, and only by the new warning.
        var check = Body(Ok(await carve.CallToolAsync("check", "{}")));
        const string text = "material 'wood' has a texture but the face has no UVs, so it samples the texture's corner";
        Assert.Equal([$"faces[1]: {text}", $"faces[4]: {text}"], Strings(check.GetProperty("warnings")));
        Assert.False(check.GetProperty("ok").GetBoolean());
        Assert.False(check.GetProperty("stats").TryGetProperty("uvs", out _));

        // A partly mapped mesh: the textured faces keep the new warning in place of the generic one, the rest get the generic one.
        Ok(await carve.CallToolAsync("project_uvs", """{"method":"box","faces":[0,1]}"""));
        var partly = Strings(Body(Ok(await carve.CallToolAsync("check", "{}"))).GetProperty("warnings"));
        Assert.Equal(1, partly.Count(w => w.StartsWith("faces[4]:")));
        Assert.Equal($"faces[4]: {text}", partly.Single(w => w.StartsWith("faces[4]:")));
        Assert.DoesNotContain(partly, w => w.StartsWith("faces[1]:") && w.Contains("no UVs"));
        Assert.Equal("faces[2]: no UVs, so the face samples the texture's corner", partly.Single(w => w.StartsWith("faces[2]:")));
    }

    [Fact]
    public async Task Occlusion_strength_without_a_map_warns_in_define_material_and_in_check_but_not_with_one()
    {
        Texture("ao.png");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var defined = Body(Ok(await carve.CallToolAsync("define_material", """{"name":"m","occlusion":0.5}""")));
        Assert.StartsWith("occlusion: 0.5 has no effect without an occlusion_texture", Strings(defined.GetProperty("warnings")).Single());
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[0]}"""));
        var warnings = Strings(Body(Ok(await carve.CallToolAsync("check", "{}"))).GetProperty("warnings"));
        Assert.Equal("materials['m']: occlusion is 0.5 but the material has no occlusion_texture, so it has no effect (glTF scales only the map)", warnings.Single());

        Ok(await carve.CallToolAsync("define_material", """{"name":"m","occlusion":0.5,"occlusion_texture":"ao.png"}"""));
        // The strength now counts; the face is only warned of as textured, having no UVs.
        Assert.DoesNotContain(Strings(Body(Ok(await carve.CallToolAsync("check", "{}"))).GetProperty("warnings")), w => w.StartsWith("materials["));
    }

    [Fact]
    public async Task Replace_colour_and_material_clear_each_other_and_undo_takes_each_step_back()
    {
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"steel","metallic":1,"roughness":0.2}"""));
        var again = Body(Ok(await carve.CallToolAsync("define_material", """{"name":"steel","metallic":0.5}""")));
        Assert.True(again.GetProperty("replaced").GetBoolean());
        Assert.Equal(1, Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("materials").GetArrayLength());

        Ok(await carve.CallToolAsync("set_colour", """{"colour":"#C8503C","faces":[0,1]}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"steel","faces":[1,2]}"""));
        var mesh = Body(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Equal([0], mesh.GetProperty("colours").EnumerateArray().Single().GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()));
        Assert.Equal([1, 2], mesh.GetProperty("face_materials").EnumerateArray().Single().GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()));

        Ok(await carve.CallToolAsync("set_colour", """{"colour":"#3C78C8","faces":[1]}"""));
        mesh = Body(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Equal([2], mesh.GetProperty("face_materials").EnumerateArray().Single().GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()));

        Assert.Contains("\"undone\":\"set_colour\"", Ok(await carve.CallToolAsync("undo", "{}")));
        mesh = Body(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Equal([1, 2], mesh.GetProperty("face_materials").EnumerateArray().Single().GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()));
        Assert.Contains("\"undone\":\"set_material\"", Ok(await carve.CallToolAsync("undo", "{}")));
        mesh = Body(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.False(mesh.TryGetProperty("face_materials", out _));
        Assert.Contains("\"undone\":\"set_colour\"", Ok(await carve.CallToolAsync("undo", "{}")));
        Assert.Contains("\"undone\":\"define_material\"", Ok(await carve.CallToolAsync("undo", "{}")));
        Assert.Contains("\"undone\":\"define_material\"", Ok(await carve.CallToolAsync("undo", "{}")));
        Assert.False(Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).TryGetProperty("materials", out _));
    }

    [Fact]
    public async Task A_model_without_materials_reads_back_exactly_as_before()
    {
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var mesh = Body(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.False(mesh.TryGetProperty("materials", out _));
        Assert.False(mesh.TryGetProperty("face_materials", out _));
    }

    [Theory]
    [InlineData("define_material", """{"name":"m","colour":"#FFFFFF"}""", "colour: not an argument of define_material; its arguments are name, base_colour, metallic, roughness, emissive, occlusion, double_sided, base_colour_texture, metallic_roughness_texture, normal_texture, emissive_texture, occlusion_texture")]
    [InlineData("set_material", """{"material":"m","faces":[0],"colour":"#FFFFFF"}""", "colour: not an argument of set_material; its arguments are material, faces, group, mesh")]
    [InlineData("define_material", """{"name":"m","roughness":2}""", "roughness: roughness is 2; it is a number from 0 to 1")]
    [InlineData("define_material", """{"name":"m","base_colour_texture":"missing.png"}""", "base_colour_texture: no file at ")]
    [InlineData("define_material", """{"name":"m","base_colour_texture":"x.jpg"}""", "define_material reads .png files")]
    [InlineData("set_material", """{"material":"nope","faces":[0]}""", "material: no material named 'nope'; the model defines none yet")]
    public async Task Refusals_name_the_argument_and_change_nothing(string tool, string arguments, string expected)
    {
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Assert.Contains(expected, Refused(await carve.CallToolAsync(tool, arguments)));
        Assert.False(Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).TryGetProperty("materials", out _));
    }

    [Fact]
    public async Task A_texture_that_is_missing_lists_the_pngs_beside_where_it_looked()
    {
        Texture("tex/one.png");
        Texture("tex/two.png");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var error = Refused(await carve.CallToolAsync("define_material", """{"name":"m","normal_texture":"tex/three.png"}"""));
        Assert.Contains("normal_texture: no file at " + Path.Combine(Root, "tex", "three.png"), error);
        Assert.Contains($"the .png files in {Path.Combine(Root, "tex")} (2) are: one.png, two.png", error);
    }

    [Fact]
    public async Task A_file_that_is_not_a_PNG_is_refused_at_its_argument()
    {
        File.WriteAllText(Path.Combine(Root, "fake.png"), "hello");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var error = Refused(await carve.CallToolAsync("define_material", """{"name":"m","emissive_texture":"fake.png"}"""));
        Assert.Contains("emissive_texture: " + Path.Combine(Root, "fake.png"), error);
        Assert.Contains("PNG", error);
    }

    [Fact]
    public async Task An_unknown_material_lists_the_defined_ones()
    {
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"a"}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"b"}"""));
        Assert.Contains("material: no material named 'c'; the materials are 'a', 'b'", Refused(await carve.CallToolAsync("set_material", """{"material":"c","faces":[0]}""")));
    }

    [Fact]
    public async Task Set_material_by_group_reads_back_and_a_face_that_is_not_there_is_refused()
    {
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"a"}"""));
        Ok(await carve.CallToolAsync("define_group", """{"name":"sides","faces":[0,1,2,3]}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"a","group":"sides"}"""));
        var faces = Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("face_materials").EnumerateArray().Single().GetProperty("faces");
        Assert.Equal([0, 1, 2, 3], faces.EnumerateArray().Select(f => f.GetInt32()));
        Assert.Contains("faces[9]: ", Refused(await carve.CallToolAsync("set_material", """{"material":"a","faces":[9]}""")));
        Assert.Contains("group: no group named 'top'", Refused(await carve.CallToolAsync("set_material", """{"material":"a","group":"top"}""")));
    }

    [Fact]
    public async Task Save_and_open_round_trip_materials_with_a_texture_in_a_subfolder_through_a_new_process()
    {
        var png = Path.GetFullPath(Texture("textures/wood/oak.png"));
        string before;
        {
            var (carve, _) = await StartAsync();
            using var scope = carve;
            Ok(await carve.CallToolAsync("define_material", """{"name":"oak","roughness":0.7,"base_colour_texture":"textures/wood/oak.png","double_sided":true}"""));
            Ok(await carve.CallToolAsync("set_material", """{"material":"oak","faces":[0,5]}"""));
            Ok(await carve.CallToolAsync("set_colour", """{"colour":"#3C78C8","faces":[1]}"""));
            Assert.Contains("saved model.carve", Ok(await carve.CallToolAsync("save", """{"path":"model.carve"}""")));
            before = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        }

        var file = File.ReadAllText(Path.Combine(Root, "model.carve"));
        Assert.Contains("\"base_colour_texture\": \"textures/wood/oak.png\"", file);

        {
            var (carve, _) = await StartAsync(mesh: null);
            using var scope = carve;
            Ok(await carve.CallToolAsync("open", """{"path":"model.carve"}"""));
            Assert.Equal(before, Ok(await carve.CallToolAsync("get_mesh", "{}")));
            var texture = Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("materials")[0].GetProperty("base_colour_texture");
            Assert.Equal(png, texture.GetProperty("path").GetString());

            // Saved into another folder the texture path is written relative to that folder.
            Ok(await carve.CallToolAsync("save", """{"path":"out/again.carve"}"""));
            Assert.Contains("\"base_colour_texture\": \"../textures/wood/oak.png\"", File.ReadAllText(Path.Combine(Root, "out", "again.carve")));
            Ok(await carve.CallToolAsync("open", """{"path":"out/again.carve"}"""));
            Assert.Equal(before, Ok(await carve.CallToolAsync("get_mesh", "{}")));
        }
    }

    [Fact]
    public async Task Open_refuses_a_file_whose_texture_is_gone_naming_the_material_and_field_and_keeps_the_model()
    {
        var png = Texture("tex/oak.png");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"oak","base_colour_texture":"tex/oak.png"}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"model.carve"}"""));
        File.Delete(png);
        var error = Refused(await carve.CallToolAsync("open", """{"path":"model.carve"}"""));
        Assert.Contains("materials[0].base_colour_texture: cannot read 'tex/oak.png'", error);
        Assert.Contains("the model is unchanged", error);
    }

    [Fact]
    public async Task Save_with_mesh_writes_only_the_materials_that_mesh_uses_and_open_with_mesh_refuses_a_different_definition()
    {
        var (carve, tools) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"red","base_colour":"#C8503C"}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"unused","base_colour":"#00FF00"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"red","faces":[0]}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"piece.carve","mesh":"mesh"}"""));
        var file = File.ReadAllText(Path.Combine(Root, "piece.carve"));
        Assert.Contains("\"name\": \"red\"", file);
        Assert.DoesNotContain("unused", file);

        // The model defines 'red' differently: open with mesh refuses and names it; the same definition is kept.
        Ok(await carve.CallToolAsync("define_material", """{"name":"red","base_colour":"#0000FF"}"""));
        var refusal = Refused(await carve.CallToolAsync("open", """{"path":"piece.carve","mesh":"mesh"}"""));
        Assert.Contains("uses the material 'red', which the model defines differently", refusal);
        Ok(await carve.CallToolAsync("define_material", """{"name":"red","base_colour":"#C8503C"}"""));
        Ok(await carve.CallToolAsync("open", """{"path":"piece.carve","mesh":"mesh"}"""));
        Assert.Equal(["red", "unused"], Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("materials").EnumerateArray().Select(m => m.GetProperty("name").GetString()!));
    }

    [Fact]
    public async Task An_emissive_texture_turns_the_default_emissive_white_unless_a_colour_is_given()
    {
        Texture("glow.png");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        string Emissive(string json) => Body(json).GetProperty("material").GetProperty("emissive").GetString()!;
        Assert.Equal("#FFFFFF", Emissive(Ok(await carve.CallToolAsync("define_material", """{"name":"a","emissive_texture":"glow.png"}"""))));
        Assert.Equal("#000000", Emissive(Ok(await carve.CallToolAsync("define_material", """{"name":"b","emissive":"#000000","emissive_texture":"glow.png"}"""))));
        Assert.Equal("#102030", Emissive(Ok(await carve.CallToolAsync("define_material", """{"name":"c","emissive":"#102030","emissive_texture":"glow.png"}"""))));
        Assert.Equal("#000000", Emissive(Ok(await carve.CallToolAsync("define_material", """{"name":"d"}"""))));
    }
}
