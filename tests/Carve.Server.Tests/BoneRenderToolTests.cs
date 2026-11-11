using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-39: render's <c>bones</c> and <c>bone_labels</c> refusals and captions; where the lines land is the crate scenario's.</summary>
[Trait("Tier", "regression")]
public sealed class BoneRenderToolTests() : TempFolderTests("carve-bone-render")
{
    private const string Tri = """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""";
    private const string Rig = """{"bones":[{"name":"Hips","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]},{"name":"Spine","parent":"Hips","position":[0,1,0],"rotation":[0,0,0,1]}]}""";

    [Fact]
    public async Task Bones_need_a_skeleton_and_bone_labels_need_bones_and_the_caption_says_so()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Assert.False(IsError(await carve.CallToolAsync("define_mesh", Tri)));

        var none = await carve.CallToolAsync("render", """{"bones":true}""");
        Assert.True(IsError(none), TextOf(none));
        Assert.Contains("no skeleton", TextOf(none));

        Assert.False(IsError(await carve.CallToolAsync("define_skeleton", Rig)));
        var alone = await carve.CallToolAsync("render", """{"bone_labels":true}""");
        Assert.True(IsError(alone), TextOf(alone));
        Assert.Contains("needs bones: true", TextOf(alone));

        var plain = await carve.CallToolAsync("render", """{"bones":false}""");
        Assert.DoesNotContain("bones", TextOf(plain));
        var drawn = await carve.CallToolAsync("render", """{"bones":true,"bone_labels":true,"size":256}""");
        Assert.False(IsError(drawn), TextOf(drawn));
        Assert.Contains("bones, bone labels", TextOf(drawn));
    }
}
