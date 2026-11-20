using System.Numerics;
using Carve.Core.Baking;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-126: the curvature map's formula on a box, an L-block, a 45 degree edge and a corner, its determinism and its refusals.</summary>
[Trait("Tier", "unit")]
public class BakeCurvatureTests
{
    /// <summary>Each live face mapped into its own cell of a square grid, its corners projected across its normal's largest axis.</summary>
    private static PolygonMesh Celled(PolygonMesh mesh)
    {
        var faces = mesh.LiveFaces.ToArray();
        var n = (int)Math.Ceiling(Math.Sqrt(faces.Length));
        var uvs = MeshUvs.None;
        for (var k = 0; k < faces.Length; k++)
        {
            var f = faces[k];
            var a = Vector3.Abs(mesh.FaceNormal(f));
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

    private static BakeLayout Lay(PolygonMesh mesh, int size)
    {
        var result = BakeLayout.Create(mesh, size);
        Assert.True(result.Succeeded, result.Error);
        return result.Layout!;
    }

    private static float At(BakeImage image, BakeTexel t) => image[t.X, t.Y];

    /// <summary>Two unit quads meeting along x = 1 with the second turned down by <paramref name="degrees"/>: a convex edge.</summary>
    private static PolygonMesh Fold(double degrees)
    {
        var c = (float)Math.Cos(degrees * Math.PI / 180);
        var s = (float)Math.Sin(degrees * Math.PI / 180);
        return TestMeshes.Make(
            [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(1 + c, 0, -s), new(1 + c, 1, -s)],
            [[0, 1, 2, 3], [2, 1, 4, 5]]);
    }

    [Fact]
    public void A_flat_boxs_edge_reads_1_at_the_edge_and_one_half_at_width()
    {
        var layout = Lay(Celled(TestMeshes.Cube()), 256);
        var image = Bake.Curvature(layout, 0.5);
        var texels = layout.Texels().ToArray();
        var distance = texels.ToDictionary(t => (t.X, t.Y), t => layout.NearEdges(t, 10).Min(e => e.Distance));
        var nearest = texels.MinBy(t => distance[(t.X, t.Y)]);
        Assert.True(At(image, nearest) > 0.99f, $"{At(image, nearest)}");

        // The texel whose edge distance is the nearest to, but not over, width reads within a hair of 0.5.
        var narrow = Bake.Curvature(layout, 0.1);
        var far = texels.Where(t => distance[(t.X, t.Y)] <= 0.1).MaxBy(t => distance[(t.X, t.Y)]);
        Assert.InRange(At(narrow, far), 0.5, 0.505);
        Assert.All(texels.Where(t => distance[(t.X, t.Y)] > 0.1), t => Assert.Equal(0.5f, At(narrow, t)));
    }

    [Fact]
    public void An_l_blocks_inner_edge_reads_0()
    {
        var layout = Lay(Celled(TestMeshes.LBlock()), 256);
        var image = Bake.Curvature(layout, 0.1);
        var inner = layout.Texels()
            .Where(t => t.Face is 4 or 5)
            .Select(t => (Texel: t, Edges: layout.NearEdges(t, 0.1)))
            .Where(p => p.Edges.Count > 0 && p.Edges.All(e => e.Shape == EdgeShape.Concave))
            .MinBy(p => p.Edges[0].Distance);
        Assert.True(At(image, inner.Texel) < 0.01f, $"{At(image, inner.Texel)}");
    }

    [Fact]
    public void A_45_degree_edge_reads_three_quarters()
    {
        var layout = Lay(Celled(Fold(45)), 256);
        var image = Bake.Curvature(layout, 0.5);
        var nearest = layout.Texels().Where(t => t.Face == 0).MinBy(t => layout.NearEdges(t, 10).Min(e => e.Distance));
        Assert.Equal(0.75f, At(image, nearest), 0.01f);
        // A bend past a right angle is capped at a right angle's weight.
        var sharp = Bake.Curvature(Lay(Celled(Fold(120)), 256), 0.5);
        Assert.True(sharp.Values.ToArray().Max() <= 1f);
    }

    [Fact]
    public void A_corner_near_two_edges_is_clamped_and_every_value_is_in_range()
    {
        var layout = Lay(Celled(TestMeshes.Cube()), 256);
        var image = Bake.Curvature(layout, 0.5);
        var corner = layout.Texels().Where(t => layout.NearEdges(t, 10).Count >= 2).MinBy(t => layout.NearEdges(t, 10)[1].Distance);
        Assert.Equal(1f, At(image, corner));
        Assert.All(layout.Texels(), t => Assert.InRange(At(image, t), 0f, 1f));
    }

    [Fact]
    public void Two_bakes_are_identical()
    {
        var layout = Lay(Celled(TestMeshes.LBlock()), 128);
        Assert.Equal(Bake.Curvature(layout, 0.05).Values.ToArray(), Bake.Curvature(layout, 0.05).Values.ToArray());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_width_that_is_not_above_0_is_refused(double width)
    {
        var layout = Lay(Celled(TestMeshes.Cube()), 64);
        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.Curvature(layout, width));
    }
}
