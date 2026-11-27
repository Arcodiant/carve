using System.Text.RegularExpressions;

namespace Carve.Scenario.Soft;

/// <summary>
/// The check on the soft gardener's export warnings, shared by the scenario and by <c>tests/Carve.SoftLoad.Tests</c> (T-197, the
/// person's rule of 2026-11-24, which replaced T-72's): per mesh up to 3 not-planar faces are each named, and more are one grouped
/// line, its count the faces <c>select faces where non_planar</c> finds and its samples select's 3 lowest ids; no other warning is there.
/// </summary>
public static partial class NotPlanarFold
{
    /// <summary>The most faces named one per line; the grouped line's samples are as many.</summary>
    public const int Named = 3;

    [GeneratedRegex(@"mesh '(?<mesh>[^']+)': (?<n>\d+) faces are not planar: worst faces\[(?<worst>\d+)\], [^;]+ off its plane \(limit [^;)]+\); e\.g\. faces\[(?<a>\d+)\], faces\[(?<b>\d+)\], faces\[(?<c>\d+)\]; select faces where non_planar names them")]
    private static partial Regex GroupLine();

    [GeneratedRegex(@"^(?<n>\d+) (?:faces are|face is) not planar: worst faces\[(?<worst>\d+)\], [^;]+ off its plane \(limit [^;)]+\); e\.g\. [^;]+; select faces where non_planar names them; expected on a smooth mesh, which export triangulates$")]
    private static partial Regex SmoothNote();

    [GeneratedRegex(@"^mesh '(?<mesh>[^']+)': faces\[(?<face>\d+)\]: face is not planar: ")]
    private static partial Regex NamedLine();

    /// <summary>
    /// The problem with an export of a smooth mesh (T-198), or null when it is right: the result has no warning at all, since a
    /// not-planar face on a smooth mesh is no warning, and select finds some (so the rule is being exercised).
    /// </summary>
    public static string? CheckSmoothExport(string exportResult, int selectedCount) =>
        exportResult.Contains("warning(s)", StringComparison.Ordinal) ? $"a smooth mesh's export warns: {exportResult}"
        : selectedCount == 0 ? "select finds no non_planar face, so nothing is tested" : null;

    /// <summary>
    /// The problem with <c>check</c>'s result JSON for a smooth mesh, or null when it is right: no warning says not planar, and one
    /// note counts select's faces, names a worst face select finds, and says it is expected.
    /// </summary>
    public static string? CheckSmoothNote(string checkResult, IReadOnlyList<int> selectedIds)
    {
        var root = System.Text.Json.JsonDocument.Parse(checkResult).RootElement;
        if (root.GetProperty("warnings").EnumerateArray().Any(w => w.GetString()!.Contains("not planar", StringComparison.Ordinal)))
            return $"check warns of a not-planar face on a smooth mesh: {root.GetProperty("warnings")}";
        var notes = root.GetProperty("notes").EnumerateArray().Select(n => SmoothNote().Match(n.GetString()!)).Where(m => m.Success).ToList();
        if (notes.Count != 1) return $"check has {notes.Count} not-planar notes, not 1: {root.GetProperty("notes")}";
        if (int.Parse(notes[0].Groups["n"].Value) != selectedIds.Count) return $"the note counts {notes[0].Groups["n"].Value}, not select's {selectedIds.Count} non_planar faces";
        return selectedIds.Contains(int.Parse(notes[0].Groups["worst"].Value)) ? null : $"the note's worst face, faces[{notes[0].Groups["worst"].Value}], is not one select finds";
    }

    /// <summary>
    /// The problem with an export's result text, or null when it is right: with no "warning(s): " the export is clean, which meets the
    /// rule only when select finds no non_planar face either; with warnings, <see cref="Check(string, IReadOnlyDictionary{string, IReadOnlyList{int}})"/>.
    /// </summary>
    public static string? CheckExport(string exportResult, IReadOnlyDictionary<string, IReadOnlyList<int>> nonPlanarIdsByMesh)
    {
        const string Prefix = "warning(s): ";
        var at = exportResult.IndexOf(Prefix, StringComparison.Ordinal);
        if (at >= 0) return Check(exportResult[(at + Prefix.Length)..], nonPlanarIdsByMesh);
        var selected = nonPlanarIdsByMesh.Values.Sum(v => v.Count);
        return selected == 0 ? null : $"the export has no warnings, but select finds {selected} non_planar faces";
    }

    /// <summary>
    /// The problem with <paramref name="warnings"/> (the text after "warning(s): "), or null when it is right, against the ids
    /// <c>select faces where non_planar</c> gives per mesh: as <see cref="Check(string, IReadOnlyDictionary{string, int})"/>, and
    /// the named faces (or the grouped line's samples) are select's lowest ids, and the worst face is one select finds.
    /// </summary>
    public static string? Check(string warnings, IReadOnlyDictionary<string, IReadOnlyList<int>> nonPlanarIdsByMesh)
    {
        var problem = Check(warnings, nonPlanarIdsByMesh.ToDictionary(p => p.Key, p => p.Value.Count), out var named, out var worst);
        if (problem is not null) return problem;
        foreach (var (mesh, selected) in nonPlanarIdsByMesh)
        {
            var ids = named.GetValueOrDefault(mesh) ?? [];
            var lowest = selected.Order().Take(Named).ToList();
            if (!ids.SequenceEqual(lowest))
                return $"mesh '{mesh}': the named faces are [{string.Join(", ", ids)}], not select's lowest {lowest.Count} [{string.Join(", ", lowest)}]";
            if (worst.TryGetValue(mesh, out var w) && !selected.Contains(w))
                return $"mesh '{mesh}': the worst face, faces[{w}], is not one select finds";
        }
        return null;
    }

    /// <summary>
    /// The problem with <paramref name="warnings"/> (the text after "warning(s): "), or null when it is right, against select's
    /// count of non-planar faces per mesh: no other warning; per mesh, up to <see cref="Named"/> faces each named in ascending
    /// order, or past that one grouped line and no named face, its count select's and its samples in ascending order.
    /// </summary>
    public static string? Check(string warnings, IReadOnlyDictionary<string, int> nonPlanarByMesh) => Check(warnings, nonPlanarByMesh, out _, out _);

    private static string? Check(string warnings, IReadOnlyDictionary<string, int> nonPlanarByMesh, out Dictionary<string, List<int>> named, out Dictionary<string, int> worst)
    {
        var grouped = new Dictionary<string, int>();
        var samples = new Dictionary<string, List<int>>();
        named = [];
        worst = [];
        var worstSeen = worst;
        // The grouped line has "; " of its own, so it is lifted out before the warnings are split, and leaves a marker in its place.
        const string Marker = "\u0001GROUP\u0001";
        var rest = GroupLine().Replace(warnings, m =>
        {
            var mesh = m.Groups["mesh"].Value;
            if (!grouped.TryAdd(mesh, int.Parse(m.Groups["n"].Value))) grouped[mesh] = -1;
            samples[mesh] = [int.Parse(m.Groups["a"].Value), int.Parse(m.Groups["b"].Value), int.Parse(m.Groups["c"].Value)];
            worstSeen[mesh] = int.Parse(m.Groups["worst"].Value);
            return Marker;
        });
        foreach (var line in rest.Split("; "))
        {
            if (line == Marker) continue;
            if (NamedLine().Match(line) is not { Success: true } m)
                return $"a warning that is not about a face being non-planar: {line}";
            var meshName = m.Groups["mesh"].Value;
            if (!named.TryGetValue(meshName, out var ids)) named[meshName] = ids = [];
            ids.Add(int.Parse(m.Groups["face"].Value));
        }

        if (!named.Keys.Concat(grouped.Keys).All(nonPlanarByMesh.ContainsKey))
            return $"warnings for a mesh select knows nothing of: {warnings}";
        if (nonPlanarByMesh.Values.All(c => c == 0)) return "select finds no non_planar face on the soft gardener";
        foreach (var (mesh, count) in nonPlanarByMesh)
        {
            var ids = named.GetValueOrDefault(mesh) ?? [];
            var n = grouped.GetValueOrDefault(mesh);
            if (n < 0) return $"mesh '{mesh}' has more than one grouped line";
            if (count <= Named)
            {
                if (grouped.ContainsKey(mesh)) return $"mesh '{mesh}': a grouped line for {count} faces, which are listed one per line";
                if (ids.Count != count) return $"mesh '{mesh}': {ids.Count} faces named, expected select's {count}";
            }
            else
            {
                if (ids.Count > 0) return $"mesh '{mesh}': {ids.Count} faces named one per line beside the grouped line";
                if (n != count) return $"mesh '{mesh}': the grouped line counts {n}, not select's {count} non_planar faces";
                named[mesh] = ids = samples[mesh];
            }
            if (!ids.SequenceEqual(ids.Order())) return $"mesh '{mesh}': the named faces are not in ascending order";
        }
        return null;
    }
}
