namespace Carve.Core;

/// <summary>
/// What one extruded face became: <see cref="Cap"/> is the face itself, which keeps its id with the new rings as its loop
/// and holes; <see cref="Walls"/> are the quads appended for it, one per edge in corner order (<see cref="Face.Corners"/>:
/// the outer loop, then each hole), wall <c>k</c> running from corner <c>k</c> to the next corner of its loop;
/// <see cref="NewVertices"/> are the rings in the same corner order (new vertex <c>k</c> stands over corner <c>k</c>).
/// </summary>
public sealed record ExtrudedFace(int Face, int Cap, IReadOnlyList<int> Walls, IReadOnlyList<int> NewVertices);

/// <summary>
/// What an edit deleted (T-64): vertex and face ids, each ascending. Their slots are tombstones in the edited mesh
/// (<see cref="PolygonMesh.IsVertexDeleted"/>, <see cref="PolygonMesh.IsFaceDeleted"/>), never given out again.
/// </summary>
public sealed record DeletedElements(IReadOnlyList<int> Vertices, IReadOnlyList<int> Faces)
{
    /// <summary>Nothing deleted: what every edit but a delete reports, unless it removes elements itself.</summary>
    public static DeletedElements None { get; } = new([], []);

    public bool IsEmpty => Vertices.Count == 0 && Faces.Count == 0;
}

/// <summary>
/// The outcome of an edit in <see cref="MeshEdits"/>: the new mesh and what the edit made, or every error that stopped it.
/// On failure <see cref="Mesh"/> is null and the id lists are empty; the mesh the edit was given is never changed.
/// </summary>
public sealed class EditResult
{
    private EditResult(PolygonMesh? mesh, IReadOnlyList<MeshError> errors, IReadOnlyList<int> movedVertices,
        IReadOnlyList<int> newVertices, IReadOnlyList<int> newFaces, IReadOnlyList<ExtrudedFace> extruded,
        DeletedElements? deleted = null, IReadOnlyList<SplitFace>? split = null)
    {
        Mesh = mesh;
        Deleted = deleted ?? DeletedElements.None;
        Split = split ?? [];
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

    /// <summary>
    /// The vertices and faces the edit deleted, ascending (<see cref="DeletedElements.None"/> when none, and on failure):
    /// the field every edit that removes elements reports, as <c>deleted</c> in the server's report.
    /// </summary>
    public DeletedElements Deleted { get; }

    /// <summary>
    /// The faces the edit split (T-65), one entry per face in id order, each with its pieces, the kept id first and the
    /// appended ones after it ascending (<see cref="SplitFace"/>); empty for every edit that splits nothing, and on
    /// failure: the field every edit reports, as <c>split</c> in the server's report.
    /// </summary>
    public IReadOnlyList<SplitFace> Split { get; }

    /// <summary>This result with <paramref name="split"/> as the faces it split (T-65).</summary>
    internal EditResult WithSplit(IReadOnlyList<SplitFace> split) =>
        Succeeded ? new(Mesh, Errors, MovedVertices, NewVertices, NewFaces, Extruded, Deleted, split) : this;

    internal static EditResult Refused(IReadOnlyList<MeshError> errors) => new(null, errors, [], [], [], []);

    /// <summary>The result of an edit that has already carried colours, groups, weights and shading onto <paramref name="mesh"/>.</summary>
    internal static EditResult Made(PolygonMesh mesh, IReadOnlyList<int> movedVertices, IReadOnlyList<int> newVertices,
        IReadOnlyList<int> newFaces, DeletedElements? deleted = null, IReadOnlyList<SplitFace>? split = null)
        => new(mesh, [], movedVertices, newVertices, newFaces, [], deleted, split);

    /// <summary>
    /// The result of an edit of <paramref name="source"/> (T-71): every layer of <see cref="LayerRegistry.Standard"/> carried
    /// onto the edited geometry by its rules, from what <paramref name="provenance"/> says each new or reshaped element came
    /// from (none said: an existing element keeps its values, a new one starts at the defaults).
    /// </summary>
    internal static EditResult From(PolygonMesh source, MeshResult result, IReadOnlyList<int> movedVertices,
        IReadOnlyList<int> newVertices, IReadOnlyList<int> newFaces, IReadOnlyList<ExtrudedFace> extruded,
        DeletedElements? deleted = null, EditProvenance? provenance = null)
        => result.Succeeded
            ? new EditResult(LayerRegistry.Edits.Carry(source, result.Mesh!, provenance ?? new EditProvenance()),
                result.Errors, movedVertices, newVertices, newFaces, extruded, deleted)
            : Refused(result.Errors);
}
