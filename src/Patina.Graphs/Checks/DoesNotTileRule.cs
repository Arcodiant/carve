using System.Globalization;

namespace Patina.Graphs.Checks;

/// <summary>
/// <c>does-not-tile</c>: across the wrap in u or v, the mean step (in packed levels, over the channels) between the last
/// and first column (or row) is greater than twice the interior mean step plus 2 levels.
/// </summary>
public sealed class DoesNotTileRule : ICheckRule
{
    public const string Id = "does-not-tile";
    public const double Factor = 2, Slack = 2;

    public string Name => Id;

    public IEnumerable<CheckProblem> Run(CheckSet set)
    {
        foreach (var o in set.Outputs.Where(o => !o.Derived && o.Width > 1 && o.Height > 1))
        {
            var seams = new List<string>();
            var (wrapU, innerU) = Steps(o, alongX: true);
            if (wrapU > Factor * innerU + Slack) seams.Add($"u (the step across the wrap is {F(wrapU)} levels against {F(innerU)} inside)");
            var (wrapV, innerV) = Steps(o, alongX: false);
            if (wrapV > Factor * innerV + Slack) seams.Add($"v (the step across the wrap is {F(wrapV)} levels against {F(innerV)} inside)");
            if (seams.Count == 0) continue;
            yield return new(Id, o.Name,
                $"a seam in {string.Join(" and ", seams)}; a wrap step over twice the inside step plus 2 levels is a fault. "
                + "Fix: every node must repeat at the image's edge: use whole-number frequencies and counts, keep turns to quarter turns, "
                + "and look at swatch's tiled view to see where the seam falls.");
        }
    }

    private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>The mean step across the wrap, and the mean step between neighbours inside, along one axis.</summary>
    internal static (double Wrap, double Interior) Steps(CheckOutput o, bool alongX)
    {
        var n = alongX ? o.Width : o.Height;
        var lines = alongX ? o.Height : o.Width;
        double wrap = 0, inside = 0;
        for (var l = 0; l < lines; l++)
            for (var c = 0; c < o.Channels; c++)
            {
                byte At(int i) => alongX ? o.Level(i, l, c) : o.Level(l, i, c);
                wrap += Math.Abs(At(n - 1) - At(0));
                for (var i = 0; i + 1 < n; i++) inside += Math.Abs(At(i + 1) - At(i));
            }
        var count = (double)lines * o.Channels;
        return (wrap / count, inside / (count * (n - 1)));
    }
}
