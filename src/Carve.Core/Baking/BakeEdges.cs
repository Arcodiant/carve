using System.Numerics;

namespace Carve.Core.Baking;

/// <summary>Which way a shading-break edge bends, seen from the face it is reported for.</summary>
public enum EdgeShape
{
    /// <summary>The two faces are coplanar (a marked hard edge across a flat stretch of a smooth mesh); the bend is 0.</summary>
    Flat,

    /// <summary>The other face turns away from this face's normal, as a box's edges do.</summary>
    Convex,

    /// <summary>The other face turns toward this face's normal, as an L-block's inner edge does.</summary>
    Concave,
}

/// <summary>
/// A shading-break edge near a texel (<see cref="BakeLayout.NearEdges"/>), seen from the texel's own face.
/// </summary>
/// <param name="Lo">The edge's lower vertex id.</param>
/// <param name="Hi">The edge's higher vertex id.</param>
/// <param name="OtherFace">The face across the edge.</param>
/// <param name="Distance">From the texel's rest position to the edge segment, in metres, in 3D.</param>
/// <param name="Closest">The point of the edge segment nearest the texel.</param>
/// <param name="OtherNormal">The other face's geometric normal.</param>
/// <param name="Bend">The dihedral bend angle in radians, between the two faces' normals: 0 when flat, π/2 at a box's edge.</param>
/// <param name="Shape">Convex, concave or flat.</param>
public readonly record struct NearEdge(
    int Lo, int Hi, int OtherFace, double Distance, Vector3 Closest, Vector3 OtherNormal, double Bend, EdgeShape Shape);

public sealed partial class BakeLayout
{
    private BreakEdge[][]? _breaks;

    /// <summary>
    /// Each shading-break edge of <paramref name="texel"/>'s own face within <paramref name="width"/> metres of the texel's
    /// rest position, measured in 3D to the edge segment, nearest first (a tie by vertex ids, then the other face). It
    /// never looks at UVs, so it works across UV seams. An edge with three or more faces is listed once for each other face.
    /// </summary>
    /// <remarks>
    /// The breaks are the edges render draws hard (<see cref="Shading.Corners"/>): on a smooth mesh, the marked hard edges
    /// (<see cref="MeshShade.Hard"/>) and the edges Shading never smooths across (one shared by three or more faces, or by
    /// two wound the same way); on a flat mesh, every edge between two faces that are not coplanar
    /// (<see cref="AreCoplanar"/>). A boundary edge, with one face, is not a break.
    /// </remarks>
    public IReadOnlyList<NearEdge> NearEdges(BakeTexel texel, double width)
    {
        if (!(width >= 0) || !double.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(width), "width must be a finite number of metres, 0 or more");
        var breaks = LazyInitializer.EnsureInitialized(ref _breaks, () => Breaks(Mesh));
        var p = texel.Position;
        var result = new List<NearEdge>();
        foreach (var e in breaks[texel.Face])
        {
            var (d, closest) = ToSegment(p, Mesh.Vertices[e.Lo], Mesh.Vertices[e.Hi]);
            if (d <= width) result.Add(new NearEdge(e.Lo, e.Hi, e.Other, d, closest, e.OtherNormal, e.Bend, e.Shape));
        }
        result.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Lo != b.Lo ? a.Lo.CompareTo(b.Lo) : a.Hi != b.Hi ? a.Hi.CompareTo(b.Hi) : a.OtherFace.CompareTo(b.OtherFace));
        return result;
    }

    /// <summary>
    /// Whether faces <paramref name="f"/> and <paramref name="g"/> are coplanar, by the planarity rule Carve's check already
    /// uses for one face (<see cref="PolygonMesh.PlanarityTolerance"/>): they face the same way, and every corner of each
    /// lies within <c>PlanarityTolerance</c> times the bounding-box diagonal of both faces' corners together of the other's
    /// plane (its normal, through the mean of its outer loop). So two faces are coplanar exactly when, merged, the check
    /// would not call the one face non-planar.
    /// </summary>
    public static bool AreCoplanar(PolygonMesh mesh, int f, int g)
    {
        var verts = mesh.Vertices;
        var nf = mesh.FaceNormal(f);
        var ng = mesh.FaceNormal(g);
        if (Vector3.Dot(nf, ng) <= 0) return false;
        var both = mesh.Faces[f].Corners.Concat(mesh.Faces[g].Corners).ToArray();
        var limit = PolygonMesh.PlanarityTolerance * PolygonMesh.FaceSize(verts, both);
        return Within(mesh, f, nf, mesh.Faces[g].Corners, limit) && Within(mesh, g, ng, mesh.Faces[f].Corners, limit);
    }

    private static bool Within(PolygonMesh mesh, int face, Vector3 normal, IReadOnlyList<int> corners, double limit)
    {
        var o = Mean(mesh, mesh.Faces[face].Loop);
        foreach (var i in corners)
        {
            var v = mesh.Vertices[i];
            var d = (v.X - o.X) * normal.X + (v.Y - o.Y) * normal.Y + (v.Z - o.Z) * normal.Z;
            if (Math.Abs(d) > limit) return false;
        }
        return true;
    }

    private static (double X, double Y, double Z) Mean(PolygonMesh mesh, IReadOnlyList<int> ids)
    {
        double x = 0, y = 0, z = 0;
        foreach (var i in ids) { var v = mesh.Vertices[i]; x += v.X; y += v.Y; z += v.Z; }
        return (x / ids.Count, y / ids.Count, z / ids.Count);
    }

    private readonly record struct BreakEdge(int Lo, int Hi, int Other, Vector3 OtherNormal, double Bend, EdgeShape Shape);

    /// <summary>Every face's break edges, in edge order, then the other faces' order along the edge.</summary>
    private static BreakEdge[][] Breaks(PolygonMesh mesh)
    {
        var lists = new List<BreakEdge>[mesh.Faces.Count];
        for (var f = 0; f < lists.Length; f++) lists[f] = [];
        var smooth = mesh.ShadingMode == ShadingMode.Smooth;
        foreach (var edge in mesh.Topology.Edges)
        {
            if (edge.Faces.Count < 2) continue;
            var edgeHard = smooth && (!edge.IsManifold || mesh.Shade!.IsHard(edge.Lo, edge.Hi));
            if (smooth && !edgeHard) continue;
            foreach (var a in edge.Faces)
            foreach (var b in edge.Faces)
            {
                if (a.Face == b.Face) continue;
                var coplanar = AreCoplanar(mesh, a.Face, b.Face);
                if (!smooth && coplanar) continue;
                var n1 = mesh.FaceNormal(a.Face);
                var n2 = mesh.FaceNormal(b.Face);
                double bend = 0;
                var shape = EdgeShape.Flat;
                if (!coplanar)
                {
                    bend = Math.Acos(Math.Clamp(Vector3.Dot(n1, n2), -1f, 1f));
                    // Which side of this face's plane the other face lies on, next to the edge: the direction into the other
                    // face, across the edge in its own plane, is its normal crossed with the edge as its winding runs it. That
                    // holds for a non-convex other face (an extruded L's cap), whose corners' mean may lie beyond the edge's
                    // line, and does not rest on the two faces being wound alike, since the other face's normal and its
                    // run of the edge turn over together.
                    var lo = mesh.Vertices[edge.Lo];
                    var hi = mesh.Vertices[edge.Hi];
                    var run = b.LoToHi ? hi - lo : lo - hi;
                    var inward = Vector3.Cross(n2, run);
                    var side = (double)inward.X * n1.X + (double)inward.Y * n1.Y + (double)inward.Z * n1.Z;
                    shape = side > 0 ? EdgeShape.Concave : EdgeShape.Convex;
                }
                lists[a.Face].Add(new BreakEdge(edge.Lo, edge.Hi, b.Face, n2, bend, shape));
            }
        }
        return lists.Select(l => l.ToArray()).ToArray();
    }

    /// <summary>The distance from p to segment a-b, in double, and the segment's nearest point.</summary>
    private static (double Distance, Vector3 Closest) ToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
        double px = p.X - a.X, py = p.Y - a.Y, pz = p.Z - a.Z;
        var len2 = dx * dx + dy * dy + dz * dz;
        var s = len2 > 0 ? Math.Clamp((px * dx + py * dy + pz * dz) / len2, 0, 1) : 0;
        double ex = px - s * dx, ey = py - s * dy, ez = pz - s * dz;
        var closest = new Vector3((float)(a.X + s * dx), (float)(a.Y + s * dy), (float)(a.Z + s * dz));
        return (Math.Sqrt(ex * ex + ey * ey + ez * ez), closest);
    }
}
