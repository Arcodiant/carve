using System.Numerics;

namespace Carve.Server;

/// <summary>The fixed views of the <c>render</c> tool.</summary>
public enum RenderView
{
    Front,
    Back,
}

/// <summary>Where the camera and the light go for one view of some bounds.</summary>
/// <param name="Eye">The camera's position.</param>
/// <param name="Target">The point looked at, the centre of the bounds.</param>
/// <param name="View">World to camera (a look-at).</param>
/// <param name="Projection">For <c>Camera.ProjectionMatrix</c>.</param>
/// <param name="LightOrientation">For the directional light's transform.</param>
public sealed record ViewFrame(Vector3 Eye, Vector3 Target, Matrix4x4 View, Matrix4x4 Projection, Quaternion LightOrientation);

/// <summary>Pure CPU: from the bounds and the view, the camera that frames them and the light.</summary>
public static class ViewFraming
{
    public const float FieldOfViewY = 0.6f;

    /// <summary>The part of the frame left clear on each side, at the tightest edge.</summary>
    public const float Margin = 0.08f;

    private static readonly Vector3 FrontDirection = Vector3.Normalize(new Vector3(1.6f, 1.2f, 2.2f));

    /// <summary>The agreed look in the front view: top bright, +Z middling, +X dark.</summary>
    private static readonly Matrix4x4 FrontLight = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);

    public static ViewFrame Frame(Vector3 min, Vector3 max, RenderView view)
    {
        var target = (min + max) * 0.5f;
        var half = (max - min) * 0.5f;

        // A point, or a tiny mesh far from the origin, still needs a camera that is somewhere else.
        var largest = Math.Max(Math.Abs(min.X), Math.Max(Math.Abs(min.Y), Math.Abs(min.Z)));
        largest = Math.Max(largest, Math.Max(Math.Abs(max.X), Math.Max(Math.Abs(max.Y), Math.Abs(max.Z))));
        var radius = Math.Max(half.Length(), Math.Max(1e-6f, largest * 1e-4f));

        var direction = Direction(view);
        var rotation = LookRotation(direction);
        var tangent = MathF.Tan(FieldOfViewY * 0.5f) * (1 - 2 * Margin);

        // The nearest distance at which each corner is inside the margin. The eye stays outside the
        // bounding sphere, so the near plane is positive whatever the shape.
        var distance = radius * 1.2f;
        for (var corner = 0; corner < 8; corner++)
        {
            var offset = new Vector3(
                (corner & 1) == 0 ? -half.X : half.X,
                (corner & 2) == 0 ? -half.Y : half.Y,
                (corner & 4) == 0 ? -half.Z : half.Z);
            var seen = Vector3.Transform(offset, rotation); // Camera space: looking down -Z.
            distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
        }

        var eye = target + direction * distance;
        return new ViewFrame(
            eye,
            target,
            Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY),
            Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewY, 1f, (distance - radius) * 0.9f, (distance + radius) * 1.1f),
            LightOrientation(view));
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
