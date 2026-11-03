namespace Carve.Core;

/// <summary>
/// What one extruded face became: <see cref="Cap"/> is the face itself, which keeps its id with the new ring as its loop;
/// <see cref="Walls"/> are the quads appended for it, one per edge in loop order (wall <c>k</c> runs from corner <c>k</c> to
/// <c>k + 1</c>); <see cref="NewVertices"/> is the ring, in the face's loop order (new vertex <c>k</c> stands over corner <c>k</c>).
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

    /// <summary>Ids of the existing vertices the edit moved, ascending (a move's vertices; empty for an extrude).</summary>
    public IReadOnlyList<int> MovedVertices { get; }

    /// <summary>Ids of the vertices the edit appended, ascending.</summary>
    public IReadOnlyList<int> NewVertices { get; }

    /// <summary>Ids of the faces the edit appended, ascending. An extruded face's cap is not new: it keeps its id.</summary>
    public IReadOnlyList<int> NewFaces { get; }

    /// <summary>For an extrude, one record per listed face, in the order listed; empty for a move.</summary>
    public IReadOnlyList<ExtrudedFace> Extruded { get; }

    internal static EditResult Refused(IReadOnlyList<MeshError> errors) => new(null, errors, [], [], [], []);

    internal static EditResult From(MeshResult result, IReadOnlyList<int> movedVertices,
        IReadOnlyList<int> newVertices, IReadOnlyList<int> newFaces, IReadOnlyList<ExtrudedFace> extruded)
        => result.Succeeded
            ? new EditResult(result.Mesh, result.Errors, movedVertices, newVertices, newFaces, extruded)
            : Refused(result.Errors);
}
