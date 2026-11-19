using System.Text.Json;
using Carve.Debug;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// T-115's stage, export end to end on the real patina: open samples/graphs/planks.texgraph, export it at its own size
/// (256) into the run folder and compare each PNG with samples/graphs/planks/*.png byte for byte, name the slots, export
/// at 1024 (base colour, normal and occlusion through outputs, then roughness alone) and check by ExportMatch's rule that
/// each is the same picture at four times the resolution (and that the rule catches the faults ExportMatch.Faults makes
/// on it: a half-tile shift, a u-mirror and a flat map for base colour and roughness, a flat map for occlusion; the
/// normal under its own rule, failing a u-mirror, a flat map and shifts in u of a quarter stave, one texel and half a
/// texel), then the error
/// paths: a size that is not a power of two, a role the graph has no output for, and a folder that is a file.
/// </summary>
internal sealed class ExportStage : IPatinaStage
{
    private static readonly string[] Roles = ["base_colour", "normal", "roughness", "occlusion"];

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var samples = Path.Combine(FindRoot(), "samples", "graphs");
        await checks.CallAsync("open", JsonSerializer.Serialize(new { path = Path.Combine(samples, "planks.texgraph") }));

        var small = Path.Combine(driver.RunFolder, "export-256");
        var result = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = small }));
        checks.Check(result.StartsWith($"export: wrote 4 files to {small} at size 256\n", StringComparison.Ordinal), "export: export said " + result);
        foreach (var role in Roles)
        {
            var path = Path.Combine(small, role + ".png");
            var expected = File.ReadAllBytes(Path.Combine(samples, "planks", role + ".png"));
            checks.Check(File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(expected), $"export: {role}.png at 256 is not the sample's bytes");
            var slot = role == "roughness" ? "metallic_roughness_texture" : role + "_texture";
            checks.Check(result.Contains($"- {role}.png: 256x256, {expected.Length} bytes, slot {slot}", StringComparison.Ordinal), $"export: the answer has no line for {role}: {result}");
        }

        // The same picture at four times the resolution under ExportMatch's rule: each 256 texel against the mean of the
        // four 1024 texels at its centre, per RGB channel, within 4 levels (the normal: 4 where flat, 32 at a kink); then
        // each of ExportMatch's faults must fail.
        var large = Path.Combine(driver.RunFolder, "export-1024");
        var big = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = large, size = 1024, outputs = new[] { "base_colour", "normal", "occlusion" } }));
        checks.Check(big.StartsWith($"export: wrote 3 files to {large} at size 1024\n", StringComparison.Ordinal), "export: the 1024 export said " + big);
        checks.Check(!File.Exists(Path.Combine(large, "roughness.png")), "export: outputs base_colour, normal and occlusion wrote a roughness.png as well");
        // Roughness too, alone into the same folder, so the rule and its faults hold for every output the ruling covers.
        var rough = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = large, size = 1024, outputs = new[] { "roughness" } }));
        checks.Check(rough.StartsWith($"export: wrote 1 file to {large} at size 1024\n", StringComparison.Ordinal), "export: the 1024 roughness export said " + rough);
        foreach (var role in Roles)
        {
            var a = Decode(Path.Combine(small, role + ".png"));
            var b = Decode(Path.Combine(large, role + ".png"));
            checks.Check(a.Width == 256 && b.Width == 1024 && b.Height == 1024, $"export: {role} sizes are {a.Width} and {b.Width}x{b.Height}, not 256 and 1024");
            if (b.Width != 1024 || b.Height != 1024 || a.Width != 256 || a.Height != 256) continue;
            if (role == "normal")
            {
                // The normal's own rule (RV-68): flat texels (local step 4 or less) within 4, the planks' kinks within 32;
                // a u-mirror, a flat map and shifts in u of a quarter stave, one texel and half a texel must fail it.
                var good = ExportMatch.NormalDifferences(a.Data, 256, b.Data, 1024);
                checks.Check(ExportMatch.NormalMatches(a.Data, 256, b.Data, 1024), $"export: normal at 1024 is not the 256 picture at matching uv (flat {good.Flat:0.0}, allowed {ExportMatch.NormalFlatTolerance}; kink {good.Kink:0.0}, allowed {ExportMatch.NormalKinkTolerance})");
                foreach (var (fault, data) in ExportMatch.Faults(role, b.Data, 1024))
                {
                    var wrong = ExportMatch.NormalDifferences(a.Data, 256, data, 1024);
                    checks.Check(!ExportMatch.NormalMatches(a.Data, 256, data, 1024), $"export: normal at 1024 with a {fault} passed the normal's rule (flat {wrong.Flat:0.0}, kink {wrong.Kink:0.0})");
                }
                continue;
            }
            var largest = ExportMatch.LargestDifference(a.Data, 256, b.Data, 1024);
            checks.Check(largest <= ExportMatch.Tolerance, $"export: {role} at 1024 is not the 256 picture at matching uv (largest difference {largest:0.0}, allowed {ExportMatch.Tolerance})");

            // The rule shown failing: each fault made on the 1024 picture must be caught.
            foreach (var (fault, data) in ExportMatch.Faults(role, b.Data, 1024))
            {
                var wrong = ExportMatch.LargestDifference(a.Data, 256, data, 1024);
                checks.Check(wrong > ExportMatch.Tolerance, $"export: {role} at 1024 with a {fault} passed the matching-uv rule (largest difference {wrong:0.0})");
            }
        }

        // The error paths: each says what is allowed, and writes nothing.
        var odd = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = Path.Combine(driver.RunFolder, "export-odd"), size = 300 }), refused: true);
        checks.Check(odd == "export: size 300 is not allowed; size is a power of two from 32 to 8192 (32, 64, 128, 256, 512, 1024, 2048, 4096 or 8192)", "export: the odd size said " + odd);
        checks.Check(!Directory.Exists(Path.Combine(driver.RunFolder, "export-odd")), "export: a refused size created its folder");
        await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = small, size = 16384 }), refused: true);
        await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = small, outputs = new[] { "metallic" } }), refused: true);
        var taken = Path.Combine(driver.RunFolder, "taken");
        File.WriteAllText(taken, "a file");
        var blocked = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = Path.Combine(taken, "inside"), size = 32 }), refused: true);
        checks.Check(blocked.StartsWith($"export: could not write to folder {Path.Combine(taken, "inside")}: ", StringComparison.Ordinal), "export: the unwritable folder said " + blocked);
    }

    private static ImageResult Decode(string path) => ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Carve.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Carve.slnx was not found above " + AppContext.BaseDirectory);
    }
}
