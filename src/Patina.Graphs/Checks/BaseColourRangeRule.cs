using System.Globalization;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Checks;

/// <summary>
/// <c>base-colour-range</c>: a non-metal texel (metallic below 0.5, or no metallic) whose brightest channel is below 20
/// or above 245 in sRGB levels, or a metal texel (metallic 0.5 or more) whose brightest channel is below 120. A fault when
/// over 1% of texels are out.
/// </summary>
public sealed class BaseColourRangeRule : ICheckRule
{
    public const string Id = "base-colour-range";
    public const int NonMetalLow = 20, NonMetalHigh = 245, MetalLow = 120;
    public const double MetallicIsMetal = 0.5;

    public string Name => Id;

    public IEnumerable<CheckProblem> Run(CheckSet set)
    {
        foreach (var o in set.Outputs.Where(o => o.IsRole(OutputRole.BaseColour)))
        {
            var metallic = set.MetallicFor(o);
            long outside = 0, outsideMetal = 0;
            var worst = 0; var worstX = 0; var worstY = 0; byte wr = 0, wg = 0, wb = 0; var worstMetal = false;
            for (var y = 0; y < o.Height; y++)
                for (var x = 0; x < o.Width; x++)
                {
                    byte r = o.Level(x, y, 0), g = o.Level(x, y, o.IsGrey ? 0 : 1), b = o.Level(x, y, o.IsGrey ? 0 : 2);
                    int top = Math.Max(r, Math.Max(g, b));
                    var metal = metallic is not null && metallic.Grey(x, y) >= MetallicIsMetal;
                    var excess = metal ? MetalLow - top : Math.Max(NonMetalLow - top, top - NonMetalHigh);
                    if (excess <= 0) continue;
                    outside++;
                    if (metal) outsideMetal++;
                    if (excess > worst) { worst = excess; worstX = x; worstY = y; wr = r; wg = g; wb = b; worstMetal = metal; }
                }
            if (outside * 100 <= o.Texels) continue;
            var band = outsideMetal > 0
                ? $"outside {NonMetalLow}..{NonMetalHigh} (brightest channel; a metal texel is out below {MetalLow})"
                : $"outside {NonMetalLow}..{NonMetalHigh} (brightest channel)";
            yield return new(Id, o.Name,
                $"{CheckRules.Percent(outside, o.Texels)} of texels are {band}, worst #{wr:x2}{wg:x2}{wb:x2} at ({worstX.ToString(CultureInfo.InvariantCulture)}, {worstY.ToString(CultureInfo.InvariantCulture)}){(worstMetal ? ", a metal texel" : "")}; over 1% is a fault. "
                + $"Fix: keep a non-metal's brightest channel from {NonMetalLow} to {NonMetalHigh} (near black and near white are holes and blown highlights under game lighting) and a metal's at {MetalLow} or more (dark metal reflects almost nothing); "
                + "lift the darks or pull the lights in with a levels or gradient_map node on the base colour.");
        }
    }
}
