using System.Numerics;
using Carve.Core.Baking;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-125's bevel normal map: the mean normal on a break edge, easing by smoothstep to the shading normal at the width.</summary>
[Trait("Tier", "unit")]
public class BevelNormalTests
{
    private static readonly Vector2[] FrontUvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

    /// <summary>A unit cube with only its +Z face mapped, as MaterialRenderTests.FrontUvs lays it (u = x, v = 1 − y).</summary>
    private static PolygonMesh FrontBox(Vector2[]? uv = null) => TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(1, uv ?? FrontUvs));

    private static byte Q(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f, MidpointRounding.AwayFromZero), 0, 255);

    /// <summary>The texel's stored bytes, as bake writes them.</summary>
    private static Vector3 Bytes(BakeImage image, int x, int y) => new(Q(image[x, y, 0]), Q(image[x, y, 1]), Q(image[x, y, 2]));

    /// <summary>The world normal the engine lights for the texel, from its stored bytes.</summary>
    private static Vector3 Decoded(BakeImage image, BakeTexel texel) => texel.Frame.FromTangentSpace(Bytes(image, texel.X, texel.Y));

    /// <summary>The texel's encoding with nothing quantised, put back into world space.</summary>
    private static Vector3 RoundTrip(BakeLayout layout, BakeTexel texel, double width) =>
        texel.Frame.FromTangentSpace(texel.Frame.ToTangentSpace(Bake.BevelWorldNormal(texel, layout.NearEdges(texel, width), width)));

    private static double Smooth(double d, double width)
    {
        var t = Math.Clamp(1 - d / width, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static void Near(Vector3 expected, Vector3 actual, float tolerance) =>
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"expected {expected}, got {actual}");

    [Fact]
    public void On_a_box_the_top_edge_takes_the_mean_normal_and_a_texel_at_the_width_or_farther_the_face_normal()
    {
        var layout = BakeLayoutTests.Lay(FrontBox(), 64);
        var image = Bake.BevelNormal(layout, 0.1);
        Assert.True(layout.TryGetTexel(32, 0, out var any));

        // On the edge itself (the texel moved onto it): exactly the mean of +Z and +Y.
        var onEdge = any with { Position = new Vector3(0.5f, 1, 1) };
        var mean = Vector3.Normalize(new Vector3(0, 1, 1));
        Near(mean, RoundTrip(layout, onEdge, 0.1), 1e-5f);
        Near(new Vector3(0, 0.70710677f, 0.70710677f), Bake.BevelWorldNormal(onEdge, layout.NearEdges(onEdge, 0.1), 0.1), 1e-5f);

        // The top row's texel, half a texel from the edge, as stored: within a level of the formula.
        var d = 0.5 / 64;
        var w = Smooth(d, 0.1);
        var expected = Vector3.Normalize(new Vector3(0, (float)(w * 0.70710678), (float)(1 + w * (0.70710678 - 1))));
        Near(expected, Decoded(image, any), 0.012f);
        Assert.True(Bytes(image, 32, 0).Y > 200, $"G {Bytes(image, 32, 0).Y}: the top edge leans up, toward +G");

        // Every texel with no edge within the width is N itself: (128, 128, 255) or a level from it, and decodes to +Z.
        var far = 0;
        foreach (var texel in layout.Texels())
        {
            var p = texel.Position;
            var reach = new[] { p.X, 1 - p.X, p.Y, 1 - p.Y }.Min();
            if (reach < 0.1) continue;
            far++;
            var b = Bytes(image, texel.X, texel.Y);
            Assert.True(Math.Abs(b.X - 128) <= 1 && Math.Abs(b.Y - 128) <= 1 && b.Z >= 254, $"texel ({texel.X}, {texel.Y}) at {reach} m holds {b}");
            Near(Vector3.UnitZ, Decoded(image, texel), 0.012f);
        }
        Assert.True(far > 40 * 40);
    }

    [Fact]
    public void A_corner_texel_sums_the_pulls_of_both_edges()
    {
        var layout = BakeLayoutTests.Lay(FrontBox(), 64);
        Assert.True(layout.TryGetTexel(62, 1, out var any));
        var corner = any with { Position = new Vector3(0.97f, 0.97f, 1) };
        var edges = layout.NearEdges(corner, 0.1);
        Assert.Equal(2, edges.Count);
        var w = Smooth(0.03, 0.1);
        var h = 0.70710678;
        var expected = Vector3.Normalize(new Vector3((float)(w * h), (float)(w * h), (float)(1 + 2 * w * (h - 1))));
        Near(expected, Bake.BevelWorldNormal(corner, edges, 0.1), 1e-4f);
        Near(expected, RoundTrip(layout, corner, 0.1), 1e-4f);

        // The stored corner texel leans toward +x and +y alike: red and green both above flat by the same.
        var image = Bake.BevelNormal(layout, 0.1);
        var b = Bytes(image, 63, 0);
        Assert.True(b.X > 150 && b.Y > 150 && Math.Abs(b.X - b.Y) <= 1, $"corner texel {b}");
    }

    [Fact]
    public void A_concave_edge_of_an_l_block_leans_toward_the_other_face()
    {
        var block = BakeLayoutTests.Celled(TestMeshes.LBlock());
        var layout = BakeLayoutTests.Lay(block, 128);
        var image = Bake.BevelNormal(layout, 0.3);
        var seen = 0;
        foreach (var texel in layout.Texels().Where(t => t.Face is 4 or 5))
        {
            var edges = layout.NearEdges(texel, 0.3);
            if (edges.Count != 1 || edges[0].Shape != EdgeShape.Concave || edges[0].Distance > 0.05) continue;
            seen++;
            var e = edges[0];
            var n = texel.ShadingNormal;
            var target = Vector3.Normalize(texel.FaceNormal + e.OtherNormal);
            var w = (float)Smooth(e.Distance, 0.3);
            var expected = Vector3.Normalize(n + w * (target - n));
            Near(expected, Decoded(image, texel), 0.012f);
            Assert.True(Vector3.Dot(Decoded(image, texel), e.OtherNormal) > 0.5f, "the bend leans toward the face across the inner edge");
        }
        Assert.True(seen > 10, $"only {seen} texels by the inner edge");
    }

    [Fact]
    public void A_mirrored_island_stores_red_flipped_and_decodes_to_the_same_world_normal()
    {
        var plain = BakeLayoutTests.Lay(FrontBox(), 64);
        var mirrored = BakeLayoutTests.Lay(FrontBox([new(1, 1), new(0, 1), new(0, 0), new(1, 0)]), 64);
        var a = Bake.BevelNormal(plain, 0.1);
        var b = Bake.BevelNormal(mirrored, 0.1);
        // Column 63 of the plain island is by the +X edge; in the mirrored one that edge is at column 0.
        for (var y = 20; y < 44; y++)
        {
            Assert.True(plain.TryGetTexel(63, y, out var p));
            Assert.True(mirrored.TryGetTexel(0, y, out var m));
            Near(p.Position, m.Position, 1e-5f);
            Assert.True(Bytes(a, 63, y).X > 200 && Bytes(b, 0, y).X < 55, $"row {y}: red {Bytes(a, 63, y).X} and mirrored {Bytes(b, 0, y).X}");
            Assert.Equal(Bytes(a, 63, y).Y, Bytes(b, 0, y).Y);
            Near(Decoded(a, p), Decoded(b, m), 0.012f);
            Assert.True(Decoded(b, m).X > 0.5f, "the mirrored texel still leans toward +x");
        }
    }

    [Fact]
    public void Two_bakes_are_identical()
    {
        var layout = BakeLayoutTests.Lay(BakeLayoutTests.Celled(TestMeshes.LBlock()), 128);
        var one = Bake.BevelNormal(layout, 0.2).Values.ToArray();
        var two = Bake.BevelNormal(BakeLayoutTests.Lay(BakeLayoutTests.Celled(TestMeshes.LBlock()), 128), 0.2).Values.ToArray();
        Assert.Equal(one, two);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_width_that_is_not_more_than_zero_and_finite_is_refused(double width)
    {
        var layout = BakeLayoutTests.Lay(FrontBox(), 64);
        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.BevelNormal(layout, width));
    }

    [Fact]
    public void No_layout_is_refused() => Assert.Throws<ArgumentNullException>(() => Bake.BevelNormal(null!, 0.1));
}
