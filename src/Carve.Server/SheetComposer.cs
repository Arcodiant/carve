using System.Numerics;

namespace Carve.Server;

/// <summary>A composed sheet: RGBA, four bytes a pixel, rows from the top.</summary>
public sealed record Sheet(int Width, int Height, byte[] Pixels);

/// <summary>
/// Pure CPU: tiles square RGBA images into rows of <c>columns</c>, in the order given, with a gutter of the
/// background between them. The engine has drawn everything on a tile; this only places pixels.
/// </summary>
public static class SheetComposer
{
    public const int Gutter = 2;

    /// <summary>A sheet's rows for a number of tiles.</summary>
    public static int Rows(int tiles, int columns) => (tiles + columns - 1) / columns;

    /// <summary>The width of n columns, or the height of n rows, of tiles of a size.</summary>
    public static int Extent(int count, int size) => count * size + (count - 1) * Gutter;

    /// <summary>A linear colour as a frame shows it: through the target's half floats, then sRGB, alpha opaque.</summary>
    public static byte[] Srgb(Vector4 linear)
    {
        static byte Encode(float value)
        {
            var c = (double)(Half)value;
            return (byte)Math.Round(255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055));
        }

        return [Encode(linear.X), Encode(linear.Y), Encode(linear.Z), 255];
    }

    /// <param name="tiles">Each <paramref name="size"/> square, RGBA.</param>
    /// <param name="columns">Tiles to a row; a row left short is filled with the background.</param>
    /// <param name="size">The tiles' width and height.</param>
    /// <param name="background">RGBA, as <see cref="Srgb"/> gives it.</param>
    public static Sheet Compose(IReadOnlyList<byte[]> tiles, int columns, int size, byte[] background)
    {
        var width = Extent(columns, size);
        var height = Extent(Rows(tiles.Count, columns), size);
        var pixels = new byte[width * height * 4];
        for (var at = 0; at < pixels.Length; at += 4)
        {
            background.CopyTo(pixels, at);
        }

        for (var tile = 0; tile < tiles.Count; tile++)
        {
            var left = tile % columns * (size + Gutter);
            var top = tile / columns * (size + Gutter);
            for (var row = 0; row < size; row++)
            {
                Buffer.BlockCopy(tiles[tile], row * size * 4, pixels, ((top + row) * width + left) * 4, size * 4);
            }
        }

        return new Sheet(width, height, pixels);
    }
}
