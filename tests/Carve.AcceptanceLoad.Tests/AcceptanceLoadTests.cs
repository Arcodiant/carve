using System.Globalization;
using Carve.LoadChecks;
using Empyrean;
using Xunit;

namespace Carve.AcceptanceLoad.Tests;

/// <summary>
/// T-49's pass checks on a fresh agent's round folder, read as a game reads it. The folder comes from <c>CARVE_ACCEPTANCE_RUN</c>
/// (<c>scripts/run-acceptance-check.ps1</c> sets it, after the frame scenario has written <c>acceptance/differences.tsv</c> there);
/// with it unset every test is skipped (the script fails a run that skipped one). The three exports are read by
/// <c>IModelGeometryLoader</c> with <c>game/Content</c> mounted, no sidecar, and compared with the saved <c>character.carve</c>.
/// </summary>
[Trait("Tier", "regression")]
public class AcceptanceLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_ACCEPTANCE_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_ACCEPTANCE_RUN is unset");

    private static Carve.Core.Model Saved() => RunContent.Saved(Path.Combine(RunFolder, "character.carve"));

    [SkippableOrRun]
    public void The_saved_model_has_two_meshes_a_skeleton_and_the_clips_Idle_and_Walk_and_no_unweighted_vertex()
    {
        var model = Saved();
        Assert.Equal(2, model.Meshes.Count);
        Assert.NotNull(model.Skeleton);
        Assert.Contains(model.Clips, c => c.Name == "Idle" && c.Loop);
        Assert.Contains(model.Clips, c => c.Name == "Walk" && c.Loop);
        foreach (var m in model.Meshes)
        {
            var weights = m.Mesh.Weights;
            Assert.NotNull(weights);
            var unweighted = Enumerable.Range(0, m.Mesh.Vertices.Count).Where(v => !weights!.Has(v)).ToList();
            Assert.True(unweighted.Count == 0, $"{m.Name}: {unweighted.Count} unweighted vertices");
        }
    }

    [SkippableOrRun]
    public void The_model_file_loads_with_the_saved_bones_and_both_clips_and_no_warnings()
    {
        var saved = Saved();
        var model = RunContent.Loader(RunFolder).Load(new ContentPath("models/character.glb"));
        Assert.Empty(model.Warnings);
        Assert.Equal(saved.Skeleton!.Names, model.Skin!.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(saved.Clips.Select(c => c.Name).Order(), model.Skin.Animations.Select(a => a.Name).Order());
    }

    [SkippableOrRun]
    public void The_clips_file_loads_as_an_animation_set_with_the_models_bones_and_clips()
    {
        var loader = RunContent.Loader(RunFolder);
        var model = loader.Load(new ContentPath("models/character.glb")).Skin!;
        var set = loader.LoadAnimationSet(new ContentPath("models/character.clips.glb"));
        Assert.Equal(model.Skeleton.Bones, set.Skeleton.Bones);
        Assert.Equal(model.Animations.Select(a => a.Name).Order(), set.Clips.Select(c => c.Name).Order());
        foreach (var clip in set.Clips)
        {
            Assert.Equal(model.Animations.Single(a => a.Name == clip.Name).Duration, clip.Duration);
        }
    }

    [SkippableOrRun]
    public void The_hat_piece_loads_with_the_models_bones_and_no_clips()
    {
        var loader = RunContent.Loader(RunFolder);
        var model = loader.Load(new ContentPath("models/character.glb")).Skin!;
        var piece = loader.Load(new ContentPath("models/character.hat.glb"));
        Assert.Empty(piece.Warnings);
        Assert.Equal(model.Skeleton.Bones, piece.Skin!.Skeleton.Bones);
        Assert.Empty(piece.Skin.Animations);
    }

    [SkippableOrRun]
    public void Every_render_glb_frame_shows_the_same_character_as_render_under_the_frame_rule()
    {
        var rows = File.ReadAllLines(Path.Combine(RunFolder, "acceptance", "differences.tsv")).Skip(1).Select(l => l.Split('\t')).ToList();
        var good = rows.Where(p => p[1] == "good").ToList();
        // Rest and three times of each of two clips, from two views.
        Assert.True(good.Count >= 14, $"only {good.Count} frame pairs");
        Assert.All(good, p => Assert.True(p[6] == "passes" && int.Parse(p[5], CultureInfo.InvariantCulture) <= 50, $"{p[0]}: {p[2]} px differ, largest patch {p[5]} px"));
        Assert.All(good, p => Assert.True(int.Parse(p[7], CultureInfo.InvariantCulture) * 50 >= int.Parse(p[3], CultureInfo.InvariantCulture), $"{p[0]}: the character covers under 2 % of the frame"));

        // The control: the front render against the file from the back fails the same rule.
        Assert.Contains(rows, p => p[1] == "control" && p[6] == "fails");
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_ACCEPTANCE_RUN") is null)
        {
            Skip = "CARVE_ACCEPTANCE_RUN is unset";
        }
    }
}
