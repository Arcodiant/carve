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

    /// <summary>RV-25 R6: open with mesh on a clip library says the file has no meshes rather than listing none.</summary>
    [Fact]
    public void Taking_a_mesh_from_a_clip_library_says_it_has_no_meshes()
    {
        Assert.Null(Model.Of("Body", Polygons()).WithMeshFrom(Library(), "Body", out var problem));
        Assert.Equal("mesh: the file has no mesh named 'Body'; it has no meshes (a clip library)", problem);
    }

    private static PolygonMesh Polygons() =>
        PolygonMesh.Create([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [[0, 1, 2]]).Mesh!;
}
