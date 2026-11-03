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
/// An immutable polygon mesh in plain indexed storage: vertices, and faces as index lists of 3 or more.
/// Winding is counter-clockwise seen from outside. Triangles are a derived view (<see cref="Triangulator"/>), never stored,
/// and so is the edge table (<see cref="Topology"/>).
/// </summary>
/// <remarks>
/// <para><b>Ids.</b> A vertex's id is its position in <see cref="Vertices"/> and a face's id its position in
/// <see cref="Faces"/>: the same numbers <c>define_mesh</c> takes and every error and warning names
/// (<c>vertices[n]</c>, <c>faces[n]</c>). An edge has no id of its own and is named by its two vertex ids.</para>
/// <para>Across every edit of a mesh, made through <see cref="Edit"/>:</para>
/// <list type="bullet">
/// <item>an edit never renumbers: every vertex and face keeps its id;</item>
/// <item>an id is never reused within a mesh's history;</item>
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
    private readonly int[][] _faces;

    private PolygonMesh(Vector3[] vertices, int[][] faces)
    {
        _vertices = vertices;
        _faces = faces;
        Vertices = Array.AsReadOnly(vertices);
        Faces = Array.AsReadOnly(faces.Select(f => (IReadOnlyList<int>)Array.AsReadOnly(f)).ToArray());
    }

    private MeshTopology? _topology;

    /// <summary>The vertices; a vertex's id is its position here.</summary>
    public IReadOnlyList<Vector3> Vertices { get; }

    /// <summary>The faces as vertex-id loops; a face's id is its position here.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Faces { get; }

    /// <summary>The mesh's edge table, built on first use and held by the mesh.</summary>
    public MeshTopology Topology => LazyInitializer.EnsureInitialized(ref _topology, () => new MeshTopology(this));

    /// <summary>Validates and builds a mesh (the inputs are copied), or returns every error found.</summary>
    public static MeshResult Create(IEnumerable<Vector3> vertices, IEnumerable<IEnumerable<int>> faces)
    {
        var verts = vertices.ToArray();
        var fcs = faces.Select(f => f.ToArray()).ToArray();
        var errors = new List<MeshError>();

        var badVertex = new bool[verts.Length];
        for (var i = 0; i < verts.Length; i++)
        {
            var v = verts[i];
            if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
            {
                badVertex[i] = true;
                errors.Add(new MeshError($"vertices[{i}]", "coordinate is not finite"));
            }
        }

        if (fcs.Length == 0)
            errors.Add(new MeshError("faces", "mesh has no faces"));

        for (var f = 0; f < fcs.Length; f++)
        {
            var face = fcs[f];
            var faceOk = true;
            if (face.Length < 3)
            {
                errors.Add(new MeshError($"faces[{f}]", $"face has {face.Length} indices; at least 3 are needed"));
                faceOk = false;
            }

            for (var k = 0; k < face.Length; k++)
            {
                if (face[k] < 0 || face[k] >= verts.Length)
                {
                    errors.Add(new MeshError($"faces[{f}][{k}]", verts.Length == 0 ? $"index {face[k]} is out of range: there are no vertices" : $"index {face[k]} is out of range 0..{verts.Length - 1}"));
                    faceOk = false;
                }
            }

            for (var k = 0; k < face.Length; k++)
            {
                for (var j = 0; j < k; j++)
                {
                    if (face[j] == face[k])
                    {
                        errors.Add(new MeshError($"faces[{f}][{k}]", $"index {face[k]} repeats faces[{f}][{j}]"));
                        faceOk = false;
                        break;
                    }
                }
            }
            if (!faceOk || face.Any(i => badVertex[i])) continue;

            // Crossing and folding first, so an outline that crosses always names its edges.
            if (Triangulator.FindCrossing(verts, face) is var (first, second, fold))
            {
                errors.Add(new MeshError($"faces[{f}]", fold
                    ? $"face folds back on itself: the edge from faces[{f}][{first}] overlaps the edge from faces[{f}][{second}]"
                    : $"face is self-intersecting: the edge from faces[{f}][{first}] crosses the edge from faces[{f}][{second}]"));
                continue;
            }

            if (IsZeroArea(verts, face))
            {
                errors.Add(new MeshError($"faces[{f}]", "face has zero area"));
                continue;
            }

            if (!Triangulator.TryTriangulate(verts, face, out var triangles))
                errors.Add(new MeshError($"faces[{f}]", "face cannot be triangulated (degenerate outline)"));
            else if (Triangulator.CoversTwice(verts, face, triangles))
                errors.Add(new MeshError($"faces[{f}]", CoversTwiceMessage));
        }

        return errors.Count > 0
            ? new MeshResult(null, errors)
            : new MeshResult(new PolygonMesh(verts, fcs), errors);
    }

    /// <summary>
    /// Builds the mesh an edit makes from this one, by the id contract on <see cref="PolygonMesh"/>, and runs
    /// <see cref="Create"/>'s validation on the whole result, so a bad result is refused by element exactly as
    /// <c>define_mesh</c> is. This mesh is not changed.
    /// </summary>
    /// <param name="movedVertices">New positions, by vertex id.</param>
    /// <param name="reshapedFaces">New loops, by face id; the face keeps its id.</param>
    /// <param name="addedVertices">Vertices appended after the last, taking ids <c>Vertices.Count</c> onward.</param>
    /// <param name="addedFaces">Faces appended after the last, taking ids <c>Faces.Count</c> onward. Their indices, like
    /// those in <paramref name="reshapedFaces"/>, are ids in the result, so they may use added vertices.</param>
    /// <remarks>An id in <paramref name="movedVertices"/> or <paramref name="reshapedFaces"/> that this mesh does not
    /// have is refused as <c>vertices[n]</c> or <c>faces[n]</c>, beside whatever else is wrong.</remarks>
    public MeshResult Edit(
        IEnumerable<KeyValuePair<int, Vector3>>? movedVertices = null,
        IEnumerable<KeyValuePair<int, IEnumerable<int>>>? reshapedFaces = null,
        IEnumerable<Vector3>? addedVertices = null,
        IEnumerable<IEnumerable<int>>? addedFaces = null)
    {
        var errors = new List<MeshError>();
        var verts = (Vector3[])_vertices.Clone();
        var fcs = (int[][])_faces.Clone();

        foreach (var (id, position) in movedVertices ?? [])
        {
            if (id < 0 || id >= verts.Length)
                errors.Add(new MeshError($"vertices[{id}]", $"there is no vertex {id} to move: ids run 0..{verts.Length - 1}"));
            else
                verts[id] = position;
        }

        foreach (var (id, loop) in reshapedFaces ?? [])
        {
            if (id < 0 || id >= fcs.Length)
                errors.Add(new MeshError($"faces[{id}]", $"there is no face {id} to reshape: ids run 0..{fcs.Length - 1}"));
            else
                fcs[id] = loop.ToArray();
        }

        var result = Create(verts.Concat(addedVertices ?? []), fcs.Concat((addedFaces ?? []).Select(f => f.ToArray())));
        return errors.Count == 0 ? result : new MeshResult(null, [.. errors, .. result.Errors]);
    }

    /// <summary>Centre of a face: the mean of its vertices.</summary>
    public Vector3 FaceCentre(int face)
    {
        var loop = _faces[face];
        var o = _vertices[loop[0]];
        double x = 0, y = 0, z = 0;
        foreach (var i in loop)
        {
            var v = _vertices[i];
            x += (double)v.X - o.X; y += (double)v.Y - o.Y; z += (double)v.Z - o.Z;
        }
        return new Vector3((float)(o.X + x / loop.Length), (float)(o.Y + y / loop.Length), (float)(o.Z + z / loop.Length));
    }

    /// <summary>Unit normal of a face by Newell's method; outward for a face wound counter-clockwise from outside.</summary>
    public Vector3 FaceNormal(int face)
    {
        var (x, y, z) = Unit(NewellVector(_vertices, _faces[face]));
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
