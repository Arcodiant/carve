using Xunit;

namespace Carve.Core.Tests;

/// <summary>The not-planar fold (the person's rule, 2026-11-13): name 10, fold the rest.</summary>
[Trait("Tier", "unit")]
public class MeshWarningFoldTests
{
    private static MeshWarning Planar(int face) =>
        new(MeshWarningKind.NonPlanarFace, $"faces[{face}]", "face is not planar: a vertex is 0.1 off its plane (limit 0.001)");

    private static readonly MeshWarning Open = new(MeshWarningKind.OpenEdge, "edges[0-1]", "edge belongs to only one face");
    private static readonly MeshWarning Stray = new(MeshWarningKind.UnusedVertex, "vertices[3]", "no face uses this vertex");

    private static IReadOnlyList<string> Fold(params MeshWarning[] warnings) => MeshWarningFold.Lines(warnings);

    [Fact]
    public void No_warnings_make_no_lines() => Assert.Empty(Fold());

    [Fact]
    public void One_face_is_listed_as_before() =>
        Assert.Equal([Planar(4).ToString()], Fold(Planar(4)));

    [Fact]
    public void Ten_faces_are_each_listed_and_nothing_folds()
    {
        var warnings = Enumerable.Range(0, 10).Select(Planar).ToArray();
        Assert.Equal(warnings.Select(w => w.ToString()), Fold(warnings));
    }

    [Fact]
    public void Eleven_faces_name_ten_and_fold_one_in_the_singular()
    {
        var lines = Fold(Enumerable.Range(0, 11).Select(Planar).ToArray());
        Assert.Equal(11, lines.Count);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => Planar(i).ToString()), lines.Take(10));
        Assert.Equal("and 1 more face is not planar; select faces where non_planar names them", lines[10]);
    }

    [Fact]
    public void Many_faces_name_the_lowest_ten_and_count_the_rest()
    {
        var lines = Fold(Enumerable.Range(0, 504).Select(Planar).ToArray());
        Assert.Equal(11, lines.Count);
        Assert.Equal("and 494 more faces are not planar; select faces where non_planar names them", lines[^1]);
    }

    [Fact]
    public void The_named_faces_are_the_lowest_ids_whatever_the_order_given()
    {
        var lines = Fold(Enumerable.Range(0, 15).Reverse().Select(Planar).ToArray());
        Assert.Equal(Enumerable.Range(0, 10).Select(i => Planar(i).ToString()).Order(), lines.Take(10).Order());
        Assert.DoesNotContain(lines, l => l.StartsWith("faces[10]") || l.StartsWith("faces[14]"));
        Assert.Equal("and 5 more faces are not planar; select faces where non_planar names them", lines[^1]);
    }

    [Fact]
    public void Other_warnings_are_never_folded_and_the_fold_line_comes_last()
    {
        var warnings = new List<MeshWarning> { Open, Open with { Element = "edges[1-2]" } };
        warnings.AddRange(Enumerable.Range(0, 12).Select(Planar));
        warnings.Add(Stray);

        var lines = Fold([.. warnings]);

        Assert.Equal(2 + 10 + 1 + 1, lines.Count);
        Assert.Contains(Open.ToString(), lines);
        Assert.Contains(Stray.ToString(), lines);
        Assert.Equal("and 2 more faces are not planar; select faces where non_planar names them", lines[^1]);
    }
}
