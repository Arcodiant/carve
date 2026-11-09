using System.Numerics;
using Carve.Core;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class ClipTests
{
    private static readonly Quaternion QuarterZ = new(0, 0, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));

    private static Skeleton Chain() => Skeleton.Create([
        new BoneSpec("base", null, Vector3.Zero, Quaternion.Identity),
        new BoneSpec("joint", "base", new Vector3(0, 1, 0), Quaternion.Identity),
    ]).Skeleton!;

    private static ChannelSpec<Quaternion> Rot(string? interpolation, params (float T, Quaternion V)[] keys) =>
        new(interpolation, keys.Select(k => new ClipKey<Quaternion>(k.T, k.V)).ToArray());

    private static ChannelSpec<Vector3> Move(string? interpolation, params (float T, Vector3 V)[] keys) =>
        new(interpolation, keys.Select(k => new ClipKey<Vector3>(k.T, k.V)).ToArray());

    private static Clip Make(float duration, params BoneChannelsSpec[] channels)
    {
        var (clip, errors) = Clip.Create("c", duration, false, channels, Chain());
        Assert.Empty(errors);
        return clip!;
    }

    private static IReadOnlyList<MeshError> Refused(string name, float duration, params BoneChannelsSpec[] channels)
    {
        var (clip, errors) = Clip.Create(name, duration, false, channels, Chain());
        Assert.Null(clip);
        Assert.NotEmpty(errors);
        return errors;
    }

    private static void Near(Quaternion expected, Quaternion? actual)
    {
        Assert.NotNull(actual);
        Assert.True(MathF.Abs(Quaternion.Dot(expected, actual!.Value)) > 1 - 1e-6f, $"expected {expected}, got {actual}");
    }

    // The wave: rest, a quarter turn about +Z at 0.5 s, rest again at 1 s.
    private static Clip Wave(string? interpolation = "LINEAR") => Make(1,
        new BoneChannelsSpec("joint", Rot(interpolation, (0, Quaternion.Identity), (0.5f, QuarterZ), (1, Quaternion.Identity)), null));

    [Fact]
    public void Linear_rotation_is_each_key_at_its_time_and_a_slerp_between()
    {
        var wave = Wave();
        Assert.Equal(Quaternion.Identity, wave.Sample(0).RotationOf("joint"));
        Assert.Equal(QuarterZ, wave.Sample(0.5f).RotationOf("joint"));
        Assert.Equal(Quaternion.Identity, wave.Sample(1).RotationOf("joint"));
        // A quarter of the way through: an eighth of a turn about +Z, unit length.
        var eighth = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4);
        Near(eighth, wave.Sample(0.25f).RotationOf("joint"));
        Near(eighth, wave.Sample(0.75f).RotationOf("joint"));
        var tenth = wave.Sample(0.1f).RotationOf("joint")!.Value;
        Near(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2 * 0.2f), tenth);
        Assert.Equal(1, tenth.Length(), 5);
    }

    [Fact]
    public void Linear_rotation_takes_the_shortest_arc_whatever_the_sign_of_the_later_key()
    {
        // -QuarterZ is the same turn as QuarterZ; the way there must be the same eighth turn, not three eighths the other way.
        var clip = Make(1, new BoneChannelsSpec("joint", Rot("LINEAR", (0, Quaternion.Identity), (1, Quaternion.Negate(QuarterZ))), null));
        var half = clip.Sample(0.5f).RotationOf("joint")!.Value;
        Near(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4), half);
        var turned = Vector3.Transform(Vector3.UnitY, half);
        Assert.True(turned.X < -0.7f && turned.Y > 0.7f, $"up turned by the half-way rotation should point up and left, got {turned}");
    }

    [Fact]
    public void Linear_translation_lerps_and_holds_the_end_keys_outside_them()
    {
        var clip = Make(2, new BoneChannelsSpec("base", null, Move(null, (0.5f, new Vector3(0, 0, 0)), (1.5f, new Vector3(2, 4, -2)))));
        Assert.Equal(new Vector3(0, 0, 0), clip.Sample(0).TranslationOf("base"));
        Assert.Equal(new Vector3(0, 0, 0), clip.Sample(0.5f).TranslationOf("base"));
        Assert.Equal(new Vector3(0.5f, 1, -0.5f), clip.Sample(0.75f).TranslationOf("base"));
        Assert.Equal(new Vector3(2, 4, -2), clip.Sample(1.5f).TranslationOf("base"));
        Assert.Equal(new Vector3(2, 4, -2), clip.Sample(2).TranslationOf("base"));
        // A time outside the clip is clamped to it, with no wrap.
        Assert.Equal(new Vector3(2, 4, -2), clip.Sample(7).TranslationOf("base"));
        Assert.Equal(new Vector3(0, 0, 0), clip.Sample(-1).TranslationOf("base"));
    }

    [Fact]
    public void Step_holds_the_earlier_key_until_the_time_reaches_the_next()
    {
        var wave = Wave("STEP");
        Assert.Equal(Quaternion.Identity, wave.Sample(0).RotationOf("joint"));
        Assert.Equal(Quaternion.Identity, wave.Sample(0.25f).RotationOf("joint"));
        Assert.Equal(Quaternion.Identity, wave.Sample(0.4999f).RotationOf("joint"));
        Assert.Equal(QuarterZ, wave.Sample(0.5f).RotationOf("joint"));
        Assert.Equal(QuarterZ, wave.Sample(0.9f).RotationOf("joint"));
        Assert.Equal(Quaternion.Identity, wave.Sample(1).RotationOf("joint"));

        var moved = Make(1, new BoneChannelsSpec("base", null, Move("STEP", (0, Vector3.Zero), (0.5f, Vector3.One))));
        Assert.Equal(Vector3.Zero, moved.Sample(0.49f).TranslationOf("base"));
        Assert.Equal(Vector3.One, moved.Sample(0.5f).TranslationOf("base"));
    }

    [Fact]
    public void A_bone_or_part_with_no_keys_holds_rest()
    {
        var wave = Wave();
        var pose = wave.Sample(0.25f);
        Assert.DoesNotContain("base", pose.Names);
        Assert.Null(pose.RotationOf("base"));
        Assert.Null(pose.TranslationOf("base"));
        Assert.Null(pose.TranslationOf("joint"));
        Assert.NotNull(pose.RotationOf("joint"));
    }

    [Fact]
    public void A_single_key_holds_for_the_whole_clip()
    {
        var clip = Make(1, new BoneChannelsSpec("joint", Rot(null, (0.5f, QuarterZ)), null));
        Assert.Equal(QuarterZ, clip.Sample(0).RotationOf("joint"));
        Assert.Equal(QuarterZ, clip.Sample(1).RotationOf("joint"));
    }

    [Fact]
    public void An_interpolation_other_than_LINEAR_or_STEP_is_refused()
    {
        foreach (var bad in new[] { "CUBICSPLINE", "linear", "" })
        {
            var errors = Refused("c", 1, new BoneChannelsSpec("joint", Rot(bad, (0, Quaternion.Identity)), null));
            Assert.Contains(errors, e => e.Element == "channels.joint.rotation.interpolation" && e.Message.Contains("LINEAR or STEP"));
        }
    }

    [Fact]
    public void An_unknown_bone_is_refused_and_the_bones_are_listed()
    {
        var errors = Refused("c", 1, new BoneChannelsSpec("elbow", Rot(null, (0, Quaternion.Identity)), null));
        Assert.Contains(errors, e => e.Element == "channels.elbow" && e.Message.Contains("'base', 'joint'"));
    }

    [Fact]
    public void A_key_outside_the_clip_is_refused()
    {
        Assert.Contains(Refused("c", 1, new BoneChannelsSpec("joint", Rot(null, (0, Quaternion.Identity), (1.5f, QuarterZ)), null)),
            e => e.Element == "channels.joint.rotation.keys[1]" && e.Message.Contains("outside"));
        Assert.Contains(Refused("c", 1, new BoneChannelsSpec("base", null, Move(null, (-0.1f, Vector3.Zero)))),
            e => e.Element == "channels.base.translation.keys[0]" && e.Message.Contains("outside"));
    }

    [Fact]
    public void Duplicate_or_unsorted_key_times_are_refused()
    {
        Assert.Contains(Refused("c", 1, new BoneChannelsSpec("joint", Rot(null, (0.5f, Quaternion.Identity), (0.5f, QuarterZ)), null)),
            e => e.Element == "channels.joint.rotation.keys[1]" && e.Message.Contains("same"));
        Assert.Contains(Refused("c", 1, new BoneChannelsSpec("base", null, Move(null, (0.6f, Vector3.Zero), (0.2f, Vector3.One)))),
            e => e.Element == "channels.base.translation.keys[1]" && e.Message.Contains("increasing"));
    }

    [Fact]
    public void A_duration_not_greater_than_zero_is_refused()
    {
        foreach (var d in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Assert.Contains(Refused("c", d, new BoneChannelsSpec("joint", Rot(null, (0, Quaternion.Identity)), null)), e => e.Element == "duration");
    }

    [Fact]
    public void A_duplicate_clip_name_is_refused_and_a_repeated_name_replaces_in_its_place()
    {
        var first = Make(1, new BoneChannelsSpec("joint", Rot(null, (0, QuarterZ)), null));
        var (other, _) = Clip.Create("d", 2, true, [new BoneChannelsSpec("base", null, Move(null, (0, Vector3.One)))], Chain());
        var (again, _) = Clip.Create("c", 3, false, [new BoneChannelsSpec("base", null, Move(null, (0, Vector3.One)))], Chain());
        var ex = Assert.Throws<ArgumentException>(() => Model.Empty.WithClips([first, other!, again!]));
        Assert.Contains("'c'", ex.Message);

        var model = Model.Empty.WithClip(first).WithClip(other!).WithClip(again!);
        Assert.Equal(["c", "d"], model.Clips.Select(c => c.Name));
        Assert.Same(again, model.FindClip("c"));
        Assert.Equal(["d"], model.WithoutClip("c").Clips.Select(c => c.Name));
    }

    [Fact]
    public void Other_bad_input_is_refused_by_place()
    {
        Assert.Contains(Refused("", 1, new BoneChannelsSpec("joint", Rot(null, (0, Quaternion.Identity)), null)), e => e.Element == "name");
        Assert.Contains(Refused("c", 1, new BoneChannelsSpec("joint", Rot(null, (0, new Quaternion(0, 0, 0, 2))), null)),
            e => e.Element == "channels.joint.rotation.keys[0]" && e.Message.Contains("unit quaternion"));
        Assert.Contains(Refused("c", 1, new BoneChannelsSpec("joint", Rot(null), null)), e => e.Element == "channels.joint.rotation.keys");
        Assert.Contains(Refused("c", 1, new BoneChannelsSpec("joint", null, null)), e => e.Element == "channels.joint");
        Assert.Contains(Refused("c", 1,
            new BoneChannelsSpec("joint", Rot(null, (0, Quaternion.Identity)), null),
            new BoneChannelsSpec("joint", null, Move(null, (0, Vector3.One)))), e => e.Element == "channels.joint" && e.Message.Contains("twice"));
    }

    [Fact]
    public void Clips_survive_mesh_and_skeleton_changes()
    {
        var clip = Wave();
        var model = Model.Of("m", TestMeshes.Cube()).WithClip(clip).WithSkeleton(Chain()).With("n", TestMeshes.Cube());
        Assert.Same(clip, Assert.Single(model.Clips));
    }
}
