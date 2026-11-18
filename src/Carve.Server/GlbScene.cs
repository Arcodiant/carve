using System.Numerics;

using Empyrean;

namespace Carve.Server;

/// <summary>
/// A <c>.glb</c> as <c>render_glb</c> draws it: the file's bytes, which the frame thread loads through the engine's model
/// loader, the bone palette the file's own clip gives at the time asked (with no clip, a skinned file's bind pose: every bone
/// at its node's rest transform composed with the file's inverse binds, as a game skins it with no clip playing; null only for
/// a file with no skin, drawn as it lies), and the
/// framing of its rest pose (D-6: the rest bounds, as <c>render</c> frames a posed model, T-32). Nothing here shares
/// Carve's preview skinning (D-6, two samplers): the pose is the engine's <see cref="ClipPoseProvider"/> and
/// <see cref="SkinningPalette"/>, drawn by the engine's GPU skinning through a <see cref="SkinnedPose"/>.
/// Compared by reference in a <see cref="RenderSpec"/>: one per call.
/// </summary>
internal sealed class GlbScene
{
    private GlbScene(byte[] bytes, Matrix4x4[]? palette, DrawnSpace space, Vector3 drawnHalf, int vertices, int triangles, IReadOnlyList<string> warnings, IReadOnlyList<KeyValuePair<string, float>> targetWeights)
    {
        TargetWeights = targetWeights;
        Bytes = bytes;
        Palette = palette;
        Space = space;
        DrawnHalf = drawnHalf;
        Vertices = vertices;
        Triangles = triangles;
        Warnings = warnings;
    }

    public byte[] Bytes { get; }

    /// <summary>The target weights asked for, name to weight, in the order given; each name is resolved once by the frame thread with <c>MorphTargetSet.IndexOfTarget</c>. Empty with no <c>targets</c>.</summary>
    public IReadOnlyList<KeyValuePair<string, float>> TargetWeights { get; }

    public Matrix4x4[]? Palette { get; }

    /// <summary>The rest bounds' drawn space: the same <see cref="DrawnSpace.Of"/> render's framing takes.</summary>
    public DrawnSpace Space { get; }

    public Vector3 DrawnHalf { get; }

    public Vector3 BoundsMin { get; private init; }

    public Vector3 BoundsMax { get; private init; }

    public int Vertices { get; }

    public int Triangles { get; }

    /// <summary>The loader's own warnings, repeated in the tool's result.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>The warnings as the result repeats them, after the caption; empty when the file loaded as authored.</summary>
    public string WarningsText => Warnings.Count == 0 ? "" : "\nThe loader's warnings:\n" + string.Join("\n", Warnings.Select(w => "- " + w));

    /// <summary>
    /// Parses <paramref name="bytes"/> with the engine's geometry loader (on the CPU, so off the frame thread) and poses it
    /// at <paramref name="clip"/> and <paramref name="time"/>; <paramref name="weightRefusals"/> (target name to the refusal of its weight) are reported for the names the file has; the problems, in the words of the arguments, are added to
    /// <paramref name="errors"/> and null returned.
    /// </summary>
    public static GlbScene? Prepare(IModelGeometryLoader loader, byte[] bytes, string? clip, float time, List<string> errors, IReadOnlyList<KeyValuePair<string, float>>? targets = null, IReadOnlyDictionary<string, string>? weightRefusals = null)
    {
        ParsedModel parsed;
        try
        {
            parsed = loader.Parse(bytes, ModelFormat.Gltf);
        }
        catch (Exception e)
        {
            errors.Add($"path: the engine's loader could not read the file: {e.GetBaseException().Message}");
            return null;
        }

        if (targets is { Count: > 0 })
        {
            var names = parsed.MorphTargets?.TargetNames ?? [];
            // A name is checked before its weight, as render checks them: an unknown target is named even when its weight is out of range too.
            foreach (var t in targets)
            {
                if (Array.IndexOf(names, t.Key) >= 0)
                {
                    // A weight that is not finite is never carried on to the draw as a number: it is refused here even when its words were lost.
                    if (weightRefusals is not null && weightRefusals.TryGetValue(t.Key, out var refusal)) errors.Add(refusal);
                    else if (!float.IsFinite(t.Value)) errors.Add($"targets.{t.Key}: the weight is not a finite number");
                    continue;
                }

                errors.Add(names.Length == 0
                    ? $"targets.{t.Key}: the file has no morph targets, so there is no weight to give; give no targets to draw it as it is"
                    : $"targets.{t.Key}: the file has no target named '{t.Key}'; the targets are {Carve.Core.NameList.Quoted(names)}");
            }

            if (errors.Count > 0) return null;
        }

        Matrix4x4[]? palette = null;
        if (clip is not null)
        {
            if (parsed.Skin is not { } skin)
            {
                errors.Add($"clip: the file has no skin, so it has no clip to pose it with; give no clip to draw it as it is");
                return null;
            }

            var found = Array.Find(skin.Animations ?? [], a => a.Name == clip);
            if (found is null)
            {
                var names = (skin.Animations ?? []).Select(a => a.Name).ToArray();
                errors.Add(names.Length == 0
                    ? $"clip: there is no clip named '{clip}'; the file has no clips"
                    : $"clip: there is no clip named '{clip}'; the clips are {Carve.Core.NameList.Quoted(names)}");
                return null;
            }

            if (time < 0 || time > found.Duration)
            {
                errors.Add(FormattableString.Invariant($"time: {time} s is outside the clip '{clip}', which runs from 0 to {found.Duration} s"));
                return null;
            }

            palette = Pose(skin.Skeleton, found, time);
        }
        else if (parsed.Skin is { } rest)
        {
            palette = Bind(rest.Skeleton);
        }

        var positions = parsed.Skin is { } skinned
            ? skinned.SkinnedMeshData.Vertices.Select(v => v.Position)
            : parsed.MeshData.Vertices.Select(v => v.Position);
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        var count = 0;
        foreach (var p in positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            count++;
        }

        if (count == 0)
        {
            errors.Add("path: the file has no vertices to draw");
            return null;
        }

        var indices = parsed.Skin is { } withIndices ? withIndices.SkinnedMeshData.Indices!.Length : parsed.MeshData.Indices!.Length;
        var space = DrawnSpace.Of(min, max);
        return new GlbScene(bytes, palette, space, space.Apply(max), count, indices / 3, parsed.Warnings ?? [], targets ?? [])
        {
            BoundsMin = min,
            BoundsMax = max,
        };
    }

    /// <summary>The skeleton in its bind pose (every bone at its node's rest transform), composed with the file's inverse binds: what a game that poses the model with no clip skins with.</summary>
    private static Matrix4x4[] Bind(Skeleton skeleton)
    {
        var local = new BonePose[skeleton.BoneCount];
        for (var i = 0; i < local.Length; i++)
            local[i] = BonePose.Bind(skeleton.Bones[i]);
        var palette = new Matrix4x4[skeleton.BoneCount];
        SkinningPalette.Compute(skeleton, local, palette);
        return palette;
    }

    /// <summary>The engine's own sampling of <paramref name="clip"/> at <paramref name="time"/>, composed into the palette the GPU skins with.</summary>
    private static Matrix4x4[] Pose(Skeleton skeleton, AnimationClip clip, float time)
    {
        var provider = new ClipPoseProvider(skeleton, clip) { Time = time };
        var local = new BonePose[skeleton.BoneCount];
        provider.Evaluate(local, new PoseScratch());
        var palette = new Matrix4x4[skeleton.BoneCount];
        SkinningPalette.Compute(skeleton, local, palette);
        return palette;
    }
}
