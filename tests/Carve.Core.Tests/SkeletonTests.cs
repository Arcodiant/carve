using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>The skeleton as given (D-6): order and names kept, every refusal, the 255 limit, the model keeping it.</summary>
[Trait("Tier", "unit")]
public class SkeletonTests
{
    private static BoneSpec Bone(string name, string? parent = null, float y = 0) =>
        new(name, parent, new Vector3(0, y, 0), Quaternion.Identity);

    private static List<string> Errors(params BoneSpec[] bones) =>
        Skeleton.Create(bones).Errors.Select(e => e.ToString()).ToList();

    private static List<BoneSpec> Chain(int count) =>
        Enumerable.Range(0, count).Select(i => Bone($"b{i}", i == 0 ? null : $"b{i - 1}")).ToList();

    [Fact]
    public void Order_names_parents_and_local_transforms_round_trip()
    {
        var q = Quaternion.Normalize(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f));
        var (skeleton, errors) = Skeleton.Create([
            new("mixamorig:Hips", null, new Vector3(1, 2, 3), q),
            Bone("spine", "mixamorig:Hips", 0.5f),
            Bone("a b", "spine"),
            Bone("arm", "mixamorig:Hips"),
        ]);
        Assert.Empty(errors);
        Assert.Equal(["mixamorig:Hips", "spine", "a b", "arm"], skeleton!.Names);
        Assert.Equal([-1, 0, 1, 0], skeleton.Bones.Select(b => b.Parent));
        Assert.Equal(new Vector3(1, 2, 3), skeleton.Bones[0].Position);
        Assert.Equal(q, skeleton.Bones[0].Rotation);
        Assert.Equal(0.5f, skeleton.Bones[1].Position.Y);
    }

    [Fact]
    public void Rest_world_position_under_two_turned_bones_applies_the_parents_turn_after_the_childs()
    {
        // a turns a quarter about Z, b (at (0, 1, 0) from a, so (-1, 0, 0)) a quarter about X. c sits (0, 1, 0) from b:
        // b's own turn takes that to (0, 0, 1), a's leaves it there, so c rests at (-1, 0, 1). The reversed product
        // (a's turn first) would put c at (-2, 0, 0).
        var (s, _) = Skeleton.Create([
            new("a", null, Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2)),
            new("b", "a", new Vector3(0, 1, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2)),
            Bone("c", "b", 1),
        ]);
        var c = s!.RestWorldPosition(2);
        Assert.Equal(-1f, c.X, 5);
        Assert.Equal(0f, c.Y, 5);
        Assert.Equal(1f, c.Z, 5);
    }

    [Fact]
    public void Rest_world_position_adds_the_parents_chain_and_turns_by_its_rotation()
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
        var (s, _) = Skeleton.Create([
            new("hips", null, new Vector3(0, 1, 0), Quaternion.Identity),
            Bone("spine", "hips", 0.3f),
            Bone("head", "spine", 0.4f),
            new("turned", "hips", new Vector3(0, 0, 0), turn),
            new("tip", "turned", new Vector3(1, 0, 0), Quaternion.Identity),
        ]);
        Assert.Equal(1.7f, s!.RestWorldPosition(2).Y, 5);
        Assert.Equal(2f, s.RestWorldPosition(4).Y, 5);
        Assert.Equal(0f, s.RestWorldPosition(4).X, 5);
    }

    [Fact]
    public void Two_hundred_fifty_five_bones_are_accepted_and_256_refused()
    {
        Assert.Empty(Errors([.. Chain(255)]));
        var errors = Errors([.. Chain(256)]);
        Assert.Single(errors);
        Assert.Contains("256", errors[0]);
        Assert.Contains("255", errors[0]);
    }

    [Fact]
    public void No_bones_is_refused()
    {
        Assert.Contains(Errors(), e => e.StartsWith("bones:"));
    }

    [Fact]
    public void An_empty_name_is_refused_naming_the_bone()
    {
        var errors = Errors(Bone("a"), Bone(""));
        Assert.Single(errors);
        Assert.StartsWith("bones[1]:", errors[0]);
    }

    [Fact]
    public void A_duplicate_name_is_refused_naming_the_later_bone()
    {
        var errors = Errors(Bone("a"), Bone("b", "a"), Bone("a"));
        Assert.Single(errors);
        Assert.StartsWith("bones[2]:", errors[0]);
    }

    [Fact]
    public void Names_are_case_sensitive()
    {
        Assert.Empty(Errors(Bone("a"), Bone("A")));
    }

    [Fact]
    public void A_parent_after_its_child_is_refused()
    {
        var errors = Errors(Bone("a", "b"), Bone("b"));
        Assert.Single(errors);
        Assert.StartsWith("bones[0]:", errors[0]);
        Assert.Contains("before", errors[0]);
    }

    [Fact]
    public void A_bone_that_is_its_own_parent_is_refused()
    {
        Assert.Single(Errors(Bone("a", "a")));
    }

    [Fact]
    public void An_unknown_parent_is_refused()
    {
        var errors = Errors(Bone("a"), Bone("b", "nobody"));
        Assert.Single(errors);
        Assert.StartsWith("bones[1]:", errors[0]);
        Assert.Contains("nobody", errors[0]);
    }

    [Theory]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(0f, 0f, 0f, 2f)]
    [InlineData(1f, 1f, 0f, 0f)]
    [InlineData(0f, 0f, 0f, 1.01f)]
    [InlineData(0f, 0f, 0f, float.NaN)]
    public void A_rotation_that_is_not_a_unit_quaternion_is_refused(float x, float y, float z, float w)
    {
        var errors = Errors(new BoneSpec("a", null, Vector3.Zero, new Quaternion(x, y, z, w)));
        Assert.Single(errors);
        Assert.StartsWith("bones[0]:", errors[0]);
        Assert.Contains("rotation", errors[0]);
    }

    [Fact]
    public void A_rotation_within_tolerance_of_unit_is_accepted()
    {
        Assert.Empty(Errors(new BoneSpec("a", null, Vector3.Zero, new Quaternion(0, 0, 0, 1.0005f))));
    }

    [Fact]
    public void A_non_finite_position_is_refused()
    {
        Assert.Single(Errors(new BoneSpec("a", null, new Vector3(float.PositiveInfinity, 0, 0), Quaternion.Identity)));
    }

    [Fact]
    public void Every_problem_is_listed_at_once()
    {
        var errors = Errors(Bone(""), Bone("a", "zz"), Bone("a"), new BoneSpec("c", null, Vector3.Zero, new Quaternion(0, 0, 0, 3)));
        Assert.Equal(4, errors.Count);
        for (var i = 0; i < 4; i++) Assert.StartsWith($"bones[{i}]:", errors[i]);
    }

    [Fact]
    public void The_model_keeps_its_skeleton_through_every_change_to_its_meshes()
    {
        var (s, _) = Skeleton.Create([Bone("a")]);
        var tri = TestMeshes.Make([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)], [[0, 1, 2]]);
        var model = Model.Empty.WithSkeleton(s!);
        Assert.Null(Model.Empty.Skeleton);
        Assert.Same(s, model.With("body", tri).Skeleton);
        Assert.Same(s, model.With("body", tri).With("body", tri).With("coat", tri).Skeleton);
        Assert.Same(s, Model.Of("x", tri).WithSkeleton(s!).With("x", tri).Skeleton);
        Assert.Equal(["body"], model.With("body", tri).Names);
    }

    [Fact]
    public void Same_names_compares_names_and_order()
    {
        var (a, _) = Skeleton.Create([Bone("a"), Bone("b", "a")]);
        var (same, _) = Skeleton.Create([Bone("a"), Bone("b", "a", 5)]);
        var (swapped, _) = Skeleton.Create([Bone("b"), Bone("a", "b")]);
        Assert.True(a!.SameNamesAs(same));
        Assert.False(a.SameNamesAs(swapped));
        Assert.False(a.SameNamesAs(null));
    }
}
