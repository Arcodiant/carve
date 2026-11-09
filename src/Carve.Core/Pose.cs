using System.Numerics;

namespace Carve.Core;

/// <summary>
/// A pose: per bone, a local rotation and a local translation (absolute, as a clip's keys are, replacing the rest ones).
/// A bone, or a part of one, not given rests. Immutable.
/// </summary>
public sealed class Pose
{
    /// <summary>The rest pose.</summary>
    public static Pose Rest { get; } = new(new Dictionary<string, (Quaternion?, Vector3?)>());

    private readonly Dictionary<string, (Quaternion? Rotation, Vector3? Translation)> _bones;

    /// <summary>A pose of <paramref name="bones"/> by bone name.</summary>
    public Pose(IReadOnlyDictionary<string, (Quaternion? Rotation, Vector3? Translation)> bones) =>
        _bones = new(bones);

    /// <summary>Equal when the same bones are set to the same values.</summary>
    public override bool Equals(object? obj) =>
        obj is Pose other && _bones.Count == other._bones.Count
        && _bones.All(b => other._bones.TryGetValue(b.Key, out var o) && o.Equals(b.Value));

    public override int GetHashCode() => _bones.Count;

    /// <summary>The names of the bones this pose sets.</summary>
    public IEnumerable<string> Names => _bones.Keys;

    /// <summary>The given rotation of <paramref name="bone"/>, or null to rest.</summary>
    public Quaternion? RotationOf(string bone) => _bones.TryGetValue(bone, out var b) ? b.Rotation : null;

    /// <summary>The given translation of <paramref name="bone"/>, or null to rest.</summary>
    public Vector3? TranslationOf(string bone) => _bones.TryGetValue(bone, out var b) ? b.Translation : null;
}
