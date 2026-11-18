using System.Numerics;

namespace Carve.Scenario.Patina;

/// <summary>
/// What the sphere view must show, worked from the geometry alone (T-114): for a pixel of the 512 px frame, the ray from
/// swatch's fixed camera, turned into the sphere's own space, and where it meets the engine's UV sphere as the engine
/// builds it, with the (u, v) there. It predicts which texel a pixel shows and how far mip filtering can blur it; it does
/// not predict a lit shade.
/// </summary>
/// <remarks>
/// The sphere is the engine's <c>PrimitiveMesh.Sphere</c>, <c>PrimitiveMeshData.Sphere(32, 16)</c> in Empyrean 0.129.0,
/// restated here: stack i (0..16) at latitude pi/2 - i pi/16, sector j (0..32) at azimuth 2 pi j / 32, position
/// (0.5 cos lat cos az, 0.5 sin lat, 0.5 cos lat sin az), uv (j / 32, i / 16); two triangles a quad. So u runs once around
/// from +x toward +z, and v from the top pole down. The camera is swatch's as its description gives it: eye (0, 0, 2), looking at
/// the origin, +y up, a 0.6 rad vertical field, square; the sphere turns by angle degrees about +y, counter-clockwise from above.
/// </remarks>
internal sealed class SwatchSphere
{
    public const int Size = 512;
    private const int Sectors = 32;
    private const int Stacks = 16;
    private const float Radius = 0.5f;
    private static readonly Vector3 Eye = new(0, 0, 2);
    private static readonly float HalfField = MathF.Tan(0.3f);

    private readonly float _cos;
    private readonly float _sin;

    public SwatchSphere(float angleDegrees)
    {
        var a = angleDegrees * MathF.PI / 180;
        _cos = MathF.Cos(a);
        _sin = MathF.Sin(a);
    }

    /// <summary>A point of the frame, in pixels from the top-left corner (a pixel's centre is x + 0.5).</summary>
    public Hit? Trace(float px, float py)
    {
        var direction = Vector3.Normalize(new Vector3((px / Size * 2 - 1) * HalfField, (1 - py / Size * 2) * HalfField, -1));
        // Into the sphere's own space: the inverse of its turn by angle about +y.
        var o = Unturn(Eye);
        var d = Unturn(direction);
        var b = Vector3.Dot(o, d);
        var c = o.LengthSquared() - Radius * Radius;
        var disc = b * b - c;
        if (disc < 0) return null;
        var near = o + (-b - MathF.Sqrt(disc)) * d;
        var lat = MathF.Asin(Math.Clamp(near.Y / Radius, -1, 1));
        var az = MathF.Atan2(near.Z, near.X);
        if (az < 0) az += 2 * MathF.PI;
        var i0 = (int)MathF.Floor((MathF.PI / 2 - lat) / (MathF.PI / Stacks));
        var j0 = (int)MathF.Floor(az / (2 * MathF.PI / Sectors));

        Hit? best = null;
        for (var k = i0 - 1; k <= i0 + 1; k++)
        {
            if (k < 0 || k >= Stacks) continue;
            for (var dl = -1; dl <= 1; dl++)
            {
                var l = ((j0 + dl) % Sectors + Sectors) % Sectors;
                var (pa, ua) = Vertex(k, l);
                var (pb, ub) = Vertex(k, l + 1);
                var (pc, uc) = Vertex(k + 1, l);
                var (pd, ud) = Vertex(k + 1, l + 1);
                foreach (var (p0, p1, p2, t0, t1, t2) in new[] { (pa, pc, pb, ua, uc, ub), (pb, pc, pd, ub, uc, ud) })
                {
                    if (Intersect(o, d, p0, p1, p2) is { } h && (best is null || h.T < best.T))
                    {
                        var uv = t0 * (1 - h.U - h.V) + t1 * h.U + t2 * h.V;
                        var normal = Vector3.Normalize(o + h.T * d);
                        best = new Hit(h.T, uv.X, uv.Y, Vector3.Dot(normal, -d));
                    }
                }
            }
        }
        return best;
    }

    private Vector3 Unturn(Vector3 p) => new(p.X * _cos - p.Z * _sin, p.Y, p.X * _sin + p.Z * _cos);

    private static (Vector3 Position, Vector2 Uv) Vertex(int stack, int sector)
    {
        var lat = MathF.PI / 2 - stack * MathF.PI / Stacks;
        var ring = Radius * MathF.Cos(lat);
        var az = sector * 2 * MathF.PI / Sectors;
        return (new Vector3(ring * MathF.Cos(az), Radius * MathF.Sin(lat), ring * MathF.Sin(az)), new Vector2((float)sector / Sectors, (float)stack / Stacks));
    }

    private static (float T, float U, float V)? Intersect(Vector3 o, Vector3 d, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        var e1 = p1 - p0;
        var e2 = p2 - p0;
        var p = Vector3.Cross(d, e2);
        var det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-12f) return null;
        var inv = 1 / det;
        var s = o - p0;
        var u = Vector3.Dot(s, p) * inv;
        if (u < -1e-6f || u > 1 + 1e-6f) return null;
        var q = Vector3.Cross(s, e1);
        var v = Vector3.Dot(d, q) * inv;
        if (v < -1e-6f || u + v > 1 + 1e-6f) return null;
        var t = Vector3.Dot(e2, q) * inv;
        return t > 0 ? (t, u, v) : null;
    }

    /// <summary>Where a pixel's ray meets the sphere: its distance, (u, v), and the cosine between the surface normal and the view.</summary>
    public sealed record Hit(float T, float U, float V, float Facing);
}
