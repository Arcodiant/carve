using System.Collections.Concurrent;
using Empyrean;

namespace Carve.Server;

/// <summary>What one render produced: the pixels, and the counts of the mesh they show.</summary>
internal sealed record RenderedFrame(RenderTargetPixels Pixels, int Vertices, int Faces);

/// <summary>One asked-for render, waiting for the frame thread.</summary>
internal sealed class RenderRequest(RenderView view, long postedVersion)
{
    private readonly TaskCompletionSource<RenderedFrame> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RenderView View => view;

    /// <summary>The store's version at the post: the oldest mesh this request may show.</summary>
    public long PostedVersion => postedVersion;

    public Task<RenderedFrame> Result => _result.Task;

    public void Complete(RenderedFrame frame) => _result.TrySetResult(frame);

    public void Fail(Exception exception) => _result.TrySetException(exception);
}

/// <summary>
/// From the tool to the frame thread. Needs no engine service, so a tool can resolve it with no
/// device; the frames it returns do carry engine pixels.
/// </summary>
internal class RenderQueue(ModelStore store)
{
    private readonly ConcurrentQueue<RenderRequest> _requests = new();

    public Task<RenderedFrame> Post(RenderView view)
    {
        var request = new RenderRequest(view, store.Current.Version);
        _requests.Enqueue(request);
        return request.Result;
    }

    public virtual bool TryPeek(out RenderRequest? request) => _requests.TryPeek(out request);

    public bool TryTake(out RenderRequest? request) => _requests.TryDequeue(out request);
}
