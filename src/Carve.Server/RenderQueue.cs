using System.Collections.Concurrent;
using Empyrean;

namespace Carve.Server;

/// <summary>What one render produced: the pixels, and the counts of the mesh they show.</summary>
internal sealed record RenderedFrame(RenderTargetPixels Pixels, int Vertices, int Faces);

/// <summary>One asked-for render, waiting for the frame thread.</summary>
internal sealed class RenderRequest(RenderView view)
{
    private readonly TaskCompletionSource<RenderedFrame> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RenderView View => view;

    public Task<RenderedFrame> Result => _result.Task;

    public void Complete(RenderedFrame frame) => _result.TrySetResult(frame);

    public void Fail(Exception exception) => _result.TrySetException(exception);
}

/// <summary>
/// From the tool to the frame thread. Holds nothing of the engine, so a tool can reach it on a
/// machine where the engine's services cannot be built.
/// </summary>
internal sealed class RenderQueue
{
    private readonly ConcurrentQueue<RenderRequest> _requests = new();

    public Task<RenderedFrame> Post(RenderView view)
    {
        var request = new RenderRequest(view);
        _requests.Enqueue(request);
        return request.Result;
    }

    public bool TryPeek(out RenderRequest? request) => _requests.TryPeek(out request);

    public bool TryTake(out RenderRequest? request) => _requests.TryDequeue(out request);
}
