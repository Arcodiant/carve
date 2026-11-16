using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>check's material warnings and merge_faces' wording for a material (T-75).</summary>
[Trait("Tier", "unit")]
public class MaterialHealthTests
{
    private static readonly Vector3[] Vertices = [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(0, 1, 0), new(1, 1, 0), new(2, 1, 0)];
    private static readonly int[][] Faces = [[0, 1, 4, 3], [1, 2, 5, 4]];

    private static MaterialTexture Texture() => MaterialTexture.FromPng(Path.Combine(Path.GetTempPath(), "t.png"), TestPngs.Png(2, 2), out _)!;

    private static readonly Material Textured = new("wood", BaseColourTexture: Texture());
    private static readonly Material Plain = new("paint", BaseColour: "#3C78C8");

    private static PolygonMesh Mesh(params (int Face, string Material)[] assign) =>
        TestMeshes.Make(Vertices, Faces).WithStyle(assign.Aggregate(FaceStyle.None, (s, a) => s.WithMaterial([a.Face], a.Material)));

    /// <summary>The face and material warnings; the two squares are an open strip, so every edge warns too, which is not this test's concern.</summary>
    private static string[] Warnings(PolygonMesh mesh, params Material[] materials) =>
        [.. MeshHealth.Create(mesh, materials).Warnings.Select(w => w.ToString()).Where(w => w.StartsWith("faces[") || w.StartsWith("materials["))];

    private const string Text = "material 'wood' has a texture but the face has no UVs, so it samples the texture's corner";

    [Fact]
    public void A_textured_face_with_no_UVs_in_a_mesh_with_none_gets_the_material_warning_and_a_plain_one_gets_nothing()
    {
        var mesh = Mesh((0, "wood"), (1, "paint"));
        Assert.Equal([$"faces[0]: {Text}"], Warnings(mesh, Textured, Plain));
        Assert.Empty(Warnings(mesh, new Material("wood"), Plain));
        Assert.Null(MeshHealth.Create(mesh, [Textured, Plain]).Stats.Uvs);
    }

    [Fact]
    public void In_a_partly_mapped_mesh_the_material_warning_replaces_the_generic_one_for_that_face_only()
    {
        var uvs = MeshUvs.None.WithFaces([KeyValuePair.Create(0, (IReadOnlyList<Vector2>?)new[] { new Vector2(0, 0), new(0, 1), new(1, 1), new(1, 0) })]);
        var mesh = Mesh((1, "wood")).WithUvs(uvs);
        Assert.Equal([$"faces[1]: {Text}"], Warnings(mesh, Textured));

        var other = Mesh((1, "paint")).WithUvs(uvs);
        Assert.Equal(["faces[1]: no UVs, so the face samples the texture's corner"], Warnings(other, Textured, Plain));
    }

    [Fact]
    public void A_mapped_textured_face_has_no_material_warning()
    {
        var uvs = MeshUvs.None.WithFaces(
        [
            KeyValuePair.Create(0, (IReadOnlyList<Vector2>?)new[] { new Vector2(0, 0), new(0, 1), new(0.5f, 1), new(0.5f, 0) }),
            KeyValuePair.Create(1, (IReadOnlyList<Vector2>?)new[] { new Vector2(0.5f, 0), new(0.5f, 1), new(1, 1), new(1, 0) }),
        ]);
        Assert.Empty(Warnings(Mesh((0, "wood"), (1, "wood")).WithUvs(uvs), Textured));
    }

    [Fact]
    public void Check_without_materials_is_what_it_always_was()
    {
        var mesh = TestMeshes.Make(Vertices, Faces);
        Assert.Equal(Warnings(mesh), Warnings(mesh, Textured));
        Assert.Equal([.. MeshHealth.Create(mesh).Warnings], [.. MeshHealth.Create(mesh, []).Warnings]);
    }

    [Theory]
    [InlineData(0.5f, true)]
    [InlineData(1f, false)]
    public void An_occlusion_strength_with_no_map_warns_once_for_a_material_the_mesh_uses(float occlusion, bool warns)
    {
        var material = new Material("wood", Occlusion: occlusion);
        var warnings = Warnings(Mesh((0, "wood")), material);
        var expected = $"materials['wood']: occlusion is {occlusion} but the material has no occlusion_texture, so it has no effect (glTF scales only the map)";
        Assert.Equal(warns ? new[] { expected } : System.Array.Empty<string>(), warnings);
        // A material no face uses is not this mesh's concern.
        Assert.Empty(Warnings(TestMeshes.Make(Vertices, Faces), material));
        // With the map the strength counts (the face is warned of as textured, not of the occlusion).
        Assert.DoesNotContain(Warnings(Mesh((0, "wood")), material with { OcclusionTexture = Texture() }), w => w.StartsWith("materials["));
    }

    [Fact]
    public void Merge_faces_names_a_material_not_colour_none_and_keeps_the_old_text_for_colours()
    {
        var mesh = Mesh((1, "wood"));
        FaceMerge.Merge(mesh, [0, 1], out var warnings);
        Assert.Equal(["faces[1]: merged with material 'wood', and the merged face keeps faces[0]'s look, no colour or material"], warnings);

        FaceMerge.Merge(Mesh((0, "paint"), (1, "wood")), [0, 1], out var both);
        Assert.Equal(["faces[1]: merged with material 'wood', and the merged face keeps faces[0]'s look, material 'paint'"], both);

        FaceMerge.Merge(Mesh((0, "paint")).WithStyle(FaceStyle.None.WithMaterial([0], "paint").WithColour([1], "#C8503C")), [0, 1], out var colour);
        Assert.Equal(["faces[1]: merged with colour #C8503C, and the merged face keeps faces[0]'s look, material 'paint'"], colour);

        FaceMerge.Merge(Mesh((0, "wood"), (1, "wood")), [0, 1], out var same);
        Assert.Empty(same);

        FaceMerge.Merge(TestMeshes.Make(Vertices, Faces).WithStyle(FaceStyle.None.WithColour([1], "#C8503C")), [0, 1], out var old);
        Assert.Equal(["faces[1]: merged with colour #C8503C, and the merged face keeps faces[0]'s colour, none"], old);
    }
}
