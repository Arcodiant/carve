using System.Numerics;
using Carve.Core;
using SharpGLTF.Schema2;

namespace Carve.Server;

/// <summary>
/// The rig half of a skinned <c>.glb</c>, written onto a <see cref="ModelRoot"/> so that every export mode lays it out
/// the same way (D-6, Export modes): an unanimated identity node <c>Armature</c>, which is not a bone, created first
/// (node 0), then one node per bone in the skeleton's own order (nodes 1 to n), each a child of its parent bone or, for
/// a root bone, of <c>Armature</c>, its local transform the bone's rest position and rotation (no scale). The skin lists
/// the bones as joints in that order, with each joint's inverse bind matrix the inverse of the bone's rest world
/// transform (D-6, The rest pose is the bind pose). One animation per clip, in the model's clip order.
/// The clips-only and piece exports (T-46, T-50) reuse these three steps.
/// </summary>
public static class GlbRig
{
    public const string ArmatureName = "Armature";

    /// <summary>The armature node and the bone nodes under it, in the default scene; returns the bones' nodes in skeleton order.</summary>
    public static Node[] WriteArmature(Scene scene, Skeleton skeleton)
    {
        var armature = scene.CreateNode(ArmatureName);
        var joints = new Node[skeleton.Bones.Count];
        for (var i = 0; i < joints.Length; i++)
        {
            var bone = skeleton.Bones[i];
            var parent = bone.Parent < 0 ? armature : joints[bone.Parent];
            var node = parent.CreateNode(bone.Name);
            node.LocalTransform = new SharpGLTF.Transforms.AffineTransform(null, Unit(bone.Rotation), bone.Position);
            joints[i] = node;
        }
        return joints;
    }

    /// <summary>The skin over <paramref name="joints"/>, with <see cref="InverseBinds"/>; named after the file.</summary>
    public static Skin WriteSkin(ModelRoot root, string name, Skeleton skeleton, Node[] joints)
    {
        var binds = InverseBinds(skeleton);
        var skin = root.CreateSkin(name);
        skin.BindJoints([.. joints.Select((j, i) => (j, binds[i]))]);
        return skin;
    }

    /// <summary>
    /// The inverse of each bone's rest world transform, composed in double from the root down (rotations normalised,
    /// as the nodes are written) and inverted as a rigid transform, then narrowed once.
    /// </summary>
    public static Matrix4x4[] InverseBinds(Skeleton skeleton)
    {
        var bones = skeleton.Bones;
        var rotations = new (double X, double Y, double Z, double W)[bones.Count];
        var translations = new (double X, double Y, double Z)[bones.Count];
        var result = new Matrix4x4[bones.Count];
        for (var i = 0; i < bones.Count; i++)
        {
            var q = Unit(bones[i].Rotation);
            var local = ((double)q.X, (double)q.Y, (double)q.Z, (double)q.W);
            var t = ((double)bones[i].Position.X, (double)bones[i].Position.Y, (double)bones[i].Position.Z);
            if (bones[i].Parent < 0)
            {
                rotations[i] = local;
                translations[i] = t;
            }
            else
            {
                var p = bones[i].Parent;
                rotations[i] = Multiply(rotations[p], local);
                var r = Rotate(rotations[p], t);
                translations[i] = (translations[p].X + r.X, translations[p].Y + r.Y, translations[p].Z + r.Z);
            }

            // Inverse of x -> R x + T is x -> R^-1 x - R^-1 T.
            var inverse = (-rotations[i].X, -rotations[i].Y, -rotations[i].Z, rotations[i].W);
            var it = Rotate(inverse, translations[i]);
            result[i] = Matrix(inverse, (-it.X, -it.Y, -it.Z));
        }
        return result;
    }

    /// <summary>
    /// One glTF animation per clip, named as the clip. Every animated channel is keyed at 0 and at the duration, holding
    /// its nearest key where the clip has none there, so the loaded clip's duration is the one given (D-6, Clips); LINEAR
    /// or STEP as the channel says. A clip that animates nothing holds the first bone's rest rotation, so it still loads,
    /// with its duration.
    /// </summary>
    public static void WriteClips(ModelRoot root, Skeleton skeleton, Node[] joints, IReadOnlyList<Clip> clips)
    {
        foreach (var clip in clips)
        {
            var animation = root.CreateAnimation(clip.Name);
            if (clip.Channels.Count == 0)
            {
                var rest = Unit(skeleton.Bones[0].Rotation);
                animation.CreateRotationChannel(joints[0], new SortedDictionary<float, Quaternion> { [0f] = rest, [clip.Duration] = rest }, false);
                continue;
            }

            foreach (var channels in clip.Channels)
            {
                var joint = joints[IndexOf(skeleton, channels.Bone)];
                if (channels.Rotation is { } rotation)
                    animation.CreateRotationChannel(joint, Keyed(rotation, clip.Duration, Unit), rotation.Interpolation == ClipInterpolation.Linear);
                if (channels.Translation is { } translation)
                    animation.CreateTranslationChannel(joint, Keyed(translation, clip.Duration, v => v), translation.Interpolation == ClipInterpolation.Linear);
            }
        }
    }

    /// <summary>The channel's keys, with the first key held back to 0 and the last held on to the duration.</summary>
    public static SortedDictionary<float, T> Keyed<T>(ClipChannel<T> channel, float duration, Func<T, T> value)
    {
        var keys = new SortedDictionary<float, T>();
        foreach (var key in channel.Keys) keys[key.Time] = value(key.Value);
        var first = channel.Keys[0];
        var last = channel.Keys[^1];
        if (first.Time > 0f) keys[0f] = value(first.Value);
        if (last.Time < duration) keys[duration] = value(last.Value);
        return keys;
    }

    private static int IndexOf(Skeleton skeleton, string bone)
    {
        for (var i = 0; i < skeleton.Bones.Count; i++)
            if (skeleton.Bones[i].Name == bone) return i;
        throw new InvalidOperationException($"no bone named '{bone}'");
    }

    /// <summary>A rotation within the skeleton's tolerance of unit, normalised in double, as glTF wants it.</summary>
    public static Quaternion Unit(Quaternion q)
    {
        var length = Math.Sqrt(((double)q.X * q.X) + ((double)q.Y * q.Y) + ((double)q.Z * q.Z) + ((double)q.W * q.W));
        return new Quaternion((float)(q.X / length), (float)(q.Y / length), (float)(q.Z / length), (float)(q.W / length));
    }

    private static (double X, double Y, double Z, double W) Multiply((double X, double Y, double Z, double W) a, (double X, double Y, double Z, double W) b) =>
        ((a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
         (a.W * b.Y) - (a.X * b.Z) + (a.Y * b.W) + (a.Z * b.X),
         (a.W * b.Z) + (a.X * b.Y) - (a.Y * b.X) + (a.Z * b.W),
         (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));

    private static (double X, double Y, double Z) Rotate((double X, double Y, double Z, double W) q, (double X, double Y, double Z) v)
    {
        // v + 2w (q x v) + 2 q x (q x v)
        var cx = (q.Y * v.Z) - (q.Z * v.Y);
        var cy = (q.Z * v.X) - (q.X * v.Z);
        var cz = (q.X * v.Y) - (q.Y * v.X);
        var ccx = (q.Y * cz) - (q.Z * cy);
        var ccy = (q.Z * cx) - (q.X * cz);
        var ccz = (q.X * cy) - (q.Y * cx);
        return (v.X + (2 * ((q.W * cx) + ccx)), v.Y + (2 * ((q.W * cy) + ccy)), v.Z + (2 * ((q.W * cz) + ccz)));
    }

    /// <summary>The row-vector matrix (System.Numerics' convention) of rotation <paramref name="q"/> then translation <paramref name="t"/>.</summary>
    private static Matrix4x4 Matrix((double X, double Y, double Z, double W) q, (double X, double Y, double Z) t)
    {
        double xx = q.X * q.X, yy = q.Y * q.Y, zz = q.Z * q.Z, xy = q.X * q.Y, xz = q.X * q.Z, yz = q.Y * q.Z, wx = q.W * q.X, wy = q.W * q.Y, wz = q.W * q.Z;
        return new Matrix4x4(
            (float)(1 - (2 * (yy + zz))), (float)(2 * (xy + wz)), (float)(2 * (xz - wy)), 0f,
            (float)(2 * (xy - wz)), (float)(1 - (2 * (xx + zz))), (float)(2 * (yz + wx)), 0f,
            (float)(2 * (xz + wy)), (float)(2 * (yz - wx)), (float)(1 - (2 * (xx + yy))), 0f,
            (float)t.X, (float)t.Y, (float)t.Z, 1f);
    }
}
