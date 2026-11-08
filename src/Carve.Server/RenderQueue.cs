using System.Collections.Concurrent;
using Empyrean;

namespace Carve.Server;

/// <summary>What one render produced: the pixels, and the counts of the mesh they show.</summary>
internal sealed record RenderedFrame(RenderTargetPixels Pixels, int Vertices, int Faces)
{
    /// <summary>The store's version of the mesh drawn, so a sheet can tell whether its tiles agree.</summary>
    public long Version { get; init; }
}

/// <summary>
/// All one render asks for. The pump compares it whole, so a change in any field, a field added here
/// included, is a change of view: applied on one <c>Update</c>, drawn on a later one. A new field takes
/// a default that means today's render.
/// </summary>
internal sealed record RenderSpec(
    CameraSpec Camera,
    Projection Projection = Projection.Perspective,
    int Size = RenderSpec.DefaultSize,
    Look Mode = Look.Shaded,
    LabelSet Labels = LabelSet.None)
{
    public const int DefaultSize = 512;

    /// <summary>What to pick out. Compared by value, so an equal highlight is not a change of view.</summary>
    public Highlight Highlight { get; init; } = Highlight.None;

    /// <summary>A sheet tile's caption, written on the image; a single render has none.</summary>
    public string? TileCaption { get; init; }

    /// <summary>The one mesh labels are written for, in a model of several; null labels the whole drawn mesh.</summary>
    public ElementWindow? LabelWindow { get; init; }
}

/// <summary>One asked-for render, waiting for the frame thread.</summary>
internal sealed class RenderRequest(RenderSpec spec, long postedVersion)
{
    private readonly TaskCompletionSource<RenderedFrame> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RenderSpec Spec => spec;

    /// <summary>The store's version at the post: the oldest mesh this request may show.</summary>
    public long PostedVersion => postedVersion;

    /// <summary>The version of the mesh the frame thread drew it on, set as it is served.</summary>
    public long ServedVersion { get; set; }

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

    public Task<RenderedFrame> Post(RenderSpec spec)
    {
        var request = new RenderRequest(spec, store.Current.Version);
        _requests.Enqueue(request);
        return request.Result;
    }

    public virtual bool TryPeek(out RenderRequest? request) => _requests.TryPeek(out request);

    public bool TryTake(out RenderRequest? request) => _requests.TryDequeue(out request);
}
