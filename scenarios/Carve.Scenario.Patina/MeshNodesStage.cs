using System.Text;
using System.Text.Json;
using Carve.Debug;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// T-162's stage, a graph that reads the mesh on the real patina: samples/graphs/weathered-iron.texgraph defined through
/// the tools, swatched (sheet and sphere), its title and results naming the neutral mesh inputs, <c>wear</c> black on every
/// texel and the base colour the clean iron (the same graph exported with wear_amount 0), then saved, opened and exported,
/// every PNG compared byte for byte with samples/graphs/weathered-iron/. The title's note is judged by how far the title
/// runs past the same graph's with no mesh node. One break, made here: the note left out of the expected title must fail.
/// Writes mesh-nodes-breaks.txt.
/// </summary>
internal sealed class MeshNodesStage : IPatinaStage
{
    private const int TitleHeight = 24;
    private const string Note = "mesh inputs neutral: curv (mesh_curvature)";
    /// <summary>The note is 21 characters of the sheet's font, well over 8 px each less a margin; the title runs at least this far past one without it.</summary>
    private const int NoteWidth = 80;

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var samples = Path.Combine(SwatchStage.RepoRoot(), "samples", "graphs");
        var body = JsonDocument.Parse(File.ReadAllText(Path.Combine(samples, "weathered-iron.texgraph"))).RootElement;
        var define = JsonSerializer.Serialize(new { graph = body });

        var defined = await checks.CallAsync("define_graph", define);
        checks.Check(defined.EndsWith("no faults: it evaluates", StringComparison.Ordinal), "mesh nodes: weathered-iron has faults: " + defined);

        var flat = await checks.CallAsync("swatch", """{"view":"flat","output":"wear","size":64}""");
        checks.Check(flat.Contains(Note, StringComparison.Ordinal), "mesh nodes: swatch's result does not name the neutral mesh node: " + flat);
        var sheet = await driver.CaptureToolAsync("swatch", "mesh-nodes-iron-sheet", new { view = "sheet" });
        await driver.CaptureToolAsync("swatch", "mesh-nodes-iron-sphere", new { view = "sphere" });
        var extent = TitleExtent(sheet);

        // The same graph with the mesh node swapped for a gradient: no note, so a title as long as the graph's without it.
        await checks.CallAsync("edit_graph", """{"operations":[{"op":"set_node","node":"curv","type":"gradient"}]}""");
        var plainText = await checks.CallAsync("swatch", """{"view":"flat","output":"wear","size":64}""");
        checks.Check(!plainText.Contains("mesh inputs", StringComparison.Ordinal), "mesh nodes: a graph with no mesh node was given the note: " + plainText);
        var plain = TitleExtent(await driver.CaptureToolAsync("swatch", "mesh-nodes-plain-sheet", new { view = "sheet" }));
        checks.Check(extent - plain >= NoteWidth, $"mesh nodes: the sheet's title runs to x {extent}, only {extent - plain} px past the title with no note ({plain}); the note is not on it");

        await checks.CallAsync("define_graph", define);
        var texels = Path.Combine(driver.RunFolder, "mesh-nodes-export");
        var exported = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = texels }));
        checks.Check(exported.StartsWith("export: wrote 3 files", StringComparison.Ordinal) && exported.EndsWith(Note + "; the real maps come from the mesh when a carve material names the graph", StringComparison.Ordinal),
            "mesh nodes: export said " + exported);
        var wear = Decode(Path.Combine(texels, "wear.png"));
        var lit = Enumerable.Range(0, wear.Length / 4).Count(i => wear[i * 4] != 0 || wear[i * 4 + 1] != 0 || wear[i * 4 + 2] != 0);
        checks.Check(lit == 0, $"mesh nodes: wear is not black on {lit} texel(s) with neutral mesh inputs");

        var clean = Path.Combine(driver.RunFolder, "mesh-nodes-clean-iron");
        await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = clean, outputs = new[] { "base_colour" }, parameters = new { wear_amount = 0 } }));
        checks.Check(File.ReadAllBytes(Path.Combine(clean, "base_colour.png")).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(texels, "base_colour.png"))),
            "mesh nodes: the base colour with neutral inputs is not the clean iron (wear_amount 0)");

        var before = await checks.GraphAsync();
        var path = Path.Combine(driver.RunFolder, "weathered-iron.texgraph");
        await checks.CallAsync("save", JsonSerializer.Serialize(new { path }));
        var opened = await checks.CallAsync("open", JsonSerializer.Serialize(new { path }));
        checks.Check(opened.Contains(Note, StringComparison.Ordinal), "mesh nodes: open did not name the mesh node: " + opened);
        checks.Check(await checks.GraphAsync() == before, "mesh nodes: the saved and opened graph is not the one saved");

        var again = Path.Combine(driver.RunFolder, "mesh-nodes-export-opened");
        await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = again }));
        foreach (var file in new[] { "base_colour.png", "roughness.png", "wear.png" })
        {
            var committed = File.ReadAllBytes(Path.Combine(samples, "weathered-iron", file));
            checks.Check(File.ReadAllBytes(Path.Combine(texels, file)).AsSpan().SequenceEqual(committed), $"mesh nodes: {file} exported from the defined graph is not the sample's bytes");
            checks.Check(File.ReadAllBytes(Path.Combine(again, file)).AsSpan().SequenceEqual(committed), $"mesh nodes: {file} exported from the opened graph is not the sample's bytes");
        }

        // The break, made here: the title expected without the note must fail the check above.
        var probe = new PatinaChecks(driver);
        probe.Check(extent - plain < 8, $"mesh nodes: the sheet's title runs to x {extent}, {extent - plain} px past the title with no note");
        var log = new StringBuilder("== the note left out of the expected title\n");
        foreach (var failure in probe.Failures) log.AppendLine("  " + failure);
        checks.Check(probe.Failures.Count > 0, "mesh nodes: the title without its note was accepted");
        File.WriteAllText(Path.Combine(driver.RunFolder, "mesh-nodes-breaks.txt"), log.ToString());
    }

    /// <summary>The rightmost bright column of the title strip.</summary>
    private static int TitleExtent(ScenarioFrame sheet)
    {
        var right = 0;
        for (var y = 0; y < TitleHeight; y++)
            for (var x = sheet.Width - 1; x > right; x--)
                if (SwatchStage.Bright(sheet, x, y, 1, 1)) { right = x; break; }
        return right;
    }

    private static byte[] Decode(string path)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        return image.Data;
    }
}
