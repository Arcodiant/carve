using System.Text.RegularExpressions;

namespace Carve.Scenario.Soft;

/// <summary>
/// The check on the soft gardener's export warnings, shared by the scenario and by <c>tests/Carve.SoftLoad.Tests</c> (T-72, the
/// person's rule of 2026-11-13): per mesh the first 10 not-planar faces are named and the rest are one folded line, the two adding up
/// to the faces <c>select faces where non_planar</c> finds, and no other warning is there.
/// </summary>
public static partial class NotPlanarFold
{
    public const int Named = 10;

    [GeneratedRegex(@"mesh '(?<mesh>[^']+)': and (?<n>\d+) more faces? (?:is|are) not planar; select faces where non_planar names them")]
    private static partial Regex FoldLine();

    [GeneratedRegex(@"^mesh '(?<mesh>[^']+)': faces\[(?<face>\d+)\]: face is not planar: ")]
    private static partial Regex NamedLine();

    /// <summary>The problem with <paramref name="warnings"/> (the text after "warning(s): "), or null when it is right.</summary>
    public static string? Check(string warnings, IReadOnlyDictionary<string, int> nonPlanarByMesh)
    {
        var folded = new Dictionary<string, int>();
        // The fold line has a "; " of its own, so it is lifted out before the warnings are split.
        var rest = FoldLine().Replace(warnings, m =>
        {
            var mesh = m.Groups["mesh"].Value;
            if (!folded.TryAdd(mesh, int.Parse(m.Groups["n"].Value))) folded[mesh] = -1;
            return "FOLD";
        });
        var named = new Dictionary<string, List<int>>();
        foreach (var line in rest.Split("; "))
        {
            if (line == "FOLD") continue;
            if (NamedLine().Match(line) is not { Success: true } m)
                return $"a warning that is not about a face being non-planar: {line}";
            if (!named.TryGetValue(m.Groups["mesh"].Value, out var ids)) named[m.Groups["mesh"].Value] = ids = [];
            ids.Add(int.Parse(m.Groups["face"].Value));
        }

        if (!named.Keys.Concat(folded.Keys).All(nonPlanarByMesh.ContainsKey))
            return $"warnings for a mesh select knows nothing of: {warnings}";
        if (nonPlanarByMesh.Values.All(c => c == 0)) return "select finds no non_planar face on the soft gardener";
        foreach (var (mesh, count) in nonPlanarByMesh)
        {
            var ids = named.GetValueOrDefault(mesh) ?? [];
            var more = folded.GetValueOrDefault(mesh);
            if (more < 0) return $"mesh '{mesh}' has more than one folded line";
            if (ids.Count != Math.Min(count, Named))
                return $"mesh '{mesh}': {ids.Count} faces named, expected {Math.Min(count, Named)} of select's {count}";
            if (!ids.SequenceEqual(ids.Order())) return $"mesh '{mesh}': the named faces are not in ascending order";
            if (ids.Count + more != count)
                return $"mesh '{mesh}': {ids.Count} named and {more} folded do not add up to select's {count} non_planar faces";
            if (count <= Named && folded.ContainsKey(mesh)) return $"mesh '{mesh}': a fold line for {count} faces, which all fit";
        }
        return null;
    }
}
