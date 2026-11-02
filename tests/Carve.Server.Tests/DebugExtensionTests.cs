using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carve.Server.Tests;

/// <summary>The real <c>carve</c> with the debug extension and the tiny scenario stacked on it.</summary>
[Trait("Tier", "regression")]
public sealed partial class DebugExtensionTests : IDisposable
{
    private static readonly TimeSpan Run = TimeSpan.FromSeconds(60);

    private readonly string _scratch = Directory.CreateTempSubdirectory("carve-debug-tests-").FullName;

    private string Folder => Path.Combine(_scratch, "run");

    private static string[] Stacked(params string[] more) =>
    [
        $"--Carve:Extensions:0={Paths.DebugExtensionDll}",
        $"--Carve:Extensions:1={Paths.TinyScenarioDll}",
        .. more,
    ];

    private string[] InFolder(params string[] more) => Stacked([$"--Carve:Debug:RunFolder={Folder}", .. more]);

    private JsonElement Log() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, "run.json"))).RootElement;

    private static IEnumerable<JsonElement> Calls(JsonElement log) => log.GetProperty("calls").EnumerateArray();

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public async Task A_scenario_runs_unattended_and_exits_0_with_a_run_folder_and_nothing_on_stdout()
    {
        using var carve = new CarveProcess(InFolder());

        Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);

        Assert.Empty(carve.StdoutLines);
        Assert.Contains("tiny: tools", carve.Stderr);
        Assert.Contains("define_mesh", carve.Stderr);
        Assert.Contains("render", carve.Stderr);
        Assert.True(Directory.Exists(Path.Combine(Folder, "frames")));
        Assert.True(Directory.Exists(Path.Combine(Folder, "user-data")));

        var log = Log();
        Assert.Equal("tiny", log.GetProperty("scenario").GetString());
        Assert.Equal("none", log.GetProperty("audio").GetString());
        Assert.Equal("passed", log.GetProperty("outcome").GetString());
        var listed = Assert.Single(Calls(log));
        Assert.Equal("tools/list", listed.GetProperty("method").GetString());
        Assert.Contains(listed.GetProperty("result").EnumerateArray(), t => t.GetProperty("name").GetString() == "render");
    }

    [Fact]
    public async Task The_default_run_folder_is_the_next_number_under_artifacts()
    {
        var root = Path.Combine(_scratch, "artifacts", "scenarios", "tiny");
        Directory.CreateDirectory(Path.Combine(root, "7"));
        Directory.CreateDirectory(Path.Combine(root, "not-a-run"));

        for (var expected = 8; expected <= 9; expected++)
        {
            using var carve = new CarveProcess(Stacked(), workingDirectory: _scratch);
            Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);
            Assert.True(File.Exists(Path.Combine(root, expected.ToString(CultureInfo.InvariantCulture), "run.json")), carve.Stderr);
        }
    }

    [Theory]
    [InlineData("fail", "tiny failed on purpose")]
    [InlineData("swallowed-fail", "tiny failed and caught it")]
    [InlineData("throw", "tiny threw on purpose")]
    [InlineData("bytes", "tiny bytes: first differ at byte 2, 3 against 4")]
    public async Task A_failed_scenario_exits_1_with_the_failure_on_stderr_and_in_the_log(string mode, string failure)
    {
        using var carve = new CarveProcess(InFolder($"--Tiny:Mode={mode}"));

        Assert.True(1 == await carve.WaitForExitAsync(Run), carve.Stderr);

        Assert.Contains(failure, carve.Stderr);
        Assert.Empty(carve.StdoutLines);
        var log = Log();
        Assert.Equal("failed", log.GetProperty("outcome").GetString());
        Assert.Contains(failure, log.GetProperty("failure").GetString());
    }

    [Fact]
    public async Task A_protocol_error_fails_the_run_and_is_logged()
    {
        using var carve = new CarveProcess(InFolder("--Tiny:Mode=unknown-tool"));

        Assert.True(1 == await carve.WaitForExitAsync(Run), carve.Stderr);

        var call = Assert.Single(Calls(Log()), c => c.GetProperty("method").GetString() == "tools/call");
        Assert.Equal("no_such_tool", call.GetProperty("tool").GetString());
        Assert.False(string.IsNullOrEmpty(call.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task With_no_scenario_the_run_is_fatal_and_says_so()
    {
        using var carve = new CarveProcess([$"--Carve:Extensions:0={Paths.DebugExtensionDll}"], workingDirectory: _scratch);

        Assert.Equal(1, await carve.WaitForExitAsync(Run));
        Assert.Contains("exactly one scenario", carve.Stderr);
    }

    [Fact]
    public async Task The_seed_defaults_to_1_and_the_command_line_sets_it()
    {
        foreach (var (arguments, seed) in new[] { (InFolder("--Tiny:Mode=seed"), 1), (InFolder("--Tiny:Mode=seed", "--Carve:Debug:Seed=7"), 7) })
        {
            using var carve = new CarveProcess(arguments);
            Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);

            Assert.Contains($"tiny: seed {seed} first {new Random(seed).Next()}", carve.Stderr);
            Assert.Equal(seed, Log().GetProperty("seed").GetInt32());
        }
    }

    [Fact]
    public async Task User_data_is_fresh_each_run_in_a_folder_used_before()
    {
        var left = Path.Combine(Folder, "user-data", "left-by-tiny.txt");
        using (var first = new CarveProcess(InFolder("--Tiny:Mode=seed")))
        {
            Assert.True(0 == await first.WaitForExitAsync(Run), first.Stderr);
        }

        Assert.True(File.Exists(left));

        using var second = new CarveProcess(InFolder());
        Assert.True(0 == await second.WaitForExitAsync(Run), second.Stderr);
        Assert.False(File.Exists(left));
    }

    [Fact]
    public async Task The_fixed_clock_reaches_the_engine_with_pacing_off_and_no_warning()
    {
        using var carve = new CarveProcess(InFolder("--Tiny:Mode=clock"));

        Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);

        var clock = ClockLine().Match(carve.Stderr);
        Assert.True(clock.Success, carve.Stderr);
        Assert.Equal(1f / 60f, Single(clock, "option"));
        Assert.Equal(1f / 60f, Single(clock, "delta"));
        Assert.Equal("False", clock.Groups["pacing"].Value);
        Assert.DoesNotContain("warn:", carve.Stderr);
    }

    [Fact]
    public async Task The_fixed_clock_is_a_default_below_the_command_line()
    {
        using var carve = new CarveProcess(InFolder("--Tiny:Mode=clock", "--Empyrean:Time:FrameDeltaTime=0.03125"));

        Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);

        Assert.Equal(0.03125f, Single(ClockLine().Match(carve.Stderr), "delta"));
    }

    [Fact]
    public async Task A_capture_saves_the_renders_png_and_logs_it_by_hash()
    {
        using var carve = new CarveProcess(InFolder("--Tiny:Mode=capture"));

        Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);

        Assert.Contains("tiny: frame 512x512 corner (", carve.Stderr);
        Assert.Contains("caption 512x512, view front, 4 vertices, 1 faces", carve.Stderr);

        var png = File.ReadAllBytes(Path.Combine(Folder, "frames", "first.png"));
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);

        var call = Assert.Single(Calls(Log()), c => c.TryGetProperty("frame", out var f) && f.GetString() == "frames/first.png");
        Assert.Equal("front", call.GetProperty("arguments").GetProperty("view").GetString());
        var image = call.GetProperty("result").GetProperty("content")[0];
        Assert.False(image.TryGetProperty("data", out _));
        Assert.Equal(png.Length, image.GetProperty("bytes").GetInt32());
        Assert.Equal(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(png)),
            image.GetProperty("sha256").GetString());
    }

    [Fact]
    public async Task A_tool_error_is_a_result_to_CallAsync_and_a_failure_to_CaptureAsync()
    {
        using var carve = new CarveProcess(InFolder("--Tiny:Mode=tool-error"));

        Assert.True(1 == await carve.WaitForExitAsync(Run), carve.Stderr);

        Assert.Contains("tiny: tool error True", carve.Stderr);
        Assert.Contains("Capturing 'no-mesh': render returned a tool error", carve.Stderr);
        Assert.Contains("define_mesh", Log().GetProperty("failure").GetString());
    }

    [Fact]
    public async Task The_script_returns_carves_exit_code_and_the_run_folder()
    {
        var (passed, passedOut, _) = await ScriptAsync($"tiny -NoBuild -CarveArguments '--Carve:Debug:RunFolder={Folder}'");
        Assert.Equal(0, passed);
        Assert.Equal(Folder, passedOut.Trim());

        var (failed, failedOut, failedErr) = await ScriptAsync(
            $"tiny -NoBuild -CarveArguments '--Carve:Debug:RunFolder={Folder}','--Tiny:Mode=fail'");
        Assert.Equal(1, failed);
        Assert.Equal(Folder, failedOut.Trim());
        Assert.Contains("tiny failed on purpose", failedErr);
    }

    [Fact]
    public async Task A_host_stop_before_the_scenario_concludes_exits_non_zero_and_the_log_says_so()
    {
        using var carve = new CarveProcess(InFolder("--Tiny:Mode=stop-host"));

        Assert.True(1 == await carve.WaitForExitAsync(Run), carve.Stderr);

        Assert.Contains("the host stopped before the scenario concluded", carve.Stderr);
        var log = Log();
        Assert.Equal("failed", log.GetProperty("outcome").GetString());
        Assert.Contains("host stopped", log.GetProperty("failure").GetString());
    }

    [Fact]
    public async Task A_command_line_switch_beats_an_earlier_extensions_default_for_a_later_hook()
    {
        using var carve = new CarveProcess(InFolder("--Tiny:Mode=configure-pacing", "--Empyrean:Time:PaceToFixedStep=true"));

        Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);

        Assert.Contains("tiny: pacing at configure true", carve.Stderr);
    }

    [Fact]
    public async Task A_named_folder_with_files_but_no_run_json_is_refused_and_left_alone()
    {
        var kept = Path.Combine(Folder, "frames", "holiday.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        File.WriteAllText(kept, "mine");

        using var carve = new CarveProcess(InFolder());

        Assert.Equal(1, await carve.WaitForExitAsync(Run));
        Assert.Contains("no run.json", carve.Stderr);
        Assert.True(File.Exists(kept));
    }

    [Fact]
    public async Task An_empty_named_folder_without_run_json_is_used()
    {
        Directory.CreateDirectory(Path.Combine(Folder, "frames"));

        using var carve = new CarveProcess(InFolder());

        Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);
    }

    [Fact]
    public async Task The_script_fails_a_run_that_wrote_to_stdout_and_repeats_what_it_wrote()
    {
        var (code, _, stderr) = await ScriptAsync($"tiny -NoBuild -CarveArguments '--Carve:Debug:RunFolder={Folder}','--Tiny:Mode=stdout'");

        Assert.NotEqual(0, code);
        Assert.Contains("which belongs to MCP", stderr);
        Assert.Contains("tiny: this belongs to MCP", stderr);
    }

    [Fact]
    public async Task The_script_finds_the_scenario_whatever_the_case_of_its_name()
    {
        var (code, stdout, _) = await ScriptAsync($"TINY -NoBuild -CarveArguments '--Carve:Debug:RunFolder={Folder}'");

        Assert.Equal(0, code);
        Assert.Equal(Folder, stdout.Trim());
    }

    [Fact]
    public async Task The_script_refuses_a_scenario_that_does_not_exist()
    {
        var (code, _, stderr) = await ScriptAsync("no-such-scenario -NoBuild");

        Assert.Equal(2, code);
        Assert.Contains("Carve.Scenario.no-such-scenario", stderr);
    }

    [Fact]
    public async Task The_script_stops_a_run_that_outlasts_its_timeout()
    {
        var (code, _, stderr) = await ScriptAsync($"tiny -NoBuild -TimeoutSeconds 0 -CarveArguments '--Carve:Debug:RunFolder={Folder}'");

        Assert.Equal(124, code);
        Assert.Contains("stopped", stderr);
    }

    [Fact]
    public void The_debug_extension_is_on_the_public_api_alone()
    {
        var references = ProductReferenceGuard.ReferencesOf(Paths.DebugExtensionDll);

        Assert.Contains("Carve.Extensibility", references);
        Assert.DoesNotContain(references, name =>
            name.Equals("carve", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Carve.Core", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Empyrean", StringComparison.OrdinalIgnoreCase));
    }

    private static float Single(Match clock, string group) =>
        float.Parse(clock.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"tiny: clock option (?<option>\S+) delta (?<delta>\S+) pacing (?<pacing>\S+)")]
    private static partial Regex ClockLine();

    private static async Task<(int Code, string Stdout, string Stderr)> ScriptAsync(string arguments)
    {
        var script = Path.Combine(Paths.RepoRoot, "scripts", "run-scenario.ps1");
        var start = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"& '{script}' {arguments} -Configuration {Paths.Configuration}; exit $LASTEXITCODE");

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cancel = new CancellationTokenSource(Run);
        await process.WaitForExitAsync(cancel.Token);
        return (process.ExitCode, await stdout, await stderr);
    }
}
