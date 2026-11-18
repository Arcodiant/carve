using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

[Trait("Tier", "regression")]
public class SkeletonToolTests
{
    private const string Three =
        """{"bones":[{"name":"hips","parent":null,"position":[0,1,0],"rotation":[0,0,0,1]},{"name":"spine","parent":"hips","position":[0,0.3,0],"rotation":[0,0,0,1]},{"name":"head","parent":"spine","position":[0,0.4,0],"rotation":[0,0,0,1]}]}""";

    private static async Task<CarveProcess> StartAsync()
    {
        var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        return carve;
    }

    private static async Task<string> DescriptionAsync(CarveProcess carve, string tool) =>
        (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == tool).GetProperty("description").GetString()!;

    [Fact]
    public async Task A_name_with_a_colon_and_the_given_order_round_trip()
    {
        using var carve = await StartAsync();
        await carve.CallToolAsync("define_skeleton",
            """{"bones":[{"name":"mixamorig:Hips","position":[0,0,0],"rotation":[0,0,0,1]},{"name":"a","parent":"mixamorig:Hips","position":[1,0,0],"rotation":[0,0,0,1]}]}""");
        var read = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_skeleton", "{}"))).RootElement.GetProperty("bones");
        Assert.Equal(["mixamorig:Hips", "a"], read.EnumerateArray().Select(b => b.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Refusals_list_every_problem_by_bone_and_leave_the_skeleton_unchanged()
    {
        using var carve = await StartAsync();
        await carve.CallToolAsync("define_skeleton", Three);
        var before = TextOf(await carve.CallToolAsync("get_skeleton", "{}"));
        var result = await carve.CallToolAsync("define_skeleton",
            """{"bones":[{"name":"","position":[0,0,0],"rotation":[0,0,0,1]},{"name":"a","parent":"zz","position":[0,0,0],"rotation":[0,0,0,2]},{"name":"b","position":[0,0],"rotation":[0,0,0,1]}]}""");
        Assert.True(IsError(result));
        var text = TextOf(result);
        Assert.Contains("bones[0]", text);
        Assert.Contains("bones[1]", text);
        Assert.Contains("bones[2].position", text);
        // The refused set also has three bones, so the whole text is compared, not the count.
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_skeleton", "{}")));
    }

    [Fact]
    public async Task More_than_255_bones_is_refused()
    {
        using var carve = await StartAsync();
        static string Bones(int n) => "{\"bones\":[" + string.Join(",", Enumerable.Range(0, n).Select(i => $"{{\"name\":\"b{i}\",\"position\":[0,0,0],\"rotation\":[0,0,0,1]}}")) + "]}";
        Assert.False(IsError(await carve.CallToolAsync("define_skeleton", Bones(255))));
        Assert.True(IsError(await carve.CallToolAsync("define_skeleton", Bones(256))));
    }

    [Fact]
    public async Task Defining_a_skeleton_is_one_undo_step_and_get_skeleton_without_one_is_an_error()
    {
        using var carve = await StartAsync();
        Assert.True(IsError(await carve.CallToolAsync("get_skeleton", "{}")));
        await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""");
        await carve.CallToolAsync("define_skeleton", Three);
        var undo = await carve.CallToolAsync("undo", "{}");
        Assert.False(IsError(undo), TextOf(undo));
        Assert.Contains("define_skeleton", TextOf(undo));
        Assert.True(IsError(await carve.CallToolAsync("get_skeleton", "{}")));
    }

    [Fact]
    public async Task The_skeleton_survives_edits_to_the_mesh()
    {
        using var carve = await StartAsync();
        await carve.CallToolAsync("define_skeleton", Three);
        await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""");
        await carve.CallToolAsync("move", """{"vertices":[0],"by":[0,0,1]}""");
        Assert.False(IsError(await carve.CallToolAsync("get_skeleton", "{}")));
    }
}
