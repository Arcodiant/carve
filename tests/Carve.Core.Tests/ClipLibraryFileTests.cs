using System.Numerics;
using Carve.Core;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>RV-24 B2/D5, the person's ruling of 2026-11-09: a Carve file of a skeleton and clips with no mesh.</summary>
[Trait("Tier", "unit")]
public class ClipLibraryFileTests
{
    private static Model Library()
    {
        var rig = Skeleton.Create([new BoneSpec("Hips", null, Vector3.UnitY, Quaternion.Identity)]).Skeleton!;
        var (clip, errors) = Clip.Create("idle", 1, false,
            [new BoneChannelsSpec("Hips", null, new ChannelSpec<Vector3>(null, [new ClipKey<Vector3>(0, Vector3.UnitY)]))], rig);
        Assert.Empty(errors);
        return Model.Empty.WithSkeleton(rig).WithClip(clip!);
    }

    [Fact]
    public void A_skeleton_and_clips_with_no_mesh_write_meshes_empty_and_read_back_the_same()
    {
        var text = CarveFile.Write(Library());
        Assert.Contains("\"meshes\": [],", text);
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("\n", read.Errors));
        Assert.Empty(read.Model!.Meshes);
        Assert.Equal(["Hips"], read.Model.Skeleton!.Names);
        Assert.Equal(["idle"], read.Model.Clips.Select(c => c.Name));
        Assert.Equal(text, CarveFile.Write(read.Model));
    }

    [Fact]
    public void No_meshes_with_a_null_skeleton_is_still_refused()
    {
        var read = CarveFile.Read("{\"carve\": 2, \"meshes\": [], \"skeleton\": null, \"clips\": []}");
        Assert.False(read.Succeeded);
        Assert.Equal("meshes: the file has no meshes and no skeleton; a Carve file holds at least one mesh, or a skeleton (a clip library)", Assert.Single(read.Errors).ToString());
    }
}
