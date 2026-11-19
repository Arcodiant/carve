using System.Numerics;

namespace Carve.Core;

/// <summary>
/// Whether a transform is a translation, rotation and scale (RV-72): the one test export (a moving part under a static
/// ancestor, T-95) and get_mesh (a part's <c>world</c>, RV-71) both use, so what export refuses as a shear is what
/// get_mesh warns of.
/// </summary>
public static class Trs
{
    /// <summary>
    /// <see cref="Matrix4x4.Decompose"/> of <paramref name="m"/>, true only when the scale and rotation it gives rebuild
    /// <paramref name="m"/>'s linear part to within 1e-5 of its trace (at least 1). The outs are Decompose's, whatever the
    /// answer; the translation is <paramref name="m"/>'s own, so it always rebuilds.
    /// </summary>
    public static bool Decomposes(Matrix4x4 m, out Vector3 scale, out Quaternion rotation, out Vector3 translation)
    {
        if (!Matrix4x4.Decompose(m, out scale, out rotation, out translation)) return false;
        var rebuilt = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation);
        var tolerance = 1e-5f * MathF.Max(1f, MathF.Abs(m.M11) + MathF.Abs(m.M22) + MathF.Abs(m.M33));
        for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
                if (MathF.Abs(rebuilt[i, j] - m[i, j]) > tolerance) return false;
        return true;
    }
}
