using System.Numerics;

namespace Carve.Core;

/// <summary>
/// The attribute layers an edit carries, in order (T-71). Every edit in the kernel describes where its elements came from
/// (<see cref="EditProvenance"/>) and calls <see cref="Carry"/> on <see cref="Standard"/>, so a mesh's colours, materials,
/// groups, weights, hard edges, UVs and seams carry through every edit by one mechanism. Immutable.
/// </summary>
public sealed class LayerRegistry
{
    private readonly AttributeLayer[] _layers;

    private LayerRegistry(AttributeLayer[] layers) => _layers = layers;

    /// <summary>A registry of no layers.</summary>
    public static LayerRegistry Empty { get; } = new([]);

    /// <summary>The layers every mesh has, in the order they are carried (<see cref="StandardLayers"/>).</summary>
    public static LayerRegistry Standard { get; } = Empty
        .With(StandardLayers.Colour).With(StandardLayers.Material).With(StandardLayers.Groups)
        .With(StandardLayers.Weights).With(StandardLayers.Hard).With(StandardLayers.Uvs).With(StandardLayers.Seams);

    /// <summary>
    /// What the kernel's edits carry: <see cref="Standard"/> and then the morph targets (<see cref="StandardLayers.Targets"/>,
    /// T-100), kept out of <see cref="Standard"/> until the person rules on it (M-21's rebase onto T-71).
    /// </summary>
    internal static LayerRegistry Edits { get; } = Standard.With(StandardLayers.Targets);

    public IReadOnlyList<AttributeLayer> Layers => _layers;

    /// <summary>The layer named <paramref name="name"/>, or null.</summary>
    public AttributeLayer? Find(string name) => _layers.FirstOrDefault(l => l.Name == name);

    /// <summary>This registry with <paramref name="layer"/> carried last. A name already registered is refused.</summary>
    public LayerRegistry With(AttributeLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (Find(layer.Name) is not null) throw new ArgumentException($"a layer named '{layer.Name}' is registered already", nameof(layer));
        return new LayerRegistry([.. _layers, layer]);
    }

    /// <summary>
    /// <paramref name="edited"/>, an edit's geometry made from <paramref name="source"/>, with every layer carried onto it
    /// by its rules, in registry order.
    /// </summary>
    public PolygonMesh Carry(PolygonMesh source, PolygonMesh edited, EditProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(provenance);
        var mesh = edited;
        foreach (var layer in _layers) mesh = layer.Carry(source, mesh, provenance);
        return mesh;
    }
}

/// <summary>The rules the standard layers are built of (T-71), for a new layer to reuse.</summary>
public static class LayerRules
{
    /// <summary>A vertex keeps its value when it moves.</summary>
    public static bool Keep<T>(int vertex, VertexOrigin? origin, T current, IReadOnlyDictionary<int, T> source, out T value)
    {
        value = current;
        return true;
    }

    /// <summary>An edge keeps its value while its pair is an edge, whatever faces it now lies between (the person, 2026-11-18).</summary>
    public static bool Keep<T>((int Lo, int Hi) edge, T current, IReadOnlyList<int> facesBefore, IReadOnlyList<int> facesAfter, out T value)
    {
        value = current;
        return true;
    }

    /// <summary>A reshaped face keeps its value.</summary>
    public static bool Keep<T>(int face, FaceOrigin? origin, T current, out T value)
    {
        value = current;
        return true;
    }

    /// <summary>A corner keeps its value when its vertex moves, so a UV stays put (the person, 2026-11-18; the stretched-UV half of T-71's bevel finding is for a later rule).</summary>
    public static bool Keep<T>(int face, int corner, int vertex, T current, out T value)
    {
        value = current;
        return true;
    }

    /// <summary>A new face takes the value of the face it came from.</summary>
    public static bool CopyFromSourceFace<T>(FaceOrigin origin, IReadOnlyDictionary<int, T> source, out T value) =>
        source.TryGetValue(origin.Face, out value!);

    /// <summary>Each piece of a split edge takes the split edge's value.</summary>
    public static bool InheritFromSplitEdge<T>((int Lo, int Hi) parent, T current, (int Lo, int Hi) piece, out T value)
    {
        value = current;
        return true;
    }

    /// <summary>A new vertex starts at the default.</summary>
    public static bool NoValue<T>(VertexOrigin origin, IReadOnlyDictionary<int, T> source, out T value)
    {
        value = default!;
        return false;
    }

    /// <summary>
    /// The lerp of 2D values (UVs): the weighted sum in double, from 0 or from the first term as the stencil says, over its
    /// divisor, rounded to float once; one term gives its value exactly.
    /// </summary>
    public static bool Lerp(IReadOnlyList<(Vector2 Value, double Weight)> terms, Stencil stencil, out Vector2 value)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(stencil);
        if (terms.Count == 1) { value = terms[0].Value; return true; }
        double x = 0, y = 0;
        var from = 0;
        if (stencil.SumsFromFirst && terms.Count > 0)
        {
            x = terms[0].Weight * terms[0].Value.X;
            y = terms[0].Weight * terms[0].Value.Y;
            from = 1;
        }
        for (var i = from; i < terms.Count; i++)
        {
            x += terms[i].Weight * terms[i].Value.X;
            y += terms[i].Weight * terms[i].Value.Y;
        }
        var d = stencil.Divisor;
        value = d == 1 ? new Vector2((float)x, (float)y) : new Vector2((float)(x / d), (float)(y / d));
        return true;
    }

    /// <summary>
    /// The weights rule: a blend's sources' bone weights summed by their stencil weights, a bone not above 0 dropped, the
    /// largest four kept (ties to the lower bone) and normalised to sum to 1 (<see cref="VertexWeights.Normalise"/>); an
    /// unweighted source adds nothing, and nothing left is no value. An offset vertex (an extrude's ring) starts unweighted
    /// (the person, 2026-11-18). Only the weights before the edit are read, so a source that is itself a new vertex adds
    /// nothing: an edit that blends weights gives its stencils over the vertices it started from.
    /// </summary>
    public static bool BlendWeights(VertexOrigin origin, IReadOnlyDictionary<int, Influence[]> source, out Influence[] value)
    {
        value = [];
        if (origin.Kind != VertexOriginKind.Blend) return false;
        var raw = new SortedDictionary<int, double>();
        foreach (var (v, c) in origin.Sources.Terms)
            if (source.TryGetValue(v, out var influences))
                foreach (var influence in influences)
                    raw[influence.Bone] = raw.GetValueOrDefault(influence.Bone) + c * influence.Weight;
        var positive = raw.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value);
        if (positive.Count == 0) return false;
        value = VertexWeights.Normalise(positive, out _);
        return true;
    }

    /// <summary>A weighted vertex keeps its weights when it moves, and takes the weights rule when it is re-made as a blend (subdivide).</summary>
    public static bool ReblendWeights(int vertex, VertexOrigin? origin, Influence[] current, IReadOnlyDictionary<int, Influence[]> source, out Influence[] value)
    {
        if (origin is null) { value = current; return true; }
        return BlendWeights(origin, source, out value);
    }

    /// <summary>
    /// The morph targets rule (T-100): a blend's delta in each target is its stencil over the sources' deltas, worked as the
    /// stencil works a position (<see cref="Stencil.Apply"/>: summed in double, over its divisor, rounded once; one term
    /// gives its delta exactly), so a target stays the edit of the morphed mesh. An offset vertex (an extrude's ring)
    /// copies its source's deltas. A source with no value adds a zero delta.
    /// </summary>
    public static bool BlendDeltas(VertexOrigin origin, IReadOnlyDictionary<int, Vector3[]> source, out Vector3[] value)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(source);
        value = [];
        var count = source.Values.FirstOrDefault()?.Length ?? 0;
        if (count == 0) return false;
        if (origin.Kind == VertexOriginKind.Offset)
        {
            var from = origin.Sources.Terms[0].Element;
            value = source.TryGetValue(from, out var own) ? [.. own] : new Vector3[count];
            return true;
        }
        value = new Vector3[count];
        for (var k = 0; k < count; k++) value[k] = origin.Sources.Apply(new DeltaView(source, k));
        return true;
    }

    /// <summary>A vertex keeps its deltas when it moves, and takes the morph targets rule when it is re-made as a blend (subdivide).</summary>
    public static bool ReblendDeltas(int vertex, VertexOrigin? origin, Vector3[] current, IReadOnlyDictionary<int, Vector3[]> source, out Vector3[] value)
    {
        if (origin is null) { value = current; return true; }
        return BlendDeltas(origin, source, out value);
    }

    /// <summary>One target's deltas by vertex id, a vertex with none reading zero, for a stencil to work.</summary>
    private sealed class DeltaView(IReadOnlyDictionary<int, Vector3[]> source, int target) : IReadOnlyList<Vector3>
    {
        public Vector3 this[int index] => source.TryGetValue(index, out var d) ? d[target] : Vector3.Zero;
        public int Count => source.Count == 0 ? 0 : source.Keys.Max() + 1;
        public IEnumerator<Vector3> GetEnumerator() => Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>2D values equal bit for bit (a merge's UVs, the person's ruling 3c, 2026-11-13).</summary>
    public static IEqualityComparer<Vector2> SameBits { get; } = new BitwiseVector2();

    private sealed class BitwiseVector2 : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 a, Vector2 b) =>
            BitConverter.SingleToInt32Bits(a.X) == BitConverter.SingleToInt32Bits(b.X)
            && BitConverter.SingleToInt32Bits(a.Y) == BitConverter.SingleToInt32Bits(b.Y);

        public int GetHashCode(Vector2 v) => HashCode.Combine(BitConverter.SingleToInt32Bits(v.X), BitConverter.SingleToInt32Bits(v.Y));
    }
}

/// <summary>
/// The layers every mesh has (T-71), each reading and writing the storage the mesh already holds: <see cref="FaceStyle"/>
/// for colours, materials and groups, <see cref="VertexWeights"/>, <see cref="MeshShade"/> for hard edges (the shading mode,
/// per mesh, carries with them), and <see cref="MeshUvs"/> for UVs and seams.
/// </summary>
public static class StandardLayers
{
    /// <summary>A face's colour, <c>#RRGGBB</c>: a new face copies its source face's.</summary>
    public static FaceLayer<string> Colour { get; } = new("colour", null!,
        m => m.Style?.Colours ?? Empty<int, string>(),
        (_, edited, values) => edited.WithStyle((edited.Style ?? FaceStyle.None).WithColours(values)),
        LayerRules.CopyFromSourceFace);

    /// <summary>A face's material name (T-74): a new face copies its source face's.</summary>
    public static FaceLayer<string> Material { get; } = new("material", null!,
        m => m.Style?.Materials ?? Empty<int, string>(),
        (_, edited, values) => edited.WithStyle((edited.Style ?? FaceStyle.None).WithMaterials(values)),
        LayerRules.CopyFromSourceFace);

    /// <summary>The groups a face is in, in the groups' definition order: a new face joins its source face's. A group left with no face goes.</summary>
    public static FaceLayer<string[]> Groups { get; } = new("groups", [],
        m => GroupsByFace(m.Style),
        (source, edited, values) => edited.WithStyle((edited.Style ?? FaceStyle.None).WithGroups(
            (source.Style?.Groups ?? []).Select(g => (g.Name, values.Where(p => p.Value.Contains(g.Name)).Select(p => p.Key).Order().ToArray()))
                .Where(g => g.Item2.Length > 0).ToList())),
        LayerRules.CopyFromSourceFace);

    /// <summary>A vertex's bone weights (D-6): a new vertex takes the weights rule over its sources, a re-made one too, a moved one keeps its own.</summary>
    public static VertexLayer<Influence[]> Weights { get; } = new("weights", [],
        m => m.Weights is { } w ? w.Vertices.ToDictionary(v => v, v => w.Of(v).ToArray()) : Empty<int, Influence[]>(),
        (_, edited, values) => edited.WithWeights(VertexWeights.Of(values)),
        LayerRules.BlendWeights, LayerRules.ReblendWeights);

    /// <summary>A hard edge (T-42): the pieces of a split hard edge are hard, a new edge is soft, and a pair that is no longer an edge drops its mark.</summary>
    public static EdgeLayer<bool> Hard { get; } = new("hard", false,
        m => m.HardEdges.ToDictionary(e => e, _ => true),
        (source, edited, values) => edited.WithShade(MeshShade.Of(source.ShadingMode, values.Where(p => p.Value).Select(p => p.Key))),
        LayerRules.InheritFromSplitEdge);

    /// <summary>A face's corner UVs (T-53): a piece lerps its source face's UVs, a merged face keeps them where its faces agree, a wall starts unmapped.</summary>
    public static CornerLayer<Vector2> Uvs { get; } = new("uvs", default,
        m => m.Uvs is { } u ? u.MappedFaces.ToDictionary(f => f, f => u.Of(f)!.ToArray()) : Empty<int, Vector2[]>(),
        (_, edited, values) => edited.WithUvs((edited.Uvs ?? MeshUvs.None).WithOnlyFaces(values)),
        LayerRules.Lerp, LayerRules.SameBits);

    /// <summary>A seam (T-53): carried as a hard edge is.</summary>
    public static EdgeLayer<bool> Seams { get; } = new("seams", false,
        m => m.Seams.ToDictionary(e => e, _ => true),
        (_, edited, values) => edited.WithUvs((edited.Uvs ?? MeshUvs.None).WithOnlySeams(values.Where(p => p.Value).Select(p => p.Key))),
        LayerRules.InheritFromSplitEdge);

    /// <summary>
    /// A vertex's deltas in every morph target, in the targets' definition order (D-6, T-99, T-100): a new vertex takes its
    /// stencil over its sources' deltas as its position does, an extrude's ring copies the corner it stands over, a re-made
    /// one (subdivide) takes its stencil, a moved one keeps its own, and a deleted vertex's deltas go. Every live vertex is
    /// read, a zero delta too, so a re-made vertex with none is blended; a zero delta is not stored.
    /// </summary>
    public static VertexLayer<Vector3[]> Targets { get; } = new("targets", [],
        m => m.Targets is { } t
            ? m.LiveVertices.ToDictionary(v => v, v => t.All.Select(x => x.DeltaOf(v)).ToArray())
            : Empty<int, Vector3[]>(),
        (source, edited, values) => source.Targets is { } t
            ? edited.WithTargets(MorphTargets.Of(t.All.Select((x, k) => MorphTarget.Of(x.Name, values.Select(p => KeyValuePair.Create(p.Key, p.Value[k]))))))
            : edited,
        LayerRules.BlendDeltas, LayerRules.ReblendDeltas);

    private static Dictionary<int, string[]> GroupsByFace(FaceStyle? style)
    {
        var byFace = new SortedDictionary<int, List<string>>();
        foreach (var (name, faces) in style?.Groups ?? [])
            foreach (var f in faces)
            {
                if (!byFace.TryGetValue(f, out var names)) byFace[f] = names = [];
                names.Add(name);
            }
        return byFace.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }

    private static IReadOnlyDictionary<TKey, T> Empty<TKey, T>() where TKey : notnull => new Dictionary<TKey, T>();
}
