using System.Diagnostics;
using System.Text;
using System.Text.Json;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Carve.Server.Tests;

/// <summary>Where the built outputs are, found from this test assembly's own location.</summary>
internal static class Paths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string Configuration => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    public static string CarveDll { get; } = Output("src/Carve.Server", "carve.dll");

    public static string DebugExtensionDll { get; } = Output("extensions/Carve.Debug", "Carve.Debug.dll");

    public static string TinyScenarioDll { get; } = Output("tests/Carve.Scenario.Tiny", "Carve.Scenario.Tiny.dll");

    public static string TestExtensionDll { get; } =
        Output("tests/Carve.Server.Tests.Extension", "Carve.Server.Tests.Extension.dll");

    public static string Output(string project, string file) =>
        Path.Combine(RepoRoot, project, "bin", Configuration, "net10.0", file);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Carve.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Carve.slnx not found above the test output");
    }
}

/// <summary>The real <c>carve</c>, started by a test and stopped by it, speaking raw JSON-RPC lines.</summary>
internal sealed class CarveProcess : IToolCaller, IDisposable
{
    public static readonly TimeSpan Answer = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Exit = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly StringBuilder _stderr = new();
    private readonly List<string> _stdout = [];
    private readonly Task _stdoutPump;
    private readonly SemaphoreSlim _sending = new(1);
    private int _lastId = 1; // InitializeAsync takes 1.

    public CarveProcess(
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        string? workingDirectory = null)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add(Paths.CarveDll);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[key] = value;
        }

        _process = Process.Start(start)!;
        _process.ErrorDataReceived += (_, e) =>
        {
            lock (_stderr)
            {
                _stderr.AppendLine(e.Data);
            }
        };
        _process.BeginErrorReadLine();
        _stdoutPump = Task.Run(PumpStdoutAsync);
    }

    public string Stderr
    {
        get
        {
            lock (_stderr)
            {
                return _stderr.ToString();
            }
        }
    }

    public IReadOnlyList<string> StdoutLines
    {
        get
        {
            lock (_stdout)
            {
                return _stdout.ToArray();
            }
        }
    }

    public static bool IsError(JsonElement result) => result.TryGetProperty("isError", out var e) && e.GetBoolean();

    /// <summary>The text of one content block of a tool result.</summary>
    public static string TextOf(JsonElement result, int block = 0) =>
        result.GetProperty("content")[block].GetProperty("text").GetString()!;

    public async Task<JsonElement> InitializeAsync()
    {
        await SendAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"0"}}}""");
        var response = await ReadResponseAsync(1);
        await SendAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        return response;
    }

    /// <summary>The <c>tools</c> array of <c>tools/list</c>.</summary>
    public async Task<JsonElement> ListToolsAsync()
    {
        var id = Interlocked.Increment(ref _lastId);
        await SendAsync($$"""{"jsonrpc":"2.0","id":{{id}},"method":"tools/list"}""");
        var tools = (await ReadResponseAsync(id)).GetProperty("result").GetProperty("tools");
        FrameLog.Tools(tools);
        return tools;
    }

    /// <summary>The <c>result</c> of <c>tools/call</c>.</summary>
    /// <param name="name">The tool.</param>
    /// <param name="arguments">The arguments, as JSON.</param>
    public async Task<JsonElement> CallToolAsync(string name, string arguments)
    {
        var id = Interlocked.Increment(ref _lastId);
        await SendAsync($$$"""{"jsonrpc":"2.0","id":{{{id}}},"method":"tools/call","params":{"name":"{{{name}}}","arguments":{{{arguments}}}}}""");
        var result = (await ReadResponseAsync(id)).GetProperty("result");
        FrameLog.Call(name, arguments, result);
        return result;
    }

    public void CloseStdin() => _process.StandardInput.Close();

    /// <summary>The exit code, or a failure with the stderr if it has not exited in time.</summary>
    public async Task<int> WaitForExitAsync(TimeSpan timeout)
    {
        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(cancel.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"carve still running after {timeout}. stderr:\n{Stderr}");
        }

        await _stdoutPump;
        _process.WaitForExit(); // Drains the stderr reader.
        return _process.ExitCode;
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
    }

    /// <summary>One line at a time: overlapping calls would otherwise share the writer.</summary>
    private async Task SendAsync(string json)
    {
        await _sending.WaitAsync();
        try
        {
            await _process.StandardInput.WriteLineAsync(json);
            await _process.StandardInput.FlushAsync();
        }
        finally
        {
            _sending.Release();
        }
    }

    /// <summary>Waits for the response carrying this id, failing with the stderr so far.</summary>
    private async Task<JsonElement> ReadResponseAsync(int id)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < Answer)
        {
            foreach (var line in StdoutLines)
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("id", out var found) && found.GetInt32() == id)
                {
                    return document.RootElement.Clone();
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"No response to {id} after {Answer}. stderr:\n{Stderr}");
    }

    private async Task PumpStdoutAsync()
    {
        while (await _process.StandardOutput.ReadLineAsync() is { } line)
        {
            lock (_stdout)
            {
                _stdout.Add(line);
            }
        }
    }
}

/// <summary>What a test helper needs of a server: <see cref="CarveProcess"/> (the real process) and <see cref="ToolHarness"/> (in this process) both call a tool this way.</summary>
internal interface IToolCaller
{
    Task<System.Text.Json.JsonElement> CallToolAsync(string name, string arguments);
}
