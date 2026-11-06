using StbImageSharp;

namespace Carve.ModelViewer.Tests;

/// <summary>A decoded PNG's pixels. Carve.ExportViewer.Tests links this file: its frames are read the same way.</summary>
internal class FramePixels(string path)
{
    private readonly ImageResult _image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

    public int Width => _image.Width;

    public int Height => _image.Height;

    public (int R, int G, int B) Pixel(int x, int y)
    {
        var at = (y * _image.Width + x) * 4;
        return (_image.Data[at], _image.Data[at + 1], _image.Data[at + 2]);
    }

    /// <summary>The largest difference in any one channel, in 1/255ths.</summary>
    public static int Difference((int R, int G, int B) a, (int R, int G, int B) b) =>
        Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
}
