using System.Numerics;
using Empyrean;

namespace Carve.Server;

/// <summary>
/// The offscreen pipeline: one tagged camera per size, each drawn at 4x MSAA and resolved into a
/// single-sampled target that can be read back. Every stage draws every frame, and a render reads back
/// the target of its size. Linear values; <c>ToDecodedImage</c> does the sRGB encoding.
/// </summary>
internal sealed class RenderComposition(IGraphicsResourceManager resources) : IRenderPipelineConfigurator
{
    /// <summary>The sizes <c>render</c> takes, each with its own targets, stage and camera.</summary>
    public static readonly IReadOnlyList<int> Sizes = [256, RenderSpec.DefaultSize, 1024];

    private static readonly Vector4 Background = new(0.03f, 0.035f, 0.04f, 1f);

    private readonly Dictionary<int, TargetSet> _targets = [];

    /// <summary>The tag joining the camera of a size to its stage.</summary>
    public static string CameraTag(int size) => $"render-{size}";

    /// <summary>The resolved target of a size, the one to read back.</summary>
    public Texture Target(int size) => Targets(size).Resolved;

    public void Compose(RenderPipelineBuilder builder, RenderPipelineContext context)
    {
        foreach (var size in Sizes)
        {
            var targets = Targets(size);
            builder
                .DeclareRenderTarget(targets.Resolved, size, size, out var resolved, $"carve-resolved-{size}")
                .DeclareRenderTarget(targets.Multisampled, size, size, out var multisampled, $"carve-multisampled-{size}")
                .DeclareRenderTarget(targets.Depth, size, size, out var depth, $"carve-depth-{size}")
                .AddClearColor(multisampled, Background)
                .AddClearDepth(depth, context.DepthConvention)
                .Add(new SceneRenderStage(multisampled, depth, cameraTag: CameraTag(size), resolveOutput: resolved));
        }
    }

    private TargetSet Targets(int size)
    {
        if (!_targets.TryGetValue(size, out var targets))
        {
            targets = new TargetSet(
                resources.CreateRenderTarget(size, size, TextureFormat.Rgba16Float, RenderTargetUsage.ColorAttachment | RenderTargetUsage.TransferSource),
                resources.CreateRenderTarget(size, size, TextureFormat.Rgba16Float, RenderTargetUsage.ColorAttachment, sampleCount: 4),
                resources.CreateRenderTarget(size, size, TextureFormat.Depth32Float, RenderTargetUsage.DepthStencilAttachment, sampleCount: 4));
            _targets.Add(size, targets);
        }

        return targets;
    }

    private sealed record TargetSet(Texture Resolved, Texture Multisampled, Texture Depth);
}
