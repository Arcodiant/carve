using System.Numerics;
using Empyrean;

namespace Carve.Server;

/// <summary>
/// The offscreen pipeline: one tagged camera per size, each drawn at 4x MSAA and resolved into a
/// single-sampled target that can be read back, then an immediate surface drawn over that target. Every
/// stage draws every frame, and a render reads back the target of its size. Linear values;
/// <c>ToDecodedImage</c> does the sRGB encoding.
/// </summary>
internal sealed class RenderComposition(IGraphicsResourceManager resources) : IRenderPipelineConfigurator
{
    /// <summary>The sizes <c>render</c> takes, each with its own targets, stage and camera.</summary>
    public static readonly IReadOnlyList<int> Sizes = [256, RenderSpec.DefaultSize, 1024];

    public static readonly Vector4 Background = new(0.03f, 0.035f, 0.04f, 1f);

    private readonly Dictionary<int, OffscreenTarget> _targets = [];

    /// <summary>The tag joining the camera of a size to its stage.</summary>
    public static string CameraTag(int size) => $"render-{size}";

    /// <summary>The tag of the immediate surface over a size's resolved target, where labels and captions are drawn.</summary>
    public static string LabelTag(int size) => $"labels-{size}";

    /// <summary>The resolved target of a size, the one to read back.</summary>
    public Texture Target(int size) => Targets(size).Resolved;

    public void Compose(RenderPipelineBuilder builder, RenderPipelineContext context)
    {
        foreach (var size in Sizes)
        {
            Targets(size).Declare(builder, context, "carve", Background, CameraTag(size), LabelTag(size));
        }
    }

    private OffscreenTarget Targets(int size)
    {
        if (!_targets.TryGetValue(size, out var targets))
        {
            targets = new OffscreenTarget(resources, size);
            _targets.Add(size, targets);
        }

        return targets;
    }
}
