namespace Carve.Core;

/// <summary>
/// <c>delete</c> (T-64): vertices and faces removed as tombstones, so every id an agent holds still means what it did or
/// is plainly gone (D-1: ids are never renumbered or reused). A deleted slot stays in <see cref="PolygonMesh.Vertices"/>
/// or <see cref="PolygonMesh.Faces"/>, marked deleted (<see cref="PolygonMesh.IsVertexDeleted"/>,
/// <see cref="PolygonMesh.IsFaceDeleted"/>), and new elements are still appended after the last slot.
/// </summary>
/// <remarks>
/// The rule of what a delete takes with it is <see cref="Closure"/>'s, and every later edit that removes elements (a
/// merge, a cut keeping one side) takes it from there and passes the result to <see cref="PolygonMesh.Edit"/>'s
/// <c>deletedVertices</c> and <c>deletedFaces</c>, reporting it as <see cref="EditResult.Deleted"/>.
/// </remarks>
public static class MeshDelete
{
    /// <summary>The error when neither list names anything.</summary>
    public const string NothingMessage = "nothing to delete: list at least one vertex or face";

    /// <summary>The error when the delete would take every live face.</summary>
    public const string EveryFaceMessage =
        "this would delete every face, and a mesh keeps at least one face (as define_mesh requires)";

    /// <summary>
    /// What deleting <paramref name="vertices"/> and <paramref name="faces"/> takes, ids ascending: the listed elements;
    /// every live face using a listed vertex, in its outer loop or in a hole (a face is deleted whole, never reshaped);
    /// and every vertex of a deleted face that no live face uses afterwards. A vertex no face used before the delete is
    /// not taken unless listed. Ids that name no live element are left out (<see cref="Delete"/> refuses them first).
    /// </summary>
    public static DeletedElements Closure(PolygonMesh mesh, IEnumerable<int> vertices, IEnumerable<int> faces)
    {
        var gone = new SortedSet<int>(vertices.Where(v => mesh.VertexProblem(v) is null));
        var dead = new SortedSet<int>(faces.Where(f => mesh.FaceProblem(f) is null));
        if (gone.Count > 0)
        {
            foreach (var f in mesh.LiveFaces)
                if (mesh.Faces[f].Corners.Any(gone.Contains)) dead.Add(f);
        }

        var used = new HashSet<int>();
        foreach (var f in mesh.LiveFaces)
            if (!dead.Contains(f)) used.UnionWith(mesh.Faces[f].Corners);
        foreach (var f in dead)
            foreach (var v in mesh.Faces[f].Corners)
                if (!used.Contains(v)) gone.Add(v);

        return new DeletedElements(gone.ToArray(), dead.ToArray());
    }

    /// <summary>
    /// Deletes <paramref name="vertices"/> and <paramref name="faces"/> and what they take (<see cref="Closure"/>). Refused
    /// when nothing is listed, when a listed id never existed or is already deleted (<c>faces[7]: deleted</c>), and when
    /// it would leave no live face. A deleted face leaves its colour and groups, a deleted vertex its weights, and a hard
    /// edge no live face has any more is dropped. <see cref="EditResult.Deleted"/> lists everything removed.
    /// </summary>
    public static EditResult Delete(PolygonMesh mesh, IEnumerable<int>? vertices, IEnumerable<int>? faces)
    {
        var vertexList = vertices?.ToArray() ?? [];
        var faceList = faces?.ToArray() ?? [];
        if (vertexList.Length == 0 && faceList.Length == 0)
            return EditResult.Refused([new MeshError("vertices, faces", NothingMessage)]);

        var errors = MeshEdits.UnknownElements(mesh, vertexList, [], faceList);
        if (errors.Count > 0) return EditResult.Refused(errors);

        var deleted = Closure(mesh, vertexList, faceList);
        if (deleted.Faces.Count == mesh.LiveFaceCount)
            return EditResult.Refused([new MeshError("vertices, faces", EveryFaceMessage)]);

        var result = mesh.Edit(deletedVertices: deleted.Vertices, deletedFaces: deleted.Faces);
        return EditResult.From(mesh, result, [], [], [], [], deleted);
    }
}
