using ModelContextProtocol.Protocol;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Weathered;

/// <summary>
/// Stage 4: the run folder's iron graph renamed on disk, then <c>render</c>, <c>check</c> and <c>export</c> each answer with T-166's whole message
/// (its table's "graph file missing" row: the path and the <c>.texgraph</c> files in the folder, render's and export's with the "after the fact"
/// tail); renamed back, all three succeed.
/// </summary>
internal sealed class ErrorsStage : IWeatheredStage
{
    public const string Tail = "; the model is unchanged: define the material again with a graph that bakes, or restore the file";

    public async Task RunAsync(WeatheredRun run)
    {
        var driver = run.Driver;
        var graph = Barrel.Graph(run, "weathered-iron");
        var folder = Path.GetDirectoryName(graph)!;
        var renamed = Path.Combine(folder, "weathered-iron-renamed.texgraph");
        var glb = run.InRun(Path.Combine("errors", "barrel.glb"));
        Directory.CreateDirectory(Path.GetDirectoryName(glb)!);
        var front = new Camera(SeenStage.Front).Args();

        File.Move(graph, renamed);
        var cause = $"no file at {graph}; the .texgraph files in {folder} (3) are: planks.texgraph, weathered-iron-renamed.texgraph, weathered-staves.texgraph";
        var slots = new[] { "base_colour_texture", "metallic_roughness_texture" }.Select(s => $"materials['weathered-iron'].{s}: {cause}").ToArray();

        foreach (var (tool, args) in new (string, object)[] { ("render", front), ("export", new { path = glb }) })
        {
            var (error, text) = await Call(driver, tool, args);
            run.Log.Add($"renamed: {tool}: {(error ? "refused" : "answered")}: {text}");
            if (!error || !slots.Any(s => text == s + Tail)) run.Note($"renamed: {tool} {(error ? "refused" : "answered")} '{text}', not T-166's '{slots[0] + Tail}' (or its metallic_roughness_texture twin)");
        }

        var check = await CallJsonAsync(driver, "check", new { });
        var warnings = check.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToArray();
        run.Log.Add($"renamed: check: {string.Join(" | ", warnings)}");
        foreach (var s in slots.Where(s => !warnings.Contains(s))) run.Note($"renamed: check's warnings [{string.Join(" | ", warnings)}] lack T-166's '{s}'");

        File.Move(renamed, graph);
        foreach (var (tool, args) in new (string, object)[] { ("render", front), ("export", new { path = glb }) })
        {
            var (error, text) = await Call(driver, tool, args);
            if (error) run.Note($"renamed back: {tool} refused: {text}");
        }

        var again = await CallJsonAsync(driver, "check", new { });
        if (again.GetProperty("warnings").GetArrayLength() != 0) run.Note($"renamed back: check warns: {again.GetProperty("warnings")}");
    }

    private static async Task<(bool Error, string Text)> Call(Carve.Debug.ScenarioDriver driver, string tool, object args)
    {
        var result = await driver.CallAsync(tool, args);
        return (result.IsError is true, result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "");
    }
}
