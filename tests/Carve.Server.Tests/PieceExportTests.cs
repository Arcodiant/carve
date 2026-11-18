extern alias carve;

using GameLimits = carve::Carve.Server.GameLimits;
using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using SharpGLTF.Schema2;
using PolygonMesh = Carve.Core.PolygonMesh;
using Skeleton = Carve.Core.Skeleton;
using Model = Carve.Core.Model;
using static Carve.Server.Tests.RigTestHelpers;

namespace Carve.Server.Tests;

/// <summary>
/// The piece export (D-6, Pieces share the body's palette; T-50) read back through the engine's own parser: no warnings,
/// the model file's bones, order, count and inverse binds in every piece (a piece on one bone, a piece on the deepest
/// bone of a breadth-first skeleton), one glTF mesh, no animations, the same bytes twice, and the refusals. The rig
/// comparison is shown failing on a piece written with the skeleton trimmed to the bones used.
/// </summary>
[Trait("Tier", "unit")]
public class PieceExportTests
{
    private static BoneSpec B(string name, string? parent, float x, float y, float z, Quaternion? r = null) =>
        new(name, parent, new Vector3(x, y, z), r ?? Quaternion.Identity);

    /// <summary>Breadth-first on purpose (every bone of one depth before the next), nine bones with turned rests.</summary>
    private static readonly Skeleton Rig = Skeleton.Create([
        B("Hips", null, 0, 1, 0),
        B("Spine", "Hips", 0, 0.3f, 0, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.1f)),
        B("LegL", "Hips", 0.2f, -0.1f, 0, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.3f)),
        B("LegR", "Hips", -0.2f, -0.1f, 0),
        B("Head", "Spine", 0, 0.5f, 0.05f),
        B("ArmL", "Spine", 0.3f, 0.3f, 0, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.4f)),
        B("FootL", "LegL", 0, -0.8f, 0.1f),
        B("FootR", "LegR", 0, -0.8f, 0.1f, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f)),
        B("Hat", "Head", 0, 0.2f, 0),
    ]).Skeleton!;

    private static PolygonMesh Box(Vector3 at, float size) =>
        TestMeshes.Make(TestMeshes.CubeVertices.Select(v => at + (v * size)), TestMeshes.CubeFaces);

    private static PolygonMesh Weighted(PolygonMesh mesh, params Influence[] influences)
    {
        var weights = VertexWeights.None;
        for (var v = 0; v < mesh.Vertices.Count; v++) weights = weights.With([v], influences);
        return mesh.WithWeights(weights);
    }

    /// <summary>A body on several bones, a hat on the deepest bone alone, a belt on the root alone, two clips.</summary>
    private static Model Character()
    {
        var body = Weighted(Box(new Vector3(0, 1, 0), 0.4f), new Influence(0, 0.5), new Influence(1, 0.25), new Influence(4, 0.25))
            .WithStyle(FaceStyle.None.WithColour([0, 1], "#3C78C8"));
        var hat = Weighted(Box(new Vector3(0, 2, 0), 0.2f), new Influence(8, 1)).WithStyle(FaceStyle.None.WithColour([0, 1, 2, 3, 4, 5], "#E8C86A"));
        var belt = Weighted(Box(new Vector3(0, 0.9f, 0), 0.1f), new Influence(0, 1));
        var walk = MakeClip("walk", 1.5f, Rig,
            new BoneChannelsSpec("LegL", new ChannelSpec<Quaternion>("LINEAR", [new(0.25f, Quaternion.Identity), new(1f, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f))]), null));
        var nod = MakeClip("nod", 1f, Rig,
            new BoneChannelsSpec("Head", new ChannelSpec<Quaternion>("STEP", [new(0.5f, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.2f))]), null));
        return Model.Of("body", body).With("hat", hat).With("belt", belt).WithSkeleton(Rig).WithClips([walk, nod]);
    }

    private static ParsedModel Parse(byte[] glb) => GlbExportTests.Parser.Parse(glb, ModelFormat.Gltf);

    private static byte[] Piece(Model model, params string[] meshes) => PieceExport.Write(model, meshes, "orc.piece").Bytes;

    private static byte[] Whole(Model model) => SkinnedExport.Write(model, "orc").Bytes;

    private static ModelRoot Read(byte[] glb) => ModelRoot.ReadGLB(new MemoryStream(glb));

    /// <summary>The rig half of a file: bone names in order, then each joint's inverse bind matrix.</summary>
    private static (string[] Names, Matrix4x4[] Binds) RigOf(byte[] glb)
    {
        var skin = Read(glb).LogicalSkins[0];
        var names = Enumerable.Range(0, skin.JointsCount).Select(i => skin.GetJoint(i).Joint.Name).ToArray();
        var binds = Enumerable.Range(0, skin.JointsCount).Select(i => skin.GetJoint(i).InverseBindMatrix).ToArray();
        return (names, binds);
    }

    /// <summary>What D-6 enforces: the piece has the model file's bones, in its order, with its inverse binds (read through the engine and the raw glTF).</summary>
    private static void AssertSameRig(byte[] model, byte[] piece)
    {
        var (a, b) = (Parse(model).Skin!, Parse(piece).Skin!);
        Assert.Equal(a.Skeleton.Bones.Length, b.Skeleton.Bones.Length);
        Assert.Equal(a.Skeleton.Bones, b.Skeleton.Bones);
        Assert.Equal(a.Skeleton.Bones.Select(x => x.Name), b.Skeleton.Bones.Select(x => x.Name));
        var (ra, rb) = (RigOf(model), RigOf(piece));
        Assert.Equal(ra.Names, rb.Names);
        Assert.Equal(ra.Binds, rb.Binds);
    }

    [Theory]
    [InlineData("belt")]
    [InlineData("hat")]
    [InlineData("body")]
    [InlineData("hat", "belt")]
    public void A_piece_has_the_model_files_bones_order_and_inverse_binds_and_no_clips(params string[] names)
    {
        var model = Character();
        var glb = PieceExport.Write(model, names, "orc.piece");
        Assert.Empty(GameLimits.Starting.Warnings(PieceExport.Select(model, names), glb.Triangles, glb.Materials.Count, wholeBody: false));
        var parsed = Parse(glb.Bytes);
        Assert.Empty(parsed.Warnings);

        Assert.Equal(Rig.Names, parsed.Skin!.Skeleton.Bones.Select(b => b.Name));
        AssertSameRig(Whole(model), glb.Bytes);
        Assert.Empty(parsed.Skin.Animations);
        Assert.Empty(Read(glb.Bytes).LogicalAnimations);
        Assert.Single(Read(glb.Bytes).LogicalMeshes);
        Assert.Equal(parsed.SubmeshMaterials.Length, glb.Materials.Count);
        Assert.Equal(model.Meshes.Where(m => names.Contains(m.Name)).Select(m => m.Name), glb.Pieces);
    }

    [Fact]
    public void A_piece_on_the_deepest_bone_alone_still_carries_every_bone()
    {
        var piece = Parse(Piece(Character(), "hat"));
        Assert.Equal(9, piece.Skin!.Skeleton.Bones.Length);
        Assert.Equal("Hat", piece.Skin.Skeleton.Bones[8].Name);
    }

    [Fact]
    public void A_pieces_primitives_are_the_model_files_for_the_same_mesh()
    {
        var model = Character();
        var whole = Read(Whole(model)).LogicalMeshes[0].Primitives.Select(p => p.Material.Name).ToList();
        var piece = Read(Piece(model, "hat")).LogicalMeshes[0].Primitives.Select(p => p.Material.Name).ToList();
        Assert.Single(piece);
        Assert.StartsWith("hat:", piece[0]);
        Assert.Contains(piece[0], whole);
    }

    [Fact]
    public void The_same_piece_is_the_same_bytes_twice_and_ignores_the_order_asked()
    {
        var model = Character();
        Assert.Equal(Piece(model, "hat"), Piece(model, "hat"));
        Assert.Equal(Piece(model, "body", "hat"), Piece(model, "hat", "body", "hat"));
    }

    [Fact]
    public void Writing_a_piece_changes_nothing_and_the_model_mode_is_the_same_bytes_as_before()
    {
        var model = Character();
        var before = Whole(model);
        _ = Piece(model, "hat");
        Assert.Equal(before, Whole(model));
        Assert.Equal(2, model.Clips.Count);
        Assert.Equal(["body", "hat", "belt"], model.Names);
    }

    [Fact]
    public void Refusals_name_the_problem_and_the_mesh_names()
    {
        var model = Character();
        Assert.Contains("meshes is empty", PieceExport.Refusal(model, []));
        var unknown = PieceExport.Refusal(model, ["hat", "cape"])!;
        Assert.Contains("no mesh named 'cape'", unknown);
        Assert.Contains("'body'", unknown);
        Assert.Contains("'belt'", unknown);
        Assert.Contains("no skeleton", PieceExport.Refusal(Model.Of("body", Box(Vector3.Zero, 1)), ["body"]));
        Assert.Null(PieceExport.Refusal(model, ["hat"]));
    }

    [Fact]
    public void A_piece_does_not_warn_on_height_where_the_model_does()
    {
        // Humanoid bones, a body well under 1.4 m: the model file warns on height, a small piece of it does not.
        var rig = Skeleton.Create([.. SkinnedExport.SyntyRequiredBones.Select(n => B(n, n == "Hips" ? null : "Hips", 0, 0.1f, 0))]).Skeleton!;
        var model = Model.Of("body", Weighted(Box(new Vector3(0, 0.5f, 0), 0.2f), new Influence(0, 1))).WithSkeleton(rig);
        // T-51: the limits are the game's; Orcward's starting numbers stand in for them.
        Assert.Contains(GameLimits.Starting.Warnings(model, 12, 1), w => w.Contains("m tall"));
        Assert.Empty(GameLimits.Starting.Warnings(PieceExport.Select(model, ["body"]), 12, 1, wholeBody: false));
    }

    /// <summary>The skeleton trimmed to the bones the hat uses: the hat bone alone, as a root at its rest world position.</summary>
    private static byte[] TrimmedHat(Model model)
    {
        var world = Rig.Bones[8].Position + Rig.Bones[4].Position + Rig.Bones[1].Position + Rig.Bones[0].Position;
        var trimmed = Skeleton.Create([B("Hat", null, world.X, world.Y, world.Z)]).Skeleton!;
        var hat = Weighted(model.Find("hat")!, new Influence(0, 1));
        return SkinnedExport.Write(Model.Of("hat", hat).WithSkeleton(trimmed), "orc.piece").Bytes;
    }

    [Fact]
    public void Shown_failing_a_piece_with_the_skeleton_trimmed_to_the_bones_used_is_caught()
    {
        var model = Character();
        var trimmed = TrimmedHat(model);
        Assert.Single(Parse(trimmed).Skin!.Skeleton.Bones); // it loads: nothing throws, which is the danger
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSameRig(Whole(model), trimmed));
        AssertSameRig(Whole(model), Piece(model, "hat")); // and the real piece passes the same check
    }
}
