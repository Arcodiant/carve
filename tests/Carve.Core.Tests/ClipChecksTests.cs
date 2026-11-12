using System.Numerics;
using Carve.Core;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class ClipChecksTests
{
    private static readonly Quaternion QuarterZ = new(0, 0, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));

    // A root (hips) with a child (spine).
    private static Skeleton Rig() => Skeleton.Create([
        new BoneSpec("hips", null, Vector3.Zero, Quaternion.Identity),
        new BoneSpec("spine", "hips", new Vector3(0, 1, 0), Quaternion.Identity),
    ]).Skeleton!;

    private static BoneChannelsSpec Rot(string bone, params Quaternion[] ends) => new(bone,
        new ChannelSpec<Quaternion>(null, [new ClipKey<Quaternion>(0, ends[0]), new ClipKey<Quaternion>(1, ends[1])]), null);

    private static BoneChannelsSpec Move(string bone, Vector3 start, Vector3 end) => new(bone, null,
        new ChannelSpec<Vector3>(null, [new ClipKey<Vector3>(0, start), new ClipKey<Vector3>(1, end)]));

    private static IReadOnlyList<string> Warnings(bool loop, params BoneChannelsSpec[] channels)
    {
        var rig = Rig();
        var (clip, errors) = Clip.Create("walk", 1, loop, channels, rig);
        Assert.Empty(errors);
        return ClipChecks.Check(clip!, rig);
    }

    [Fact]
    public void A_clean_looping_clip_has_no_warning() =>
        Assert.Empty(Warnings(true, Rot("spine", Quaternion.Identity, Quaternion.Identity), Move("hips", new Vector3(0, 1, 0), new Vector3(0, 1.2f, 0))));

    [Fact]
    public void A_rotation_equal_up_to_sign_counts_as_closed() =>
        Assert.Empty(Warnings(true, Rot("spine", QuarterZ, -QuarterZ)));

    [Fact]
    public void A_looping_rotation_that_ends_elsewhere_names_the_bone_and_the_difference()
    {
        var w = Assert.Single(Warnings(true, Rot("spine", Quaternion.Identity, QuarterZ)));
        Assert.Contains("'walk'", w);
        Assert.Contains("'spine' rotation", w);
        Assert.Contains("90 degrees", w);
    }

    [Fact]
    public void A_rotation_within_a_degree_is_closed_and_beyond_it_is_not()
    {
        static Quaternion About(float degrees) => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, degrees * MathF.PI / 180);
        Assert.Empty(Warnings(true, Rot("spine", Quaternion.Identity, About(0.9f))));
        Assert.Single(Warnings(true, Rot("spine", Quaternion.Identity, About(1.2f))));
    }

    [Fact]
    public void A_non_looping_rotation_may_end_elsewhere() =>
        Assert.Empty(Warnings(false, Rot("spine", Quaternion.Identity, QuarterZ)));

    [Fact]
    public void A_looping_root_that_ends_elsewhere_warns_as_a_loop_and_as_travel()
    {
        var w = Warnings(true, Move("hips", Vector3.Zero, new Vector3(0, 0, 0.5f)));
        Assert.Equal(2, w.Count);
        Assert.Contains(w, x => x.Contains("loops") && x.Contains("'hips' translation") && x.Contains("500 mm"));
        Assert.Contains(w, x => x.Contains("root bone 'hips'") && x.Contains("in place"));
    }

    [Fact]
    public void A_root_that_travels_warns_even_without_a_loop()
    {
        var w = Assert.Single(Warnings(false, Move("hips", Vector3.Zero, new Vector3(0.3f, 0, 0))));
        Assert.Contains("root bone 'hips'", w);
    }

    [Fact]
    public void A_root_moving_up_or_within_a_millimetre_is_in_place()
    {
        Assert.Empty(Warnings(false, Move("hips", Vector3.Zero, new Vector3(0, 0.5f, 0))));
        Assert.Empty(Warnings(false, Move("hips", Vector3.Zero, new Vector3(0.0009f, 0, 0))));
        Assert.Single(Warnings(false, Move("hips", Vector3.Zero, new Vector3(0.0012f, 0, 0))));
    }

    [Fact]
    public void A_child_bone_moving_is_not_travel() =>
        Assert.Empty(Warnings(false, Move("spine", new Vector3(0, 1, 0), new Vector3(0.4f, 1, 0))));
}
