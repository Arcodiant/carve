using System.Numerics;

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

    internal static EditResult Refused(IReadOnlyList<MeshError> errors) => new(null, errors, [], [], [], []);

    /// <summary>The result of an edit that has already carried colours, groups, weights and shading onto <paramref name="mesh"/>.</summary>
    internal static EditResult Made(PolygonMesh mesh, IReadOnlyList<int> movedVertices, IReadOnlyList<int> newVertices,
        IReadOnlyList<int> newFaces, DeletedElements? deleted = null, IReadOnlyList<SplitFace>? split = null)
        => new(mesh, [], movedVertices, newVertices, newFaces, [], deleted, split);

    private static PolygonMesh Carried(PolygonMesh source, PolygonMesh edited, IReadOnlyList<(int Face, IReadOnlyList<int> Added)> made,
        IReadOnlyDictionary<int, IReadOnlyList<Vector2>?>? uvFaces = null)
        => CarriedUvs(source, CarriedRest(source, edited, made), uvFaces);

    /// <summary>
    /// UVs and seams carried onto <paramref name="mesh"/> (T-53): a face of the source that is mapped and still live with the
    /// same corner count keeps its UVs by position (a moved face, an extruded cap, every face the edit left alone); a face
    /// whose corner count changed, a tombstoned face and every appended face end up unmapped, unless the edit worked a
    /// face's UVs out itself (<paramref name="uvFaces"/>, null to unmap). A seam the edit no longer has as an edge is dropped,
    /// as a hard edge is (T-42).
    /// </summary>
    internal static PolygonMesh CarriedUvs(PolygonMesh source, PolygonMesh mesh, IReadOnlyDictionary<int, IReadOnlyList<Vector2>?>? uvFaces = null)
    {
        if (source.Uvs is not { } uvs) return mesh;
        var kept = new List<KeyValuePair<int, IReadOnlyList<Vector2>?>>();
        foreach (var f in uvs.MappedFaces)
        {
            var keep = f < mesh.Faces.Count && !mesh.Faces[f].IsDeleted && mesh.Faces[f].Corners.Count == source.Faces[f].Corners.Count;
            if (!keep) kept.Add(KeyValuePair.Create(f, (IReadOnlyList<Vector2>?)null));
        }
        if (uvFaces is not null) kept.AddRange(uvFaces);
        var carried = uvs.WithFaces(kept);
        var gone = carried.Seams.Where(e => !mesh.Topology.TryGetEdge(e.Lo, e.Hi, out _)).Select(e => (e.Lo, e.Hi)).ToArray();
        return mesh.WithUvs(gone.Length == 0 ? carried : carried.WithSeams(gone, false));
    }

    private static PolygonMesh CarriedRest(PolygonMesh source, PolygonMesh edited, IReadOnlyList<(int Face, IReadOnlyList<int> Added)> made)
    {
        var mesh = source.Style is { } style ? edited.WithStyle(style.Carry(made)) : edited;
        mesh = source.Weights is { } weights ? mesh.WithWeights(weights) : mesh;
        // A tombstoned face leaves its colour and its groups, and a tombstoned vertex its weights (T-64).
        if (mesh.HasTombstones)
        {
            if (mesh.Style is { } kept) mesh = mesh.WithStyle(kept.Without(Enumerable.Range(0, mesh.Faces.Count).Where(mesh.IsFaceDeleted)));
            if (mesh.Weights is { } held) mesh = mesh.WithWeights(held.Without(Enumerable.Range(0, mesh.Vertices.Count).Where(mesh.IsVertexDeleted)));
        }
        // The mode and the hard edges carry; a hard edge the edit no longer has is dropped (T-42).
        if (source.Shade is not { } shade) return mesh;
        var gone = shade.Hard.Where(e => !mesh.Topology.TryGetEdge(e.Lo, e.Hi, out _)).ToArray();
        return mesh.WithShade(gone.Length == 0 ? shade : shade.WithEdges(gone.Select(e => (e.Lo, e.Hi)), false));
    }

    /// <summary>The result of an edit of <paramref name="source"/>: the new mesh keeps the source's face colours and groups, and walls take their source face's.</summary>
    internal static EditResult From(PolygonMesh source, MeshResult result, IReadOnlyList<int> movedVertices,
        IReadOnlyList<int> newVertices, IReadOnlyList<int> newFaces, IReadOnlyList<ExtrudedFace> extruded,
        DeletedElements? deleted = null, IReadOnlyDictionary<int, IReadOnlyList<Vector2>?>? uvFaces = null)
        => result.Succeeded
            ? new EditResult(Carried(source, result.Mesh!, extruded.Select(x => (x.Face, x.Walls)).ToList(), uvFaces),
                result.Errors, movedVertices, newVertices, newFaces, extruded, deleted)
            : Refused(result.Errors);

    /// <summary>
    /// The result of an edit of <paramref name="source"/> that appends faces taking another face's colour and groups
    /// (<c>bridge</c>, T-68): each <c>Added</c> face takes its <c>Face</c>'s, as extrude's walls take theirs.
    /// </summary>
    internal static EditResult From(PolygonMesh source, MeshResult result, IReadOnlyList<int> newFaces,
        IReadOnlyList<(int Face, IReadOnlyList<int> Added)> styledFrom)
        => result.Succeeded
            ? new EditResult(Carried(source, result.Mesh!, styledFrom), result.Errors, [], [], newFaces, [])
            : Refused(result.Errors);
}
