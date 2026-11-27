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
        // Grouping is decided on the count the line reports: the elements, both faces of each UV overlap.
        var counts = groups.ToDictionary(g => g.Key, g => ElementsOf(g.Value).Count());
        var lines = new List<string>(warnings.Count);
        var done = new HashSet<MeshWarningKind>();
        foreach (var w in warnings)
        {
            var kind = groups[w.Kind];
            if (counts[w.Kind] <= ListedLimit) lines.Add(w.ToString());
            else if (done.Add(w.Kind)) lines.Add(Grouped(w.Kind, kind));
        }
        return lines;
    }

    /// <summary>The note <c>check</c> gives of the not-planar faces of a smooth mesh (T-198): the grouped line at any count, then why it is no warning.</summary>
    public static string SmoothNote(IReadOnlyList<MeshWarning> nonPlanar) =>
        Grouped(MeshWarningKind.NonPlanarFace, nonPlanar.ToList()) + "; expected on a smooth mesh, which export triangulates";

    private static string Grouped(MeshWarningKind kind, List<MeshWarning> all)
    {
        var sorted = all.OrderBy(w => Key(w.Element).A).ThenBy(w => Key(w.Element).B).ThenBy(w => w.Element, StringComparer.Ordinal).ToList();
        var elements = ElementsOf(all).OrderBy(e => Key(e).A).ThenBy(e => Key(e).B).ThenBy(e => e, StringComparer.Ordinal).ToList();
        var (one, many, query) = Says(kind);
        var line = elements.Count == 1 ? $"1 {one}: " : string.Create(CultureInfo.InvariantCulture, $"{elements.Count} {many}: ");
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

    /// <summary>The distinct elements warned of: a face can warn more than once (a UV overlap per other face), and an overlap names both faces.</summary>
    private static IEnumerable<string> ElementsOf(IEnumerable<MeshWarning> warnings) =>
        warnings.SelectMany(w => w.Other is null ? [w.Element] : new[] { w.Element, w.Other }).Distinct();

    private static double Severity(MeshWarning w) => w.Limit > 0 ? w.Measure / w.Limit : w.Measure;

    private static (string One, string Many, string? Query) Says(MeshWarningKind kind) => kind switch
    {
        MeshWarningKind.OpenEdge => ("edge belongs to only one face", "edges belong to only one face", "edges where open"),
        MeshWarningKind.NonManifoldEdge => ("edge is shared by more than two faces", "edges are shared by more than two faces", "edges where nonmanifold"),
        MeshWarningKind.InconsistentWinding => ("edge has two faces crossing it in the same direction", "edges have two faces crossing them in the same direction", null),
        MeshWarningKind.NonPlanarFace => ("face is not planar", "faces are not planar", "faces where non_planar"),
        MeshWarningKind.UnusedVertex => ("vertex is used by no face", "vertices are used by no face", "vertices where unattached"),
        MeshWarningKind.UvUnmapped => ("face has no UVs, so it samples the texture's corner", "faces have no UVs, so they sample the texture's corner", null),
        MeshWarningKind.UvFlipped => ("face has flipped UVs, so the texture is mirrored on it", "faces have flipped UVs, so the texture is mirrored on them", null),
        MeshWarningKind.UvZeroArea => ("face has UVs of zero area, so it is one smeared texel", "faces have UVs of zero area, so each is one smeared texel", null),
        MeshWarningKind.UvOverlap => ("face has UVs that overlap another face", "faces have UVs that overlap another face", null),
        MeshWarningKind.MaterialNoEffect => ("material sets occlusion that has no effect without an occlusion_texture", "materials set occlusion that has no effect without an occlusion_texture", null),
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
