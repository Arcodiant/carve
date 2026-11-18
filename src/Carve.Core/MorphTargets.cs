using System.Numerics;

namespace Carve.Core;

/// <summary>
/// One morph target of a mesh (D-6, Morph targets): a name and a second set of positions on the same vertices, held as
/// sparse deltas from the base by vertex id. A vertex not listed has a zero delta, and a delta of exactly (0,0,0) is never
/// stored. Immutable.
/// </summary>
public sealed class MorphTarget
{
    private readonly SortedDictionary<int, Vector3> _deltas;

    private MorphTarget(string name, SortedDictionary<int, Vector3> deltas)
    {
        Name = name;
        _deltas = deltas;
    }

    /// <summary>The target's name: one dial across the model (the same name on two meshes is one glTF target).</summary>
    public string Name { get; }

    /// <summary>The stored deltas by vertex id, ascending; none is (0,0,0).</summary>
    public IReadOnlyDictionary<int, Vector3> Deltas => _deltas;

    /// <summary>How many vertices the target moves.</summary>
    public int Count => _deltas.Count;

    /// <summary>The delta of <paramref name="vertex"/>: (0,0,0) when it is not listed.</summary>
    public Vector3 DeltaOf(int vertex) => _deltas.TryGetValue(vertex, out var d) ? d : Vector3.Zero;

    /// <summary>
    /// A target of exactly these deltas, unchecked: a delta of (0,0,0) is dropped, a vertex given twice keeps the last.
    /// For edits carrying a checked target; a new target from outside comes through <see cref="FromDeltas"/>.
    /// </summary>
    public static MorphTarget Of(string name, IEnumerable<KeyValuePair<int, Vector3>> deltas)
    {
        ArgumentNullException.ThrowIfNull(name);
        var kept = new SortedDictionary<int, Vector3>();
        foreach (var (v, d) in deltas)
        {
            if (d == Vector3.Zero) kept.Remove(v);
            else kept[v] = d;
        }
        return new MorphTarget(name, kept);
    }

    /// <summary>This target under another name.</summary>
    public MorphTarget Renamed(string name) => new(name, _deltas);

    /// <summary>This target with no delta on <paramref name="vertices"/> (a deleted vertex drops its delta).</summary>
    public MorphTarget Without(IEnumerable<int> vertices)
    {
        var next = new SortedDictionary<int, Vector3>(_deltas);
        var changed = false;
        foreach (var v in vertices) changed |= next.Remove(v);
        return changed ? new MorphTarget(Name, next) : this;
    }

    /// <summary>This target with its vertex ids moved up by <paramref name="offset"/> (a combined model's mesh).</summary>
    internal MorphTarget Offset(int offset) =>
        offset == 0 ? this : new MorphTarget(Name, new SortedDictionary<int, Vector3>(_deltas.ToDictionary(p => p.Key + offset, p => p.Value)));

    /// <summary>Why <paramref name="name"/> cannot name a target, or null when it can: not empty, and no ':' (as a mesh name).</summary>
    public static string? NameProblem(string? name) =>
        string.IsNullOrEmpty(name) ? "a target name must not be empty"
        : name.Contains(':') ? "a target name must not contain ':' (as a mesh name must not)"
        : null;

    /// <summary>
    /// A target from absolute positions, one per vertex slot of <paramref name="mesh"/> (a tombstone's slot is skipped):
    /// the delta is <c>given - base</c> in float. Refused, by <c>name</c> or <c>positions</c> / <c>positions[id]</c>, for
    /// a bad name, the wrong count, or a non-finite coordinate.
    /// </summary>
    public static MorphTargetResult FromPositions(PolygonMesh mesh, string? name, IReadOnlyList<Vector3> positions)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(positions);
        var errors = new List<MeshError>();
        if (NameProblem(name) is { } bad) errors.Add(new MeshError("name", bad));
        if (positions.Count != mesh.Vertices.Count)
        {
            errors.Add(new MeshError("positions", MeshEdits.Invariant(
                $"{positions.Count} positions given; the mesh has {mesh.Vertices.Count} vertex slots, and each needs one (a deleted slot's is ignored)")));
            return new MorphTargetResult(null, errors);
        }
        var deltas = new List<KeyValuePair<int, Vector3>>();
        for (var v = 0; v < positions.Count; v++)
        {
            if (mesh.IsVertexDeleted(v)) continue;
            if (!IsFinite(positions[v])) errors.Add(new MeshError(MeshEdits.Invariant($"positions[{v}]"), NotFinite));
            else deltas.Add(KeyValuePair.Create(v, positions[v] - mesh.Vertices[v]));
        }
        return errors.Count > 0 ? new MorphTargetResult(null, errors) : new MorphTargetResult(Of(name!, deltas), []);
    }

    /// <summary>
    /// A target from absolute positions of some vertices (vertex id to position); the rest keep a zero delta. Refused, by
    /// <c>positions[id]</c>, for an id the mesh does not have or has deleted, or a non-finite coordinate.
    /// </summary>
    public static MorphTargetResult FromPositions(PolygonMesh mesh, string? name, IEnumerable<KeyValuePair<int, Vector3>> positions)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(positions);
        return Sparse(mesh, name, positions, "positions", (v, p) => p - mesh.Vertices[v]);
    }

    /// <summary>
    /// A target from deltas of some vertices (vertex id to delta), as the Carve file holds them. Refused, by
    /// <c>deltas[id]</c>, for an id the mesh does not have or has deleted, or a non-finite coordinate.
    /// </summary>
    public static MorphTargetResult FromDeltas(PolygonMesh mesh, string? name, IEnumerable<KeyValuePair<int, Vector3>> deltas)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(deltas);
        return Sparse(mesh, name, deltas, "deltas", (_, d) => d);
    }

    /// <summary>
    /// A target from a mesh of the same topology, its positions taken as the target's (a tombstone's skipped). The topology
    /// must match: the same vertex slot count, the same tombstones, and the same faces in the same order with identical
    /// loops and holes. A mismatch is refused naming the <b>first</b> difference
    /// (<see cref="TopologyDifference"/>); a non-finite coordinate in the other mesh as <c>positions[id]</c>.
    /// </summary>
    public static MorphTargetResult FromMesh(PolygonMesh mesh, string? name, PolygonMesh other)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(other);
        var errors = new List<MeshError>();
        if (NameProblem(name) is { } bad) errors.Add(new MeshError("name", bad));
        if (TopologyDifference(mesh, other) is { } difference)
        {
            errors.Add(difference);
            return new MorphTargetResult(null, errors);
        }
        var result = FromPositions(mesh, name, other.Vertices);
        return errors.Count == 0 ? result : new MorphTargetResult(null, [.. errors, .. result.Errors.Where(e => e.Element != "name")]);
    }

    /// <summary>
    /// The first way <paramref name="other"/>'s topology differs from <paramref name="mesh"/>'s, or null when they match:
    /// <c>vertices: 88 here, 90 in the other mesh</c>; <c>vertices[5]: deleted here, live in the other mesh</c>;
    /// <c>faces: 82 here, 80 in the other mesh</c>; <c>faces[12]: 4 corners here, 3 in the other mesh</c> (a deleted face
    /// has 0); <c>faces[12][2]: vertex 40 here, 41 in the other mesh</c>; <c>faces[3].holes: 1 here, 0 in the other mesh</c>;
    /// <c>faces[3].holes[0]</c> and <c>faces[3].holes[0][1]</c> as the loop's.
    /// </summary>
    public static MeshError? TopologyDifference(PolygonMesh mesh, PolygonMesh other)
    {
        static MeshError Differ(string at, string here, string there) =>
            new(at, MeshEdits.Invariant($"{here} here, {there} in the other mesh"));
        static string Live(bool deleted) => deleted ? "deleted" : "live";

        if (mesh.Vertices.Count != other.Vertices.Count)
            return Differ("vertices", MeshEdits.Invariant($"{mesh.Vertices.Count}"), MeshEdits.Invariant($"{other.Vertices.Count}"));
        for (var v = 0; v < mesh.Vertices.Count; v++)
            if (mesh.IsVertexDeleted(v) != other.IsVertexDeleted(v))
                return Differ(MeshEdits.Invariant($"vertices[{v}]"), Live(mesh.IsVertexDeleted(v)), Live(other.IsVertexDeleted(v)));
        if (mesh.Faces.Count != other.Faces.Count)
            return Differ("faces", MeshEdits.Invariant($"{mesh.Faces.Count}"), MeshEdits.Invariant($"{other.Faces.Count}"));

        static MeshError? Loop(string at, IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            if (a.Count != b.Count) return Differ(at, MeshEdits.Invariant($"{a.Count} corners"), MeshEdits.Invariant($"{b.Count}"));
            for (var i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return Differ(MeshEdits.Invariant($"{at}[{i}]"), MeshEdits.Invariant($"vertex {a[i]}"), MeshEdits.Invariant($"{b[i]}"));
            return null;
        }

        for (var f = 0; f < mesh.Faces.Count; f++)
        {
            Face a = mesh.Faces[f], b = other.Faces[f];
            var at = MeshEdits.Invariant($"faces[{f}]");
            if (a.IsDeleted != b.IsDeleted) return Differ(at, Live(a.IsDeleted), Live(b.IsDeleted));
            if (Loop(at, a.Loop, b.Loop) is { } outer) return outer;
            if (a.Holes.Count != b.Holes.Count)
                return Differ($"{at}.holes", MeshEdits.Invariant($"{a.Holes.Count}"), MeshEdits.Invariant($"{b.Holes.Count}"));
            for (var h = 0; h < a.Holes.Count; h++)
                if (Loop(MeshEdits.Invariant($"{at}.holes[{h}]"), a.Holes[h], b.Holes[h]) is { } hole) return hole;
        }
        return null;
    }

    private const string NotFinite = "a coordinate is not finite";

    private static bool IsFinite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

    private static MorphTargetResult Sparse(PolygonMesh mesh, string? name, IEnumerable<KeyValuePair<int, Vector3>> given, string at,
        Func<int, Vector3, Vector3> delta)
    {
        var errors = new List<MeshError>();
        if (NameProblem(name) is { } bad) errors.Add(new MeshError("name", bad));
        var deltas = new List<KeyValuePair<int, Vector3>>();
        foreach (var (v, p) in given)
        {
            var where = MeshEdits.Invariant($"{at}[{v}]");
            if (mesh.VertexProblem(v) is { } problem) errors.Add(new MeshError(where, problem));
            else if (!IsFinite(p)) errors.Add(new MeshError(where, NotFinite));
            else deltas.Add(KeyValuePair.Create(v, delta(v, p)));
        }
        return errors.Count > 0 ? new MorphTargetResult(null, errors) : new MorphTargetResult(Of(name!, deltas), []);
    }
}

/// <summary>A target made, or every reason it was refused.</summary>
public sealed class MorphTargetResult
{
    internal MorphTargetResult(MorphTarget? target, IReadOnlyList<MeshError> errors)
    {
        Target = target;
        Errors = errors;
    }

    /// <summary>The target, or null when refused.</summary>
    public MorphTarget? Target { get; }

    /// <summary>Why it was refused; empty when it was made.</summary>
    public IReadOnlyList<MeshError> Errors { get; }

    /// <summary>True when the target was made.</summary>
    public bool Succeeded => Target is not null;
}

/// <summary>
/// The morph targets of one mesh (D-6, Morph targets), in definition order: names unique and case-sensitive, each a sparse
/// set of position deltas by vertex id. Never empty: a mesh with none has <see cref="PolygonMesh.Targets"/> null. Immutable.
/// </summary>
public sealed class MorphTargets
{
    private readonly MorphTarget[] _targets;

    private MorphTargets(MorphTarget[] targets) => _targets = targets;

    /// <summary>The targets in definition order.</summary>
    public IReadOnlyList<MorphTarget> All => _targets;

    /// <summary>How many targets there are.</summary>
    public int Count => _targets.Length;

    /// <summary>The names in definition order.</summary>
    public IEnumerable<string> Names => _targets.Select(t => t.Name);

    /// <summary>The target named <paramref name="name"/> (case-sensitive), or null.</summary>
    public MorphTarget? Find(string name) => Array.Find(_targets, t => t.Name == name);

    /// <summary>The names, quoted and comma-separated, in definition order.</summary>
    public string ListNames() => NameList.Quoted(Names);

    /// <summary>
    /// These targets, in order (null for none). Throws on a bad or repeated name: a target from outside is checked by
    /// <see cref="MorphTarget"/>'s constructors, and a name given twice is the caller's to refuse.
    /// </summary>
    public static MorphTargets? Of(IEnumerable<MorphTarget> targets)
    {
        var all = targets.ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in all)
        {
            if (MorphTarget.NameProblem(t.Name) is { } problem) throw new ArgumentException(problem, nameof(targets));
            if (!seen.Add(t.Name)) throw new ArgumentException($"the target name '{t.Name}' is given twice", nameof(targets));
        }
        return all.Length == 0 ? null : new MorphTargets(all);
    }

    /// <summary>
    /// These targets with <paramref name="target"/>: one of the same name is replaced in its place (<paramref name="replaced"/>
    /// true), otherwise it is added last.
    /// </summary>
    public MorphTargets With(MorphTarget target, out bool replaced)
    {
        var at = Array.FindIndex(_targets, t => t.Name == target.Name);
        replaced = at >= 0;
        var next = (MorphTarget[])_targets.Clone();
        if (replaced) next[at] = target;
        else next = [.. next, target];
        return new MorphTargets(next);
    }

    /// <summary>These targets with <paramref name="target"/> added or replaced (see <see cref="With(MorphTarget, out bool)"/>).</summary>
    public MorphTargets With(MorphTarget target) => With(target, out _);

    /// <summary>A mesh's targets with <paramref name="target"/>; for a mesh with none, a set of that one.</summary>
    public static MorphTargets With(MorphTargets? targets, MorphTarget target) =>
        targets?.With(target) ?? new MorphTargets([target]);

    /// <summary>These targets without the one named <paramref name="name"/>; null when none is left.</summary>
    public MorphTargets? Without(string name)
    {
        var next = Array.FindAll(_targets, t => t.Name != name);
        return next.Length == _targets.Length ? this : next.Length == 0 ? null : new MorphTargets(next);
    }

    /// <summary>These targets with no delta on <paramref name="vertices"/> (deleted vertices drop their deltas); a target left
    /// with no delta is kept, with none.</summary>
    public MorphTargets WithoutVertices(IEnumerable<int> vertices)
    {
        var gone = vertices.ToArray();
        return new MorphTargets(_targets.Select(t => t.Without(gone)).ToArray());
    }

    /// <summary>
    /// Every target mapped through <paramref name="map"/>, a function from each target to its new deltas: for an edit that
    /// works deltas as it works positions.
    /// </summary>
    public MorphTargets Select(Func<MorphTarget, IEnumerable<KeyValuePair<int, Vector3>>> map) =>
        new(_targets.Select(t => MorphTarget.Of(t.Name, map(t))).ToArray());

    /// <summary>
    /// The positions at <paramref name="weights"/> (target name to weight): <c>base + Σ wᵢ·deltaᵢ</c>, each vertex summed in
    /// double in definition order from its base and rounded once, so the result is the same bits every time. A name these
    /// targets do not have is ignored; a target not given has weight 0. With every weight 0 the base comes back exactly.
    /// </summary>
    public Vector3[] Apply(IReadOnlyList<Vector3> positions, IReadOnlyDictionary<string, float> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var x = new double[positions.Count];
        var y = new double[positions.Count];
        var z = new double[positions.Count];
        for (var v = 0; v < positions.Count; v++) (x[v], y[v], z[v]) = (positions[v].X, positions[v].Y, positions[v].Z);
        var touched = new bool[positions.Count];
        foreach (var t in _targets)
        {
            if (!weights.TryGetValue(t.Name, out var w) || w == 0) continue;
            foreach (var (v, d) in t.Deltas)
            {
                if (v >= positions.Count) continue;
                x[v] += (double)w * d.X; y[v] += (double)w * d.Y; z[v] += (double)w * d.Z;
                touched[v] = true;
            }
        }
        var result = new Vector3[positions.Count];
        for (var v = 0; v < result.Length; v++)
            result[v] = touched[v] ? new Vector3((float)x[v], (float)y[v], (float)z[v]) : positions[v];
        return result;
    }

    /// <summary>These targets with every vertex id moved up by <paramref name="offset"/>.</summary>
    internal MorphTargets Offset(int offset) => offset == 0 ? this : new(_targets.Select(t => t.Offset(offset)).ToArray());
}
