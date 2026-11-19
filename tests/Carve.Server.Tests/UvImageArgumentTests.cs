using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-121: the arguments of <c>render</c> <c>mode: "uvs"</c> <c>image</c> on <see cref="ToolHarness"/>. A bad image is refused
/// with the problems of the call before the engine is asked, so the harness (which has none) answers them.
/// </summary>
[Trait("Tier", "unit")]
public sealed class UvImageArgumentTests() : TempFolderTests("carve-uv-image-args")
{
    private static string Text(JsonElement result)
    {
        Assert.True(IsError(result), "expected a tool error");
        return TextOf(result);
    }

    private async Task<string> RenderAsync(string arguments)
    {
        using var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        return Text(await carve.CallToolAsync("render", arguments));
    }

    [Theory]
    [InlineData("""{"image":"a.png"}""", "shaded")]
    [InlineData("""{"mode":"checker","image":"a.png"}""", "checker")]
    [InlineData("""{"mode":"wireframe","image":"a.png"}""", "wireframe")]
    public async Task Image_with_another_mode_is_refused_by_name(string arguments, string mode)
    {
        File.WriteAllBytes(Path.Combine(Root, "a.png"), TestPng.Marker);
        var text = await RenderAsync(arguments);
        Assert.Contains($"image: not with mode '{mode}'", text);
        Assert.Contains("mode 'uvs'", text);
    }

    [Fact]
    public async Task A_missing_file_lists_the_pngs_in_its_folder()
    {
        Directory.CreateDirectory(Path.Combine(Root, "textures"));
        File.WriteAllBytes(Path.Combine(Root, "textures", "wrench.occlusion.png"), TestPng.Marker);
        File.WriteAllBytes(Path.Combine(Root, "textures", "wrench.normal.png"), TestPng.Marker);
        File.WriteAllText(Path.Combine(Root, "textures", "notes.txt"), "not a png");
        var text = await RenderAsync("""{"mode":"uvs","image":"textures/wrench.base.png"}""");
        Assert.Contains("image: no file at ", text);
        Assert.Contains("wrench.base.png", text);
        Assert.Contains("the .png files in ", text);
        Assert.Contains("(2) are: wrench.normal.png, wrench.occlusion.png", text);
        Assert.DoesNotContain("notes.txt", text);
    }

    [Fact]
    public async Task A_file_that_is_not_a_png_and_a_png_over_4096_are_refused_in_define_materials_words()
    {
        File.WriteAllText(Path.Combine(Root, "text.png"), "this is not a PNG at all, just text long enough to hold a header");
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header, 5000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 100);
        header[8] = 8;
        header[9] = 2;
        File.WriteAllBytes(Path.Combine(Root, "huge.png"), TestPng.Write(header, new byte[1]));

        var bad = await RenderAsync("""{"mode":"uvs","image":"text.png"}""");
        Assert.Contains("image: ", bad);
        Assert.Contains("the file is not a PNG: it does not start with the PNG signature", bad);

        var huge = await RenderAsync("""{"mode":"uvs","image":"huge.png"}""");
        Assert.Contains("the PNG is 5000 x 100; a texture is 1 to 4096 pixels on each side", huge);
    }

    [Fact]
    public async Task The_image_problem_is_reported_with_the_calls_other_problems()
    {
        var text = await RenderAsync("""{"mode":"uvs","image":"gone.png","view":"top"}""");
        Assert.Contains("view: not with mode 'uvs'", text);
        Assert.Contains("image: no file at ", text);
        Assert.Contains("2 problem", text);
    }
}
