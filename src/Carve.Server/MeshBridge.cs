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
/// own carrying the normal of its corner, all from <see cref="Shading"/>, the triangles and normals the export
/// writes too (a flat mesh gives each corner its face's Newell normal). Each triangle's winding is reversed, because Carve winds counter-clockwise from outside
/// and Empyrean clockwise.
/// </summary>
public static class MeshBridge
{
    /// <param name="uvs">One UV per vertex written, in this order (A, C, B per triangle), from <see cref="LookMeshes"/>'s checker
    /// look; null leaves every <c>TexCoord</c> zero, the buffer every other look has always had.</param>
    public static MeshData<PositionUvNormalVertex> ToMeshData(PolygonMesh mesh, DrawnSpace space, Vector2[]? uvs = null)
    {
        var triangles = Shading.Triangles(mesh);
        var vertices = new PositionUvNormalVertex[triangles.Count * 3];
        var indices = new uint[vertices.Length];

        for (var t = 0; t < triangles.Count; t++)
        {
            var triangle = triangles[t];
            var at = t * 3;
            vertices[at] = new PositionUvNormalVertex { Position = space.Apply(triangle.A), Normal = triangle.NormalA };
            vertices[at + 1] = new PositionUvNormalVertex { Position = space.Apply(triangle.C), Normal = triangle.NormalC };
            vertices[at + 2] = new PositionUvNormalVertex { Position = space.Apply(triangle.B), Normal = triangle.NormalB };
            indices[at] = (uint)at;
            indices[at + 1] = (uint)(at + 1);
            indices[at + 2] = (uint)(at + 2);
            if (uvs is not null)
            {
                vertices[at].TexCoord = uvs[at];
                vertices[at + 1].TexCoord = uvs[at + 1];
                vertices[at + 2].TexCoord = uvs[at + 2];
            }
        }

        return new MeshData<PositionUvNormalVertex>(vertices, indices);
    }

    /// <summary>
    /// The same triangles and vertices as <see cref="ToMeshData"/>, with the index buffer cut into one submesh per colour
    /// (D-6, Colour is a material): the uncoloured faces first when there are any, then each colour in the order its first
    /// face appears. <paramref name="colours"/> is each submesh's colour as <c>#RRGGBB</c>, null for the uncoloured one.
    /// Vertices stay in triangle order, so per-vertex data made for <see cref="ToMeshData"/> still lines up.
    /// </summary>
    public static MeshData<PositionUvNormalVertex> ToSubmeshedMeshData(PolygonMesh mesh, DrawnSpace space, out string?[] colours)
    {
        var plain = ToMeshData(mesh, space);
        var triangles = Shading.Triangles(mesh);
        var keys = new List<string?>();
        var ranges = new List<List<uint>>();
        var style = mesh.Style;
        for (var t = 0; t < triangles.Count; t++)
        {
            var key = style?.ColourOf(triangles[t].Face)?.ToUpperInvariant();
            var at = keys.IndexOf(key);
            if (at < 0) { keys.Add(key); ranges.Add([]); at = keys.Count - 1; }
            ranges[at].AddRange([(uint)(t * 3), (uint)(t * 3 + 1), (uint)(t * 3 + 2)]);
        }

        var order = Enumerable.Range(0, keys.Count).OrderBy(i => keys[i] is null ? 0 : 1).ThenBy(i => i).ToArray();
        colours = order.Select(i => keys[i]).ToArray();
        var indices = new List<uint>(triangles.Count * 3);
        var submeshes = new Submesh[order.Length];
        for (var s = 0; s < order.Length; s++)
        {
            submeshes[s] = new Submesh(indices.Count, ranges[order[s]].Count);
            indices.AddRange(ranges[order[s]]);
        }

        return new MeshData<PositionUvNormalVertex>(plain.Vertices!, indices.ToArray(), submeshes);
    }
}
