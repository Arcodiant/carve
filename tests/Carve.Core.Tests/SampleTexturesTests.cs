using System.Numerics;
using Carve.SampleTextures;
using StbImageSharp;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-77: the committed sample textures equal what the generator makes, and the rune sits where the barrel's UVs put it.</summary>
[Trait("Tier", "unit")]
public class SampleTexturesTests
{
    static string TexturesFolder() => Path.Combine(RepoRoot.Find(), "samples", "textures");

    static ImageResult Decode(string path) => ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var (name, _) in TextureSet.Build()) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_committed_png_decodes_to_exactly_the_generators_pixels(string name)
    {
        var expected = TextureSet.Build().Single(t => t.Name == name).Rgba;
        var image = Decode(Path.Combine(TexturesFolder(), name));
        Assert.Equal(TextureSet.Size, image.Width);
        Assert.Equal(TextureSet.Size, image.Height);
        Assert.Equal(expected.Length, image.Data.Length);
        var first = Enumerable.Range(0, expected.Length).FirstOrDefault(i => expected[i] != image.Data[i], -1);
        Assert.True(first < 0, $"{name}: texel ({first / 4 % TextureSet.Size}, {first / 4 / TextureSet.Size}) channel {first % 4} differs");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_texture_is_256_square_opaque_and_under_64_KB(string name)
    {
        var path = Path.Combine(TexturesFolder(), name);
        Assert.True(new FileInfo(path).Length < 64 * 1024, $"{name} is {new FileInfo(path).Length} bytes");
        var image = Decode(path);
        Assert.Equal((256, 256), (image.Width, image.Height));
        for (var i = 3; i < image.Data.Length; i += 4) Assert.Equal(255, image.Data[i]);
    }

    [Fact]
    public void The_generator_is_deterministic()
    {
        var a = TextureSet.Build().ToList();
        var b = TextureSet.Build().ToList();
        Assert.Equal(a.Select(t => t.Name), b.Select(t => t.Name));
        for (var i = 0; i < a.Count; i++) Assert.True(a[i].Rgba.SequenceEqual(b[i].Rgba), a[i].Name);
    }

    [Fact]
    public void The_runes_rectangle_is_the_uv_bounds_of_faces_14_and_22_under_cylindrical()
    {
        var barrel = TestMeshes.Barrel();
        var uvs = UvProjection.Cylindrical(barrel, Enumerable.Range(0, 32).ToArray(), Vector3.UnitY, null, -Vector3.UnitZ, UvProjection.Rect.Unit);
        var corners = new[] { 14, 22 }.SelectMany(f => uvs[f]).ToArray();
        var (u0, v0, u1, v1) = TextureSet.RuneRect;
        Assert.Equal(u0, corners.Min(p => p.X), 4);
        Assert.Equal(v0, corners.Min(p => p.Y), 4);
        Assert.Equal(u1, corners.Max(p => p.X), 4);
        Assert.Equal(v1, corners.Max(p => p.Y), 4);
    }

    [Fact]
    public void The_glyph_is_white_on_black_and_inside_that_rectangle()
    {
        var (u0, v0, u1, v1) = TextureSet.RuneRect;
        var image = Decode(Path.Combine(TexturesFolder(), "rune-emissive.png"));
        int lit = 0;
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var i = (y * image.Width + x) * 4;
                var white = image.Data[i] == 255 && image.Data[i + 1] == 255 && image.Data[i + 2] == 255;
                Assert.True(white || (image.Data[i] == 0 && image.Data[i + 1] == 0 && image.Data[i + 2] == 0), $"({x}, {y}) is neither black nor white");
                if (!white) continue;
                lit++;
                Assert.True(x >= u0 * 256 && x < u1 * 256 && y >= v0 * 256 && y < v1 * 256, $"lit texel ({x}, {y}) lies outside the rune's rectangle");
            }
        Assert.InRange(lit, 150, 2000);
    }
}
