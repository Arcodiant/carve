namespace Patina.Graphs.Nodes;

/// <summary>
/// A power function made of basic IEEE operations only, so it gives the same bits on every machine (<c>Math.Pow</c> may
/// not). Accurate to about 1e-12, far below the 8-bit rounding of an output.
/// </summary>
internal static class DeterministicMath
{
    /// <summary><paramref name="x"/> to the power <paramref name="p"/>, for x in 0..1 and p greater than 0.</summary>
    public static double Pow(double x, double p)
    {
        if (x <= 0) return 0;
        if (x == 1 || p == 1) return x;
        // log2 x: split x = m * 2^e with m in [sqrt(1/2), sqrt(2)), then ln m by the series of atanh.
        var bits = BitConverter.DoubleToInt64Bits(x);
        var e = (int)((bits >> 52) & 0x7FF) - 1023;
        var m = BitConverter.Int64BitsToDouble((bits & 0xFFFFFFFFFFFFFL) | 0x3FF0000000000000L);
        if (m > 1.4142135623730951) { m *= 0.5; e++; }
        var z = (m - 1) / (m + 1);
        var z2 = z * z;
        var series = 0.0;
        for (var k = 21; k >= 1; k -= 2) series = series * z2 + 1.0 / k;
        var lnM = 2 * z * series;
        const double Ln2 = 0.6931471805599453;
        var y = p * (e + lnM / Ln2);           // log2 of the result
        var n = Math.Floor(y);
        var t = (y - n) * Ln2;                 // 2^frac = e^t, t in [0, ln 2)
        var term = 1.0; var sum = 1.0;
        for (var k = 1; k <= 18; k++) { term *= t / k; sum += term; }
        return Math.ScaleB(sum, (int)n);
    }
}
