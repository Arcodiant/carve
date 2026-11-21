using System.Globalization;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Checks;

/// <summary>
/// <c>one-value</c>: a greyscale output (roughness, occlusion, height, or a greyscale named output) whose texels all pack to
/// one byte; a mask of one value masks nothing. Metallic is exempt, from a graph or a PNG alike: an all-metal or
/// all-non-metal material is legitimate, and <c>metallic-not-binary</c> still judges its values (RV-76, the person's ruling).
/// </summary>
public sealed class OneValueRule : ICheckRule
{
    public const string Id = "one-value";

    public string Name => Id;

    public IEnumerable<CheckProblem> Run(CheckSet set)
    {
        foreach (var o in set.Outputs.Where(o => o.IsGrey && !o.IsRole(OutputRole.BaseColour) && !o.IsRole(OutputRole.Metallic)))
        {
            var first = o.Level(o.Data[0]);
            if (o.Data.Any(v => o.Level(v) != first)) continue;
            yield return new(Id, o.Name,
                $"every texel is {(first / 255.0).ToString("0.000", CultureInfo.InvariantCulture)} ({first}); a mask of one value masks nothing. "
                + "Fix: it is clipped or flat; widen its levels (a levels node's in_low and in_high), or drive it from a noise, a pattern or a distance instead of a constant; "
                + "if the one value is meant, leave the output out of the graph.");
        }
    }
}
