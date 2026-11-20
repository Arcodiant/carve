using StbImageWriteSharp;

namespace Patina.Graphs.Imaging;

/// <summary>What an output image is for, in role order (the order a swatch sheet's columns take).</summary>
public enum OutputRole { BaseColour, Normal, Roughness, Occlusion }

public static class OutputRoles
{
    public static readonly IReadOnlyList<OutputRole> All = [OutputRole.BaseColour, OutputRole.Normal, OutputRole.Roughness, OutputRole.Occlusion];

    public static readonly IReadOnlyList<string> Names = ["base_colour", "normal", "roughness", "occlusion"];

    /// <summary>The role's name in a file and a PNG's name: <c>base_colour</c>, <c>normal</c>, <c>roughness</c>, <c>occlusion</c>.</summary>
    public static string Name(OutputRole role) => Names[(int)role];

    public static bool TryParse(string name, out OutputRole role)
    {
        var i = Names.ToList().IndexOf(name);
        role = i < 0 ? default : (OutputRole)i;
        return i >= 0;
    }

    /// <summary>What the role's input must carry: colour for <c>normal</c>, greyscale for <c>roughness</c> and <c>occlusion</c>, either for <c>base_colour</c> (greyscale spread to grey).</summary>
    public static PortKind Takes(OutputRole role) => role switch
    {
        OutputRole.Normal => PortKind.Colour,
        OutputRole.Roughness or OutputRole.Occlusion => PortKind.Grey,
        _ => PortKind.Any,
    };
}

/// <summary>
/// How each role is written as RGBA8, rounding <see cref="MidpointRounding.AwayFromZero"/> as <c>TextureSet</c> does:
/// <list type="bullet">
/// <item><c>base_colour</c>: sRGB RGB, 255 * v, alpha 255;</item>
/// <item><c>normal</c>: tangent space, glTF convention (green up), 127.5 + 127.5 * n, alpha 255;</item>
/// <item><c>roughness</c>: a glTF metallic-roughness image, R 255, G 255 * roughness, B metallic 0, alpha 255;</item>
/// <item><c>occlusion</c>: 255 * occlusion in R, G and B, alpha 255.</item>
/// </list>
/// </summary>
public static class OutputPacking
{
    public static byte Byte(float v) => (byte)Math.Clamp(MathF.Round(v, MidpointRounding.AwayFromZero), 0f, 255f);

    /// <summary>Packs <paramref name="tile"/> into its place in <paramref name="rgba"/>, the whole image of side <paramref name="size"/>, row by row.</summary>
    public static void PackTile(OutputRole role, Tile tile, byte[] rgba, int size)
    {
        if (rgba.LongLength != (long)size * size * 4) throw new ArgumentException("an RGBA8 image is four bytes a texel", nameof(rgba));
        for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
            PackRow(role, tile, y, rgba.AsSpan((int)(((long)y * size + tile.Rect.X) * 4), tile.Rect.Width * 4));
    }

    /// <summary>Packs row <paramref name="y"/> of <paramref name="tile"/>, its columns <c>Rect.X</c> on, into <paramref name="rgba"/>, four bytes a texel.</summary>
    public static void PackRow(OutputRole role, Tile tile, int y, Span<byte> rgba)
    {
        if (rgba.Length != tile.Rect.Width * 4) throw new ArgumentException("one row is four bytes a texel", nameof(rgba));
        for (var i = 0; i < tile.Rect.Width; i++)
        {
            var x = tile.Rect.X + i;
            var o = i * 4;
            switch (role)
            {
                case OutputRole.BaseColour:
                {
                    var (r, g, b) = tile.Colour(x, y);
                    rgba[o] = Byte(255f * r); rgba[o + 1] = Byte(255f * g); rgba[o + 2] = Byte(255f * b);
                    break;
                }
                case OutputRole.Normal:
                {
                    var (r, g, b) = tile.Colour(x, y);
                    rgba[o] = Byte(127.5f + 127.5f * r); rgba[o + 1] = Byte(127.5f + 127.5f * g); rgba[o + 2] = Byte(127.5f + 127.5f * b);
                    break;
                }
                case OutputRole.Roughness:
                    rgba[o] = 255; rgba[o + 1] = Byte(255f * tile.Grey(x, y)); rgba[o + 2] = 0;
                    break;
                case OutputRole.Occlusion:
                    var a = Byte(255f * tile.Grey(x, y));
                    rgba[o] = a; rgba[o + 1] = a; rgba[o + 2] = a;
                    break;
            }
            rgba[o + 3] = 255;
        }
    }
}

/// <summary>PNG bytes of an RGBA8 image through <c>StbImageWriteSharp</c>'s <c>ImageWriter</c>, which writes the same bytes every time (probed at M-27's planning at 2048 and 8192).</summary>
public static class Png
{
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        if (rgba.LongLength != (long)width * height * 4) throw new ArgumentException("an RGBA8 image is four bytes a texel", nameof(rgba));
        using var stream = new MemoryStream();
        new ImageWriter().WritePng(rgba, width, height, ColorComponents.RedGreenBlueAlpha, stream);
        return stream.ToArray();
    }
}
