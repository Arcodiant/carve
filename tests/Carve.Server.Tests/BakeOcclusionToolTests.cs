extern alias carve;
using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>T-124: <c>bake</c>'s <c>occlusion</c> and <c>orm</c> maps and its <c>occlusion</c> block on the unit harness.</summary>
[Trait("Tier", "unit")]
public class BakeOcclusionToolTests : FileToolTests
{
    public BakeOcclusionToolTests() : base("bake")
    {
    }

    private static JsonElement Body(string text) => JsonDocument.Parse(text).RootElement;

    /// <summary>A lid 0.2 over the strip, wider than it.</summary>
    private const string Lid =
        """{"name":"lid","vertices":[[-1,-1,0.2],[5,-1,0.2],[5,2,0.2],[-1,2,0.2]],"faces":[[0,1,2,3]]}""";

    private async Task<ToolHarness> StartStripAsync(bool lid = false)
    {
        var (carve, _) = await StartAsync(BakeToolTests.Strip);
        try
        {
            Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
            if (lid) Ok(await carve.CallToolAsync("define_mesh", Lid));
            return carve;
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    private byte[] Read(string path) => File.ReadAllBytes(Path.Combine(Root, path));

    private ImageResult Image(string path, ColorComponents components) => ImageResult.FromMemory(Read(path), components);

    /// <summary>Covered texels of the strip at 64: the middle row of each face's cell.</summary>
    private static readonly (int X, int Y)[] Centres = [(8, 32), (24, 32), (40, 32), (56, 32)];

    [Fact]
    public async Task A_lone_strip_reads_open_and_the_orm_carries_it_with_each_faces_roughness_and_metallic()
    {
        await using var carve = await StartStripAsync();
        Ok(await carve.CallToolAsync("define_group", """{"name":"a","faces":[0,1]}"""));
        Ok(await carve.CallToolAsync("define_group", """{"name":"b","faces":[1]}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"m","roughness":0.25,"metallic":0.5}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[2]}"""));

        // Face 0: a's 0.88 / 0. Face 1: b's roughness wins over a's, a's metallic stays. Face 2: the material's. Face 3: 1 and 0.
        var result = Body(Ok(await carve.CallToolAsync("bake",
            """{"size":64,"padding":0,"maps":["occlusion","orm"],"occlusion":{"reach":0.5},"surface":{"a":{"roughness":0.88,"metallic":0},"b":{"roughness":0.36}}}""")));
        var files = result.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(["occlusion", "orm"], files.Select(f => f.GetProperty("map").GetString()));
        Assert.Equal("textures/strip.occlusion.png", BakeToolTests.Entry(result, "occlusion").GetProperty("path").GetString());
        Assert.Equal("textures/strip.orm.png", BakeToolTests.Entry(result, "orm").GetProperty("path").GetString());
        Assert.All(files, f => Assert.True(f.GetProperty("ms").GetInt64() >= 0));
        Assert.Equal(1, BakeToolTests.Entry(result, "occlusion").GetProperty("min").GetDouble());

        var grey = Image("textures/strip.occlusion.png", ColorComponents.Grey);
        Assert.Equal(ColorComponents.Grey, grey.SourceComp);
        var orm = Image("textures/strip.orm.png", ColorComponents.RedGreenBlue);
        Assert.Equal(ColorComponents.RedGreenBlue, orm.SourceComp);
        byte[][] want = [[255, 224, 0], [255, 92, 0], [255, 64, 128], [255, 255, 0]];
        for (var f = 0; f < 4; f++)
        {
            var (x, y) = Centres[f];
            Assert.Equal(255, grey.Data[y * 64 + x]);
            Assert.Equal(want[f], orm.Data.AsSpan((y * 64 + x) * 3, 3).ToArray());
        }
        Assert.Equal(new byte[3], orm.Data.AsSpan(0, 3).ToArray()); // uncovered, no padding
    }

    [Fact]
    public async Task A_lid_darkens_the_strip_unless_occluders_leave_it_out_and_two_bakes_are_byte_identical()
    {
        await using var carve = await StartStripAsync(lid: true);
        const string Covered = """{"mesh":"strip","size":64,"maps":["occlusion"],"occlusion":{"reach":1,"rays":16}}""";
        var lidded = Body(Ok(await carve.CallToolAsync("bake", Covered)));
        var first = Read("textures/strip.occlusion.png");
        Ok(await carve.CallToolAsync("bake", Covered));
        Assert.Equal(first, Read("textures/strip.occlusion.png"));
        Assert.True(BakeToolTests.Entry(lidded, "occlusion").GetProperty("max").GetDouble() < 0.5);

        var alone = Body(Ok(await carve.CallToolAsync("bake", """{"mesh":"strip","size":64,"maps":["occlusion"],"occlusion":{"reach":1,"occluders":["strip"]}}""")));
        Assert.Equal(1, BakeToolTests.Entry(alone, "occlusion").GetProperty("min").GetDouble());
    }

    [Theory]
    [InlineData("""{"size":64,"maps":["orm"]}""", "occlusion.reach: required for the occlusion and orm maps")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"rays":8}}""", "occlusion.reach: required")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":0}}""", "occlusion.reach: must be over 0 metres, not 0")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":"far"}}""", "occlusion.reach:")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":1,"rays":0}}""", "occlusion.rays: must be a whole number from 1 to 1024")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":1,"rays":1025}}""", "occlusion.rays: must be a whole number from 1 to 1024")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":1,"rays":2.5}}""", "occlusion.rays:")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":1,"occluders":["ghost"]}}""", "occlusion.occluders[0]: 'ghost' is not a mesh; the meshes are 'strip'")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":1,"occluders":[]}}""", "occlusion.occluders: is empty")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":1,"occluders":"strip"}}""", "occlusion.occluders: expected an array")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":{"reach":1,"spread":2}}""", "occlusion: 'spread' is not an occlusion field; the fields are 'rays', 'reach', 'occluders'")]
    [InlineData("""{"size":64,"maps":["occlusion"],"occlusion":[1]}""", "occlusion: expected {rays, reach, occluders}")]
    [InlineData("""{"size":64,"maps":["base_colour"],"occlusion":{"reach":-1}}""", "occlusion.reach: must be over 0 metres")]
    public async Task Each_occlusion_error_is_refused_and_writes_nothing(string args, string message)
    {
        await using var carve = await StartStripAsync();
        Assert.Contains(message, Refused(await carve.CallToolAsync("bake", args)));
        Assert.False(Directory.Exists(Path.Combine(Root, "textures")));
    }

    [Fact]
    public async Task The_occlusion_block_without_a_map_that_reads_it_bakes_what_was_asked_and_warns()
    {
        await using var carve = await StartStripAsync();
        var result = Body(Ok(await carve.CallToolAsync("bake", """{"size":64,"occlusion":{"reach":1}}""")));
        Assert.DoesNotContain(result.GetProperty("files").EnumerateArray(), f => f.GetProperty("map").GetString() is "occlusion" or "orm");
        Assert.Contains(result.GetProperty("files").EnumerateArray(), f => f.GetProperty("map").GetString() == "base_colour");
        Assert.Contains("occlusion: ignored, neither 'occlusion' nor 'orm' is in maps",
            result.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()));

        // With a map that reads it, no such warning.
        var read = Body(Ok(await carve.CallToolAsync("bake", """{"size":64,"maps":["orm"],"occlusion":{"reach":1}}""")));
        Assert.DoesNotContain(read.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith("occlusion:"));
    }

    [Fact]
    public async Task Define_material_takes_the_orm_in_both_slots()
    {
        await using var carve = await StartStripAsync();
        Ok(await carve.CallToolAsync("bake", """{"size":64,"maps":["orm"],"occlusion":{"reach":0.5}}"""));
        Ok(await carve.CallToolAsync("define_material",
            """{"name":"m","metallic_roughness_texture":"textures/strip.orm.png","occlusion_texture":"textures/strip.orm.png","occlusion":1}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[0]}"""));
        var material = Body(Ok(await carve.CallToolAsync("get_mesh", """{}""")));
        Assert.Contains("strip.orm.png", material.GetRawText());
    }
}
