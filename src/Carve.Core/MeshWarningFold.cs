using System.Globalization;

namespace Carve.Core;

/// <summary>
/// The warnings as lines of text with the not-planar ones folded (the person's rule, 2026-11-13, "Name 10, fold the rest"):
/// up to <see cref="NamedLimit"/> not-planar faces are listed one per line, the lowest face ids first; past that the rest
/// become one line at the end. Every other warning is kept, in order. <c>select faces where non_planar</c> names every face.
/// </summary>
public static class MeshWarningFold
{
    /// <summary>How many not-planar faces are named before the rest are folded.</summary>
    public const int NamedLimit = 10;

    public static IReadOnlyList<string> Lines(IReadOnlyList<MeshWarning> warnings)
    {
        var lines = new List<string>(warnings.Count);
        var folded = 0;
        // Report order is ascending face id, but the fold does not rely on it: the named ones are the lowest ids.
        var keep = warnings.Where(w => w.Kind == MeshWarningKind.NonPlanarFace)
            .OrderBy(w => FaceId(w.Element)).Take(NamedLimit).ToHashSet();
        foreach (var w in warnings)
        {
            if (w.Kind != MeshWarningKind.NonPlanarFace) lines.Add(w.ToString());
            else if (keep.Contains(w)) lines.Add(w.ToString());
            else folded++;
        }

        if (folded > 0)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"and {folded} more {(folded == 1 ? "face is" : "faces are")} not planar; select faces where non_planar names them"));
        return lines;
    }

    private static int FaceId(string element)
    {
        var open = element.IndexOf('[');
        var close = element.IndexOf(']');
        return open >= 0 && close > open && int.TryParse(element.AsSpan(open + 1, close - open - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id : int.MaxValue;
    }
}
