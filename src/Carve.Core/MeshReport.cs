using System.Numerics;

namespace Carve.Core;

public enum MeshWarningKind
{
    OpenEdge,
    NonManifoldEdge,
    InconsistentWinding,
    NonPlanarFace,
    UnusedVertex,
}

/// <summary>A legal-but-suspicious feature of a mesh. <see cref="Element"/> names it, e.g. <c>faces[2]</c>, <c>vertices[7]</c> or <c>edges[0-1]</c>.</summary>
public sealed record MeshWarning(MeshWarningKind Kind, string Element, string Message)
{
    public override string ToString() => $"{Element}: {Message}";
}

/// <summary>Counts, bounds and warnings for a <see cref="PolygonMesh"/>.</summary>
public sealed class MeshReport
{
    private MeshReport(int vertexCount, int faceCount, int edgeCount, bool closed, Vector3 boundsMin, Vector3 boundsMax, IReadOnlyList<MeshWarning> warnings)
    {
        VertexCount = vertexCount;
        FaceCount = faceCount;
        EdgeCount = edgeCount;
        Closed = closed;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;
        Warnings = warnings;
    }

    public int VertexCount { get; }
    public int FaceCount { get; }

    /// <summary>Distinct undirected edges.</summary>
    public int EdgeCount { get; }

    /// <summary>True when every edge is shared by exactly two faces (no open and no over-shared edges).</summary>
    public bool Closed { get; }

    /// <summary>Axis-aligned bounds of the vertices the faces use.</summary>
    public Vector3 BoundsMin { get; }
    public Vector3 BoundsMax { get; }
    public IReadOnlyList<MeshWarning> Warnings { get; }

    public static MeshReport Create(PolygonMesh mesh)
    {
        var verts = mesh.Vertices;
        var warnings = new List<MeshWarning>();
        var used = new bool[verts.Count];
        // undirected edge (lo, hi) -> faces running lo->hi, faces running hi->lo
        var edges = new Dictionary<(int, int), (int Forward, int Backward)>();

        for (var f = 0; f < mesh.Faces.Count; f++)
        {
            var face = mesh.Faces[f];
            for (var k = 0; k < face.Count; k++)
            {
                var a = face[k];
                var b = face[(k + 1) % face.Count];
                used[a] = true;
                var key = a < b ? (a, b) : (b, a);
                edges.TryGetValue(key, out var c);
                edges[key] = a < b ? (c.Forward + 1, c.Backward) : (c.Forward, c.Backward + 1);
            }
        }

        var closed = true;
        foreach (var (key, c) in edges.OrderBy(e => e.Key))
        {
            var name = $"edges[{key.Item1}-{key.Item2}]";
            var total = c.Forward + c.Backward;
            if (total == 1)
            {
                closed = false;
                warnings.Add(new MeshWarning(MeshWarningKind.OpenEdge, name, "edge belongs to only one face"));
            }
            else if (total > 2)
            {
                closed = false;
                warnings.Add(new MeshWarning(MeshWarningKind.NonManifoldEdge, name, $"edge is shared by {total} faces"));
            }
            else if (c.Forward == 2 || c.Backward == 2)
            {
                warnings.Add(new MeshWarning(MeshWarningKind.InconsistentWinding, name, "two faces cross this edge in the same direction"));
            }
        }

        for (var f = 0; f < mesh.Faces.Count; f++)
        {
            var face = mesh.Faces[f];
            if (face.Count <= 3) continue;
            var normal = mesh.FaceNormal(f);
            var centroid = Vector3.Zero;
            foreach (var i in face) centroid += verts[i];
            centroid /= face.Count;
            var limit = PolygonMesh.PlanarityTolerance * PolygonMesh.FaceSize(verts, face);
            var worst = face.Max(i => MathF.Abs(Vector3.Dot(verts[i] - centroid, normal)));
            if (worst > limit)
                warnings.Add(new MeshWarning(MeshWarningKind.NonPlanarFace, $"faces[{f}]", $"face is not planar: a vertex is {worst:G3} off its plane (limit {limit:G3})"));
        }

        for (var i = 0; i < verts.Count; i++)
        {
            if (!used[i])
                warnings.Add(new MeshWarning(MeshWarningKind.UnusedVertex, $"vertices[{i}]", "no face uses this vertex"));
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i < verts.Count; i++)
        {
            if (!used[i]) continue;
            min = Vector3.Min(min, verts[i]);
            max = Vector3.Max(max, verts[i]);
        }

        return new MeshReport(verts.Count, mesh.Faces.Count, edges.Count, closed, min, max, warnings);
    }
}
