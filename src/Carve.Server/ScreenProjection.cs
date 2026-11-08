using System.Numerics;

namespace Carve.Server;

/// <summary>Pure CPU: where a drawn-space point lands on an image, for what the engine does not place itself.</summary>
public static class ScreenProjection
{
    /// <summary>
    /// The point in pixels of an image of <paramref name="size"/>, from the top left with Y down, on the
    /// engine's continuous scale: pixel n covers n to n + 1, so its centre is at n + 0.5.
    /// </summary>
    public static Vector2 ToPixel(Vector3 drawn, Matrix4x4 view, Matrix4x4 projection, int size)
    {
        var clip = Vector4.Transform(new Vector4(drawn, 1), view * projection);
        return new Vector2((clip.X / clip.W + 1) / 2 * size, (1 - clip.Y / clip.W) / 2 * size);
    }
}
