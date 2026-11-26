using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Patina.Server.Tests;

/// <summary>Where the built outputs are, found from this test assembly's own location.</summary>
internal static class Paths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string Configuration => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    public static string PatinaDll { get; } = Output("src/Patina.Server", "patina.dll");

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

/// <summary>What a test needs of a server: the harness here, or the real process.</summary>
internal interface IToolCaller
{
    Task<JsonElement> CallToolAsync(string name, string arguments);
}

/// <summary>
/// patina's real tools in this process, as carve's <c>ToolHarness</c> (T-87) serves carve's: the host is built with
/// <c>Program.AddTools</c>, the registration patina's <c>Program.cs</c> makes, and spoken to by an MCP client over
/// in-memory pipes. There is no engine (swatch's sphere is refused), no stdio and no extension.
/// </summary>
internal sealed class PatinaHarness : IToolCaller, IAsyncDisposable
{
    private const string ProtocolVersion = "2025-06-18";

    /// <summary>The engine fault the harness reports, having no engine.</summary>
    public const string NoEngine = "the test harness has no engine";

    private readonly IHost _host;
    private readonly Pipe _toServer = new();
    private readonly Pipe _fromServer = new();
    private McpClient? _client;

    private readonly CancellationTokenSource _stop = new();
    private Task _drawer = Task.CompletedTask;

    /// <summary>The labels of the last sheet a blank engine drew (its first is the title); null before one.</summary>
    public IReadOnlyList<string>? LastLabels { get; private set; }

    private PatinaHarness(string? workingDirectory, bool blankEngine)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        var mcp = builder.Services.AddMcpServer(o => o.ServerInfo = new Implementation { Name = "patina", Version = "0" });
        mcp.WithStreamServerTransport(_toServer.Reader.AsStream(), _fromServer.Writer.AsStream());
        Program.AddTools(builder.Services, mcp, workingDirectory is null ? null : Path.GetFullPath(workingDirectory));
        // No engine here: swatch's flat and tiled views need none, and its sphere and sheet are refused with this.
        // A blank engine instead answers every draw with blank frames, so the sphere's, cube's and sheet's text is served (RV-82).
        var status = Carve.Hosting.EngineStatus.Faulted(NoEngine);
        if (blankEngine)
        {
            status = new Carve.Hosting.EngineStatus();
            // shortcut: reaches Carve.Hosting's internal SetReady by name (no public ready, no InternalsVisibleTo), so a rename
            // fails every blankEngine test with a NullReferenceException here; replace it when EngineStatus gains a public ready state.
            typeof(Carve.Hosting.EngineStatus).GetMethod("SetReady", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(status, null);
        }
        builder.Services.AddSingleton(status);
        _host = builder.Build();
    }

    private async Task DrawBlank(Carve.Hosting.OffscreenQueue<SwatchRequest> queue)
    {
        while (!_stop.IsCancellationRequested)
        {
            if (queue.TryTake(out var request) && request is not null)
            {
                LastLabels = request.Spec.Labels;
                request.Complete(new SwatchFrame(new byte[SwatchImages.SphereCell * SwatchImages.SphereCell * 4], new byte[LabelAtlas.Size * LabelAtlas.Size * 4],
                    request.Spec.Labels is { } labels ? [.. labels.Select(_ => System.Numerics.Vector2.Zero)] : null));
            }
            else await Task.Delay(5);
        }
    }

    /// <summary>A started harness; relative paths given to save and open resolve against <paramref name="workingDirectory"/>. With <paramref name="blankEngine"/>, swatch's engine views draw blank frames.</summary>
    public static async Task<PatinaHarness> StartAsync(string? workingDirectory = null, bool blankEngine = false)
    {
        var harness = new PatinaHarness(workingDirectory, blankEngine);
        await harness._host.StartAsync();
        if (blankEngine) harness._drawer = Task.Run(() => harness.DrawBlank(harness._host.Services.GetRequiredService<Carve.Hosting.OffscreenQueue<SwatchRequest>>()));
        harness._client = await McpClient.CreateAsync(
            new PipeClientTransport(harness._fromServer.Reader.AsStream(), harness._toServer.Writer.AsStream()),
            new McpClientOptions { ProtocolVersion = ProtocolVersion });
        return harness;
    }

    public GraphStore Store => _host.Services.GetRequiredService<GraphStore>();

    public async Task<JsonElement> ListToolsAsync() => (await SendAsync("tools/list", null)).GetProperty("tools");

    public Task<JsonElement> CallToolAsync(string name, string arguments) => CallToolAsync(name, arguments, CancellationToken.None);

    public Task<JsonElement> CallToolAsync(string name, string arguments, CancellationToken cancellationToken) =>
        SendAsync("tools/call", new JsonObject { ["name"] = name, ["arguments"] = JsonNode.Parse(arguments) }, cancellationToken);

    /// <summary>A tools/call sent with the id <paramref name="id"/>, so a test can cancel it by that id.</summary>
    public Task CallToolWithIdAsync(string id, string name, string arguments, CancellationToken cancellationToken) =>
        _client!.SendRequestAsync(
            new JsonRpcRequest { Id = new RequestId(id), Method = "tools/call", Params = new JsonObject { ["name"] = name, ["arguments"] = JsonNode.Parse(arguments) } },
            cancellationToken);

    /// <summary>Sends notifications/cancelled for the request with <paramref name="id"/> by hand, as the reviewer's stdio probe did.</summary>
    public Task CancelAsync(string id) =>
        _client!.SendMessageAsync(new JsonRpcNotification
        {
            Method = "notifications/cancelled",
            Params = new JsonObject { ["requestId"] = id, ["reason"] = "test" },
        });

    public async ValueTask DisposeAsync()
    {
        if (_client is not null) await _client.DisposeAsync();
        await _host.StopAsync();
        _stop.Cancel();
        await _drawer;
        _host.Dispose();
    }

    private async Task<JsonElement> SendAsync(string method, JsonNode? parameters, CancellationToken cancellationToken = default)
    {
        // Cancelling the token stops the wait only: the SDK's client (2.3.0) sends no notifications/cancelled for it
        // (RV-68; the tap showed none), so a test that cancels a call on the server uses CancelAsync.
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancel.CancelAfter(TimeSpan.FromSeconds(30));
        var response = await _client!.SendRequestAsync(new JsonRpcRequest { Method = method, Params = parameters }, cancel.Token);
        return JsonSerializer.SerializeToElement(response.Result);
    }

    private sealed class PipeClientTransport(Stream fromServer, Stream toServer) : IClientTransport
    {
        public string Name => "patina-tests";

        public Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ITransport>(new StreamServerTransport(fromServer, toServer, Name));
    }
}

/// <summary>Reading a tool result.</summary>
internal static class Results
{
    public static bool IsError(JsonElement result) => result.TryGetProperty("isError", out var e) && e.GetBoolean();

    public static string TextOf(JsonElement result) => result.GetProperty("content")[0].GetProperty("text").GetString()!;

    /// <summary>The text of a call that must succeed.</summary>
    public static async Task<string> Ok(this IToolCaller server, string tool, string arguments)
    {
        var result = await server.CallToolAsync(tool, arguments);
        Assert.False(IsError(result), $"{tool} {arguments} failed: {TextOf(result)}");
        return TextOf(result);
    }

    /// <summary>The text of a call that must be refused.</summary>
    public static async Task<string> Refused(this IToolCaller server, string tool, string arguments)
    {
        var result = await server.CallToolAsync(tool, arguments);
        Assert.True(IsError(result), $"{tool} {arguments} was not refused: {TextOf(result)}");
        return TextOf(result);
    }

    /// <summary>The open graph's file text, from get_graph (everything from the file's first line, after its head line and any parameter lines).</summary>
    public static async Task<string> GraphText(this IToolCaller server)
    {
        var text = await server.Ok("get_graph", "{}");
        return text[(text.IndexOf("\n{", StringComparison.Ordinal) + 1)..];
    }
}

/// <summary>A fresh folder under the temp folder, deleted after the test.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "patina-tests-" + Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    /// <summary><paramref name="text"/> with this folder written as &lt;folder&gt; and backslashes as slashes, as the descriptions write paths.</summary>
    public string Normalise(string text) => text.Replace(Path, "<folder>").Replace('\\', '/');

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
