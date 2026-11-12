namespace Carve.Core;

/// <summary>
/// What one extruded face became: <see cref="Cap"/> is the face itself, which keeps its id with the new rings as its loop
/// and holes; <see cref="Walls"/> are the quads appended for it, one per edge in corner order (<see cref="Face.Corners"/>:
/// the outer loop, then each hole), wall <c>k</c> running from corner <c>k</c> to the next corner of its loop;
/// <see cref="NewVertices"/> are the rings in the same corner order (new vertex <c>k</c> stands over corner <c>k</c>).
/// </summary>
public sealed record ExtrudedFace(int Face, int Cap, IReadOnlyList<int> Walls, IReadOnlyList<int> NewVertices);

/// <summary>
/// The outcome of an edit in <see cref="MeshEdits"/>: the new mesh and what the edit made, or every error that stopped it.
/// On failure <see cref="Mesh"/> is null and the id lists are empty; the mesh the edit was given is never changed.
/// </summary>
public sealed class EditResult
{
    private EditResult(PolygonMesh? mesh, IReadOnlyList<MeshError> errors, IReadOnlyList<int> movedVertices,
        IReadOnlyList<int> newVertices, IReadOnlyList<int> newFaces, IReadOnlyList<ExtrudedFace> extruded)
    {
        Mesh = mesh;
        Errors = errors;
        MovedVertices = movedVertices;
        NewVertices = newVertices;
        NewFaces = newFaces;
        Extruded = extruded;
    }

    /// <summary>The edited mesh, or null when the edit was refused.</summary>
    public PolygonMesh? Mesh { get; }

    /// <summary>Every problem found, each named by element (<c>vertices[n]</c>, <c>faces[n]</c>, <c>edges[a-b]</c>) or by argument.</summary>
    public IReadOnlyList<MeshError> Errors { get; }

    public bool Succeeded => Mesh is not null;

    /// <summary>Ids of the existing vertices the edit moved, ascending (a move's vertices, or those a subdivide moved to their vertex points; empty for an extrude).</summary>
    public IReadOnlyList<int> MovedVertices { get; }

    /// <summary>Ids of the vertices the edit appended, ascending.</summary>
    public IReadOnlyList<int> NewVertices { get; }

    /// <summary>Ids of the faces the edit appended, ascending. An extruded face's cap is not new: it keeps its id.</summary>
    public IReadOnlyList<int> NewFaces { get; }

    /// <summary>For an extrude, one record per listed face, in the order listed; empty for a move.</summary>
    public IReadOnlyList<ExtrudedFace> Extruded { get; }

    internal static EditResult Refused(IReadOnlyList<MeshError> errors) => new(null, errors, [], [], [], []);

    /// <summary>The result of an edit that has already carried colours, groups, weights and shading onto <paramref name="mesh"/>.</summary>
    internal static EditResult Made(PolygonMesh mesh, IReadOnlyList<int> movedVertices, IReadOnlyList<int> newVertices,
        IReadOnlyList<int> newFaces) => new(mesh, [], movedVertices, newVertices, newFaces, []);

    private static PolygonMesh Carried(PolygonMesh source, PolygonMesh edited, IReadOnlyList<ExtrudedFace> extruded)
    {
        var mesh = source.Style is { } style ? edited.WithStyle(style.Carry(extruded)) : edited;
        mesh = source.Weights is { } weights ? mesh.WithWeights(weights) : mesh;
        // The mode and the hard edges carry; a hard edge the edit no longer has is dropped (T-42).
        if (source.Shade is not { } shade) return mesh;
        var gone = shade.Hard.Where(e => !mesh.Topology.TryGetEdge(e.Lo, e.Hi, out _)).ToArray();
        return mesh.WithShade(gone.Length == 0 ? shade : shade.WithEdges(gone.Select(e => (e.Lo, e.Hi)), false));
    }

    /// <summary>The result of an edit of <paramref name="source"/>: the new mesh keeps the source's face colours and groups, and walls take their source face's.</summary>
    internal static EditResult From(PolygonMesh source, MeshResult result, IReadOnlyList<int> movedVertices,
        IReadOnlyList<int> newVertices, IReadOnlyList<int> newFaces, IReadOnlyList<ExtrudedFace> extruded)
        => result.Succeeded
            ? new EditResult(Carried(source, result.Mesh!, extruded),
                result.Errors, movedVertices, newVertices, newFaces, extruded)
            : Refused(result.Errors);
}
