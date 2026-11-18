using System.Text.Json;
using Carve.Debug;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// T-115's stage, export end to end on the real patina: open samples/graphs/planks.texgraph, export it at its own size
/// (256) into the run folder and compare each PNG with samples/graphs/planks/*.png byte for byte, name the slots, export
/// at 1024 and check by sampling at matching uv that it is the same picture at four times the resolution, then the error
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

        // The same picture at four times the resolution: each 256 texel against the 1024 texel at its centre.
        var large = Path.Combine(driver.RunFolder, "export-1024");
        var big = await checks.CallAsync("export", JsonSerializer.Serialize(new { folder = large, size = 1024, outputs = new[] { "base_colour", "occlusion" } }));
        checks.Check(big.StartsWith($"export: wrote 2 files to {large} at size 1024\n", StringComparison.Ordinal), "export: the 1024 export said " + big);
        checks.Check(!File.Exists(Path.Combine(large, "normal.png")), "export: outputs base_colour and occlusion wrote a normal.png as well");
        foreach (var role in new[] { "base_colour", "occlusion" })
        {
            var a = Decode(Path.Combine(small, role + ".png"));
            var b = Decode(Path.Combine(large, role + ".png"));
            checks.Check(a.Width == 256 && b.Width == 1024 && b.Height == 1024, $"export: {role} sizes are {a.Width} and {b.Width}x{b.Height}, not 256 and 1024");
            if (b.Width != 1024) continue;
            double sum = 0;
            for (var y = 0; y < 256; y++)
                for (var x = 0; x < 256; x++)
                    sum += Math.Abs(a.Data[(y * 256 + x) * 4 + 1] - b.Data[((y * 4 + 2) * 1024 + x * 4 + 2) * 4 + 1]);
            checks.Check(sum / (256 * 256) < 25, $"export: {role} at 1024 is not the 256 picture at matching uv (mean difference {sum / (256 * 256):0.0})");
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
