namespace Carve.Scenario.Patina;

/// <summary>
/// RV-68's rule that an export at a larger size is the same picture as the graph's own size (approved 2026-11-18): each
/// small texel is compared, per RGB channel, with the mean of the 2 x 2 large texels at its centre (for 256 against 1024,
/// rows and columns 4x+1..4x+2), and every texel of every output but the normal must be within <see cref="Tolerance"/> levels. On the
/// planks the good export's largest difference is 2.0 (base colour) and 0.5 (occlusion); a half-tile shift gives 31, a
/// one-stave shift 35, a u-mirror 26.5 and a flat map 109 and 86 (roughness: good 1.0, faults 27 or more). The normal is
/// not under it: its good 1024 export differs by 30, which this rule does not settle (raised in RV-68). Linked by
/// tests/Patina.Server.Tests; RGBA8 data only.
/// </summary>
internal static class ExportMatch
{
    public const double Tolerance = 4;

    /// <summary>The largest per-channel difference between a small texel and the mean of the four large texels at its centre.</summary>
    public static double LargestDifference(byte[] small, int smallSize, byte[] large, int largeSize)
    {
        if (largeSize % smallSize != 0 || largeSize / smallSize < 2) throw new ArgumentException($"{largeSize} is not a multiple of {smallSize} by 2 or more");
        var f = largeSize / smallSize;
        var c = f / 2 - 1;
        double largest = 0;
        for (var y = 0; y < smallSize; y++)
            for (var x = 0; x < smallSize; x++)
                for (var ch = 0; ch < 3; ch++)
                {
                    int At(int dy, int dx) => large[((y * f + c + dy) * largeSize + x * f + c + dx) * 4 + ch];
                    var mean = (At(0, 0) + At(0, 1) + At(1, 0) + At(1, 1)) / 4.0;
                    largest = Math.Max(largest, Math.Abs(mean - small[(y * smallSize + x) * 4 + ch]));
                }
        return largest;
    }

    public static bool Matches(byte[] small, int smallSize, byte[] large, int largeSize) => LargestDifference(small, smallSize, large, largeSize) <= Tolerance;

    /// <summary>
    /// The faults the rule must catch, each made on a square RGBA8 image of one output: a half-tile shift, a u-mirror and a
    /// flat map. The planks' occlusion is its own half-tile shift and its own u-mirror (largest difference 0.5 against the
    /// unchanged picture), so a shift or mirror leaves it the same picture and only the flat map is a fault there.
    /// </summary>
    public static IReadOnlyList<(string Name, byte[] Data)> Faults(string role, byte[] image, int size) => role == "occlusion"
        ? [("flat", Flat(image, size))]
        :
        [
            ("half-tile shift", Shift(image, size, size / 2, size / 2)),
            ("u-mirror", Mirror(image, size)),
            ("flat", Flat(image, size)),
        ];

    private static byte[] Shift(byte[] image, int size, int dx, int dy)
    {
        var result = new byte[image.Length];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                Array.Copy(image, (y * size + x) * 4, result, (((y + dy) % size) * size + (x + dx) % size) * 4, 4);
        return result;
    }

    private static byte[] Mirror(byte[] image, int size)
    {
        var result = new byte[image.Length];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                Array.Copy(image, (y * size + x) * 4, result, (y * size + size - 1 - x) * 4, 4);
        return result;
    }

    private static byte[] Flat(byte[] image, int size)
    {
        var mean = new byte[4];
        for (var ch = 0; ch < 4; ch++)
        {
            long sum = 0;
            for (var i = ch; i < image.Length; i += 4) sum += image[i];
            mean[ch] = (byte)Math.Round(sum / (double)(size * size));
        }
        var result = new byte[image.Length];
        for (var i = 0; i < result.Length; i++) result[i] = mean[i % 4];
        return result;
    }
}
