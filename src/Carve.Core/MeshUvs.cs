using System.Numerics;

namespace Carve.Core;

/// <summary>
/// A mesh's texture coordinates and seams (T-53, M-6): UVs per face corner, and the edges marked as seams. Immutable;
/// held on <see cref="PolygonMesh.Uvs"/>, null there when empty, as <see cref="FaceStyle"/> and <see cref="MeshShade"/> are.
/// </summary>
/// <remarks>
/// <para><b>UVs.</b> A mapped face has one <see cref="Vector2"/> per entry of its <see cref="Face.Corners"/>, in that order:
/// its outer loop, then each hole (the person, 2026-11-13). A face may have none (unmapped), so a mesh may be mapped on some
/// faces only; a deleted face's slot is never mapped. Coordinates follow glTF: (0, 0) is the texture's top-left, u runs
/// right and v runs down. Any finite value is legal, and values outside 0..1 tile. Where an edge's two faces give its ends
/// different UVs, the texture is split there; that needs no seam.</para>
/// <para><b>Seams.</b> Undirected edges as vertex-id pairs, lower id first, in order, stored as <see cref="MeshShade.Hard"/>
/// is. A seam is only an input to unwrap (T-56) and a line in the layout look (T-58); it never changes geometry or
/// shading.</para>
/// </remarks>
public sealed class MeshUvs
{
    private readonly SortedDictionary<int, Vector2[]> _faces;
    private readonly (int Lo, int Hi)[] _seams;

    private MeshUvs(SortedDictionary<int, Vector2[]> faces, (int Lo, int Hi)[] seams)
    {
        _faces = faces;
        _seams = seams;
        MappedFaces = [.. faces.Keys];
    }

    /// <summary>No mapped face and no seam.</summary>
    public static MeshUvs None { get; } = new([], []);

    /// <summary>Whether nothing is mapped and nothing is a seam: what a mesh holds as null.</summary>
    public bool IsEmpty => _faces.Count == 0 && _seams.Length == 0;

    /// <summary>The mapped faces' ids, ascending.</summary>
    public IReadOnlyList<int> MappedFaces { get; }

    /// <summary>Face <paramref name="face"/>'s corner UVs in <see cref="Face.Corners"/> order, or null when it is unmapped.</summary>
    public IReadOnlyList<Vector2>? Of(int face) => _faces.TryGetValue(face, out var uv) ? Array.AsReadOnly(uv) : null;

    public bool IsMapped(int face) => _faces.ContainsKey(face);

    /// <summary>The seams, lower id first, in order.</summary>
    public IReadOnlyList<(int Lo, int Hi)> Seams => _seams;

    public bool IsSeam(int a, int b) => Array.BinarySearch(_seams, a < b ? (a, b) : (b, a)) >= 0;

    /// <summary>These UVs with face <paramref name="face"/> mapped to <paramref name="corners"/>, or unmapped when null. Unchecked: <see cref="Check"/> is the guard.</summary>
    public MeshUvs WithFace(int face, IReadOnlyList<Vector2>? corners) => WithFaces([KeyValuePair.Create(face, corners)]);

    /// <summary>These UVs with each face mapped to its corners, or unmapped when null.</summary>
    public MeshUvs WithFaces(IEnumerable<KeyValuePair<int, IReadOnlyList<Vector2>?>> faces)
    {
        var copy = new SortedDictionary<int, Vector2[]>(_faces);
        foreach (var (face, corners) in faces)
        {
            if (face < 0) throw new ArgumentOutOfRangeException(nameof(faces), "a face id is not negative");
            if (corners is null) copy.Remove(face);
            else copy[face] = [.. corners];
        }
        return new MeshUvs(copy, _seams);
    }

    /// <summary>These seams with <paramref name="edges"/> added (<paramref name="seam"/>) or removed; vertex order in a pair does not matter.</summary>
    public MeshUvs WithSeams(IEnumerable<(int A, int B)> edges, bool seam)
    {
        var set = new SortedSet<(int Lo, int Hi)>(_seams);
        foreach (var (a, b) in edges)
        {
            var pair = a < b ? (a, b) : (b, a);
            if (seam) set.Add(pair); else set.Remove(pair);
        }
        return new MeshUvs(_faces, [.. set]);
    }

    /// <summary>These UVs with <paramref name="faces"/> unmapped: a tombstoned face leaves its UVs, as it leaves its colour.</summary>
    internal MeshUvs Without(IEnumerable<int> faces) => WithFaces(faces.Select(f => KeyValuePair.Create(f, (IReadOnlyList<Vector2>?)null)));

    /// <summary>The message for a face given the wrong number of UVs.</summary>
    public static string CornerCountMessage(Face face, int given)
    {
        var loops = face.HasHoles
            ? MeshEdits.Invariant($" (its loop {face.Loop.Count}, holes {string.Join(", ", face.Holes.Select(h => h.Count))})")
            : "";
        return MeshEdits.Invariant($"{given} UVs given, but the face has {face.Corners.Count} corners{loops}; write {face.Corners.Count} UVs, one per corner");
    }

    /// <summary>
    /// Every fault in <paramref name="uvs"/> as UVs of <paramref name="mesh"/>, each named <c>{at}faces[f]</c> or
    /// <c>{at}seams[a-b]</c>: a face out of range or deleted, a corner count that is not the face's, a value that is not
    /// finite, a seam that is not an edge of the mesh. Empty when they fit.
    /// </summary>
    public static IReadOnlyList<MeshError> Check(PolygonMesh mesh, MeshUvs uvs, string at = "")
    {
        var errors = new List<MeshError>();
        foreach (var (f, corners) in uvs._faces)
        {
            var e = MeshEdits.Invariant($"{at}faces[{f}]");
            if (f >= mesh.Faces.Count) { errors.Add(new MeshError(e, MeshEdits.Invariant($"face {f} is not a face of the mesh, which has {mesh.Faces.Count}"))); continue; }
            if (mesh.Faces[f].IsDeleted) { errors.Add(new MeshError(e, MeshEdits.Invariant($"face {f} is deleted, and a deleted face holds no UVs"))); continue; }
            if (corners.Length != mesh.Faces[f].Corners.Count) { errors.Add(new MeshError(e, CornerCountMessage(mesh.Faces[f], corners.Length))); continue; }
            for (var c = 0; c < corners.Length; c++)
                if (!float.IsFinite(corners[c].X) || !float.IsFinite(corners[c].Y))
                    errors.Add(new MeshError(MeshEdits.Invariant($"{e}.uv[{c}]"), "a UV is not finite"));
        }
        foreach (var (lo, hi) in uvs._seams)
            if (!mesh.Topology.TryGetEdge(lo, hi, out _))
                errors.Add(new MeshError(MeshEdits.Invariant($"{at}seams[{lo}-{hi}]"), MeshEdits.Invariant($"{lo}-{hi} is not an edge of the mesh")));
        return errors;
    }
}
