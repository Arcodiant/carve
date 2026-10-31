namespace Carve.Debug;

/// <summary>A captured render: the PNG as saved in the run folder, and its decoded pixels.</summary>
public sealed class ScenarioFrame(string name, string path, byte[] png, int width, int height, byte[] pixels, string? caption)
{
    public string Name { get; } = name;

    /// <summary>Full path of the saved PNG.</summary>
    public string Path { get; } = path;

    public byte[] Png { get; } = png;

    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>The decoded pixels, RGBA, four bytes each, rows from the top. Compare these.</summary>
    public byte[] Pixels { get; } = pixels;

    /// <summary>The result's text block, e.g. <c>512x512, view front, 8 vertices, 6 faces</c>.</summary>
    public string? Caption { get; } = caption;

    /// <summary>One pixel, from the top left.</summary>
    public (byte R, byte G, byte B, byte A) Pixel(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        var at = ((y * Width) + x) * 4;
        return (Pixels[at], Pixels[at + 1], Pixels[at + 2], Pixels[at + 3]);
    }
}
