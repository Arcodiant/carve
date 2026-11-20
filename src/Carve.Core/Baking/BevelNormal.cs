using System.Numerics;

namespace Carve.Core.Baking;

public static partial class Bake
{
    /// <summary>
    /// The bevel normal map (T-125): a tangent-space normal map, three channels from 0 to 1 holding glTF's encoding
    /// <c>(127.5 + 127.5 * n) / 255</c> in the engine's own frame (<see cref="TangentFrame"/>, green up), that rounds every
    /// shading-break edge over <paramref name="width"/> metres. Each covered texel starts from its shading normal N, the one
    /// render lights; every break edge within <paramref name="width"/> (<see cref="BakeLayout.NearEdges"/>, nearest first)
    /// pulls it toward that edge's mean normal, <c>normalize(nFace + nOther)</c>, with weight <c>smoothstep(width, 0, d)</c>,
    /// 1 on the edge and 0 at <paramref name="width"/>; the pulls are summed, <c>normalize(N + Σ wᵢ (targetᵢ − N))</c>. A texel
    /// with no edge within reach encodes N, flat (127.5, 127.5, 255). Uncovered texels are 0; pad with <see cref="Pad"/>.
    /// </summary>
    public static BakeImage BevelNormal(BakeLayout layout, double width)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!(width > 0) || !double.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(width), "width must be a finite number of metres, more than 0");
        var image = layout.NewImage(3);
        foreach (var texel in layout.Texels())
        {
            var encoded = texel.Frame.ToTangentSpace(BevelWorldNormal(texel, layout.NearEdges(texel, width), width)) / 255f;
            image[texel.X, texel.Y, 0] = encoded.X;
            image[texel.X, texel.Y, 1] = encoded.Y;
            image[texel.X, texel.Y, 2] = encoded.Z;
        }
        return image;
    }

    /// <summary>
    /// The unit world normal <see cref="BevelNormal"/> writes for <paramref name="texel"/>, given its break edges within
    /// <paramref name="width"/> in the order <see cref="BakeLayout.NearEdges"/> gives them. Worked in double; a sum that
    /// cancels to nothing gives the shading normal.
    /// </summary>
    public static Vector3 BevelWorldNormal(BakeTexel texel, IReadOnlyList<NearEdge> edges, double width)
    {
        ArgumentNullException.ThrowIfNull(edges);
        var n = D(texel.ShadingNormal);
        var face = Unit(D(texel.FaceNormal));
        var sum = n;
        foreach (var edge in edges)
        {
            if (!(edge.Distance < width)) continue;
            var target = Unit(Add(face, Unit(D(edge.OtherNormal))));
            if (target == default) continue;
            var t = Math.Clamp(1 - edge.Distance / width, 0, 1);
            var w = t * t * (3 - 2 * t);
            sum = Add(sum, Scale(Sub(target, n), w));
        }
        var unit = Unit(sum);
        return unit == default ? texel.ShadingNormal : new Vector3((float)unit.X, (float)unit.Y, (float)unit.Z);
    }

    private static (double X, double Y, double Z) D(Vector3 v) => (v.X, v.Y, v.Z);

    private static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static (double X, double Y, double Z) Sub((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, double s) => (a.X * s, a.Y * s, a.Z * s);

    private static (double X, double Y, double Z) Unit((double X, double Y, double Z) a)
    {
        var len = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
        return len > 1e-12 && double.IsFinite(len) ? (a.X / len, a.Y / len, a.Z / len) : default;
    }
}
