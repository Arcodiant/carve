using Carve.Core;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server;

/// <summary>
/// A Carve mesh as the engine draws it: flat shaded, so each face's triangles get vertices of their
/// own carrying the face's Newell normal. Each triangle's winding is reversed, because Carve winds
/// counter-clockwise from outside and Empyrean clockwise.
/// </summary>
public static class MeshBridge
{
    public static MeshData<PositionUvNormalVertex> ToMeshData(PolygonMesh mesh)
    {
        var triangles = Triangulator.Triangulate(mesh);
        var vertices = new PositionUvNormalVertex[triangles.Count * 3];
        var indices = new uint[vertices.Length];

        for (var t = 0; t < triangles.Count; t++)
        {
            var triangle = triangles[t];
            var normal = mesh.FaceNormal(triangle.Face);
            var at = t * 3;
            vertices[at] = new PositionUvNormalVertex { Position = mesh.Vertices[triangle.A], Normal = normal };
            vertices[at + 1] = new PositionUvNormalVertex { Position = mesh.Vertices[triangle.C], Normal = normal };
            vertices[at + 2] = new PositionUvNormalVertex { Position = mesh.Vertices[triangle.B], Normal = normal };
            indices[at] = (uint)at;
            indices[at + 1] = (uint)(at + 1);
            indices[at + 2] = (uint)(at + 2);
        }

        return new MeshData<PositionUvNormalVertex>(vertices, indices);
    }
}
