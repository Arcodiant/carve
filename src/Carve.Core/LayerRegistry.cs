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

    /// <summary>A corner keeps its value when its vertex moves (so a UV stays put; the bevel finding of T-71 is for a later rule).</summary>
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
    /// The lerp of 2D values (UVs): the weighted sum in double over the divisor, rounded to float once; one term gives its
    /// value exactly.
    /// </summary>
    public static bool Lerp(IReadOnlyList<(Vector2 Value, double Weight)> terms, double divisor, out Vector2 value)
    {
        if (terms.Count == 1) { value = terms[0].Value; return true; }
        double x = 0, y = 0;
        foreach (var (v, c) in terms)
        {
            x += c * v.X;
            y += c * v.Y;
        }
        value = divisor == 1 ? new Vector2((float)x, (float)y) : new Vector2((float)(x / divisor), (float)(y / divisor));
        return true;
    }

    /// <summary>
    /// The weights rule: a blend's sources' bone weights summed by their stencil weights, a bone not above 0 dropped, the
    /// largest four kept (ties to the lower bone) and normalised to sum to 1 (<see cref="VertexWeights.Normalise"/>); an
    /// unweighted source adds nothing, and nothing left is no value. An offset vertex (an extrude's ring) starts unweighted
    /// (the person, 2026-11-18).
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
