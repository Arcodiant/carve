using System.Numerics;

namespace Carve.Core;

/// <summary>
/// One flat-shaded triangle in model space: its corners in Carve's winding (counter-clockwise seen from outside),
/// the Newell normal of the face it came from, and that face's id.
/// </summary>
public readonly record struct FlatTriangle(Vector3 A, Vector3 B, Vector3 C, Vector3 Normal, int Face);

/// <summary>
/// The one source of the triangles that are drawn and exported: every triangle in <see cref="Triangulator"/> order, each
/// corner the mesh's own position and each triangle its face's <see cref="PolygonMesh.FaceNormal"/>, so every edge is hard.
/// A derived view; never stored.
/// </summary>
public static class FlatShading
{
    public static IReadOnlyList<FlatTriangle> Triangles(PolygonMesh mesh)
    {
        var triangles = Triangulator.Triangulate(mesh);
        var result = new FlatTriangle[triangles.Count];
        var normals = new Vector3?[mesh.Faces.Count];
        for (var t = 0; t < triangles.Count; t++)
        {
            var triangle = triangles[t];
            var normal = normals[triangle.Face] ??= mesh.FaceNormal(triangle.Face);
            result[t] = new FlatTriangle(
                mesh.Vertices[triangle.A], mesh.Vertices[triangle.B], mesh.Vertices[triangle.C], normal, triangle.Face);
        }
        return result;
    }
}
