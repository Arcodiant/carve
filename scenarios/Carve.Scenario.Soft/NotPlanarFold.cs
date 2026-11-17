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

    /// <summary>
    /// The problem with <paramref name="warnings"/> (the text after "warning(s): "), or null when it is right, against the ids
    /// <c>select faces where non_planar</c> gives per mesh: as <see cref="Check(string, IReadOnlyDictionary{string, int})"/>, and
    /// the named faces are select's lowest ids, the first <see cref="Named"/> in id order (T-72's reading as landed, RV-52).
    /// </summary>
    public static string? Check(string warnings, IReadOnlyDictionary<string, IReadOnlyList<int>> nonPlanarIdsByMesh)
    {
        var problem = Check(warnings, nonPlanarIdsByMesh.ToDictionary(p => p.Key, p => p.Value.Count), out var named);
        if (problem is not null) return problem;
        foreach (var (mesh, selected) in nonPlanarIdsByMesh)
        {
            var ids = named.GetValueOrDefault(mesh) ?? [];
            var lowest = selected.Order().Take(Named).ToList();
            if (!ids.SequenceEqual(lowest))
                return $"mesh '{mesh}': the named faces are [{string.Join(", ", ids)}], not select's lowest {lowest.Count} [{string.Join(", ", lowest)}]";
        }
        return null;
    }

    /// <summary>
    /// The problem with <paramref name="warnings"/> (the text after "warning(s): "), or null when it is right, against select's
    /// count of non-planar faces per mesh: no other warning, per mesh the named faces in ascending order and as many as fit, one fold
    /// line after the mesh's named faces (the fold line comes last), and the two adding up to the count.
    /// </summary>
    public static string? Check(string warnings, IReadOnlyDictionary<string, int> nonPlanarByMesh) => Check(warnings, nonPlanarByMesh, out _);

    private static string? Check(string warnings, IReadOnlyDictionary<string, int> nonPlanarByMesh, out Dictionary<string, List<int>> named)
    {
        var folded = new Dictionary<string, int>();
        named = [];
        // The fold line has a "; " of its own, so it is lifted out before the warnings are split, and leaves a marker naming its mesh.
        const string Marker = "\u0001FOLD\u0001";
        var rest = FoldLine().Replace(warnings, m =>
        {
            var mesh = m.Groups["mesh"].Value;
            if (!folded.TryAdd(mesh, int.Parse(m.Groups["n"].Value))) folded[mesh] = -1;
            return Marker + mesh;
        });
        var foldSeen = new HashSet<string>();
        foreach (var line in rest.Split("; "))
        {
            if (line.StartsWith(Marker, StringComparison.Ordinal))
            {
                foldSeen.Add(line[Marker.Length..]);
                continue;
            }

            if (NamedLine().Match(line) is not { Success: true } m)
                return $"a warning that is not about a face being non-planar: {line}";
            var meshName = m.Groups["mesh"].Value;
            if (foldSeen.Contains(meshName)) return $"mesh '{meshName}': a face is named after the mesh's fold line, which must come last";
            if (!named.TryGetValue(meshName, out var ids)) named[meshName] = ids = [];
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
