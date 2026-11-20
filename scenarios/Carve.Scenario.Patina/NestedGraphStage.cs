using System.Text.Json;
using Carve.Debug;

namespace Carve.Scenario.Patina;

/// <summary>
/// T-140's stage, graphs as nodes on the real patina: open samples/graphs/old-wall.texgraph (bricks and grime used as
/// nodes, read from beside it), check open names the two nested files, export it and compare each PNG with
/// samples/graphs/old-wall/*.png byte for byte, and capture its sheet (flat, tiled and the sphere) and grime's at its
/// defaults for the person to judge.
/// </summary>
internal sealed class NestedGraphStage : IPatinaStage
{
    private static readonly string[] Roles = ["base_colour", "normal", "roughness", "occlusion"];

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var samples = Path.Combine(FindRoot(), "samples", "graphs");
        var opened = await checks.CallAsync("open", JsonSerializer.Serialize(new { path = Path.Combine(samples, "old-wall.texgraph") }));
        checks.Check(opened.Contains($"read 2 nested files for its graph nodes: {Path.Combine(samples, "grime.texgraph")}, {Path.Combine(samples, "bricks.texgraph")}", StringComparison.Ordinal),
            "nested: open did not name the nested files it read: " + opened);
        checks.Check(opened.Contains("no faults: it evaluates", StringComparison.Ordinal), "nested: old-wall opened with faults: " + opened);

        var folder = Path.Combine(driver.RunFolder, "export-old-wall");
        var result = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder }));
        checks.Check(result.StartsWith($"export: wrote 4 files to {folder} at size 256\n", StringComparison.Ordinal), "nested: export said " + result);
        foreach (var role in Roles)
        {
            var path = Path.Combine(folder, role + ".png");
            checks.Check(File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(samples, "old-wall", role + ".png"))),
                $"nested: old-wall's {role}.png is not the sample's bytes");
        }

        var graph = await checks.GraphAsync();
        checks.Check(graph.Contains("graph nodes (2), paths resolved against the graph's own folder", StringComparison.Ordinal), "nested: get_graph did not list the graph nodes: " + graph);
        await driver.CaptureToolAsync("swatch", "nested-old-wall-sheet", new { view = "sheet" });

        await checks.CallAsync("open", JsonSerializer.Serialize(new { path = Path.Combine(samples, "grime.texgraph") }));
        await driver.CaptureToolAsync("swatch", "nested-grime-sheet", new { view = "sheet" });
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Carve.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Carve.slnx was not found above " + AppContext.BaseDirectory);
    }
}
