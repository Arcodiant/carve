using System.Numerics;

namespace Carve.Core;

/// <summary>One reason a mesh was refused. <see cref="Element"/> names the offender, e.g. <c>vertices[3]</c> or <c>faces[2][1]</c>.</summary>
public sealed record MeshError(string Element, string Message)
{
    public override string ToString() => $"{Element}: {Message}";
}

/// <summary>The outcome of <see cref="PolygonMesh.Create"/>: a mesh, or the errors that stopped one.</summary>
public sealed class MeshResult
{
    internal MeshResult(PolygonMesh? mesh, IReadOnlyList<MeshError> errors)
    {
        Mesh = mesh;
        Errors = errors;
    }

    public PolygonMesh? Mesh { get; }
    public IReadOnlyList<MeshError> Errors { get; }
    public bool Succeeded => Mesh is not null;
}

/// <summary>
/// An immutable polygon mesh in plain indexed storage: vertices, and faces (<see cref="Face"/>), each an outer loop of 3
/// or more vertex ids and zero or more hole loops of 3 or more. Winding is counter-clockwise seen from outside, holes clockwise. Triangles are a derived view (<see cref="Triangulator"/>), never stored,
/// and so is the edge table (<see cref="Topology"/>).
/// </summary>
/// <remarks>
/// <para><b>Ids.</b> A vertex's id is its position in <see cref="Vertices"/> and a face's id its position in
/// <see cref="Faces"/>: the same numbers <c>define_mesh</c> takes and every error and warning names
/// (<c>vertices[n]</c>, <c>faces[n]</c>). An edge has no id of its own and is named by its two vertex ids.</para>
/// <para>Across every edit of a mesh, made through <see cref="Edit"/>:</para>
/// <list type="bullet">
/// <item>an edit never renumbers: every vertex and face keeps its id;</item>
/// <item>an id is never reused along the current chain of edits; an edit that is undone frees its ids, and the next
/// edit gives them out again;</item>
/// <item>new vertices and faces are added at the end, taking the next ids in order;</item>
/// <item>a face that is reshaped (its loop replaced) keeps its id.</item>
/// </list>
/// </remarks>
public sealed class PolygonMesh
{
    /// <summary>A face is non-planar when a vertex lies further from its plane than this fraction of the face's bounding-box diagonal.</summary>
    public const float PlanarityTolerance = 1e-4f;

    /// <summary>A face is zero-area when its area is below this fraction of its bounding-box diagonal squared.</summary>
    public const float ZeroAreaTolerance = 1e-9f;

    /// <summary>The error on a face whose outline winds round some point twice, so its triangles would cover it twice.</summary>
    public const string CoversTwiceMessage =
        "face covers part of its area twice: its outline overlaps itself";

    private readonly Vector3[] _vertices;
    private readonly Face[] _faces;
    private readonly bool[]? _deletedVertices;

    private PolygonMesh(Vector3[] vertices, Face[] faces, FaceStyle? style = null, VertexWeights? weights = null,
        MeshShade? shade = null, Vector3[][]? corners = null, bool[]? flatFaces = null, bool[]? deletedVertices = null,
        MeshUvs? uvs = null, MorphTargets? targets = null)
    {
        _deletedVertices = deletedVertices is not null && Array.IndexOf(deletedVertices, true) >= 0 ? deletedVertices : null;
        LiveVertexCount = vertices.Length - (_deletedVertices?.Count(d => d) ?? 0);
        LiveFaceCount = faces.Count(f => !f.IsDeleted);
        FlatFaces = flatFaces;
        _vertices = vertices;
        _faces = faces;
        Shade = shade is { IsDefault: false } ? shade : null;
        PosedCorners = corners;
        Style = style is { IsEmpty: false } ? style : null;
        Weights = weights is { IsEmpty: false } ? weights : null;
        Uvs = uvs is { IsEmpty: false } ? uvs : null;
        Targets = targets is { Count: > 0 } ? targets : null;
        Vertices = Array.AsReadOnly(vertices);
        Faces = Array.AsReadOnly(faces);
    }

    private MeshTopology? _topology;

    /// <summary>The faces' colours and named groups (D-6, Colour is a material); null when the mesh has neither.</summary>
    public FaceStyle? Style { get; }

    /// <summary>This mesh, its geometry shared, with <paramref name="style"/> in place of its own (null or empty for none).</summary>
    public PolygonMesh WithStyle(FaceStyle? style) => new(_vertices, _faces, style, Weights, Shade, PosedCorners, FlatFaces, _deletedVertices, Uvs, Targets);

    /// <summary>The mesh's shading and hard edges (D-6, Smooth shading is per mesh); null for flat with no hard edge marked.</summary>
    public MeshShade? Shade { get; }

    /// <summary>Flat (the default) or smooth.</summary>
    public ShadingMode ShadingMode => Shade?.Mode ?? ShadingMode.Flat;

    /// <summary>The hard edges as vertex-id pairs, lower id first, in order; kept while the mesh is flat, and then they do nothing.</summary>
    public IReadOnlyList<(int Lo, int Hi)> HardEdges => Shade?.Hard ?? [];

    /// <summary>This mesh, its geometry shared, with <paramref name="shade"/> in place of its own (null for flat with no hard edges).</summary>
    public PolygonMesh WithShade(MeshShade? shade) => new(_vertices, _faces, Style, Weights, shade, PosedCorners, null, _deletedVertices, Uvs, Targets);

    /// <summary>
    /// Corner normals given for a posed copy (<see cref="Skinning"/>), by face and then by position in the face's loop;
    /// null otherwise. <see cref="Shading"/> uses them in place of its own working.
    /// </summary>
    internal Vector3[][]? PosedCorners { get; }

    /// <summary>
    /// For a combined mesh (<see cref="Model.Combined"/>) with corner normals, which faces came from a flat mesh and are
    /// drawn flat (<see cref="Shading.FaceHasCornerNormals"/>); null otherwise.
    /// </summary>
    internal bool[]? FlatFaces { get; }

    /// <summary>Faces and vertices joined as given, unchecked: for drawing meshes already checked, or posed.</summary>
    internal static PolygonMesh Assemble(Vector3[] vertices, Face[] faces, MeshShade? shade = null, Vector3[][]? corners = null,
        bool[]? flatFaces = null, bool[]? deletedVertices = null)
        => new(vertices, faces, null, null, shade, corners, flatFaces, deletedVertices);

    /// <summary>
    /// This mesh's faces and colours on other vertex positions, unchecked and with no weights: a posed copy to draw, whose
    /// faces need not stay planar; it keeps the UVs. Never stored.
    /// </summary>
    public PolygonMesh WithVertices(IReadOnlyList<Vector3> vertices) => WithVertices(vertices, null);

    /// <summary>As above, drawn with <paramref name="corners"/> (face, then corner, <see cref="Face.Corners"/>) as its corner normals when given.</summary>
    internal PolygonMesh WithVertices(IReadOnlyList<Vector3> vertices, Vector3[][]? corners)
    {
        if (vertices.Count != _vertices.Length) throw new ArgumentException("one position per vertex", nameof(vertices));
        return new PolygonMesh(vertices.ToArray(), _faces, Style, null, Shade, corners, null, _deletedVertices, Uvs, Targets);
    }

    /// <summary>The vertices' bone weights by vertex id (D-6, Weights); null when no vertex has any.</summary>
    public VertexWeights? Weights { get; }

    /// <summary>This mesh, its geometry shared, with <paramref name="weights"/> in place of its own (null or empty for none).</summary>
    public PolygonMesh WithWeights(VertexWeights? weights) => new(_vertices, _faces, Style, weights, Shade, PosedCorners, null, _deletedVertices, Uvs, Targets);

    /// <summary>The mesh's morph targets (D-6, Morph targets) in definition order; null when it has none.</summary>
    public MorphTargets? Targets { get; }

    /// <summary>This mesh, its geometry and every other attribute shared, with <paramref name="targets"/> in place of its own (null for none). Unchecked: a target is checked when it is made (<see cref="MorphTarget"/>).</summary>
    public PolygonMesh WithTargets(MorphTargets? targets) => new(_vertices, _faces, Style, Weights, Shade, PosedCorners, FlatFaces, _deletedVertices, Uvs, targets);

    /// <summary>
    /// The mesh at <paramref name="weights"/> (target name to weight): its positions <c>base + Σ wᵢ·deltaᵢ</c>
    /// (<see cref="MorphTargets.Apply"/>), as <see cref="WithVertices(IReadOnlyList{Vector3})"/> gives them: unchecked, faces,
    /// style, shading and UVs kept, no weights, and no targets (it is a copy to draw, already morphed). A name the mesh does
    /// not have is ignored; the tools refuse one. A mesh with no targets gives its own positions.
    /// </summary>
    public PolygonMesh Morphed(IReadOnlyDictionary<string, float> weights) =>
        new(Targets?.Apply(_vertices, weights) ?? _vertices, _faces, Style, null, Shade, null, null, _deletedVertices, Uvs);

    /// <summary>The faces' corner UVs and the seams (T-53, <see cref="MeshUvs"/>); null when no face is mapped and no edge is a seam.</summary>
    public MeshUvs? Uvs { get; }

    /// <summary>The seams as vertex-id pairs, lower id first, in order.</summary>
    public IReadOnlyList<(int Lo, int Hi)> Seams => Uvs?.Seams ?? [];

    /// <summary>This mesh, its geometry and every other attribute shared, with <paramref name="uvs"/> in place of its own (null or empty for none). Unchecked: <see cref="MeshUvs.Check"/> is the guard.</summary>
    public PolygonMesh WithUvs(MeshUvs? uvs) => new(_vertices, _faces, Style, Weights, Shade, PosedCorners, FlatFaces, _deletedVertices, uvs, Targets);

    /// <summary>
    /// The vertex slots; a vertex's id is its position here, so <c>Count</c> is a slot count, tombstones included. A
    /// deleted vertex's slot (<see cref="IsVertexDeleted"/>) holds <see cref="Vector3.Zero"/> and must be skipped.
    /// </summary>
    public IReadOnlyList<Vector3> Vertices { get; }

    /// <summary>
    /// The face slots, each an outer loop and its holes (<see cref="Face"/>); a face's id is its position here, so
    /// <c>Count</c> is a slot count, tombstones included. A deleted face's slot holds <see cref="Face.Deleted"/>, which has
    /// no corners, and must be skipped (<see cref="IsFaceDeleted"/>, <see cref="LiveFaces"/>).
    /// </summary>
    public IReadOnlyList<Face> Faces { get; }

    /// <summary>The vertices not deleted (D-1, T-64: a deleted slot is kept, and its id is never given out again).</summary>
    public int LiveVertexCount { get; }

    /// <summary>The faces not deleted.</summary>
    public int LiveFaceCount { get; }

    /// <summary>Whether any slot, vertex or face, is a tombstone.</summary>
    public bool HasTombstones => _deletedVertices is not null || LiveFaceCount != _faces.Length;

    /// <summary>Whether vertex <paramref name="id"/> is a tombstone. False for an id out of range (see <see cref="VertexProblem"/>).</summary>
    public bool IsVertexDeleted(int id) => _deletedVertices is { } d && id >= 0 && id < d.Length && d[id];

    /// <summary>Whether face <paramref name="id"/> is a tombstone. False for an id out of range (see <see cref="FaceProblem"/>).</summary>
    public bool IsFaceDeleted(int id) => id >= 0 && id < _faces.Length && _faces[id].IsDeleted;

    /// <summary>The live vertex ids, ascending.</summary>
    public IEnumerable<int> LiveVertices => Enumerable.Range(0, _vertices.Length).Where(v => !IsVertexDeleted(v));

    /// <summary>The live face ids, ascending.</summary>
    public IEnumerable<int> LiveFaces => Enumerable.Range(0, _faces.Length).Where(f => !_faces[f].IsDeleted);

    /// <summary>The message an edit gives for a tombstoned id it is handed: <c>faces[7]: deleted</c>.</summary>
    public const string DeletedMessage = "deleted";

    /// <summary>
    /// Why <paramref name="id"/> names no live vertex, or null when it does: <c>there is no vertex n: ids run 0..m</c> for
    /// an id that never existed, <see cref="DeletedMessage"/> for a tombstone. Element <c>vertices[n]</c>.
    /// </summary>
    public string? VertexProblem(int id) =>
        id < 0 || id >= _vertices.Length ? MeshEdits.Invariant($"there is no vertex {id}: ids run 0..{_vertices.Length - 1}")
        : IsVertexDeleted(id) ? DeletedMessage : null;

    /// <summary>As <see cref="VertexProblem"/>, for face <paramref name="id"/>. Element <c>faces[n]</c>.</summary>
    public string? FaceProblem(int id) =>
        id < 0 || id >= _faces.Length ? MeshEdits.Invariant($"there is no face {id}: ids run 0..{_faces.Length - 1}")
        : _faces[id].IsDeleted ? DeletedMessage : null;

    /// <summary>The deleted vertex ids as a mask by slot, or null when none is deleted.</summary>
    internal bool[]? DeletedVertexMask => _deletedVertices;

    /// <summary>The mesh's edge table, built on first use and held by the mesh.</summary>
    public MeshTopology Topology => LazyInitializer.EnsureInitialized(ref _topology, () => new MeshTopology(this));

    /// <summary>Validates and builds a mesh of plain faces (the inputs are copied), or returns every error found.</summary>
    public static MeshResult Create(IEnumerable<Vector3> vertices, IEnumerable<IEnumerable<int>> faces)
        => Create(vertices, faces.Select(f => new Face(f)));

    /// <summary>
    /// Validates and builds a mesh (the inputs are copied), or returns every error found. A face's outer loop is checked
    /// as a plain face's always was. A hole is refused, by <c>faces[n].holes[k]</c> or one corner's
    /// <c>faces[n].holes[k][i]</c>, when it has fewer than 3 vertices, an index out of range, a vertex used twice across
    /// the face's loops, the outer loop's winding, a place not strictly inside the outer loop, or a meeting with another
    /// hole; then the whole face, its holes bridged in (<see cref="Triangulator.Bridge"/>), takes the plain face's checks.
    /// </summary>
    public static MeshResult Create(IEnumerable<Vector3> vertices, IEnumerable<Face> faces) => Create(vertices, faces, null);

    /// <summary>
    /// As <see cref="Create(IEnumerable{Vector3}, IEnumerable{Face})"/>, with tombstones (T-64): the vertex ids in
    /// <paramref name="deletedVertices"/> are deleted slots (their positions are not checked and are kept as
    /// <see cref="Vector3.Zero"/>), and a <see cref="Face.Deleted"/> in <paramref name="faces"/> is a deleted face's slot,
    /// which is not checked. A live face using a deleted vertex is refused at its corner (<c>faces[n][i]</c>), and so is a
    /// mesh with no live face.
    /// </summary>
    public static MeshResult Create(IEnumerable<Vector3> vertices, IEnumerable<Face> faces, IEnumerable<int>? deletedVertices)
    {
        var verts = vertices.ToArray();
        var fcs = faces.ToArray();
        var errors = new List<MeshError>();

        var deleted = new bool[verts.Length];
        foreach (var d in deletedVertices ?? [])
        {
            if (d < 0 || d >= verts.Length) throw new ArgumentOutOfRangeException(nameof(deletedVertices), d, "no such vertex slot");
            deleted[d] = true;
            verts[d] = Vector3.Zero;
        }

        var badVertex = new bool[verts.Length];
        for (var i = 0; i < verts.Length; i++)
        {
            if (deleted[i]) continue;
            var v = verts[i];
            if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
            {
                badVertex[i] = true;
                errors.Add(new MeshError($"vertices[{i}]", "coordinate is not finite"));
            }
        }

        if (fcs.Length == 0)
            errors.Add(new MeshError("faces", "mesh has no faces"));
        else if (fcs.All(f => f.IsDeleted))
            errors.Add(new MeshError("faces", "mesh has no faces: every face is deleted"));

        for (var f = 0; f < fcs.Length; f++)
        {
            var face = fcs[f];
            if (face.IsDeleted) continue;
            var faceOk = true;
            var seen = new Dictionary<int, string>();
            for (var h = -1; h < face.Holes.Count; h++)
            {
                var loop = face.LoopAt(h);
                var what = h < 0 ? "face" : "hole";
                if (loop.Count < 3)
                {
                    errors.Add(new MeshError(Face.LoopName(f, h), $"{what} has {loop.Count} indices; at least 3 are needed"));
                    faceOk = false;
                }

                for (var k = 0; k < loop.Count; k++)
                {
                    if (loop[k] < 0 || loop[k] >= verts.Length)
                    {
                        errors.Add(new MeshError(Face.CornerName(f, h, k), verts.Length == 0 ? MeshEdits.Invariant($"index {loop[k]} is out of range: there are no vertices") : MeshEdits.Invariant($"index {loop[k]} is out of range 0..{verts.Length - 1}")));
                        faceOk = false;
                    }
                    else if (deleted[loop[k]])
                    {
                        errors.Add(new MeshError(Face.CornerName(f, h, k), MeshEdits.Invariant($"index {loop[k]} is a deleted vertex")));
                        faceOk = false;
                    }
                }

                for (var k = 0; k < loop.Count; k++)
                {
                    if (seen.TryGetValue(loop[k], out var earlier))
                    {
                        errors.Add(new MeshError(Face.CornerName(f, h, k), MeshEdits.Invariant($"index {loop[k]} repeats {earlier}")));
                        faceOk = false;
                    }
                    else
                        seen[loop[k]] = Face.CornerName(f, h, k);
                }
            }
            if (!faceOk || face.Corners.Any(i => badVertex[i])) continue;

            // Crossing and folding first, so an outline that crosses always names its edges.
            if (Triangulator.FindCrossing(verts, face.Loop) is var (first, second, fold))
            {
                errors.Add(new MeshError($"faces[{f}]", fold
                    ? $"face folds back on itself: the edge from faces[{f}][{first}] overlaps the edge from faces[{f}][{second}]"
                    : $"face is self-intersecting: the edge from faces[{f}][{first}] crosses the edge from faces[{f}][{second}]"));
                continue;
            }

            if (IsZeroArea(verts, face.Loop))
            {
                errors.Add(new MeshError($"faces[{f}]", "face has zero area"));
                continue;
            }

            IReadOnlyList<int> outline = face.Loop;
            if (face.HasHoles)
            {
                var holeErrors = Triangulator.HoleProblems(verts, face, f);
                if (holeErrors.Count > 0)
                {
                    errors.AddRange(holeErrors);
                    continue;
                }
                if (Triangulator.Bridge(verts, face) is not { } bridged)
                {
                    errors.Add(new MeshError($"faces[{f}]", "face cannot be triangulated: a hole could not be bridged to its outer loop"));
                    continue;
                }

                // The plain face's checks on the bridged outline, naming each edge by the corner it starts at.
                outline = bridged.Ids;
                string Name(int p)
                {
                    var (hole, i) = face.Locate(bridged.Corners[p]);
                    return Face.CornerName(f, hole, i);
                }
                if (Triangulator.FindCrossing(verts, outline) is var (a, b, folds))
                {
                    errors.Add(new MeshError($"faces[{f}]", folds
                        ? $"face folds back on itself: the edge from {Name(a)} overlaps the edge from {Name(b)}"
                        : $"face is self-intersecting: the edge from {Name(a)} crosses the edge from {Name(b)}"));
                    continue;
                }
                if (IsZeroArea(verts, outline))
                {
                    errors.Add(new MeshError($"faces[{f}]", "face has zero area once its holes are taken out"));
                    continue;
                }
            }

            if (!Triangulator.TryTriangulate(verts, outline, out var triangles))
                errors.Add(new MeshError($"faces[{f}]", "face cannot be triangulated (degenerate outline)"));
            else if (Triangulator.CoversTwice(verts, outline, triangles))
                errors.Add(new MeshError($"faces[{f}]", CoversTwiceMessage));
        }

        return errors.Count > 0
            ? new MeshResult(null, errors)
            : new MeshResult(new PolygonMesh(verts, fcs, deletedVertices: deleted), errors);
    }

    /// <summary>
    /// Builds the mesh an edit makes from this one, by the id contract on <see cref="PolygonMesh"/>, and runs
    /// <see cref="Create(IEnumerable{Vector3}, IEnumerable{Face})"/>'s validation on the whole result, so a bad result is
    /// refused by element exactly as <c>define_mesh</c> is. This mesh is not changed.
    /// </summary>
    /// <param name="movedVertices">New positions, by vertex id.</param>
    /// <param name="reshapedFaces">New faces (outer loop and holes), by face id; the face keeps its id.</param>
    /// <param name="addedVertices">Vertices appended after the last, taking ids <c>Vertices.Count</c> onward.</param>
    /// <param name="addedFaces">Faces appended after the last, taking ids <c>Faces.Count</c> onward. Their indices, like
    /// those in <paramref name="reshapedFaces"/>, are ids in the result, so they may use added vertices.</param>
    /// <param name="deletedVertices">Vertex ids made tombstones (T-64), exactly these: the rule of what a delete takes
    /// with it is <see cref="MeshDelete.Closure"/>'s, not this method's. A live face left using one is refused.</param>
    /// <param name="deletedFaces">Face ids made tombstones (<see cref="Face.Deleted"/>), exactly these.</param>
    /// <remarks>An id in <paramref name="movedVertices"/>, <paramref name="reshapedFaces"/> or the deleted lists that
    /// this mesh does not have is refused as <c>vertices[n]</c> or <c>faces[n]</c>, beside whatever else is wrong; one
    /// already deleted is refused as <c>vertices[n]: deleted</c> or <c>faces[n]: deleted</c>. Tombstones carry: their
    /// slots stay, and added elements are appended after them, so no id is ever given out twice.</remarks>
    public MeshResult Edit(
        IEnumerable<KeyValuePair<int, Vector3>>? movedVertices = null,
        IEnumerable<KeyValuePair<int, Face>>? reshapedFaces = null,
        IEnumerable<Vector3>? addedVertices = null,
        IEnumerable<Face>? addedFaces = null,
        IEnumerable<int>? deletedVertices = null,
        IEnumerable<int>? deletedFaces = null)
    {
        var errors = new List<MeshError>();
        var verts = (Vector3[])_vertices.Clone();
        var fcs = (Face[])_faces.Clone();
        var gone = new SortedSet<int>(_deletedVertices is null ? [] : Enumerable.Range(0, verts.Length).Where(IsVertexDeleted));

        foreach (var (id, position) in movedVertices ?? [])
        {
            if (id < 0 || id >= verts.Length)
                errors.Add(new MeshError($"vertices[{id}]", $"there is no vertex {id} to move: ids run 0..{verts.Length - 1}"));
            else if (IsVertexDeleted(id))
                errors.Add(new MeshError($"vertices[{id}]", DeletedMessage));
            else
                verts[id] = position;
        }

        foreach (var (id, face) in reshapedFaces ?? [])
        {
            if (id < 0 || id >= fcs.Length)
                errors.Add(new MeshError($"faces[{id}]", $"there is no face {id} to reshape: ids run 0..{fcs.Length - 1}"));
            else if (fcs[id].IsDeleted)
                errors.Add(new MeshError($"faces[{id}]", DeletedMessage));
            else
                fcs[id] = face;
        }

        foreach (var id in deletedVertices ?? [])
        {
            if (VertexProblem(id) is { } problem)
                errors.Add(new MeshError(MeshEdits.Invariant($"vertices[{id}]"), problem));
            else
                gone.Add(id);
        }

        foreach (var id in deletedFaces ?? [])
        {
            if (FaceProblem(id) is { } problem)
                errors.Add(new MeshError(MeshEdits.Invariant($"faces[{id}]"), problem));
            else
                fcs[id] = Face.Deleted;
        }

        var result = Create(verts.Concat(addedVertices ?? []), fcs.Concat(addedFaces ?? []), gone);
        return errors.Count == 0 ? result : new MeshResult(null, [.. errors, .. result.Errors]);
    }

    /// <summary>A live face; a tombstone is refused plainly (callers skip tombstones, through <see cref="LiveFaces"/>).</summary>
    private Face LiveFace(int face) =>
        _faces[face] is { IsDeleted: false } f ? f : throw new InvalidOperationException($"faces[{face}]: {DeletedMessage}");

    /// <summary>A face's area: its outer loop's less its holes'.</summary>
    public double FaceArea(int face)
    {
        var f = LiveFace(face);
        var area = Length(NewellVector(_vertices, f.Loop)) / 2;
        foreach (var hole in f.Holes) area -= Length(NewellVector(_vertices, hole)) / 2;
        return area;
    }
    /// <summary>Centre of a face: the mean of its outer loop's vertices.</summary>
    public Vector3 FaceCentre(int face)
    {
        var loop = LiveFace(face).Loop;
        var o = _vertices[loop[0]];
        double x = 0, y = 0, z = 0;
        foreach (var i in loop)
        {
            var v = _vertices[i];
            x += (double)v.X - o.X; y += (double)v.Y - o.Y; z += (double)v.Z - o.Z;
        }
        return new Vector3((float)(o.X + x / loop.Count), (float)(o.Y + y / loop.Count), (float)(o.Z + z / loop.Count));
    }

    /// <summary>
    /// Where a face's label goes: on the face itself. For a plain face this is <see cref="FaceCentre"/>; for a face with
    /// holes, whose centre may lie in a hole, it is the centroid of the largest triangle of its triangulation, which lies
    /// on the outer loop's area less its holes.
    /// </summary>
    public Vector3 FaceLabelPoint(int face)
    {
        if (!LiveFace(face).HasHoles) return FaceCentre(face);
        var best = -1.0;
        var at = FaceCentre(face);
        foreach (var t in Triangulator.TriangulateFace(this, face))
        {
            Vector3 a = _vertices[t.A], b = _vertices[t.B], c = _vertices[t.C];
            var area = Vector3.Cross(b - a, c - a).Length();
            if (area <= best) continue;
            best = area;
            at = (a + b + c) / 3;
        }
        return at;
    }

    /// <summary>Unit normal of a face's outer loop by Newell's method; outward for a face wound counter-clockwise from outside.</summary>
    public Vector3 FaceNormal(int face)
    {
        var (x, y, z) = Unit(NewellVector(_vertices, LiveFace(face).Loop));
        return new Vector3((float)x, (float)y, (float)z);
    }

    /// <summary>
    /// Newell's method on any vertex loop, in double and relative to the loop's first vertex so rounding does not
    /// grow with the distance from the origin and squares do not overflow; the result is unnormalised (its length is twice the area).
    /// </summary>
    internal static (double X, double Y, double Z) NewellVector(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face)
    {
        var o = verts[face[0]];
        double nx = 0, ny = 0, nz = 0;
        for (var i = 0; i < face.Count; i++)
        {
            var a = verts[face[i]];
            var b = verts[face[(i + 1) % face.Count]];
            double ax = (double)a.X - o.X, ay = (double)a.Y - o.Y, az = (double)a.Z - o.Z;
            double bx = (double)b.X - o.X, by = (double)b.Y - o.Y, bz = (double)b.Z - o.Z;
            nx += (ay - by) * (az + bz);
            ny += (az - bz) * (ax + bx);
            nz += (ax - bx) * (ay + by);
        }
        return (nx, ny, nz);
    }

    internal static double Length((double X, double Y, double Z) v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    internal static (double X, double Y, double Z) Unit((double X, double Y, double Z) v)
    {
        var len = Length(v);
        return len > 0 ? (v.X / len, v.Y / len, v.Z / len) : (0, 0, 0);
    }

    /// <summary>Bounding-box diagonal of a face's vertices, the "size" tolerances are relative to.</summary>
    internal static double FaceSize(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var i in face)
        {
            var v = verts[i];
            minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X);
            minY = Math.Min(minY, v.Y); maxY = Math.Max(maxY, v.Y);
            minZ = Math.Min(minZ, v.Z); maxZ = Math.Max(maxZ, v.Z);
        }
        return Length((maxX - minX, maxY - minY, maxZ - minZ));
    }

    /// <summary>Whether a vector twice an area long, as Newell's is, holds more than a zero area for this size.</summary>
    internal static bool HasArea((double X, double Y, double Z) doubled, double size)
        => Length(doubled) * 0.5 > ZeroAreaTolerance * size * size;

    private static bool IsZeroArea(IReadOnlyList<Vector3> verts, IReadOnlyList<int> face)
        => !HasArea(NewellVector(verts, face), FaceSize(verts, face));
}
