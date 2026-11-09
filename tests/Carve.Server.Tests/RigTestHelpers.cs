using System.Numerics;
using System.Text;
using System.Text.Json;
using Carve.Core;
using Skeleton = Carve.Core.Skeleton;

namespace Carve.Server.Tests;

/// <summary>Helpers the skeleton, clip and skinned-export tests share.</summary>
internal static class RigTestHelpers
{
    /// <summary>A chain of bones, each a unit up from its parent, the first the root.</summary>
    public static Skeleton Bones(params string[] names) => Skeleton.Create(names.Select((n, i) =>
        new BoneSpec(n, i == 0 ? null : names[i - 1], new Vector3(0, 1, 0), Quaternion.Identity)).ToList()).Skeleton!;

    /// <summary>A looping clip, asserted valid.</summary>
    public static Clip MakeClip(string name, float duration, Skeleton rig, params BoneChannelsSpec[] channels)
    {
        var (clip, errors) = Clip.Create(name, duration, true, channels, rig);
        Assert.True(clip is not null, string.Join("; ", errors));
        return clip!;
    }

    /// <summary>The JSON chunk of a .glb, asserted to be one.</summary>
    public static JsonElement Json(byte[] glb)
    {
        var length = BitConverter.ToInt32(glb, 12);
        Assert.Equal("JSON", Encoding.ASCII.GetString(glb, 16, 4));
        return JsonDocument.Parse(glb.AsMemory(20, length)).RootElement;
    }
}
