using System.Numerics;

namespace Carve.Core;

/// <summary>A bone as the caller gives it: its parent by name (null for a root), and its rest transform local to the parent.</summary>
public sealed record BoneSpec(string Name, string? Parent, Vector3 Position, Quaternion Rotation);

/// <summary>A bone of a <see cref="Skeleton"/>: <see cref="Parent"/> is an index before this bone's own, or -1 for a root.</summary>
public sealed record Bone(string Name, int Parent, Vector3 Position, Quaternion Rotation);

/// <summary>
/// The bones of a model, exactly as given (D-6, Bones as given): names and order are the caller's, parents come before
/// children, names are unique and non-empty, and a rest position and rotation are local to the parent (a root's are
/// relative to the model). No scale. Immutable.
/// </summary>
public sealed class Skeleton
{
    /// <summary>The most bones a skeleton may have.</summary>
    public const int MaxBones = 255;

    /// <summary>How far a rotation's length may be from 1 and still count as a unit quaternion.</summary>
    public const float UnitTolerance = 1e-3f;

    private readonly Vector3[] _worldPositions;

    private Skeleton(Bone[] bones)
    {
        Bones = Array.AsReadOnly(bones);
        _worldPositions = new Vector3[bones.Length];
        var worldRotations = new Quaternion[bones.Length];
        for (var i = 0; i < bones.Length; i++)
        {
            var b = bones[i];
            var local = Quaternion.Normalize(b.Rotation);
            if (b.Parent < 0)
            {
                _worldPositions[i] = b.Position;
                worldRotations[i] = local;
            }
            else
            {
                _worldPositions[i] = _worldPositions[b.Parent] + Vector3.Transform(b.Position, worldRotations[b.Parent]);
                worldRotations[i] = Quaternion.Normalize(worldRotations[b.Parent] * local);
            }
        }
    }

    /// <summary>The bones in the order given.</summary>
    public IReadOnlyList<Bone> Bones { get; }

    /// <summary>Where bone <paramref name="index"/> rests, in model space.</summary>
    public Vector3 RestWorldPosition(int index) => _worldPositions[index];

    /// <summary>The bone names in order.</summary>
    public IEnumerable<string> Names => Bones.Select(b => b.Name);

    /// <summary>True when both skeletons have the same bone names in the same order (what weights and clips depend on).</summary>
    public bool SameNamesAs(Skeleton? other) => other is not null && Names.SequenceEqual(other.Names);

    /// <summary>
    /// The skeleton of <paramref name="specs"/>, or every problem found, each naming its bone as <c>bones[i]</c>: no
    /// bones, more than <see cref="MaxBones"/>, an empty or duplicate name, a parent that is unknown or comes after its
    /// child, a non-finite position or rotation, a rotation that is not a unit quaternion.
    /// </summary>
    public static (Skeleton? Skeleton, IReadOnlyList<MeshError> Errors) Create(IReadOnlyList<BoneSpec> specs)
    {
        var errors = new List<MeshError>();
        if (specs.Count == 0) errors.Add(new MeshError("bones", "a skeleton needs at least one bone"));
        if (specs.Count > MaxBones)
            errors.Add(new MeshError("bones", $"{specs.Count} bones given; a skeleton may have at most {MaxBones}"));

        var first = new Dictionary<string, int>();
        for (var i = 0; i < specs.Count; i++)
            if (!string.IsNullOrEmpty(specs[i].Name)) first.TryAdd(specs[i].Name, i);

        var bones = new Bone[specs.Count];
        for (var i = 0; i < specs.Count; i++)
        {
            var s = specs[i];
            var at = $"bones[{i}]";
            if (string.IsNullOrEmpty(s.Name)) errors.Add(new MeshError(at, "a bone name must not be empty"));
            else if (first[s.Name] != i)
                errors.Add(new MeshError(at, $"the name '{s.Name}' is already bones[{first[s.Name]}]'s; names must be unique"));

            var parent = -1;
            if (s.Parent is not null)
            {
                if (s.Parent.Length == 0) errors.Add(new MeshError(at, "parent must not be empty; leave it out or null for a root"));
                else if (!first.TryGetValue(s.Parent, out parent))
                {
                    parent = -1;
                    errors.Add(new MeshError(at, $"unknown parent '{s.Parent}'; no bone has that name"));
                }
                else if (parent >= i)
                {
                    errors.Add(new MeshError(at, parent == i
                        ? $"parent '{s.Parent}' is the bone itself"
                        : $"parent '{s.Parent}' is bones[{parent}], after this bone; parents must come before their children"));
                    parent = -1;
                }
            }

            if (!float.IsFinite(s.Position.X + s.Position.Y + s.Position.Z))
                errors.Add(new MeshError(at, "position must be three finite numbers"));
            var q = s.Rotation;
            var length = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
            if (!float.IsFinite(length)) errors.Add(new MeshError(at, "rotation must be four finite numbers [x, y, z, w]"));
            else if (MathF.Abs(length - 1) > UnitTolerance)
                errors.Add(new MeshError(at, $"rotation is not a unit quaternion: its length is {length:G6}, and it must be 1 (within {UnitTolerance})"));
            bones[i] = new Bone(s.Name, parent, s.Position, s.Rotation);
        }

        return errors.Count > 0 ? (null, errors) : (new Skeleton(bones), errors);
    }
}
