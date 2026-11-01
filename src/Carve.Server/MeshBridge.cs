using System.Numerics;
using Carve.Core;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server;

/// <summary>
/// The view of a mesh that is drawn: centred on the origin with a half-diagonal of 1, worked in double
/// and narrowed to float only after that, so the camera's arithmetic never meets the mesh's size or
/// place. The scale is uniform, so normals stay as they are. Only the drawn copy lives here: the stored
/// mesh is never changed, and the agent is never told these coordinates.
/// </summary>
public readonly record struct DrawnSpace(double X, double Y, double Z, double Radius)
{
    public static DrawnSpace Of(Vector3 min, Vector3 max)
    {
        var (x, y, z) = (((double)max.X - min.X) / 2, ((double)max.Y - min.Y) / 2, ((double)max.Z - min.Z) / 2);
        var radius = Math.Sqrt(x * x + y * y + z * z);
        return new DrawnSpace(min.X + x, min.Y + y, min.Z + z, radius > 0 ? radius : 1);
    }

    public Vector3 Apply(Vector3 v) =>
        new((float)((v.X - X) / Radius), (float)((v.Y - Y) / Radius), (float)((v.Z - Z) / Radius));
}

/// <summary>
/// A Carve mesh as the engine draws it: flat shaded, so each face's triangles get vertices of their
/// own carrying the face's Newell normal. Each triangle's winding is reversed, because Carve winds
/// counter-clockwise from outside and Empyrean clockwise.
/// </summary>
public static class MeshBridge
{
    public static MeshData<PositionUvNormalVertex> ToMeshData(PolygonMesh mesh, DrawnSpace space)
    {
        var triangles = Triangulator.Triangulate(mesh);
        var vertices = new PositionUvNormalVertex[triangles.Count * 3];
        var indices = new uint[vertices.Length];

        for (var t = 0; t < triangles.Count; t++)
        {
            var triangle = triangles[t];
            var normal = mesh.FaceNormal(triangle.Face);
            var at = t * 3;
            vertices[at] = new PositionUvNormalVertex { Position = space.Apply(mesh.Vertices[triangle.A]), Normal = normal };
            vertices[at + 1] = new PositionUvNormalVertex { Position = space.Apply(mesh.Vertices[triangle.C]), Normal = normal };
            vertices[at + 2] = new PositionUvNormalVertex { Position = space.Apply(mesh.Vertices[triangle.B]), Normal = normal };
            indices[at] = (uint)at;
            indices[at + 1] = (uint)(at + 1);
            indices[at + 2] = (uint)(at + 2);
        }

        return new MeshData<PositionUvNormalVertex>(vertices, indices);
    }
}
