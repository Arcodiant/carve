using System.Numerics;

namespace Carve.Server;

/// <summary>
/// The checker <c>render</c>'s <c>checker</c> look draws at each face's corner UVs (T-57): 256 x 256 RGBA8, sRGB, made in
/// code, 8 x 8 cells of 32 px. Column c has its own hue, red at u = 0 to violet at u = 1; cell (c, r) is that hue when
/// c + r is even and the hue at 0.55 per channel when odd; cell (0, 0), at uv (0, 0), the top-left, is white. Pure CPU:
/// the engine texture is made from <see cref="Pixels"/> by <see cref="RenderSystem"/>, which holds it.
/// </summary>
public static class CheckerTexture
{
    public const int Size = 256;

    public const int Cells = 8;

    public const int CellPixels = Size / Cells;

    /// <summary>The eight column hues, #RRGGBB in sRGB, column 0 (u = 0) first.</summary>
    public static readonly IReadOnlyList<uint> Hues = [0xE04040, 0xE08A30, 0xD8C830, 0x50B050, 0x40B0C0, 0x4070E0, 0x6A50D0, 0xB050C0];

    /// <summary>The white cell's centre in UV, where an unmapped face's corners sample, so that magenta times it is magenta.</summary>
    public static readonly Vector2 WhiteCentre = new(CellPixels / 2f / Size, CellPixels / 2f / Size);

    private static readonly Lazy<byte[]> Made = new(Make);

    /// <summary>The texture, RGBA8 with A = 255, row 0 first (v = 0, the top), tightly packed.</summary>
    public static ReadOnlyMemory<byte> Pixels => Made.Value;

    /// <summary>Cell (c, r)'s colour as #RRGGBB, sRGB: white at (0, 0); the column's hue when c + r is even; else each channel times 0.55, rounded.</summary>
    public static uint Cell(int column, int row)
    {
        if (column == 0 && row == 0) return 0xFFFFFF;
        var hue = Hues[column];
        if ((column + row) % 2 == 0) return hue;
        // round(x * 0.55) in integers; no channel lands on a half.
        static uint Dark(uint x) => (x * 55 + 50) / 100;
        return (Dark(hue >> 16 & 0xFF) << 16) | (Dark(hue >> 8 & 0xFF) << 8) | Dark(hue & 0xFF);
    }

    private static byte[] Make()
    {
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var colour = Cell(x / CellPixels, y / CellPixels);
                var at = (y * Size + x) * 4;
                pixels[at] = (byte)(colour >> 16);
                pixels[at + 1] = (byte)(colour >> 8);
                pixels[at + 2] = (byte)colour;
                pixels[at + 3] = 255;
            }
        }

        return pixels;
    }
}
