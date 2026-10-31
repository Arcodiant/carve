using System.Globalization;
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
/// <param name="World">Camera to world, for the camera's <c>WorldTransform</c>.</param>
/// <param name="View">World to camera: the inverse of <paramref name="World"/>, as the engine takes it.</param>
/// <param name="Projection">For <c>Camera.ProjectionMatrix</c>.</param>
/// <param name="LightOrientation">For the directional light's transform.</param>
/// <param name="Framed">
/// Whether these matrices, in float, put every corner of the bounds where the frame needs it. When
/// false the matrices may hold anything, NaN included, and must not reach the engine.
/// </param>
public sealed record ViewFrame(
    Vector3 Eye,
    Vector3 Target,
    Matrix4x4 World,
    Matrix4x4 View,
    Matrix4x4 Projection,
    Quaternion LightOrientation,
    bool Framed);

/// <summary>The mesh shown can't be framed in this view. The message is for the agent, as it is.</summary>
internal sealed class MeshNotFramedException(string message) : Exception(message);

/// <summary>Pure CPU: from the bounds and the view, the camera that frames them and the light.</summary>
public static class ViewFraming
{
    public const float FieldOfViewY = 0.6f;

    /// <summary>The part of the frame left clear on each side, at the tightest edge.</summary>
    public const float Margin = 0.08f;

    /// <summary>At most the gap between neighbouring floats, as a fraction of their value.</summary>
    private const float FloatStep = 1.1920929e-7f;

    /// <summary>The frame's half width left once the margin is off, in device units.</summary>
    private const float Inner = 1 - 2 * Margin;

    private static readonly Vector3 FrontDirection = Vector3.Normalize(new Vector3(1.6f, 1.2f, 2.2f));

    /// <summary>The agreed look in the front view: top bright, +Z middling, +X dark.</summary>
    private static readonly Matrix4x4 FrontLight = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);

    public static ViewFrame Frame(Vector3 min, Vector3 max, RenderView view)
    {
        var target = (min + max) * 0.5f;
        var half = (max - min) * 0.5f;

        var radius = half.Length();
        var largest = Math.Max(Math.Abs(min.X), Math.Max(Math.Abs(min.Y), Math.Abs(min.Z)));
        largest = Math.Max(largest, Math.Max(Math.Abs(max.X), Math.Max(Math.Abs(max.Y), Math.Abs(max.Z))));

        // Only when floats cannot tell the bounds from a point: then the radius just keeps the
        // camera somewhere else. Whether that shows the mesh is for the check to say.
        if (radius <= largest * FloatStep)
        {
            radius = Math.Max(1e-6f, largest * 1e-4f);
        }

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
        var lookAt = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
        Matrix4x4.Invert(lookAt, out var world);

        // The engine throws on planes it can't use, and these can be NaN at float's far end.
        var (near, far) = ((distance - radius) * 0.9f, (distance + radius) * 1.1f);
        var usable = near > 0 && near < far && float.IsFinite(far);
        var projection = usable ? Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewY, 1f, near, far) : default;

        var framed = usable && Fits(min, max, world, projection);
        return new ViewFrame(eye, target, world, lookAt, projection, LightOrientation(view), framed);
    }

    /// <summary>What <c>render</c> says for bounds that <see cref="Frame"/> could not frame.</summary>
    public static string Refusal(Vector3 min, Vector3 max)
    {
        var side = Math.Max((double)max.X - min.X, Math.Max((double)max.Y - min.Y, (double)max.Z - min.Z));
        var (x, y, z) = (((double)min.X + max.X) * 0.5, ((double)min.Y + max.Y) * 0.5, ((double)min.Z + max.Z) * 0.5);
        var offset = Math.Sqrt(x * x + y * y + z * z);
        return $"This mesh can't be framed: side {Short(side)}, about {Short(offset)} from the origin. "
            + "Move it nearer the origin or scale it, then call define_mesh again.";
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

    private static string Short(double value) =>
        value.ToString("G3", CultureInfo.InvariantCulture).Replace('E', 'e');

    /// <summary>
    /// The outcome, worked as the engine works it: its view is the inverse of the world transform it
    /// is given, and the shader applies that and then the projection, each rounding on its own.
    /// Every corner must be finite and between the near and far planes, and the widest must land
    /// within <see cref="Margin"/> of the margin line, device units, so neither crop nor collapse
    /// passes. No size or offset is compared with anything.
    /// </summary>
    private static bool Fits(Vector3 min, Vector3 max, Matrix4x4 world, Matrix4x4 projection)
    {
        Matrix4x4.Invert(world, out var view);

        var widest = 0f;
        for (var corner = 0; corner < 8; corner++)
        {
            var point = new Vector3(
                (corner & 1) == 0 ? min.X : max.X,
                (corner & 2) == 0 ? min.Y : max.Y,
                (corner & 4) == 0 ? min.Z : max.Z);
            var seen = Vector4.Transform(new Vector4(point, 1), view);
            var clip = Vector4.Transform(seen, projection);
            if (!float.IsFinite(clip.X + clip.Y + clip.Z + clip.W) || clip.W <= 0 || clip.Z < 0 || clip.Z > clip.W)
            {
                return false;
            }

            // Shading squares this distance, and a GPU's arithmetic flushes a subnormal to zero.
            if (!float.IsNormal(new Vector3(seen.X, seen.Y, seen.Z).LengthSquared()))
            {
                return false;
            }

            widest = Math.Max(widest, Math.Max(Math.Abs(clip.X), Math.Abs(clip.Y)) / clip.W);
        }

        return widest <= Inner + Margin && widest >= Inner - Margin;
    }

    private static Vector3 Direction(RenderView view) => view == RenderView.Front ? FrontDirection : -FrontDirection;

    /// <summary>World to camera, rotation only.</summary>
    private static Matrix4x4 LookRotation(Vector3 direction) =>
        Matrix4x4.CreateLookAt(direction, Vector3.Zero, Vector3.UnitY) with { M41 = 0, M42 = 0, M43 = 0 };
}
