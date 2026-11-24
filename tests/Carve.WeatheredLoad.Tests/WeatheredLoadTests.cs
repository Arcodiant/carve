using Carve.Core;
using Carve.Graphs;
using Carve.LoadChecks;
using Empyrean;
using Xunit;
using Model = Carve.Core.Model;

namespace Carve.WeatheredLoad.Tests;

/// <summary>
/// What a game sees of the exported weathered barrel, and the saved file held to the sample: the last step of suite <c>scenario-weathered</c>.
/// The run folder comes from <c>CARVE_WEATHERED_RUN</c>, which <c>scripts/run-weathered-scenario.ps1</c> sets; with it unset every test is
/// skipped (the script fails a run that skipped one).
/// </summary>
[Trait("Tier", "regression")]
public class WeatheredLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_WEATHERED_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_WEATHERED_RUN is unset");

    /// <summary>A file read with its texture and graph paths resolved against its own folder.</summary>
    private static Model Read(string path)
    {
        var read = CarveFile.Read(File.ReadAllText(path), Path.GetDirectoryName(path));
        return read.Model ?? throw new InvalidOperationException($"{path}: {string.Join("; ", read.Errors)}");
    }

    [SkippableOrRun]
    public void The_saved_file_is_the_sample_by_content()
    {
        var saved = Read(Path.Combine(RunFolder, "barrel-weathered.carve"));
        var sample = Read(RunContent.SamplePath("barrel-weathered.carve"));
        Assert.Equal(
            CarveFile.Write(sample, Path.GetDirectoryName(RunContent.SamplePath("barrel-weathered.carve"))!),
            CarveFile.Write(saved, RunFolder));
        Assert.Equal(3, sample.Materials.Count);
    }

    [SkippableOrRun]
    public void The_glb_loads_three_materials_with_no_warning_and_each_texture_is_the_bake_byte_for_byte()
    {
        var loaded = RunContent.Loader(RunFolder).Load(new ContentPath("models/barrel-weathered.glb"));
        Assert.Empty(loaded.Warnings);
        var materials = loaded.SubmeshMaterials.Select(m => m!).ToArray();
        Assert.Equal(3, materials.Length);

        var model = Read(Path.Combine(RunFolder, "barrel-weathered.carve"));
        var mesh = model.Meshes.Single().Name;
        byte[]? Expected(MaterialSlot? slot, string field) => slot switch
        {
            null => null,
            MaterialTexture png => png.Png.ToArray(),
            MaterialGraph graph => GraphSlotBake.Png(graph with { Path = Path.GetFullPath(graph.Path, RunFolder) }, field, model, mesh, TestContext.Current.CancellationToken),
            _ => throw new InvalidOperationException($"{field}: an unknown slot {slot}"),
        };

        foreach (var m in model.Materials)
        {
            var want = m.Textures.ToDictionary(t => t.Field, t => Expected(t.Texture, t.Field));
            var match = Assert.Single(materials, l => l.TextureBytes is { } b && b.SequenceEqual(want["base_colour_texture"]!));
            Assert.Equal(want["metallic_roughness_texture"], match.MetallicRoughnessTexture?.Bytes);
            Assert.Equal(want["normal_texture"], match.NormalTexture?.Bytes);
            Assert.Equal(want["occlusion_texture"], match.OcclusionTexture?.Bytes);
        }
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_WEATHERED_RUN") is null)
        {
            Skip = "CARVE_WEATHERED_RUN is unset";
        }
    }
}
