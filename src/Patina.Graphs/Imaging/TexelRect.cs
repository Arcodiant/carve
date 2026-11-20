namespace Patina.Graphs.Imaging;

/// <summary>
/// A rectangle of logical texels: columns <see cref="X"/> to <see cref="Right"/> - 1, rows <see cref="Y"/> to
/// <see cref="Bottom"/> - 1. <see cref="X"/> and <see cref="Y"/> may be any int, negative or past the image: a texel
/// <c>(x, y)</c> is the image's <c>(Tile.Wrap(x, size), Tile.Wrap(y, size))</c> when it is read, since every texture tiles.
/// </summary>
public readonly record struct TexelRect
{
    public TexelRect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        X = x; Y = y; Width = width; Height = height;
    }

    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>One past the last column.</summary>
    public int Right => X + Width;

    /// <summary>One past the last row.</summary>
    public int Bottom => Y + Height;

    /// <summary>Grown by <paramref name="r"/> texels on every side.</summary>
    public TexelRect Inflate(int r) => Inflate(r, r);

    /// <summary>Grown by <paramref name="rx"/> texels left and right and <paramref name="ry"/> above and below.</summary>
    public TexelRect Inflate(int rx, int ry) => new(X - rx, Y - ry, Width + 2 * rx, Height + 2 * ry);

    /// <summary>
    /// The texels a continuous box touches, in texel units, with <paramref name="margin"/> more on every side: columns
    /// <c>floor(minX) - margin</c> to <c>ceil(maxX) + margin - 1</c>, and the same for rows.
    /// </summary>
    public static TexelRect Bounding(float minX, float minY, float maxX, float maxY, int margin)
    {
        int x0 = (int)MathF.Floor(minX) - margin, y0 = (int)MathF.Floor(minY) - margin;
        int x1 = (int)MathF.Ceiling(maxX) + margin, y1 = (int)MathF.Ceiling(maxY) + margin;
        return new(x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));
    }

    public override string ToString() => $"columns {X} to {Right - 1}, rows {Y} to {Bottom - 1}";
}
