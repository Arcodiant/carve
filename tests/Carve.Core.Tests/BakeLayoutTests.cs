using System.Numerics;
using Carve.Core.Baking;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-120's bake kernel: coverage, refusals, padding, the engine's tangent frame, break edges and masks.</summary>
[Trait("Tier", "unit")]
public class BakeLayoutTests
{
    private static readonly Vector3[] QuadVertices = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)];

    private static PolygonMesh Quad(params Vector2[] uv) =>
        TestMeshes.Make(QuadVertices, [[0, 1, 2, 3]]).WithUvs(MeshUvs.None.WithFace(0, uv));

    /// <summary>The layout of <paramref name="mesh"/> at <paramref name="size"/>, asserted to succeed.</summary>
    internal static BakeLayout Lay(PolygonMesh mesh, int size, IEnumerable<int>? faces = null)
    {
        var result = BakeLayout.Create(mesh, size, faces);
        Assert.True(result.Succeeded, result.Error);
        return result.Layout!;
    }

    /// <summary>
    /// Each live face mapped into its own cell of a square grid: its corners projected onto the plane across its normal's
    /// largest axis, fitted into the cell with a margin. Planar faces never overlap themselves, so no layout overlaps.
    /// </summary>
    internal static PolygonMesh Celled(PolygonMesh mesh)
    {
        var faces = mesh.LiveFaces.ToArray();
        var n = (int)Math.Ceiling(Math.Sqrt(faces.Length));
        var uvs = MeshUvs.None;
        for (var k = 0; k < faces.Length; k++)
        {
            var f = faces[k];
            var normal = mesh.FaceNormal(f);
            var a = Vector3.Abs(normal);
            Func<Vector3, Vector2> flat = a.X >= a.Y && a.X >= a.Z ? v => new(v.Y, v.Z) : a.Y >= a.Z ? v => new(v.X, v.Z) : v => new(v.X, v.Y);
            var pts = mesh.Faces[f].Corners.Select(i => flat(mesh.Vertices[i])).ToArray();
            var min = pts.Aggregate(Vector2.Min);
            var max = pts.Aggregate(Vector2.Max);
            var span = MathF.Max(max.X - min.X, max.Y - min.Y);
            var cell = new Vector2(k % n, k / n);
            uvs = uvs.WithFace(f, pts.Select(p => (cell + new Vector2(0.1f) + (p - min) / span * 0.8f) / n).ToArray());
        }
        return mesh.WithUvs(uvs);
    }

    /// <summary>The centre-covered texel of <paramref name="face"/> nearest the mean uv of its texels.</summary>
    private static BakeTexel TexelOf(BakeLayout layout, int face)
    {
        var texels = layout.Texels().Where(t => t.Face == face && t.Coverage == BakeCoverage.Centre).ToArray();
        var mean = texels.Aggregate(Vector2.Zero, (s, u) => s + u.Uv) / texels.Length;
        return texels.MinBy(t => Vector2.Distance(t.Uv, mean));
    }

    [Fact]
    public void A_unit_quad_over_the_whole_square_covers_every_texel_by_its_centre()
    {
        var layout = Lay(Quad(new(0, 1), new(1, 1), new(1, 0), new(0, 0)), 64);
        Assert.Equal(64 * 64, layout.CoveredCount);
        Assert.Equal(0, layout.ClampedCount);
        Assert.All(layout.Texels(), t => Assert.Equal(BakeCoverage.Centre, t.Coverage));
        var texel = layout.Texels().Single(t => t.X == 10 && t.Y == 20);
        Assert.Equal(new Vector2(10.5f / 64, 20.5f / 64), texel.Uv);
        // u = x and v = 1 − y on the quad.
        Assert.Equal(10.5f / 64, texel.Position.X, 5);
        Assert.Equal(1 - 20.5f / 64, texel.Position.Y, 5);
        Assert.Equal(1f, texel.Weights.X + texel.Weights.Y + texel.Weights.Z, 5);
    }

    [Fact]
    public void An_inset_quad_covers_exactly_the_centres_it_holds_and_clamps_the_ring_within_half_a_diagonal()
    {
        // Pixel rectangle x 9.9 .. 30.6, y 4.9 .. 40.9: no centre on an edge.
        float x0 = 9.9f / 64, x1 = 30.6f / 64, y0 = 4.9f / 64, y1 = 40.9f / 64;
        var layout = Lay(Quad(new(x0, y1), new(x1, y1), new(x1, y0), new(x0, y0)), 64);
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++)
        {
            double cx = x + 0.5, cy = y + 0.5;
            double px0 = x0 * 64.0, px1 = x1 * 64.0, py0 = y0 * 64.0, py1 = y1 * 64.0;
            var inside = cx > px0 && cx < px1 && cy > py0 && cy < py1;
            var dx = Math.Max(0, Math.Max(px0 - cx, cx - px1));
            var dy = Math.Max(0, Math.Max(py0 - cy, cy - py1));
            var expected = inside ? BakeCoverage.Centre : dx * dx + dy * dy <= 0.5 ? BakeCoverage.Clamp : BakeCoverage.None;
            Assert.True(expected == layout.CoverageAt(x, y), $"texel ({x}, {y}): {layout.CoverageAt(x, y)}, expected {expected}");
        }
        Assert.True(layout.ClampedCount > 0);
        // A clamped texel samples the quad's nearest point: the corner (x0, y0) for the texel up and left of it.
        Assert.True(layout.TryGetTexel(9, 4, out var corner));
        Assert.Equal(BakeCoverage.Clamp, corner.Coverage);
        Assert.Equal(x0, corner.Uv.X, 5);
        Assert.Equal(y0, corner.Uv.Y, 5);
    }

    [Fact]
    public void A_sliver_face_is_covered_by_the_clamp()
    {
        // Two texels wide in pixels (x 10.6 .. 12.4): it holds column 11's centres, and the clamp takes columns 10 and 12.
        float x0 = 10.6f / 64, x1 = 12.4f / 64, y0 = 8f / 64, y1 = 40f / 64;
        var layout = Lay(Quad(new(x0, y1), new(x1, y1), new(x1, y0), new(x0, y0)), 64);
        for (var y = 8; y < 40; y++)
        {
            Assert.Equal(BakeCoverage.Clamp, layout.CoverageAt(10, y));
            Assert.Equal(BakeCoverage.Centre, layout.CoverageAt(11, y));
            Assert.Equal(BakeCoverage.Clamp, layout.CoverageAt(12, y));
        }

        // Thinner than a texel and between two columns of centres: no centre at all, every texel by the clamp.
        float t0 = 20.6f / 64, t1 = 20.9f / 64;
        var thin = Lay(Quad(new(t0, y1), new(t1, y1), new(t1, y0), new(t0, y0)), 64);
        Assert.Equal(thin.CoveredCount, thin.ClampedCount);
        Assert.True(thin.CoveredCount >= 2 * 32);
        Assert.All(thin.Texels(), t => Assert.Equal(0, t.Face));
    }

    [Fact]
    public void Two_islands_sharing_an_edge_in_uv_overlap_nowhere_and_count_no_texel_twice()
    {
        // Two triangles of the unit square as two faces, split along the diagonal that runs through every texel centre
        // (x + 0.5, y + 0.5); and two quads sharing the column of centres u = 32.5 / 64.
        var diagonal = TestMeshes.Make(QuadVertices, [[0, 1, 2], [0, 2, 3]]).WithUvs(MeshUvs.None
            .WithFace(0, [new(0, 0), new(1, 0), new(1, 1)])
            .WithFace(1, [new(0, 0), new(1, 1), new(0, 1)]));
        var layout = Lay(diagonal, 64);
        Assert.Equal(64 * 64, layout.CoveredCount);
        Assert.Equal(0, layout.ClampedCount);
        Assert.Equal(64, Enumerable.Range(0, 64).Count(k => layout.FaceAt(k, k) >= 0));

        var cube = TestMeshes.Cube();
        var m = 32.5f / 64;
        var pair = cube.WithUvs(MeshUvs.None
            .WithFace(1, [new(0, 1), new(m, 1), new(m, 0), new(0, 0)])
            .WithFace(5, [new(m, 1), new(1, 1), new(1, 0), new(m, 0)]));
        var both = Lay(pair, 64);
        Assert.Equal(64 * 64, both.CoveredCount);
        Assert.Equal(0, both.ClampedCount);
        // The column of centres on the shared edge is the left edge of face 5's island, so face 5 takes it.
        Assert.Equal(32 * 64, both.Texels().Count(t => t.Face == 1));
    }

    [Fact]
    public void A_stacked_island_is_refused_naming_both_faces()
    {
        var square = new Vector2[] { new(0, 1), new(1, 1), new(1, 0), new(0, 0) };
        var mesh = TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(1, square).WithFace(5, square).WithFace(3, [new(0.5f, 0.5f), new(0.6f, 0.5f), new(0.6f, 0.4f), new(0.5f, 0.4f)]));
        var result = BakeLayout.Create(mesh, 64);
        Assert.False(result.Succeeded);
        Assert.Contains("faces[1] and faces[5]", result.Error);
        Assert.Contains("faces[1] and faces[3]", result.Error);
        Assert.Contains("faces[3] and faces[5]", result.Error);

        // More than eight pairs: eight named, the rest counted.
        var many = TestMeshes.Grid(4, 4);
        var stacked = many.WithUvs(MeshUvs.None.WithFaces(many.LiveFaces.Select(f =>
            KeyValuePair.Create(f, (IReadOnlyList<Vector2>?)square))));
        var error = BakeLayout.Create(stacked, 64).Error!;
        var faceCount = stacked.LiveFaceCount;
        var pairs = faceCount * (faceCount - 1) / 2;
        Assert.Contains($"and {pairs - BakeLayout.NamedOverlaps} more pairs", error);
        Assert.StartsWith("the UVs overlap, so a texel would show two faces: faces[0] and faces[1], faces[0] and faces[2]", error);
    }

    [Fact]
    public void Unmapped_faces_are_listed_and_a_mesh_with_no_uvs_is_refused()
    {
        var mesh = TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(1, [new(0, 1), new(1, 1), new(1, 0), new(0, 0)]));
        var result = BakeLayout.Create(mesh, 64);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal([0, 2, 3, 4, 5], result.Unmapped);
        Assert.Equal([1], result.Layout!.Faces);
        Assert.All(result.Layout.Texels(), t => Assert.Equal(1, t.Face));

        var some = BakeLayout.Create(mesh, 64, [1, 2]);
        Assert.Equal([2], some.Unmapped);

        Assert.Equal(BakeLayout.NoUvsMessage, BakeLayout.Create(TestMeshes.Cube(), 64).Error);
        Assert.Contains("power of two", BakeLayout.Create(mesh, 100).Error);
        Assert.Contains("power of two", BakeLayout.Create(mesh, 32).Error);
        Assert.Contains("power of two", BakeLayout.Create(mesh, 8192).Error);
        Assert.StartsWith("faces[9]", BakeLayout.Create(mesh, 64, [9]).Error);
    }

    /// <summary>Two islands, each covered texel valued by its index, padded, against a brute-force reading of the rule.</summary>
    [Fact]
    public void Padding_fills_rings_from_the_nearest_island_leaves_covered_texels_and_breaks_ties_by_index()
    {
        var mesh = TestMeshes.Cube().WithUvs(MeshUvs.None
            .WithFace(1, [new(0.1f, 0.4f), new(0.3f, 0.4f), new(0.3f, 0.2f), new(0.1f, 0.2f)])
            .WithFace(5, [new(0.4f, 0.4f), new(0.8f, 0.4f), new(0.8f, 0.2f), new(0.4f, 0.2f)]));
        // Side by side in the same rows, covered up to column 19 and from column 25: column 22 is 3 from both.
        var layout = Lay(mesh, 64);
        var image = layout.NewImage(2);
        foreach (var t in layout.Texels()) { image[t.X, t.Y, 0] = t.Y * 64 + t.X; image[t.X, t.Y, 1] = t.Face; }
        for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++) if (!layout.IsCovered(x, y)) image[x, y, 1] = -7;

        Assert.True(Bake.Pad(image, layout, 0).Values.SequenceEqual(image.Values));

        foreach (var n in new[] { 1, 3, 6 })
        {
            var padded = Bake.Pad(image, layout, n);
            var ties = 0;
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                if (layout.IsCovered(x, y))
                {
                    Assert.Equal(image[x, y, 0], padded[x, y, 0]);
                    continue;
                }
                long best = long.MaxValue; var at = -1; var count = 0;
                for (var sy = 0; sy < 64; sy++)
                for (var sx = 0; sx < 64; sx++)
                {
                    if (!layout.IsCovered(sx, sy)) continue;
                    long d2 = (long)(sx - x) * (sx - x) + (long)(sy - y) * (sy - y);
                    if (d2 < best) { best = d2; at = sy * 64 + sx; count = 1; }
                    else if (d2 == best) count++; // later in row-major order, so the first stays
                }
                if (best <= (long)n * n)
                {
                    if (count > 1) ties++;
                    Assert.True(at == padded[x, y, 0], $"padding {n}, texel ({x}, {y}): {padded[x, y, 0]}, expected {at}");
                    Assert.Equal(image[at % 64, at / 64, 1], padded[x, y, 1]);
                }
                else
                {
                    Assert.Equal(0, padded[x, y, 0]);
                    Assert.Equal(-7, padded[x, y, 1]);
                }
            }
            if (n >= 3) Assert.True(ties > 0, $"padding {n} met no tie");
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.Pad(image, layout, 33));
        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.Pad(image, layout, -1));
        _ = Bake.Pad(image, layout, 32);
    }

    /// <summary>
    /// T-120's probe restated: on the +Z face of a unit box with u = x and v = 1 − y (MaterialRenderTests.FrontUvs), the
    /// engine leans the normal toward world +x for red and world +y for green. The cotangent formula as written runs its
    /// second vector along +v, which is world −y here, so the engine's green is that vector negated.
    /// </summary>
    [Fact]
    public void The_frame_matches_the_probe_red_along_world_x_and_green_along_world_y()
    {
        var mesh = TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(1, [new(0, 1), new(1, 1), new(1, 0), new(0, 0)]));
        var layout = Lay(mesh, 64);
        foreach (var texel in layout.Texels())
        {
            var f = texel.Frame;
            AssertNear(Vector3.UnitZ, f.N);
            AssertNear(Vector3.UnitX, Vector3.Normalize(f.T));
            AssertNear(Vector3.UnitY, Vector3.Normalize(f.B));
        }

        var any = layout.Texels().First();
        var tri = layout.Triangles[any.Triangle];
        var uv = mesh.Uvs!.Of(1)!;
        var face = mesh.Faces[1];
        var (_, alongV) = TangentFrame.Cotangent(mesh.Vertices[tri.A], mesh.Vertices[tri.B], mesh.Vertices[tri.C],
            uv[face.CornerOf(tri.A)], uv[face.CornerOf(tri.B)], uv[face.CornerOf(tri.C)], Vector3.UnitZ);
        AssertNear(-Vector3.UnitY, Vector3.Normalize(alongV)); // the formula's own sign, which the engine flips

        // The probe's maps: R+ (218, 128, 218) toward +x, G+ (128, 218, 218) toward +y, G− toward −y, flat along N.
        var frame = any.Frame;
        var r = frame.FromTangentSpace(new(218, 128, 218));
        Assert.True(r.X > 0.5f && Math.Abs(r.Y) < 0.01f && r.Z > 0); // 128 is half a level off 127.5
        var g = frame.FromTangentSpace(new(128, 218, 218));
        Assert.True(g.Y > 0.5f && Math.Abs(g.X) < 0.01f && g.Z > 0);
        Assert.True(frame.FromTangentSpace(new(128, 38, 218)).Y < -0.5f);
        AssertNear(Vector3.UnitZ, frame.FromTangentSpace(new(127.5f, 127.5f, 255)));

        // Encoding and decoding agree.
        var w = Vector3.Normalize(new Vector3(0.3f, -0.2f, 0.9f));
        AssertNear(w, frame.FromTangentSpace(frame.ToTangentSpace(w)));
        AssertNear(new Vector3(127.5f, 127.5f, 255f), frame.ToTangentSpace(Vector3.UnitZ), 1e-3f);
    }

    [Fact]
    public void A_mirrored_island_flips_red_and_keeps_green()
    {
        var mesh = TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(1, [new(1, 1), new(0, 1), new(0, 0), new(1, 0)]));
        var layout = Lay(mesh, 64);
        Assert.Equal(64 * 64, layout.CoveredCount);
        foreach (var texel in layout.Texels())
        {
            AssertNear(-Vector3.UnitX, Vector3.Normalize(texel.Frame.T));
            AssertNear(Vector3.UnitY, Vector3.Normalize(texel.Frame.B));
        }
        var any = layout.Texels().First().Frame;
        Assert.True(any.FromTangentSpace(new(218, 128, 218)).X < -0.5f);
        Assert.True(any.FromTangentSpace(new(128, 218, 218)).Y > 0.5f);
    }

    [Fact]
    public void On_a_flat_box_every_edge_is_a_ninety_degree_convex_break()
    {
        var cube = Celled(TestMeshes.Cube());
        var layout = Lay(cube, 128);
        for (var f = 0; f < 6; f++)
        {
            var texel = TexelOf(layout, f);
            var near = layout.NearEdges(texel, 2);
            Assert.Equal(4, near.Count);
            Assert.All(near, e =>
            {
                Assert.Equal(EdgeShape.Convex, e.Shape);
                Assert.Equal(Math.PI / 2, e.Bend, 5);
                Assert.Equal(0, Vector3.Dot(e.OtherNormal, cube.FaceNormal(f)), 5);
            });
            Assert.True(near.Zip(near.Skip(1)).All(p => p.First.Distance <= p.Second.Distance));
            // A narrow width keeps only what is within it.
            var nearest = near[0].Distance;
            Assert.All(layout.NearEdges(texel, nearest), e => Assert.True(e.Distance <= nearest));
            Assert.Empty(layout.NearEdges(texel, nearest * 0.99));
        }
    }

    [Fact]
    public void On_an_l_block_the_inner_edge_is_concave()
    {
        var block = Celled(TestMeshes.LBlock());
        var layout = Lay(block, 128);
        var concave = new List<(int Face, NearEdge Edge)>();
        for (var f = 0; f < block.Faces.Count; f++)
            foreach (var e in layout.NearEdges(TexelOf(layout, f), 10))
            {
                Assert.Equal(Math.PI / 2, e.Bend, 5);
                if (e.Shape == EdgeShape.Concave) concave.Add((f, e));
            }
        // The reflex corner at (1, 1): vertices 3 and 9, between sides 4 (corner 2 to 3) and 5 (corner 3 to 4).
        Assert.Equal(2, concave.Count);
        Assert.All(concave, c => Assert.Equal((3, 9), (c.Edge.Lo, c.Edge.Hi)));
        Assert.Equal([4, 5], concave.Select(c => c.Face).Order());
    }

    [Fact]
    public void On_a_long_armed_l_block_only_the_inner_edge_is_concave()
    {
        // One arm three long: the caps' corner mean, (1.33, 0.83), lies beyond the inner wall x = 1's line, so a side
        // test from the other face's mean calls the wall's two cap edges, (3, 4) and (9, 10), concave.
        var block = Celled(TestMeshes.LBlock([new(0, 0, 0), new(3, 0, 0), new(3, 1, 0), new(1, 1, 0), new(1, 2, 0), new(0, 2, 0)]));
        var layout = Lay(block, 128);
        var concave = new List<(int Face, NearEdge Edge)>();
        for (var f = 0; f < block.Faces.Count; f++)
            foreach (var e in layout.NearEdges(TexelOf(layout, f), 10))
            {
                Assert.Equal(Math.PI / 2, e.Bend, 5);
                if (e.Shape == EdgeShape.Concave) concave.Add((f, e));
            }
        Assert.Equal(2, concave.Count);
        Assert.All(concave, c => Assert.Equal((3, 9), (c.Edge.Lo, c.Edge.Hi)));
        Assert.Equal([4, 5], concave.Select(c => c.Face).Order());

        // The wall x = 1 (side 5, corner 3 to 4) reads light by its cap edges, as a box's edges do.
        var wall = layout.NearEdges(TexelOf(layout, 5), 10).Where(e => e.OtherFace is 0 or 1).ToArray();
        Assert.Equal(2, wall.Length);
        Assert.All(wall, e => Assert.Equal(EdgeShape.Convex, e.Shape));
    }

    [Fact]
    public void On_a_smooth_mesh_only_the_marked_hard_edge_is_a_break_and_normals_interpolate()
    {
        var cube = Celled(TestMeshes.Cube()).WithShade(MeshShade.Of(ShadingMode.Smooth, [(4, 5)]));
        var layout = Lay(cube, 128);
        var top = layout.NearEdges(TexelOf(layout, 1), 10);
        var edge = Assert.Single(top);
        Assert.Equal((4, 5, 2), (edge.Lo, edge.Hi, edge.OtherFace));
        Assert.Equal(EdgeShape.Convex, edge.Shape);
        Assert.Equal(Math.PI / 2, edge.Bend, 5);
        Assert.Empty(layout.NearEdges(TexelOf(layout, 3), 10));

        // Smooth: a texel near a corner leans off its face normal; the face normal stays geometric.
        var corner = layout.Texels().Where(t => t.Face == 3).MaxBy(t => (t.Weights - new Vector3(1f / 3)).Length());
        AssertNear(Vector3.UnitY, corner.FaceNormal);
        Assert.True(Vector3.Dot(corner.ShadingNormal, corner.FaceNormal) < 0.99f);
        Assert.Equal(1f, corner.ShadingNormal.Length(), 5);

        // The same faces coplanar but with a hard mark: flat, bend 0.
        var strip = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(2, 1, 0), new(1, 1, 0), new(0, 1, 0)],
            [[0, 1, 4, 5], [1, 2, 3, 4]]);
        var smoothStrip = Celled(strip).WithShade(MeshShade.Of(ShadingMode.Smooth, [(1, 4)]));
        var flatEdge = Assert.Single(Lay(smoothStrip, 64).NearEdges(TexelOf(Lay(smoothStrip, 64), 0), 10));
        Assert.Equal((EdgeShape.Flat, 0.0), (flatEdge.Shape, flatEdge.Bend));
        Assert.Empty(Lay(Celled(strip), 64).NearEdges(TexelOf(Lay(Celled(strip), 64), 0), 10)); // flat and coplanar: no break
    }

    [Fact]
    public void Masks_give_one_image_per_group_then_per_material()
    {
        var style = FaceStyle.None.WithGroup("lid", [1, 3]).WithGroup("body", [0, 2, 4, 5]).WithMaterial([5, 2], "steel").WithMaterial([4], "brass");
        var cube = Celled(TestMeshes.Cube()).WithStyle(style);
        var layout = Lay(cube, 64);
        var masks = Bake.Masks(layout, cube);
        Assert.Equal([(MaskSource.Group, "lid"), (MaskSource.Group, "body"), (MaskSource.Material, "steel"), (MaskSource.Material, "brass")],
            masks.Select(m => (m.Source, m.Name)));
        int[][] members = [[1, 3], [0, 2, 4, 5], [2, 5], [4]];
        for (var k = 0; k < masks.Count; k++)
        {
            var image = masks[k].Image;
            Assert.Equal(1, image.Channels);
            var ones = 0;
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var f = layout.FaceAt(x, y);
                var expected = f >= 0 && members[k].Contains(f) ? 1f : 0f;
                Assert.Equal(expected, image[x, y]);
                if (expected == 1) ones++;
            }
            Assert.True(ones > 0);
        }
    }

    [Fact]
    public void Two_runs_give_bit_identical_results()
    {
        var barrel = Barrel();
        var a = Lay(barrel, 128);
        var b = Lay(barrel, 128);
        Assert.Equal(a.Texels().ToArray(), b.Texels().ToArray());
        var image = a.NewImage(3);
        foreach (var t in a.Texels()) { image[t.X, t.Y, 0] = t.Position.X; image[t.X, t.Y, 1] = t.ShadingNormal.Y; image[t.X, t.Y, 2] = t.Frame.B.Z; }
        var p1 = Bake.Pad(image, a, 8).Values.ToArray();
        var p2 = Bake.Pad(image, b, 8).Values.ToArray();
        Assert.Equal(p1.Select(BitConverter.SingleToInt32Bits), p2.Select(BitConverter.SingleToInt32Bits));
        var texel = a.Texels().First();
        Assert.Equal(a.NearEdges(texel, 0.5), b.NearEdges(texel, 0.5));
    }

    [Fact]
    public void The_barrel_sample_lays_out_at_256_with_no_overlap()
    {
        var barrel = Barrel();
        var result = BakeLayout.Create(barrel, 256);
        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(result.Unmapped);
        Assert.Equal(34, result.Layout!.Faces.Count);
        Assert.Equal(34, result.Layout.Texels().Select(t => t.Face).Distinct().Count());
        Assert.All(result.Layout.Texels(), t => Assert.True(t.Weights.X >= 0 && t.Weights.Y >= 0 && t.Weights.Z >= 0));
    }

    private static PolygonMesh Barrel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "barrel.carve"))) dir = dir.Parent!;
        var read = CarveFile.Read(File.ReadAllText(Path.Combine(dir.FullName, "samples", "barrel.carve")));
        Assert.True(read.Model is not null, string.Join("; ", read.Errors));
        return read.Model!.Meshes.Single().Mesh;
    }

    private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-5f) =>
        Assert.True((expected - actual).Length() <= tolerance, $"expected {expected}, got {actual}");
}
