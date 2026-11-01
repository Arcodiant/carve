using System.Numerics;
using Empyrean;

namespace Carve.Server;

/// <summary>
/// The offscreen pipeline: one tagged camera drawn at 4x MSAA and resolved into a single-sampled
/// target that can be read back. Linear values; <c>ToDecodedImage</c> does the sRGB encoding.
/// </summary>
internal sealed class RenderComposition(IGraphicsResourceManager resources) : IRenderPipelineConfigurator
{
    public const int Size = 512;
    public const string CameraTag = "render";

    private static readonly Vector4 Background = new(0.03f, 0.035f, 0.04f, 1f);

    private Texture? _resolved;
    private Texture? _multisampled;
    private Texture? _depth;

    public Texture Target => _resolved ??= resources.CreateRenderTarget(
        Size, Size, TextureFormat.Rgba16Float, RenderTargetUsage.ColorAttachment | RenderTargetUsage.TransferSource);

    public void Compose(RenderPipelineBuilder builder, RenderPipelineContext context)
    {
        _multisampled ??= resources.CreateRenderTarget(
            Size, Size, TextureFormat.Rgba16Float, RenderTargetUsage.ColorAttachment, sampleCount: 4);
        _depth ??= resources.CreateRenderTarget(
            Size, Size, TextureFormat.Depth32Float, RenderTargetUsage.DepthStencilAttachment, sampleCount: 4);

        builder
            .DeclareRenderTarget(Target, Size, Size, out var resolved, "carve-resolved")
            .DeclareRenderTarget(_multisampled, Size, Size, out var multisampled, "carve-multisampled")
            .DeclareRenderTarget(_depth, Size, Size, out var depth, "carve-depth")
            .AddClearColor(multisampled, Background)
            .AddClearDepth(depth, context.DepthConvention)
            .Add(new SceneRenderStage(multisampled, depth, cameraTag: CameraTag, resolveOutput: resolved));
    }
}
