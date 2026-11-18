using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>Face colours in the export (read back by the engine's parser), in the submeshes render draws, and through the store's undo.</summary>
[Trait("Tier", "unit")]
public class FaceColourTests
{
    private const string Red = "#C8503C";
    private const string Blue = "#3C78C8";

    /// <summary>The cube with faces 0 and 1 red, 2 blue and 3 to 5 uncoloured.</summary>
    private static PolygonMesh Coloured() => TestMeshes.Cube().WithStyle(
        FaceStyle.None.WithColour([0, 1], Red).WithColour([2], Blue));

    [Fact]
    public void The_export_has_a_material_per_colour_with_its_linear_base_colour_and_its_own_triangles()
    {
        var mesh = Coloured();
        var glb = GlbExport.Write(mesh, Shading.Triangles(mesh), "cube");
        var parsed = GlbExportTests.Parser.Parse(glb, ModelFormat.Gltf);

        var materials = parsed.SubmeshMaterials.Select(m => m!).ToArray();
        Assert.Equal(3, materials.Length);
        Assert.Equal(new Vector4(0.72f, 0.72f, 0.70f, 1f), materials[0].BaseColor); // the uncoloured faces, first
        Assert.Equal(FaceStyle.ToLinear(Red), materials[1].BaseColor);
        Assert.Equal(FaceStyle.ToLinear(Blue), materials[2].BaseColor);
        Assert.All(materials, m =>
        {
            Assert.True(m.Pbr);
            Assert.Equal(0f, m.Metallic);
            Assert.Equal(1f, m.Roughness);
            Assert.Equal(1f, m.BaseColor.W); // opaque
        });

        // A cube face is two triangles: three plain faces, two red, one blue.
        var ranges = parsed.MeshData.Submeshes!;
        Assert.Equal([3 * 6, 2 * 6, 1 * 6], ranges.Select(r => r.IndexCount));
    }

    [Fact]
    public void An_uncoloured_mesh_exports_as_it_always_did_with_one_material()
    {
        var mesh = TestMeshes.Cube();
        var triangles = Shading.Triangles(mesh);
        Assert.Equal(GlbExport.Write(triangles, "cube"), GlbExport.Write(mesh, triangles, "cube"));
        Assert.Equal(["carve"], GlbExport.MaterialNames(mesh));
    }

    [Fact]
    public void Hex_in_either_case_is_one_material()
    {
        var mesh = TestMeshes.Cube().WithStyle(FaceStyle.None.WithColour([0], "#c8503c").WithColour([1], "#C8503C"));
        Assert.Equal(["carve", "carve #C8503C"], GlbExport.MaterialNames(mesh));
    }

    [Fact]
    public void Render_cuts_the_triangles_into_a_submesh_per_colour_over_the_same_vertices()
    {
        var mesh = Coloured();
        var space = DrawnSpace.Of(Vector3.Zero, Vector3.One);
        var plain = MeshBridge.ToMeshData(mesh, space);
        var cut = MeshBridge.ToSubmeshedMeshData(mesh, space, out var colours, out _);

        Assert.Equal(new string?[] { null, Red.ToUpperInvariant(), Blue }, colours);
        Assert.Equal(plain.Vertices, cut.Vertices);
        Assert.Equal([18, 12, 6], cut.Submeshes!.Select(s => s.IndexCount));
        Assert.Equal([0, 18, 30], cut.Submeshes!.Select(s => s.StartIndex));
        Assert.Equal(plain.Indices!.Order(), cut.Indices!.Order()); // every triangle once
    }

    [Fact]
    public void Render_draws_each_colour_with_the_PBR_material_the_loader_makes_from_the_export()
    {
        // T-82: the preview shades as the game will. Each of render's submesh colours, in render's order, against the material
        // the engine's glTF parser reads from the exported file in the same submesh: every parameter the loader passes to
        // Material.Pbr is equal, and the material is the PBR path, not the classic lit one.
        var mesh = Coloured();
        MeshBridge.ToSubmeshedMeshData(mesh, DrawnSpace.Of(Vector3.Zero, Vector3.One), out var colours, out _);
        var parsed = GlbExportTests.Parser.Parse(GlbExport.Write(mesh, Shading.Triangles(mesh), "cube"), ModelFormat.Gltf)
            .SubmeshMaterials.Select(m => m!).ToArray();

        Assert.Equal(parsed.Length, colours.Length);
        foreach (var (colour, file) in colours.Zip(parsed))
        {
            var drawn = SurfaceMaterial.Of(colour);
            Assert.True(file.Pbr);
            Assert.True(drawn.Flags.HasFlag(MaterialFlags.Lit | MaterialFlags.Pbr), $"{colour ?? "grey"}: {drawn.Flags} is not lit PBR");
            Assert.Equal(file.BaseColor, drawn.Color);
            Assert.Equal(file.Metallic, drawn.Metallic);
            Assert.Equal(file.Roughness, drawn.Roughness);
            Assert.Equal(file.EmissiveColor, drawn.EmissiveColor);
            Assert.Equal(file.Occlusion, drawn.Occlusion);
            Assert.Equal(file.DoubleSided, drawn.DoubleSided);
            Assert.Null(file.TextureBytes);
            Assert.Null(drawn.Texture);
            Assert.Null(drawn.MetallicRoughnessTexture);
        }
    }

    [Fact]
    public void Colours_and_groups_step_back_with_undo_and_an_extrude_hands_them_to_its_walls()
    {
        var store = new ModelStore();
        store.Replace(TestMeshes.Cube());

        Assert.NotNull(store.Restyle("define_group", null, m => (FaceStyle.None.WithGroup("lid", [1]), []), out _, out _));
        Assert.NotNull(store.Restyle("set_colour", null, m => (m.Style!.WithColour([1], Red), []), out _, out _));
        store.Edit("extrude", m => MeshEdits.Extrude(m, [1], 0.5f), out var result);
        var extruded = store.Current.Model.Meshes[0].Mesh.Style!;
        Assert.Equal(Red, extruded.ColourOf(result!.Extruded[0].Walls[0]));
        Assert.Contains(result.Extruded[0].Walls[0], extruded.FacesOf("lid")!);

        Assert.Equal("extrude", store.Undo()!.Undone);
        var coloured = store.Current.Model.Meshes[0].Mesh.Style!;
        Assert.Equal([1], coloured.Colours.Keys);
        Assert.Equal([1], coloured.FacesOf("lid"));

        Assert.Equal("set_colour", store.Undo()!.Undone);
        Assert.Empty(store.Current.Model.Meshes[0].Mesh.Style!.Colours);

        Assert.Equal("define_group", store.Undo()!.Undone);
        Assert.Null(store.Current.Model.Meshes[0].Mesh.Style);
    }

    [Fact]
    public void A_refused_restyle_pushes_nothing()
    {
        var store = new ModelStore();
        store.Replace(TestMeshes.Cube());
        var version = store.Current.Version;
        Assert.Null(store.Restyle("set_colour", null, m => (null, ["no"]), out var refused, out _));
        Assert.Equal(["no"], refused);
        Assert.Equal(version, store.Current.Version);
    }

    [Fact]
    public void Defining_the_mesh_again_starts_it_uncoloured()
    {
        var store = new ModelStore();
        store.Replace(Coloured());
        store.Replace(TestMeshes.Cube());
        Assert.Null(store.Current.Model.Meshes[0].Mesh.Style);
    }
}
