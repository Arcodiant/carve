using System.Numerics;

namespace Carve.ModelViewer;

/// <summary>Where the camera goes for one view: on its direction from the bounds' centre, +Y up.</summary>
internal sealed record ViewFrame(ViewSpec View, Vector3 Eye, Matrix4x4 World)
{
    /// <summary>The same lens as the product's render, so a frame compares with the M-1 cube.</summary>
    public const float FieldOfViewY = 0.6f;

    /// <summary>The part of the frame left clear on each side, at the tightest edge.</summary>
    private const float Margin = 0.08f;

    public static ViewFrame Fit(ViewSpec view, Vector3 min, Vector3 max)
    {
        var centre = (min + max) * 0.5f;
        var half = (max - min) * 0.5f;
        var direction = Vector3.Normalize(view.Direction);
        var rotation = Matrix4x4.CreateLookAt(direction, Vector3.Zero, Vector3.UnitY) with { M41 = 0, M42 = 0, M43 = 0 };
        var tangent = MathF.Tan(FieldOfViewY * 0.5f) * (1 - 2 * Margin);

        // The nearest distance at which each corner is inside the margin, and outside the bounding sphere.
        var distance = half.Length() * 1.2f;
        for (var corner = 0; corner < 8; corner++)
        {
            var offset = new Vector3(
                (corner & 1) == 0 ? -half.X : half.X,
                (corner & 2) == 0 ? -half.Y : half.Y,
                (corner & 4) == 0 ? -half.Z : half.Z);
            var seen = Vector3.Transform(offset, rotation); // Camera space: looking down -Z.
            distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
        }

        var eye = centre + direction * distance;
        Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye, centre, Vector3.UnitY), out var world);
        return new ViewFrame(view, eye, world);
    }
}
