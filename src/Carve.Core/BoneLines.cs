using System.Numerics;

namespace Carve.Core;

/// <summary>One line of the skeleton drawn by <c>render</c>: from <see cref="From"/> to <see cref="To"/>, for bone <see cref="Bone"/>.</summary>
/// <param name="Leaf">True for a leaf bone's own segment, which no child's head ends.</param>
public readonly record struct BoneSegment(int Bone, Vector3 From, Vector3 To, bool Leaf);

/// <summary>
/// The lines that show a skeleton (T-39), from where its bones' heads are. A bone with children is a line from its head to
/// each child's head. A leaf is a line from its head along its parent's direction, for half the parent's length (the leaf
/// segment of automatic weights, T-37, D-6). A bone with no parent and no child has no direction and no line, only its head.
/// </summary>
public static class BoneLines
{
    /// <summary>Segments in bone order; <paramref name="heads"/> are model-space heads by bone index (<see cref="Skinning.Heads"/>).</summary>
    public static IReadOnlyList<BoneSegment> Segments(Skeleton skeleton, IReadOnlyList<Vector3> heads)
    {
        var segments = new List<BoneSegment>();
        var hasChild = new bool[skeleton.Bones.Count];
        foreach (var b in skeleton.Bones.Where(b => b.Parent >= 0)) hasChild[b.Parent] = true;
        for (var i = 0; i < skeleton.Bones.Count; i++)
        {
            if (hasChild[i])
            {
                for (var c = 0; c < skeleton.Bones.Count; c++)
                    if (skeleton.Bones[c].Parent == i) segments.Add(new BoneSegment(i, heads[i], heads[c], false));
            }
            else if (skeleton.Bones[i].Parent is var p and >= 0)
            {
                var along = heads[i] - heads[p];
                if (along.Length() > 1e-6f) segments.Add(new BoneSegment(i, heads[i], heads[i] + along / 2, true));
            }
        }
        return segments;
    }
}
