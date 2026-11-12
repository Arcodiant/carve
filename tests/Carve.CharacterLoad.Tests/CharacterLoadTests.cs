using Empyrean;
using CarveFile = Carve.Core.CarveFile;
using CarveModel = Carve.Core.Model;
using Carve.LoadChecks;
using Xunit;

namespace Carve.CharacterLoad.Tests;

/// <summary>
/// What a game sees of the exported gardener: the last step of suite <c>scenario-character</c>. The run folder comes from
/// <c>CARVE_CHARACTER_RUN</c>, which <c>scripts/run-character-scenario.ps1</c> sets; with it unset every test is skipped
/// (the script fails a run that skipped one). The files are read as the game would read them: a content folder mounted on
/// the engine's registry and <c>IModelGeometryLoader.Load</c>, no sidecar.
/// </summary>
[Trait("Tier", "regression")]
public class CharacterLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_CHARACTER_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_CHARACTER_RUN is unset");

    private static IModelGeometryLoader Loader() => RunContent.Loader(RunFolder);

    private static CarveModel Saved(string path) => RunContent.Saved(path);

    private static string SamplePath() => RunContent.SamplePath("gardener.carve");

    /// <summary>T-59's guard: the gardener has no UVs, so both files are the bytes written before UVs existed (SHA-256, taken on milestone/M-6 before T-59).</summary>
    [SkippableOrRun]
    public void The_exported_files_are_byte_identical_to_those_written_before_UVs()
    {
        static string Hash(string file) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(RunFolder, "game", "Content", "models", file))));
        Assert.Equal("7C09FEA328FB611232B036517897F3FFC12816E268F25A0AF8D5C4442E71A8AD", Hash("gardener.glb"));
        Assert.Equal("6D25ED0B3CE3E0576B23131EFD01D7BACCE987C278480E32160C4D1CBCEAF596", Hash("gardener.clips.glb"));
    }

    [SkippableOrRun]
    public void The_model_loads_with_the_bones_in_order_and_the_clips_named()
    {
        var saved = Saved(Path.Combine(RunFolder, "work", "gardener.carve"));
        var model = Loader().Load(new ContentPath("models/gardener.glb"));
        var skin = model.Skin!;
        Assert.NotNull(skin);
        Assert.Equal(22, skin.Skeleton.Bones.Count());
        Assert.Equal(saved.Skeleton!.Names, skin.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(["Idle", "Walk"], skin.Animations.Select(a => a.Name));
        Assert.Equal(["Idle", "Walk"], saved.Clips.Select(c => c.Name));
        Assert.Empty(model.Warnings);
    }

    [SkippableOrRun]
    public void The_Synty_preset_resolves_on_the_skeleton_with_no_sidecar()
    {
        var skeleton = Loader().Load(new ContentPath("models/gardener.glb")).Skin!.Skeleton;
        Assert.False(File.Exists(Path.Combine(RunFolder, "game", "Content", "models", "gardener.humanoid.json")));
        var rig = HumanoidRoleMap.Synty.Resolve(skeleton, "gardener");
        var fromPreset = HumanoidRoleMap.ResolvePreset(skeleton, "gardener");
        foreach (var role in HumanoidRoles.Required)
        {
            Assert.True(rig.BoneIndices[(int)role] >= 0, role.ToString());
            Assert.Equal(rig.BoneIndices[(int)role], fromPreset.BoneIndices[(int)role]);
        }

        Assert.True(HumanoidRoleMap.Synty.TryGetBone(HumanoidRole.Hips, out var hips));
        Assert.Equal("Hips", skeleton.Bones[rig.BoneIndices[(int)HumanoidRole.Hips]].Name);
        Assert.Equal("Hips", hips);
    }

    [SkippableOrRun]
    public void The_clips_file_loads_beside_it_with_the_same_bones_and_clips()
    {
        var loader = Loader();
        var model = loader.Load(new ContentPath("models/gardener.glb")).Skin!;
        var set = loader.LoadAnimationSet(new ContentPath("models/gardener.clips.glb"));
        Assert.Equal(model.Skeleton.Bones.Select(b => b.Name), set.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(model.Skeleton.Bones, set.Skeleton.Bones);
        Assert.Equal(["Idle", "Walk"], set.Clips.Select(c => c.Name));
        Assert.Equal(model.Animations.Select(a => a.Duration), set.Clips.Select(c => c.Duration));
        Assert.Contains("no meshes", Assert.ThrowsAny<Exception>(() => loader.Load(new ContentPath("models/gardener.clips.glb"))).Message);
    }

    [SkippableOrRun]
    public void The_hat_piece_loads_with_the_bodys_bones_in_order_and_no_clips()
    {
        var loader = Loader();
        var body = loader.Load(new ContentPath("models/gardener.glb")).Skin!;
        var piece = loader.Load(new ContentPath("models/gardener.hat.glb"));
        Assert.Empty(piece.Warnings);
        Assert.Equal(body.Skeleton.Bones.Select(b => b.Name), piece.Skin!.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(body.Skeleton.Bones, piece.Skin.Skeleton.Bones);
        Assert.Empty(piece.Skin.Animations);
    }

    [SkippableOrRun]
    public void The_sample_is_the_gardener_the_tools_built_by_content()
    {
        // Compared by content, not bytes: each is read, then written the one way, which gives floats back bit for bit.
        var sample = CarveFile.Write(Saved(SamplePath()));
        var built = CarveFile.Write(Saved(Path.Combine(RunFolder, "work", "gardener.carve")));
        Assert.Equal(built, sample);
    }

    [SkippableOrRun]
    public void The_export_is_clean_the_gardener_trips_no_limit()
    {
        var result = File.ReadAllText(Path.Combine(RunFolder, "export-result.txt"));
        Assert.DoesNotContain("warning(s)", result);
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_CHARACTER_RUN") is null)
        {
            Skip = "CARVE_CHARACTER_RUN is unset";
        }
    }
}
