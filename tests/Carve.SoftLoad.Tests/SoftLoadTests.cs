using System.Numerics;
using Empyrean;
using Carve.LoadChecks;
using Xunit;
using CarveFile = Carve.Core.CarveFile;
using CarveModel = Carve.Core.Model;
using CarveShading = Carve.Core.Shading;

namespace Carve.SoftLoad.Tests;

/// <summary>
/// What a game sees of the exported soft gardener: the last step of suite <c>scenario-soft</c>. The run folder comes from
/// <c>CARVE_SOFT_RUN</c>, which <c>scripts/run-soft-scenario.ps1</c> sets; with it unset every test is skipped (the script fails a
/// run that skipped one). The files are read as the game would read them: a content folder mounted on the engine's registry and
/// <c>IModelGeometryLoader.Load</c>, no sidecar.
/// </summary>
[Trait("Tier", "regression")]
public class SoftLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_SOFT_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_SOFT_RUN is unset");

    private static IModelGeometryLoader Loader() => RunContent.Loader(RunFolder);

    private static CarveModel Saved(string path) => RunContent.Saved(path);

    private static string SamplePath() => RunContent.SamplePath("gardener-soft.carve");

    private static string Key(Vector3 v) => $"{v.X:0.0000},{v.Y:0.0000},{v.Z:0.0000}";

    /// <summary>The corners (position, normal) the saved model's triangles give, as the export writes them.</summary>
    private static List<(Vector3 Position, Vector3 Normal)> Expected(CarveModel model) =>
    [
        .. model.Meshes.SelectMany(m => CarveShading.Triangles(m.Mesh))
            .SelectMany(t => new[] { (Position: t.A, Normal: t.NormalA), (Position: t.B, Normal: t.NormalB), (Position: t.C, Normal: t.NormalC) }),
    ];

    private static List<(Vector3 Position, Vector3 Normal)> Loaded(string file)
    {
        var data = Loader().Load(new ContentPath(file)).MeshData;
        return [.. data.Indices!.Select(i => (data.Vertices[(int)i].Position, data.Vertices[(int)i].Normal))];
    }

    [SkippableOrRun]
    public void The_model_loads_with_the_bones_in_order_and_the_clips_named()
    {
        var saved = Saved(Path.Combine(RunFolder, "work", "gardener-soft.carve"));
        var model = Loader().Load(new ContentPath("models/gardener-soft.glb"));
        var skin = model.Skin!;
        Assert.Equal(22, skin.Skeleton.Bones.Count());
        Assert.Equal(saved.Skeleton!.Names, skin.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(["Idle", "Walk"], skin.Animations.Select(a => a.Name));
        Assert.Empty(model.Warnings);
    }

    [SkippableOrRun]
    public void The_triangle_count_is_the_subdivided_models_and_within_the_budget()
    {
        var saved = Saved(Path.Combine(RunFolder, "work", "gardener-soft.carve"));
        var soft = Loaded("models/gardener-soft.glb").Count / 3;
        var flat = Loaded("models/gardener-flat.glb").Count / 3;
        Assert.Equal(saved.Meshes.Sum(m => CarveShading.Triangles(m.Mesh).Count), soft);
        Assert.Equal(680, flat);
        Assert.Equal(float.Parse(File.ReadAllText(Path.Combine(RunFolder, "triangles.txt")).Trim()), soft);
        Assert.InRange(soft, flat + 1, 5000);
    }

    [SkippableOrRun]
    public void The_loaded_normals_are_the_corner_normals_of_the_saved_model()
    {
        var saved = Saved(Path.Combine(RunFolder, "work", "gardener-soft.carve"));
        var expected = Expected(saved).Select(c => Key(c.Position) + " " + Key(c.Normal)).Order().ToList();
        var loaded = Loaded("models/gardener-soft.glb").Select(c => Key(c.Position) + " " + Key(c.Normal)).Order().ToList();
        Assert.Equal(expected.Count, loaded.Count);
        Assert.Equal(expected, loaded);
        Assert.All(Loaded("models/gardener-soft.glb"), c => Assert.InRange(c.Normal.Length(), 0.999f, 1.001f));
    }

    [SkippableOrRun]
    public void Shading_runs_on_across_soft_edges_and_breaks_across_the_brims_hard_ones()
    {
        var saved = Saved(Path.Combine(RunFolder, "work", "gardener-soft.carve"));
        var hat = CarveShading.Triangles(saved.Find("Hat")!).SelectMany(t => new[] { (Position: t.A, Normal: t.NormalA), (Position: t.B, Normal: t.NormalB), (Position: t.C, Normal: t.NormalC) }).ToList();
        var byPosition = hat.GroupBy(c => Key(c.Position)).ToList();
        int Distinct(IGrouping<string, (Vector3 Position, Vector3 Normal)> g) => g.Select(c => Key(c.Normal)).Distinct().Count();

        // A crown vertex has one normal at every face it meets (all edges soft); a vertex on the brim's top rim has two (cap and wall).
        var crown = byPosition.Where(g => g.First().Position.Y is > 1.37f and < 1.49f).ToList();
        var rim = byPosition.Where(g => g.First().Position.Y is > 1.345f and < 1.36f && new Vector2(g.First().Position.X, g.First().Position.Z).Length() > 0.2f).ToList();
        Assert.NotEmpty(crown);
        Assert.NotEmpty(rim);
        Assert.All(crown, g => Assert.Equal(1, Distinct(g)));
        Assert.All(rim, g => Assert.Equal(2, Distinct(g)));

        // The same on what the game loads, from the glb.
        var loaded = Loaded("models/gardener-soft.glb")
            .Where(c => c.Position.Y is > 1.33f)
            .GroupBy(c => Key(c.Position))
            .ToList();
        var loadedCrown = loaded.Where(g => g.First().Position.Y is > 1.37f and < 1.49f).ToList();
        Assert.NotEmpty(loadedCrown);
        Assert.All(loadedCrown, g => Assert.Single(g.Select(c => Key(c.Normal)).Distinct()));
    }

    [SkippableOrRun]
    public void The_sample_is_the_soft_gardener_the_tools_built_by_content()
    {
        // Compared by content, not bytes: each is read, then written the one way, which gives floats back bit for bit.
        var sample = CarveFile.Write(Saved(SamplePath()));
        var built = CarveFile.Write(Saved(Path.Combine(RunFolder, "work", "gardener-soft.carve")));
        Assert.Equal(built, sample);
    }

    [SkippableOrRun]
    public void The_flat_export_is_clean_and_the_soft_ones_warnings_are_the_non_planar_faces_select_finds()
    {
        Assert.DoesNotContain("warning(s)", File.ReadAllText(Path.Combine(RunFolder, "export-flat-result.txt")));
        var soft = File.ReadAllText(Path.Combine(RunFolder, "export-result.txt"));
        var at = soft.IndexOf("warning(s): ", StringComparison.Ordinal);
        Assert.True(at >= 0, "subdivide leaves faces that are not planar; the export lists them");
        var warnings = soft[(at + "warning(s): ".Length)..].Split("; ");
        Assert.All(warnings, w => Assert.Contains("face is not planar", w));
        // As many as select finds non_planar on the same model, mesh by mesh (the person, 2026-11-10, until T-72 lands).
        var nonPlanar = new[] { "Body", "Hat" }.Sum(name => System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RunFolder, $"select-non-planar-{name}.txt"))).RootElement.GetProperty("count").GetInt32());
        Assert.True(nonPlanar > 0, "select finds no non_planar face on the soft gardener");
        Assert.Equal(nonPlanar, warnings.Length);
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_SOFT_RUN") is null)
        {
            Skip = "CARVE_SOFT_RUN is unset";
        }
    }
}
