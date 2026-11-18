using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using SharpGLTF.Scenes;
using System.Text.Json.Nodes;

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

    public static byte[] Write() => Build().ToGltf2().WriteGLB().ToArray();

    /// <summary>The cube with one morph target, <c>Sink</c>: every vertex of the top face (y = 1) moves down 0.3 at weight 1, so the shape stays in the rest bounds the viewer frames.</summary>
    public static byte[] WriteWithSink()
    {
        var root = Build().ToGltf2();
        var mesh = root.LogicalMeshes[0];
        foreach (var primitive in mesh.Primitives)
        {
            var deltas = primitive.GetVertexAccessor("POSITION").AsVector3Array()
                .Select(p => p.Y > 0.5f ? new Vector3(0, -0.3f, 0) : Vector3.Zero).ToArray();
            var zeros = new Vector3[deltas.Length];
            primitive.SetMorphTargetAccessors(0, new Dictionary<string, Accessor>
            {
                ["POSITION"] = Floats(root, deltas),
                ["NORMAL"] = Floats(root, zeros),
            });
        }

        mesh.SetMorphWeights(new float[1]);
        mesh.Extras = new JsonObject { ["targetNames"] = new JsonArray("Sink") };
        return root.WriteGLB().ToArray();
    }

    private static Accessor Floats(ModelRoot root, Vector3[] values)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        var accessor = root.CreateAccessor();
        accessor.SetVertexData(root.UseBufferView(bytes, 0, null, 0, BufferMode.ARRAY_BUFFER), 0, values.Length, AttributeFormat.Float3);
        return accessor;
    }

    private static SceneBuilder Build()
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
        return scene;
    }
}
