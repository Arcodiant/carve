using System.Numerics;
using System.Text;
using System.Text.Json;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using SharpGLTF.Schema2;
using PolygonMesh = Carve.Core.PolygonMesh;
using Skeleton = Carve.Core.Skeleton;
using Model = Carve.Core.Model;

namespace Carve.Server.Tests;

/// <summary>
/// The skinned export (D-6) read back through the engine's own parser, <c>IModelGeometryLoader.Parse</c>: no warnings,
/// a submesh per piece and colour written, bones as given, clips with their durations, inverse binds against the rest
/// world transforms, weights summing to 1, the limits' warnings at and past each limit, and the same bytes twice.
/// </summary>
[Trait("Tier", "unit")]
public class SkinnedExportTests
{
    /// <summary>Breadth-first on purpose, as D-6's probe: the loader keeps a skin's order, and only a skin's.</summary>
    private static readonly string[] BoneNames = ["Hips", "LegL", "LegR", "FootL", "FootR"];

    private static Skeleton Rig() => Skeleton.Create([
        new BoneSpec("Hips", null, new Vector3(0, 1, 0), Quaternion.Identity),
        new BoneSpec("LegL", "Hips", new Vector3(0.2f, -0.1f, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.3f)),
        new BoneSpec("LegR", "Hips", new Vector3(-0.2f, -0.1f, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.4f)),
        new BoneSpec("FootL", "LegL", new Vector3(0, -0.8f, 0.1f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f)),
        new BoneSpec("FootR", "LegR", new Vector3(0, -0.8f, 0.1f), Quaternion.Identity),
    ]).Skeleton!;

    private static PolygonMesh Box(Vector3 at, float size) =>
        TestMeshes.Make(TestMeshes.CubeVertices.Select(v => at + (v * size)), TestMeshes.CubeFaces);

    private static PolygonMesh Weighted(PolygonMesh mesh, Func<int, Influence[]> influences)
    {
        var weights = VertexWeights.None;
        for (var v = 0; v < mesh.Vertices.Count; v++)
            if (influences(v) is { Length: > 0 } i) weights = weights.With([v], i);
        return mesh.WithWeights(weights);
    }

    private static Clip MakeClip(string name, float duration, Skeleton rig, params BoneChannelsSpec[] channels)
    {
        var (clip, errors) = Clip.Create(name, duration, true, channels, rig);
        Assert.True(clip is not null, string.Join("; ", errors));
        return clip!;
    }

    /// <summary>A body (two colours, one a group) and a coat (uncoloured) on the five bones, with two clips keyed away from 0 and the end.</summary>
    private static Model Character()
    {
        var rig = Rig();
        var body = Weighted(Box(new Vector3(0, 0.5f, 0), 0.5f), v => v % 3 == 0
            ? [new Influence(0, 0.1), new Influence(1, 0.2), new Influence(3, 0.3), new Influence(4, 0.4)]
            : [new Influence(v % 5, 1.0 / 3), new Influence((v + 1) % 5, 2.0 / 3)]);
        body = body.WithStyle(FaceStyle.None.WithGroup("panels", [0, 1]).WithColour([0, 1], "#3C78C8").WithColour([4], "#c8503c"));
        var coat = Weighted(Box(new Vector3(0, 1.2f, 0), 0.3f), _ => [new Influence(0, 1)]);
        var walk = MakeClip("walk", 1.5f, rig,
            new BoneChannelsSpec("LegL", new ChannelSpec<Quaternion>("LINEAR", [new(0.25f, Quaternion.Identity), new(1f, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f))]), null),
            new BoneChannelsSpec("Hips", null, new ChannelSpec<Vector3>("STEP", [new(0.5f, new Vector3(0, 1.1f, 0))])));
        var idle = MakeClip("idle", 2f, rig,
            new BoneChannelsSpec("FootR", new ChannelSpec<Quaternion>("STEP", [new(0f, Quaternion.Identity), new(2f, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.2f))]), null));
        return Model.Of("body", body).With("coat", coat).WithSkeleton(rig).WithClips([walk, idle]);
    }

    private static ParsedModel Parse(byte[] glb) => GlbExportTests.Parser.Parse(glb, ModelFormat.Gltf);

    private static JsonElement Json(byte[] glb)
    {
        var length = BitConverter.ToInt32(glb, 12);
        Assert.Equal("JSON", Encoding.ASCII.GetString(glb, 16, 4));
        return JsonDocument.Parse(glb.AsMemory(20, length)).RootElement;
    }

    [Fact]
    public void The_engine_loads_every_piece_bone_and_clip_with_no_warning()
    {
        var model = Character();
        var glb = SkinnedExport.Write(model, "orc");
        var parsed = Parse(glb.Bytes);

        Assert.Empty(parsed.Warnings);
        Assert.Equal(["body:panels", "body", "body:c8503c", "coat"], glb.Materials);
        Assert.Equal(glb.Materials.Count, parsed.SubmeshMaterials.Length);
        Assert.Equal(["body", "coat"], glb.Pieces);
        Assert.NotNull(parsed.Skin);
        Assert.Equal(BoneNames, parsed.Skin!.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal([-1, 0, 0, 1, 2], parsed.Skin.Skeleton.Bones.Select(b => b.ParentIndex));
        Assert.Equal(Matrix4x4.Identity, parsed.Skin.Skeleton.RootTransform);
        Assert.Equal(["walk", "idle"], parsed.Skin.Animations.Select(a => a.Name));
        Assert.Equal([1.5f, 2f], parsed.Skin.Animations.Select(a => a.Duration));
        Assert.All(parsed.Skin.Animations, a => Assert.Equal(BoneNames.Length, a.Bones.Length));
        Assert.Empty(glb.Warnings);
        Assert.Equal(0, glb.BoundToRoot);
    }

    [Fact]
    public void Inverse_binds_are_the_inverse_of_each_bones_rest_world_transform()
    {
        var rig = Rig();
        var parsed = Parse(SkinnedExport.Write(Character(), "orc").Bytes);
        var world = new Matrix4x4[rig.Bones.Count];
        for (var i = 0; i < world.Length; i++)
        {
            var b = rig.Bones[i];
            var local = Matrix4x4.CreateFromQuaternion(b.Rotation) * Matrix4x4.CreateTranslation(b.Position);
            world[i] = b.Parent < 0 ? local : local * world[b.Parent];
            Assert.True(Matrix4x4.Invert(world[i], out var expected));
            var actual = parsed.Skin!.Skeleton.Bones[i].InverseBindMatrix;
            for (var r = 0; r < 4; r++)
                for (var c = 0; c < 4; c++)
                    Assert.True(Math.Abs(expected[r, c] - actual[r, c]) < 1e-5f, $"{b.Name} [{r},{c}]: expected {expected[r, c]}, got {actual[r, c]}");
            // The rest world position the skeleton reports goes to the origin under the inverse bind.
            Assert.True(Vector3.Transform(rig.RestWorldPosition(i), actual).Length() < 1e-5f, b.Name);
        }
    }

    [Fact]
    public void The_file_layout_is_one_mesh_one_skin_an_armature_and_clips_keyed_at_0_and_the_duration()
    {
        var json = Json(SkinnedExport.Write(Character(), "orc").Bytes);
        Assert.Equal(1, json.GetProperty("meshes").GetArrayLength());
        Assert.Equal(1, json.GetProperty("skins").GetArrayLength());
        var nodes = json.GetProperty("nodes");
        Assert.Equal(["Armature", .. BoneNames, "orc"], nodes.EnumerateArray().Select(n => n.GetProperty("name").GetString()));
        Assert.False(nodes[0].TryGetProperty("translation", out _));
        Assert.False(nodes[0].TryGetProperty("rotation", out _));
        Assert.Equal([1, 2, 3, 4, 5], json.GetProperty("skins")[0].GetProperty("joints").EnumerateArray().Select(j => j.GetInt32()));
        Assert.True(json.GetProperty("skins")[0].TryGetProperty("inverseBindMatrices", out _));
        var meshNode = nodes[6];
        Assert.Equal(0, meshNode.GetProperty("mesh").GetInt32());
        Assert.Equal(0, meshNode.GetProperty("skin").GetInt32());
        Assert.False(meshNode.TryGetProperty("matrix", out _));
        Assert.Equal([0, 6], json.GetProperty("scenes")[json.GetProperty("scene").GetInt32()].GetProperty("nodes").EnumerateArray().Select(n => n.GetInt32()));

        var primitives = json.GetProperty("meshes")[0].GetProperty("primitives");
        Assert.Equal(4, primitives.GetArrayLength());
        Assert.All(primitives.EnumerateArray(), p =>
        {
            var a = p.GetProperty("attributes");
            Assert.Equal(["JOINTS_0", "NORMAL", "POSITION", "WEIGHTS_0"], a.EnumerateObject().Select(x => x.Name).Order());
        });

        var accessors = json.GetProperty("accessors");
        var animations = json.GetProperty("animations");
        Assert.Equal(["walk", "idle"], animations.EnumerateArray().Select(a => a.GetProperty("name").GetString()));
        foreach (var (animation, duration) in animations.EnumerateArray().Zip([1.5f, 2f]))
        {
            foreach (var sampler in animation.GetProperty("samplers").EnumerateArray())
            {
                var input = accessors[sampler.GetProperty("input").GetInt32()];
                Assert.Equal(0f, input.GetProperty("min")[0].GetSingle());
                Assert.Equal(duration, input.GetProperty("max")[0].GetSingle());
            }
        }
        var walk = animations[0].GetProperty("samplers");
        Assert.Equal(["LINEAR", "STEP"], walk.EnumerateArray().Select(s => s.TryGetProperty("interpolation", out var i) ? i.GetString() : "LINEAR"));
        Assert.Equal(4, accessors[walk[0].GetProperty("input").GetInt32()].GetProperty("count").GetInt32());
        Assert.Equal(3, accessors[walk[1].GetProperty("input").GetInt32()].GetProperty("count").GetInt32());
    }

    [Fact]
    public void Weights_sum_to_1_on_every_vertex_and_an_unweighted_vertex_binds_to_the_first_root_bone()
    {
        var model = Character();
        var coat = Weighted(model.Find("coat")!.WithWeights(null), v => v is 2 or 5 ? [] : [new Influence(1, 1)]);
        model = model.With("coat", coat);
        var unweighted = Assert.Single(SkinnedExport.Unweighted(model));
        Assert.Equal("coat", unweighted.Mesh);
        Assert.Equal([2, 5], unweighted.Vertices);

        var glb = SkinnedExport.Write(model, "orc");
        Assert.Equal(2, glb.BoundToRoot);
        var root = ModelRoot.ParseGLB(glb.Bytes);
        var count = 0;
        foreach (var primitive in root.LogicalMeshes[0].Primitives)
        {
            var weights = primitive.GetVertexAccessor("WEIGHTS_0").AsVector4Array();
            var joints = primitive.GetVertexAccessor("JOINTS_0").AsVector4Array();
            for (var i = 0; i < weights.Count; i++)
            {
                var w = weights[i];
                Assert.True(Math.Abs((double)w.X + w.Y + w.Z + w.W - 1) < 1e-6, $"vertex {i}: {w}");
                Assert.All([w.X, w.Y, w.Z, w.W], x => Assert.InRange(x, 0f, 1f));
                count++;
            }
            if (primitive.Material.Name == "coat")
                Assert.Contains(Enumerable.Range(0, weights.Count), i => weights[i] == new Vector4(1, 0, 0, 0) && joints[i].X == 0);
        }
        Assert.True(count > 0);
        Assert.Empty(Parse(glb.Bytes).Warnings);
    }

    [Fact]
    public void Four_influences_narrowed_to_float_still_sum_to_1()
    {
        var joints = SkinnedExport.Joints([new Influence(0, 0.1), new Influence(1, 0.2), new Influence(3, 0.3), new Influence(4, 0.4)]);
        var w = joints.Weights;
        Assert.Equal(1.0, (double)w.X + w.Y + w.Z + w.W, 6);
        Assert.Equal(new Vector4(1, 0, 0, 0), SkinnedExport.Joints([]).Weights);
        Assert.Equal(Vector4.Zero, SkinnedExport.Joints([]).Joints);
    }

    [Fact]
    public void The_same_model_writes_the_same_bytes_twice_with_several_clips()
    {
        var a = SkinnedExport.Write(Character(), "orc").Bytes;
        var b = SkinnedExport.Write(Character(), "orc").Bytes;
        Assert.Equal(a, b);
    }

    [Fact]
    public void A_clip_with_no_channels_still_loads_with_its_duration()
    {
        var model = Character();
        model = model.WithClip(MakeClip("still", 0.75f, model.Skeleton!));
        var parsed = Parse(SkinnedExport.Write(model, "orc").Bytes);
        Assert.Empty(parsed.Warnings);
        Assert.Equal(["walk", "idle", "still"], parsed.Skin!.Animations.Select(a => a.Name));
        Assert.Equal(0.75f, parsed.Skin.Animations[2].Duration);
    }

    // ---- D-6, Warnings for a game's limits: at the limit nothing, just past it a warning.

    [Fact]
    public void Triangles_warn_past_5000_and_not_at_it()
    {
        var model = Character();
        Assert.Empty(SkinnedExport.Warnings(model, SkinnedExport.MaxTriangles, 1));
        Assert.Contains("5001 triangles, above the 5000", Assert.Single(SkinnedExport.Warnings(model, SkinnedExport.MaxTriangles + 1, 1)));
    }

    [Fact]
    public void Submeshes_warn_past_8_and_not_at_it()
    {
        var model = Character();
        Assert.Empty(SkinnedExport.Warnings(model, 1, SkinnedExport.MaxSubmeshes));
        Assert.Contains("9 submeshes", Assert.Single(SkinnedExport.Warnings(model, 1, SkinnedExport.MaxSubmeshes + 1)));
    }

    [Fact]
    public void Colours_warn_past_8_and_not_at_it()
    {
        static Model Coloured(int colours)
        {
            var model = Character();
            var style = FaceStyle.None;
            for (var c = 0; c < 6; c++) style = style.WithColour([c], $"#0000{c:X2}");
            var coatStyle = FaceStyle.None;
            for (var c = 6; c < colours; c++) coatStyle = coatStyle.WithColour([c - 6], $"#0000{c:X2}");
            return model.With("body", model.Find("body")!.WithStyle(style)).With("coat", model.Find("coat")!.WithStyle(coatStyle));
        }

        // Submeshes passed as 1 so only the colours speak.
        Assert.Empty(SkinnedExport.Warnings(Coloured(8), 1, 1));
        Assert.Contains("9 colours", Assert.Single(SkinnedExport.Warnings(Coloured(9), 1, 1)));
        var glb = SkinnedExport.Write(Coloured(9), "orc");
        Assert.Contains(glb.Warnings, w => w.Contains("10 submeshes", StringComparison.Ordinal));
    }

    private static Model Humanoid(float height, bool synty = true)
    {
        var names = synty ? SkinnedExport.SyntyRequiredBones : [.. SkinnedExport.SyntyRequiredBones.Select(n => "Dragon" + n)];
        var specs = names.Select((n, i) => new BoneSpec(n, i == 0 ? null : names[0], new Vector3(0, 0.1f, 0), Quaternion.Identity)).ToArray();
        var rig = Skeleton.Create(specs).Skeleton!;
        var mesh = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => new Vector3(v.X, v.Y > 0 ? height : 0f, v.Z)), TestMeshes.CubeFaces);
        return Model.Of("body", mesh).WithSkeleton(rig);
    }

    [Theory]
    [InlineData(1.4f, false)]
    [InlineData(2.4f, false)]
    [InlineData(1.39f, true)]
    [InlineData(2.41f, true)]
    public void Height_warns_outside_1_4_to_2_4_m_on_the_Synty_bones(float height, bool warns)
    {
        var warnings = SkinnedExport.Warnings(Humanoid(height), 1, 1);
        if (warns) Assert.Contains("m tall, outside the 1.4 to 2.4 m", Assert.Single(warnings));
        else Assert.Empty(warnings);
    }

    [Fact]
    public void A_rig_without_the_Synty_bones_gets_no_height_warning()
    {
        Assert.Empty(SkinnedExport.Warnings(Humanoid(0.5f, synty: false), 1, 1));
        Assert.Empty(SkinnedExport.Warnings(Humanoid(4f, synty: false), 1, 1));
        // One required bone short of the preset is not a humanoid either.
        var model = Humanoid(0.5f);
        Assert.Single(SkinnedExport.Warnings(model, 1, 1));
        var specs = model.Skeleton!.Bones.Where(b => b.Name != "Ankle_R").Select(b => new BoneSpec(b.Name, b.Parent < 0 ? null : "Hips", b.Position, b.Rotation)).ToArray();
        Assert.Empty(SkinnedExport.Warnings(model.WithSkeleton(Skeleton.Create(specs).Skeleton!), 1, 1));
    }
}
