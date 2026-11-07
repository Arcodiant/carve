using System.Numerics;

namespace Carve.ModelViewer.Tests;

/// <summary>A decoded PNG, and where a world point lands in it for a known eye.</summary>
internal sealed class ViewerFrame(string path, Vector3 eye, Vector3 centre) : FramePixels(path)
{
    /// <summary>The lens the agreed look uses; the test has its own copy so it does not trust the viewer's.</summary>
    private const float FieldOfViewY = 0.6f;

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
}
