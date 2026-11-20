using Xunit;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Tests;

/// <summary>The image pieces a node reads and fills: a rectangle's cover of tiles, wrapped reads, and what throws.</summary>
[Trait("Tier", "unit")]
public class TileReadTests
{
    private const int Size = 64, Side = 16, Across = Size / Side;

    private static float Value(int x, int y) => x * 100 + y;

    // The image's 4 x 4 tiles of 16, greyscale, texel (x, y) holding Value(x, y).
    private static readonly Tile[,] Tiles = Make();

    private static Tile[,] Make()
    {
        var tiles = new Tile[Across, Across];
        for (var ty = 0; ty < Across; ty++)
            for (var tx = 0; tx < Across; tx++)
            {
                var tile = tiles[tx, ty] = new Tile(PortKind.Grey, new TexelRect(tx * Side, ty * Side, Side, Side));
                for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                    for (var x = tile.Rect.X; x < tile.Rect.Right; x++) tile.SetGrey(x, y, Value(x, y));
            }
        return tiles;
    }

    private static TileRead Read(TexelRect asked)
    {
        var cover = TileRead.Cover(asked, Size, Side);
        var tiles = new Tile[cover.Width * cover.Height];
        for (var r = 0; r < cover.Height; r++)
            for (var c = 0; c < cover.Width; c++)
                tiles[r * cover.Width + c] = Tiles[Tile.Wrap(cover.X + c, Across), Tile.Wrap(cover.Y + r, Across)];
        return new TileRead(PortKind.Grey, Size, Side, asked, tiles);
    }

    [Fact]
    public void ARectangleIsCoveredByTheTilesItTouchesPastEachEdgeAndCorner()
    {
        Assert.Equal(new TexelRect(0, 0, 1, 1), TileRead.Cover(new TexelRect(0, 0, 16, 16), Size, Side));
        Assert.Equal(new TexelRect(-1, -1, 3, 3), TileRead.Cover(new TexelRect(-1, -1, 18, 18), Size, Side)); // a tile widened by 1
        Assert.Equal(new TexelRect(3, 3, 2, 2), TileRead.Cover(new TexelRect(60, 60, 10, 10), Size, Side)); // past the bottom-right
        Assert.Equal(new TexelRect(-1, 3, 2, 2), TileRead.Cover(new TexelRect(-3, 60, 10, 10), Size, Side)); // past the left and bottom
        Assert.Equal(new TexelRect(-2, -5, 1, 2), TileRead.Cover(new TexelRect(-32, -70, 16, 16), Size, Side)); // wholly negative: rows -70 to -55
        // An axis the image's width or more is the whole axis.
        Assert.Equal(new TexelRect(0, 2, 4, 1), TileRead.Cover(new TexelRect(-5, 33, 100, 10), Size, Side));
        Assert.Equal(new TexelRect(0, 0, 64, 10), TileRead.Clamp(new TexelRect(-5, 0, 64, 10), Size));
    }

    [Theory]
    [InlineData(0, 0, 16, 16)]
    [InlineData(-1, -1, 18, 18)]
    [InlineData(55, -7, 20, 30)]
    [InlineData(-40, 100, 33, 5)]
    [InlineData(-5, 10, 100, 70)]
    public void EveryReadInsideTheRectangleIsTheImagesTexelWrapped(int x, int y, int width, int height)
    {
        var asked = new TexelRect(x, y, width, height);
        var read = Read(asked);
        for (var dy = 0; dy < Math.Min(height, Size); dy++)
            for (var dx = 0; dx < Math.Min(width, Size); dx++)
            {
                int lx = read.Asked.X + dx, ly = read.Asked.Y + dy;
                var want = Value(Tile.Wrap(lx, Size), Tile.Wrap(ly, Size));
                Assert.Equal(want, read.Grey(lx, ly));
                Assert.Equal(want, read.Grey(lx + Size, ly - 2 * Size)); // the same texel a repeat away
                Assert.Equal((want, want, want), read.Colour(lx, ly)); // greyscale spreads to grey
            }
    }

    [Fact]
    public void AReadOutsideTheRectangleThrowsNamingIt()
    {
        var read = Read(new TexelRect(-1, -1, 18, 18));
        Assert.Equal(Value(63, 63), read.Grey(-1, -1));
        Assert.Equal(Value(16, 16), read.Grey(16, 16));
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => read.Grey(17, 0));
        Assert.Contains("(17, 0)", e.Message);
        Assert.Contains("columns -1 to 16, rows -1 to 16", e.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => read.Grey(0, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => read.Grey(62, 0)); // -2, wrapped
    }

    [Fact]
    public void ARowIsReadFromOneTileAndNeverAcrossTwo()
    {
        var read = Read(new TexelRect(-1, -1, 18, 18));
        Assert.Equal([Value(0, 5), Value(1, 5), Value(2, 5)], read.Row(5, 0, 3).ToArray());
        Assert.Equal([Value(63, 63)], read.Row(-1, -1, 1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => read.Row(5, -1, 2)); // columns 63 and 0: two tiles
        Assert.Throws<ArgumentOutOfRangeException>(() => read.Row(5, 15, 3)); // past what was asked
    }

    [Fact]
    public void ATileHoldsOnlyItsRectangleAndAColourOneHasNoGrey()
    {
        var tile = new Tile(PortKind.Colour, new TexelRect(16, 32, 16, 16));
        tile.SetColour(31, 47, 0.25f, 0.5f, 0.75f);
        Assert.Equal((0.25f, 0.5f, 0.75f), tile.Colour(31, 47));
        Assert.Equal([0.25f, 0.5f, 0.75f], tile.Row(47)[^3..].ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.Colour(32, 47));
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.Row(31));
        Assert.Throws<InvalidOperationException>(() => tile.Grey(16, 32));
        Assert.Equal(16 * 16 * 3 * 4, tile.Bytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tile(PortKind.Grey, new TexelRect(-16, 0, 16, 16)));
    }

    [Fact]
    public void ABoundingBoxTakesTheTexelsItTouchesAndItsMargin()
    {
        Assert.Equal(new TexelRect(-4, 1, 9, 5), TexelRect.Bounding(-1.5f, 3f, 2.2f, 4f, 2)); // columns -2 - 2 to 3 + 2 - 1, rows 3 - 2 to 4 + 2 - 1
        Assert.Equal(new TexelRect(-2, -2, 20, 22), new TexelRect(0, 0, 16, 18).Inflate(2, 1).Inflate(0, 1));
    }
}
