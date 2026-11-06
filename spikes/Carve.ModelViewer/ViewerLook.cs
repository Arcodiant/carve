using System.Numerics;

namespace Carve.ModelViewer;

/// <summary>The look agreed at planning. Changing any of it is the person's call, not the viewer's.</summary>
internal static class ViewerLook
{
    public const int Size = 512;

    /// <summary>
    /// Toward the sun, in world space, fixed: it never turns with the camera. Each face takes the
    /// cosine of its normal with this, so the shades run +Y, then +Z, then +X. -X faces away and has
    /// the ambient alone. In both agreed views the three visible faces therefore differ: top, +Z and
    /// +X in <c>front</c>, top, +Z and -X in <c>minus-x</c>.
    /// </summary>
    public static readonly Vector3 ToSun = Vector3.Normalize(new Vector3(0.3f, 0.85f, 0.55f));

    /// <summary>
    /// With the ambient below, the widest gaps tried once tonemapped (2.5/0.3, 1.6/0.25, 1.2/0.3 and
    /// 1/0.2 all closer): on the matte grey cube, top 207, +Z 191, +X 171, -X 125 out of 255.
    /// </summary>
    public const float SunIntensity = 2.0f;

    public const float AmbientIntensity = 0.2f;

    /// <summary>The sun's transform: its local -Z is the way the light travels.</summary>
    public static Matrix4x4 SunWorld()
    {
        Matrix4x4.Invert(Matrix4x4.CreateLookAt(ToSun, Vector3.Zero, Vector3.UnitY), out var world);
        return world with { M41 = 0, M42 = 0, M43 = 0 };
    }
}
