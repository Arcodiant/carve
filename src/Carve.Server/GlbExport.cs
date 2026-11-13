using System.Globalization;
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
/// <see cref="Shading"/>, with every position the mesh's own float (no scale, axis change or recentring) and every
/// normal its corner's: its face's on a flat mesh, so every edge is hard. glTF winds counter-clockwise from outside, as Carve does, so the
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
    public static string[] MaterialNames(PolygonMesh mesh) => [.. Materials(mesh, Shading.Triangles(mesh)).Select(m => m.Name)];

    private static List<(string Name, string? Colour)> Materials(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles)
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
    public static byte[] Write(IReadOnlyList<ShadedTriangle> triangles, string name) => Write(null, triangles, name);

    /// <summary>True when any live face of <paramref name="mesh"/> has UVs: only then is <c>TEXCOORD_0</c> written.</summary>
    internal static bool HasUvs(PolygonMesh? mesh) => mesh?.Uvs is { } uvs && mesh.LiveFaces.Any(uvs.IsMapped);

    /// <summary>The UV of corner <paramref name="vertex"/> of <paramref name="face"/>: its own, or (0, 0) for an unmapped face.</summary>
    internal static Vector2 UvAt(PolygonMesh mesh, int face, int vertex) =>
        mesh.Uvs?.Of(face) is { } uv ? uv[mesh.Faces[face].CornerOf(vertex)] : Vector2.Zero;

    /// <summary>
    /// The warning for each piece with faces that have no UVs while the export carries UVs, or none when no piece has any.
    /// The text is <c>&lt;mesh&gt;: N faces have no UVs; their corners are written as (0, 0)</c>, then the first face ids.
    /// </summary>
    public static IReadOnlyList<string> UvWarnings(Model model)
    {
        if (!model.Meshes.Any(m => HasUvs(m.Mesh))) return [];
        var warnings = new List<string>();
        foreach (var (name, mesh) in model.Meshes)
        {
            var bare = mesh.LiveFaces.Where(f => mesh.Uvs?.IsMapped(f) != true).ToList();
            if (bare.Count == 0) continue;
            var shown = string.Join(", ", bare.Take(UvWarningIds));
            warnings.Add(bare.Count == 1
                ? $"{name}: 1 face has no UVs; its corners are written as (0, 0) (face {shown})"
                : $"{name}: {bare.Count.ToString(CultureInfo.InvariantCulture)} faces have no UVs; their corners are written as (0, 0) (faces {shown}"
                    + (bare.Count > UvWarningIds ? $" and {(bare.Count - UvWarningIds).ToString(CultureInfo.InvariantCulture)} more)" : ")"));
        }
        return warnings;
    }

    private const int UvWarningIds = 5;

    /// <summary>As above, with the colours of <paramref name="mesh"/>.</summary>
    public static byte[] Write(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles, string name) =>
        HasUvs(mesh)
            ? Write<VertexTexture1>(mesh, triangles, name, (m, i, v) => new VertexTexture1(UvAt(m!, i, v)), true)
            : Write<VertexEmpty>(mesh, triangles, name, (_, _, _) => default, false);

    private static byte[] Write<TvM>(PolygonMesh? mesh, IReadOnlyList<ShadedTriangle> triangles, string name,
        Func<PolygonMesh?, int, int, TvM> texture, bool uvs)
        where TvM : struct, IVertexMaterial
    {
        var mesh2 = new MeshBuilder<VertexPositionNormal, TvM, VertexEmpty>(name);
        var primitives = new Dictionary<string, PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, TvM, VertexEmpty>>();
        foreach (var (materialName, colour) in Materials(mesh, triangles))
            primitives[materialName] = mesh2.UsePrimitive(Material(materialName, colour));

        // Shading.Triangles is in Triangulator order, so the ids of a triangle's vertices (and so its corners) are read from there.
        var ids = uvs ? Triangulator.Triangulate(mesh!) : null;
        for (var i = 0; i < triangles.Count; i++)
        {
            var t = triangles[i];
            var colour = ColourOf(mesh, t.Face);
            var primitive = primitives[colour is null ? MaterialName : $"{MaterialName} {colour}"];
            var id = ids?[i] ?? default;
            primitive.AddTriangle(
                new VertexBuilder<VertexPositionNormal, TvM, VertexEmpty>(new VertexPositionNormal(t.A, t.NormalA), uvs ? texture(mesh, t.Face, id.A) : default),
                new VertexBuilder<VertexPositionNormal, TvM, VertexEmpty>(new VertexPositionNormal(t.B, t.NormalB), uvs ? texture(mesh, t.Face, id.B) : default),
                new VertexBuilder<VertexPositionNormal, TvM, VertexEmpty>(new VertexPositionNormal(t.C, t.NormalC), uvs ? texture(mesh, t.Face, id.C) : default));
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh2, Matrix4x4.Identity).WithName(name);
        return scene.ToGltf2().WriteGLB().ToArray();
    }
}
