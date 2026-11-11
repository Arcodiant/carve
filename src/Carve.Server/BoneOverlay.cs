using System.Numerics;
using Carve.Core;

namespace Carve.Server;

/// <summary>
/// The skeleton <c>render</c> draws (T-39), in the mesh's own coordinates and in the pose of the frame: the segments of
/// <see cref="BoneLines"/>, and every bone's name and head. Built once per call, so equal draws are one object.
/// </summary>
internal sealed record BoneDraw(IReadOnlyList<BoneSegment> Segments, IReadOnlyList<string> Names, IReadOnlyList<Vector3> Heads, bool Labels)
{
    public static BoneDraw Of(Skeleton skeleton, Pose? pose, bool labels)
    {
        var heads = Skinning.Heads(skeleton, pose ?? Pose.Rest);
        return new BoneDraw(BoneLines.Segments(skeleton, heads), [.. skeleton.Names], heads, labels);
    }
}

/// <summary>
/// Pure CPU: where the skeleton lands on an image. Engine screen primitives are depth-free and draw over the whole frame,
/// so a bone shows through the model (a line list in the scene would be depth-tested, and hidden by the very mesh it is in).
/// </summary>
internal static class BoneOverlay
{
    /// <summary>Linear RGBA of lines and joints: magenta, which no surface colour of Carve's is.</summary>
    public static readonly Vector4 Colour = new(1, 0, 1, 1);

    public const float LineWidth = 3;

    public const float JointRadius = 4;

    public static (IReadOnlyList<(Vector2 From, Vector2 To)> Lines, IReadOnlyList<Vector2> Joints) Plan(BoneDraw draw, DrawnSpace space, ViewFrame frame, int size)
    {
        Vector2 Pixel(Vector3 p) => ScreenProjection.ToPixel(space.Apply(p), frame.View, frame.Projection, size);
        return ([.. draw.Segments.Select(s => (Pixel(s.From), Pixel(s.To)))], [.. draw.Heads.Select(Pixel)]);
    }
}
