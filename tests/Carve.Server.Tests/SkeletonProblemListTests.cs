using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>RV-24 A4: a bone that cannot be read adds no false problem to the list, and its true ones still show.</summary>
[Trait("Tier", "regression")]
public class SkeletonProblemListTests
{
    private static async Task<string> RefusalAsync(string bones)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        var result = await carve.CallToolAsync("define_skeleton", "{\"bones\":" + bones + "}");
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    [Fact]
    public async Task A_malformed_bone_lends_no_name_to_the_duplicate_check()
    {
        var text = await RefusalAsync("""[{"name":"hips","position":[0,0,0]},{"name":"x","position":[0,0,0],"rotation":[0,0,0,1]}]""");
        Assert.Contains("bones[0].rotation: required but missing", text);
        Assert.DoesNotContain("already", text);
        Assert.DoesNotContain("bones[1]", text);
    }

    [Fact]
    public async Task A_child_of_a_malformed_bone_finds_its_parent()
    {
        var text = await RefusalAsync("""[{"name":"hips","position":[0,0,0]},{"name":"spine","parent":"hips","position":[0,1,0],"rotation":[0,0,0,1]}]""");
        Assert.Contains("bones[0].rotation: required but missing", text);
        Assert.DoesNotContain("unknown parent", text);
        Assert.DoesNotContain("bones[1]", text);
    }

    [Fact]
    public async Task A_malformed_bone_still_reports_its_true_name_and_parent_problems()
    {
        var text = await RefusalAsync("""[{"name":"hips","position":[0,0,0],"rotation":[0,0,0,1]},{"name":"hips","parent":"tail","position":[0,0,0]}]""");
        Assert.Contains("bones[1].rotation: required but missing", text);
        Assert.Contains("bones[1]: the name 'hips' is already bones[0]'s", text);
        Assert.Contains("bones[1]: unknown parent 'tail'", text);
    }
}
