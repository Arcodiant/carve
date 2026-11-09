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

    /// <summary>The colour <c>render</c> draws the surface in: <c>RenderSystem</c> takes its surface colour from here, so the game sees what Carve shows.</summary>
    public static readonly Vector4 BaseColor = new(0.72f, 0.72f, 0.70f, 1f);

    /// <summary>Set explicitly: glTF's default material is fully metallic.</summary>
    public const float Metallic = 0f;

    public const float Roughness = 1f;

    /// <summary>The materials a mesh exports as, in the order their primitives are written: the grey one first when any face has no colour.</summary>
    public static string[] MaterialNames(PolygonMesh mesh) => [.. Materials(mesh, FlatShading.Triangles(mesh)).Select(m => m.Name)];

    private static List<(string Name, string? Colour)> Materials(PolygonMesh? mesh, IReadOnlyList<FlatTriangle> triangles)
    {
        var colours = triangles.Select(t => ColourOf(mesh, t.Face)).Distinct().ToList();
        return colours.OrderBy(c => c is null ? 0 : 1)
            .Select(c => (c is null ? MaterialName : $"{MaterialName} {c}", c)).ToList();
    }

    /// <summary>A face's colour as an upper-case hex, or null for an uncoloured face or no mesh.</summary>
    internal static string? ColourOf(PolygonMesh? mesh, int face) => mesh?.Style?.ColourOf(face)?.ToUpperInvariant();

    /// <summary>The opaque matte material of one colour (D-6): metallic 0, roughness 1, single-sided; no colour is the default grey.</summary>
    internal static MaterialBuilder Material(string name, string? colour) => new MaterialBuilder(name)
        .WithMetallicRoughnessShader()
        .WithBaseColor(colour is null ? BaseColor : FaceStyle.ToLinear(colour))
        .WithMetallicRoughness(Metallic, Roughness)
        .WithDoubleSide(false);

    /// <summary>One opaque matte material per colour, metallic 0 and roughness 1, its base colour the linear form of the sRGB hex (D-6).</summary>
    public static byte[] Write(IReadOnlyList<FlatTriangle> triangles, string name) => Write(null, triangles, name);

    /// <summary>As above, with the colours of <paramref name="mesh"/>.</summary>
    public static byte[] Write(PolygonMesh? mesh, IReadOnlyList<FlatTriangle> triangles, string name)
    {
        var mesh2 = new MeshBuilder<VertexPositionNormal>(name);
        var primitives = new Dictionary<string, PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexEmpty>>();
        foreach (var (materialName, colour) in Materials(mesh, triangles))
            primitives[materialName] = mesh2.UsePrimitive(Material(materialName, colour));

        foreach (var t in triangles)
        {
            var colour = ColourOf(mesh, t.Face);
            var primitive = primitives[colour is null ? MaterialName : $"{MaterialName} {colour}"];
            primitive.AddTriangle(
                new VertexPositionNormal(t.A, t.Normal),
                new VertexPositionNormal(t.B, t.Normal),
                new VertexPositionNormal(t.C, t.Normal));
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh2, Matrix4x4.Identity).WithName(name);
        return scene.ToGltf2().WriteGLB().ToArray();
    }
}
