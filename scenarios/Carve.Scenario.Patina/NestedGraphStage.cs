using System.Text.Json;
using Carve.Debug;

namespace Carve.Scenario.Patina;

/// <summary>
/// T-140's stage, graphs as nodes on the real patina: open samples/graphs/old-wall.texgraph (bricks and grime used as
/// nodes, read from beside it), check open names the two nested files, export it and compare each PNG with
/// samples/graphs/old-wall/*.png byte for byte, and its sheet (flat, tiled and the sphere) and grime's at their defaults, each
/// judged against its export and its sphere view (RV-76; before, they were captured for the person to judge alone).
/// </summary>
internal sealed class NestedGraphStage : IPatinaStage
{
    private static readonly string[] Roles = ["base_colour", "normal", "roughness", "occlusion"];

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var samples = Path.Combine(SwatchStage.RepoRoot(), "samples", "graphs");
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
        await SheetAsync(driver, checks, "old-wall", Path.Combine(samples, "old-wall.texgraph"), folder);

        await checks.CallAsync("open", JsonSerializer.Serialize(new { path = Path.Combine(samples, "grime.texgraph") }));
        var grime = Path.Combine(driver.RunFolder, "export-grime");
        await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = grime }));
        await SheetAsync(driver, checks, "grime", Path.Combine(samples, "grime.texgraph"), grime);
    }

    /// <summary>
    /// The open graph's sheet at its defaults, judged (RV-76): every flat and tiled cell against the graph's export in
    /// <paramref name="texels"/>, and the sphere cell against the sphere view, whose silhouette is judged by the planks' rule.
    /// </summary>
    private static async Task SheetAsync(ScenarioDriver driver, PatinaChecks checks, string name, string graphPath, string texels)
    {
        var sheet = await driver.CaptureToolAsync("swatch", $"nested-{name}-sheet", new { view = "sheet" });
        var sphere = await driver.CaptureToolAsync("swatch", $"nested-{name}-sphere", new { view = "sphere" });
        var (outside, slateInside, inside) = LibrarySphere.Silhouette(sphere, 0);
        checks.Check(outside == 0 && slateInside <= inside * SwatchStage.SlateInside,
            $"nested: {name} sphere: {outside} pixel(s) outside the silhouette are not the slate, {slateInside} of {inside} inside are");
        LibraryStage.CheckGraphSheet(checks, name, graphPath, texels, sheet, sphere);
    }
}
