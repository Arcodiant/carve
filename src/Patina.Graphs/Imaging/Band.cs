namespace Patina.Graphs.Imaging;

/// <summary>What a port carries: one channel (greyscale) or three (colour). <see cref="Any"/> is for an input port that takes either.</summary>
public enum PortKind { Grey, Colour, Any }

public static class PortKinds
{
    public static string Name(PortKind kind) => kind switch
    {
        PortKind.Grey => "greyscale",
        PortKind.Colour => "colour",
        _ => "greyscale or colour",
    };

    public static int Channels(PortKind kind) => kind == PortKind.Colour ? 3 : 1;
}

/// <summary>
/// A band of an image: the full width, rows <see cref="Y0"/> to <see cref="Y0"/> + <see cref="Rows"/> - 1, one or three
/// <c>float</c> channels per texel. Rows are logical: row <c>y</c> is the image's row <c>Wrap(y, size)</c>, so a band
/// asked for with rows -2 to 33 holds the image's last two rows first. Columns are always 0 to width - 1; a node reads
/// across the left and right edges through <see cref="Wrap"/>, since every texture tiles. Colour channels hold
/// sRGB-encoded values, as a <c>#rrggbb</c> does; a normal's are the vector's components.
/// </summary>
public sealed class Band
{
    public Band(PortKind kind, int width, int y0, int rows)
    {
        if (kind == PortKind.Any) throw new ArgumentException("a band is greyscale or colour", nameof(kind));
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        Kind = kind; Width = width; Y0 = y0; Rows = rows;
        Channels = PortKinds.Channels(kind);
        Data = new float[(long)width * rows * Channels];
    }

    public PortKind Kind { get; }
    public int Width { get; }
    public int Y0 { get; }
    public int Rows { get; }
    public int Channels { get; }

    /// <summary>Row-major, <see cref="Channels"/> floats per texel.</summary>
    public float[] Data { get; }

    public long Bytes => Data.LongLength * sizeof(float);

    public bool HoldsRow(int y) => y >= Y0 && y < Y0 + Rows;

    /// <summary><paramref name="v"/> modulo <paramref name="n"/>, always 0 to n - 1.</summary>
    public static int Wrap(int v, int n) => ((v % n) + n) % n;

    /// <summary>The texels of logical row <paramref name="y"/>, <see cref="Channels"/> floats each.</summary>
    public Span<float> Row(int y)
    {
        if (!HoldsRow(y)) throw new ArgumentOutOfRangeException(nameof(y), $"row {y} is not in this band (rows {Y0} to {Y0 + Rows - 1})");
        return Data.AsSpan((y - Y0) * Width * Channels, Width * Channels);
    }

    private int Index(int x, int y)
    {
        if (!HoldsRow(y)) throw new ArgumentOutOfRangeException(nameof(y), $"row {y} is not in this band (rows {Y0} to {Y0 + Rows - 1})");
        return ((y - Y0) * Width + Wrap(x, Width)) * Channels;
    }

    /// <summary>The greyscale value at (x, y), x wrapping. A colour band has none, and throws.</summary>
    public float Grey(int x, int y) =>
        Kind == PortKind.Grey ? Data[Index(x, y)] : throw new InvalidOperationException("a colour band has no single grey value");

    /// <summary>The colour at (x, y), x wrapping; a greyscale band is spread to grey.</summary>
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
