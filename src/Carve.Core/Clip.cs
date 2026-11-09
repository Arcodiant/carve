using System.Numerics;

namespace Carve.Core;

/// <summary>How a channel moves between its keys (D-6, clips): glTF's LINEAR and STEP. CUBICSPLINE is not offered.</summary>
public enum ClipInterpolation
{
    /// <summary>Between the two keys around the time: translation lerps, rotation slerps along the shortest arc.</summary>
    Linear,

    /// <summary>The earlier of the two keys around the time, until the time reaches the later one.</summary>
    Step,
}

/// <summary>One key: a time in seconds and the value at it.</summary>
public readonly record struct ClipKey<T>(float Time, T Value);

/// <summary>The keys of one part (rotation or translation) of one bone, sorted by time, and how they interpolate.</summary>
public sealed class ClipChannel<T>
{
    internal ClipChannel(ClipKey<T>[] keys, ClipInterpolation interpolation)
    {
        Keys = Array.AsReadOnly(keys);
        Interpolation = interpolation;
    }

    /// <summary>The keys, sorted by time, no two at the same time.</summary>
    public IReadOnlyList<ClipKey<T>> Keys { get; }

    public ClipInterpolation Interpolation { get; }

    /// <summary>
    /// The value at <paramref name="time"/>, as the engine's <c>AnimationInterpolation</c> samples a channel: before the
    /// first key or after the last it holds that key (no wrap); at a key it is that key; between two it is the earlier
    /// one for STEP, or <paramref name="mix"/> of the two by how far the time is between them for LINEAR.
    /// </summary>
    internal T Sample(float time, Func<T, T, float, T> mix)
    {
        var keys = Keys;
        if (time <= keys[0].Time) return keys[0].Value;
        if (time >= keys[^1].Time) return keys[^1].Value;
        var after = 1;
        while (keys[after].Time <= time) after++;
        var (from, to) = (keys[after - 1], keys[after]);
        if (Interpolation == ClipInterpolation.Step || time == from.Time) return from.Value;
        return mix(from.Value, to.Value, (time - from.Time) / (to.Time - from.Time));
    }
}

/// <summary>The animated parts of one bone; a part left null holds rest.</summary>
public sealed record BoneChannels(string Bone, ClipChannel<Quaternion>? Rotation, ClipChannel<Vector3>? Translation);

/// <summary>What a channel is given as, before it is checked: the interpolation by its glTF name and the keys in order.</summary>
public sealed record ChannelSpec<T>(string? Interpolation, IReadOnlyList<ClipKey<T>> Keys);

/// <summary>What a bone's channels are given as, before they are checked.</summary>
public sealed record BoneChannelsSpec(string Bone, ChannelSpec<Quaternion>? Rotation, ChannelSpec<Vector3>? Translation);

/// <summary>
/// A clip (D-6, clips): a name, a duration in seconds, a loop flag and, per bone, rotation and translation keys in that
/// bone's local space as absolute values (replacing the rest ones, as a <see cref="Pose"/>'s do). Bones with no keys
/// hold rest. Immutable; made only by <see cref="Create"/>, which checks it against a skeleton.
/// </summary>
public sealed class Clip
{
    private Clip(string name, float duration, bool loop, BoneChannels[] channels)
    {
        Name = name;
        Duration = duration;
        Loop = loop;
        Channels = Array.AsReadOnly(channels);
    }

    public string Name { get; }

    /// <summary>The length in seconds, greater than 0. Every key lies in [0, Duration].</summary>
    public float Duration { get; }

    /// <summary>Whether the clip is meant to loop. Sampling does not wrap: a time is clamped to [0, Duration].</summary>
    public bool Loop { get; }

    /// <summary>The animated bones, in the order given.</summary>
    public IReadOnlyList<BoneChannels> Channels { get; }

    /// <summary>
    /// The pose at <paramref name="time"/> seconds, as the engine's <c>ClipPoseProvider</c> evaluates a clip: the time
    /// is clamped to [0, <see cref="Duration"/>] (a looping clip wraps only as it is played, not when a time is set),
    /// each channel is sampled as <see cref="ClipChannel{T}.Sample"/> says, LINEAR rotation by spherical interpolation
    /// along the shortest arc (the sign of the later key is flipped when the two keys' dot product is negative), and a
    /// bone or part with no keys is left out of the pose, so it rests.
    /// </summary>
    public Pose Sample(float time)
    {
        if (float.IsNaN(time)) throw new ArgumentOutOfRangeException(nameof(time), "time is not a number");
        var t = Math.Clamp(time, 0, Duration);
        var bones = new Dictionary<string, (Quaternion?, Vector3?)>();
        foreach (var c in Channels)
            bones[c.Bone] = (c.Rotation?.Sample(t, Slerp), c.Translation?.Sample(t, Vector3.Lerp));
        return new Pose(bones);
    }

    /// <summary>
    /// Spherical interpolation along the shortest arc: q and -q are one rotation, so when the keys' dot product is
    /// negative the later key is negated, and the turn between them is never more than half a revolution.
    /// </summary>
    // Interim until engine need 5: the engine's docs give slerp along the shortest arc but not its numerics, so this is
    // System.Numerics' (negates the later key when the dot product is negative; lerps when the keys are almost equal).
    public static Quaternion Slerp(Quaternion from, Quaternion to, float amount) => Quaternion.Slerp(from, to, amount);

    /// <summary>
    /// The clip, or every problem found, each named by where it is (<c>name</c>, <c>duration</c>,
    /// <c>channels.hand.rotation.keys[2]</c>): an empty name; a duration not greater than 0 or not finite; a bone the
    /// skeleton lacks or given twice; a bone with neither part; an interpolation other than LINEAR or STEP; a channel
    /// with no keys; a key time outside [0, duration], or not after the key before it (a duplicate or unsorted time); a
    /// value that is not finite; a rotation that is not a unit quaternion within <see cref="Skeleton.UnitTolerance"/>.
    /// </summary>
    public static (Clip? Clip, IReadOnlyList<MeshError> Errors) Create(string name, float duration, bool loop,
        IReadOnlyList<BoneChannelsSpec> channels, Skeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        var errors = new List<MeshError>();
        if (string.IsNullOrEmpty(name)) errors.Add(new MeshError("name", "a clip name must not be empty"));
        var durationOk = float.IsFinite(duration) && duration > 0;
        if (!durationOk) errors.Add(new MeshError("duration", FormattableString.Invariant($"{duration} seconds: a duration must be greater than 0")));

        var seen = new HashSet<string>();
        var built = new List<BoneChannels>();
        foreach (var spec in channels)
        {
            var at = $"channels.{spec.Bone}";
            if (!skeleton.Names.Contains(spec.Bone))
            {
                errors.Add(new MeshError(at, $"no bone named '{spec.Bone}'; the bones are {NameList.Quoted(skeleton.Names)}"));
                continue;
            }
            if (!seen.Add(spec.Bone))
            {
                errors.Add(new MeshError(at, "this bone is given twice; give each bone once, with both its parts"));
                continue;
            }
            if (spec.Rotation is null && spec.Translation is null)
            {
                errors.Add(new MeshError(at, "neither rotation nor translation is given; leave the bone out to hold it at rest"));
                continue;
            }
            var rotation = Channel(spec.Rotation, $"{at}.rotation", durationOk ? duration : null, errors, q =>
            {
                var length = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
                if (!float.IsFinite(length)) return "must be four finite numbers [x, y, z, w]";
                return MathF.Abs(length - 1) > Skeleton.UnitTolerance
                    ? FormattableString.Invariant($"not a unit quaternion: its length is {length:G6}, and it must be 1 (within {Skeleton.UnitTolerance})")
                    : null;
            });
            var translation = Channel(spec.Translation, $"{at}.translation", durationOk ? duration : null, errors,
                v => float.IsFinite(v.X + v.Y + v.Z) ? null : "must be three finite numbers [x, y, z]");
            built.Add(new BoneChannels(spec.Bone, rotation, translation));
        }

        return errors.Count > 0 ? (null, errors) : (new Clip(name, duration, loop, [.. built]), errors);
    }

    private static ClipChannel<T>? Channel<T>(ChannelSpec<T>? spec, string at, float? duration, List<MeshError> errors, Func<T, string?> valueProblem)
    {
        if (spec is null) return null;
        var before = errors.Count;
        ClipInterpolation interpolation = ClipInterpolation.Linear;
        switch (spec.Interpolation)
        {
            case null or "LINEAR": break;
            case "STEP": interpolation = ClipInterpolation.Step; break;
            default:
                errors.Add(new MeshError($"{at}.interpolation", $"'{spec.Interpolation}' is not offered; use LINEAR or STEP"));
                break;
        }
        if (spec.Keys.Count == 0) errors.Add(new MeshError($"{at}.keys", "a channel needs at least one key; leave the part out to hold it at rest"));
        for (var i = 0; i < spec.Keys.Count; i++)
        {
            var (time, value) = spec.Keys[i];
            var key = $"{at}.keys[{i}]";
            if (!float.IsFinite(time)) errors.Add(new MeshError(key, "the time must be a finite number"));
            else if (duration is { } d && (time < 0 || time > d))
                errors.Add(new MeshError(key, FormattableString.Invariant($"time {time} is outside the clip, which runs from 0 to {d}")));
            if (i > 0 && float.IsFinite(time) && float.IsFinite(spec.Keys[i - 1].Time) && time <= spec.Keys[i - 1].Time)
                errors.Add(new MeshError(key, time == spec.Keys[i - 1].Time
                    ? FormattableString.Invariant($"time {time} is the same as the key before; key times must differ")
                    : FormattableString.Invariant($"time {time} comes before the key before it ({spec.Keys[i - 1].Time}); keys must be in increasing time")));
            if (valueProblem(value) is { } problem) errors.Add(new MeshError(key, $"value {problem}"));
        }
        return errors.Count > before ? null : new ClipChannel<T>([.. spec.Keys], interpolation);
    }
}
