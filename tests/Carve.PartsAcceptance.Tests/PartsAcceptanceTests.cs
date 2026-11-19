using System.Text.Json;
using Carve.LoadChecks;
using Empyrean;
using Xunit;
using CarveFile = Carve.Core.CarveFile;
using CarveModel = Carve.Core.Model;

namespace Carve.PartsAcceptance.Tests;

/// <summary>
/// T-98's pass criteria on one round folder (<c>CARVE_PARTS_ACCEPTANCE_RUN</c>): the saved <c>cart.carve</c> has no envelope warning, two moving wheels one an instance
/// of the other, and level 0 on every part with geometry; <c>cart.glb</c> loads, split, with the wheel nodes sharing one mesh; the viewer loaded it.
/// </summary>
[Trait("Tier", "regression")]
public class PartsAcceptanceTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_PARTS_ACCEPTANCE_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_PARTS_ACCEPTANCE_RUN is unset");

    private static CarveModel Saved()
    {
        var path = Path.Combine(RunFolder, "cart.carve");
        var read = CarveFile.Read(File.ReadAllText(path), RunFolder);
        return read.Model ?? throw new InvalidOperationException($"{path}: {string.Join("; ", read.Errors)}");
    }

    [SkippableOrRun]
    public void The_saved_cart_has_no_envelope_warning()
    {
        Assert.Empty(Saved().EnvelopeWarnings());
    }

    [SkippableOrRun]
    public void The_saved_cart_has_two_moving_wheels_one_an_instance_of_the_other()
    {
        var parts = Saved().Parts;
        var wheels = parts.Where(p => p.Name.StartsWith("Wheel", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, wheels.Count);
        Assert.All(wheels, w => Assert.True(w.Moves, $"{w.Name} moves"));
        var instance = Assert.Single(wheels, w => w.InstanceOf is not null);
        Assert.Contains(wheels, w => w.Name == instance.InstanceOf);
    }

    [SkippableOrRun]
    public void Level_0_is_kept_on_every_part_with_geometry()
    {
        var model = Saved();
        var owners = model.Parts.Where(p => p.InstanceOf is null && model.Find(p.Name) is not null).ToList();
        Assert.True(owners.Count >= 3, "the bed, the handle and a wheel own geometry");
        Assert.All(owners, p => Assert.Contains(p.Levels, l => l.Number == 0));
    }

    [SkippableOrRun]
    public void The_split_load_has_the_wheel_nodes_sharing_one_mesh_and_no_warning()
    {
        var parsed = RunContent.Loader(RunFolder).Load(new ContentPath("models/cart.glb"), new ModelParseOptions(SplitParts: true));
        Assert.Empty(parsed.Warnings ?? []);
        var parts = parsed.Parts!.Parts;
        var wheels = parts.Where(p => p.Name.StartsWith("Wheel", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, wheels.Count);
        Assert.Equal(wheels[0].MeshIndex, wheels[1].MeshIndex);
        Assert.NotEqual(wheels[0].Translation, wheels[1].Translation);
        Assert.Equal(2, parsed.Parts!.Meshes.Length);
    }

    [SkippableOrRun]
    public void The_viewer_loaded_the_export()
    {
        var view = JsonDocument.Parse(File.ReadAllText(Path.Combine(RunFolder, "viewer", "view.json"))).RootElement;
        Assert.Equal("models/cart.glb", view.GetProperty("model").GetString());
        Assert.True(view.GetProperty("triangles").GetInt32() > 0);
        Assert.True(File.Exists(Path.Combine(RunFolder, "viewer", "front.png")));
    }
}

public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_PARTS_ACCEPTANCE_RUN") is null)
        {
            Skip = "CARVE_PARTS_ACCEPTANCE_RUN is unset";
        }
    }
}
