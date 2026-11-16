using System.Buffers.Binary;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>Small PNGs made in code: the signature, an IHDR of the given size and an IEND. The Core never decodes them.</summary>
internal static class TestPngs
{
    public static byte[] Png(uint width, uint height, string firstChunk = "IHDR", byte fill = 0)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; ihdr[9] = 2; ihdr[12] = fill;
        Chunk(bytes, firstChunk, ihdr);
        Chunk(bytes, "IEND", []);
        return [.. bytes];
    }

    private static void Chunk(List<byte> bytes, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        bytes.AddRange(length);
        bytes.AddRange(type.Select(c => (byte)c));
        bytes.AddRange(data);
        bytes.AddRange(new byte[4]); // the CRC, which the Core does not read
    }

    public static string AbsolutePath(string name) => Path.Combine(Path.GetTempPath(), "carve-t74", name);

    public static MaterialTexture Texture(string name = "wood.png", uint width = 4, uint height = 2, byte fill = 0) =>
        MaterialTexture.FromPng(AbsolutePath(name), Png(width, height, fill: fill), out var problem) ?? throw new InvalidOperationException(problem);
}

/// <summary>The material record (T-74): its defaults, each validation problem, each name rule and texture equality.</summary>
[Trait("Tier", "unit")]
public class MaterialTests
{
    [Fact]
    public void Every_field_but_the_name_defaults_to_glTFs_value()
    {
        var m = new Material("wood");
        Assert.Equal("#FFFFFF", m.BaseColour);
        Assert.Equal(0f, m.Metallic);
        Assert.Equal(1f, m.Roughness);
        Assert.Equal("#000000", m.Emissive);
        Assert.Equal(1f, m.Occlusion);
        Assert.False(m.DoubleSided);
        Assert.All(m.Textures, t => Assert.Null(t.Texture));
        Assert.Equal(["base_colour_texture", "metallic_roughness_texture", "normal_texture", "emissive_texture", "occlusion_texture"],
            m.Textures.Select(t => t.Field));
        Assert.Empty(Material.Problems(m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b")]
    [InlineData("barrel:lid")]
    [InlineData("carve #c8503c")]
    [InlineData("#wood")]
    [InlineData("carve")]
    public void A_name_that_could_be_an_exported_colour_materials_is_refused(string name)
    {
        Assert.NotNull(Material.NameProblem(name));
        Assert.Contains(Material.Problems(new Material(name)), p => p.Field == "name");
    }

    [Theory]
    [InlineData("wood")]
    [InlineData("Carve")] // the rule is ordinal: only the exact name an export uses is taken
    [InlineData("carved")]
    [InlineData("barrel")] // a mesh's name is allowed (the person's ruling, 2026-11-16)
    [InlineData("rusty metal 2")]
    public void Any_other_name_is_allowed(string name) => Assert.Null(Material.NameProblem(name));

    [Fact]
    public void A_null_name_is_refused() => Assert.NotNull(Material.NameProblem(null));

    [Theory]
    [InlineData("#C8503")]
    [InlineData("red")]
    [InlineData("")]
    public void A_bad_hex_in_either_colour_is_refused_as_a_face_colour_is(string hex)
    {
        Assert.Equal([("base_colour", FaceStyle.ColourProblem(hex)!)], Material.Problems(new Material("m", BaseColour: hex)));
        Assert.Equal([("emissive", FaceStyle.ColourProblem(hex)!)], Material.Problems(new Material("m", Emissive: hex)));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void A_factor_outside_0_to_1_is_refused(float value)
    {
        Assert.Equal(["metallic"], Material.Problems(new Material("m", Metallic: value)).Select(p => p.Field));
        Assert.Equal(["roughness"], Material.Problems(new Material("m", Roughness: value)).Select(p => p.Field));
        Assert.Equal(["occlusion"], Material.Problems(new Material("m", Occlusion: value)).Select(p => p.Field));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(0.5f)]
    public void A_factor_from_0_to_1_is_allowed(float value) =>
        Assert.Empty(Material.Problems(new Material("m", Metallic: value, Roughness: value, Occlusion: value)));

    [Fact]
    public void A_texture_reads_its_size_from_IHDR()
    {
        var t = TestPngs.Texture(width: 64, height: 32);
        Assert.Equal((64, 32), (t.Width, t.Height));
        Assert.Equal(TestPngs.Png(64, 32), t.Png.ToArray());
    }

    [Theory]
    [InlineData(1u, 1u)]
    [InlineData(4096u, 4096u)]
    [InlineData(4096u, 1u)]
    public void A_texture_of_1_to_4096_on_each_side_is_allowed(uint w, uint h) =>
        Assert.Null(MaterialTexture.PngProblem(TestPngs.Png(w, h)));

    [Theory]
    [InlineData(0u, 4u)]
    [InlineData(4u, 0u)]
    [InlineData(4097u, 4u)]
    [InlineData(4u, 4097u)]
    public void A_texture_of_another_size_is_refused(uint w, uint h)
    {
        Assert.Equal($"the PNG is {w} x {h}; a texture is 1 to 4096 pixels on each side", MaterialTexture.PngProblem(TestPngs.Png(w, h)));
        Assert.Null(MaterialTexture.FromPng(TestPngs.AbsolutePath("x.png"), TestPngs.Png(w, h), out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void Bytes_that_are_not_a_PNG_are_refused()
    {
        Assert.Equal("the file is not a PNG: it does not start with the PNG signature", MaterialTexture.PngProblem("GIF89a not a png at all"u8));
        Assert.Equal("the file is not a PNG: it does not start with the PNG signature", MaterialTexture.PngProblem([]));
        Assert.Equal("the PNG is malformed: its first chunk is not IHDR", MaterialTexture.PngProblem(TestPngs.Png(4, 4, "IDAT")));
        Assert.Equal("the PNG is malformed: its first chunk is not IHDR", MaterialTexture.PngProblem(TestPngs.Png(4, 4).AsSpan(0, 20)));
    }

    [Fact]
    public void A_texture_path_must_be_absolute()
    {
        Assert.Null(MaterialTexture.FromPng("tex/wood.png", TestPngs.Png(4, 4), out var problem));
        Assert.Equal("'tex/wood.png' is not an absolute path", problem);
    }

    [Fact]
    public void A_bad_texture_is_a_problem_of_its_field()
    {
        var bad = new MaterialTexture(TestPngs.AbsolutePath("n.png"), [.. TestPngs.Png(4097, 4)], 4097, 4);
        Assert.Equal(["normal_texture"], Material.Problems(new Material("m", NormalTexture: bad)).Select(p => p.Field));
    }

    [Fact]
    public void Textures_are_equal_by_path_size_and_bytes()
    {
        Assert.Equal(TestPngs.Texture(), TestPngs.Texture());
        Assert.Equal(new Material("m", BaseColourTexture: TestPngs.Texture()), new Material("m", BaseColourTexture: TestPngs.Texture()));
        Assert.NotEqual(TestPngs.Texture(), TestPngs.Texture(fill: 1));
        Assert.NotEqual(TestPngs.Texture(), TestPngs.Texture("other.png"));
    }

    [Fact]
    public void Load_reads_the_file_once_and_holds_its_bytes()
    {
        var path = TestPngs.AbsolutePath(Guid.NewGuid().ToString("N") + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, TestPngs.Png(8, 8));
        try
        {
            var t = MaterialTexture.Load(path, out var problem)!;
            Assert.Null(problem);
            File.WriteAllBytes(path, TestPngs.Png(16, 16)); // a change on disk does not change the texture already read
            Assert.Equal(TestPngs.Png(8, 8), t.Png.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
        Assert.Null(MaterialTexture.Load(path, out var missing));
        Assert.NotNull(missing);
    }
}
