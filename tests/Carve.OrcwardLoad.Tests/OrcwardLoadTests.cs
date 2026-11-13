using System.Globalization;
using Carve.LoadChecks;
using Empyrean;
using Xunit;

namespace Carve.OrcwardLoad.Tests;

/// <summary>
/// Orcward's done-when, read as the game reads it: the last step of suite <c>scenario-orcward</c>. The run folder comes from
/// <c>CARVE_ORCWARD_RUN</c>, which <c>scripts/run-orcward-scenario.ps1</c> sets; with it unset every test is skipped (the script
/// fails a run that skipped one). The three exports are read by <c>IModelGeometryLoader</c> with a content folder mounted, no sidecar.
/// </summary>
[Trait("Tier", "regression")]
public class OrcwardLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_ORCWARD_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_ORCWARD_RUN is unset");

    private static IModelGeometryLoader Loader() => RunContent.Loader(RunFolder);

    [SkippableOrRun]
    public void The_model_loads_with_22_bones_and_both_clips_and_no_warnings()
    {
        var model = Loader().Load(new ContentPath("models/gardener.glb"));
        Assert.Empty(model.Warnings);
        Assert.Equal(22, model.Skin!.Skeleton.Bones.Count());
        Assert.Equal(["Idle", "Walk"], model.Skin.Animations.Select(a => a.Name));
    }

    [SkippableOrRun]
    public void The_clips_file_loads_as_an_animation_set_with_the_same_bones_and_clips()
    {
        var loader = Loader();
        var model = loader.Load(new ContentPath("models/gardener.glb")).Skin!;
        var set = loader.LoadAnimationSet(new ContentPath("models/gardener.clips.glb"));
        Assert.Equal(model.Skeleton.Bones.Select(b => b.Name), set.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(model.Skeleton.Bones, set.Skeleton.Bones);
        Assert.Equal(model.Animations.Select(a => a.Name), set.Clips.Select(c => c.Name));
        Assert.Equal(model.Animations.Select(a => a.Duration), set.Clips.Select(c => c.Duration));
    }

    [SkippableOrRun]
    public void The_hat_piece_loads_with_the_bodys_bones_and_inverse_binds_and_no_clips()
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
    public void No_export_gave_a_warning()
    {
        foreach (var file in new[] { "gardener.glb", "gardener.clips.glb", "gardener.hat.glb" })
        {
            Assert.DoesNotContain("warning(s)", File.ReadAllText(Path.Combine(RunFolder, "work", $"export-{file}.txt")));
        }
    }

    private sealed record Row(string Name, string Kind, int Differing, int Total, bool Fails, int Patch);

    private static List<Row> Rows() =>
        File.ReadAllLines(Path.Combine(RunFolder, "differences.tsv")).Skip(1).Select(l => l.Split('\t'))
            .Select(p => new Row(p[0], p[1], int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture), p[6] == "fails", int.Parse(p[5], CultureInfo.InvariantCulture))).ToList();

    [SkippableOrRun]
    public void Every_good_frame_passes_Orcwards_rule_at_rest_and_between_keys()
    {
        var good = Rows().Where(r => r.Kind == "good").ToList();
        Assert.Equal(10, good.Count);
        Assert.Contains(good, r => r.Name == "rest-front");
        Assert.Contains(good, r => r.Name == "idle-0.500-front");
        Assert.Contains(good, r => r.Name == "walk-0.125-side");
        Assert.All(good, r => Assert.False(r.Fails, $"{r.Name}: {r.Differing} of {r.Total} px, largest patch {r.Patch} px"));
    }

    [SkippableOrRun]
    public void Every_twisted_limb_fails_as_a_patch()
    {
        // The cluster rule (ruling of 2026-11-13): a patch of differing pixels past 50 px fails the frame, whatever the whole-frame count.
        var twists = Rows().Where(r => r.Kind == "twist").ToList();
        Assert.Equal(5, twists.Count);
        Assert.All(twists, r => Assert.True(r.Fails && r.Patch > 50, $"{r.Name}: {r.Differing} px, largest patch {r.Patch} px"));
        Assert.All(Rows().Where(r => r.Kind == "good"), r => Assert.True(r.Patch <= 50, $"{r.Name}: patch {r.Patch} px"));
    }

    [SkippableOrRun]
    public void Every_wrong_file_fails_the_same_rule()
    {
        var wrong = Rows().Where(r => r.Kind == "break").ToList();
        foreach (var kind in new[] { "swapped-bone-order", "wrong-inverse-binds", "reversed-winding", "dropped-piece" })
        {
            Assert.Contains(wrong, r => r.Name.StartsWith(kind, StringComparison.Ordinal));
        }

        // A rest frame is drawn with no palette, so a wrong bone order or inverse bind shows only in a clip frame: each kind fails at one frame at least.
        foreach (var kind in wrong.GroupBy(r => System.Text.RegularExpressions.Regex.Replace(r.Name, "-(rest|walk).*$", "")))
        {
            Assert.True(kind.Any(r => r.Fails), $"{kind.Key}: passes at every frame ({string.Join(", ", kind.Select(r => $"{r.Name} {r.Differing} px"))})");
        }

        // The control, a file rewritten with nothing changed, is the model's own frame.
        Assert.All(Rows().Where(r => r.Kind == "control"), r => Assert.Equal(0, r.Differing));
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_ORCWARD_RUN") is null)
        {
            Skip = "CARVE_ORCWARD_RUN is unset";
        }
    }
}
