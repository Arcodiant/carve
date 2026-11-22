using System.Globalization;

namespace Patina.Graphs.Checks;

/// <summary>
/// <c>does-not-tile</c>: the mean step (in packed levels, over the channels and over every row) is measured across each
/// column boundary, the wrap's between the last and first column included, and the same for rows in v. The wrap is a
/// fault when its mean step is greater than twice the 99th percentile of the interior boundaries' mean steps plus 2
/// levels, so a pattern's own hard edge that falls on the wrap, which every interior edge matches, is not a seam. Every
/// output is judged, a metallic read from a packed image's blue channel too (RV-76: it is exempt from one-value only).
/// </summary>
public sealed class DoesNotTileRule : ICheckRule
{
    public const string Id = "does-not-tile";
    public const double Factor = 2, Slack = 2, Percentile = 0.99;

    public string Name => Id;

    public IEnumerable<CheckProblem> Run(CheckSet set)
    {
        foreach (var o in set.Outputs.Where(o => o.Width > 1 && o.Height > 1))
        {
            var seams = new List<string>();
            var (wrapU, p99U) = Steps(o, alongX: true);
            if (wrapU > Factor * p99U + Slack) seams.Add($"u (the step across the wrap is {F(wrapU)} levels; the 99th percentile of the steps inside is {F(p99U)})");
            var (wrapV, p99V) = Steps(o, alongX: false);
            if (wrapV > Factor * p99V + Slack) seams.Add($"v (the step across the wrap is {F(wrapV)} levels; the 99th percentile of the steps inside is {F(p99V)})");
            if (seams.Count == 0) continue;
            yield return new(Id, o.Name,
                $"a seam in {string.Join(" and ", seams)}; a wrap step over twice that percentile plus 2 levels is a fault. "
                + "Fix: every node must repeat at the image's edge: use whole-number frequencies and counts, keep turns to quarter turns, "
                + "and look at swatch's tiled view to see where the seam falls.");
        }
    }

    private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>The mean step across the wrap, and the 99th percentile (nearest rank) of the mean steps across the interior boundaries, along one axis.</summary>
    internal static (double Wrap, double InteriorP99) Steps(CheckOutput o, bool alongX)
    {
        var n = alongX ? o.Width : o.Height;
        var lines = alongX ? o.Height : o.Width;
        var sums = new double[n]; // boundary i is between i and i+1; the last is the wrap
        for (var l = 0; l < lines; l++)
            for (var c = 0; c < o.Channels; c++)
            {
                byte At(int i) => alongX ? o.Level(i, l, c) : o.Level(l, i, c);
                var prev = At(0);
                for (var i = 0; i < n - 1; i++)
                {
                    var next = At(i + 1);
                    sums[i] += Math.Abs(next - prev);
                    prev = next;
                }
                sums[n - 1] += Math.Abs(prev - At(0));
            }
        var count = (double)lines * o.Channels;
        var inside = new double[n - 1];
        for (var i = 0; i < n - 1; i++) inside[i] = sums[i] / count;
        Array.Sort(inside);
        var rank = (int)Math.Ceiling(Percentile * inside.Length) - 1;
        return (sums[n - 1] / count, inside[Math.Clamp(rank, 0, inside.Length - 1)]);
    }
}
