namespace Carve.Core.Baking;

public static partial class Bake
{
    /// <summary>The refusal for a width that is not a finite number of metres above 0.</summary>
    public const string CurvatureWidthProblem = "curvature.width: must be a number of metres greater than 0";

    /// <summary>
    /// The curvature map (T-126): one channel, 0.5 where a texel is far from every break edge. Each break edge of the texel's face
    /// within <paramref name="width"/> metres (<see cref="BakeLayout.NearEdges"/>, in its order) adds
    /// <c>0.5 · s · min(θ / (π/2), 1) · w</c>, where <c>s</c> is +1 for a convex edge and −1 for a concave one (0 for a flat one),
    /// <c>θ</c> is the edge's bend in radians and <c>w = smoothstep(width, 0, d)</c> falls from 1 on the edge to 0 at
    /// <paramref name="width"/>. The sum is clamped to 0..1, so a right-angle convex edge reads 1 and a right-angle crease 0.
    /// An uncovered texel is 0. The image is not padded (<see cref="Pad"/>).
    /// </summary>
    public static BakeImage Curvature(BakeLayout layout, double width)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!(width > 0) || !double.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(width), CurvatureWidthProblem);
        var image = layout.NewImage(1);
        foreach (var texel in layout.Texels())
        {
            var value = 0.5;
            foreach (var e in layout.NearEdges(texel, width))
            {
                var s = e.Shape switch { EdgeShape.Convex => 1.0, EdgeShape.Concave => -1.0, _ => 0.0 };
                var t = Math.Clamp(1 - e.Distance / width, 0, 1);
                value += 0.5 * s * Math.Min(e.Bend / (Math.PI / 2), 1) * (t * t * (3 - 2 * t));
            }
            image[texel.X, texel.Y] = (float)Math.Clamp(value, 0, 1);
        }
        return image;
    }
}
