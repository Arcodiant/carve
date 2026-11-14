using System.Numerics;
using Xunit;
using static Carve.Core.Tests.Uv;

namespace Carve.Core.Tests;

/// <summary>How every edit carries UVs and seams (T-53 and the person's rulings of 2026-11-13).</summary>
[Trait("Tier", "unit")]
public class UvCarryTests
{
    private static PolygonMesh Ok(EditResult r) => TestMeshes.Ok(r);

    /// <summary>Every mapped face's corners still equal their positions projected: what any interpolation of a projection gives.</summary>
    private static void StillProjected(PolygonMesh m, float tolerance = 0)
    {
        Assert.NotNull(m.Uvs);
        foreach (var f in m.Uvs.MappedFaces)
        {
            var expected = ProjectedCorners(m, f);
            var actual = m.Uvs.Of(f)!;
            for (var c = 0; c < expected.Length; c++)
                Assert.True(Vector2.Distance(expected[c], actual[c]) <= tolerance, $"face {f} corner {c}: expected {expected[c]}, got {actual[c]}");
        }
    }

    [Fact]
    public void Move_keeps_every_uv_bit_exact()
    {
        var cube = MappedCube();
        var moved = Ok(MeshEdits.Move(cube, [6], null, null, new Vector3(0.3f, 0.2f, 0.1f)));
        BitEqual(cube.Uvs, moved.Uvs);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    public void Extrude_keeps_the_caps_uvs_by_corner_and_leaves_the_walls_unmapped(float inset)
    {
        var cube = MappedCube();
        var result = MeshEdits.Extrude(cube, [1], 0.5f, inset);
        var m = Ok(result);
        var x = Assert.Single(result.Extruded);
        BitEqual(cube.Uvs!.Of(1)!, m.Uvs!.Of(x.Cap));
        Assert.All(x.Walls, w => Assert.False(m.Uvs.IsMapped(w)));
        foreach (var f in new[] { 0, 2, 3, 4, 5 }) BitEqual(cube.Uvs.Of(f)!, m.Uvs.Of(f));
        Assert.Equal(cube.Seams, m.Seams);
    }

    [Fact]
    public void Extrude_of_a_holed_face_keeps_all_eight_corner_uvs()
    {
        var plate = Mapped(HoledPlate());
        var result = MeshEdits.Extrude(plate, [0], 0.25f);
        var m = Ok(result);
        Assert.Equal(8, m.Uvs!.Of(0)!.Count);
        BitEqual(plate.Uvs!.Of(0)!, m.Uvs.Of(0));
        Assert.Equal([0], m.Uvs.MappedFaces);
    }

    [Fact]
    public void Subdivide_a_unit_quad_gives_linear_uvs_worked_by_hand()
    {
        var quad = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [[0, 1, 2, 3]])
            .WithUvs(MeshUvs.None.WithFace(0, Square));
        var m = Ok(MeshEdits.Subdivide(quad, 1));
        Assert.Equal([0, 1, 2, 3], m.Uvs!.MappedFaces);
        BitEqual([V(0, 0), V(0.5f, 0), V(0.5f, 0.5f), V(0, 0.5f)], m.Uvs.Of(0));
        BitEqual([V(1, 0), V(1, 0.5f), V(0.5f, 0.5f), V(0.5f, 0)], m.Uvs.Of(1));
        BitEqual([V(1, 1), V(0.5f, 1), V(0.5f, 0.5f), V(1, 0.5f)], m.Uvs.Of(2));
        BitEqual([V(0, 1), V(0, 0.5f), V(0.5f, 0.5f), V(0.5f, 1)], m.Uvs.Of(3));
    }

    [Fact]
    public void Subdivide_uses_the_linear_rule_not_the_smoothing_stencil()
    {
        // On a closed cube the vertex points move inward, but a corner keeps its UV and a midpoint takes the mean.
        var m = Ok(MeshEdits.Subdivide(MappedCube(), 1));
        var u = MappedCube().Uvs!.Of(0)!;
        var child = m.Uvs!.Of(0)!;
        BitEqual([u[0], (u[0] + u[1]) / 2, (u[0] + u[1] + u[2] + u[3]) / 4, (u[3] + u[0]) / 2], child);
        Assert.NotEqual(TestMeshes.Cube().Vertices[0], m.Vertices[0]);
    }

    [Fact]
    public void An_unmapped_face_gives_unmapped_children_and_two_levels_equal_one_twice()
    {
        var cube = TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(2, Square).WithSeams([(0, 1)], true));
        var once = Ok(MeshEdits.Subdivide(cube, 1));
        Assert.Equal(4, once.Uvs!.MappedFaces.Count);
        Assert.Contains(2, once.Uvs.MappedFaces);
        Assert.All(once.Uvs.MappedFaces.Where(f => f != 2), f => Assert.True(f >= 6));
        var twice = Ok(MeshEdits.Subdivide(once, 1));
        var two = Ok(MeshEdits.Subdivide(cube, 2));
        BitEqual(twice.Uvs, two.Uvs);
        Assert.Equal(16, two.Uvs!.MappedFaces.Count);
    }

    [Fact]
    public void Subdivide_puts_a_seam_on_both_halves()
    {
        var cube = TestMeshes.Cube().WithUvs(MeshUvs.None.WithSeams([(0, 1)], true));
        var m = Ok(MeshEdits.Subdivide(cube, 1));
        // The edge point of 0-1 is the vertex both halves share; it is not the midpoint, as the edge is smooth.
        Assert.Equal(2, m.Seams.Count);
        var (a, b) = (m.Seams[0], m.Seams[1]);
        Assert.Equal((0, 1, a.Hi), (a.Lo, b.Lo, b.Hi));
        Assert.True(a.Hi >= 8 && m.Topology.TryGetEdge(0, a.Hi, out _) && m.Topology.TryGetEdge(1, a.Hi, out _));
    }

    [Fact]
    public void Delete_drops_the_uvs_of_deleted_faces_and_the_seams_no_face_has()
    {
        var cube = MappedCube();
        var m = Ok(MeshDelete.Delete(cube, null, [3, 5]));
        Assert.Equal([0, 1, 2, 4], m.Uvs!.MappedFaces);
        Assert.Equal([(0, 1)], m.Seams); // 2-6 was an edge of faces 3 and 5 only
    }

    [Fact]
    public void Plane_cut_both_interpolates_the_pieces_per_face()
    {
        var cube = MappedCube();
        var result = PlaneCut.Cut(cube, new Vector3(0, 0.3f, 0), Vector3.UnitY);
        var m = Ok(result.Edit);
        Assert.NotEmpty(result.Edit.Split);
        foreach (var s in result.Edit.Split) Assert.All(s.Pieces, f => Assert.True(m.Uvs!.IsMapped(f)));
        StillProjected(m, 1e-6f);
        // A new vertex on the edge 0-3 has a different UV in each face on it: the texture stays split there.
        var v = Enumerable.Range(8, m.Vertices.Count - 8).Single(i => m.Vertices[i].X == 0 && m.Vertices[i].Z == 0);
        var uses = m.LiveFaces.Where(f => m.Faces[f].Corners.Contains(v)).Select(f => m.Uvs!.Of(f)![m.Faces[f].CornerOf(v)]).Distinct().ToArray();
        Assert.Equal(2, uses.Length);
    }

    [Fact]
    public void Plane_cut_above_leaves_the_cap_unmapped()
    {
        var result = PlaneCut.Cut(MappedCube(), new Vector3(0, 0.3f, 0), Vector3.UnitY, PlaneKeep.Above);
        var m = Ok(result.Edit);
        Assert.NotEmpty(result.Edit.NewFaces);
        var caps = result.Edit.NewFaces.Where(f => !m.IsFaceDeleted(f) && !result.Edit.Split.Any(s => s.Pieces.Contains(f))).ToArray();
        Assert.NotEmpty(caps);
        Assert.All(caps, f => Assert.False(m.Uvs!.IsMapped(f)));
        Assert.DoesNotContain(m.Uvs!.MappedFaces, m.IsFaceDeleted);
        StillProjected(m, 1e-6f);
        Assert.Empty(MeshUvs.Check(m, m.Uvs));
    }

    [Fact]
    public void Loop_cut_at_a_quarter_gives_exact_values()
    {
        var result = LoopCut.Cut(MappedCube(), (0, 1), [0.25]);
        var m = Ok(result.Edit);
        StillProjected(m);
        Assert.Contains(m.Uvs!.MappedFaces, f => m.Uvs.Of(f)!.Contains(V(0.25f, 0)));
    }

    [Fact]
    public void Knife_gives_an_interior_point_its_barycentric_uv()
    {
        var result = Knife.Cut(MappedCube(), [new(0.25f, 0.25f, 1), new(0.75f, 0.25f, 1), new(0.5f, 0.75f, 1)], true);
        var m = Ok(result.Edit);
        Assert.Contains(result.Edit.NewVertices, v => m.Vertices[v] == new Vector3(0.25f, 0.25f, 1));
        StillProjected(m, 1e-6f);
    }

    [Fact]
    public void Merge_keeps_uvs_that_agree_and_drops_with_a_warning_those_that_do_not()
    {
        var cut = Ok(LoopCut.Cut(MappedCube(), (0, 1), [0.5]).Edit);
        var pieces = cut.LiveFaces.Where(f => Normal(cut, f).Z < 0).ToArray();
        Assert.Equal(2, pieces.Length);

        var merged = FaceMerge.Merge(cut, pieces, out var warnings);
        var m = Ok(merged);
        Assert.Empty(warnings);
        Assert.True(m.Uvs!.IsMapped(pieces.Min()));
        StillProjected(m);

        var shifted = cut.WithUvs(cut.Uvs!.WithFace(pieces.Max(), cut.Uvs.Of(pieces.Max())!.Select(u => u + V(0.5f, 0)).ToArray()));
        var split = Ok(FaceMerge.Merge(shifted, pieces, out var dropped));
        Assert.False(split.Uvs!.IsMapped(pieces.Min()));
        Assert.Contains(dropped, w => w.StartsWith($"faces[{pieces.Min()}]:") && w.Contains("UVs") && w.Contains("dropped"));

        var half = cut.WithUvs(cut.Uvs!.WithFace(pieces.Max(), null));
        Ok(FaceMerge.Merge(half, pieces, out var unmapped));
        Assert.Contains(unmapped, w => w.StartsWith($"faces[{pieces.Max()}]: it has no UVs"));
    }

    [Fact]
    public void Bridge_quads_are_unmapped()
    {
        var ends = Ok(MeshDelete.Delete(MappedCube(), null, [2, 3, 4, 5]));
        var result = Bridge.Join(ends, [0, 1, 2, 3], [4, 5, 6, 7]);
        var m = Ok(result);
        Assert.Equal(4, result.NewFaces.Count);
        Assert.All(result.NewFaces, f => Assert.False(m.Uvs!.IsMapped(f)));
        Assert.Equal([0, 1], m.Uvs!.MappedFaces);
    }

    public static TheoryData<string> SeamCuts => new() { "plane_cut", "loop_cut", "knife" };

    /// <summary>
    /// Ruling 4 (RV-43): a seam a cut splits keeps the mark on every piece. Each cut crosses one of the mapped cube's two
    /// seams, 0-1 or 2-6; the crossed seam's whole edge is gone and every piece of it between its two ends is a seam, and the
    /// seam the cut misses is kept as it was.
    /// </summary>
    [Theory]
    [MemberData(nameof(SeamCuts))]
    public void A_seam_a_cut_splits_keeps_the_mark_on_every_piece(string cut)
    {
        var cube = MappedCube();
        var (m, crossed, missed) = cut switch
        {
            "plane_cut" => (Ok(PlaneCut.Cut(cube, new Vector3(0.4f, 0, 0), Vector3.UnitX).Edit), (0, 1), (2, 6)),
            "loop_cut" => (Ok(LoopCut.Cut(cube, (0, 4), [0.3]).Edit), (2, 6), (0, 1)),
            _ => (Ok(Knife.Cut(cube, [new(0.4f, 0, 0), new(0.4f, 0, 1)]).Edit), (0, 1), (2, 6)),
        };
        Assert.Contains(missed, m.Seams);
        Assert.DoesNotContain(crossed, m.Seams);
        var (a, b) = (m.Vertices[crossed.Item1], m.Vertices[crossed.Item2]);
        // The pieces: the new vertices on the open segment a-b, in order from a.
        var inner = Enumerable.Range(8, m.Vertices.Count - 8)
            .Where(v => Vector3.Cross(m.Vertices[v] - a, b - a).Length() < 1e-6f && Vector3.Dot(m.Vertices[v] - a, b - a) > 0 && Vector3.Dot(m.Vertices[v] - b, a - b) > 0)
            .OrderBy(v => Vector3.Distance(m.Vertices[v], a)).ToArray();
        Assert.NotEmpty(inner);
        var path = inner.Prepend(crossed.Item1).Append(crossed.Item2).ToArray();
        for (var i = 0; i + 1 < path.Length; i++)
        {
            var piece = (Math.Min(path[i], path[i + 1]), Math.Max(path[i], path[i + 1]));
            Assert.True(m.Topology.TryGetEdge(piece.Item1, piece.Item2, out _), $"{cut}: {piece} is not an edge");
            Assert.Contains(piece, m.Seams);
        }
        Assert.Equal(path.Length, m.Seams.Count); // the pieces and the missed seam, nothing else
    }

    public static TheoryData<string> Edits => new() { "move", "extrude", "subdivide", "delete", "plane_cut", "loop_cut", "knife", "merge_faces", "bridge" };

    [Theory]
    [MemberData(nameof(Edits))]
    public void Every_edit_leaves_uvs_that_fit_the_mesh(string edit)
    {
        var cube = MappedCube();
        var m = edit switch
        {
            "move" => Ok(MeshEdits.Move(cube, [6], null, null, Vector3.One)),
            "extrude" => Ok(MeshEdits.Extrude(cube, [1, 3], 0.5f, 0.1f)),
            "subdivide" => Ok(MeshEdits.Subdivide(cube, 2)),
            "delete" => Ok(MeshDelete.Delete(cube, [6], null)),
            "plane_cut" => Ok(PlaneCut.Cut(cube, new Vector3(0.4f, 0.4f, 0.4f), new Vector3(1, 2, 3)).Edit),
            "loop_cut" => Ok(LoopCut.Cut(cube, (0, 4), [0.2, 0.7]).Edit),
            "knife" => Ok(Knife.Cut(cube, [new(0, 0.3f, 1), new(0.6f, 0.5f, 1), new(1, 0.6f, 1)]).Edit),
            "merge_faces" => MergedBack(LoopCut.Cut(cube, (0, 1), [0.5]).Edit),
            _ => Ok(Bridge.Join(Ok(MeshDelete.Delete(cube, null, [2, 3, 4, 5])), [0, 1, 2, 3], [4, 5, 6, 7])),
        };
        Assert.NotNull(m.Uvs);
        Assert.Empty(MeshUvs.Check(m, m.Uvs));
    }

    private static PolygonMesh MergedBack(EditResult cut) =>
        Ok(FaceMerge.Merge(Ok(cut), cut.Split.Single(s => s.Face == 0).Pieces, out _));

    private static PolygonMesh MappedCube() => Mapped(TestMeshes.Cube(), (0, 1), (2, 6));

    private static PolygonMesh HoledPlate()
    {
        Vector3[] plate =
        [
            new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0),
            new(-0.5f, -0.5f, 0), new(-0.5f, 0.5f, 0), new(0.5f, 0.5f, 0), new(0.5f, -0.5f, 0),
        ];
        return PolygonMesh.Create(plate, [new Face([0, 1, 2, 3], [[4, 5, 6, 7]])]).Mesh!;
    }
}
