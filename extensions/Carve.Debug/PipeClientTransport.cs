using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Carve.Debug;

/// <summary>
/// The client's end of the in-memory pipe. The SDK has no public client transport over streams,
/// but its stream transport is plain JSON-RPC lines both ways and says nothing of which end it is
/// on, so the client uses one too.
/// </summary>
internal sealed class PipeClientTransport(Stream fromServer, Stream toServer) : IClientTransport
{
    public string Name => "carve-debug";

    public Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ITransport>(new StreamServerTransport(fromServer, toServer, Name));
}
