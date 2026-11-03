using System.Globalization;
using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>RV-10: what an edit's refusal names, and that it names all of it.</summary>
[Trait("Tier", "unit")]
public class EditRefusalTests
{
    /// <summary>The unit square in the XY plane, facing +z: one open face.</summary>
    private static PolygonMesh Quad() =>
        Make([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [[0, 1, 2, 3]]);

    private static void AssertRefused(EditResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.Mesh);
        Assert.Empty(result.MovedVertices);
        Assert.Empty(result.NewVertices);
        Assert.Empty(result.NewFaces);
        Assert.Empty(result.Extruded);
    }

    [Fact]
    public void A_move_names_what_the_result_would_break_beside_an_unknown_id()
    {
        var result = MeshEdits.Move(Quad(), [2, 9], null, null, new Vector3(-1, -1, 0));

        AssertRefused(result);
        Assert.Equal(["vertices[9]", "faces[0]"], result.Errors.Select(e => e.Element));
    }

    [Fact]
    public void A_move_that_folds_a_face_is_refused_naming_the_faces_it_breaks()
    {
        var cube = Cube();
        var result = MeshEdits.Move(cube, [6], null, null, new Vector3(-2, 0, 0));

        AssertRefused(result);
        Assert.Equal(["faces[1]", "faces[3]"], result.Errors.Select(e => e.Element));
        Assert.All(result.Errors, e => Assert.Contains("self-intersecting", e.Message));
        Assert.Equal(CubeVertices, cube.Vertices);
    }

    [Fact]
    public void A_move_is_checked_as_a_whole_not_corner_by_corner()
    {
        var cube = Cube();
        var by = new Vector3(-2, 0, 0);

        // Corner 6 alone, past the far side, makes bow-ties; the whole +x face, all four corners at once, does not.
        Assert.False(MeshEdits.Move(cube, [6], null, null, by).Succeeded);
        Ok(MeshEdits.Move(cube, null, null, [5], by));
    }

    [Fact]
    public void A_move_too_small_to_change_any_coordinate_is_refused_like_a_zero_move()
    {
        var result = MeshEdits.Move(Cube(), [1], null, null, new Vector3(1e-10f, 0, 0));

        AssertRefused(result);
        var error = Assert.Single(result.Errors);
        Assert.Equal("by", error.Element);
        Assert.Equal("the move is zero: nothing would change", error.Message);
    }

    [Fact]
    public void A_move_small_but_changing_one_coordinate_goes_through()
    {
        // Vertex 0 sits at the origin, where 1e-10 is not lost; vertex 1, at x = 1, does not change.
        var moved = Ok(MeshEdits.Move(Cube(), [0, 1], null, null, new Vector3(1e-10f, 0, 0)));
        Assert.Equal(1e-10f, moved.Vertices[0].X);
    }

    [Theory]
    [InlineData(1e-30f, "the distance 1E-30 is too small for this face")]
    [InlineData(3e38f, "the distance 3E+38 is too large for this face")]
    public void A_wall_the_distance_breaks_is_named_by_the_face_it_was_made_for(float distance, string says)
    {
        var result = MeshEdits.Extrude(Quad(), [0], distance);

        AssertRefused(result);
        Assert.Equal(["faces[0]", "faces[0]", "faces[0]", "faces[0]"], result.Errors.Select(e => e.Element));
        for (var k = 0; k < 4; k++)
        {
            var message = result.Errors[k].Message;
            Assert.StartsWith(says, message);
            Assert.Contains($"its new wall faces[{k + 1}], over the edge from corner {k} to corner {(k + 1) % 4}, is refused: face has zero area", message);
        }
    }

    [Fact]
    public void An_extrude_names_a_bad_wall_beside_an_unknown_face()
    {
        var result = MeshEdits.Extrude(Quad(), [5, 0], 1e-30f);

        AssertRefused(result);
        Assert.Equal(["faces[5]", "faces[0]", "faces[0]", "faces[0]", "faces[0]"], result.Errors.Select(e => e.Element));
    }

    [Fact]
    public void An_extrude_names_one_face_too_small_for_its_inset_beside_an_unknown_face()
    {
        var result = MeshEdits.Extrude(Cube(), [9, 1], 0, 0.6f);

        AssertRefused(result);
        Assert.Equal(["faces[9]", "faces[1]"], result.Errors.Select(e => e.Element));
    }

    [Fact]
    public void Numbers_and_signs_in_edit_text_do_not_follow_the_current_culture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NegativeSign = "~";
        var was = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            var cube = Cube();
            Assert.Contains("the inset 0.6 is too large", Assert.Single(MeshEdits.Extrude(cube, [1], 0, 0.6f).Errors).Message);
            Assert.Contains("(-0.5)", Assert.Single(MeshEdits.Extrude(cube, [1], 1, -0.5f).Errors).Message);
            var unknown = Assert.Single(MeshEdits.Move(cube, [-1], null, null, Vector3.UnitX).Errors);
            Assert.Equal("vertices[-1]: the 1st listed vertex: there is no vertex -1: ids run 0..7", unknown.ToString());
            Assert.Contains("index -1 is out of range 0..2", Assert.Single(PolygonMesh.Create([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, -1]]).Errors).Message);

            var bent = Ok(MeshEdits.Move(cube, [6], null, null, new Vector3(0, 0.25f, 0)));
            var warning = MeshReport.Create(bent).Warnings.First(w => w.Kind == MeshWarningKind.NonPlanarFace);
            Assert.DoesNotContain(",", warning.Message);
            Assert.Matches(@"is \d\.\d+ off its plane \(limit \d", warning.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }
}
