using System.Numerics;

namespace Carve.Core;

/// <summary>A part's envelope (D-2, M-9): its intended bounding box, in metres, in the part's own local space, covering its whole subtree.</summary>
public sealed record PartEnvelope(Vector3 Min, Vector3 Max);

/// <summary>One kept level of a part (M-9): a full snapshot of its geometry, kept under an explicit level number.</summary>
public sealed record PartLevel(int Number, PolygonMesh Mesh);

/// <summary>
/// One part of a <see cref="Model"/>'s part tree (D-2, M-9, T-91). Immutable. A part owns geometry (the model's mesh of
/// the same name), has none (an <i>empty</i>, for grouping), or is <see cref="InstanceOf"/> a part that owns geometry and
/// shows that part's geometry and levels; never both an owner and an instance. Its transform is relative to its
/// <see cref="Parent"/> (or the model, when top-level): <see cref="Scale"/>, then <see cref="Rotation"/>, then
/// <see cref="Translation"/>, so the part's origin is its pivot. A plain named mesh is the part <see cref="Of"/> its name.
/// </summary>
public sealed record Part(string Name)
{
    /// <summary>The parent part's name, or null for a top-level part.</summary>
    public string? Parent { get; init; }

    /// <summary>Where the part's origin is in its parent's space, metres.</summary>
    public Vector3 Translation { get; init; }

    /// <summary>The part's rotation in its parent's space, a unit quaternion (within <see cref="Skeleton.UnitTolerance"/> of length 1, the <c>define_skeleton</c> rule), kept as given.</summary>
    public Quaternion Rotation { get; init; } = Quaternion.Identity;

    /// <summary>The part's scale per axis, every component &gt; 0: a mirror is refused.</summary>
    public Vector3 Scale { get; init; } = Vector3.One;

    /// <summary>The part's envelope in its own local space, or null.</summary>
    public PartEnvelope? Envelope { get; init; }

    /// <summary>The part whose geometry and levels this one shows, or null.</summary>
    public string? InstanceOf { get; init; }

    /// <summary>Whether the part moves in a game, so the export keeps its own node (M-9).</summary>
    public bool Moves { get; init; }

    /// <summary>The kept levels, by ascending level number, each number once. Only a part that owns geometry has any.</summary>
    public IReadOnlyList<PartLevel> Levels { get; init; } = [];

    /// <summary>The part a plain named mesh is: top-level at the origin, identity rotation, scale 1, no envelope, no levels, static.</summary>
    public static Part Of(string name) => new(name);

    /// <summary>True when the part is as a plain named mesh's (<see cref="Of"/>): nothing but its name.</summary>
    public bool IsPlain => Parent is null && !HasTransform && Envelope is null && InstanceOf is null && !Moves && Levels.Count == 0;

    /// <summary>True when the part's own transform is not the identity: a translation, a rotation or a scale.</summary>
    public bool HasTransform => Translation != Vector3.Zero || Rotation != Quaternion.Identity || Scale != Vector3.One;

    /// <summary>The matrix from the part's space to its parent's (row vectors, as System.Numerics): scale, then the rotation normalised, then the translation.</summary>
    public Matrix4x4 LocalTransform => HasTransform
        ? Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(Rotation)) * Matrix4x4.CreateTranslation(Translation)
        : Matrix4x4.Identity;

    /// <summary>The newest kept level whose number is <paramref name="level"/> or less, or null when there is none.</summary>
    public PartLevel? LevelAt(int level) => Levels.LastOrDefault(l => l.Number <= level);

    /// <summary>The newest kept level, or null.</summary>
    public PartLevel? NewestLevel => Levels.Count == 0 ? null : Levels[^1];

    /// <summary>
    /// What is wrong with the part on its own, each by its field (<c>rotation</c>, <c>scale</c>, ...): a bad name, a
    /// non-finite translation, a rotation that is not a unit quaternion, a scale component not &gt; 0, an envelope whose min
    /// passes its max, or levels not ascending, repeated or negative. How it sits in a model is <see cref="Model.PartProblems"/>.
    /// </summary>
    public static IReadOnlyList<(string Field, string Problem)> Problems(Part part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var problems = new List<(string, string)>();
        if (Model.NameProblem(part.Name) is { } name) problems.Add(("name", name.Replace("a mesh name", "a part name")));
        var t = part.Translation;
        if (!float.IsFinite(t.X) || !float.IsFinite(t.Y) || !float.IsFinite(t.Z)) problems.Add(("translation", "must be three finite numbers"));
        var q = part.Rotation;
        var length = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        if (!float.IsFinite(length)) problems.Add(("rotation", "must be four finite numbers [x, y, z, w]"));
        else if (MathF.Abs(length - 1) > Skeleton.UnitTolerance)
            problems.Add(("rotation", FormattableString.Invariant($"not a unit quaternion: its length is {length:G6}, and it must be 1 (within {Skeleton.UnitTolerance})")));
        var s = part.Scale;
        if (!float.IsFinite(s.X) || !float.IsFinite(s.Y) || !float.IsFinite(s.Z)) problems.Add(("scale", "must be three finite numbers"));
        else if (s.X <= 0 || s.Y <= 0 || s.Z <= 0)
            problems.Add(("scale", "each component must be > 0 (a mirroring scale is refused: Empyrean's split load refuses a mirrored node)"));
        if (part.Envelope is { } e)
        {
            if (!new[] { e.Min.X, e.Min.Y, e.Min.Z, e.Max.X, e.Max.Y, e.Max.Z }.All(float.IsFinite))
                problems.Add(("envelope", "min and max must each be three finite numbers"));
            else if (Axes.FirstOrDefault(a => Component(e.Min, a) > Component(e.Max, a)) is var axis and not '\0')
                problems.Add(("envelope", $"min is above max on {axis}; min must be at or below max on every axis"));
        }
        ArgumentNullException.ThrowIfNull(part.Levels, nameof(part.Levels));
        for (var i = 0; i < part.Levels.Count; i++)
        {
            var level = part.Levels[i];
            if (level?.Mesh is null) problems.Add(($"levels[{i}]", "a level needs a mesh"));
            else if (level.Number < 0) problems.Add(($"levels[{i}]", $"level {level.Number} is negative; a level number is 0 or more"));
            else if (i > 0 && part.Levels[i - 1] is { } before && level.Number <= before.Number)
                problems.Add(($"levels[{i}]", $"level {level.Number} comes after level {before.Number}; levels are kept in ascending order, each number once"));
        }
        return problems;
    }

    internal static readonly char[] Axes = ['x', 'y', 'z'];

    internal static float Component(Vector3 v, char axis) => axis switch { 'x' => v.X, 'y' => v.Y, _ => v.Z };
}

/// <summary>
/// A part that leaves an envelope (M-9: a warning, never a refusal). With <see cref="Parent"/> null, the part's own live
/// geometry (in its local space) leaves its own envelope; otherwise the part's envelope, placed in <see cref="Parent"/>'s
/// space (the axis-aligned box of the transformed box), leaves the parent's envelope. One warning per axis and side, by the
/// furthest it goes, in metres. On the bound counts as inside (M-4's rule).
/// </summary>
public sealed record EnvelopeWarning(string Part, string? Parent, char Axis, bool Above, float By)
{
    /// <summary>The warning as a sentence.</summary>
    public string Message => Parent is null
        ? FormattableString.Invariant($"'{Part}': its geometry goes {(Above ? "above" : "below")} its envelope on {Axis} by {By:G4} m")
        : FormattableString.Invariant($"'{Part}': its envelope, placed in '{Parent}', goes {(Above ? "above" : "below")} the envelope of '{Parent}' on {Axis} by {By:G4} m");
}
