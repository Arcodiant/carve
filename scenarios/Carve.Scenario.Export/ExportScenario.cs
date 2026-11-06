using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Crate;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Export;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class ExportExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, ExportScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The milestone's sample, proved to the end: the crate is built (the Crate scenario's own sequence), saved,
/// reopened unchanged and exported into <c>game/Content/models/</c> of the run folder, which stands in for a
/// game's content folder. <c>work/</c> holds the <c>.carve</c> file. The export's result line is written to
/// <c>export-result.txt</c> in the run folder, for the viewer's checks that run after this.
/// </summary>
public sealed class ExportScenario : IScenario
{
    public const string ResultFile = "export-result.txt";

    public async Task RunAsync(ScenarioDriver driver)
    {
        var carvePath = Path.Combine(driver.RunFolder, "work", "crate.carve");
        var glbPath = Path.Combine(driver.RunFolder, "game", "Content", "models", "crate.glb");

        // 1. The crate, counts checked after every call, and its native front.
        await CrateBuild.BuildAsync(driver);
        var meshBefore = await TextAsync(driver, "get_mesh", new { });
        var front = await driver.CaptureAsync("front", new { view = "front" });

        // 2. save: the path, 72 vertices and 70 faces; a new file.
        var saved = await TextAsync(driver, "save", new { path = carvePath });
        Expect(driver, "save", saved, "crate.carve", carvePath, "72 vertices, 70 faces");
        ExpectNot(driver, "save", saved, "replaced");
        var savedBytes = await File.ReadAllBytesAsync(carvePath);

        // 3. The example cube replaces it, then open brings the crate back: the report, get_mesh and the frame.
        await CrateBuild.DefineAsync(driver);
        var opened = await CrateBuild.CallJsonAsync(driver, "open", new { path = carvePath });
        CrateBuild.CheckCounts(driver, "open", opened, 72, 70);
        if (opened.GetProperty("warnings").GetArrayLength() != 0 || opened.GetProperty("path").GetString() != carvePath)
        {
            driver.Fail($"open: expected no warnings and the path {carvePath}, got {opened}");
        }

        var meshOpened = await TextAsync(driver, "get_mesh", new { });
        if (meshOpened != meshBefore)
        {
            driver.Fail("open: get_mesh differs from the one taken before the save");
        }

        var reopened = await driver.CaptureAsync("front-reopened", new { view = "front" });
        driver.AssertBytesEqual("front, after save and open (pixels)", front.Pixels, reopened.Pixels);
        driver.AssertBytesEqual("front, after save and open (png)", front.Png, reopened.Png);

        // 4. open is one undo step and gives the cube back; open again.
        var undone = await CrateBuild.CallJsonAsync(driver, "undo", new { });
        if (undone.GetProperty("undone").GetString() != "open")
        {
            driver.Fail($"undo: expected to undo open, got {undone}");
        }

        CrateBuild.CheckCounts(driver, "undo", undone, 8, 6);
        await CrateBuild.CallJsonAsync(driver, "open", new { path = carvePath });

        // 5. save to the same path: replaced, the same bytes.
        var savedAgain = await TextAsync(driver, "save", new { path = carvePath });
        Expect(driver, "save (again)", savedAgain, "crate.carve", carvePath, "72 vertices, 70 faces", "(replaced)");
        driver.AssertBytesEqual("crate.carve, saved again", savedBytes, await File.ReadAllBytesAsync(carvePath));

        // 6. export into the game's content folder.
        var exported = await TextAsync(driver, "export", new { path = glbPath });
        Expect(driver, "export", exported, "crate.glb", glbPath, "140 triangles", "1 material 'carve'", "size 1.04 x 0.8 x 1 m");
        ExpectNot(driver, "export", exported, "replaced");
        var glbBytes = await File.ReadAllBytesAsync(glbPath);
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, ResultFile), exported);

        var exportedAgain = await TextAsync(driver, "export", new { path = glbPath });
        Expect(driver, "export (again)", exportedAgain, "crate.glb", "140 triangles", "(replaced)");
        driver.AssertBytesEqual("crate.glb, exported again", glbBytes, await File.ReadAllBytesAsync(glbPath));

        // 7. Error paths: each a tool error, and the next front render is the same as the last.
        var last = front;
        last = await ErrorThenFrontAsync(driver, last, "after-open-error", "open", new { path = Path.Combine(driver.RunFolder, "work", "crates.carve") }, "crates.carve", "crate.carve");
        last = await ErrorThenFrontAsync(driver, last, "after-save-error", "save", new { path = Path.Combine(driver.RunFolder, "work", "crate.glb") }, "crate.glb", ".carve");
        last = await ErrorThenFrontAsync(driver, last, "after-export-error", "export", new { path = Path.Combine(driver.RunFolder, "game", "Content", "models", "crate.carve") }, "crate.carve", ".glb");

        // Neither wrong-extension call left a file behind.
        foreach (var stray in new[] { Path.Combine(driver.RunFolder, "work", "crate.glb"), Path.Combine(driver.RunFolder, "game", "Content", "models", "crate.carve") })
        {
            if (File.Exists(stray))
            {
                driver.Fail($"a failed call left {stray}");
            }
        }

        if (!File.ReadAllBytes(glbPath).SequenceEqual(glbBytes) || !File.ReadAllBytes(carvePath).SequenceEqual(savedBytes))
        {
            driver.Fail("a failed call changed the saved files");
        }
    }

    private static async Task<ScenarioFrame> ErrorThenFrontAsync(ScenarioDriver driver, ScenarioFrame last, string name, string tool, object arguments, params string[] mentions)
    {
        await CrateBuild.ExpectToolErrorAsync(driver, tool, arguments, mentions);
        var frame = await driver.CaptureAsync("front-" + name, new { view = "front" });
        driver.AssertBytesEqual($"front, {name} (pixels)", last.Pixels, frame.Pixels);
        driver.AssertBytesEqual($"front, {name} (png)", last.Png, frame.Png);
        return frame;
    }

    /// <summary>A call that must succeed; its first text block.</summary>
    private static async Task<string> TextAsync(ScenarioDriver driver, string tool, object arguments)
    {
        var result = await driver.CallAsync(tool, arguments);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError is true)
        {
            driver.Fail($"{tool}: {text}");
        }

        return text;
    }

    private static void Expect(ScenarioDriver driver, string what, string text, params string[] parts)
    {
        foreach (var part in parts)
        {
            if (!text.Contains(part, StringComparison.Ordinal))
            {
                driver.Fail($"{what}: expected '{part}' in: {text}");
            }
        }
    }

    private static void ExpectNot(ScenarioDriver driver, string what, string text, string part)
    {
        if (text.Contains(part, StringComparison.Ordinal))
        {
            driver.Fail($"{what}: did not expect '{part}' in: {text}");
        }
    }
}
