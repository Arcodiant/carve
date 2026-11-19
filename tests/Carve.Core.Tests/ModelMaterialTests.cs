using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>Materials on the model and faces (T-74): definition order, colour and material clearing each other, and each edit carrying the assignment.</summary>
[Trait("Tier", "unit")]
public class ModelMaterialTests
{
    private const string Red = "#C8503C";

    private static readonly Material Wood = new("wood", Roughness: 0.8f);
    private static readonly Material Steel = new("steel", Metallic: 1f, Roughness: 0.3f);

    private static PolygonMesh WoodCube() =>
        TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial([1], "wood").WithColour([2], Red).WithGroup("lid", [1]));

    private static Model WoodModel() => Model.Empty.WithMaterial(Wood).WithMaterial(Steel).With("box", WoodCube());

    [Fact]
    public void Materials_keep_definition_order_and_a_replacement_keeps_its_place()
    {
        var model = Model.Empty.WithMaterial(Wood, out var first).WithMaterial(Steel);
        Assert.False(first);
        var darker = Wood with { BaseColour = "#402010" };
        var replaced = model.WithMaterial(darker, out var was);
        Assert.True(was);
        Assert.Equal(["wood", "steel"], replaced.Materials.Select(m => m.Name));
        Assert.Equal(darker, replaced.FindMaterial("wood"));
        Assert.Equal(Wood, model.FindMaterial("wood")); // the model it was given is unchanged
        Assert.Null(replaced.FindMaterial("glass"));
    }

    [Fact]
    public void A_material_with_a_problem_is_refused()
    {
        var e = Assert.Throws<ArgumentException>(() => Model.Empty.WithMaterial(new Material("m", Roughness: 2f)));
        Assert.StartsWith("roughness: ", e.Message);
        Assert.Throws<ArgumentException>(() => Model.Empty.WithMaterial(new Material("carve")));
    }

    [Fact]
    public void Every_change_to_the_model_keeps_its_materials()
    {
        var model = WoodModel();
        var skeleton = Skeleton.Create([new BoneSpec("root", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        Assert.Equal(model.Materials, model.With("other", TestMeshes.Cube()).Materials);
        Assert.Equal(model.Materials, model.WithSkeleton(skeleton).Materials);
        Assert.Equal(model.Materials, model.WithSkeleton(skeleton).WithClips([]).Materials);
        Assert.Equal(model.Materials, model.WithoutClip("none").Materials);
    }

    [Fact]
    public void A_mesh_using_an_undefined_material_is_refused()
    {
        var e = Assert.Throws<ArgumentException>(() => Model.Empty.With("box", WoodCube()));
        Assert.Contains("'wood'", e.Message);
    }

    [Fact]
    public void A_material_clears_the_faces_colour_and_a_colour_clears_their_material()
    {
        var style = FaceStyle.None.WithColour([0, 1, 2], Red).WithMaterial([1, 3], "wood");
        Assert.Equal(Red, style.ColourOf(0));
        Assert.Null(style.ColourOf(1));
        Assert.Equal("wood", style.MaterialOf(1));
        Assert.Equal(Red, style.ColourOf(2)); // a face not named is untouched
        Assert.Equal("wood", style.MaterialOf(3));

        var back = style.WithColour([3], "#3C78C8");
        Assert.Null(back.MaterialOf(3));
        Assert.Equal("#3C78C8", back.ColourOf(3));
        Assert.Equal("wood", back.MaterialOf(1)); // a face not named is untouched
        Assert.Equal([1], back.Materials.Keys);
    }

    [Fact]
    public void A_style_with_only_a_material_is_not_empty()
    {
        Assert.False(FaceStyle.None.WithMaterial([0], "wood").IsEmpty);
        Assert.NotNull(TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial([0], "wood")).Style);
        Assert.Throws<ArgumentException>(() => FaceStyle.None.WithMaterial([0], "a:b"));
    }

    [Fact]
    public void Move_keeps_the_assignment()
    {
        var moved = TestMeshes.Ok(MeshEdits.Move(WoodCube(), [0], null, null, new Vector3(0, 0, -0.1f)));
        Assert.Equal("wood", moved.Style!.MaterialOf(1));
        Assert.Equal(Red, moved.Style.ColourOf(2));
    }

    [Fact]
    public void Extrude_gives_the_cap_and_walls_their_source_faces_material()
    {
        var result = MeshEdits.Extrude(WoodCube(), [1, 2], 0.5f);
        var style = TestMeshes.Ok(result).Style!;
        Assert.Equal("wood", style.MaterialOf(1));
        foreach (var x in result.Extruded)
            Assert.All(x.Walls, w =>
            {
                Assert.Equal(x.Face == 1 ? "wood" : null, style.MaterialOf(w));
                Assert.Equal(x.Face == 2 ? Red : null, style.ColourOf(w));
            });
        Assert.Equal(1 + 4, style.Materials.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Subdivide_gives_every_child_its_parents_material(int levels)
    {
        var mesh = TestMeshes.Ok(MeshEdits.Subdivide(WoodCube(), levels));
        var style = mesh.Style!;
        Assert.Equal((int)Math.Pow(4, levels), style.Materials.Count); // face 1's children, and nothing else
        Assert.Equal(style.Colours.Count, style.Materials.Count);
        Assert.Empty(style.Materials.Keys.Intersect(style.Colours.Keys));
        Assert.Equal(style.FacesOf("lid"), style.Materials.Keys.ToArray());
    }

    [Fact]
    public void Loop_cut_gives_both_halves_the_material()
    {
        var cube = TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial(TestMeshes.AllCubeFaces, "wood"));
        var mesh = TestMeshes.Ok(LoopCut.Cut(cube, (0, 3), [0.5]).Edit);
        Assert.Equal(mesh.LiveFaceCount, mesh.Style!.Materials.Count);
        Assert.All(mesh.Style.Materials.Values, v => Assert.Equal("wood", v));
    }

    [Fact]
    public void Delete_drops_a_deleted_faces_material()
    {
        var mesh = TestMeshes.Ok(MeshDelete.Delete(WoodCube(), null, [1]));
        Assert.Null(mesh.Style!.MaterialOf(1));
        Assert.Empty(mesh.Style.Materials);
    }

    [Fact]
    public void An_undo_snapshot_holds_the_materials_and_assignment_it_was_taken_with()
    {
        var before = WoodModel();
        var after = before.WithMaterial(Wood with { Metallic = 1f })
            .With("box", WoodCube().WithStyle(WoodCube().Style!.WithColour([1], Red)));
        Assert.Equal("wood", before.Find("box")!.Style!.MaterialOf(1));
        Assert.Equal(Wood, before.FindMaterial("wood"));
        Assert.Null(after.Find("box")!.Style!.MaterialOf(1));
        Assert.Equal(1f, after.FindMaterial("wood")!.Metallic);
    }

    [Fact]
    public void Combined_offsets_each_meshs_materials_with_its_faces()
    {
        var model = WoodModel().With("lid", TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial([0], "steel")));
        var style = model.Combined!.Style!;
        Assert.Equal("wood", style.MaterialOf(1));
        Assert.Equal(Red, style.ColourOf(2));
        Assert.Equal("steel", style.MaterialOf(6));
        Assert.Equal(2, style.Materials.Count);
    }

    [Fact]
    public void Combined_of_a_model_without_materials_is_unchanged()
    {
        var model = Model.Of("a", TestMeshes.Cube().WithStyle(FaceStyle.None.WithColour([0], Red))).With("b", TestMeshes.Cube());
        var style = model.Combined!.Style!;
        Assert.Empty(style.Materials);
        Assert.Equal([0], style.Colours.Keys);
        Assert.Null(Model.Of("a", TestMeshes.Cube()).With("b", TestMeshes.Cube()).Combined!.Style);
    }

    [Fact]
    public void Posed_keeps_the_materials_and_the_assignment()
    {
        var skeleton = Skeleton.Create([new BoneSpec("root", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        // Render draws the posed mesh with the model's own materials (RenderPump), so the assignment must still name them.
        var model = WoodModel().WithSkeleton(skeleton);
        var posed = Skinning.PosedOf(model, Pose.Rest, null, null)!;
        Assert.Equal(["wood", "steel"], model.Materials.Select(m => m.Name));
        Assert.Equal("wood", posed.Style!.MaterialOf(1));
        Assert.Equal("wood", Skinning.PosedOf(model, Pose.Rest, null, "box")!.Style!.MaterialOf(1));
    }

    [Fact]
    public void Create_checks_each_material_their_names_and_every_faces_material()
    {
        var (model, errors) = Model.Create([new NamedMesh("box", WoodCube())], null, [], [Wood, Steel, new Material("wood"), new Material("x", Metallic: -1)]);
        Assert.Null(model);
        Assert.Equal(["materials[2].name", "materials[3].metallic"], errors.Select(e => e.Element));

        (model, errors) = Model.Create([new NamedMesh("box", WoodCube())], null, [], [Steel]);
        Assert.Null(model);
        Assert.Equal("meshes[0].materials", Assert.Single(errors).Element);

        (model, errors) = Model.Create([new NamedMesh("box", WoodCube())], null, [], [Wood, Steel]);
        Assert.Empty(errors);
        Assert.Equal(["wood", "steel"], model!.Materials.Select(m => m.Name));
    }

    [Fact]
    public void A_piece_keeps_only_the_materials_its_faces_use()
    {
        var model = Model.Empty.WithMaterial(Steel).WithMaterial(Wood).WithMaterial(new Material("glass"))
            .With("box", WoodCube()).With("plain", TestMeshes.Cube());
        Assert.Equal(["wood"], model.PartPiece("box", out _)!.Materials.Select(m => m.Name));
        Assert.Empty(model.PartPiece("plain", out _)!.Materials);
    }

    [Fact]
    public void Opening_one_mesh_brings_its_materials_and_refuses_one_defined_differently()
    {
        var source = WoodModel();
        var into = Model.Empty.WithMaterial(Steel);
        var opened = into.WithMeshFrom(source, "box", out var problem)!;
        Assert.Null(problem);
        Assert.Equal(["steel", "wood"], opened.Materials.Select(m => m.Name)); // the undefined one is added last, the unused one stays out

        var same = Model.Empty.WithMaterial(Wood).WithMeshFrom(source, "box", out problem)!;
        Assert.Null(problem);
        Assert.Equal([Wood], same.Materials);

        var clash = Model.Empty.WithMaterial(Wood with { Roughness = 0.1f });
        Assert.Null(clash.WithMeshFrom(source, "box", out problem));
        Assert.Equal("mesh: the file's mesh 'box' uses the material 'wood', which the model defines differently; "
            + "define one of them again under another name, or open the whole file", problem);
    }
}
