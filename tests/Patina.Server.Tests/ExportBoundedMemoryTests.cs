using System.Diagnostics;
using System.Text.Json;
using Carve.Server.Tests;

namespace Patina.Server.Tests;

/// <summary>
/// T-115: the real patina exports the planks at 8192, all four outputs, and the process's peak working set stays at 2 GB
/// or less (outputs are encoded one at a time, and the evaluation holds tiles under a capped cache, never a node's full image). The time and
/// the peak are written to <c>patina-export-8192.txt</c> in the temp folder and to the test's output.
/// </summary>
[Trait("Tier", "regression")]
public class ExportBoundedMemoryTests
{
    public const long BoundBytes = 2L * 1024 * 1024 * 1024;

    [Fact]
    public async Task Export_of_the_planks_at_8192_writes_four_pngs_in_a_process_that_peaks_at_2_GB_or_less()
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
            var planks = Path.Combine(Paths.RepoRoot, "samples", "graphs", "planks.texgraph");
            var opened = await Send(2, "tools/call", JsonSerializer.Serialize(new { name = "open", arguments = new { path = planks } }));
            Assert.False(opened.TryGetProperty("isError", out var openFailed) && openFailed.GetBoolean(), opened.ToString());

            var clock = Stopwatch.StartNew();
            var result = await Send(3, "tools/call", """{"name":"export","arguments":{"folder":"big","size":8192}}""", TimeSpan.FromMinutes(10));
            clock.Stop();
            process.Refresh();
            var peak = process.PeakWorkingSet64;
            var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.False(result.TryGetProperty("isError", out var failed) && failed.GetBoolean(), text);

            var summary = $"export of the planks at 8192, four outputs: {clock.Elapsed.TotalSeconds:0.0} s, peak working set {peak / 1048576.0:0} MB (bound {BoundBytes / 1048576} MB)";
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "patina-export-8192.txt"), summary + "\n" + text + "\n");
            TestContext.Current.SendDiagnosticMessage(summary);

            foreach (var role in new[] { "base_colour", "normal", "roughness", "occlusion" })
            {
                var png = Path.Combine(folder.Path, "big", role + ".png");
                var head = new byte[24];
                using (var stream = File.OpenRead(png)) Assert.Equal(24, stream.Read(head));
                Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, head[..4]);
                Assert.Equal(8192, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(16)));
                Assert.Equal(8192, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(20)));
                Assert.Contains($"- {role}.png: 8192x8192, ", text);
            }
            Assert.True(peak <= BoundBytes, summary);

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
