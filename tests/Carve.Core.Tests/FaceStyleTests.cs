using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>Per-face colour and named groups (D-6, Colour is a material): the sRGB to linear conversion, the hex check, and what an edit does to them.</summary>
[Trait("Tier", "unit")]
public class FaceStyleTests
{
    private const string Red = "#C8503C";
    private const string Blue = "#3C78C8";

    [Theory]
    [InlineData("#000000", 0f, 0f, 0f)]
    [InlineData("#FFFFFF", 1f, 1f, 1f)]
    [InlineData("#FF0000", 1f, 0f, 0f)]
    [InlineData("#808080", 0.21586f, 0.21586f, 0.21586f)] // 128/255 through the sRGB curve
    [InlineData("#0A0A0A", 0.003035f, 0.003035f, 0.003035f)] // 10/255 is on the straight part, v / 12.92
    [InlineData("#C8503C", 0.57758f, 0.08022f, 0.04519f)]
    [InlineData("#c8503c", 0.57758f, 0.08022f, 0.04519f)] // digits in either case
    public void An_sRGB_hex_converts_to_linear_by_the_sRGB_curve(string hex, float r, float g, float b)
    {
        var linear = FaceStyle.ToLinear(hex);
        Assert.Equal(r, linear.X, 4);
        Assert.Equal(g, linear.Y, 4);
        Assert.Equal(b, linear.Z, 4);
        Assert.Equal(1f, linear.W);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C8503C")]
    [InlineData("#C8503")]
    [InlineData("#C8503CC")]
    [InlineData("#C8503G")]
    [InlineData("#C850 3")]
    [InlineData("red")]
    [InlineData("rgb(1,2,3)")]
    public void A_malformed_hex_is_refused(string? text)
    {
        Assert.NotNull(FaceStyle.ColourProblem(text));
        Assert.Throws<ArgumentException>(() => FaceStyle.ToLinear(text!));
    }

    [Fact]
    public void A_well_formed_hex_is_not_a_problem()
    {
        Assert.Null(FaceStyle.ColourProblem(Red));
        Assert.Null(FaceStyle.ColourProblem("#abcdef"));
    }

    [Fact]
    public void A_colour_is_kept_as_given()
    {
        var style = FaceStyle.None.WithColour([1], "#c8503c");
        Assert.Equal("#c8503c", style.ColourOf(1));
        Assert.Null(style.ColourOf(0));
    }

    [Fact]
    public void A_mesh_with_an_empty_style_has_none()
    {
        Assert.Null(TestMeshes.Cube().WithStyle(FaceStyle.None).Style);
    }

    private static PolygonMesh StyledCube() => TestMeshes.Cube().WithStyle(
        FaceStyle.None.WithColour([1], Red).WithColour([2, 3], Blue).WithGroup("lid", [1]).WithGroup("sides", [2, 3]));

    [Fact]
    public void Walls_and_caps_of_an_extrude_take_the_source_faces_colour_and_groups()
    {
        var cube = StyledCube();
        var result = MeshEdits.Extrude(cube, [1, 2], 0.5f);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var style = result.Mesh!.Style!;

        Assert.Equal(Red, style.ColourOf(1)); // the cap keeps its id and its colour
        Assert.Equal(Blue, style.ColourOf(2));
        foreach (var extruded in result.Extruded)
        {
            var expected = extruded.Face == 1 ? Red : Blue;
            Assert.All(extruded.Walls, wall => Assert.Equal(expected, style.ColourOf(wall)));
        }

        Assert.Equal([1, .. result.Extruded[0].Walls], style.FacesOf("lid"));
        Assert.Equal([2, 3, .. result.Extruded[1].Walls], style.FacesOf("sides"));
        Assert.Equal(3 + 8, style.Colours.Count); // three coloured faces and eight walls
        Assert.Equal(Red, cube.Style!.ColourOf(1));
        Assert.Equal(3, cube.Style.Colours.Count); // the mesh the edit was given is unchanged
    }

    [Fact]
    public void An_uncoloured_face_extrudes_to_uncoloured_walls()
    {
        var result = MeshEdits.Extrude(StyledCube(), [0], 0.5f);
        var style = result.Mesh!.Style!;
        Assert.All(result.Extruded[0].Walls, wall => Assert.Null(style.ColourOf(wall)));
        Assert.Equal(3, style.Colours.Count);
    }

    [Fact]
    public void A_move_keeps_every_colour_and_group()
    {
        var result = MeshEdits.Move(StyledCube(), [0], null, null, new Vector3(0, 0, -0.1f));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(Red, result.Mesh!.Style!.ColourOf(1));
        Assert.Equal([2, 3], result.Mesh.Style.FacesOf("sides"));
    }

    [Fact]
    public void An_unstyled_mesh_stays_unstyled_through_an_edit()
    {
        Assert.Null(MeshEdits.Extrude(TestMeshes.Cube(), [1], 0.5f).Mesh!.Style);
    }

    [Fact]
    public void Redefining_a_group_replaces_it_in_place()
    {
        var style = FaceStyle.None.WithGroup("a", [3, 1, 1]).WithGroup("b", [2]).WithGroup("a", [5]);
        Assert.Equal(["a", "b"], style.Groups.Select(g => g.Name));
        Assert.Equal([5], style.FacesOf("a"));
        Assert.Null(style.FacesOf("c"));
        Assert.Equal([1, 3], FaceStyle.None.WithGroup("a", [3, 1, 1]).FacesOf("a"));
    }

    [Fact]
    public void The_combined_mesh_carries_each_meshs_colours_at_its_offset()
    {
        var model = Model.Of("a", StyledCube()).With("b", TestMeshes.Cube().WithStyle(FaceStyle.None.WithColour([4], Blue)));
        var combined = model.Combined!;
        Assert.Equal(Red, combined.Style!.ColourOf(1));
        Assert.Equal(Blue, combined.Style.ColourOf(6 + 4));
        Assert.Equal(4, combined.Style.Colours.Count);
    }

    [Fact]
    public void A_model_of_one_mesh_combines_to_that_mesh_with_its_style()
    {
        var cube = StyledCube();
        Assert.Same(cube, Model.Of("a", cube).Combined);
    }
}
