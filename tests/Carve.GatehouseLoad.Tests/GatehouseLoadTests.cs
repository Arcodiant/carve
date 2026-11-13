using System.Numerics;
using Empyrean;
using Carve.LoadChecks;
using Xunit;
using CarveFile = Carve.Core.CarveFile;
using CarveModel = Carve.Core.Model;
using CarveShading = Carve.Core.Shading;
using FaceStyle = Carve.Core.FaceStyle;

namespace Carve.GatehouseLoad.Tests;

/// <summary>
/// What a game sees of the exported gatehouse: the last step of suite <c>scenario-gatehouse</c>. The run folder comes from
/// <c>CARVE_GATEHOUSE_RUN</c>, which <c>scripts/run-gatehouse-scenario.ps1</c> sets; with it unset every test is skipped (the
/// script fails a run that skipped one). The file is read as the game would read it: a content folder mounted on the engine's
/// registry and <c>IModelGeometryLoader.Load</c>, no sidecar.
/// </summary>
[Trait("Tier", "regression")]
public class GatehouseLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_GATEHOUSE_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_GATEHOUSE_RUN is unset");

    private static CarveModel Saved() => RunContent.Saved(Path.Combine(RunFolder, "work", "gatehouse.carve"));

    private static string Key(Vector3 v) => $"{v.X:0.0000},{v.Y:0.0000},{v.Z:0.0000}";

    [SkippableOrRun]
    public void The_sample_is_the_gatehouse_the_tools_built_by_content()
    {
        // Compared by content, not bytes: each is read, then written the one way, which gives floats back bit for bit.
        var sample = CarveFile.Write(RunContent.Saved(RunContent.SamplePath("gatehouse.carve")));
        var built = CarveFile.Write(Saved());
        Assert.Equal(built, sample);
    }

    [SkippableOrRun]
    public void The_export_loads_with_the_live_triangles_only()
    {
        var saved = Saved();
        var mesh = Assert.Single(saved.Meshes).Mesh;
        Assert.True(mesh.HasTombstones, "the saved gatehouse should keep its tombstones");
        var model = RunContent.Loader(RunFolder).Load(new ContentPath("models/gatehouse.glb"));
        Assert.Empty(model.Warnings);
        // Worked from the faces, not from the product's triangulation: a live face of n corners (its holes' included) and
        // h holes is n - 2 + 2h triangles, and a deleted face is none.
        var faces = mesh.LiveFaces.ToArray();
        Assert.Equal(19, faces.Length);
        var triangles = faces.Sum(f => mesh.Faces[f].Loop.Count + mesh.Faces[f].Holes.Sum(h => h.Count) - 2 + 2 * mesh.Faces[f].Holes.Count);
        var data = model.MeshData;
        Assert.Equal(triangles * 3, data.Indices!.Length);
        // Every corner the file gives is at a corner of a live face, so none is where only a deleted vertex is.
        var live = faces.SelectMany(f => mesh.Faces[f].Loop.Concat(mesh.Faces[f].Holes.SelectMany(h => h))).Select(v => Key(mesh.Vertices[v])).ToHashSet();
        Assert.All(data.Indices!, i => Assert.Contains(Key(data.Vertices[(int)i].Position), live));
        // The product's triangulation, which the export draws with, agrees with the count.
        Assert.Equal(triangles, CarveShading.Triangles(mesh).Count);
    }

    [SkippableOrRun]
    public void The_doors_material_is_present_and_one_other_is_the_wall()
    {
        var model = RunContent.Loader(RunFolder).Load(new ContentPath("models/gatehouse.glb"));
        var door = FaceStyle.ToLinear("#6b4423");
        var materials = model.SubmeshMaterials.Select(m => m!).ToArray();
        Assert.Equal(2, materials.Length);
        Assert.Contains(materials, m => m.BaseColor == door);
        Assert.Equal(2, model.MeshData.Submeshes!.Length);
    }

    [SkippableOrRun]
    public void The_export_is_clean_the_gatehouse_trips_no_limit()
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
        if (Environment.GetEnvironmentVariable("CARVE_GATEHOUSE_RUN") is null)
        {
            Skip = "CARVE_GATEHOUSE_RUN is unset";
        }
    }
}
