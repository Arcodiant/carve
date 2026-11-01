using System.Numerics;

namespace Carve.Server;

/// <summary>The fixed views of the <c>render</c> tool.</summary>
public enum RenderView
{
    Front,
    Back,
}

/// <summary>Where the camera and the light go for one view of the drawn box.</summary>
/// <param name="Eye">The camera's position.</param>
/// <param name="World">Camera to world, for the camera's <c>WorldTransform</c>.</param>
/// <param name="View">World to camera: the inverse of <paramref name="World"/>, as the engine takes it.</param>
/// <param name="Projection">For <c>Camera.ProjectionMatrix</c>.</param>
/// <param name="LightOrientation">For the directional light's transform.</param>
public sealed record ViewFrame(
    Vector3 Eye,
    Matrix4x4 World,
    Matrix4x4 View,
    Matrix4x4 Projection,
    Quaternion LightOrientation);

/// <summary>Pure CPU: the camera that frames a <see cref="DrawnSpace"/> box from a view, and the light.</summary>
public static class ViewFraming
{
    public const float FieldOfViewY = 0.6f;

    /// <summary>The part of the frame left clear on each side, at the tightest edge.</summary>
    public const float Margin = 0.08f;

    private static readonly Vector3 FrontDirection = Vector3.Normalize(new Vector3(1.6f, 1.2f, 2.2f));

    /// <summary>The agreed look in the front view: top bright, +Z middling, +X dark.</summary>
    private static readonly Matrix4x4 FrontLight = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);

    /// <param name="half">The drawn box's largest corner: it is centred on the origin, half-diagonal 1.</param>
    /// <param name="view">The view.</param>
    public static ViewFrame Frame(Vector3 half, RenderView view)
    {
        var direction = Direction(view);
        var rotation = LookRotation(direction);
        var tangent = MathF.Tan(FieldOfViewY * 0.5f) * (1 - 2 * Margin);

        // The nearest distance at which each corner is inside the margin. The eye stays outside the
        // bounding sphere, so the near plane is positive whatever the shape.
        var distance = 1.2f;
        for (var corner = 0; corner < 8; corner++)
        {
            var offset = new Vector3(
                (corner & 1) == 0 ? -half.X : half.X,
                (corner & 2) == 0 ? -half.Y : half.Y,
                (corner & 4) == 0 ? -half.Z : half.Z);
            var seen = Vector3.Transform(offset, rotation); // Camera space: looking down -Z.
            distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
        }

        var eye = direction * distance;
        var lookAt = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
        Matrix4x4.Invert(lookAt, out var world);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewY, 1f, (distance - 1) * 0.9f, (distance + 1) * 1.1f);

        return new ViewFrame(eye, world, lookAt, projection, LightOrientation(view));
    }

    /// <summary>
    /// The light keeps its place relative to the camera: the front light, turned by whatever turns
    /// the front camera into this view's. A world-fixed light would leave the far faces ambient-only.
    /// </summary>
    public static Quaternion LightOrientation(RenderView view)
    {
        Matrix4x4.Invert(LookRotation(Direction(view)), out var cameraToWorld);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(
            FrontLight * LookRotation(Direction(RenderView.Front)) * cameraToWorld));
    }

    private static Vector3 Direction(RenderView view) => view == RenderView.Front ? FrontDirection : -FrontDirection;

    /// <summary>World to camera, rotation only.</summary>
    private static Matrix4x4 LookRotation(Vector3 direction) =>
        Matrix4x4.CreateLookAt(direction, Vector3.Zero, Vector3.UnitY) with { M41 = 0, M42 = 0, M43 = 0 };
}
