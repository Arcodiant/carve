namespace Carve.Server;

/// <summary>
/// Where the engine has got to. A Vulkan failure lands here and nowhere else: the process and MCP
/// stay up, and whatever needs the engine reports <see cref="FaultMessage"/>.
/// </summary>
public sealed class EngineStatus
{
    private readonly TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _initialised;
    private volatile bool _stopped;
    private volatile string? _faultMessage;

    /// <summary>The engine is running and can take work. False before start and after a stop or fault.</summary>
    public bool IsReady => _initialised && !_stopped && _faultMessage is null;

    /// <summary>The engine failed, and will not come back in this process.</summary>
    public bool IsFaulted => _faultMessage is not null;

    /// <summary>Why it failed; null unless <see cref="IsFaulted"/>.</summary>
    public string? FaultMessage => _faultMessage;

    /// <summary>
    /// Completes once the engine is ready, has faulted or has stopped, and never throws: after
    /// awaiting it, check <see cref="IsReady"/> and <see cref="FaultMessage"/>. A fault can also
    /// arrive after this has completed, so check them each time.
    /// </summary>
    public Task WhenSettled => _settled.Task;

    /// <summary>Completes when the frame thread has ended, for whatever reason, and never throws.</summary>
    public Task WhenStopped => _ended.Task;

    internal void SetReady()
    {
        _initialised = true;
        _settled.TrySetResult();
    }

    internal void SetFaulted(string message)
    {
        _faultMessage = message;
        _settled.TrySetResult();
    }

    internal void SetStopped()
    {
        _stopped = true;
        _settled.TrySetResult();
        _ended.TrySetResult();
    }
}
