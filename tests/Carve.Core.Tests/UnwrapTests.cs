using Xunit;
using System.Numerics;
using Carve.Core;

namespace Carve.Core.Tests;

/// <summary>unwrap's kernel (T-56): islands, LSCM, orientation and packing.</summary>
public class UnwrapTests
{
    private static UnwrapResult Ok((UnwrapResult? Result, IReadOnlyList<MeshError> Errors) run)
    {
        Assert.True(run.Result is not null, string.Join("; ", run.Errors.Select(e => $"{e.Element}: {e.Message}")));
        return run.Result!;
    }

    /// <summary>Twice the signed area of a face's outer loop in UV; negative is unflipped (corners counterclockwise on screen, v down).</summary>
    private static double Shoelace(PolygonMesh mesh, int face)
    {
        var uv = mesh.Uvs!.Of(face)!;
        var n = mesh.Faces[face].Loop.Count;
        var sum = 0.0;
        for (var c = 0; c < n; c++)
        {
            Vector2 a = uv[c], b = uv[(c + 1) % n];
            sum += (double)a.X * b.Y - (double)b.X * a.Y;
        }
        return sum;
    }

    [Fact]
    public void A_flat_grid_comes_back_as_itself_up_to_a_rigid_motion()
    {
        var grid = TestMeshes.Grid(4, 3);
        var (islands, errors) = Unwrap.Flatten(grid);
        Assert.Empty(errors);
        var island = Assert.Single(islands!);
        Assert.Equal(12, island.Faces.Count);

        // Every pair of corners keeps its 3D distance (an isometry), and the faces are unflipped (no mirror):
        // so the flattening is the grid moved rigidly. 1e-9 of the grid's size (1.25 m diagonal), in double.
        var points = new Dictionary<int, (double U, double V)>();
        for (var f = 0; f < island.Faces.Count; f++)
            for (var c = 0; c < 4; c++) points[grid.Faces[island.Faces[f]].Corners[c]] = island.Corners[f][c];
        var worst = 0.0;
        foreach (var (a, pa) in points)
            foreach (var (b, pb) in points)
            {
                double dx = (double)grid.Vertices[a].X - grid.Vertices[b].X, dy = (double)grid.Vertices[a].Y - grid.Vertices[b].Y;
                var d3 = Math.Sqrt(dx * dx + dy * dy);
                var d2 = Math.Sqrt((pa.U - pb.U) * (pa.U - pb.U) + (pa.V - pb.V) * (pa.V - pb.V));
                worst = Math.Max(worst, Math.Abs(d2 - d3));
            }
        Assert.True(worst <= 1e-9 * 1.25, $"largest distance error {worst:R}");

        var result = Ok(Unwrap.Run(grid));
        var only = Assert.Single(result.Islands);
        Assert.True(only.Stretch <= 1 + 1e-9, $"stretch {only.Stretch:R}");
        Assert.Equal(0, only.Flipped);
        Assert.All(Enumerable.Range(0, 12), f => Assert.True(Shoelace(result.Mesh, f) < 0, $"faces[{f}] is flipped"));

        // After packing and the float round, UV / scale is still the grid within 1e-6 of its size.
        worst = 0;
        var uvAt = new Dictionary<int, Vector2>();
        for (var f = 0; f < 12; f++)
            for (var c = 0; c < 4; c++) uvAt[grid.Faces[f].Corners[c]] = result.Mesh.Uvs!.Of(f)![c];
        foreach (var (a, pa) in uvAt)
            foreach (var (b, pb) in uvAt)
            {
                var d3 = Vector3.Distance(grid.Vertices[a], grid.Vertices[b]);
                var d2 = Vector2.Distance(pa, pb) / result.Scale;
                worst = Math.Max(worst, Math.Abs(d2 - d3));
            }
        Assert.True(worst <= 1e-6 * 1.25, $"largest distance error after packing {worst:R}");
    }

    [Fact]
    public void A_bent_strip_unfolds_with_stretch_near_one()
    {
        var result = Ok(Unwrap.Run(TestMeshes.BentStrip()));
        var island = Assert.Single(result.Islands);
        Assert.Equal(4, island.Faces.Count);
        Assert.True(island.Stretch <= 1 + 1e-6, $"stretch {island.Stretch:R}");
        Assert.Equal(0, island.Flipped);
        // Unfolded it is a 2 m x 0.5 m strip, laid wider than tall.
        Assert.Equal(4.0, (island.Rect.U1 - island.Rect.U0) / (island.Rect.V1 - island.Rect.V0), 4);
    }

    [Fact]
    public void A_tube_with_no_seam_is_refused_naming_its_loops()
    {
        var tube = TestMeshes.Tube();
        var (result, errors) = Unwrap.Run(tube);
        Assert.Null(result);
        var error = Assert.Single(errors);
        Assert.Equal("faces[0]", error.Element);
        Assert.Equal("an island of 32 faces with 2 boundary loops is not a disc; mark a seam across it with mark_edges, cutting every closed loop until each island is a disc", error.Message);
    }

    [Fact]
    public void A_closed_cube_with_no_seam_is_refused_naming_its_loops()
    {
        var (result, errors) = Unwrap.Run(TestMeshes.Cube());
        Assert.Null(result);
        var error = Assert.Single(errors);
        Assert.Equal("faces[0]", error.Element);
        Assert.Contains("an island of 6 faces with 0 boundary loops is not a disc", error.Message);
        Assert.Contains("mark_edges", error.Message);
    }

    [Fact]
    public void A_holed_face_is_one_island_with_its_hole_as_an_extra_loop()
    {
        var plate = PolygonMesh.Create(
            [new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(0, 4, 0), new(1, 1, 0), new(1, 3, 0), new(3, 3, 0), new(3, 1, 0)],
            [new Face([0, 3, 2, 1], [[4, 7, 6, 5]])], null).Mesh!;
        var result = Ok(Unwrap.Run(plate));
        var island = Assert.Single(result.Islands);
        Assert.True(island.Stretch <= 1 + 1e-6, $"stretch {island.Stretch:R}");
        Assert.Equal(8, result.Mesh.Uvs!.Of(0)!.Count);
        Assert.True(Shoelace(result.Mesh, 0) < 0);
    }

    [Fact]
    public void Two_runs_are_bit_identical()
    {
        var first = Ok(Unwrap.Run(TestMeshes.SeamedBarrel()));
        var second = Ok(Unwrap.Run(TestMeshes.SeamedBarrel()));
        for (var f = 0; f < 34; f++)
            Assert.Equal(first.Mesh.Uvs!.Of(f)!.SelectMany(p => new[] { BitConverter.SingleToInt32Bits(p.X), BitConverter.SingleToInt32Bits(p.Y) }),
                second.Mesh.Uvs!.Of(f)!.SelectMany(p => new[] { BitConverter.SingleToInt32Bits(p.X), BitConverter.SingleToInt32Bits(p.Y) }));
        Assert.Equal(BitConverter.DoubleToInt64Bits(first.Fill), BitConverter.DoubleToInt64Bits(second.Fill));
        Assert.Equal(BitConverter.DoubleToInt64Bits(first.Scale), BitConverter.DoubleToInt64Bits(second.Scale));
    }

    [Fact]
    public void The_barrel_gives_three_islands_apart_unflipped_and_inside()
    {
        var result = Ok(Unwrap.Run(TestMeshes.SeamedBarrel()));
        Assert.Equal([(32, 0), (1, 32), (1, 33)], result.Islands.Select(i => (i.Faces.Count, i.LowestFace)));
        Assert.Equal(Enumerable.Range(0, 32), result.Islands[0].Faces);
        Assert.Equal(1.0 / 64, result.Margin);
        Assert.All(result.Islands, i => Assert.Equal(0, i.Flipped));
        Assert.All(result.Islands, i => Assert.InRange(i.Iterations, 0, Unwrap.MaxIterations - 1));
        Assert.All(Enumerable.Range(0, 34), f => Assert.True(Shoelace(result.Mesh, f) < 0, $"faces[{f}] is flipped"));
        Assert.All(Enumerable.Range(0, 34), f => Assert.All(result.Mesh.Uvs!.Of(f)!, p =>
        {
            Assert.InRange(p.X, 1f / 64, 1 - 1f / 64);
            Assert.InRange(p.Y, 1f / 64, 1 - 1f / 64);
        }));

        // Rectangles apart by at least the margin, along one axis or the other.
        for (var a = 0; a < 3; a++)
            for (var b = a + 1; b < 3; b++)
            {
                var (ra, rb) = (result.Islands[a].Rect, result.Islands[b].Rect);
                var gap = Math.Max(Math.Max(rb.U0 - ra.U1, ra.U0 - rb.U1), Math.Max(rb.V0 - ra.V1, ra.V0 - rb.V1));
                Assert.True(gap >= 1.0 / 64 - 1e-6, $"islands {a} and {b} are {gap} apart");
            }

        // The side is curved, so it stretches a little; the flat caps not at all. Real-area rescale: the caps' UV
        // area over the side's matches their 3D areas' ratio.
        Assert.InRange(result.Islands[0].Stretch, 1, 1.5);
        Assert.True(result.Islands[1].Stretch <= 1 + 1e-6 && result.Islands[2].Stretch <= 1 + 1e-6);
        double Area(int face) => Math.Abs(Shoelace(result.Mesh, face)) / 2;
        var side = Enumerable.Range(0, 32).Sum(Area);
        var area3 = Enumerable.Range(0, 32).Sum(f => Area3(result.Mesh, f));
        Assert.Equal(area3 / Area3(result.Mesh, 32), side / Area(32), 3);
        Assert.InRange(result.Fill, 0.3, 1);
    }

    private static double Area3(PolygonMesh mesh, int face)
    {
        var sum = 0.0;
        foreach (var t in Triangulator.TriangulateFace(mesh, face))
            sum += Vector3.Cross(mesh.Vertices[t.B] - mesh.Vertices[t.A], mesh.Vertices[t.C] - mesh.Vertices[t.A]).Length() / 2;
        return sum;
    }

    [Fact]
    public void Faces_outside_the_set_keep_their_uvs_bit_for_bit()
    {
        var mapped = Ok(Unwrap.Run(TestMeshes.SeamedBarrel(), null, (0, 0, 0.5, 0.5))).Mesh;
        var partial = Ok(Unwrap.Run(mapped, [32, 33])).Mesh;
        for (var f = 0; f < 32; f++) Assert.Equal(mapped.Uvs!.Of(f), partial.Uvs!.Of(f));
        Assert.NotEqual(mapped.Uvs!.Of(32), partial.Uvs!.Of(32));
        Assert.Equal(mapped.Seams, partial.Seams);
    }

    [Fact]
    public void A_partial_set_is_cut_at_its_border()
    {
        // The side alone, with the vertical seam: a disc, though the barrel's caps are joined to it in the mesh.
        var result = Ok(Unwrap.Run(TestMeshes.Barrel().WithUvs(MeshUvs.None.WithSeams(TestMeshes.BarrelSeams.Take(4), true)), [.. Enumerable.Range(0, 32)]));
        Assert.Equal(32, Assert.Single(result.Islands).Faces.Count);
        Assert.False(result.Mesh.Uvs!.IsMapped(32));
    }

    // Two unit quads in z = 0 either side of the edge 1-2 at x = 1, and a third standing up on it at z 0..1.
    private static readonly Vector3[] Fin =
    [
        new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(2, 1, 0), new(2, 0, 0), new(1, 0, 1), new(1, 1, 1),
    ];

    /// <summary>
    /// Ruling 2 (RV-43): an edge whose two faces run it the same way (one of them mis-wound) is cut, so the two quads are two
    /// islands; wound to agree, the same quads are one.
    /// </summary>
    [Fact]
    public void A_mis_wound_edge_is_cut_into_two_islands()
    {
        var agreeing = Ok(Unwrap.Run(TestMeshes.Make(Fin, [[0, 1, 2, 3], [5, 4, 2, 1]])));
        Assert.Equal([0, 1], Assert.Single(agreeing.Islands).Faces);

        var misWound = TestMeshes.Make(Fin, [[0, 1, 2, 3], [1, 2, 4, 5]]);
        Assert.True(misWound.Topology.TryGetEdge(1, 2, out var edge) && edge.Faces.Count == 2 && !edge.IsManifold);
        var result = Ok(Unwrap.Run(misWound));
        Assert.Equal([[0], [1]], result.Islands.Select(i => i.Faces.ToArray()));
    }

    /// <summary>Ruling 2 (RV-43): an edge of three or more faces is cut, so a fin of three quads on one edge is three islands.</summary>
    [Fact]
    public void An_edge_of_three_faces_is_cut_into_an_island_each()
    {
        var fin = TestMeshes.Make(Fin, [[0, 1, 2, 3], [5, 4, 2, 1], [1, 6, 7, 2]]);
        Assert.True(fin.Topology.TryGetEdge(1, 2, out var edge) && edge.Faces.Count == 3);
        var result = Ok(Unwrap.Run(fin));
        Assert.Equal([[0], [1], [2]], result.Islands.Select(i => i.Faces.ToArray()));
    }

    /// <summary>
    /// The did-not-converge refusal (RV-43), reached through unwrap itself: a strip of 150 unit-high quads whose widths grow by
    /// 5% each is ill-conditioned enough that CG is still at a relative residual of about 1.7e-8 (against 1e-10) when it
    /// reaches <see cref="Unwrap.MaxIterations"/>. The cap and tolerance are constants with no seam to lower them, so the
    /// mesh is built to need more; it was found by trying strips (one of 1000 equal quads fails too, but takes seconds).
    /// </summary>
    [Fact]
    public void An_island_cg_cannot_settle_is_refused_as_not_converging()
    {
        const int n = 150;
        var vertices = new List<Vector3>();
        double x = 0, width = 1;
        for (var i = 0; i <= n; i++)
        {
            vertices.Add(new((float)x, 0, 0));
            vertices.Add(new((float)x, 1, 0));
            x += width;
            width *= 1.05;
        }
        var faces = Enumerable.Range(0, n).Select(i => new[] { 2 * i, 2 * i + 2, 2 * i + 3, 2 * i + 1 });
        var (result, errors) = Unwrap.Run(TestMeshes.Make(vertices, faces));
        Assert.Null(result);
        var error = Assert.Single(errors);
        Assert.Equal("faces[0]", error.Element);
        Assert.StartsWith("the flattening did not converge (relative residual ", error.Message);
        Assert.EndsWith(" after 10000 iterations; the limit is 1E-10 within 10000); mark seams to cut the island into smaller ones", error.Message);
    }

    /// <summary>RV-43, the person's ruling (2026-11-14): a 1 m cube slit along one edge passes the disc test but collapses.</summary>
    [Fact]
    public void A_cube_with_one_seam_is_refused_as_having_too_few_seams_not_no_extent()
    {
        var (result, errors) = Unwrap.Run(TestMeshes.Cube().WithUvs(MeshUvs.None.WithSeams([(0, 1)], true)));
        Assert.Null(result);
        Assert.Equal("faces[0]: the island of 6 faces has too few seams to lay flat; mark more with mark_edges seam, e.g. a path round it",
            Assert.Single(errors).ToString());
    }

    /// <summary>
    /// A truly degenerate island, its corners on one line, keeps the no-extent refusal. define_mesh refuses a face of zero
    /// area, so the triangle is made whole and then laid on a line with the unchecked <see cref="PolygonMesh.WithVertices(IReadOnlyList{Vector3})"/>.
    /// </summary>
    [Fact]
    public void An_island_on_one_line_is_refused_as_having_no_extent()
    {
        var line = TestMeshes.Make([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 1, 0)], [[0, 1, 2]])
            .WithVertices([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0)]);
        var (result, errors) = Unwrap.Run(line);
        Assert.Null(result);
        Assert.Equal("faces[0]: the island of 1 face has no extent: its corners all lie on one point or one line, so it cannot be flattened",
            Assert.Single(errors).ToString());
    }

    [Theory]
    [InlineData(new[] { 40 }, "faces[40]", "face 40 is not a face of the mesh, which has 34")]
    [InlineData(new[] { 3, 3 }, "faces[3]", "face 3 is listed more than once")]
    [InlineData(new int[0], "faces", "the list is empty")]
    public void Bad_faces_are_named(int[] faces, string element, string message)
    {
        var (result, errors) = Unwrap.Run(TestMeshes.SeamedBarrel(), faces);
        Assert.Null(result);
        Assert.Contains(errors, e => e.Element == element && e.Message.StartsWith(message, StringComparison.Ordinal));
    }

    [Fact]
    public void Bad_into_and_margin_are_named()
    {
        Assert.Equal("into", Assert.Single(Unwrap.Run(TestMeshes.SeamedBarrel(), null, (0, 0, 0, 1)).Errors).Element);
        Assert.Equal("into", Assert.Single(Unwrap.Run(TestMeshes.SeamedBarrel(), null, (0, 0, double.NaN, 1)).Errors).Element);
        Assert.Equal("margin", Assert.Single(Unwrap.Run(TestMeshes.SeamedBarrel(), null, null, -0.1).Errors).Element);
        Assert.Equal("margin", Assert.Single(Unwrap.Run(TestMeshes.SeamedBarrel(), null, null, 0.5).Errors).Element);
    }

    [Fact]
    public void A_given_into_and_margin_are_kept()
    {
        var result = Ok(Unwrap.Run(TestMeshes.SeamedBarrel(), null, (0.5, 0.25, 1.5, 0.75), 0.01));
        Assert.Equal(0.01, result.Margin);
        Assert.All(result.Islands, i =>
        {
            Assert.InRange(i.Rect.U0, 0.51f - 1e-6f, 1.49f);
            Assert.InRange(i.Rect.U1, 0.51f, 1.49f + 1e-6f);
            Assert.InRange(i.Rect.V0, 0.26f - 1e-6f, 0.74f);
            Assert.InRange(i.Rect.V1, 0.26f, 0.74f + 1e-6f);
        });
    }
}
