using System.Numerics;

namespace Carve.Server;

/// <summary>The fixed views of the <c>render</c> tool.</summary>
public enum RenderView
{
    Front,
    Back,
}

/// <summary>How the camera projects: a game's view, or parallel lines that stay parallel.</summary>
public enum Projection
{
    Perspective,
    Orthographic,
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

/// <summary>
/// Where to look from: <paramref name="Direction"/> runs from the point looked at toward the eye (any
/// length, not zero), <paramref name="Zoom"/> is 1 to 8, and <paramref name="Target"/> is the point
/// looked at, in drawn space once it reaches <see cref="ViewFraming"/> (null: the centre).
/// </summary>
public sealed record CameraSpec(Vector3 Direction, float Zoom = 1f, Vector3? Target = null)
{
    /// <summary>The M-1 view, as a camera: zoom 1 at the centre. Not normalised, so it normalises as M-1 did.</summary>
    public static CameraSpec ForView(RenderView view) =>
        new(view == RenderView.Front ? ViewFraming.FrontDirection : -ViewFraming.FrontDirection);
}

/// <summary>Pure CPU: the camera that frames a <see cref="DrawnSpace"/> box from a direction, and the light.</summary>
public static class ViewFraming
{
    public const float FieldOfViewY = 0.6f;

    /// <summary>The part of the frame left clear on each side, at the tightest edge.</summary>
    public const float Margin = 0.08f;

    /// <summary>Within this cone of vertical, up is -Z: CreateLookAt returns NaN looking along up.</summary>
    public static readonly float VerticalCosine = RenderLights.VerticalCosine;

    internal static readonly Vector3 FrontDirection = new(1.6f, 1.2f, 2.2f);

    /// <summary>The names <c>view</c> takes, each with its direction: the two corners, then head-on along each axis.</summary>
    public static readonly IReadOnlyDictionary<string, Vector3> NamedViews = new Dictionary<string, Vector3>
    {
        ["front"] = FrontDirection,
        ["back"] = -FrontDirection,
        ["+x"] = Vector3.UnitX,
        ["-x"] = -Vector3.UnitX,
        ["+y"] = Vector3.UnitY,
        ["-y"] = -Vector3.UnitY,
        ["+z"] = Vector3.UnitZ,
        ["-z"] = -Vector3.UnitZ,
    };

    public static ViewFrame Frame(Vector3 half, RenderView view) => Frame(half, CameraSpec.ForView(view));

    /// <param name="half">The drawn box's largest corner: it is centred on the origin, half-diagonal 1.</param>
    /// <param name="camera">The direction, zoom and target (in drawn space).</param>
    /// <param name="projection">
    /// Orthographic keeps the perspective eye, so near and far stay positive, and takes the smallest
    /// half-height that holds every corner inside the margin; zoom divides that half-height.
    /// </param>
    public static ViewFrame Frame(Vector3 half, CameraSpec camera, Projection projection = Projection.Perspective)
    {
        var direction = Vector3.Normalize(camera.Direction);
        var target = camera.Target ?? Vector3.Zero;
        var up = UpFor(direction);
        var rotation = LookRotation(direction);
        var tangent = MathF.Tan(FieldOfViewY * 0.5f) * (1 - 2 * Margin);

        // The nearest distance at which each corner is inside the margin. The eye stays outside the
        // bounding sphere, so the near plane is positive whatever the shape.
        var distance = 1.2f;
        var reach = 0f;
        for (var corner = 0; corner < 8; corner++)
        {
            var offset = new Vector3(
                (corner & 1) == 0 ? -half.X : half.X,
                (corner & 2) == 0 ? -half.Y : half.Y,
                (corner & 4) == 0 ? -half.Z : half.Z);
            var seen = Vector3.Transform(offset - target, rotation); // Camera space: looking down -Z.
            distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
            reach = Math.Max(reach, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)));
        }

        // Off centre, the eye is that much further out, so it stays outside the bounding sphere.
        var offCentre = target.Length();
        var eye = target + direction * (distance + offCentre);
        var lookAt = Matrix4x4.CreateLookAt(eye, target, up);
        Matrix4x4.Invert(lookAt, out var world);

        var near = (distance - 1) * 0.9f;
        var far = (distance + offCentre + 1) * 1.1f + offCentre;
        Matrix4x4 matrix;
        if (projection == Projection.Orthographic)
        {
            var height = reach / (1 - 2 * Margin) / camera.Zoom;
            matrix = Matrix4x4.CreateOrthographic(2 * height, 2 * height, near, far);
        }
        else
        {
            // Zoom divides the tangent of half the field of view; it never moves the eye.
            var fov = camera.Zoom == 1f ? FieldOfViewY : 2 * MathF.Atan(MathF.Tan(FieldOfViewY * 0.5f) / camera.Zoom);
            matrix = Matrix4x4.CreatePerspectiveFieldOfView(fov, 1f, near, far);
        }

        return new ViewFrame(eye, world, lookAt, matrix, LightOrientation(direction));
    }

    /// <summary>
    /// The light keeps its place relative to the camera (<see cref="RenderLights.Orientation"/>, shared with patina's swatch).
    /// </summary>
    public static Quaternion LightOrientation(Vector3 direction) => RenderLights.Orientation(direction);

    public static Quaternion LightOrientation(RenderView view) => LightOrientation(CameraSpec.ForView(view).Direction);

    /// <summary>+Y, or -Z when the unit direction is within 1 degree of vertical.</summary>
    public static Vector3 UpFor(Vector3 direction) => RenderLights.UpFor(direction);

    /// <summary>World to camera, rotation only.</summary>
    private static Matrix4x4 LookRotation(Vector3 direction) =>
        Matrix4x4.CreateLookAt(direction, Vector3.Zero, UpFor(direction)) with { M41 = 0, M42 = 0, M43 = 0 };
}
