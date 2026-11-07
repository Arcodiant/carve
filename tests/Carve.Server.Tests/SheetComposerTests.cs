using System.Numerics;

namespace Carve.Server.Tests;

/// <summary>Tiling, on tiles of 4 pixels, each one colour, so a pixel says which tile it came from.</summary>
[Trait("Tier", "unit")]
public class SheetComposerTests
{
    private const int Size = 4;

    private static readonly byte[] Background = [9, 8, 7, 255];

    private static byte[] Filled(int tile)
    {
        var pixels = new byte[Size * Size * 4];
        for (var at = 0; at < pixels.Length; at += 4)
        {
            pixels[at] = (byte)(100 + tile);
            pixels[at + 3] = 255;
        }

        return pixels;
    }

    private static byte[] At(Sheet sheet, int x, int y) => sheet.Pixels.AsSpan((y * sheet.Width + x) * 4, 4).ToArray();

    public static TheoryData<int, int> Layouts()
    {
        var data = new TheoryData<int, int>();
        for (var tiles = 2; tiles <= 6; tiles++)
        for (var columns = 1; columns <= 3; columns++)
        {
            data.Add(tiles, columns);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Tiles_go_in_order_along_rows_with_the_rest_background(int tiles, int columns)
    {
        var sheet = SheetComposer.Compose(Enumerable.Range(0, tiles).Select(Filled).ToList(), columns, Size, Background);

        var rows = (tiles + columns - 1) / columns;
        Assert.Equal(columns * Size + (columns - 1) * 2, sheet.Width);
        Assert.Equal(rows * Size + (rows - 1) * 2, sheet.Height);
        Assert.Equal(sheet.Width * sheet.Height * 4, sheet.Pixels.Length);

        for (var y = 0; y < sheet.Height; y++)
        for (var x = 0; x < sheet.Width; x++)
        {
            var (column, row) = (x / (Size + 2), y / (Size + 2));
            var inTile = x % (Size + 2) < Size && y % (Size + 2) < Size;
            var tile = row * columns + column;
            byte[] expected = inTile && tile < tiles ? [(byte)(100 + tile), 0, 0, 255] : Background;
            Assert.Equal(expected, At(sheet, x, y));
        }
    }

    [Fact]
    public void Rows_of_a_tile_stay_rows()
    {
        var tile = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            tile[(y * Size + x) * 4] = (byte)(y * Size + x);
        }

        var sheet = SheetComposer.Compose([tile, tile], 2, Size, Background);

        Assert.Equal(7, At(sheet, Size + 2 + 3, 1)[0]);
        Assert.Equal(13, At(sheet, 1, 3)[0]);
    }

    [Theory]
    [InlineData(6, 3, 256, 772, 514)]
    [InlineData(4, 2, 256, 514, 514)]
    [InlineData(2, 2, 1024, 2050, 1024)]
    [InlineData(2, 3, 512, 1540, 512)]
    [InlineData(6, 1, 256, 256, 1546)]
    public void The_size_of_a_sheet(int tiles, int columns, int size, int width, int height)
    {
        Assert.Equal(width, SheetComposer.Extent(columns, size));
        Assert.Equal(height, SheetComposer.Extent(SheetComposer.Rows(tiles, columns), size));
    }

    /// <summary>What a frame's empty pixel reads, measured in the crate's frames.</summary>
    [Fact]
    public void The_background_is_what_a_frame_shows() =>
        Assert.Equal(new byte[] { 48, 53, 56, 255 }, SheetComposer.Srgb(new Vector4(0.03f, 0.035f, 0.04f, 1)));
}
