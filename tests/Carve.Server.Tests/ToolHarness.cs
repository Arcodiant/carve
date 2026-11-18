extern alias carve;
using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using CarveServer = carve::Carve.Server.CarveServer;
using EngineStatus = Carve.Hosting.EngineStatus;

namespace Carve.Server.Tests;

/// <summary>
/// carve's real tools in this process (T-87): the host is built with <c>CarveServer.AddTools</c>, the same registration
/// <c>Program.cs</c> makes, and spoken to by an MCP client over in-memory pipes, as the debug extension does. There is
/// no engine, no transport to stdio and no extension: render (every look) refuses at once with <see cref="NoEngine"/>.
/// A test moves here from <see cref="CarveProcess"/> by its start line: <c>new ToolHarness(workingDirectory: Root)</c>
/// for <c>new CarveProcess([], workingDirectory: Root)</c>, with <see cref="InitializeAsync"/>,
/// <see cref="ListToolsAsync"/> and <see cref="CallToolAsync"/> giving the same JSON as the process's.
/// </summary>
internal sealed class ToolHarness : IToolCaller, IDisposable, IAsyncDisposable
{
    /// <summary>What render says on the harness.</summary>
    public const string NoEngine = "render: no engine in the unit harness; a render needs carve itself (a regression test on CarveProcess).";

    /// <summary>The version <see cref="CarveProcess"/> asks for; a later one adds fields (resultType) to every result.</summary>
    private const string ProtocolVersion = "2025-06-18";

    private readonly IHost _host;
    private readonly Pipe _toServer = new();
    private readonly Pipe _fromServer = new();
    private McpClient? _client;

    /// <param name="workingDirectory">What a relative path given to a tool resolves against; this process's working directory when null.</param>
    public ToolHarness(string? workingDirectory = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        var mcp = builder.Services.AddMcpServer(o => o.ServerInfo = new Implementation
        {
            Name = "carve",
            Version = typeof(CarveServer).Assembly.GetName().Version?.ToString() ?? "0",
        });
        mcp.WithStreamServerTransport(_toServer.Reader.AsStream(), _fromServer.Writer.AsStream());
        CarveServer.AddTools(builder.Services, mcp, workingDirectory is null ? null : Path.GetFullPath(workingDirectory));
        builder.Services.Replace(ServiceDescriptor.Singleton(EngineStatus.Faulted(NoEngine)));
        _host = builder.Build();
    }

    /// <summary>The model store the tools share.</summary>
    public carve::Carve.Server.ModelStore Store => _host.Services.GetRequiredService<carve::Carve.Server.ModelStore>();

    /// <summary>Starts the host and the client's session; the initialize result, as the process's <c>InitializeAsync</c> gives the response.</summary>
    public async Task<JsonElement> InitializeAsync()
    {
        await _host.StartAsync();
        _client = await McpClient.CreateAsync(
            new PipeClientTransport(_fromServer.Reader.AsStream(), _toServer.Writer.AsStream()),
            new McpClientOptions { ProtocolVersion = ProtocolVersion });
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["result"] = JsonSerializer.SerializeToNode(
                new InitializeResult
                {
                    ProtocolVersion = _client.NegotiatedProtocolVersion ?? "",
                    Capabilities = _client.ServerCapabilities!,
                    ServerInfo = _client.ServerInfo!,
                    Instructions = _client.ServerInstructions,
                },
                McpJsonUtilities.DefaultOptions),
        });
    }

    /// <summary>The <c>tools</c> array of <c>tools/list</c>.</summary>
    public async Task<JsonElement> ListToolsAsync() => (await SendAsync("tools/list", null)).GetProperty("tools");

    /// <summary>The <c>result</c> of <c>tools/call</c>.</summary>
    /// <param name="name">The tool.</param>
    /// <param name="arguments">The arguments, as JSON.</param>
    public Task<JsonElement> CallToolAsync(string name, string arguments) =>
        SendAsync("tools/call", new JsonObject { ["name"] = name, ["arguments"] = JsonNode.Parse(arguments) });

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>One raw request, so the result is the JSON the server sent, as the process's tests read it.</summary>
    private async Task<JsonElement> SendAsync(string method, JsonNode? parameters)
    {
        var client = _client ?? throw new InvalidOperationException("InitializeAsync first, as on CarveProcess");
        using var cancel = new CancellationTokenSource(CarveProcess.Answer);
        var response = await client.SendRequestAsync(new JsonRpcRequest { Method = method, Params = parameters }, cancel.Token);
        return JsonSerializer.SerializeToElement(response.Result);
    }

    /// <summary>The client's end of the pipes; the SDK's stream transport says nothing of which end it is on (as in Carve.Debug).</summary>
    private sealed class PipeClientTransport(Stream fromServer, Stream toServer) : IClientTransport
    {
        public string Name => "carve-tests";

        public Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ITransport>(new StreamServerTransport(fromServer, toServer, Name));
    }
}
