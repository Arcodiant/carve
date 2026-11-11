using Carve.Core;

namespace Carve.Server;

/// <summary>One bone's weight at each vertex of the model drawn as one (T-40), for the weights look.</summary>
internal static class WeightHeat
{
    /// <summary>
    /// The weight of <paramref name="bone"/> at every vertex of <see cref="Model.Combined"/>, in its vertex order: the sum of that
    /// bone's influences on the vertex (0 where the vertex is weighted but not by this bone), NaN where the vertex has no weight at all
    /// (a mesh with no weights has no weighted vertex). Throws when the model has no such bone.
    /// </summary>
    public static float[] Of(Model model, string bone)
    {
        if (Refusal(model, bone) is { } refusal) throw new InvalidOperationException(refusal);
        var index = model.Skeleton!.Names.ToList().IndexOf(bone);
        var heat = new List<float>();
        foreach (var (_, mesh) in model.Meshes)
        {
            for (var v = 0; v < mesh.Vertices.Count; v++)
            {
                if (mesh.Weights is not { } weights || !weights.Has(v)) { heat.Add(float.NaN); continue; }
                heat.Add((float)weights.Of(v).Where(i => i.Bone == index).Sum(i => i.Weight));
            }
        }

        return [.. heat];
    }

    /// <summary>Why <paramref name="bone"/>'s weights cannot be drawn on <paramref name="model"/>, or null when they can.</summary>
    public static string? Refusal(Model model, string bone) =>
        model.Skeleton is not { } skeleton ? "weights: the model has no skeleton; call define_skeleton first"
        : skeleton.Names.Contains(bone) ? null
        : $"weights: no bone named '{bone}'; the bones are {NameList.Quoted(skeleton.Names)}";
}
