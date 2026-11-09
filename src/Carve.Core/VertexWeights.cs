namespace Carve.Core;

/// <summary>One bone's pull on a vertex: the bone's index in the skeleton and a weight above 0.</summary>
public readonly record struct Influence(int Bone, double Weight);

/// <summary>
/// The bone weights of one mesh by vertex id (D-6, Weights): a vertex has at most <see cref="MaxInfluences"/>
/// influences, normalised to sum to 1; a vertex not listed is unweighted. Bones are held by index, so a skeleton change
/// that keeps names and order keeps the weights valid. Immutable.
/// </summary>
public sealed class VertexWeights
{
    /// <summary>The most influences a vertex keeps.</summary>
    public const int MaxInfluences = 4;

    /// <summary>No weights.</summary>
    public static VertexWeights None { get; } = new(new SortedDictionary<int, Influence[]>());

    private readonly SortedDictionary<int, Influence[]> _byVertex;

    private VertexWeights(SortedDictionary<int, Influence[]> byVertex) => _byVertex = byVertex;

    /// <summary>True when no vertex is weighted.</summary>
    public bool IsEmpty => _byVertex.Count == 0;

    /// <summary>How many vertices have weights.</summary>
    public int Count => _byVertex.Count;

    /// <summary>The influences of <paramref name="vertex"/> (empty when unweighted).</summary>
    public IReadOnlyList<Influence> Of(int vertex) => _byVertex.TryGetValue(vertex, out var i) ? i : [];

    /// <summary>True when <paramref name="vertex"/> has weights.</summary>
    public bool Has(int vertex) => _byVertex.ContainsKey(vertex);

    /// <summary>The weighted vertex ids, ascending.</summary>
    public IEnumerable<int> Vertices => _byVertex.Keys;

    /// <summary>
    /// Normalises raw weights (bone index to weight): keeps the largest <see cref="MaxInfluences"/> (ties to the lower bone
    /// index), then divides by their sum, in bone order. <paramref name="capped"/> is true when any were dropped. The raw
    /// weights must be positive and finite, and at least one given.
    /// </summary>
    public static Influence[] Normalise(IReadOnlyDictionary<int, double> raw, out bool capped)
    {
        var kept = raw.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Take(MaxInfluences).ToArray();
        capped = kept.Length < raw.Count;
        var sum = kept.Sum(p => p.Value);
        return kept.OrderBy(p => p.Key).Select(p => new Influence(p.Key, p.Value / sum)).ToArray();
    }

    /// <summary>
    /// The influences that <paramref name="weights"/> (a bone's name to its weight, as <c>set_weights</c> and the Carve file
    /// give them) make on <paramref name="skeleton"/>, before normalising: bone index to weight, a weight of 0 left out.
    /// Null, with every problem added to <paramref name="errors"/> under <paramref name="at"/> (<c>weights</c>,
    /// <c>weights.Head</c>), when a bone is not in the skeleton, a weight is not finite or below 0, none is given, or all are 0.
    /// The one place these refusals live: the tool and the file reader both call it.
    /// </summary>
    public static Dictionary<int, double>? Raw(Skeleton skeleton, IEnumerable<KeyValuePair<string, double>> weights, string at, List<MeshError> errors)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        var before = errors.Count;
        var raw = new Dictionary<int, double>();
        var names = skeleton.Names.ToList();
        var given = 0;
        foreach (var (bone, weight) in weights)
        {
            given++;
            var index = names.IndexOf(bone);
            if (index < 0) errors.Add(new MeshError($"{at}.{bone}", $"no bone named '{bone}'; the bones are {NameList.Quoted(names)}"));
            else if (!double.IsFinite(weight) || weight < 0) errors.Add(new MeshError($"{at}.{bone}", FormattableString.Invariant($"{weight} is not a weight; weights are 0 or more")));
            else if (weight > 0) raw[index] = weight;
        }
        if (given == 0) errors.Add(new MeshError(at, "give at least one bone and its weight"));
        else if (raw.Count == 0 && errors.Count == before) errors.Add(new MeshError(at, "every weight is 0; at least one must be above 0"));
        return errors.Count > before ? null : raw;
    }

    /// <summary>Why <paramref name="vertex"/> is not a vertex of a mesh of <paramref name="vertexCount"/> vertices, or null when it is.</summary>
    public static string? VertexProblem(int vertexCount, int vertex) =>
        vertex < 0 || vertex >= vertexCount ? $"there is no vertex {vertex}: ids run 0..{vertexCount - 1}" : null;

    /// <summary>
    /// The tolerance on a sum of weights within which <see cref="Kept"/> takes them as already normalised: far above the
    /// rounding of <see cref="Normalise"/>'s own output (a few parts in 10^16), far below any weight a person writes.
    /// </summary>
    public const double NormalisedTolerance = 1e-12;

    /// <summary>
    /// The Carve file's reading of raw weights: kept bit for bit when they are already normalised (at most
    /// <see cref="MaxInfluences"/>, summing to 1 within <see cref="NormalisedTolerance"/>), as every saved file's are, so
    /// they read back exactly; otherwise <see cref="Normalise"/>d as <c>set_weights</c> does.
    /// </summary>
    public static Influence[] Kept(IReadOnlyDictionary<int, double> raw, out bool capped)
    {
        var sum = raw.OrderBy(p => p.Key).Sum(p => p.Value);
        if (raw.Count <= MaxInfluences && Math.Abs(sum - 1) <= NormalisedTolerance)
        {
            capped = false;
            return raw.OrderBy(p => p.Key).Select(p => new Influence(p.Key, p.Value)).ToArray();
        }
        return Normalise(raw, out capped);
    }

    /// <summary>Weights of exactly these influences by vertex id (each already normalised).</summary>
    public static VertexWeights Of(IEnumerable<KeyValuePair<int, Influence[]>> byVertex)
    {
        var all = new SortedDictionary<int, Influence[]>();
        foreach (var (v, i) in byVertex) all[v] = i;
        return all.Count == 0 ? None : new VertexWeights(all);
    }

    /// <summary>These weights with <paramref name="influences"/> (already normalised) on every vertex of <paramref name="vertices"/>.</summary>
    public VertexWeights With(IEnumerable<int> vertices, Influence[] influences)
    {
        var next = new SortedDictionary<int, Influence[]>(_byVertex);
        foreach (var v in vertices) next[v] = influences;
        return new VertexWeights(next);
    }
}
