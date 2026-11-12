using Carve.Core;

namespace Carve.Server;

/// <summary>
/// The piece <c>.glb</c> (D-6, Export modes and Pieces share the body's palette; T-50): the named meshes of a skinned
/// model in one file, as the model file writes them (one glTF mesh, one primitive per mesh and colour), with the whole
/// skeleton: every bone in the model's order and the model's inverse binds, even bones no vertex of the piece follows,
/// and no animations. The game binds the piece to the body's <c>SkinnedPose.Palette</c>, which is positional, so the
/// skeleton is never trimmed to the bones used. Built by <see cref="SkinnedExport"/> on a model of the chosen meshes
/// and no clips, so the armature and skin are the model file's byte for byte. The chosen meshes keep the model's order.
/// </summary>
public static class PieceExport
{
    /// <summary>Why the meshes cannot be written as a piece, or null when they can.</summary>
    public static string? Refusal(Model model, IReadOnlyList<string> meshes)
    {
        if (meshes.Count == 0)
            return $"meshes is empty, and a piece is the meshes it names; the model's meshes are {model.ListNames()}";
        var unknown = meshes.Where(m => model.Find(m) is null).Distinct().ToList();
        if (unknown.Count > 0)
            return $"no mesh named {NameList.Quoted(unknown)}; the model's meshes are {model.ListNames()}";
        if (model.Skeleton is null)
            return "the model has no skeleton, and a piece shares the body's bones (define_skeleton first)";
        return null;
    }

    /// <summary>The model of just the named meshes (in the model's order), with the whole skeleton and no clips; call after <see cref="Refusal"/>.</summary>
    public static Model Select(Model model, IReadOnlyList<string> meshes)
    {
        var (piece, errors) = Model.Create([.. model.Meshes.Where(m => meshes.Contains(m.Name))], model.Skeleton, []);
        return piece ?? throw new ArgumentException(string.Join("; ", errors.Select(e => e.ToString())), nameof(meshes));
    }

    /// <summary>Writes the piece; the model must have a skeleton and every name must be one of its meshes.</summary>
    public static SkinnedGlb Write(Model model, IReadOnlyList<string> meshes, string name) =>
        SkinnedExport.Write(Select(model, meshes), name);
}
