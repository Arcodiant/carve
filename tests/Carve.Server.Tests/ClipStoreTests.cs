using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>Clips in the store: one undo step each, and T-31's rule, clips half: no renaming or reordering bones once a clip exists.</summary>
[Trait("Tier", "unit")]
public class ClipStoreTests
{
    private static Skeleton Bones(params string[] names) => Skeleton.Create(names.Select((n, i) =>
        new BoneSpec(n, i == 0 ? null : names[i - 1], new Vector3(0, 1, 0), Quaternion.Identity)).ToList()).Skeleton!;

    private static Func<Skeleton, (Clip?, IReadOnlyList<MeshError>)> Wave(string name, string bone = "b") => skeleton =>
        Clip.Create(name, 1, true, [new BoneChannelsSpec(bone, new ChannelSpec<Quaternion>(null, [new ClipKey<Quaternion>(0, Quaternion.Identity)]), null)], skeleton);

    /// <summary>A mesh with no weights, a skeleton a, b, and one clip: only the clip holds the bones.</summary>
    private static ModelStore Clipped(out ModelSnapshot snapshot)
    {
        var store = new ModelStore();
        store.Define("mesh", TestMeshes.Cube(), "define_mesh");
        store.DefineSkeleton(Bones("a", "b"), "define_skeleton", out _);
        snapshot = store.DefineClip("define_clip", Wave("wave"), out _, out _, out var problems)!;
        Assert.Empty(problems);
        return store;
    }

    [Fact]
    public void Renaming_reordering_or_adding_bones_is_refused_once_a_clip_exists_and_nothing_changes()
    {
        var store = Clipped(out var before);
        foreach (var other in new[] { Bones("a", "c"), Bones("b", "a"), Bones("a", "b", "c"), Bones("a") })
        {
            Assert.Null(store.DefineSkeleton(other, "define_skeleton", out var refusal));
            Assert.Contains("names and order", refusal);
            Assert.Contains("'wave'", refusal);
            Assert.Same(before, store.Current);
        }
        Assert.NotNull(store.DefineSkeleton(Skeleton.Create([new BoneSpec("a", null, Vector3.One, Quaternion.Identity), new BoneSpec("b", "a", Vector3.Zero, Quaternion.Identity)]).Skeleton!, "define_skeleton", out _));
        Assert.Single(store.Current.Model.Clips);
    }

    [Fact]
    public void Deleting_the_last_clip_lets_the_skeleton_change_again()
    {
        var store = Clipped(out _);
        Assert.NotNull(store.DeleteClip("delete_clip", "wave", out _));
        Assert.NotNull(store.DefineSkeleton(Bones("x"), "define_skeleton", out _));
    }

    [Fact]
    public void A_repeated_name_replaces_the_clip_and_each_change_is_one_undo_step()
    {
        var store = Clipped(out var first);
        Assert.NotNull(store.DefineClip("define_clip", Wave("wave", "a"), out var clip, out var replaced, out _));
        Assert.True(replaced);
        Assert.Equal("a", Assert.Single(Assert.Single(store.Current.Model.Clips).Channels).Bone);
        Assert.NotNull(store.DefineClip("define_clip", Wave("other"), out _, out replaced, out _));
        Assert.False(replaced);
        Assert.Equal(["wave", "other"], store.Current.Model.Clips.Select(c => c.Name));

        Assert.Equal("define_clip", store.Undo()!.Undone);
        Assert.Equal("define_clip", store.Undo()!.Undone);
        Assert.Same(first.Model.Clips[0], Assert.Single(store.Current.Model.Clips));
    }

    [Fact]
    public void A_clip_needs_a_skeleton_and_a_refused_clip_or_unknown_delete_changes_nothing()
    {
        var empty = new ModelStore();
        empty.Define("mesh", TestMeshes.Cube(), "define_mesh");
        Assert.Null(empty.DefineClip("define_clip", Wave("wave"), out _, out _, out var problems));
        Assert.Contains(problems, p => p.Contains("define_skeleton"));

        var store = Clipped(out var before);
        Assert.Null(store.DefineClip("define_clip", Wave("wave", "elbow"), out _, out _, out problems));
        Assert.Contains(problems, p => p.Contains("channels.elbow"));
        Assert.Null(store.DeleteClip("delete_clip", "walk", out var refusal));
        Assert.Contains("'wave'", refusal);
        Assert.Same(before, store.Current);
    }
}
