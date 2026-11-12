using System.Globalization;
using System.Numerics;

namespace Carve.Core;

/// <summary>
/// Warnings on a clip (D-6, clips): a looping clip whose channels end differently from how they start, and a clip whose
/// root bones end elsewhere on the ground plane. Warnings only; nothing is refused.
/// </summary>
public static class ClipChecks
{
    /// <summary>
    /// How far a clip's end may be from its start and still count as closed, and how far a root may end from where it
    /// began and still count as in place: rotation within 1 degree (q and -q are the same rotation), translation within
    /// 1 mm. A looping translation closes on X, Y and Z (the person's ruling, 2026-11-12); a root's travel is X/Z only, on the
    /// ground plane. The person's ruling, 2026-11-11, recorded in D-6's Clips section.
    /// </summary>
    public const float RotationToleranceDegrees = 1f;

    /// <summary>The translation tolerance in metres: 1 mm: the distance between first and last key in 3D for loop closure, on X and Z only for root travel.</summary>
    public const float TranslationToleranceMetres = 0.001f;

    /// <summary>The warnings for <paramref name="clip"/> on <paramref name="skeleton"/>, in channel order; empty for a clean clip.</summary>
    public static IReadOnlyList<string> Check(Clip clip, Skeleton skeleton)
    {
        var warnings = new List<string>();
        foreach (var c in clip.Channels)
        {
            var isRoot = skeleton.Bones.First(b => b.Name == c.Bone).Parent < 0;
            if (c.Rotation is { } r)
            {
                var degrees = AngleDegrees(r.Keys[0].Value, r.Keys[^1].Value);
                if (clip.Loop && degrees > RotationToleranceDegrees)
                    warnings.Add(Inv($"clip '{clip.Name}' loops but bone '{c.Bone}' rotation ends {degrees:0.##} degrees from where it starts (more than {RotationToleranceDegrees:0.##}); the loop pops at its seam"));
            }
            if (c.Translation is { } t)
            {
                var (a, b) = (t.Keys[0].Value, t.Keys[^1].Value);
                var ground = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                var full = (b - a).Length();
                if (clip.Loop && full > TranslationToleranceMetres)
                    warnings.Add(Inv($"clip '{clip.Name}' loops but bone '{c.Bone}' translation ends {full * 1000:0.#} mm from where it starts on X/Y/Z (more than {TranslationToleranceMetres * 1000:0.#} mm); the loop pops at its seam"));
                if (isRoot && ground > TranslationToleranceMetres)
                    warnings.Add(Inv($"clip '{clip.Name}' moves root bone '{c.Bone}' {ground * 1000:0.#} mm along the ground (X/Z) between its start and end (more than {TranslationToleranceMetres * 1000:0.#} mm); clips are in place"));
            }
        }
        return warnings;
    }

    /// <summary>The warnings for every clip of <paramref name="model"/>, in clip order; empty with no skeleton or no clips.</summary>
    public static IReadOnlyList<string> Check(Model model) =>
        model.Skeleton is { } skeleton ? model.Clips.SelectMany(c => Check(c, skeleton)).ToList() : [];

    private static float AngleDegrees(Quaternion a, Quaternion b)
    {
        var dot = Math.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b)));
        return (float)(2 * Math.Acos(Math.Min(1.0, dot)) * 180 / Math.PI);
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
