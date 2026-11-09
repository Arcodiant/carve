using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using static Carve.Server.Tests.RigTestHelpers;

namespace Carve.Server.Tests;

/// <summary>T-31's rule, weights half: a skeleton that changes bone names or order once weights exist is refused, under the store's lock.</summary>
[Trait("Tier", "unit")]
public class WeightsStoreTests
{
    private static ModelStore Weighted(out ModelSnapshot? snapshot)
    {
        var store = new ModelStore();
        store.Define("mesh", TestMeshes.Cube(), "define_mesh");
        store.DefineSkeleton(Bones("a", "b"), "define_skeleton", out _);
        snapshot = store.SetWeights("set_weights", null, m => ([0, 1], []), new Dictionary<string, double> { ["b"] = 1 }, out _, out _);
        return store;
    }

    [Fact]
    public void Renaming_reordering_or_adding_bones_is_refused_once_weights_exist_and_nothing_changes()
    {
        var store = Weighted(out var before);
        foreach (var other in new[] { Bones("a", "c"), Bones("b", "a"), Bones("a", "b", "c"), Bones("a") })
        {
            Assert.Null(store.DefineSkeleton(other, "define_skeleton", out var refusal));
            Assert.Contains("names and order", refusal);
            Assert.Contains("'mesh'", refusal);
            Assert.Same(before, store.Current);
        }
    }

    [Fact]
    public void The_same_names_in_the_same_order_are_accepted_with_new_rest_transforms_and_weights_stay()
    {
        var store = Weighted(out _);
        var moved = Skeleton.Create([
            new("a", null, new Vector3(0, 0.5f, 0), Quaternion.Identity),
            new("b", "a", new Vector3(0, 2, 0), Quaternion.Identity),
        ]).Skeleton!;
        var snapshot = store.DefineSkeleton(moved, "define_skeleton", out var refusal);
        Assert.Null(refusal);
        Assert.NotNull(snapshot);
        Assert.Equal([0, 1], snapshot.Model.Find("mesh")!.Weights!.Vertices);
    }

    [Fact]
    public void Without_weights_any_skeleton_replaces_any_and_defining_the_mesh_again_drops_the_weights()
    {
        var store = new ModelStore();
        store.Define("mesh", TestMeshes.Cube(), "define_mesh");
        store.DefineSkeleton(Bones("a", "b"), "define_skeleton", out _);
        Assert.NotNull(store.DefineSkeleton(Bones("x"), "define_skeleton", out _));

        var weighted = Weighted(out _);
        weighted.Define("mesh", TestMeshes.Cube(), "define_mesh");
        Assert.NotNull(weighted.DefineSkeleton(Bones("x"), "define_skeleton", out var refusal));
        Assert.Null(refusal);
    }

    [Fact]
    public void An_undone_weights_step_lets_the_skeleton_change_again()
    {
        var store = Weighted(out _);
        store.Undo();
        Assert.NotNull(store.DefineSkeleton(Bones("x", "y"), "define_skeleton", out _));
    }

    [Fact]
    public void Set_weights_counts_capped_and_unweighted_vertices_and_refuses_unknown_bones_and_ids()
    {
        var store = new ModelStore();
        store.Define("mesh", TestMeshes.Cube(), "define_mesh");
        store.DefineSkeleton(Bones("a", "b", "c", "d", "e"), "define_skeleton", out _);
        var all = new Dictionary<string, double> { ["a"] = 1, ["b"] = 2, ["c"] = 3, ["d"] = 4, ["e"] = 5 };
        var done = store.SetWeights("set_weights", null, m => ([0, 1, 2], []), all, out var change, out var problems);
        Assert.NotNull(done);
        Assert.Empty(problems);
        Assert.Equal(3, change!.Set);
        Assert.Equal(3, change.Capped);
        Assert.Equal(5, change.Unweighted);
        Assert.Equal(4, change.Influences.Count);

        var version = store.Current.Version;
        Assert.Null(store.SetWeights("set_weights", null, m => ([0], []), new Dictionary<string, double> { ["zz"] = 1 }, out _, out problems));
        Assert.Contains("no bone named 'zz'", problems.Single());
        Assert.Null(store.SetWeights("set_weights", null, m => ([0], []), new Dictionary<string, double> { ["a"] = -1 }, out _, out problems));
        Assert.Null(store.SetWeights("set_weights", null, m => ([0], []), new Dictionary<string, double> { ["a"] = 0 }, out _, out problems));
        Assert.Equal(version, store.Current.Version);
    }

    [Fact]
    public void Set_weights_needs_a_skeleton()
    {
        var store = new ModelStore();
        store.Define("mesh", TestMeshes.Cube(), "define_mesh");
        Assert.Null(store.SetWeights("set_weights", null, m => ([0], []), new Dictionary<string, double> { ["a"] = 1 }, out _, out var problems));
        Assert.Contains("no skeleton", problems.Single());
    }
}
