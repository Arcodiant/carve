using System.Numerics;

namespace Carve.Core.Baking;

/// <summary>
/// What <see cref="Bake.Occlusion"/> gives: the single-channel image, unpadded (1 open, 0 closed; uncovered texels 0), the
/// offset its rays were lifted by, the rays cast and how many of them met the texel's own face first (none, on a planar face).
/// </summary>
public sealed record OcclusionBake(BakeImage Image, float Offset, long Rays, long SelfHits);

public static partial class Bake
{
    /// <summary>The most rays a texel may cast.</summary>
    public const int MaxRays = 1024;

    /// <summary>The rays a texel casts when the call does not say.</summary>
    public const int DefaultRays = 32;

    /// <summary>
    /// The self-hit offset as a fraction of the model's scale: <see cref="OcclusionOffset"/> is this times the larger of the
    /// bounding box's diagonal and its largest absolute coordinate.
    /// </summary>
    public const float OffsetScale = 1e-5f;

    /// <summary>
    /// How far a ray's origin is lifted off its texel along the face's geometric normal: <see cref="OffsetScale"/> × the larger of
    /// the diagonal of the box around the occluders and the baked mesh, and the largest absolute coordinate of that box. A ray
    /// leaves the face's plane upward (every direction has a positive height over the normal, at least √(0.5 / rays)), so on a
    /// planar face it can only meet its own plane behind its origin; the offset keeps that so when float rounding (a few units in
    /// the last place of the largest coordinate, about 10⁻⁷ of it) moves the origin. At 10⁻⁵ of the scale it is about a hundred
    /// times that rounding and far under a texel: on the wrench, about 2.5 µm.
    /// </summary>
    public static float OcclusionOffset(BakeLayout layout, RayCaster caster)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(caster);
        var lo = caster.Triangles > 0 ? caster.BoundsMin : new Vector3(float.MaxValue);
        var hi = caster.Triangles > 0 ? caster.BoundsMax : new Vector3(float.MinValue);
        foreach (var v in layout.Mesh.Vertices)
        {
            lo = Vector3.Min(lo, v);
            hi = Vector3.Max(hi, v);
        }
        if (lo.X > hi.X) return OffsetScale;
        var largest = Vector3.Max(Vector3.Abs(lo), Vector3.Abs(hi));
        var scale = MathF.Max((hi - lo).Length(), MathF.Max(largest.X, MathF.Max(largest.Y, largest.Z)));
        return OffsetScale * MathF.Max(scale, 1e-3f);
    }

    /// <summary>
    /// The fixed directions about +z, cosine-weighted and stratified: a Fibonacci spiral over the unit disc, lifted onto the
    /// hemisphere. Ray <c>i</c> of <paramref name="rays"/> lies at radius √((i + ½) / rays) and angle i × the golden angle, so each
    /// covers an equal share of the disc and the set's mean is the cosine-weighted mean over the hemisphere.
    /// </summary>
    public static Vector3[] OcclusionDirections(int rays)
    {
        if (rays < 1 || rays > MaxRays) throw new ArgumentOutOfRangeException(nameof(rays), $"rays must be from 1 to {MaxRays}");
        var golden = Math.PI * (3 - Math.Sqrt(5));
        var set = new Vector3[rays];
        for (var i = 0; i < rays; i++)
        {
            var r = Math.Sqrt((i + 0.5) / rays);
            var a = i * golden;
            set[i] = new Vector3((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)), (float)Math.Sqrt(1 - r * r));
        }
        return set;
    }

    /// <summary>
    /// Ambient occlusion over <paramref name="layout"/> against <paramref name="caster"/>'s triangles (T-124). Each covered texel
    /// casts <paramref name="rays"/> rays from its rest position lifted by <see cref="OcclusionOffset"/> along its face's geometric
    /// normal, in <see cref="OcclusionDirections"/> about that normal, the set turned about the normal by an angle from T-77's
    /// integer hash of the texel's index (<c>y × size + x</c>), which trades banding for fine noise. A hit at distance
    /// <c>t &lt; reach</c> occludes by <c>1 − (t / reach)²</c>; the texel's value is 1 − the mean occlusion, with no curve. The
    /// result is unpadded (<see cref="Pad"/>). <paramref name="self"/> is the baked mesh's index among the caster's meshes, or −1,
    /// and is only used to count self-hits.
    /// </summary>
    public static OcclusionBake Occlusion(BakeLayout layout, RayCaster caster, int self, int rays, float reach)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(caster);
        if (!(reach > 0) || !float.IsFinite(reach)) throw new ArgumentOutOfRangeException(nameof(reach), "reach must be over 0");
        var set = OcclusionDirections(rays);
        var offset = OcclusionOffset(layout, caster);
        var image = layout.NewImage(1);
        var size = layout.Size;
        var values = new float[size * size];
        long selfHits = 0, cast = 0;
        Parallel.For(0, size, y =>
        {
            long rowSelf = 0, rowCast = 0;
            for (var x = 0; x < size; x++)
            {
                if (!layout.TryGetTexel(x, y, out var texel)) continue;
                var n = Vector3.Normalize(texel.FaceNormal);
                var (t, b) = Basis(n);
                var angle = Hash(y * size + x) * (2 * Math.PI / 4294967296.0);
                float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                var origin = texel.Position + n * offset;
                double occluded = 0;
                foreach (var d in set)
                {
                    float dx = d.X * cos - d.Y * sin, dy = d.X * sin + d.Y * cos;
                    var direction = Vector3.Normalize(t * dx + b * dy + n * d.Z);
                    if (!caster.Cast(origin, direction, reach, out var hit)) continue;
                    var f = hit.Distance / reach;
                    occluded += 1 - f * f;
                    if (hit.Mesh == self && hit.Face == texel.Face) rowSelf++;
                }
                rowCast += set.Length;
                values[y * size + x] = (float)(1 - occluded / set.Length);
            }
            Interlocked.Add(ref selfHits, rowSelf);
            Interlocked.Add(ref cast, rowCast);
        });
        values.CopyTo(image.Values);
        return new OcclusionBake(image, offset, cast, selfHits);
    }

    /// <summary>
    /// The ORM image over <paramref name="layout"/>, unpadded and linear: R the <paramref name="occlusion"/> texel, G the face's
    /// <paramref name="roughness"/> and B its <paramref name="metallic"/>, both indexed by face id; uncovered texels 0.
    /// </summary>
    public static BakeImage Orm(BakeLayout layout, BakeImage occlusion, IReadOnlyList<float> roughness, IReadOnlyList<float> metallic)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(occlusion);
        ArgumentNullException.ThrowIfNull(roughness);
        ArgumentNullException.ThrowIfNull(metallic);
        if (occlusion.Width != layout.Size || occlusion.Height != layout.Size || occlusion.Channels != 1)
            throw new ArgumentException("the occlusion must be one channel at the layout's size", nameof(occlusion));
        var image = layout.NewImage(3);
        for (var y = 0; y < layout.Size; y++)
        for (var x = 0; x < layout.Size; x++)
        {
            var f = layout.FaceAt(x, y);
            if (f < 0) continue;
            image[x, y, 0] = occlusion[x, y];
            image[x, y, 1] = roughness[f];
            image[x, y, 2] = metallic[f];
        }
        return image;
    }

    /// <summary>A unit tangent and bitangent about the unit <paramref name="n"/> (Duff et al., 2017), the same for one normal every time.</summary>
    private static (Vector3 T, Vector3 B) Basis(Vector3 n)
    {
        var sign = n.Z >= 0 ? 1f : -1f;
        var a = -1f / (sign + n.Z);
        var b = n.X * n.Y * a;
        return (new Vector3(1 + sign * n.X * n.X * a, sign * b, -sign * n.X), new Vector3(b, sign + n.Y * n.Y * a, -n.Y));
    }

    /// <summary>T-77's integer hash (<c>TextureSet.Hash</c> in tools/Carve.SampleTextures/TextureSet.cs) of one index, seed 0, copied.</summary>
    public static uint Hash(int index)
    {
        var h = (uint)index * 0x9E3779B1u;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return h;
    }
}
