namespace Patina.Graphs.Imaging;

/// <summary>
/// One tile of one port's image: the texels of <see cref="Rect"/>, a rectangle inside the image (the evaluator only
/// makes canonical tiles, so a tile's texels are addressed by their image coordinates and never wrap), one or three
/// <c>float</c> channels a texel. Colour channels hold sRGB-encoded values, as a <c>#rrggbb</c> does; a normal's are the
/// vector's components. A coordinate outside <see cref="Rect"/> throws.
/// </summary>
public sealed class Tile
{
    public Tile(PortKind kind, TexelRect rect) : this(kind, rect, null) { }

    /// <summary>A tile over <paramref name="data"/>, zeros of the tile's length (the evaluator reuses dropped tiles' arrays, T-155).</summary>
    internal Tile(PortKind kind, TexelRect rect, float[]? data)
    {
        if (kind == PortKind.Any) throw new ArgumentException("a tile is greyscale or colour", nameof(kind));
        ArgumentOutOfRangeException.ThrowIfNegative(rect.X, nameof(rect));
        ArgumentOutOfRangeException.ThrowIfNegative(rect.Y, nameof(rect));
        Kind = kind; Rect = rect;
        Channels = PortKinds.Channels(kind);
        var length = (long)rect.Width * rect.Height * Channels;
        if (data is not null && data.LongLength != length) throw new ArgumentException("the array is not the tile's length", nameof(data));
        Data = data ?? new float[length];
    }

    public PortKind Kind { get; }
    public TexelRect Rect { get; }
    public int Channels { get; }

    /// <summary>Row-major over <see cref="Rect"/>, <see cref="Channels"/> floats per texel.</summary>
    public float[] Data { get; }

    public long Bytes => Data.LongLength * sizeof(float);

    /// <summary><paramref name="v"/> modulo <paramref name="n"/>, always 0 to n - 1.</summary>
    public static int Wrap(int v, int n) => ((v % n) + n) % n;

    /// <summary>The texels of row <paramref name="y"/> from <see cref="TexelRect.X"/>, <see cref="Channels"/> floats each.</summary>
    public Span<float> Row(int y)
    {
        var ry = y - Rect.Y;
        if ((uint)ry >= (uint)Rect.Height) throw new ArgumentOutOfRangeException(nameof(y), $"row {y} is not in this tile ({Rect})");
        return Data.AsSpan(ry * Rect.Width * Channels, Rect.Width * Channels);
    }

    private int Index(int x, int y)
    {
        int rx = x - Rect.X, ry = y - Rect.Y;
        if ((uint)rx >= (uint)Rect.Width || (uint)ry >= (uint)Rect.Height)
            throw new ArgumentOutOfRangeException(nameof(x), $"texel ({x}, {y}) is not in this tile ({Rect})");
        return (ry * Rect.Width + rx) * Channels;
    }

    /// <summary>The greyscale value at (x, y). A colour tile has none, and throws.</summary>
    public float Grey(int x, int y) =>
        Kind == PortKind.Grey ? Data[Index(x, y)] : throw new InvalidOperationException("a colour tile has no single grey value");

    /// <summary>The colour at (x, y); a greyscale tile is spread to grey.</summary>
    public (float R, float G, float B) Colour(int x, int y)
    {
        var i = Index(x, y);
        return Kind == PortKind.Grey ? (Data[i], Data[i], Data[i]) : (Data[i], Data[i + 1], Data[i + 2]);
    }

    public void SetGrey(int x, int y, float v) => Data[Index(x, y)] = v;

    public void SetColour(int x, int y, float r, float g, float b)
    {
        var i = Index(x, y);
        Data[i] = r; Data[i + 1] = g; Data[i + 2] = b;
    }
}
