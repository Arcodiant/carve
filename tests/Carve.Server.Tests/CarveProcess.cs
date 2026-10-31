using System.Diagnostics;
using System.Text;
using System.Text.Json;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Carve.Server.Tests;

/// <summary>Where the built outputs are, found from this test assembly's own location.</summary>
internal static class Paths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    private static string Configuration => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    public static string CarveDll { get; } = Output("src/Carve.Server", "carve.dll");

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
internal sealed class CarveProcess : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _stderr = new();
    private readonly List<string> _stdout = [];
    private readonly Task _stdoutPump;

    public CarveProcess(IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo("dotnet")
        {
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

    public async Task SendAsync(string json)
    {
        await _process.StandardInput.WriteLineAsync(json);
        await _process.StandardInput.FlushAsync();
    }

    /// <summary>Waits for the response carrying this id, failing with the stderr so far.</summary>
    public async Task<JsonElement> ReadResponseAsync(int id, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
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

        throw new TimeoutException($"No response to {id} after {timeout}. stderr:\n{Stderr}");
    }

    public async Task<JsonElement> InitializeAsync(TimeSpan timeout)
    {
        await SendAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"0"}}}""");
        var response = await ReadResponseAsync(1, timeout);
        await SendAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        return response;
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
