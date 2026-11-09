using Carve.Core;
using SharpGLTF.Schema2;

namespace Carve.Server;

/// <summary>
/// The clips-only <c>.glb</c> (D-6, Export modes; T-46): the rig half of a skinned file and nothing else, one file of
/// clips that serves every character in the game. Layout: node 0 <c>Armature</c> (identity, not a bone), nodes 1 to n
/// the bones in the skeleton's order, <c>scene</c> 0 whose only root is the armature, <c>skins[0]</c> named for the file
/// over every bone with its inverse binds, and one animation per clip in the model's order. There is no mesh, no
/// material, no mesh node: the skin stays even so, because without one the loader reorders bones depth-first and a
/// clip's tracks then line up with the wrong bones. Built by the same <see cref="GlbRig"/> steps as the model mode, so the
/// bones, inverse binds and animations are byte for byte the model file's.
/// </summary>
public static class ClipsExport
{
    /// <summary>Why the model cannot be written as clips, or null when it can.</summary>
    public static string? Refusal(Model model)
    {
        if (model.Skeleton is null) return "the model has no skeleton, and a clips file is a skeleton's clips (define_skeleton first)";
        if (model.Clips.Count == 0) return "the model has no clips, and a clips file is its clips (define_clip first)";
        return null;
    }

    /// <summary>Writes the model's armature, skin and clips; the model must have a skeleton and clips.</summary>
    public static byte[] Write(Model model, string name)
    {
        var skeleton = model.Skeleton ?? throw new ArgumentException("the model has no skeleton", nameof(model));
        if (model.Clips.Count == 0) throw new ArgumentException("the model has no clips", nameof(model));
        var root = ModelRoot.CreateModel();
        var scene = root.UseScene(0);
        root.DefaultScene = scene;
        var joints = GlbRig.WriteArmature(scene, skeleton);
        GlbRig.WriteSkin(root, name, skeleton, joints);
        GlbRig.WriteClips(root, skeleton, joints, model.Clips);
        return root.WriteGLB().ToArray();
    }
}
