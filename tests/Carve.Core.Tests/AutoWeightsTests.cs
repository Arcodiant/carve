using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-37: automatic weights by distance to each bone's segment (D-6, Weights).</summary>
[Trait("Tier", "unit")]
public class AutoWeightsTests
{
    private static Skeleton Chain(params (string Name, string? Parent, Vector3 At)[] bones) =>
        Skeleton.Create([.. bones.Select(b => new BoneSpec(b.Name, b.Parent, b.At, Quaternion.Identity))]).Skeleton!;

    /// <summary>base at the origin, joint 1 above it: base's segment is y 0..1, joint's (a leaf) y 1..1.5.</summary>
    private static Skeleton TwoBones() => Chain(("base", null, Vector3.Zero), ("joint", "base", Vector3.UnitY));

    private static PolygonMesh Points(params Vector3[] points)
    {
        // A fan of triangles over the points, so each is a vertex of a valid mesh; only the vertices matter here.
        var all = points.Concat([new Vector3(9, 0, 0), new Vector3(9, 0, 1), new Vector3(9, 1, 0)]).ToArray();
        var n = all.Length;
        return PolygonMesh.Create(all, [[n - 3, n - 2, n - 1]]).Mesh!;
    }

    private static Influence[] At(Skeleton s, Vector3 p) =>
        AutoWeights.For(Vector3D.From(p), AutoWeights.Segments(s), [.. Enumerable.Range(0, s.Bones.Count)]);

    [Fact]
    public void A_vertex_on_a_bone_is_wholly_that_bones()
    {
        var s = TwoBones();
        Assert.Equal([new Influence(0, 1)], At(s, new Vector3(0, 0.4f, 0)));
        // On the leaf's own segment, which runs on past its head for half the parent's length.
        Assert.Equal([new Influence(1, 1)], At(s, new Vector3(0, 1.4f, 0)));
    }

    [Fact]
    public void A_point_midway_between_two_bones_shares_them_evenly()
    {
        // Two leaves under one root, 2 apart along X: a point on the plane x = 0, beside both, is the same distance from each.
        var s = Chain(("root", null, Vector3.Zero), ("left", "root", new Vector3(-1, 1, 0)), ("right", "root", new Vector3(1, 1, 0)));
        var w = At(s, new Vector3(0, 2, 0));
        Assert.Equal(2, w.Count(i => i.Bone is 1 or 2));
        Assert.Equal(w.Single(i => i.Bone == 1).Weight, w.Single(i => i.Bone == 2).Weight);
        // The joint of the two-bone chain is on both bones' segments: half each.
        Assert.Equal([new Influence(0, 0.5), new Influence(1, 0.5)], At(TwoBones(), Vector3.UnitY));
    }

    [Fact]
    public void Off_a_bone_the_falloff_is_the_fourth_power_of_the_distance_ratio()
    {
        // (0.5, 0.5): 0.5 from base, and from joint's segment (y 1..1.5) sqrt(0.5); joint's raw weight is (0.5 / sqrt(0.5))^4 = 0.25.
        var w = At(TwoBones(), new Vector3(0.5f, 0.5f, 0));
        Assert.Equal(1 / 1.25, w[0].Weight, 12);
        Assert.Equal(0.25 / 1.25, w[1].Weight, 12);
    }

    [Fact]
    public void A_leafs_segment_runs_on_for_half_its_parents_length_and_no_further()
    {
        // (0, 1.8): 0.3 past the leaf's end at y 1.5 and 0.8 from base's end at y 1. A leaf of the parent's whole length (to y 2)
        // would hold the point (wholly joint's); one of twice it, the same.
        var w = At(TwoBones(), new Vector3(0, 1.8f, 0));
        var y = (double)1.8f;
        var r = Math.Pow((y - 1.5) / (y - 1), 4);
        Assert.Equal(r / (1 + r), w.Single(i => i.Bone == 0).Weight, 9);
        Assert.Equal(1 / (1 + r), w.Single(i => i.Bone == 1).Weight, 9);
    }

    [Fact]
    public void Under_bones_a_vertex_as_near_a_named_bone_as_an_unnamed_one_of_lower_index_is_out_of_scope()
    {
        // The joint at (0, 1, 0) lies on base's segment and on joint's: the tie goes to base, index 0, so with bones [1] it is left alone.
        var w = AutoWeights.Weigh(Points(Vector3.UnitY), TwoBones(), [1], false, out var outcome);
        Assert.False(w.Has(0));
        Assert.Equal(0, outcome.Weighed);
        // With bones [0] it is in scope, and weighed among base alone.
        Assert.Equal([new Influence(0, 1)], AutoWeights.Weigh(Points(Vector3.UnitY), TwoBones(), [0], false, out _).Of(0));
    }

    [Fact]
    public void A_bone_with_several_children_is_as_near_as_its_nearest_segment()
    {
        var s = Chain(("hips", null, Vector3.Zero), ("leg", "hips", new Vector3(1, -1, 0)), ("spine", "hips", new Vector3(0, 1, 0)));
        var segments = AutoWeights.Segments(s);
        Assert.Equal(2, segments[0].Length);
        Assert.Equal(0, AutoWeights.Distance(new Vector3D(0.5, -0.5, 0), segments[0]), 12);
        Assert.Equal(0, AutoWeights.Distance(new Vector3D(0, 0.5, 0), segments[0]), 12);
    }

    [Fact]
    public void Weighing_is_deterministic_bit_for_bit()
    {
        var s = Chain(("a", null, Vector3.Zero), ("b", "a", new Vector3(0.3f, 1, 0.1f)), ("c", "b", new Vector3(-0.2f, 0.7f, 0.4f)),
            ("d", "a", new Vector3(0.8f, -0.2f, 0)), ("e", "d", new Vector3(0.1f, -0.9f, 0.3f)));
        var mesh = Points([.. Enumerable.Range(0, 40).Select(i => new Vector3(MathF.Sin(i * 1.7f), MathF.Cos(i * 0.9f) * 2, i * 0.01f))]);
        var one = AutoWeights.Weigh(mesh, s, null, false, out _);
        var two = AutoWeights.Weigh(mesh, s, null, false, out _);
        foreach (var v in one.Vertices)
        {
            Assert.Equal(one.Of(v).Select(i => (i.Bone, BitConverter.DoubleToInt64Bits(i.Weight))), two.Of(v).Select(i => (i.Bone, BitConverter.DoubleToInt64Bits(i.Weight))));
        }
        Assert.Equal(one.Vertices, two.Vertices);
    }

    [Fact]
    public void At_most_four_bones_are_kept_with_ties_to_the_lower_index_and_they_sum_to_one()
    {
        // Four leaves around a root at the same distance from a point below it, and one above, farther: the root (nearest)
        // and the three lowest of the four tied leaves are kept.
        var s = Chain(("root", null, Vector3.Zero), ("east", "root", Vector3.UnitX), ("west", "root", -Vector3.UnitX),
            ("north", "root", Vector3.UnitZ), ("south", "root", -Vector3.UnitZ), ("up", "root", Vector3.UnitY));
        var w = At(s, new Vector3(0, -0.5f, 0));
        Assert.Equal([0, 1, 2, 3], w.Select(i => i.Bone));
        Assert.Equal(1, w.Sum(i => i.Weight), 12);
    }

    [Fact]
    public void Existing_weights_are_kept_unless_replace_and_every_vertex_ends_weighted()
    {
        var s = TwoBones();
        var mesh = Points(new Vector3(0, 0.2f, 0), new Vector3(0, 1.3f, 0));
        mesh = mesh.WithWeights(VertexWeights.Of([KeyValuePair.Create(0, new[] { new Influence(1, 1) })]));

        var kept = AutoWeights.Weigh(mesh, s, null, false, out var outcome);
        Assert.Equal(new WeighOutcome(4, 1, 0), outcome);
        Assert.Equal([new Influence(1, 1)], kept.Of(0));
        Assert.Equal([new Influence(1, 1)], kept.Of(1));

        var replaced = AutoWeights.Weigh(mesh, s, null, true, out outcome);
        Assert.Equal(new WeighOutcome(5, 0, 0), outcome);
        Assert.Equal([new Influence(0, 1)], replaced.Of(0));
    }

    [Fact]
    public void Named_bones_weigh_only_the_vertices_nearest_them_and_among_them_alone()
    {
        var s = TwoBones();
        var mesh = Points(new Vector3(0.3f, 0.2f, 0), new Vector3(0.3f, 1.3f, 0), new Vector3(0.3f, 0.95f, 0));
        var w = AutoWeights.Weigh(mesh, s, [1], false, out var outcome);
        Assert.False(w.Has(0));
        Assert.Equal([new Influence(1, 1)], w.Of(1));
        Assert.False(w.Has(2)); // nearer base (0.3 away) than joint
        Assert.Equal(1, outcome.Weighed);
        Assert.Equal(5, outcome.Unweighted);
    }

    [Fact]
    public void A_lone_root_takes_every_vertex()
    {
        var s = Chain(("only", null, new Vector3(0, 1, 0)));
        var w = AutoWeights.Weigh(Points(new Vector3(3, 3, 3)), s, null, false, out var outcome);
        Assert.Equal(0, outcome.Unweighted);
        Assert.All(w.Vertices, v => Assert.Equal([new Influence(0, 1)], w.Of(v)));
    }
}
