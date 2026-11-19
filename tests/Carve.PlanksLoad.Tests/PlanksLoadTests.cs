using Carve.LoadChecks;
using Empyrean;
using Xunit;
using CarveFile = Carve.Core.CarveFile;
using CarveTexture = Carve.Core.MaterialTexture;

namespace Carve.PlanksLoad.Tests;

/// <summary>
/// What a game sees of the exported planks barrel, and the saved file held to the sample: the last step of suite <c>scenario-planks</c>. The run
/// folder comes from <c>CARVE_PLANKS_RUN</c>, which <c>scripts/run-planks-scenario.ps1</c> sets; with it unset every test is skipped (the
/// script fails a run that skipped one).
/// </summary>
[Trait("Tier", "regression")]
public class PlanksLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_PLANKS_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_PLANKS_RUN is unset");

    /// <summary>A file read with its texture paths resolved against its own folder.</summary>
    private static Carve.Core.Model Read(string path)
    {
        var read = CarveFile.Read(File.ReadAllText(path), Path.GetDirectoryName(path));
        return read.Model ?? throw new InvalidOperationException($"{path}: {string.Join("; ", read.Errors)}");
    }

    private static byte[] Planks(string file) => File.ReadAllBytes(RunContent.SamplePath("graphs/planks/" + file));

    [SkippableOrRun]
    public void The_saved_file_is_the_sample_by_content()
    {
        var saved = Read(Path.Combine(RunFolder, "barrel-planks.carve"));
        var sample = Read(RunContent.SamplePath("barrel-planks.carve"));
        Assert.Equal(
            CarveFile.Write(sample, Path.GetDirectoryName(RunContent.SamplePath("barrel-planks.carve"))!),
            CarveFile.Write(saved, RunFolder));
        Assert.Equal(4, sample.Materials.Count);
    }

    [SkippableOrRun]
    public void The_sample_binds_the_planks_maps_to_the_staves_and_the_rune_stave_and_keeps_the_rest()
    {
        var sample = Read(RunContent.SamplePath("barrel-planks.carve"));
        foreach (var name in new[] { "staves", "rune-stave" })
        {
            var m = sample.Materials.Single(x => x.Name == name);
            Assert.Equal(Planks("base_colour.png"), m.BaseColourTexture!.Png.ToArray());
            Assert.Equal(Planks("normal.png"), m.NormalTexture!.Png.ToArray());
            Assert.Equal(Planks("occlusion.png"), m.OcclusionTexture!.Png.ToArray());
            Assert.Equal(Planks("roughness.png"), m.MetallicRoughnessTexture!.Png.ToArray());
        }

        Assert.Equal(File.ReadAllBytes(RunContent.SamplePath("textures/rune-emissive.png")), sample.Materials.Single(x => x.Name == "rune-stave").EmissiveTexture!.Png.ToArray());
        var iron = sample.Materials.Single(x => x.Name == "iron");
        Assert.Equal(File.ReadAllBytes(RunContent.SamplePath("textures/iron-mr.png")), iron.MetallicRoughnessTexture!.Png.ToArray());
        Assert.Equal(File.ReadAllBytes(RunContent.SamplePath("textures/end-grain-base.png")), sample.Materials.Single(x => x.Name == "end-grain").BaseColourTexture!.Png.ToArray());
    }

    [SkippableOrRun]
    public void The_glb_loads_four_materials_and_the_staves_carry_the_planks_maps_with_no_warning()
    {
        var model = RunContent.Loader(RunFolder).Load(new ContentPath("models/barrel-planks.glb"));
        Assert.Empty(model.Warnings);
        var loaded = model.SubmeshMaterials.Select(m => m!).ToArray();
        Assert.Equal(4, loaded.Length);
        Assert.Equal(2, loaded.Count(m => m.TextureBytes is { } b && b.SequenceEqual(Planks("base_colour.png"))));
        Assert.All(loaded.Where(m => m.TextureBytes is { } b && b.SequenceEqual(Planks("base_colour.png"))), m =>
        {
            Assert.Equal(Planks("normal.png"), m.NormalTexture!.Bytes);
            Assert.Equal(Planks("occlusion.png"), m.OcclusionTexture!.Bytes);
            Assert.Equal(Planks("roughness.png"), m.MetallicRoughnessTexture!.Bytes);
        });
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_PLANKS_RUN") is null)
        {
            Skip = "CARVE_PLANKS_RUN is unset";
        }
    }
}
