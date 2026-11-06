using System.Numerics;
using Carve.Core;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace Carve.Server;

/// <summary>
/// The mesh as a self-contained binary glTF: one scene, one node with an identity transform and one mesh, both named
/// for the file; one indexed triangle primitive of <c>POSITION</c> and <c>NORMAL</c> alone, from
/// <see cref="FlatShading"/>, with every position the mesh's own float (no scale, axis change or recentring) and every
/// normal its face's, so every edge is hard. glTF winds counter-clockwise from outside, as Carve does, so the
/// triangles are written as they are. One matte material. The same mesh always writes the same bytes.
/// </summary>
public static class GlbExport
{
    public const string MaterialName = "carve";

    /// <summary>The colour <c>render</c> draws the surface in (RenderSystem's), so the game sees what Carve shows.</summary>
    public static readonly Vector4 BaseColor = new(0.72f, 0.72f, 0.70f, 1f);

    /// <summary>Set explicitly: glTF's default material is fully metallic.</summary>
    public const float Metallic = 0f;

    public const float Roughness = 1f;

    public static byte[] Write(IReadOnlyList<FlatTriangle> triangles, string name)
    {
        var material = new MaterialBuilder(MaterialName)
            .WithMetallicRoughnessShader()
            .WithBaseColor(BaseColor)
            .WithMetallicRoughness(Metallic, Roughness)
            .WithDoubleSide(false);

        var mesh = new MeshBuilder<VertexPositionNormal>(name);
        var primitive = mesh.UsePrimitive(material);
        foreach (var t in triangles)
        {
            primitive.AddTriangle(
                new VertexPositionNormal(t.A, t.Normal),
                new VertexPositionNormal(t.B, t.Normal),
                new VertexPositionNormal(t.C, t.Normal));
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity).WithName(name);
        return scene.ToGltf2().WriteGLB().ToArray();
    }
}
