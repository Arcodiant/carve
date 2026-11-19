namespace Carve.Scenario.Patina;

/// <summary>
/// RV-68's rule that an export at a larger size is the same picture as the graph's own size (approved 2026-11-18): each
/// small texel is compared, per RGB channel, with the mean of the 2 x 2 large texels at its centre (for 256 against 1024,
/// rows and columns 4x+1..4x+2), and every texel of every output but the normal must be within <see cref="Tolerance"/> levels. On the
/// planks the good export's largest difference is 2.0 (base colour) and 0.5 (occlusion); a half-tile shift gives 31, a
/// one-stave shift 35, a u-mirror 26.5 and a flat map 109 and 86 (roughness: good 1.0, faults 27 or more). The normal has
/// its own rule (<see cref="NormalMatches"/>), since a correct normal differs by up to 30 where it is sampled at the planks'
/// kinks. Linked by tests/Patina.Server.Tests; RGBA8 data only.
/// </summary>
internal static class ExportMatch
{
    public const double Tolerance = 4;

    /// <summary>A 256 texel whose local step is this or less is flat for the normal's rule.</summary>
    public const int NormalFlatStep = 4;

    /// <summary>The normal's allowed difference on a flat texel (the good planks: 0.0).</summary>
    public const double NormalFlatTolerance = 4;

    /// <summary>The normal's allowed difference elsewhere, at a kink (the good planks: 30).</summary>
    public const double NormalKinkTolerance = 32;

    /// <summary>The largest per-channel difference between a small texel and the mean of the four large texels at its centre.</summary>
    public static double LargestDifference(byte[] small, int smallSize, byte[] large, int largeSize)
    {
        var f = Factor(smallSize, largeSize);
        double largest = 0;
        for (var y = 0; y < smallSize; y++)
            for (var x = 0; x < smallSize; x++)
                largest = Math.Max(largest, TexelDifference(small, smallSize, large, largeSize, f, x, y));
        return largest;
    }

    /// <summary>
    /// The normal's rule (RV-68, approved 2026-11-18): a 256 texel's local step is its largest channel difference to any of
    /// its 8 neighbours (wrapping) in the small picture. Where the local step is <see cref="NormalFlatStep"/> or less the
    /// texel is flat and must be within <see cref="NormalFlatTolerance"/>; elsewhere (the stave gap edges and bevel ends,
    /// where height_to_normal's slope is sampled at a kink) within <see cref="NormalKinkTolerance"/>. Gives the largest
    /// difference on flat texels and on the rest, and the share of texels that are flat (the planks: about 62.5 %).
    /// </summary>
    public static (double Flat, double Kink, double FlatShare) NormalDifferences(byte[] small, int smallSize, byte[] large, int largeSize)
    {
        var f = Factor(smallSize, largeSize);
        double flat = 0, kink = 0;
        var flatCount = 0;
        for (var y = 0; y < smallSize; y++)
            for (var x = 0; x < smallSize; x++)
            {
                var difference = TexelDifference(small, smallSize, large, largeSize, f, x, y);
                if (LocalStep(small, smallSize, x, y) <= NormalFlatStep)
                {
                    flat = Math.Max(flat, difference);
                    flatCount++;
                }
                else kink = Math.Max(kink, difference);
            }
        return (flat, kink, flatCount / (double)(smallSize * smallSize));
    }

    public static bool NormalMatches(byte[] small, int smallSize, byte[] large, int largeSize)
    {
        var (flat, kink, _) = NormalDifferences(small, smallSize, large, largeSize);
        return flat <= NormalFlatTolerance && kink <= NormalKinkTolerance;
    }

    /// <summary>A texel's largest RGB channel difference to any of its 8 neighbours, wrapping at the edges.</summary>
    public static int LocalStep(byte[] image, int size, int x, int y)
    {
        var step = 0;
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = (((y + dy + size) % size) * size + (x + dx + size) % size) * 4;
                for (var ch = 0; ch < 3; ch++) step = Math.Max(step, Math.Abs(image[n + ch] - image[(y * size + x) * 4 + ch]));
            }
        return step;
    }

    private static int Factor(int smallSize, int largeSize)
    {
        if (largeSize % smallSize != 0 || largeSize / smallSize < 2) throw new ArgumentException($"{largeSize} is not a multiple of {smallSize} by 2 or more");
        return largeSize / smallSize;
    }

    private static double TexelDifference(byte[] small, int smallSize, byte[] large, int largeSize, int f, int x, int y)
    {
        var c = f / 2 - 1;
        double largest = 0;
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
    /// unchanged picture), so a shift or mirror leaves it the same picture and only the flat map is a fault there. The normal
    /// repeats every stave (an eighth of the tile) and does not vary down the image, so a half-tile or one-stave shift is
    /// invisible in it under any rule (the base colour catches those); its faults are a u-mirror, a flat map and shifts in u
    /// of a quarter stave, of one 256 texel and of half a 256 texel (an image of 1024 against 256).
    /// </summary>
    public static IReadOnlyList<(string Name, byte[] Data)> Faults(string role, byte[] image, int size) => role switch
    {
        "occlusion" => [("flat", Flat(image, size))],
        "normal" =>
        [
            ("u-mirror", Mirror(image, size)),
            ("flat", Flat(image, size)),
            ("quarter-stave shift", Shift(image, size, size / 32, 0)),
            ("one-texel shift", Shift(image, size, size / 256, 0)),
            ("half-texel shift", Shift(image, size, size / 512, 0)),
        ],
        _ =>
        [
            ("half-tile shift", Shift(image, size, size / 2, size / 2)),
            ("u-mirror", Mirror(image, size)),
            ("flat", Flat(image, size)),
        ],
    };

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
