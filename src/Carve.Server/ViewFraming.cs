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

    /// <summary>
    /// How far past the margin line a corner may land, in device units: what rounding alone needs.
    /// Measured over the clean bounds in <c>ViewFramingTests</c>: at most 8.3e-7.
    /// </summary>
    public const float MarginSlack = 1e-6f;

    /// <summary>
    /// How far a corner may land from where exact arithmetic puts it, in device units: the 0.1 that
    /// T-4 holds a side-1 box to at 1e6. Past this, float rounding is drawing a different shape, and
    /// that shape merely fitting the frame proves nothing; a collapsed mesh fits it very well.
    /// </summary>
    public const float RoundingTolerance = 0.1f;

    /// <summary>Tries at backing the camera off, each from what the last one measured.</summary>
    private const int Attempts = 8;

    /// <summary>
    /// How many times over a clip coordinate must still fit a float. Measured on the real render:
    /// a cube of side 1e34 draws right, 5e34 and 1e35 lose pixels and 1e36 is blank, with every matrix
    /// finite, so the GPU's own arithmetic overflows about this far above the coordinates it is given.
    /// </summary>
    private const float ClipHeadroom = 8192;

    /// <summary>
    /// A shader squares a corner's distance from the camera. Measured: a cube of side 3e-20 draws
    /// right, 2e-20 changes and 1e-30 is blank, which is that square going below a normal float.
    /// </summary>
    private const float SmallestNormal = 1.17549435e-38f;

    private const double Inner = 1 - 2 * (double)Margin;

    private static readonly Vector3 FrontDirection = Vector3.Normalize(new Vector3(1.6f, 1.2f, 2.2f));

    /// <summary>The agreed look in the front view: top bright, +Z middling, +X dark.</summary>
    private static readonly Matrix4x4 FrontLight = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);

    /// <summary>
    /// Computes in double, where the square of any float is finite, and measures in float, as what
    /// is drawn. Rounding can push a corner out, so the camera backs off by what the measurement
    /// says; nothing else moves, the mesh least of all.
    /// </summary>
    public static ViewFrame Frame(Vector3 min, Vector3 max, RenderView view)
    {
        var low = new Point(min);
        var high = new Point(max);
        var target = (low + high) * 0.5;
        var half = (high - low) * 0.5;

        // A point has no size to fit, so this only keeps the camera somewhere else.
        var radius = half.Length;
        if (radius == 0)
        {
            var largest = Math.Max(Math.Abs(low.X), Math.Max(Math.Abs(low.Y), Math.Abs(low.Z)));
            radius = Math.Max(1e-6, largest * 1e-4);
        }

        var rotation = LookRotation(Direction(view));
        var camera = new Axes(rotation);
        var tangent = Math.Tan(FieldOfViewY * 0.5) * Inner;

        // The nearest distance at which each corner is inside the margin. The eye stays outside the
        // bounding sphere, so the near plane is positive whatever the shape.
        var distance = radius * 1.2;
        foreach (var offset in Corners(half * -1, half))
        {
            var (x, y, z) = (camera.Right.Dot(offset), camera.Up.Dot(offset), camera.Back.Dot(offset)); // Looking down -Z.
            distance = Math.Max(distance, Math.Max(Math.Abs(x), Math.Abs(y)) / tangent + z);
        }

        var corners = Corners(low, high).ToArray();
        var light = LightOrientation(view);
        for (var attempt = 1; ; attempt++)
        {
            var eye = target + camera.Back * distance;
            var world = Matrix4x4.Transpose(rotation) with { M41 = (float)eye.X, M42 = (float)eye.Y, M43 = (float)eye.Z };

            // First the plain float look-at, which holds for an ordinary mesh; it fails the
            // measurement, or overflows, where the exact axes do not.
            if (attempt == 1)
            {
                var plain = target.ToFloat() + Direction(view) * (float)distance;
                Matrix4x4.Invert(Matrix4x4.CreateLookAt(plain, target.ToFloat(), Vector3.UnitY), out world);
                eye = new Point(plain);
            }

            var inverted = Matrix4x4.Invert(world, out var seen);
            var projection = Perspective((distance - radius) * 0.9, (distance + radius) * 1.1);

            var (tightest, sound) = Measure(corners, seen * projection, camera, eye, projection.M11);
            sound = sound && inverted && IsFinite(world) && IsFinite(seen) && IsFinite(projection);

            var inside = tightest <= Inner + MarginSlack;
            if ((sound && inside) || attempt == Attempts || (!sound && attempt > 1))
            {
                return new ViewFrame(eye.ToFloat(), target.ToFloat(), world, seen, projection, light, sound && inside);
            }

            if (attempt > 1)
            {
                // Twice the excess: rounding moves with the camera, so aiming at the line misses it half the time.
                distance *= 1 + 2 * (tightest / Inner - 1);
            }
        }
    }

    /// <summary>What <c>render</c> says for bounds that <see cref="Frame"/> could not frame.</summary>
    public static string Refusal(Vector3 min, Vector3 max)
    {
        var (low, high) = (new Point(min), new Point(max));
        var size = high - low;
        var side = Math.Max(size.X, Math.Max(size.Y, size.Z));
        var offset = ((low + high) * 0.5).Length;
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
    /// The outcome: the tightest corner in device units, and whether every corner is finite, between
    /// the near and far planes, within <see cref="RoundingTolerance"/> of where it belongs, and in
    /// the float range the GPU can draw it in. No size or offset is compared with anything.
    /// </summary>
    private static (double Tightest, bool Sound) Measure(Point[] corners, Matrix4x4 worldToClip, Axes camera, Point eye, float focal)
    {
        var tightest = 0.0;
        foreach (var corner in corners)
        {
            var clip = Vector4.Transform(new Vector4(corner.ToFloat(), 1), worldToClip);
            if (!float.IsFinite(clip.X + clip.Y + clip.Z + clip.W) || clip.W <= 0 || clip.Z < 0 || clip.Z > clip.W)
            {
                return (double.PositiveInfinity, false);
            }

            if (!float.IsFinite(clip.W * ClipHeadroom))
            {
                return (double.PositiveInfinity, false);
            }

            var (x, y) = ((double)clip.X / clip.W, (double)clip.Y / clip.W);
            tightest = Math.Max(tightest, Math.Max(Math.Abs(x), Math.Abs(y)));

            if (Vector3.DistanceSquared(corner.ToFloat(), eye.ToFloat()) < SmallestNormal)
            {
                return (tightest, false);
            }

            var from = corner - new Point(eye.ToFloat()); // Where the camera really is.
            var depth = -camera.Back.Dot(from);
            if (Math.Abs(x - camera.Right.Dot(from) * focal / depth) > RoundingTolerance
                || Math.Abs(y - camera.Up.Dot(from) * focal / depth) > RoundingTolerance)
            {
                return (tightest, false);
            }
        }

        return (tightest, true);
    }

    private static Matrix4x4 Perspective(double near, double far)
    {
        var focal = (float)(1 / Math.Tan(FieldOfViewY * 0.5));
        var range = near - far;
        return new Matrix4x4
        {
            M11 = focal,
            M22 = focal,
            M33 = (float)(far / range),
            M34 = -1,
            M43 = (float)(near * far / range),
        };
    }

    private static bool IsFinite(Matrix4x4 m) =>
        float.IsFinite(m.M11 + m.M12 + m.M13 + m.M14)
        && float.IsFinite(m.M21 + m.M22 + m.M23 + m.M24)
        && float.IsFinite(m.M31 + m.M32 + m.M33 + m.M34)
        && float.IsFinite(m.M41) && float.IsFinite(m.M42) && float.IsFinite(m.M43) && float.IsFinite(m.M44);

    private static IEnumerable<Point> Corners(Point low, Point high)
    {
        for (var corner = 0; corner < 8; corner++)
        {
            yield return new Point(
                (corner & 1) == 0 ? low.X : high.X,
                (corner & 2) == 0 ? low.Y : high.Y,
                (corner & 4) == 0 ? low.Z : high.Z);
        }
    }

    private static Vector3 Direction(RenderView view) => view == RenderView.Front ? FrontDirection : -FrontDirection;

    /// <summary>World to camera, rotation only.</summary>
    private static Matrix4x4 LookRotation(Vector3 direction) =>
        Matrix4x4.CreateLookAt(direction, Vector3.Zero, Vector3.UnitY) with { M41 = 0, M42 = 0, M43 = 0 };

    /// <summary>The camera's axes in world space.</summary>
    private readonly struct Axes(Matrix4x4 rotation)
    {
        public Point Right { get; } = new(rotation.M11, rotation.M21, rotation.M31);

        public Point Up { get; } = new(rotation.M12, rotation.M22, rotation.M32);

        public Point Back { get; } = new(rotation.M13, rotation.M23, rotation.M33);
    }

    /// <summary>A point in double: no float's square overflows or underflows here.</summary>
    private readonly record struct Point(double X, double Y, double Z)
    {
        public Point(Vector3 v)
            : this(v.X, v.Y, v.Z)
        {
        }

        public double Length => Math.Sqrt(Dot(this));

        public double Dot(Point other) => X * other.X + Y * other.Y + Z * other.Z;

        public Vector3 ToFloat() => new((float)X, (float)Y, (float)Z);

        public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Point operator *(Point a, double b) => new(a.X * b, a.Y * b, a.Z * b);
    }
}
