using Patina.Graphs.Imaging;

namespace Patina.Graphs.Checks;

/// <summary><c>normal-not-unit</c>: over 0.1% of texels whose vector's length differs from 1 by more than 0.02 (0.03 for a PNG, for the 8-bit rounding).</summary>
public sealed class NormalNotUnitRule : ICheckRule
{
    public const string Id = "normal-not-unit";
    public const double GraphTolerance = 0.02, PngTolerance = 0.03;

    public string Name => Id;

    public IEnumerable<CheckProblem> Run(CheckSet set)
    {
        var tolerance = set.Source == CheckSource.Png ? PngTolerance : GraphTolerance;
        foreach (var o in set.Outputs.Where(o => o.IsRole(OutputRole.Normal) && o.Channels == 3))
        {
            long off = 0;
            double worst = 0; var worstAt = 0L;
            for (long i = 0; i < o.Texels; i++)
            {
                double x = o.Data[i * 3], y = o.Data[i * 3 + 1], z = o.Data[i * 3 + 2];
                var d = Math.Abs(Math.Sqrt(x * x + y * y + z * z) - 1);
                if (d <= tolerance) continue;
                off++;
                if (d > worst) { worst = d; worstAt = i; }
            }
            if (off * 1000 <= o.Texels) continue;
            yield return new(Id, o.Name,
                $"{CheckRules.Percent(off, o.Texels)} of texels have a length more than {tolerance:0.00} from 1, worst {worst:0.000} off at ({worstAt % o.Width}, {worstAt / o.Width}); over 0.1% is a fault. "
                + "Fix: a normal is a unit vector, or the lighting is stronger or weaker than it should be; make it with a height_to_normal node, which normalises, and do not mix or scale a normal's components as if they were colours.");
        }
    }
}
