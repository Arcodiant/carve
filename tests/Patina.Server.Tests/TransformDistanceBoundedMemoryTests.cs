using System.Diagnostics;
using System.Text.Json;
using Carve.Server.Tests;

namespace Patina.Server.Tests;

/// <summary>
/// T-138 and T-155: the real patina exports, at 8192, a colour fbm turned 30 degrees by transform, a tiles mask measured
/// by distance at range 0.1, the colour fbm blurred smooth at radius 0.05, the colour fbm tiled 16 x 16 at scale
/// 0.25 (a strong minify), and (T-173) the tiles mask minified the same, each in a process whose peak working set stays at 2 GB or less
/// (<see cref="ExportBoundedMemoryTests.BoundBytes"/>). The time and the peak are written to <c>patina-t138-*.txt</c> in
/// the temp folder and to the test's output; the time is a figure, not a pass mark: the person ruled (2026-11-20) that
/// this suite, a Debug patina, asserts the peak only, and the 10 s checks are measured in Release (T-155's As built).
/// </summary>
[Trait("Tier", "regression")]
public class TransformDistanceBoundedMemoryTests
{
    /// <summary>A colour fbm (8 x 8 cells, 4 octaves, toned by a colour) turned 30 degrees.</summary>
    public const string Turned = """{"version":1,"size":256,"seed":7,"nodes":{"f":{"type":"fbm","params":{"cells":[8,8],"octaves":4}},"tint":{"type":"colour","params":{"colour":"#c08040"}},"c":{"type":"blend","inputs":{"a":"tint","b":"f"},"params":{"mode":"multiply"}},"turned":{"type":"transform","inputs":{"in":"c"},"params":{"rotation":30}},"out":{"type":"output","inputs":{"in":"turned"},"params":{"role":"base_colour"}}}}""";

    /// <summary>The same colour fbm blurred smooth at radius 0.05, blur's largest (T-155).</summary>
    public const string Blurred = """{"version":1,"size":256,"seed":7,"nodes":{"f":{"type":"fbm","params":{"cells":[8,8],"octaves":4}},"tint":{"type":"colour","params":{"colour":"#c08040"}},"c":{"type":"blend","inputs":{"a":"tint","b":"f"},"params":{"mode":"multiply"}},"b":{"type":"blur","inputs":{"in":"c"},"params":{"radius":0.05,"quality":"smooth"}},"out":{"type":"output","inputs":{"in":"b"},"params":{"role":"base_colour"}}}}""";

    /// <summary>The same colour fbm tiled 16 x 16 at scale 0.25: each output tile reads its whole input (T-155).</summary>
    public const string Minified = """{"version":1,"size":256,"seed":7,"nodes":{"f":{"type":"fbm","params":{"cells":[8,8],"octaves":4}},"tint":{"type":"colour","params":{"colour":"#c08040"}},"c":{"type":"blend","inputs":{"a":"tint","b":"f"},"params":{"mode":"multiply"}},"m":{"type":"transform","inputs":{"in":"c"},"params":{"tile":[16,16],"scale":0.25}},"out":{"type":"output","inputs":{"in":"m"},"params":{"role":"base_colour"}}}}""";

    /// <summary>
    /// The floor's 4 x 4 tiles' mask tiled 16 x 16 at scale 0.25: a strong minify of one port of a node of four, six
    /// floats a texel (RV-76: 1.5 GiB pinned at 8192 when a read pinned every port; T-173 pins the mask alone).
    /// </summary>
    public const string MinifiedMask = """{"version":1,"size":256,"seed":7,"nodes":{"t":{"type":"tiles","params":{"count":[4,4],"gap":0.06,"bevel":0}},"m":{"type":"transform","inputs":{"in":"t.mask"},"params":{"tile":[16,16],"scale":0.25}},"out":{"type":"output","inputs":{"in":"m"},"params":{"role":"height"}}}}""";

    /// <summary>The floor's 4 x 4 tiles' mask, measured inside at range 0.1.</summary>
    public const string Measured = """{"version":1,"size":256,"seed":7,"nodes":{"t":{"type":"tiles","params":{"count":[4,4],"gap":0.06,"bevel":0}},"d":{"type":"distance","inputs":{"in":"t.mask"},"params":{"range":0.1}},"out":{"type":"output","inputs":{"in":"d"},"params":{"role":"height"}}}}""";

    /// <summary>
    /// How long an export may take, in a Debug patina, before the test gives up: about four times what each took in
    /// Debug on the T-155 machine (turn 129 s, distance 147 s, minify 114 s; blur 640 s).
    /// </summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(10);

    /// <summary>The blur's deadline: it took 640 s in Debug, so about three times that.</summary>
    private static readonly TimeSpan BlurDeadline = TimeSpan.FromMinutes(30);

    [Fact]
    public Task A_30_degree_turn_of_a_colour_fbm_at_8192_peaks_at_2_GB_or_less() => ExportAsync("turn", Turned, "base_colour", 8192, Deadline);

    [Fact]
    public Task Distance_at_range_0_1_at_8192_peaks_at_2_GB_or_less() => ExportAsync("distance", Measured, "height", 8192, Deadline);

    [Fact]
    public Task A_smooth_blur_at_radius_0_05_at_8192_peaks_at_2_GB_or_less() => ExportAsync("blur", Blurred, "base_colour", 8192, BlurDeadline);

    [Fact]
    public Task A_strong_minify_at_8192_peaks_at_2_GB_or_less() => ExportAsync("minify", Minified, "base_colour", 8192, Deadline);

    [Fact]
    public Task A_strong_minify_of_the_tiles_mask_at_8192_peaks_at_2_GB_or_less() => ExportAsync("minify-mask", MinifiedMask, "height", 8192, Deadline);

    private static async Task ExportAsync(string label, string graph, string role, int size, TimeSpan deadline)
    {
            using var folder = new TempFolder();
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = folder.Path,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add(Paths.PatinaDll);
            using var process = Process.Start(start)!;
            var stderr = new System.Text.StringBuilder();
            process.ErrorDataReceived += (_, e) => { lock (stderr) stderr.AppendLine(e.Data); };
            process.BeginErrorReadLine();
            try
            {
                async Task<JsonElement> Send(int? id, string method, string parameters, TimeSpan? within = null)
                {
                    var head = id is { } n ? $"\"id\":{n}," : "";
                    await process.StandardInput.WriteLineAsync($"{{\"jsonrpc\":\"2.0\",{head}\"method\":\"{method}\",\"params\":{parameters}}}");
                    await process.StandardInput.FlushAsync();
                    if (id is null) return default;
                    using var cancel = new CancellationTokenSource(within ?? TimeSpan.FromSeconds(60));
                    while (true)
                    {
                        var line = await process.StandardOutput.ReadLineAsync(cancel.Token)
                            ?? throw new InvalidOperationException("patina closed stdout. stderr:\n" + stderr);
                        using var document = JsonDocument.Parse(line);
                        if (document.RootElement.TryGetProperty("id", out var got) && got.GetInt32() == id) return document.RootElement.GetProperty("result").Clone();
                    }
                }

                await Send(1, "initialize", """{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"0"}}""");
                await Send(null, "notifications/initialized", "{}");
                var defined = await Send(2, "tools/call", JsonSerializer.Serialize(new { name = "define_graph", arguments = new { graph = JsonDocument.Parse(graph).RootElement } }));
                var said = defined.GetProperty("content")[0].GetProperty("text").GetString()!;
                Assert.True(said.EndsWith("no faults: it evaluates", StringComparison.Ordinal), said);

            var clock = Stopwatch.StartNew();
            var result = await Send(3, "tools/call", JsonSerializer.Serialize(new { name = "export", arguments = new { folder = "big", size, outputs = new[] { role } } }), deadline);
            clock.Stop();
            process.Refresh();
            var peak = process.PeakWorkingSet64;
            var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.False(result.TryGetProperty("isError", out var failed) && failed.GetBoolean(), text);

            var summary = $"export of the {label} graph at {size}, {role}: {clock.Elapsed.TotalSeconds:0.0} s, peak working set {peak / 1048576.0:0} MB (bound {ExportBoundedMemoryTests.BoundBytes / 1048576} MB)";
            File.WriteAllText(Path.Combine(Path.GetTempPath(), $"patina-t138-{size}-{label}.txt"), summary + Environment.NewLine + text + Environment.NewLine);
            TestContext.Current.SendDiagnosticMessage(summary);
            Assert.Contains($"- {role}.png: {size}x{size}, ", text);
            Assert.True(peak <= ExportBoundedMemoryTests.BoundBytes, summary);

            process.StandardInput.Close();
            using var exit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(exit.Token);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
