using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace Carve.ModelViewer.Tests;

/// <summary>
/// The unit cube from (0,0,0) to (1,1,1), flat normals, the <c>carve</c> material, written with
/// SharpGLTF straight: the viewer is proved on its own, with no call to Carve's exporter.
/// </summary>
internal static class CubeFixture
{
    public const float Metallic = 0f;

    public const float Roughness = 1f;

    public static readonly Vector4 BaseColor = new(0.72f, 0.72f, 0.70f, 1f);

    /// <summary>Each face's normal and its corners, counter-clockwise from outside.</summary>
    private static readonly (Vector3 Normal, Vector3[] Corners)[] Faces =
    [
        (Vector3.UnitX, [new(1, 0, 1), new(1, 0, 0), new(1, 1, 0), new(1, 1, 1)]),
        (-Vector3.UnitX, [new(0, 0, 0), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0)]),
        (Vector3.UnitY, [new(0, 1, 1), new(1, 1, 1), new(1, 1, 0), new(0, 1, 0)]),
        (-Vector3.UnitY, [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)]),
        (Vector3.UnitZ, [new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)]),
        (-Vector3.UnitZ, [new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0)]),
    ];

    public static byte[] Write()
    {
        var material = new MaterialBuilder("carve")
            .WithMetallicRoughnessShader()
            .WithBaseColor(BaseColor)
            .WithMetallicRoughness(Metallic, Roughness)
            .WithDoubleSide(false);

        var mesh = new MeshBuilder<VertexPositionNormal>("cube");
        var primitive = mesh.UsePrimitive(material);
        foreach (var (normal, c) in Faces)
        {
            primitive.AddQuadrangle(
                new VertexPositionNormal(c[0], normal),
                new VertexPositionNormal(c[1], normal),
                new VertexPositionNormal(c[2], normal),
                new VertexPositionNormal(c[3], normal));
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity).WithName("cube");
        return scene.ToGltf2().WriteGLB().ToArray();
    }
}
