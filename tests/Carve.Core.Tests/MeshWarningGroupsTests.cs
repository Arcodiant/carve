using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-197: one line per kind past 3 (the person's ruling, 2026-11-24), replacing T-72's "name 10, fold the rest".</summary>
[Trait("Tier", "unit")]
public class MeshWarningGroupsTests
{
    private static MeshWarning Planar(int face, double off = 0.1) =>
        new(MeshWarningKind.NonPlanarFace, $"faces[{face}]", FormattableString.Invariant($"face is not planar: a vertex is {off:G3} off its plane (limit 0.001)"), off, 0.001);

    private static MeshWarning Open(int a, int b) => new(MeshWarningKind.OpenEdge, $"edges[{a}-{b}]", "edge belongs to only one face");

    private static MeshWarning Stray(int v) => new(MeshWarningKind.UnusedVertex, $"vertices[{v}]", "no face uses this vertex");

    private static IReadOnlyList<string> Lines(params MeshWarning[] warnings) => MeshWarningGroups.Lines(warnings);

    [Fact]
    public void No_warnings_make_no_lines() => Assert.Empty(Lines());

    [Fact]
    public void One_face_is_listed_as_before() =>
        Assert.Equal(["faces[4]: face is not planar: a vertex is 0.1 off its plane (limit 0.001)"], Lines(Planar(4)));

    [Fact]
    public void Three_faces_are_each_listed_and_nothing_groups()
    {
        var warnings = Enumerable.Range(0, 3).Select(i => Planar(i)).ToArray();
        Assert.Equal(warnings.Select(w => w.ToString()), Lines(warnings));
    }

    [Fact]
    public void Four_faces_are_one_line_with_the_worst_three_samples_and_the_query()
    {
        var lines = Lines(Planar(0), Planar(1), Planar(2, 0.5), Planar(3));
        Assert.Equal(["4 faces are not planar: worst faces[2], 0.5 off its plane (limit 0.001); e.g. faces[0], faces[1], faces[2]; select faces where non_planar names them"], lines);
    }

    [Fact]
    public void Many_faces_are_one_line_counting_them_all()
    {
        var lines = Lines([.. Enumerable.Range(0, 504).Select(i => Planar(i))]);
        Assert.Equal(["504 faces are not planar: worst faces[0], 0.1 off its plane (limit 0.001); e.g. faces[0], faces[1], faces[2]; select faces where non_planar names them"], lines);
    }

    [Fact]
    public void The_samples_are_the_lowest_ids_whatever_the_order_given_and_a_tie_goes_to_the_lowest()
    {
        var lines = Lines([.. Enumerable.Range(0, 15).Reverse().Select(i => Planar(i + 10))]);
        Assert.Equal("15 faces are not planar: worst faces[10], 0.1 off its plane (limit 0.001); e.g. faces[10], faces[11], faces[12]; select faces where non_planar names them", Assert.Single(lines));
    }

    [Fact]
    public void The_worst_face_is_the_most_over_its_own_limit_not_the_farthest_off()
    {
        // faces[5] is farther off, but faces[7] is further over its (smaller) limit.
        MeshWarning[] warnings = [Planar(1), Planar(5, 0.4), Planar(6), Planar(7, 0.3) with { Limit = 0.0005 }];
        Assert.StartsWith("4 faces are not planar: worst faces[7], 0.3 off its plane (limit 0.0005); ", Assert.Single(Lines(warnings)));
    }

    [Fact]
    public void Edges_sort_by_both_ids_and_a_kind_without_a_measure_has_no_worst()
    {
        var lines = Lines(Open(1, 2), Open(0, 9), Open(10, 11), Open(0, 1));
        Assert.Equal(["4 edges belong to only one face: e.g. edges[0-1], edges[0-9], edges[1-2]; select edges where open names them"], lines);
    }

    [Fact]
    public void A_non_manifold_edge_kind_names_the_edge_with_the_most_faces()
    {
        MeshWarning Shared(int a, int n) => new(MeshWarningKind.NonManifoldEdge, $"edges[{a}-{a + 1}]", $"edge is shared by {n} faces", n);
        var line = Assert.Single(Lines(Shared(0, 3), Shared(2, 4), Shared(4, 5), Shared(6, 5)));
        Assert.Equal("4 edges are shared by more than two faces: worst edges[4-5], shared by 5 faces; e.g. edges[0-1], edges[2-3], edges[4-5]; select edges where nonmanifold names them", line);
    }

    [Fact]
    public void A_face_warned_twice_counts_once()
    {
        MeshWarning Overlap(int f, int g) => new(MeshWarningKind.UvOverlap, $"faces[{f}]", $"UVs overlap faces[{g}], so the two faces show the same texels");
        var line = Assert.Single(Lines(Overlap(3, 7), Overlap(3, 12), Overlap(7, 12), Overlap(12, 20), Overlap(20, 21)));
        Assert.Equal("4 faces have UVs that overlap another face: e.g. faces[3], faces[7], faces[12]", line);
    }

    [Fact]
    public void Two_kinds_group_each_where_its_first_warning_stood_and_a_short_kind_stays_listed()
    {
        MeshWarning[] warnings = [Open(0, 1), Planar(9), Open(1, 2), Open(2, 3), Stray(7), Open(3, 4), Planar(8), Planar(10), Planar(11), Stray(8)];
        Assert.Equal(
        [
            "4 edges belong to only one face: e.g. edges[0-1], edges[1-2], edges[2-3]; select edges where open names them",
            "4 faces are not planar: worst faces[8], 0.1 off its plane (limit 0.001); e.g. faces[8], faces[9], faces[10]; select faces where non_planar names them",
            "vertices[7]: no face uses this vertex",
            "vertices[8]: no face uses this vertex",
        ], Lines(warnings));
    }

    [Fact]
    public void Every_kind_has_a_grouped_line()
    {
        foreach (var kind in Enum.GetValues<MeshWarningKind>())
        {
            var line = Assert.Single(Lines([.. Enumerable.Range(0, 4).Select(i => new MeshWarning(kind, $"faces[{i}]", "m", 1, 1))]));
            Assert.StartsWith("4 ", line);
        }
    }
}
