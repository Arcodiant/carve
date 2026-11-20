using System.Numerics;

namespace Carve.Core.Baking;

/// <summary>
/// A baked image (M-24, T-120): <see cref="Width"/> by <see cref="Height"/> texels of <see cref="Channels"/> (1 to 4)
/// <see cref="float"/> values each, row-major with the top row first and a texel's channels together. Texel (x, y) has
/// its centre at uv ((x + 0.5) / width, (y + 0.5) / height), so uv (0, 0) is the top-left corner, as in M-7's textures
/// and the <c>uvs</c> look. What the values mean is the bake's own: 0 or 1 for a mask, glTF's 0 to 255 encoding for a
/// tangent-space normal (<see cref="TangentFrame"/>). A new image is all zeros.
/// </summary>
public sealed class BakeImage
{
    private readonly float[] _values;

    /// <summary>An image of zeros. Width and height are at least 1, channels 1 to 4.</summary>
    public BakeImage(int width, int height, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 4);
        Width = width;
        Height = height;
        Channels = channels;
        _values = new float[checked(width * height * channels)];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Values per texel, 1 to 4.</summary>
    public int Channels { get; }

    /// <summary>Every value, row-major with the top row first, a texel's <see cref="Channels"/> values together.</summary>
    public Span<float> Values => _values;

    /// <summary>The value of channel <paramref name="channel"/> at texel (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public float this[int x, int y, int channel = 0]
    {
        get => _values[Index(x, y, channel)];
        set => _values[Index(x, y, channel)] = value;
    }

    /// <summary>The <see cref="Channels"/> values of texel (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public Span<float> Texel(int x, int y) => _values.AsSpan(Index(x, y, 0), Channels);

    /// <summary>A copy, value for value.</summary>
    public BakeImage Clone()
    {
        var copy = new BakeImage(Width, Height, Channels);
        _values.CopyTo(copy._values, 0);
        return copy;
    }

    private int Index(int x, int y, int channel)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        if ((uint)channel >= (uint)Channels) throw new ArgumentOutOfRangeException(nameof(channel));
        return (y * Width + x) * Channels + channel;
    }
}
