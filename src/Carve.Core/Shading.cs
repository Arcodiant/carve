using System.Numerics;

namespace Carve.Core;

/// <summary>How a mesh is shaded (D-6, Smooth shading is per mesh): <see cref="Flat"/> is the default, as M-3 draws it.</summary>
public enum ShadingMode
{
    Flat,
    Smooth,
}

/// <summary>
/// A mesh's shading: its mode and its hard edges, each a vertex-id pair, lower id first. Immutable. The hard edges are kept
/// while the mesh is flat (every edge is hard then), so switching to smooth finds them again.
/// </summary>
public sealed class MeshShade
{
    private readonly (int Lo, int Hi)[] _hard;

    private MeshShade(ShadingMode mode, (int Lo, int Hi)[] hard)
    {
        Mode = mode;
        _hard = hard;
    }

    public static MeshShade Flat { get; } = new(ShadingMode.Flat, []);

    public ShadingMode Mode { get; }

    /// <summary>The hard edges, in order.</summary>
    public IReadOnlyList<(int Lo, int Hi)> Hard => _hard;

    /// <summary>Flat and no hard edge: what a mesh with no shading holds.</summary>
    public bool IsDefault => Mode == ShadingMode.Flat && _hard.Length == 0;

    public bool IsHard(int a, int b) => System.Array.BinarySearch(_hard, a < b ? (a, b) : (b, a)) >= 0;

    public MeshShade WithMode(ShadingMode mode) => new(mode, _hard);

    /// <summary>These hard edges with <paramref name="edges"/> added (<paramref name="hard"/>) or removed; vertex order in a pair does not matter.</summary>
    public MeshShade WithEdges(IEnumerable<(int A, int B)> edges, bool hard)
    {
        var set = new SortedSet<(int Lo, int Hi)>(_hard);
        foreach (var (a, b) in edges)
        {
            var pair = a < b ? (a, b) : (b, a);
            if (hard) set.Add(pair); else set.Remove(pair);
        }
        return new MeshShade(Mode, [.. set]);
    }

    /// <summary>A shade of <paramref name="mode"/> with these hard edges.</summary>
    public static MeshShade Of(ShadingMode mode, IEnumerable<(int A, int B)> hard) =>
        Flat.WithMode(mode).WithEdges(hard, true);
}

/// <summary>One triangle to draw and export: its corners in Carve's winding, a normal at each corner, its face's Newell normal and its face id.</summary>
public readonly record struct ShadedTriangle(
    Vector3 A, Vector3 B, Vector3 C, Vector3 NormalA, Vector3 NormalB, Vector3 NormalC, Vector3 Normal, int Face);

/// <summary>
/// Per-corner normals (D-6, Smooth shading is per mesh, with hard edges marked): the one function that render and export both
/// take their triangles and normals from, so they cannot disagree. A flat mesh gives each corner its face's normal, exactly
/// <see cref="FlatShading"/>'s. A smooth mesh averages, at each vertex, the normals of the faces joined by soft edges, each
/// face weighted by its corner angle there (the person, 2026-11-09). An edge is soft when exactly two faces share it, wound
/// opposite ways, and it is not marked hard; an open edge (one face) has nothing across it, and an edge of three or more
/// faces is hard, so the result never depends on face order. A corner that averages one face keeps that face's normal exactly.
/// </summary>
public static class Shading
{
    /// <summary>The triangles in <see cref="Triangulator"/> order, each with its corner normals.</summary>
    public static IReadOnlyList<ShadedTriangle> Triangles(PolygonMesh mesh)
    {
        var triangles = Triangulator.Triangulate(mesh);
        var result = new ShadedTriangle[triangles.Count];
        var faceNormals = new Vector3?[mesh.Faces.Count];
        var corners = mesh.ShadingMode == ShadingMode.Smooth || mesh.PosedCorners is not null ? CornersOf(mesh) : null;
        for (var t = 0; t < triangles.Count; t++)
        {
            var triangle = triangles[t];
            var normal = faceNormals[triangle.Face] ??= mesh.FaceNormal(triangle.Face);
            var a = mesh.Vertices[triangle.A];
            var b = mesh.Vertices[triangle.B];
            var c = mesh.Vertices[triangle.C];
            if (corners is null)
            {
                result[t] = new ShadedTriangle(a, b, c, normal, normal, normal, normal, triangle.Face);
                continue;
            }
            var loop = mesh.Faces[triangle.Face];
            var at = corners[triangle.Face];
            result[t] = new ShadedTriangle(a, b, c, at[IndexOf(loop, triangle.A)], at[IndexOf(loop, triangle.B)],
                at[IndexOf(loop, triangle.C)], normal, triangle.Face);
        }
        return result;
    }

    /// <summary>The normal at each corner of each face (face id, then position in its loop): a posed copy's own when it has them, else <see cref="Corners"/>.</summary>
    public static Vector3[][] CornersOf(PolygonMesh mesh) => mesh.PosedCorners ?? Corners(mesh);

    /// <summary>The normal at each corner of each face, worked from the mesh's positions, its mode and its hard edges.</summary>
    public static Vector3[][] Corners(PolygonMesh mesh)
    {
        var faces = mesh.Faces;
        var result = new Vector3[faces.Count][];
        for (var f = 0; f < faces.Count; f++)
        {
            var n = mesh.FaceNormal(f);
            result[f] = Enumerable.Repeat(n, faces[f].Count).ToArray();
        }
        if (mesh.ShadingMode != ShadingMode.Smooth) return result;

        var topology = mesh.Topology;
        var shade = mesh.Shade!;
        var faceNormals = result.Select(r => r[0]).ToArray();
        for (var v = 0; v < mesh.Vertices.Count; v++)
        {
            var around = topology.VertexFaces(v);
            if (around.Count < 2) continue;
            var parent = Enumerable.Range(0, around.Count).ToArray();
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            var slot = new Dictionary<int, int>();
            for (var i = 0; i < around.Count; i++) slot[around[i]] = i;

            for (var i = 0; i < around.Count; i++)
            {
                var loop = faces[around[i]];
                var k = IndexOf(loop, v);
                foreach (var other in new[] { loop[(k + 1) % loop.Count], loop[(k + loop.Count - 1) % loop.Count] })
                {
                    if (!topology.TryGetEdge(v, other, out var edge) || !edge.IsManifold || shade.IsHard(v, other)) continue;
                    foreach (var use in edge.Faces)
                        if (use.Face != around[i] && slot.TryGetValue(use.Face, out var j)) parent[Find(i)] = Find(j);
                }
            }

            foreach (var group in Enumerable.Range(0, around.Count).GroupBy(Find))
            {
                var members = group.ToList();
                if (members.Count == 1) continue; // one face keeps its own normal, to the bit
                double x = 0, y = 0, z = 0;
                foreach (var i in members)
                {
                    var f = around[i];
                    var loop = faces[f];
                    var k = IndexOf(loop, v);
                    var w = Angle(mesh.Vertices[v], mesh.Vertices[loop[(k + 1) % loop.Count]], mesh.Vertices[loop[(k + loop.Count - 1) % loop.Count]]);
                    var n = faceNormals[f];
                    x += w * n.X; y += w * n.Y; z += w * n.Z;
                }
                var len = Math.Sqrt(x * x + y * y + z * z);
                foreach (var i in members)
                {
                    var f = around[i];
                    result[f][IndexOf(faces[f], v)] = len > 0 ? new Vector3((float)(x / len), (float)(y / len), (float)(z / len)) : faceNormals[f];
                }
            }
        }
        return result;
    }

    /// <summary>The angle at <paramref name="at"/> between the directions to <paramref name="p"/> and <paramref name="q"/>, in double.</summary>
    private static double Angle(Vector3 at, Vector3 p, Vector3 q)
    {
        double ax = (double)p.X - at.X, ay = (double)p.Y - at.Y, az = (double)p.Z - at.Z;
        double bx = (double)q.X - at.X, by = (double)q.Y - at.Y, bz = (double)q.Z - at.Z;
        var cx = ay * bz - az * by;
        var cy = az * bx - ax * bz;
        var cz = ax * by - ay * bx;
        return Math.Atan2(Math.Sqrt(cx * cx + cy * cy + cz * cz), ax * bx + ay * by + az * bz);
    }

    private static int IndexOf(IReadOnlyList<int> loop, int vertex)
    {
        for (var i = 0; i < loop.Count; i++) if (loop[i] == vertex) return i;
        throw new ArgumentException("the vertex is not in the face", nameof(vertex));
    }
}
