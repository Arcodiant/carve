using System.Numerics;

using Empyrean;

namespace Carve.Server;

/// <summary>
/// A <c>.glb</c> as <c>render_glb</c> draws it: the file's bytes, which the frame thread loads through the engine's model
/// loader, the bone palette the file's own clip gives at the time asked (null draws the file as it lies, unskinned), and the
/// framing of its rest pose (D-6: the rest bounds, as <c>render</c> frames a posed model, T-32). Nothing here shares
/// Carve's preview skinning (D-6, two samplers): the pose is the engine's <see cref="ClipPoseProvider"/> and
/// <see cref="SkinningPalette"/>, drawn by the engine's GPU skinning through a <see cref="SkinnedPose"/>.
/// Compared by reference in a <see cref="RenderSpec"/>: one per call.
/// </summary>
internal sealed class GlbScene
{
    private GlbScene(byte[] bytes, Matrix4x4[]? palette, DrawnSpace space, Vector3 drawnHalf, int vertices, int triangles, IReadOnlyList<string> warnings)
    {
        Bytes = bytes;
        Palette = palette;
        Space = space;
        DrawnHalf = drawnHalf;
        Vertices = vertices;
        Triangles = triangles;
        Warnings = warnings;
    }

    public byte[] Bytes { get; }

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
    /// at <paramref name="clip"/> and <paramref name="time"/>; the problems, in the words of the arguments, are added to
    /// <paramref name="errors"/> and null returned.
    /// </summary>
    public static GlbScene? Prepare(IModelGeometryLoader loader, byte[] bytes, string? clip, float time, List<string> errors)
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
        return new GlbScene(bytes, palette, space, space.Apply(max), count, indices / 3, parsed.Warnings ?? [])
        {
            BoundsMin = min,
            BoundsMax = max,
        };
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
