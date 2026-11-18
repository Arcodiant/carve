namespace Carve.Core;

/// <summary>The kind of element an attribute layer keys its values by (T-71).</summary>
public enum LayerElement
{
    /// <summary>A vertex, by id.</summary>
    Vertex,

    /// <summary>An edge, by its vertex pair, lower id first.</summary>
    Edge,

    /// <summary>A face, by id.</summary>
    Face,

    /// <summary>A face corner: a face id, and the corner's place in <see cref="Face.Corners"/>. A face has a value at every corner or at none.</summary>
    Corner,
}

/// <summary>How a vertex was made from others (<see cref="VertexOrigin"/>).</summary>
public enum VertexOriginKind
{
    /// <summary>A weighted blend of its sources: an edge point, a face point, a point inside a face, a subdivided vertex point.</summary>
    Blend,

    /// <summary>One source vertex offset in space (an extrude's ring): it stands over its source, but is not made of it.</summary>
    Offset,
}

/// <summary>
/// Where a vertex of an edited mesh came from (T-71): its <see cref="Kind"/> and its sources, a stencil over the vertex ids
/// of the mesh before the edit (or, for a cut, earlier new vertices). A layer's rules decide what each kind gives.
/// </summary>
public sealed record VertexOrigin(VertexOriginKind Kind, Stencil Sources)
{
    public static VertexOrigin Blend(Stencil sources) => new(VertexOriginKind.Blend, sources);

    public static VertexOrigin Offset(int vertex) => new(VertexOriginKind.Offset, Stencil.Of(vertex));
}

/// <summary>How a new or reshaped face's corner values are found from its source face (<see cref="FaceOrigin"/>).</summary>
public enum CornerOriginKind
{
    /// <summary>Corner by corner, by place in <see cref="Face.Corners"/>, when the corner count is unchanged; otherwise none.</summary>
    Position,

    /// <summary>By vertex: a corner at a vertex of the source face takes that corner's value, and a new vertex takes its own
    /// <see cref="VertexOrigin"/>'s blend over them; a vertex the source face does not reach leaves the face without values.</summary>
    Vertex,

    /// <summary>By the face's own <see cref="FaceOrigin.CornerStencils"/>, one per corner, over the source face's corner indices.</summary>
    Stencils,

    /// <summary>A merge: each corner takes the value every face of <see cref="FaceOrigin.Joined"/> at its vertex gives, when they
    /// all have values and agree bit for bit; otherwise the face has none.</summary>
    Agree,

    /// <summary>None: the face starts without corner values (an extrude's walls, a bridge's quads).</summary>
    None,
}

/// <summary>
/// Where a new or reshaped face came from (T-71): the face whose face values it takes (<see cref="Face"/>, an id of the
/// mesh before the edit) and how its corner values are found (<see cref="Corners"/>).
/// </summary>
public sealed record FaceOrigin(int Face, CornerOriginKind Corners)
{
    /// <summary>For <see cref="CornerOriginKind.Stencils"/>: one stencil per corner of the new face, over <see cref="Face"/>'s corner indices.</summary>
    public IReadOnlyList<Stencil>? CornerStencils { get; init; }

    /// <summary>For <see cref="CornerOriginKind.Agree"/>: the faces merged into this one, ascending.</summary>
    public IReadOnlyList<int>? Joined { get; init; }
}

/// <summary>
/// What an edit says about where the elements of its result came from (T-71): each new or re-made vertex's
/// <see cref="VertexOrigin"/>, each new or reshaped face's <see cref="FaceOrigin"/>, and each edge that was split, with the
/// new vertices along it. <see cref="LayerRegistry.Carry"/> fills every layer from it. An element it does not name is
/// kept (an existing one, whose id holds) or starts at its layers' defaults (a new one). Built by the edit, then read only.
/// </summary>
public sealed class EditProvenance
{
    private readonly SortedDictionary<int, VertexOrigin> _vertices = [];
    private readonly SortedDictionary<int, FaceOrigin> _faces = [];
    private readonly SortedDictionary<(int Lo, int Hi), int[]> _splits = [];

    /// <summary>Vertex id in the edited mesh to its origin: new vertices, and existing ones re-made (subdivide's vertex points).</summary>
    public IReadOnlyDictionary<int, VertexOrigin> Vertices => _vertices;

    /// <summary>Face id in the edited mesh to its origin: new faces, and reshaped ones that say how their corners carry.</summary>
    public IReadOnlyDictionary<int, FaceOrigin> Faces => _faces;

    /// <summary>Each split edge of the mesh before the edit, lower id first, to the new vertices along it from its lower end.</summary>
    public IReadOnlyDictionary<(int Lo, int Hi), int[]> SplitEdges => _splits;

    public EditProvenance Vertex(int id, VertexOrigin origin)
    {
        _vertices[id] = origin;
        return this;
    }

    public EditProvenance Face(int id, FaceOrigin origin)
    {
        _faces[id] = origin;
        return this;
    }

    /// <summary>Edge <paramref name="a"/>-<paramref name="b"/> split by <paramref name="along"/>, given from <paramref name="a"/> toward <paramref name="b"/>.</summary>
    public EditProvenance Split(int a, int b, IEnumerable<int> along)
    {
        var chain = along.ToArray();
        if (a > b) Array.Reverse(chain);
        _splits[(Math.Min(a, b), Math.Max(a, b))] = chain;
        return this;
    }

    /// <summary>The pieces of split edge <paramref name="edge"/>, from its lower end, each lower id first.</summary>
    public IEnumerable<(int Lo, int Hi)> Pieces((int Lo, int Hi) edge)
    {
        var path = _splits[edge].Prepend(edge.Lo).Append(edge.Hi).ToArray();
        for (var i = 0; i + 1 < path.Length; i++)
            yield return path[i] < path[i + 1] ? (path[i], path[i + 1]) : (path[i + 1], path[i]);
    }
}
