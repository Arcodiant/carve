using System.Globalization;
using System.Numerics;
using Carve.Core;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>RV-24 B5/D7: the weight, clip and skeleton refusals write numbers the same on every machine.</summary>
[Trait("Tier", "unit")]
public class RefusalCultureTests
{
    private static T InGerman<T>(Func<T> act)
    {
        var was = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try { return act(); }
        finally { CultureInfo.CurrentCulture = was; }
    }

    private static Skeleton Two() => Skeleton.Create([
        new BoneSpec("a", null, Vector3.Zero, Quaternion.Identity),
        new BoneSpec("b", "a", Vector3.UnitY, Quaternion.Identity)]).Skeleton!;

    [Fact]
    public void A_bad_weight_is_written_with_a_point()
    {
        var errors = InGerman(() =>
        {
            var list = new List<MeshError>();
            VertexWeights.Raw(Two(), [KeyValuePair.Create("a", -0.5)], "weights", list);
            return list;
        });
        Assert.Equal("weights.a: -0.5 is not a weight; weights are 0 or more", Assert.Single(errors).ToString());
    }

    [Fact]
    public void A_bad_duration_and_key_times_are_written_with_a_point()
    {
        var (_, errors) = InGerman(() => Clip.Create("wave", 1.5f, false,
            [new BoneChannelsSpec("b", new ChannelSpec<Quaternion>(null,
                [new ClipKey<Quaternion>(0.5f, Quaternion.Identity), new ClipKey<Quaternion>(0.25f, Quaternion.Identity), new ClipKey<Quaternion>(2.5f, new Quaternion(0, 0, 0, 1.5f))]), null)],
            Two()));
        var text = string.Join("\n", errors);
        Assert.Contains("time 0.25 comes before the key before it (0.5)", text);
        Assert.Contains("time 2.5 is outside the clip, which runs from 0 to 1.5", text);
        Assert.Contains("its length is 1.5, and it must be 1 (within 0.001)", text);
        Assert.DoesNotContain(",5", text);

        var (_, bad) = InGerman(() => Clip.Create("wave", -0.5f, false, [], Two()));
        Assert.Contains("duration: -0.5 seconds: a duration must be greater than 0", bad.Select(e => e.ToString()));
    }

    [Fact]
    public void A_bone_rotation_length_is_written_with_a_point()
    {
        var (_, errors) = InGerman(() => Skeleton.Create([new BoneSpec("a", null, Vector3.Zero, new Quaternion(0, 0, 0, 1.5f))]));
        Assert.Equal("bones[0]: rotation is not a unit quaternion: its length is 1.5, and it must be 1 (within 0.001)", Assert.Single(errors).ToString());
    }
}
