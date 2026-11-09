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

    /// <summary>These weights with <paramref name="influences"/> (already normalised) on every vertex of <paramref name="vertices"/>.</summary>
    public VertexWeights With(IEnumerable<int> vertices, Influence[] influences)
    {
        var next = new SortedDictionary<int, Influence[]>(_byVertex);
        foreach (var v in vertices) next[v] = influences;
        return new VertexWeights(next);
    }
}
