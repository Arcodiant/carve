using Patina.Graphs.Imaging;

namespace Patina.Graphs.Checks;

/// <summary><c>metallic-not-binary</c>: over 5% of texels between 0.1 and 0.9 (both included); a surface is metal or it is not.</summary>
public sealed class MetallicNotBinaryRule : ICheckRule
{
    public const string Id = "metallic-not-binary";
    public const double Low = 0.1, High = 0.9;

    public string Name => Id;

    public IEnumerable<CheckProblem> Run(CheckSet set)
    {
        foreach (var o in set.Outputs.Where(o => o.IsRole(OutputRole.Metallic) && o.IsGrey))
        {
            long between = 0;
            foreach (var v in o.Data)
                if ((double)v >= Low && (double)v <= High) between++;
            if (between * 20 <= o.Texels) continue;
            yield return new(Id, o.Name,
                $"{CheckRules.Percent(between, o.Texels)} of texels are between 0.1 and 0.9; over 5% is a fault. "
                + "Fix: metallic is 1 on bare metal and 0 on everything else, with a sharp edge between; a middle value is a half-metal that does not exist in a real surface, and renders as a dull chrome. "
                + "Drive it from a mask pushed to 0 and 1 (a levels node with in_low equal to in_high is a step), or set it to 0 or 1.");
        }
    }
}
