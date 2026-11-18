namespace Carve.Core;

/// <summary>
/// One per-element data layer of a mesh (T-71): a name, the element kind it keys by, a value type, a default (what an
/// element with no value has), and its rules for what an edit does to it, as <see cref="EditProvenance"/> describes the
/// edit. A layer reads its values from a mesh and writes them back, so it adds no storage of its own; the standard layers
/// are in <see cref="LayerRegistry.Standard"/>. Every rule is deterministic: elements are visited in id order, edges by
/// pair, and no rule draws on anything but its inputs.
/// </summary>
public abstract class AttributeLayer
{
    private protected AttributeLayer(string name, LayerElement element)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Element = element;
    }

    public string Name { get; }

    public LayerElement Element { get; }

    /// <summary>The type of one element's value.</summary>
    public abstract Type ValueType { get; }

    /// <summary>
    /// <paramref name="edited"/> (the edit's geometry) with this layer's values carried from <paramref name="source"/> (the
    /// mesh before the edit) by its rules.
    /// </summary>
    public abstract PolygonMesh Carry(PolygonMesh source, PolygonMesh edited, EditProvenance provenance);
}

/// <summary>Reads a layer's values from a mesh: element key to value, for the elements that have one.</summary>
public delegate IReadOnlyDictionary<TKey, T> LayerReader<TKey, T>(PolygonMesh mesh) where TKey : notnull;

/// <summary>Writes a layer's carried values onto <paramref name="edited"/>; <paramref name="source"/> is the mesh before the edit.</summary>
public delegate PolygonMesh LayerWriter<TKey, T>(PolygonMesh source, PolygonMesh edited, IReadOnlyDictionary<TKey, T> values) where TKey : notnull;

/// <summary>A layer keyed by vertex id (<see cref="LayerElement.Vertex"/>).</summary>
public sealed class VertexLayer<T> : AttributeLayer
{
    /// <summary>A new vertex's value from its origin and the values before the edit; false leaves it at the default.</summary>
    public delegate bool CreateRule(VertexOrigin origin, IReadOnlyDictionary<int, T> source, out T value);

    /// <summary>
    /// What an existing vertex's value becomes when it moves, or is re-made with an <paramref name="origin"/> (subdivide's
    /// vertex points); false leaves it at the default. Called only for a vertex that has a value.
    /// </summary>
    public delegate bool MoveRule(int vertex, VertexOrigin? origin, T current, IReadOnlyDictionary<int, T> source, out T value);

    private readonly LayerReader<int, T> _read;
    private readonly LayerWriter<int, T> _write;

    public VertexLayer(string name, T defaultValue, LayerReader<int, T> read, LayerWriter<int, T> write, CreateRule create, MoveRule? move = null)
        : base(name, LayerElement.Vertex)
    {
        Default = defaultValue;
        _read = read;
        _write = write;
        Create = create;
        Move = move ?? LayerRules.Keep;
    }

    public T Default { get; }
    public CreateRule Create { get; }
    public MoveRule Move { get; }
    public override Type ValueType => typeof(T);

    /// <summary>The carried values: an existing live vertex keeps its value or takes <see cref="Move"/>'s, a new one takes <see cref="Create"/>'s from its origin, a deleted one has none.</summary>
    public IReadOnlyDictionary<int, T> Carried(PolygonMesh source, PolygonMesh edited, EditProvenance provenance)
    {
        var before = _read(source);
        var after = new SortedDictionary<int, T>();
        foreach (var (v, value) in before.OrderBy(p => p.Key))
        {
            if (v >= edited.Vertices.Count || edited.IsVertexDeleted(v)) continue;
            var origin = provenance.Vertices.GetValueOrDefault(v);
            if (origin is null && edited.Vertices[v].Equals(source.Vertices[v])) after[v] = value;
            else if (Move(v, origin, value, before, out var moved)) after[v] = moved;
        }
        foreach (var (v, origin) in provenance.Vertices)
        {
            if (v < source.Vertices.Count || edited.IsVertexDeleted(v)) continue;
            if (Create(origin, before, out var made)) after[v] = made;
        }
        return after;
    }

    public override PolygonMesh Carry(PolygonMesh source, PolygonMesh edited, EditProvenance provenance) =>
        _write(source, edited, Carried(source, edited, provenance));
}

/// <summary>A layer keyed by an edge's vertex pair, lower id first (<see cref="LayerElement.Edge"/>).</summary>
public sealed class EdgeLayer<T> : AttributeLayer
{
    /// <summary>A piece of a split edge's value, from the split edge's; false leaves the piece at the default.</summary>
    public delegate bool SplitRule((int Lo, int Hi) parent, T value, (int Lo, int Hi) piece, out T result);

    /// <summary>
    /// What a value becomes when its pair is still an edge after the edit, between <paramref name="facesAfter"/> where it was
    /// between <paramref name="facesBefore"/> (both ascending face ids; they differ when the edit reshaped a face across it);
    /// false leaves it at the default.
    /// </summary>
    public delegate bool ReshapeRule((int Lo, int Hi) edge, T value, IReadOnlyList<int> facesBefore, IReadOnlyList<int> facesAfter, out T result);

    private readonly LayerReader<(int Lo, int Hi), T> _read;
    private readonly LayerWriter<(int Lo, int Hi), T> _write;

    public EdgeLayer(string name, T defaultValue, LayerReader<(int Lo, int Hi), T> read, LayerWriter<(int Lo, int Hi), T> write,
        SplitRule split, ReshapeRule? reshape = null)
        : base(name, LayerElement.Edge)
    {
        Default = defaultValue;
        _read = read;
        _write = write;
        Split = split;
        Reshape = reshape ?? LayerRules.Keep;
    }

    public T Default { get; }
    public SplitRule Split { get; }
    public ReshapeRule Reshape { get; }
    public override Type ValueType => typeof(T);

    /// <summary>
    /// The carried values: a split edge's pieces take <see cref="Split"/>'s, a pair still an edge takes <see cref="Reshape"/>'s,
    /// and a pair that is no longer an edge has none. An edge the edit made, not a piece, starts at the default.
    /// </summary>
    public IReadOnlyDictionary<(int Lo, int Hi), T> Carried(PolygonMesh source, PolygonMesh edited, EditProvenance provenance)
    {
        var after = new SortedDictionary<(int Lo, int Hi), T>();
        foreach (var (edge, value) in _read(source).OrderBy(p => p.Key))
        {
            if (provenance.SplitEdges.ContainsKey(edge))
            {
                foreach (var piece in provenance.Pieces(edge))
                    if (Split(edge, value, piece, out var result)) after[piece] = result;
                continue;
            }
            if (!edited.Topology.TryGetEdge(edge.Lo, edge.Hi, out var now)) continue;
            var was = source.Topology.TryGetEdge(edge.Lo, edge.Hi, out var old) ? FacesOf(old) : [];
            if (Reshape(edge, value, was, FacesOf(now), out var kept)) after[edge] = kept;
        }
        return after;
    }

    private static int[] FacesOf(MeshEdge edge) => [.. edge.Faces.Select(u => u.Face).Distinct().Order()];

    public override PolygonMesh Carry(PolygonMesh source, PolygonMesh edited, EditProvenance provenance) =>
        _write(source, edited, Carried(source, edited, provenance));
}

/// <summary>A layer keyed by face id (<see cref="LayerElement.Face"/>).</summary>
public sealed class FaceLayer<T> : AttributeLayer
{
    /// <summary>A new face's value from its origin and the values before the edit; false leaves it at the default.</summary>
    public delegate bool CreateRule(FaceOrigin origin, IReadOnlyDictionary<int, T> source, out T value);

    /// <summary>What a reshaped face's value becomes (its id holds; <paramref name="origin"/> when the edit gave one); false leaves it at the default.</summary>
    public delegate bool ReshapeRule(int face, FaceOrigin? origin, T current, out T value);

    private readonly LayerReader<int, T> _read;
    private readonly LayerWriter<int, T> _write;

    public FaceLayer(string name, T defaultValue, LayerReader<int, T> read, LayerWriter<int, T> write, CreateRule create, ReshapeRule? reshape = null)
        : base(name, LayerElement.Face)
    {
        Default = defaultValue;
        _read = read;
        _write = write;
        Create = create;
        Reshape = reshape ?? LayerRules.Keep;
    }

    public T Default { get; }
    public CreateRule Create { get; }
    public ReshapeRule Reshape { get; }
    public override Type ValueType => typeof(T);

    /// <summary>The carried values: a live face keeps its value or takes <see cref="Reshape"/>'s, a new face with an origin takes <see cref="Create"/>'s, a deleted face has none.</summary>
    public IReadOnlyDictionary<int, T> Carried(PolygonMesh source, PolygonMesh edited, EditProvenance provenance)
    {
        var before = _read(source);
        var after = new SortedDictionary<int, T>();
        foreach (var (f, value) in before.OrderBy(p => p.Key))
        {
            if (f >= edited.Faces.Count || edited.IsFaceDeleted(f)) continue;
            var origin = provenance.Faces.GetValueOrDefault(f);
            if (origin is null && edited.Faces[f].Equals(source.Faces[f])) after[f] = value;
            else if (Reshape(f, origin, value, out var reshaped)) after[f] = reshaped;
        }
        foreach (var (f, origin) in provenance.Faces)
        {
            if (f < source.Faces.Count || edited.IsFaceDeleted(f)) continue;
            if (Create(origin, before, out var made)) after[f] = made;
        }
        return after;
    }

    public override PolygonMesh Carry(PolygonMesh source, PolygonMesh edited, EditProvenance provenance) =>
        _write(source, edited, Carried(source, edited, provenance));
}

/// <summary>
/// A layer of face corners (<see cref="LayerElement.Corner"/>), keyed by face id with one value per entry of the face's
/// <see cref="Face.Corners"/>: a face has a value at every corner or at none.
/// </summary>
public sealed class CornerLayer<T> : AttributeLayer
{
    /// <summary>
    /// The value a stencil makes of <paramref name="terms"/> (each source value and its weight, in the stencil's order) over
    /// <paramref name="divisor"/>: the lerp of UVs; false leaves the face without values.
    /// </summary>
    public delegate bool CombineRule(IReadOnlyList<(T Value, double Weight)> terms, double divisor, out T value);

    /// <summary>What a corner's value becomes when its vertex moves; false leaves the face without values.</summary>
    public delegate bool MoveRule(int face, int corner, int vertex, T current, out T value);

    private readonly LayerReader<int, T[]> _read;
    private readonly LayerWriter<int, T[]> _write;

    public CornerLayer(string name, T defaultValue, LayerReader<int, T[]> read, LayerWriter<int, T[]> write,
        CombineRule combine, IEqualityComparer<T> agree, MoveRule? move = null)
        : base(name, LayerElement.Corner)
    {
        Default = defaultValue;
        _read = read;
        _write = write;
        Combine = combine;
        Agree = agree;
        Move = move ?? LayerRules.Keep;
    }

    public T Default { get; }
    public CombineRule Combine { get; }

    /// <summary>When two values are the same, for a merge (<see cref="CornerOriginKind.Agree"/>).</summary>
    public IEqualityComparer<T> Agree { get; }

    public MoveRule Move { get; }
    public override Type ValueType => typeof(T);

    /// <summary>
    /// The carried values. A face with a <see cref="FaceOrigin"/> takes its corners by the origin's
    /// <see cref="CornerOriginKind"/>; a live face without one keeps its values when its corner count holds (by place) and
    /// loses them when it changes; a deleted face has none. Then each kept corner whose vertex moved takes <see cref="Move"/>'s.
    /// </summary>
    public IReadOnlyDictionary<int, T[]> Carried(PolygonMesh source, PolygonMesh edited, EditProvenance provenance)
    {
        var before = _read(source);
        var after = new SortedDictionary<int, T[]>();
        foreach (var (f, values) in before.OrderBy(p => p.Key))
        {
            if (f >= edited.Faces.Count || edited.IsFaceDeleted(f) || provenance.Faces.ContainsKey(f)) continue;
            if (edited.Faces[f].Corners.Count != source.Faces[f].Corners.Count) continue;
            if (Moved(edited.Faces[f], f, values, source, edited) is { } kept) after[f] = kept;
        }
        foreach (var (f, origin) in provenance.Faces)
        {
            if (edited.IsFaceDeleted(f)) continue;
            if (FromOrigin(edited.Faces[f], origin, before, source, provenance) is { } made) after[f] = made;
        }
        return after;
    }

    private T[]? Moved(Face face, int f, T[] values, PolygonMesh source, PolygonMesh edited)
    {
        T[]? copy = null;
        for (var c = 0; c < values.Length; c++)
        {
            var v = face.Corners[c];
            if (v >= source.Vertices.Count || edited.Vertices[v].Equals(source.Vertices[v])) continue;
            if (!Move(f, c, v, values[c], out var moved)) return null;
            (copy ??= (T[])values.Clone())[c] = moved;
        }
        return copy ?? values;
    }

    private T[]? FromOrigin(Face face, FaceOrigin origin, IReadOnlyDictionary<int, T[]> before, PolygonMesh source, EditProvenance provenance)
    {
        switch (origin.Corners)
        {
            case CornerOriginKind.None:
                return null;
            case CornerOriginKind.Position:
                return before.TryGetValue(origin.Face, out var same) && same.Length == face.Corners.Count ? same : null;
            case CornerOriginKind.Stencils:
            {
                if (!before.TryGetValue(origin.Face, out var own) || origin.CornerStencils is not { } stencils) return null;
                var result = new T[face.Corners.Count];
                for (var c = 0; c < result.Length; c++)
                    if (!Apply(stencils[c], i => (true, own[i]), out result[c])) return null;
                return result;
            }
            case CornerOriginKind.Vertex:
            {
                if (!before.TryGetValue(origin.Face, out var own)) return null;
                var sourceFace = source.Faces[origin.Face];
                (bool, T) At(int v)
                {
                    if (v < source.Vertices.Count)
                        return sourceFace.Corners.Contains(v) ? (true, own[sourceFace.CornerOf(v)]) : (false, default!);
                    if (provenance.Vertices.GetValueOrDefault(v) is not { Kind: VertexOriginKind.Blend } blend) return (false, default!);
                    return Apply(blend.Sources, At, out var made) ? (true, made) : (false, default!);
                }
                var result = new T[face.Corners.Count];
                for (var c = 0; c < result.Length; c++)
                {
                    var (has, value) = At(face.Corners[c]);
                    if (!has) return null;
                    result[c] = value;
                }
                return result;
            }
            case CornerOriginKind.Agree:
            {
                var joined = origin.Joined ?? [origin.Face];
                if (joined.Any(j => !before.ContainsKey(j))) return null;
                var result = new T[face.Corners.Count];
                for (var c = 0; c < result.Length; c++)
                {
                    var v = face.Corners[c];
                    var found = false;
                    foreach (var j in joined)
                    {
                        var sourceFace = source.Faces[j];
                        if (!sourceFace.Corners.Contains(v)) continue;
                        var value = before[j][sourceFace.CornerOf(v)];
                        if (found && !Agree.Equals(result[c], value)) return null;
                        result[c] = value;
                        found = true;
                    }
                    if (!found) return null;
                }
                return result;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin.Corners, "no such corner origin");
        }
    }

    private bool Apply(Stencil stencil, Func<int, (bool Has, T Value)> at, out T value)
    {
        var terms = new List<(T, double)>();
        foreach (var (element, weight) in stencil.Terms)
        {
            var (has, v) = at(element);
            if (!has) { value = default!; return false; }
            terms.Add((v, weight));
        }
        return Combine(terms, stencil.Divisor, out value);
    }

    public override PolygonMesh Carry(PolygonMesh source, PolygonMesh edited, EditProvenance provenance) =>
        _write(source, edited, Carried(source, edited, provenance));
}
