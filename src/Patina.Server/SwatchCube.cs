using System.Numerics;
using Empyrean;

namespace Patina.Server;

/// <summary>
/// The cube view's geometry (T-157), built here because the engine's own <c>PrimitiveMeshData.Cube</c> documents neither its
/// face UVs nor their orientation. A cube of edge <see cref="Edge"/> centred on the origin, each face its own four vertices
/// with UVs 0..1 across the face (u to the right and v down as the face is seen from outside, with +y up on the side faces;
/// the top has -z at its top edge, the bottom +z), flat normals. A texture that tiles is continuous across a face and meets
/// itself at each edge as the face beside it begins. Seen by <see cref="SwatchCamera"/>, turned by <see cref="Orientation"/>
/// so the front, the left and the top show. The scenario restates the faces to predict the frame.
/// </summary>
public static class SwatchCube
{
    /// <summary>The cube's edge: 0.7 keeps its nearest corner inside the frame at any angle.</summary>
    public const float Edge = 0.7f;

    /// <summary>The turn about y the cube has at angle 0, so three faces show (degrees).</summary>
    public const float Yaw = 35f;

    /// <summary>The cube's lean toward the camera, showing the top (degrees, about x).</summary>
    public const float Tilt = 25f;

    /// <summary>Each face: its outward normal, the corner at UV (0, 0), and the directions u and v run (unit edge units).</summary>
    public static readonly (Vector3 Normal, Vector3 Origin, Vector3 U, Vector3 V)[] Faces =
    [
        (new(0, 0, 1), new(-0.5f, 0.5f, 0.5f), new(1, 0, 0), new(0, -1, 0)),
        (new(0, 0, -1), new(0.5f, 0.5f, -0.5f), new(-1, 0, 0), new(0, -1, 0)),
        (new(1, 0, 0), new(0.5f, 0.5f, 0.5f), new(0, 0, -1), new(0, -1, 0)),
        (new(-1, 0, 0), new(-0.5f, 0.5f, -0.5f), new(0, 0, 1), new(0, -1, 0)),
        (new(0, 1, 0), new(-0.5f, 0.5f, -0.5f), new(1, 0, 0), new(0, 0, 1)),
        (new(0, -1, 0), new(-0.5f, -0.5f, 0.5f), new(1, 0, 0), new(0, 0, -1)),
    ];

    /// <summary>The cube's turn at <paramref name="angle"/> degrees (the sphere's angle, added to <see cref="Yaw"/>).</summary>
    public static Quaternion Orientation(float angle) =>
        Quaternion.Concatenate(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, (Yaw + angle) * MathF.PI / 180),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, Tilt * MathF.PI / 180));

    /// <summary>The mesh: 24 vertices and 12 triangles, wound as the engine's sphere is (front faces clockwise from outside).</summary>
    public static MeshData<PositionUvNormalVertex> Mesh()
    {
        var vertices = new PositionUvNormalVertex[Faces.Length * 4];
        var indices = new uint[Faces.Length * 6];
        for (var f = 0; f < Faces.Length; f++)
        {
            var (normal, origin, u, v) = Faces[f];
            for (var corner = 0; corner < 4; corner++)
            {
                var cu = corner & 1;
                var cv = corner >> 1;
                vertices[f * 4 + corner] = new PositionUvNormalVertex
                {
                    Position = (origin + u * cu + v * cv) * Edge,
                    TexCoord = new Vector2(cu, cv),
                    Normal = normal,
                };
            }
            var b = (uint)(f * 4);
            // Corners 0 (0,0), 1 (1,0), 2 (0,1), 3 (1,1); u x v points into the cube, so (0, 1, 2) is clockwise from outside.
            uint[] face = [b, b + 1, b + 2, b + 1, b + 3, b + 2];
            face.CopyTo(indices, f * 6);
        }
        return new MeshData<PositionUvNormalVertex>(vertices, indices);
    }
}
