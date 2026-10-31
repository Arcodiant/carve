namespace Carve.Server;

/// <summary>
/// Where the engine has got to. A Vulkan failure lands here and nowhere else: the process and MCP
/// stay up, and whatever needs the engine reports <see cref="FaultMessage"/>.
/// </summary>
public sealed class EngineStatus
{
    private readonly TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile string? _faultMessage;

    /// <summary>Why the engine failed, for good in this process; null unless it has.</summary>
    public string? FaultMessage => _faultMessage;

    /// <summary>
    /// Completes once the engine is ready, has faulted or has stopped, and never throws: after
    /// awaiting it, check <see cref="FaultMessage"/>. A fault can also arrive after this has
    /// completed, so check it each time.
    /// </summary>
    public Task WhenSettled => _settled.Task;

    /// <summary>Completes when the frame thread has ended, for whatever reason, and never throws.</summary>
    public Task WhenStopped => _ended.Task;

    internal void SetReady() => _settled.TrySetResult();

    internal void SetFaulted(string message)
    {
        _faultMessage = message;
        _settled.TrySetResult();
    }

    internal void SetStopped()
    {
        _settled.TrySetResult();
        _ended.TrySetResult();
    }
}
