using System.Globalization;

namespace Carve.Core;

/// <summary>
/// The warnings as lines of text, one line per kind past <see cref="ListedLimit"/> (the person's ruling, 2026-11-24, which
/// replaced T-72's "Name 10, fold the rest"). A kind with 1 to 3 warnings is listed one per line as the warnings say it; a kind
/// with more is one line where its first warning stood: the count, the worst case where the kind measures one, three samples
/// (the lowest ids), and the <c>select</c> query that names them all where <c>select</c> has the predicate.
/// </summary>
public static class MeshWarningGroups
{
    /// <summary>The most warnings of one kind listed one per line; one more and the kind is one line.</summary>
    public const int ListedLimit = 3;

    /// <summary>How many elements a grouped line names as samples.</summary>
    public const int Samples = 3;

    public static IReadOnlyList<string> Lines(IReadOnlyList<MeshWarning> warnings)
    {
        var groups = warnings.GroupBy(w => w.Kind).ToDictionary(g => g.Key, g => g.ToList());
        var lines = new List<string>(warnings.Count);
        var done = new HashSet<MeshWarningKind>();
        foreach (var w in warnings)
        {
            var kind = groups[w.Kind];
            if (kind.Count <= ListedLimit) lines.Add(w.ToString());
            else if (done.Add(w.Kind)) lines.Add(Grouped(w.Kind, kind));
        }
        return lines;
    }

    private static string Grouped(MeshWarningKind kind, List<MeshWarning> all)
    {
        var sorted = all.OrderBy(w => Key(w.Element).A).ThenBy(w => Key(w.Element).B).ThenBy(w => w.Element, StringComparer.Ordinal).ToList();
        // A face can warn more than once (a UV overlap per other face), so the count and the samples are of elements.
        var elements = sorted.Select(w => w.Element).Distinct().ToList();
        var (noun, wrong, query) = Says(kind);
        var line = string.Create(CultureInfo.InvariantCulture, $"{elements.Count} {noun} {wrong}: ");
        if (kind is MeshWarningKind.NonPlanarFace or MeshWarningKind.NonManifoldEdge)
        {
            var worst = sorted[0];
            foreach (var w in sorted)
                if (Severity(w) > Severity(worst)) worst = w; // strictly greater, so a tie goes to the lowest id
            line += kind == MeshWarningKind.NonPlanarFace
                ? MeshEdits.Invariant($"worst {worst.Element}, {worst.Measure:G3} off its plane (limit {worst.Limit:G3}); ")
                : MeshEdits.Invariant($"worst {worst.Element}, shared by {worst.Measure} faces; ");
        }
        line += "e.g. " + string.Join(", ", elements.Take(Samples));
        return query is null ? line : line + $"; select {query} names them";
    }

    private static double Severity(MeshWarning w) => w.Limit > 0 ? w.Measure / w.Limit : w.Measure;

    private static (string Noun, string Wrong, string? Query) Says(MeshWarningKind kind) => kind switch
    {
        MeshWarningKind.OpenEdge => ("edges", "belong to only one face", "edges where open"),
        MeshWarningKind.NonManifoldEdge => ("edges", "are shared by more than two faces", "edges where nonmanifold"),
        MeshWarningKind.InconsistentWinding => ("edges", "have two faces crossing them in the same direction", null),
        MeshWarningKind.NonPlanarFace => ("faces", "are not planar", "faces where non_planar"),
        MeshWarningKind.UnusedVertex => ("vertices", "are used by no face", "vertices where unattached"),
        MeshWarningKind.UvUnmapped => ("faces", "have no UVs, so they sample the texture's corner", null),
        MeshWarningKind.UvFlipped => ("faces", "have flipped UVs, so the texture is mirrored on them", null),
        MeshWarningKind.UvZeroArea => ("faces", "have UVs of zero area, so each is one smeared texel", null),
        MeshWarningKind.UvOverlap => ("faces", "have UVs that overlap another face", null),
        MeshWarningKind.MaterialNoEffect => ("materials", "set occlusion that has no effect without an occlusion_texture", null),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The ids in <c>faces[12]</c> or <c>edges[0-9]</c>; a name (<c>materials['a']</c>) sorts after, by its text.</summary>
    private static (int A, int B) Key(string element)
    {
        var open = element.IndexOf('[');
        var close = element.LastIndexOf(']');
        if (open < 0 || close <= open) return (int.MaxValue, int.MaxValue);
        var parts = element[(open + 1)..close].Split('-');
        static int Id(string s) => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : int.MaxValue;
        return (Id(parts[0]), parts.Length > 1 ? Id(parts[1]) : -1);
    }
}
