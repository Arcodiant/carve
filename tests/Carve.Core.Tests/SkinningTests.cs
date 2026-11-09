using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>Weights as D-6 says (four at most, normalised, ties to the lower bone) and linear blend skinning against hand-worked values.</summary>
[Trait("Tier", "unit")]
public class SkinningTests
{
    private static readonly Quaternion QuarterZ = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);

    // base at the origin, joint 1 m above it, both unrotated at rest.
    private static Skeleton Chain(Quaternion? baseRest = null) => Skeleton.Create([
        new("base", null, Vector3.Zero, baseRest ?? Quaternion.Identity),
        new("joint", "base", new Vector3(0, 1, 0), Quaternion.Identity),
    ]).Skeleton!;

    private static PolygonMesh Weighted(Vector3[] points, params (int Vertex, int Bone, double Weight)[] weights)
    {
        var n = points.Length;
        var mesh = TestMeshes.Make(points.Concat([new Vector3(9, 9, 9), new Vector3(9, 9, 10)]).Take(n + 2), [[0, 1, n]]);
        var w = VertexWeights.None;
        foreach (var g in weights.GroupBy(x => x.Vertex))
            w = w.With([g.Key], VertexWeights.Normalise(g.ToDictionary(x => x.Bone, x => x.Weight), out _));
        return mesh.WithWeights(w);
    }

    private static Pose Of(params (string Bone, Quaternion? Rotation, Vector3? Translation)[] bones) =>
        new(bones.ToDictionary(b => b.Bone, b => (b.Rotation, b.Translation)));

    [Fact]
    public void A_quarter_turn_of_the_joint_carries_its_vertex_round_the_joint_by_hand_worked_values()
    {
        // (1, 1.5, 0) is (1, 0.5, 0) from the joint at (0, 1, 0); a quarter turn about Z gives (-0.5, 1, 0) from it.
        var mesh = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 1, 1), (1, 0, 1));
        var posed = Skinning.Positions(mesh, Chain(), Of(("joint", QuarterZ, null)));
        Assert.Equal(-0.5f, posed[0].X, 5);
        Assert.Equal(2f, posed[0].Y, 5);
        Assert.Equal(0f, posed[0].Z, 5);
        Assert.Equal(Vector3.Zero, posed[1]);
    }

    [Fact]
    public void Two_equal_influences_land_halfway_between_what_each_bone_does()
    {
        var mesh = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 0, 1), (0, 1, 1), (1, 0, 1));
        var posed = Skinning.Positions(mesh, Chain(), Of(("joint", QuarterZ, null)));
        Assert.Equal(0.25f, posed[0].X, 5);
        Assert.Equal(1.75f, posed[0].Y, 5);
    }

    [Fact]
    public void A_translation_replaces_the_rest_position_and_moves_what_hangs_on_the_bone()
    {
        // joint rests 1 m up; posed 3 m up, so its vertex rises 2 m; base posed 5 m along Z moves both.
        var mesh = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 1, 1), (1, 0, 1));
        var up = Skinning.Positions(mesh, Chain(), Of(("joint", null, new Vector3(0, 3, 0))));
        Assert.Equal(new Vector3(1, 3.5f, 0), up[0]);
        var both = Skinning.Positions(mesh, Chain(), Of(("base", null, new Vector3(0, 0, 5))));
        Assert.Equal(new Vector3(1, 1.5f, 5), both[0]);
        Assert.Equal(new Vector3(0, 0, 5), both[1]);
    }

    [Fact]
    public void A_parents_turn_carries_the_child_bone_and_its_vertices()
    {
        // base turns a quarter about Z: the joint rises along -X, so the joint's vertex (1, 1.5, 0) goes to (-1.5, 1, 0).
        var mesh = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 1, 1), (1, 0, 1));
        var posed = Skinning.Positions(mesh, Chain(), Of(("base", QuarterZ, null)));
        Assert.Equal(-1.5f, posed[0].X, 5);
        Assert.Equal(1f, posed[0].Y, 5);
    }

    [Fact]
    public void A_turned_joint_under_a_turned_parent_composes_the_parent_after_the_child()
    {
        // base rests a quarter turn about Z, so the joint rests at (-1, 0, 0) and its vertex (0, 1, 0) along it at
        // (-2, 0, 0). Posed, the joint turns a quarter about X: the vertex goes to (0, 0, 1) in the joint's frame, which
        // the base's turn leaves at (0, 0, 1), so (-1, 0, 1). The two turns do not commute, so the product's order shows:
        // child x parent instead gives (-2, 0, 0) for the joint's turn applied first in model space.
        var quarterX = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
        var mesh = Weighted([new(-2, 0, 0), new(0, 0, 0)], (0, 1, 1), (1, 0, 1));
        var posed = Skinning.Positions(mesh, Chain(QuarterZ), Of(("joint", quarterX, null)));
        Assert.Equal(-1f, posed[0].X, 5);
        Assert.Equal(0f, posed[0].Y, 5);
        Assert.Equal(1f, posed[0].Z, 5);
    }

    [Fact]
    public void The_rest_pose_returns_the_rest_positions_exactly()
    {
        var odd = Quaternion.Normalize(new Quaternion(0.3f, -0.2f, 0.5f, 0.7f));
        var skeleton = Skeleton.Create([
            new("a", null, new Vector3(0.1f, 0.7f, -0.3f), odd),
            new("b", "a", new Vector3(0.3f, 1.1f, 0.2f), Quaternion.Normalize(new Quaternion(-0.4f, 0.1f, 0.2f, 0.9f))),
        ]).Skeleton!;
        var points = new[] { new Vector3(0.123f, 4.567f, -8.9f), new Vector3(-3.3f, 0.01f, 7.7f), new Vector3(1e3f, -2e-3f, 0.5f) };
        var mesh = Weighted(points, (0, 0, 0.3), (0, 1, 0.7), (1, 1, 1), (2, 0, 1));
        var rest = Skinning.Positions(mesh, skeleton, Pose.Rest);
        for (var i = 0; i < points.Length; i++) Assert.Equal(points[i], rest[i]);
        // giving every bone its own rest transform is the rest pose too.
        var spelled = Of(("a", skeleton.Bones[0].Rotation, skeleton.Bones[0].Position), ("b", skeleton.Bones[1].Rotation, skeleton.Bones[1].Position));
        var same = Skinning.Positions(mesh, skeleton, spelled);
        for (var i = 0; i < points.Length; i++) Assert.Equal(points[i], same[i]);
    }

    [Fact]
    public void An_unweighted_vertex_stays_put_in_any_pose()
    {
        var mesh = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 1, 1));
        var posed = Skinning.Positions(mesh, Chain(), Of(("base", QuarterZ, new Vector3(4, 4, 4))));
        Assert.Equal(new Vector3(0, 0, 0), posed[1]);
        Assert.Equal(new Vector3(9, 9, 9), posed[2]);
    }

    [Fact]
    public void Normals_turn_with_the_bone_and_stay_unit_length()
    {
        var mesh = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 1, 1), (1, 0, 1));
        var normals = Skinning.Normals(mesh, [Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX], Chain(), Of(("joint", QuarterZ, null)));
        Assert.Equal(0f, normals[0].X, 5);
        Assert.Equal(1f, normals[0].Y, 5);
        Assert.Equal(Vector3.UnitX, normals[1]);

        var blend = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 0, 1), (0, 1, 1), (1, 0, 1));
        var half = Skinning.Normals(blend, [Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX], Chain(), Of(("joint", QuarterZ, null)))[0];
        Assert.Equal(1f, half.Length(), 5);
        Assert.Equal(MathF.Sqrt(0.5f), half.X, 5);
        Assert.Equal(MathF.Sqrt(0.5f), half.Y, 5);
    }

    [Fact]
    public void Weights_are_normalised_to_sum_to_one()
    {
        var kept = VertexWeights.Normalise(new Dictionary<int, double> { [0] = 2, [1] = 6 }, out var capped);
        Assert.False(capped);
        Assert.Equal([0.25, 0.75], kept.Select(i => i.Weight));
        Assert.Equal(1.0, kept.Sum(i => i.Weight), 12);
    }

    [Fact]
    public void More_than_four_influences_keep_the_largest_four_and_say_so()
    {
        var kept = VertexWeights.Normalise(new Dictionary<int, double> { [0] = 1, [1] = 2, [2] = 3, [3] = 4, [4] = 5 }, out var capped);
        Assert.True(capped);
        Assert.Equal([1, 2, 3, 4], kept.Select(i => i.Bone));
        Assert.Equal(1.0, kept.Sum(i => i.Weight), 12);
        Assert.Equal(2.0 / 14, kept[0].Weight, 12);
    }

    [Fact]
    public void Ties_in_the_cap_go_to_the_lower_bone_index()
    {
        var kept = VertexWeights.Normalise(new Dictionary<int, double> { [4] = 1, [3] = 1, [2] = 1, [1] = 1, [0] = 1 }, out var capped);
        Assert.True(capped);
        Assert.Equal([0, 1, 2, 3], kept.Select(i => i.Bone));
        Assert.All(kept, i => Assert.Equal(0.25, i.Weight));
    }

    [Fact]
    public void Weights_follow_a_mesh_through_move_and_extrude_and_drop_with_a_new_mesh()
    {
        var mesh = TestMeshes.Cube().WithWeights(VertexWeights.None.With([0, 1], [new Influence(1, 1)]));
        var moved = MeshEdits.Move(mesh, [0], null, null, new Vector3(0, 0, -1));
        Assert.True(moved.Succeeded);
        Assert.Equal([0, 1], moved.Mesh!.Weights!.Vertices);
        var extruded = MeshEdits.Extrude(mesh, [1], 1f);
        Assert.True(extruded.Succeeded, string.Join("; ", extruded.Errors));
        Assert.Equal([0, 1], extruded.Mesh!.Weights!.Vertices);
        Assert.Null(TestMeshes.Cube().Weights);
    }

    [Fact]
    public void Posed_model_moves_only_weighted_meshes_and_keeps_colours()
    {
        var weighted = Weighted([new(1, 1.5f, 0), new(0, 0, 0)], (0, 1, 1), (1, 1, 1), (2, 1, 1), (3, 1, 1))
            .WithStyle(FaceStyle.None.WithColour([0], "#C8503C"));
        var plain = TestMeshes.Cube();
        var model = Model.Of("a", weighted).With("b", plain).WithSkeleton(Chain());
        var posed = Skinning.Posed(model, Of(("joint", QuarterZ, null)));
        Assert.Same(plain, posed.Find("b"));
        Assert.Equal("#C8503C", posed.Find("a")!.Style!.ColourOf(0));
        Assert.NotEqual(weighted.Vertices[0], posed.Find("a")!.Vertices[0]);
        Assert.Null(posed.Find("a")!.Weights);
    }
}
