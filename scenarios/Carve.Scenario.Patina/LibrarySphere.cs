using Carve.Debug;

namespace Carve.Scenario.Patina;

/// <summary>
/// What a library sample's sphere must show, worked from <see cref="SwatchSphere"/>'s geometry and the exported texels alone
/// (T-142). A lit shade cannot be predicted without a lighting model, but the sphere is round, so a pixel has the same light at
/// every angle: between two angles the pixel's luma changes with the texel under it. Each judged pixel (facing the camera, a
/// neighbour to the right and below also on the sphere) is given a tone: the mean luma of the base colour texels under its
/// footprint (the texels one pixel step spans, at the tiling), times <c>1 - MetalDrop m</c> where m is the metallic texel (T-134
/// measured a grey-200 sphere's median luma 192 at metallic 0 and 127 at 1: 0.34 lower). The rule is the Pearson correlation,
/// over the pixels judged at both angles, between the tone difference and the frame's luma difference, at least
/// <see cref="CorrelationMin"/>; and the silhouette: every pixel whose rays all miss is the slate, and of those whose rays all
/// hit no more than 0.1 % are.
/// </summary>
internal static class LibrarySphere
{
    private const int N = SwatchSphere.Size;
    private const int Size = 256;
    public const float Facing = 0.5f;
    public const double MetalDrop = 0.34;
    /// <summary>Measured 0.896 to 0.994 on the samples without metallic texels, mirrored 0.053 at most (library-checks.tsv); set at about two thirds of the lowest.</summary>
    public const double CorrelationMin = 0.6;
    /// <summary>A sphere with metallic texels must correlate at least this much (painted-metal measured 0.661 with metallic in the prediction and 0.571 without; set between).</summary>
    public const double MetalMin = 0.62;

    private static readonly Dictionary<float, SwatchSphere.Hit?[,]> Hits = [];

    private static SwatchSphere.Hit?[,] HitsAt(float angle)
    {
        if (Hits.TryGetValue(angle, out var hits)) return hits;
        var sphere = new SwatchSphere(angle);
        hits = new SwatchSphere.Hit?[N + 1, N + 1];
        for (var y = 0; y <= N; y++) for (var x = 0; x <= N; x++) hits[x, y] = sphere.Trace(x + 0.5f, y + 0.5f);
        return Hits[angle] = hits;
    }

    /// <summary>Per pixel the predicted tone, NaN where the pixel is not judged.</summary>
    public static double[] Tones(byte[] baseColour, byte[]? roughnessFile, float angle, int tiling, double metalDrop)
    {
        var tone = new double[Size * Size];
        for (var i = 0; i < tone.Length; i++)
        {
            var m = roughnessFile is null ? 0 : roughnessFile[i * 4 + 2] / 255.0;
            tone[i] = SwatchStage.Luma(baseColour.AsSpan(i * 4, 4)) * (1 - metalDrop * m);
        }
        static float Wrapped(float d) => MathF.Abs(d - MathF.Round(d));
        static int Wrap(int v) => ((v % Size) + Size) % Size;
        var hits = HitsAt(angle);
        var result = new double[N * N];
        for (var y = 0; y < N; y++)
        {
            for (var x = 0; x < N; x++)
            {
                result[y * N + x] = double.NaN;
                if (hits[x, y] is not { } hit || hit.Facing < Facing || hits[x + 1, y] is not { } right || hits[x, y + 1] is not { } below) continue;
                var footprint = MathF.Max(
                    MathF.Max(Wrapped((right.U - hit.U) * tiling), Wrapped((below.U - hit.U) * tiling)),
                    MathF.Max(Wrapped((right.V - hit.V) * tiling), Wrapped((below.V - hit.V) * tiling))) * Size;
                var reach = (int)MathF.Ceiling(footprint / 2);
                var tx = (int)MathF.Floor(hit.U * tiling * Size);
                var ty = (int)MathF.Floor(hit.V * tiling * Size);
                double sum = 0;
                for (var dy = -reach; dy <= reach; dy++)
                    for (var dx = -reach; dx <= reach; dx++)
                        sum += tone[Wrap(ty + dy) * Size + Wrap(tx + dx)];
                result[y * N + x] = sum / ((2 * reach + 1) * (2 * reach + 1));
            }
        }
        return result;
    }

    /// <summary>The correlation of the tone difference with the luma difference between two angles, and the pixels it is over.</summary>
    public static (double R, int Pixels) Correlation(double[] toneA, double[] toneB, ScenarioFrame a, ScenarioFrame b)
    {
        int n = 0;
        double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
        for (var at = 0; at < N * N; at++)
        {
            if (double.IsNaN(toneA[at]) || double.IsNaN(toneB[at])) continue;
            var x = toneA[at] - toneB[at];
            var y = SwatchStage.Luma(a.Pixels.AsSpan(at * 4, 4)) - SwatchStage.Luma(b.Pixels.AsSpan(at * 4, 4));
            n++; sx += x; sy += y; sxx += x * x; syy += y * y; sxy += x * y;
        }
        if (n < 1000) return (double.NaN, n);
        var cov = sxy - sx * sy / n;
        var vx = sxx - sx * sx / n;
        var vy = syy - sy * sy / n;
        return (vx <= 0 || vy <= 0 ? 0 : cov / Math.Sqrt(vx * vy), n);
    }

    /// <summary>The silhouette faults of a frame at an angle: outside pixels that are not the slate, and inside ones that are (beyond 0.1 %).</summary>
    public static (int Outside, int SlateInside, int Inside) Silhouette(ScenarioFrame frame, float angle)
    {
        var sphere = new SwatchSphere(angle);
        var hits = HitsAt(angle);
        const float m = SwatchStage.SilhouetteMargin;
        int outside = 0, slateInside = 0, inside = 0;
        for (var y = 0; y < N; y++)
        {
            for (var x = 0; x < N; x++)
            {
                var cx = x + 0.5f;
                var cy = y + 0.5f;
                var ring = new[] { (cx - m, cy - m), (cx + m, cy - m), (cx - m, cy + m), (cx + m, cy + m), (cx - m, cy), (cx + m, cy), (cx, cy - m), (cx, cy + m) }
                    .Select(p => sphere.Trace(p.Item1, p.Item2) is not null).ToArray();
                var hit = hits[x, y];
                if (hit is null && ring.All(h => !h) && !SwatchStage.IsSlate(frame, x, y)) outside++;
                if (hit is not null && ring.All(h => h)) { inside++; if (SwatchStage.IsSlate(frame, x, y)) slateInside++; }
            }
        }
        return (outside, slateInside, inside);
    }
}
