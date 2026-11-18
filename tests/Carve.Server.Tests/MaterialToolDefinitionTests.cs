using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public class MaterialToolDefinitionTests : TempFolderTests
{
    private static string Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync(string? mesh = "cube")
    {
        var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (mesh is not null) Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh == "cube" ? Cube(tools) : mesh)));
        return (carve, tools);
    }

    public MaterialToolDefinitionTests() : base("material")
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
        // ... and nothing else, as the example says (RV-47).
        Assert.Equal(new[] { 0, 1, 4, 5 }.Select(f => $"faces[{f}]: no UVs, so the face samples the texture's corner"), warnings);
        Assert.Contains("check warns only of the other four faces, 0, 1, 4 and 5", Description(tools, "set_material"));
    }
}
