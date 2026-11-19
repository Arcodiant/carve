using System.Collections.Concurrent;
using System.Numerics;
using Empyrean;

namespace Carve.Hosting;

/// <summary>
/// The targets one square offscreen render draws into (moved from carve's <c>RenderComposition</c> by T-111): a resolved
/// colour target that can be read back, a 4x multisampled colour target and a 4x multisampled depth target, all
/// <c>Rgba16Float</c> / <c>Depth32Float</c>, made in that order when constructed.
/// </summary>
public sealed class OffscreenTarget
{
    public OffscreenTarget(IGraphicsResourceManager resources, int size)
    {
        Size = size;
        Resolved = resources.CreateRenderTarget(size, size, TextureFormat.Rgba16Float, RenderTargetUsage.ColorAttachment | RenderTargetUsage.TransferSource);
        Multisampled = resources.CreateRenderTarget(size, size, TextureFormat.Rgba16Float, RenderTargetUsage.ColorAttachment, sampleCount: 4);
        Depth = resources.CreateRenderTarget(size, size, TextureFormat.Depth32Float, RenderTargetUsage.DepthStencilAttachment, sampleCount: 4);
    }

    internal int Size { get; }

    /// <summary>What <see cref="OffscreenFrame.ReadAsync"/> reads.</summary>
    public Texture Resolved { get; }

    internal Texture Multisampled { get; }

    internal Texture Depth { get; }

    /// <summary>
    /// Declares the three targets (named <c>&lt;prefix&gt;-resolved-&lt;size&gt;</c>, <c>-multisampled-</c>, <c>-depth-</c>),
    /// clears colour to <paramref name="background"/> and depth, draws the scene seen by the cameras tagged
    /// <paramref name="cameraTag"/> resolved into <see cref="Resolved"/>, then, when <paramref name="immediateTag"/> is
    /// given, an immediate surface of that tag over the resolved image (labels, overlays).
    /// </summary>
    public RenderPipelineBuilder Declare(RenderPipelineBuilder builder, RenderPipelineContext context, string prefix,
        Vector4 background, string cameraTag, string? immediateTag = null)
    {
        builder
            .DeclareRenderTarget(Resolved, Size, Size, out var resolved, $"{prefix}-resolved-{Size}")
            .DeclareRenderTarget(Multisampled, Size, Size, out var multisampled, $"{prefix}-multisampled-{Size}")
            .DeclareRenderTarget(Depth, Size, Size, out var depth, $"{prefix}-depth-{Size}")
            .AddClearColor(multisampled, background)
            .AddClearDepth(depth, context.DepthConvention)
            .Add(new SceneRenderStage(multisampled, depth, cameraTag: cameraTag, resolveOutput: resolved));
        if (immediateTag is not null)
        {
            builder.AddImmediate(resolved, context.CreateImmediateSurface(), immediateTag: immediateTag);
        }

        return builder;
    }
}

/// <summary>One frame of a render target, read back after the frame that draws it.</summary>
public static class OffscreenFrame
{
    /// <summary>
    /// Asks for <paramref name="target"/>'s pixels, then renders one frame; on the engine's frame thread (a system's
    /// <c>Update</c>). The task is complete by the time <c>RenderFrame</c> returns; a continuation runs on the pool.
    /// </summary>
    public static Task<RenderTargetPixels> ReadAsync(IOffscreenRenderer renderer, IRenderTargetReadback readback, Texture target)
    {
        var read = readback.ReadAsync(target);
        renderer.RenderFrame();
        return read;
    }
}

/// <summary>A render asked for by a tool and served on the frame thread: what to draw, and the frame or failure that answers it.</summary>
public class OffscreenRequest<TSpec, TFrame>(TSpec spec)
{
    private readonly TaskCompletionSource<TFrame> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TSpec Spec => spec;

    /// <summary>Completes on the pool, never inline on the thread that answers it.</summary>
    public Task<TFrame> Result => _result.Task;

    public void Complete(TFrame frame) => _result.TrySetResult(frame);

    public void Fail(Exception exception) => _result.TrySetException(exception);
}

/// <summary>
/// The requests waiting for the frame thread, first in first out: tools <see cref="Enqueue"/> from any thread, and a
/// system peeks and takes in its <c>Update</c>.
/// </summary>
public class OffscreenQueue<TRequest>
{
    private readonly ConcurrentQueue<TRequest> _requests = new();

    public void Enqueue(TRequest request) => _requests.Enqueue(request);

    public virtual bool TryPeek(out TRequest? request) => _requests.TryPeek(out request);

    public bool TryTake(out TRequest? request) => _requests.TryDequeue(out request);
}
