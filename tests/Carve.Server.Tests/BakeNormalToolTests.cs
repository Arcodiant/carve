extern alias carve;
using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>T-125: <c>bake</c>'s <c>normal</c> map and its <c>bevel</c> block on the unit harness: the file, flat where nothing bends, the box's edges leaning the right way, and each refusal.</summary>
[Trait("Tier", "unit")]
public class BakeNormalToolTests : FileToolTests
{
    public BakeNormalToolTests() : base("bake-normal")
    {
    }

    private static JsonElement Body(string text) => JsonDocument.Parse(text).RootElement;

    private async Task<ToolHarness> BoxAsync()
    {
        var (carve, _) = await StartAsync(MaterialRenderTests.Box("box", 0));
        Ok(await carve.CallToolAsync("set_uvs", MaterialRenderTests.FrontUvs("box")));
        return carve;
    }

    private ImageResult Image(string path) => ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(Root, path)), ColorComponents.RedGreenBlue);

    private static (int R, int G, int B) Pixel(ImageResult image, int x, int y)
    {
        var at = (y * image.Width + x) * 3;
        return (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
    }

    [Fact]
    public async Task The_box_s_front_face_rounds_toward_the_top_and_the_side_and_is_flat_in_the_middle()
    {
        using var carve = await BoxAsync();
        var result = Body(Ok(await carve.CallToolAsync("bake", """{"size":64,"padding":0,"maps":["normal"],"bevel":{"width":0.1}}""")));
        var file = Assert.Single(result.GetProperty("files").EnumerateArray());
        Assert.Equal("normal", file.GetProperty("map").GetString());
        Assert.Equal("textures/box.normal.png", file.GetProperty("path").GetString());
        Assert.Equal(3, file.GetProperty("mean").GetArrayLength());
        Assert.Equal([0, 2, 3, 4, 5], result.GetProperty("skipped_faces").EnumerateArray().Select(f => f.GetInt32()));

        var image = Image("textures/box.normal.png");
        var middle = Pixel(image, 32, 32);
        Assert.True(Math.Abs(middle.R - 128) <= 1 && Math.Abs(middle.G - 128) <= 1 && middle.B >= 254, $"middle {middle}");
        var top = Pixel(image, 32, 0); // v = 0 is the top edge, y = 1: leans toward +y, which is +G
        Assert.True(top.G > 200 && Math.Abs(top.R - 128) <= 1, $"top {top}");
        var side = Pixel(image, 63, 32); // u = 1 is the +X edge: leans toward +x, which is +R
        Assert.True(side.R > 200 && Math.Abs(side.G - 128) <= 1, $"side {side}");
        var bottom = Pixel(image, 32, 63);
        Assert.True(bottom.G < 55, $"bottom {bottom}");

        // Two bakes are byte-identical.
        var first = File.ReadAllBytes(Path.Combine(Root, "textures/box.normal.png"));
        Ok(await carve.CallToolAsync("bake", """{"size":64,"padding":0,"maps":["normal"],"bevel":{"width":0.1}}"""));
        Assert.Equal(first, File.ReadAllBytes(Path.Combine(Root, "textures/box.normal.png")));
    }

    [Fact]
    public async Task A_flat_strip_has_no_break_so_every_texel_is_flat_and_maps_left_out_bake_no_normal()
    {
        var (carve, _) = await StartAsync("""{"name":"strip","vertices":[[0,0,0],[1,0,0],[2,0,0],[0,1,0],[1,1,0],[2,1,0]],"faces":[[0,1,4,3],[1,2,5,4]]}""");
        using var scope = carve;
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,0.75],[0.5,0.75],[0.5,0.25],[0,0.25]]},{"face":1,"uv":[[0.5,0.75],[1,0.75],[1,0.25],[0.5,0.25]]}]}"""));
        Ok(await carve.CallToolAsync("bake", """{"size":64,"maps":["normal"],"bevel":{"width":0.5}}"""));
        var image = Image("textures/strip.normal.png");
        for (var y = 16; y < 48; y++) // the islands' rows; beyond the padding the image is black
        for (var x = 0; x < 64; x++)
        {
            var p = Pixel(image, x, y);
            Assert.True(Math.Abs(p.R - 128) <= 1 && Math.Abs(p.G - 128) <= 1 && p.B >= 254, $"({x}, {y}) {p}");
        }

        var plain = Body(Ok(await carve.CallToolAsync("bake", """{"size":64}""")));
        Assert.DoesNotContain(plain.GetProperty("files").EnumerateArray(), f => f.GetProperty("map").GetString() == "normal");
    }

    [Theory]
    [InlineData("""{"maps":["normal"]}""", "bevel: required with map 'normal'")]
    [InlineData("""{"maps":["normal"],"bevel":{}}""", "bevel.width: required")]
    [InlineData("""{"maps":["normal"],"bevel":0.1}""", "bevel: expected {width}")]
    [InlineData("""{"maps":["normal"],"bevel":{"width":0}}""", "bevel.width: must be more than 0 metres, not 0")]
    [InlineData("""{"maps":["normal"],"bevel":{"width":-0.5}}""", "bevel.width: must be more than 0 metres, not -0.5")]
    [InlineData("""{"maps":["normal"],"bevel":{"width":"wide"}}""", "bevel.width: expected a number of metres")]
    [InlineData("""{"maps":["normal"],"bevel":{"width":0.1,"radius":2}}""", "bevel.radius: not a field of bevel")]
    [InlineData("""{"maps":["masks"],"bevel":{"width":0.1}}""", "bevel: only with map 'normal'")]
    public async Task A_bad_bevel_is_refused_by_name_and_writes_nothing(string arguments, string expected)
    {
        using var carve = await BoxAsync();
        var text = Refused(await carve.CallToolAsync("bake", arguments));
        Assert.Contains(expected, text);
        Assert.StartsWith("bake failed with", text);
        Assert.False(Directory.Exists(Path.Combine(Root, "textures")));
    }

    [Fact]
    public async Task The_description_says_what_the_map_rounds_its_frame_and_where_it_goes()
    {
        var (carve, tools) = await StartAsync(null);
        using var scope = carve;
        var bake = (tools.ValueKind == JsonValueKind.Array ? tools : tools.GetProperty("tools")).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "bake");
        var text = bake.GetProperty("description").GetString()!;
        Assert.Contains("'normal' writes <folder>/<mesh>.normal.png", text);
        Assert.Contains("green up", text);
        Assert.Contains("normal_texture", text);
        Assert.Contains("normal", bake.GetProperty("inputSchema").GetProperty("properties").GetProperty("maps").GetProperty("items").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.True(bake.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("bevel", out _));
    }
}
