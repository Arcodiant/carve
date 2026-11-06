using System.Numerics;
using StbImageSharp;

namespace Carve.ModelViewer.Tests;

/// <summary>A decoded PNG, and where a world point lands in it for a known eye.</summary>
internal sealed class ViewerFrame(string path, Vector3 eye, Vector3 centre)
{
    /// <summary>The lens the agreed look uses; the test has its own copy so it does not trust the viewer's.</summary>
    private const float FieldOfViewY = 0.6f;

    private readonly ImageResult _image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

    public int Width => _image.Width;

    public int Height => _image.Height;

    public (int R, int G, int B) Pixel(int x, int y)
    {
        var at = (y * _image.Width + x) * 4;
        return (_image.Data[at], _image.Data[at + 1], _image.Data[at + 2]);
    }

    /// <summary>The pixel a world point projects to, +Y up and +X to the camera's right.</summary>
    public (int X, int Y) Project(Vector3 point)
    {
        var view = Matrix4x4.CreateLookAt(eye, centre, Vector3.UnitY);
        var seen = Vector3.Transform(point, view);
        var scale = 1f / MathF.Tan(FieldOfViewY * 0.5f) / -seen.Z;
        return (
            (int)MathF.Round((seen.X * scale * 0.5f + 0.5f) * Width),
            (int)MathF.Round((0.5f - seen.Y * scale * 0.5f) * Height));
    }

    public (int R, int G, int B) At(Vector3 point)
    {
        var (x, y) = Project(point);
        return Pixel(x, y);
    }

    /// <summary>The largest difference in any one channel, in 1/255ths.</summary>
    public static int Difference((int R, int G, int B) a, (int R, int G, int B) b) =>
        Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
}
