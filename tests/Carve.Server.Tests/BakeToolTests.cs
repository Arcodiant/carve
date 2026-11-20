extern alias carve;
using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>T-122: <c>bake</c> on the unit harness: the files, the masks and base colour, the order colours resolve in, the errors and the hand-off to define_material.</summary>
[Trait("Tier", "unit")]
public class BakeToolTests : FileToolTests
{
    public BakeToolTests() : base("bake")
    {
    }

    private static JsonElement Body(string text) => JsonDocument.Parse(text).RootElement;

    /// <summary>
    /// Four quads in a strip, face i mapped to the cell [i/4, (i+1)/4] x [1/4, 3/4]: at 64 a cell is 16 texels wide and 32 high,
    /// islands that touch but never overlap.
    /// </summary>
    private const string Strip =
        """{"name":"strip","vertices":[[0,0,0],[1,0,0],[2,0,0],[3,0,0],[4,0,0],[0,1,0],[1,1,0],[2,1,0],[3,1,0],[4,1,0]],"faces":[[0,1,6,5],[1,2,7,6],[2,3,8,7],[3,4,9,8]]}""";

    private static string StripUvs()
    {
        var faces = Enumerable.Range(0, 4).Select(i => $$"""{"face":{{i}},"uv":[[{{i * 0.25}},0.75],[{{(i + 1) * 0.25}},0.75],[{{(i + 1) * 0.25}},0.25],[{{i * 0.25}},0.25]]}""");
        return "{\"faces\":[" + string.Join(",", faces) + "]}";
    }

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartStripAsync()
    {
        var (carve, tools) = await StartAsync(Strip);
        try
        {
            Ok(await carve.CallToolAsync("set_uvs", StripUvs()));
            return (carve, tools);
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    private byte[] Read(string path) => File.ReadAllBytes(Path.Combine(Root, path));

    private ImageResult Image(string path, ColorComponents components) => ImageResult.FromMemory(Read(path), components);

    private static JsonElement Entry(JsonElement result, string map, string? name = null) =>
        result.GetProperty("files").EnumerateArray().Single(f => f.GetProperty("map").GetString() == map && (name is null || f.GetProperty("name").GetString() == name));

    private static (byte R, byte G, byte B) Pixel(ImageResult image, int x, int y)
    {
        var at = (y * image.Width + x) * 3;
        return (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
    }

    [Fact]
    public async Task A_two_group_box_at_64_gives_each_mask_its_texels_exactly_and_padding_to_the_ring()
    {
        var (carve, _) = await StartAsync("""{"name":"box","vertices":[[0,0,0],[1,0,0],[2,0,0],[0,1,0],[1,1,0],[2,1,0]],"faces":[[0,1,4,3],[1,2,5,4]]}""");
        using var scope = carve;
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0.125,0.75],[0.375,0.75],[0.375,0.25],[0.125,0.25]]},{"face":1,"uv":[[0.625,0.75],[0.875,0.75],[0.875,0.25],[0.625,0.25]]}]}"""));
        Ok(await carve.CallToolAsync("define_group", """{"name":"left","faces":[0]}"""));
        Ok(await carve.CallToolAsync("define_group", """{"name":"right","faces":[1]}"""));

        var result = Body(Ok(await carve.CallToolAsync("bake", """{"size":64,"padding":8,"maps":["masks"],"folder":"out"}""")));
        Assert.Equal(["out/box.mask.left.png", "out/box.mask.right.png"], result.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()));
        Assert.Empty(result.GetProperty("skipped_faces").EnumerateArray());

        var left = Entry(result, "masks", "left");
        // T-120's layout covers a face's texels and the ring of texels whose centres lie within sqrt(0.5) of it: 18 x 34 each.
        Assert.Equal(2 * 18 * 34 / 4096.0, left.GetProperty("coverage").GetDouble());
        Assert.Equal(0, left.GetProperty("min").GetDouble());
        Assert.Equal(1, left.GetProperty("max").GetDouble());
        Assert.Equal(0.5, left.GetProperty("mean").GetDouble());
        Assert.False(left.GetProperty("replaced").GetBoolean());

        // Face texels: x 8..23 and 40..55, y 16..47; covered with the clamp ring: x 7..24 and 39..56, y 15..48. Padded: a texel within 8 of the nearer island takes its mask.
        var l = Image("out/box.mask.left.png", ColorComponents.Grey);
        var r = Image("out/box.mask.right.png", ColorComponents.Grey);
        Assert.Equal(ColorComponents.Grey, l.SourceComp);
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++)
        {
            var d0 = Distance(x, y, 7, 24);
            var d1 = Distance(x, y, 39, 56);
            var inLeft = x is >= 7 and <= 24 && y is >= 15 and <= 48;
            var inRight = x is >= 39 and <= 56 && y is >= 15 and <= 48;
            if (inLeft) { Assert.Equal(255, l.Data[y * 64 + x]); Assert.Equal(0, r.Data[y * 64 + x]); }
            else if (inRight) { Assert.Equal(0, l.Data[y * 64 + x]); Assert.Equal(255, r.Data[y * 64 + x]); }
            else if (d0 == d1) continue; // a tie goes to the lowest texel index, not worked here
            else
            {
                Assert.Equal(d0 <= 64 && d0 < d1 ? 255 : 0, l.Data[y * 64 + x]);
                Assert.Equal(d1 <= 64 && d1 < d0 ? 255 : 0, r.Data[y * 64 + x]);
            }
        }

        static int Distance(int x, int y, int x0, int x1)
        {
            var dx = x < x0 ? x0 - x : x > x1 ? x - x1 : 0;
            var dy = y < 15 ? 15 - y : y > 48 ? y - 48 : 0;
            return dx * dx + dy * dy;
        }
    }

    [Fact]
    public async Task Base_colour_is_the_surface_then_the_face_colour_then_the_materials_then_white()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_group", """{"name":"a","faces":[0]}"""));
        Ok(await carve.CallToolAsync("set_colour", """{"colour":"#C8503C","faces":[1]}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"m","base_colour":"#336699"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[2]}"""));

        var result = Body(Ok(await carve.CallToolAsync("bake", """{"size":64,"padding":0,"maps":["base_colour"],"surface":{"a":{"base_colour":"#102030"}}}""")));
        var rgb = Image("textures/strip.base_colour.png", ColorComponents.RedGreenBlue);
        Assert.Equal(ColorComponents.RedGreenBlue, rgb.SourceComp);
        Assert.Equal(((byte)0x10, (byte)0x20, (byte)0x30), Pixel(rgb, 8, 32));
        Assert.Equal(((byte)0xC8, (byte)0x50, (byte)0x3C), Pixel(rgb, 24, 32));
        Assert.Equal(((byte)0x33, (byte)0x66, (byte)0x99), Pixel(rgb, 40, 32));
        Assert.Equal(((byte)255, (byte)255, (byte)255), Pixel(rgb, 56, 32));
        Assert.Equal(64, rgb.Width);

        var entry = Entry(result, "base_colour");
        Assert.Equal(64 * 34 / 4096.0, entry.GetProperty("coverage").GetDouble());
        Assert.Equal(3, entry.GetProperty("min").GetArrayLength());
        Assert.Equal(0x10 / 255.0, entry.GetProperty("min")[0].GetDouble(), 6);
        Assert.Equal(1, entry.GetProperty("max")[2].GetDouble());
    }

    [Fact]
    public async Task A_surface_entry_beats_the_face_colour_and_the_material_and_the_later_entry_wins()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_group", """{"name":"all","faces":[0,1,2,3]}"""));
        Ok(await carve.CallToolAsync("define_group", """{"name":"a","faces":[0,1]}"""));
        Ok(await carve.CallToolAsync("define_group", """{"name":"b","faces":[1]}"""));
        Ok(await carve.CallToolAsync("set_colour", """{"colour":"#C8503C","faces":[2]}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"m","base_colour":"#336699"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[3]}"""));

        // "all" first as the default; a, then b after it on face 1; the material m last, on face 3.
        Ok(await carve.CallToolAsync("bake", """{"size":64,"padding":0,"maps":["base_colour"],"surface":{"all":{"base_colour":"#808080","roughness":0.5},"a":{"base_colour":"#101010"},"b":{"base_colour":"#202020"},"m":{"base_colour":"#303030","metallic":1}}}"""));
        var rgb = Image("textures/strip.base_colour.png", ColorComponents.RedGreenBlue);
        Assert.Equal(((byte)0x10, (byte)0x10, (byte)0x10), Pixel(rgb, 8, 32));
        Assert.Equal(((byte)0x20, (byte)0x20, (byte)0x20), Pixel(rgb, 24, 32));
        Assert.Equal(((byte)0x80, (byte)0x80, (byte)0x80), Pixel(rgb, 40, 32));
        Assert.Equal(((byte)0x30, (byte)0x30, (byte)0x30), Pixel(rgb, 56, 32));
    }

    [Fact]
    public async Task Groups_and_materials_have_their_own_mask_files_and_two_bakes_are_byte_identical()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_group", """{"name":"same","faces":[0]}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"same"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"same","faces":[1,2]}"""));
        var version = carve.Store.Current.Version;

        var first = Body(Ok(await carve.CallToolAsync("bake", """{"size":128,"padding":4}""")));
        var paths = first.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()!).ToArray();
        Assert.Equal(["textures/strip.mask.same.png", "textures/strip.material.same.png", "textures/strip.base_colour.png"], paths);
        Assert.All(first.GetProperty("files").EnumerateArray(), f => Assert.False(f.GetProperty("replaced").GetBoolean()));
        var bytes = paths.Select(Read).ToArray();

        var second = Body(Ok(await carve.CallToolAsync("bake", """{"size":128,"padding":4}""")));
        Assert.All(second.GetProperty("files").EnumerateArray(), f => Assert.True(f.GetProperty("replaced").GetBoolean()));
        for (var i = 0; i < paths.Length; i++) Assert.Equal(bytes[i], Read(paths[i]));

        // A bake changes nothing in the model: no undo step, no new version.
        Assert.Equal(version, carve.Store.Current.Version);
    }

    [Fact]
    public async Task A_baked_png_is_taken_by_define_material_as_it_comes_back_and_get_mesh_reads_it()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        var result = Body(Ok(await carve.CallToolAsync("bake", """{"size":64,"maps":["base_colour"]}""")));
        var path = Entry(result, "base_colour").GetProperty("path").GetString()!;
        var defined = Body(Ok(await carve.CallToolAsync("define_material", $$"""{"name":"baked","base_colour_texture":"{{path}}"}""")));
        Assert.Equal(64, defined.GetProperty("material").GetProperty("base_colour_texture").GetProperty("width").GetInt32());
        Ok(await carve.CallToolAsync("set_material", """{"material":"baked","faces":[0,1,2,3]}"""));
        var mesh = Body(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Equal(64, mesh.GetProperty("materials")[0].GetProperty("base_colour_texture").GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task Unmapped_faces_are_skipped_listed_and_warned()
    {
        var (carve, _) = await StartAsync(Strip);
        using var scope = carve;
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,1],[0.5,1],[0.5,0],[0,0]]}]}"""));
        var result = Body(Ok(await carve.CallToolAsync("bake", """{"size":64,"maps":["base_colour"]}""")));
        Assert.Equal([1, 2, 3], result.GetProperty("skipped_faces").EnumerateArray().Select(f => f.GetInt32()));
        Assert.Contains("have no UVs", result.GetProperty("warnings")[0].GetString());
    }

    [Theory]
    [InlineData("""{"size":100}""", "size must be a power of two from 64 to 4096, not 100")]
    [InlineData("""{"size":64,"padding":33}""", "padding: must be from 0 to 32")]
    [InlineData("""{"padding":-1}""", "padding: must be from 0 to 512")]
    [InlineData("""{"maps":["normals"]}""", "'normals' is not a map; the maps are 'masks', 'base_colour'")]
    [InlineData("""{"maps":[]}""", "maps: is empty")]
    [InlineData("""{"mesh":"nope"}""", "nope")]
    [InlineData("""{"surface":{"ghost":{"base_colour":"#000000"}}}""", "'ghost' is not a group or a material; the names are 'g'")]
    [InlineData("""{"surface":{"g":{"base_colour":"red"}}}""", "is not a colour")]
    [InlineData("""{"surface":{"g":{"roughness":2}}}""", "roughness: must be from 0 to 1")]
    [InlineData("""{"surface":{"g":{"gloss":1}}}""", "'gloss' is not a surface field")]
    [InlineData("""{"folder":""}""", "folder: is empty")]
    public async Task A_bad_argument_is_refused_by_name_and_writes_nothing(string arguments, string expected)
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_group", """{"name":"g","faces":[0]}"""));
        var text = Refused(await carve.CallToolAsync("bake", arguments));
        Assert.Contains(expected, text);
        Assert.StartsWith("bake failed with", text);
        Assert.False(Directory.Exists(Path.Combine(Root, "textures")));
    }

    [Fact]
    public async Task Every_problem_is_listed_at_once()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        var text = Refused(await carve.CallToolAsync("bake", """{"size":64,"padding":99,"maps":["x"],"surface":{"ghost":{}}}"""));
        Assert.StartsWith("bake failed with 3 problem(s)", text);
    }

    [Fact]
    public async Task A_group_name_a_file_cannot_hold_is_refused_naming_it_and_only_when_masks_need_it()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_group", """{"name":"a/b","faces":[0]}"""));
        Assert.Contains("'a/b'", Refused(await carve.CallToolAsync("bake", """{"size":64}""")));
        Ok(await carve.CallToolAsync("bake", """{"size":64,"maps":["base_colour"]}"""));
    }

    [Fact]
    public async Task A_mesh_with_no_uvs_and_a_mesh_with_overlapping_faces_are_refused_as_the_core_words_them()
    {
        var (carve, _) = await StartAsync(Strip);
        using var scope = carve;
        Assert.Contains("the mesh has no UVs, so it has nothing to bake into", Refused(await carve.CallToolAsync("bake", """{"size":64}""")));
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,1],[0.5,1],[0.5,0],[0,0]]},{"face":1,"uv":[[0,1],[0.5,1],[0.5,0],[0,0]]}]}"""));
        Assert.Contains("overlap", Refused(await carve.CallToolAsync("bake", """{"size":64}""")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_model_with_several_meshes_needs_the_mesh_named_and_an_unknown_one_lists_them()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_mesh", Strip.Replace("\"strip\"", "\"second\"")));
        Assert.Contains("required, since the model has 2 meshes", Refused(await carve.CallToolAsync("bake", """{"size":64}""")));
        Assert.Contains("'strip'", Refused(await carve.CallToolAsync("bake", """{"size":64,"mesh":"third"}""")));
        Ok(await carve.CallToolAsync("bake", """{"size":64,"mesh":"strip","maps":["base_colour"]}"""));
    }

    [Fact]
    public async Task The_descriptions_example_bakes_the_wrench_and_its_paths_define_a_material()
    {
        var (carve, tools) = await StartAsync(null);
        using var scope = carve;
        var wrench = Path.Combine(Paths.RepoRoot, "samples", "wrench.carve");
        Ok(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = wrench })));
        var example = Examples(tools, "bake").Single();
        Assert.Equal(carve::Carve.Server.BakeTool.Example, example);
        var result = Body(Ok(await carve.CallToolAsync("bake", example)));

        var files = result.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(["rubber", "steel", "dark", "red", "black", "all"], files.Where(f => f.GetProperty("map").GetString() == "masks").Select(f => f.GetProperty("name").GetString()));
        Assert.Equal(7, files.Length);
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(Root, f.GetProperty("path").GetString()!))));
        var colour = Entry(result, "base_colour");
        Assert.Equal(1024, colour.GetProperty("size").GetInt32());

        // "all" first is the default: dark and black take it, rubber and red their own.
        var rgb = ImageResult.FromMemory(Read(colour.GetProperty("path").GetString()!), ColorComponents.RedGreenBlue);
        var distinct = new HashSet<(byte, byte, byte)>();
        for (var i = 0; i < rgb.Width * rgb.Height; i++) distinct.Add((rgb.Data[i * 3], rgb.Data[i * 3 + 1], rgb.Data[i * 3 + 2]));
        Assert.Contains(((byte)0x80, (byte)0x85, (byte)0x8A), distinct);
        Assert.Contains(((byte)0x28, (byte)0x2A, (byte)0x2E), distinct);
        Assert.Contains(((byte)0xB0, (byte)0x2A, (byte)0x22), distinct);
        Assert.Contains(((byte)0, (byte)0, (byte)0), distinct); // beyond the padding
        Assert.Equal(4, distinct.Count);

        Ok(await carve.CallToolAsync("define_material", """{"name":"wrench","base_colour_texture":"textures/wrench.base_colour.png"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"wrench","group":"all"}"""));
    }

    [Fact]
    public async Task Curvature_writes_a_grey_png_flat_ground_at_128_and_two_bakes_are_byte_identical()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        var args = """{"size":64,"maps":["curvature"],"folder":"out","curvature":{"width":0.5}}""";
        var result = Body(Ok(await carve.CallToolAsync("bake", args)));
        var entry = Assert.Single(result.GetProperty("files").EnumerateArray());
        Assert.Equal("out/strip.curvature.png", entry.GetProperty("path").GetString());
        Assert.False(entry.TryGetProperty("name", out _));
        // A flat strip has no break edge: every covered texel is 0.5, which is 128.
        Assert.Equal(128 / 255.0, entry.GetProperty("min").GetDouble(), 6);
        Assert.Equal(128 / 255.0, entry.GetProperty("max").GetDouble(), 6);
        var first = Read("out/strip.curvature.png");
        var image = Image("out/strip.curvature.png", ColorComponents.Grey);
        Assert.Equal(128, image.Data[32 * 64 + 6]);
        Ok(await carve.CallToolAsync("bake", args));
        Assert.Equal(first, Read("out/strip.curvature.png"));
    }

    [Fact]
    public async Task Curvature_needs_a_width_above_0_and_names_each_problem()
    {
        var (carve, _) = await StartStripAsync();
        using var scope = carve;
        Assert.Contains("needs a curvature block", Refused(await carve.CallToolAsync("bake", """{"size":64,"maps":["curvature"]}""")));
        Assert.Contains("must be greater than 0", Refused(await carve.CallToolAsync("bake", """{"size":64,"maps":["curvature"],"curvature":{"width":0}}""")));
        Assert.Contains("must be greater than 0", Refused(await carve.CallToolAsync("bake", """{"size":64,"maps":["curvature"],"curvature":{"width":-0.1}}""")));
        Assert.Contains("expected a number", Refused(await carve.CallToolAsync("bake", """{"size":64,"maps":["curvature"],"curvature":{"width":"wide"}}""")));
        Assert.Contains("required but missing", Refused(await carve.CallToolAsync("bake", """{"size":64,"maps":["curvature"],"curvature":{}}""")));
        Assert.Contains("'depth' is not a curvature field", Refused(await carve.CallToolAsync("bake", """{"size":64,"maps":["curvature"],"curvature":{"width":1,"depth":2}}""")));
        Assert.Contains("expected {width}", Refused(await carve.CallToolAsync("bake", """{"size":64,"maps":["curvature"],"curvature":3}""")));
        // Not asked for, a good block is harmless and the default maps still need none.
        Ok(await carve.CallToolAsync("bake", """{"size":64,"maps":["masks"],"curvature":{"width":1}}"""));
        Ok(await carve.CallToolAsync("bake", """{"size":64}"""));
    }
}
