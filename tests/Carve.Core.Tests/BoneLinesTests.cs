using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>The lines that show a skeleton (T-39), against hand-worked heads.</summary>
[Trait("Tier", "unit")]
public class BoneLinesTests
{
    private static readonly Quaternion Identity = Quaternion.Identity;

    // hips (0,1,0); spine 2 up from it; armL and armR each 1 to the side of the spine; hand 1 on from armL.
    private static Skeleton Rig() => Skeleton.Create([
        new("hips", null, new Vector3(0, 1, 0), Identity),
        new("spine", "hips", new Vector3(0, 2, 0), Identity),
        new("armL", "spine", new Vector3(-1, 0, 0), Identity),
        new("armR", "spine", new Vector3(1, 0, 0), Identity),
        new("hand", "armL", new Vector3(-1, 0, 0), Identity),
    ]).Skeleton!;

    [Fact]
    public void Heads_at_rest_are_the_rest_world_positions()
    {
        var rig = Rig();
        var heads = Skinning.Heads(rig, Pose.Rest);
        for (var i = 0; i < heads.Length; i++) Assert.Equal(rig.RestWorldPosition(i), heads[i]);
    }

    [Fact]
    public void A_parent_turned_carries_its_children_heads()
    {
        var turned = new Pose(new Dictionary<string, (Quaternion?, Vector3?)>
        {
            ["spine"] = (Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2), null),
        });
        var heads = Skinning.Heads(Rig(), turned);
        Assert.Equal(new Vector3(0, 3, 0), heads[1]);
        // armL's local (-1, 0, 0) turned a quarter about +Z is (0, -1, 0): below the spine's head.
        Assert.True(Vector3.Distance(new Vector3(0, 2, 0), heads[2]) < 1e-5f);
    }

    [Fact]
    public void A_bone_is_a_line_to_each_child_and_a_leaf_runs_on_for_half_its_parents_length()
    {
        var rig = Rig();
        var segments = BoneLines.Segments(rig, Skinning.Heads(rig, Pose.Rest));
        Assert.Equal(
        [
            (0, new Vector3(0, 1, 0), new Vector3(0, 3, 0)),
            (1, new Vector3(0, 3, 0), new Vector3(-1, 3, 0)),
            (1, new Vector3(0, 3, 0), new Vector3(1, 3, 0)),
            (2, new Vector3(-1, 3, 0), new Vector3(-2, 3, 0)),
            // armR: parent spine's head to armR's is (1, 0, 0), so the leaf runs on half of it.
            (3, new Vector3(1, 3, 0), new Vector3(1.5f, 3, 0)),
            // hand: its parent armL's direction is (-1, 0, 0), half is 0.5.
            (4, new Vector3(-2, 3, 0), new Vector3(-2.5f, 3, 0)),
        ], segments.Select(s => (s.Bone, s.From, s.To)));
        Assert.Equal([false, false, false, false, true, true], segments.Select(s => s.Leaf));
    }

    [Fact]
    public void A_lone_bone_has_a_head_and_no_line()
    {
        var rig = Skeleton.Create([new("only", null, Vector3.One, Identity)]).Skeleton!;
        Assert.Empty(BoneLines.Segments(rig, Skinning.Heads(rig, Pose.Rest)));
    }
}
