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
        var topology = mesh.Topology;
        var warnings = new List<MeshWarning>();
        var used = new bool[verts.Count];
        for (var i = 0; i < verts.Count; i++)
            used[i] = topology.VertexFaces(i).Count > 0;

        var closed = true;
        foreach (var edge in topology.Edges)
        {
            var name = edge.Name;
            var total = edge.Faces.Count;
            var forward = edge.Faces.Count(u => u.LoToHi);
            var c = (Forward: forward, Backward: total - forward);
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

            // Two or more faces running the same way along an edge disagree about which side is outside.
            if (c.Forward >= 2 || c.Backward >= 2)
                warnings.Add(new MeshWarning(MeshWarningKind.InconsistentWinding, name, "two faces cross this edge in the same direction"));
        }

        for (var f = 0; f < mesh.Faces.Count; f++)
        {
            // The plane is the outer loop's (its normal and the mean of its corners); a hole corner off it is non-planar too.
            var face = mesh.Faces[f].Loop;
            var corners = mesh.Faces[f].Corners;
            if (corners.Count <= 3) continue;
            var normal = PolygonMesh.Unit(PolygonMesh.NewellVector(verts, face));
            var o = verts[face[0]];
            double cx = 0, cy = 0, cz = 0;
            foreach (var i in face) { cx += (double)verts[i].X - o.X; cy += (double)verts[i].Y - o.Y; cz += (double)verts[i].Z - o.Z; }
            cx /= face.Count; cy /= face.Count; cz /= face.Count;
            var limit = PolygonMesh.PlanarityTolerance * PolygonMesh.FaceSize(verts, corners);
            var worst = corners.Max(i => Math.Abs(((double)verts[i].X - o.X - cx) * normal.X + ((double)verts[i].Y - o.Y - cy) * normal.Y + ((double)verts[i].Z - o.Z - cz) * normal.Z));
            if (worst > limit)
                warnings.Add(new MeshWarning(MeshWarningKind.NonPlanarFace, $"faces[{f}]", MeshEdits.Invariant($"face is not planar: a vertex is {worst:G3} off its plane (limit {limit:G3})")));
        }

        for (var i = 0; i < verts.Count; i++)
        {
            if (!used[i] && !mesh.IsVertexDeleted(i))
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

        return new MeshReport(mesh.LiveVertexCount, mesh.LiveFaceCount, topology.Edges.Count, closed, min, max, warnings);
    }
}
