using System.Numerics;

namespace Carve.Scenario.Crate;

/// <summary>
/// Worked out here, not asked of the product: where a point of the mesh lands in a perspective frame,
/// in pixels from the top left, under <c>render</c>'s framing rule for the box <c>min</c> to <c>max</c>.
/// A pixel covers x to x + 1, so <c>(int)Math.Floor</c> of a coordinate is the pixel holding the point.
/// </summary>
internal static class CameraPixels
{
    private const float FieldOfViewY = 0.6f;
    private const float Margin = 0.08f;
    private static readonly float VerticalCosine = MathF.Cos(MathF.PI / 180f);

    /// <summary>
    /// The camera of <paramref name="direction"/> (from the point looked at toward the eye), up +Y, or -Z when the
    /// direction is within 1 degree of vertical (render's <c>ViewFraming.UpFor</c>).
    /// </summary>
    public static Func<Vector3, Vector2> Perspective(Vector3 min, Vector3 max, Vector3 direction, int size, float zoom = 1, Vector3? target = null)
    {
        var (centre, radius, eye, look, up) = Place(min, max, direction, target);
        Vector3 Drawn(Vector3 p) => (p - centre) / radius;
        var view = Matrix4x4.CreateLookAt(eye, look, up);
        var field = 2 * MathF.Atan(MathF.Tan(FieldOfViewY / 2) / zoom);
        var camera = view * Matrix4x4.CreatePerspectiveFieldOfView(field, 1, 0.01f, 100f);
        return p =>
        {
            var device = Vector4.Transform(new Vector4(Drawn(p), 1), camera);
            return new Vector2((device.X / device.W + 1) / 2 * size, (1 - device.Y / device.W) / 2 * size);
        };
    }

    /// <summary>Where the eye of <see cref="Perspective"/> is, in the mesh's coordinates: what a ray test of what is in sight starts from.</summary>
    public static Vector3 Eye(Vector3 min, Vector3 max, Vector3 direction, Vector3? target = null)
    {
        var (centre, radius, eye, _, _) = Place(min, max, direction, target);
        return centre + eye * radius;
    }

    /// <summary>The camera in the drawn coordinates (the box's centre at the origin, its half-diagonal 1): eye, point looked at, up.</summary>
    private static (Vector3 Centre, float Radius, Vector3 Eye, Vector3 Look, Vector3 Up) Place(Vector3 min, Vector3 max, Vector3 direction, Vector3? target)
    {
        var centre = (min + max) / 2;
        var radius = (max - min).Length() / 2;
        Vector3 Drawn(Vector3 p) => (p - centre) / radius;

        var unit = Vector3.Normalize(direction);
        var look = target is { } t ? Drawn(t) : Vector3.Zero;
        var up = MathF.Abs(unit.Y) > VerticalCosine ? -Vector3.UnitZ : Vector3.UnitY;
        var rotation = Matrix4x4.CreateLookAt(unit, Vector3.Zero, up) with { M41 = 0, M42 = 0, M43 = 0 };
        var tangent = MathF.Tan(FieldOfViewY / 2) * (1 - 2 * Margin);

        // The nearest distance at which every corner of the box is inside the margin.
        var distance = 1.2f;
        foreach (var x in new[] { min.X, max.X })
        foreach (var y in new[] { min.Y, max.Y })
        foreach (var z in new[] { min.Z, max.Z })
        {
            var seen = Vector3.Transform(Drawn(new Vector3(x, y, z)) - look, rotation);
            distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
        }

        return (centre, radius, look + unit * (distance + look.Length()), look, up);
    }

    /// <summary>The pixel holding a projected point.</summary>
    public static (int X, int Y) Holding(Vector2 pixel) => ((int)MathF.Floor(pixel.X), (int)MathF.Floor(pixel.Y));
}
