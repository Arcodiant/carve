using System.Numerics;
using System.Text;
using System.Text.Json;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;
using Skeleton = Carve.Core.Skeleton;
using Model = Carve.Core.Model;
using static Carve.Server.Tests.RigTestHelpers;

namespace Carve.Server.Tests;

/// <summary>
/// The clips-only export (D-6, T-46) read back through the engine's own parser: <c>ParseAnimationSet</c> gives the
/// model file's bones in its order (a breadth-first skeleton) and its clips, <c>Parse</c> refuses it for having no mesh,
/// the bytes are the same every time, and <c>ClipRetargeter</c> takes the clips onto a second Synty-named skeleton of
/// other proportions, so the file is proved as a retargeting source.
/// </summary>
[Trait("Tier", "unit")]
public class ClipsExportTests
{
    private static BoneSpec B(string name, string? parent, float x, float y, float z, Quaternion? rotation = null) =>
        new(name, parent, new Vector3(x, y, z), rotation ?? Quaternion.Identity);

    private static int Idx(Skeleton rig, string name) => rig.Names.ToList().IndexOf(name);

    /// <summary>
    /// A Synty-named humanoid, breadth-first on purpose (every bone of one depth before the next), so a loader that reorders
    /// bones depth-first would get it wrong. <paramref name="hips"/> is the hips' height and <paramref name="leg"/> each
    /// leg segment's length; the arms and spine take the other proportions.
    /// </summary>
    private static Skeleton Synty(float hips, float leg, float spine, float arm, float toe) => Skeleton.Create([
        B("Hips", null, 0, hips, 0),
        B("Spine_01", "Hips", 0, spine * 0.4f, 0),
        B("UpperLeg_L", "Hips", 0.1f, 0, 0, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.05f)),
        B("UpperLeg_R", "Hips", -0.1f, 0, 0),
        B("Clavicle_L", "Spine_01", 0.05f, spine * 0.6f, 0),
        B("Clavicle_R", "Spine_01", -0.05f, spine * 0.6f, 0),
        B("Head", "Spine_01", 0, spine * 0.8f, 0),
        B("LowerLeg_L", "UpperLeg_L", 0, -leg, 0),
        B("LowerLeg_R", "UpperLeg_R", 0, -leg, 0),
        B("Shoulder_L", "Clavicle_L", 0.1f, 0, 0),
        B("Shoulder_R", "Clavicle_R", -0.1f, 0, 0),
        B("Ankle_L", "LowerLeg_L", 0, -leg, 0),
        B("Ankle_R", "LowerLeg_R", 0, -leg, 0),
        B("Elbow_L", "Shoulder_L", arm, 0, 0),
        B("Elbow_R", "Shoulder_R", -arm, 0, 0),
        B("Hand_L", "Elbow_L", arm, 0, 0),
        B("Hand_R", "Elbow_R", -arm, 0, 0),
        B("Ball_L", "Ankle_L", 0, -0.03f, toe),
    ]).Skeleton!;

    private static readonly Skeleton Tall = Synty(1.0f, 0.5f, 0.5f, 0.28f, 0.12f);

    private static PolygonMesh Block() =>
        TestMeshes.Make(TestMeshes.CubeVertices.Select(v => new Vector3(0, 1, 0) + (v * 0.3f)), TestMeshes.CubeFaces);

    private static Quaternion Turn(Vector3 axis, float angle) => Quaternion.CreateFromAxisAngle(axis, angle);

    /// <summary>The tall character with a walk (keys away from 0 and the end, both interpolations), a wave and an empty clip.</summary>
    private static Model Character()
    {
        var walk = MakeClip("walk", 1.2f, Tall,
            new BoneChannelsSpec("UpperLeg_L", new ChannelSpec<Quaternion>("LINEAR", [new(0.2f, Turn(Vector3.UnitX, 0.4f)), new(0.8f, Turn(Vector3.UnitX, -0.4f))]), null),
            new BoneChannelsSpec("UpperLeg_R", new ChannelSpec<Quaternion>("LINEAR", [new(0f, Turn(Vector3.UnitX, -0.4f)), new(0.6f, Turn(Vector3.UnitX, 0.4f)), new(1.2f, Turn(Vector3.UnitX, -0.4f))]), null),
            new BoneChannelsSpec("Hips", null, new ChannelSpec<Vector3>("STEP", [new(0.3f, new Vector3(0, 1.02f, 0)), new(0.9f, new Vector3(0, 0.98f, 0.1f))])),
            new BoneChannelsSpec("Shoulder_L", new ChannelSpec<Quaternion>("LINEAR", [new(0.1f, Turn(Vector3.UnitZ, 0.3f))]), null));
        var wave = MakeClip("wave", 2f, Tall,
            new BoneChannelsSpec("Shoulder_R", new ChannelSpec<Quaternion>("LINEAR", [new(0f, Quaternion.Identity), new(1f, Turn(Vector3.UnitZ, 1.2f)), new(2f, Quaternion.Identity)]), null),
            new BoneChannelsSpec("Elbow_R", new ChannelSpec<Quaternion>("STEP", [new(0.5f, Turn(Vector3.UnitY, 0.5f))]), null));
        var still = MakeClip("still", 3f, Tall);
        var weights = VertexWeights.None;
        for (var v = 0; v < 8; v++) weights = weights.With([v], [new Influence(0, 0.5), new Influence(1, 0.5)]);
        return Model.Of("body", Block().WithWeights(weights)).WithSkeleton(Tall).WithClips([walk, wave, still]);
    }

    private static ParsedModel Parse(byte[] glb) => GlbExportTests.Parser.Parse(glb, ModelFormat.Gltf);

    private static ParsedAnimationSet ParseSet(byte[] glb) => GlbExportTests.Parser.ParseAnimationSet(glb);

    private static bool SameChannel<T>(AnimationChannel<T>? a, AnimationChannel<T>? b) where T : struct
    {
        if (a is null || b is null) return a is null && b is null;
        return a.Interpolation == b.Interpolation && a.Times.SequenceEqual(b.Times) && a.Values.SequenceEqual(b.Values);
    }

    [Fact]
    public void The_animation_set_has_the_model_files_bones_in_its_order_and_its_clips()
    {
        var model = Character();
        var modelFile = Parse(SkinnedExport.Write(model, "orc").Bytes);
        var set = ParseSet(ClipsExport.Write(model, "orc"));

        // Breadth-first, as written: the loader keeps the skin's order, and only the skin's.
        Assert.Equal(Tall.Names, set.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(modelFile.Skin!.Skeleton.Bones, set.Skeleton.Bones);
        Assert.Equal(modelFile.Skin.Skeleton.RootTransform, set.Skeleton.RootTransform);
        Assert.True(set.Skeleton.ParentsPrecedeChildren());

        Assert.Equal(["walk", "wave", "still"], set.Clips.Select(c => c.Name));
        Assert.Equal(modelFile.Skin.Animations.Select(a => a.Name), set.Clips.Select(c => c.Name));
        Assert.Equal([1.2f, 2f, 3f], set.Clips.Select(c => c.Duration));
        for (var c = 0; c < set.Clips.Length; c++)
        {
            var (a, b) = (modelFile.Skin.Animations[c], set.Clips[c]);
            Assert.Equal(a.Duration, b.Duration);
            Assert.Equal(Tall.Bones.Count, a.Bones.Length);
            Assert.Equal(Tall.Bones.Count, b.Bones.Length);
            for (var bone = 0; bone < a.Bones.Length; bone++)
            {
                var what = $"{a.Name}/{Tall.Bones[bone].Name}";
                Assert.True(SameChannel(a.Bones[bone].Rotation, b.Bones[bone].Rotation), what + " rotation");
                Assert.True(SameChannel(a.Bones[bone].Translation, b.Bones[bone].Translation), what + " translation");
            }
        }

        // Spot checks that the channels are the clip's, not merely equal to each other.
        var walk = set.Clips[0];
        var leg = walk.Bones[Idx(Tall, "UpperLeg_L")].Rotation!;
        Assert.Equal([0f, 0.2f, 0.8f, 1.2f], leg.Times);
        Assert.Equal(AnimationInterpolation.Linear, leg.Interpolation);
        Assert.Equal(AnimationInterpolation.Step, walk.Bones[0].Translation!.Interpolation);
    }

    [Fact]
    public void The_model_loader_refuses_the_clips_file_for_having_no_mesh()
    {
        var bytes = ClipsExport.Write(Character(), "orc");
        var thrown = Assert.ThrowsAny<Exception>(() => Parse(bytes));
        Assert.Equal("glTF contains no meshes.", thrown.Message);
    }

    [Fact]
    public void The_file_is_an_armature_a_skin_and_animations_and_nothing_else()
    {
        var json = Json(ClipsExport.Write(Character(), "orc"));
        foreach (var absent in new[] { "meshes", "materials", "textures", "images" })
            Assert.False(json.TryGetProperty(absent, out _), absent);
        var nodes = json.GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Equal(["Armature", .. Tall.Names], nodes.Select(n => n.GetProperty("name").GetString()));
        Assert.All(nodes, n =>
        {
            Assert.False(n.TryGetProperty("mesh", out _));
            Assert.False(n.TryGetProperty("skin", out _));
            Assert.False(n.TryGetProperty("scale", out _));
        });
        Assert.False(nodes[0].TryGetProperty("translation", out _));
        Assert.False(nodes[0].TryGetProperty("rotation", out _));
        Assert.Equal([0], json.GetProperty("scenes")[0].GetProperty("nodes").EnumerateArray().Select(n => n.GetInt32()));
        Assert.Equal(0, json.GetProperty("scene").GetInt32());

        // The skin stays with no mesh: its joints are the bones in order, nodes 1 to n, and it has every inverse bind.
        var skin = Assert.Single(json.GetProperty("skins").EnumerateArray());
        Assert.Equal("orc", skin.GetProperty("name").GetString());
        Assert.Equal(Enumerable.Range(1, Tall.Bones.Count), skin.GetProperty("joints").EnumerateArray().Select(j => j.GetInt32()));
        Assert.True(skin.TryGetProperty("inverseBindMatrices", out _));
        Assert.Equal(["walk", "wave", "still"], json.GetProperty("animations").EnumerateArray().Select(a => a.GetProperty("name").GetString()));
    }

    [Fact]
    public void The_same_model_writes_the_same_bytes_and_the_mesh_does_not_change_them()
    {
        var model = Character();
        var bytes = ClipsExport.Write(model, "orc");
        Assert.Equal(bytes, ClipsExport.Write(Character(), "orc"));

        // The meshes are not in the file: adding a piece writes the same bytes.
        var other = model.With("hat", Block().WithWeights(VertexWeights.None.With([0], [new Influence(2, 1)])));
        Assert.Equal(bytes, ClipsExport.Write(other, "orc"));

        // The skin is named for the file, as the model file's is.
        Assert.NotEqual(bytes, ClipsExport.Write(model, "elf"));
    }

    [Fact]
    public void A_model_with_no_skeleton_or_no_clips_is_refused()
    {
        Assert.Contains("no skeleton", ClipsExport.Refusal(Model.Of("body", Block())));
        Assert.Contains("no clips", ClipsExport.Refusal(Model.Of("body", Block()).WithSkeleton(Tall)));
        Assert.Null(ClipsExport.Refusal(Character()));
        Assert.Throws<ArgumentException>(() => ClipsExport.Write(Model.Of("body", Block()).WithSkeleton(Tall), "orc"));
    }

    [Fact]
    public void ClipRetargeter_takes_the_clips_onto_a_second_synty_skeleton_of_other_proportions()
    {
        var set = ParseSet(ClipsExport.Write(Character(), "orc"));
        var other = Synty(0.7f, 0.32f, 0.38f, 0.2f, 0.09f);
        var target = ParseSet(ClipsExport.Write(Model.Of("kid", Block()).WithSkeleton(other).WithClips([MakeClip("rest", 1f, other)]), "kid"));

        var source = HumanoidRoleMap.ResolvePreset(set.Skeleton, "orc");
        var dest = HumanoidRoleMap.ResolvePreset(target.Skeleton, "kid");
        Assert.True(source.TryGetBone(HumanoidRole.Hips, out var hips));
        Assert.Equal("Hips", set.Skeleton.Bones[hips].Name);
        Assert.NotEqual(set.Skeleton.Bones[Idx(Tall, "LowerLeg_L")].BindTranslation, target.Skeleton.Bones[Idx(other, "LowerLeg_L")].BindTranslation);

        foreach (var clip in set.Clips)
        {
            var moved = ClipRetargeter.Retarget(source, dest, clip);
            Assert.Equal(clip.Name, moved.Name);
            Assert.Equal(clip.Duration, moved.Duration);
            Assert.Equal(other.Bones.Count, moved.Bones.Length);
            foreach (var track in moved.Bones)
            {
                Assert.True(track.Rotation is null || track.Rotation.Values.All(q => float.IsFinite(q.LengthSquared()) && Math.Abs(q.Length() - 1) < 1e-3f));
                Assert.True(track.Translation is null || track.Translation.Values.All(t => float.IsFinite(t.LengthSquared())));
            }
        }

        // The walk moves the target's own leg and hips, and the hips travel in the target's proportions.
        var walk = ClipRetargeter.Retarget(source, dest, set.Clips[0]);
        Assert.NotNull(walk.Bones[Idx(other, "UpperLeg_L")].Rotation);
        Assert.True(walk.Bones[0].Translation is { } t && t.Values.Any(v => v.Y < 0.7f * 1.05f && v.Y > 0.7f * 0.9f), "hips height scaled to the target");
    }
}
