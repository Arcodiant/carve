using System.Numerics;

namespace Carve.Core.Baking;

/// <summary>
/// The frame a texel's tangent-space normal is read in, as Empyrean's <c>applyNormalMap</c> builds it (T-120, decided at
/// planning 2026-11-18): no vertex tangent, but a cotangent frame from the derivatives of the world position and the uv,
/// about the interpolated shading normal <see cref="N"/>. Over one triangle that frame is constant up to N. With
/// <c>dp1</c>, <c>dp2</c> the triangle's edges from its first corner (in Carve's winding) and <c>duv1</c>, <c>duv2</c>
/// their uv deltas, the cotangent formula is
/// <c>T = cross(dp2, N) * duv1.x + cross(N, dp1) * duv2.x</c> and <c>B' = cross(dp2, N) * duv1.y + cross(N, dp1) * duv2.y</c>,
/// both scaled by <c>1 / sqrt(max(|T|², |B'|²))</c>. That B' runs along +v, which is down the image; the engine reads
/// green as up (the probe in T-120's Notes: on a box's +Z face with u = x and v = 1 − y, +G leans the normal toward world
/// +y), so <see cref="B"/> is <c>−B'</c>. A world normal is <c>normalize(T * n.x + B * n.y + N * n.z)</c> for a
/// tangent-space normal n; T and B are not unit and need not be orthogonal, and the decoding inverts that exactly.
/// </summary>
/// <param name="T">Red's direction: along +u.</param>
/// <param name="B">Green's direction: along −v, up the image.</param>
/// <param name="N">The unit interpolated shading normal; blue's direction.</param>
public readonly record struct TangentFrame(Vector3 T, Vector3 B, Vector3 N)
{
    /// <summary>
    /// The frame of a triangle with corners <paramref name="p0"/>, <paramref name="p1"/>, <paramref name="p2"/> (in its
    /// winding) and uvs <paramref name="uv0"/>, <paramref name="uv1"/>, <paramref name="uv2"/>, about unit normal
    /// <paramref name="n"/>: the cotangent formula with green flipped up (see the type). Zero T and B when the uv
    /// triangle or the triangle has no area.
    /// </summary>
    public static TangentFrame Of(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector3 n)
    {
        var (t, b) = Cotangent(p0, p1, p2, uv0, uv1, uv2, n);
        return new TangentFrame(t, -b, n);
    }

    /// <summary>
    /// The cotangent formula as written, before green is flipped: T along +u and B' along +v. Public so a test can show
    /// which sign the engine needs; bakes use <see cref="Of"/>.
    /// </summary>
    public static (Vector3 T, Vector3 BAlongV) Cotangent(
        Vector3 p0, Vector3 p1, Vector3 p2, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector3 n)
    {
        var dp1 = p1 - p0;
        var dp2 = p2 - p0;
        var duv1 = uv1 - uv0;
        var duv2 = uv2 - uv0;
        var dp2Perp = Vector3.Cross(dp2, n);
        var dp1Perp = Vector3.Cross(n, dp1);
        var t = dp2Perp * duv1.X + dp1Perp * duv2.X;
        var b = dp2Perp * duv1.Y + dp1Perp * duv2.Y;
        var max = MathF.Max(t.LengthSquared(), b.LengthSquared());
        if (!(max > 0)) return (Vector3.Zero, Vector3.Zero);
        var scale = 1f / MathF.Sqrt(max);
        return (t * scale, b * scale);
    }

    /// <summary>
    /// The texel value, in glTF's encoding <c>127.5 + 127.5 * n</c> per channel (red, green, blue, 0 to 255), that the
    /// engine reads back as <paramref name="worldNormal"/>: the unit n with <c>T n.x + B n.y + N n.z</c> along it.
    /// The encoded blue of a normal on N's side is above 127.5. A degenerate frame gives flat (127.5, 127.5, 255).
    /// </summary>
    public Vector3 ToTangentSpace(Vector3 worldNormal)
    {
        // Solve [T B N] n = w by Cramer's rule, in double.
        var t = (X: (double)T.X, Y: (double)T.Y, Z: (double)T.Z);
        var b = (X: (double)B.X, Y: (double)B.Y, Z: (double)B.Z);
        var c = (X: (double)N.X, Y: (double)N.Y, Z: (double)N.Z);
        var w = (X: (double)worldNormal.X, Y: (double)worldNormal.Y, Z: (double)worldNormal.Z);
        var det = Det(t, b, c);
        if (det == 0 || !double.IsFinite(det)) return new Vector3(127.5f, 127.5f, 255f);
        var x = Det(w, b, c) / det;
        var y = Det(t, w, c) / det;
        var z = Det(t, b, w) / det;
        var len = Math.Sqrt(x * x + y * y + z * z);
        if (!(len > 0)) return new Vector3(127.5f, 127.5f, 255f);
        return new Vector3((float)(127.5 + 127.5 * x / len), (float)(127.5 + 127.5 * y / len), (float)(127.5 + 127.5 * z / len));
    }

    /// <summary>
    /// The unit world normal the engine lights for a texel holding <paramref name="encoded"/> (red, green, blue, 0 to 255,
    /// glTF's <c>127.5 + 127.5 * n</c>): <c>normalize(T n.x + B n.y + N n.z)</c>.
    /// </summary>
    public Vector3 FromTangentSpace(Vector3 encoded)
    {
        var n = (encoded - new Vector3(127.5f)) / 127.5f;
        var w = T * n.X + B * n.Y + N * n.Z;
        var len = w.Length();
        return len > 0 ? w / len : N;
    }

    private static double Det((double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) c) =>
        a.X * (b.Y * c.Z - b.Z * c.Y) - b.X * (a.Y * c.Z - a.Z * c.Y) + c.X * (a.Y * b.Z - a.Z * b.Y);
}
