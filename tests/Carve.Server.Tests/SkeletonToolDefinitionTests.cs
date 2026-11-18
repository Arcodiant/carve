using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public class SkeletonToolDefinitionTests
{
    private const string Three =
        """{"bones":[{"name":"hips","parent":null,"position":[0,1,0],"rotation":[0,0,0,1]},{"name":"spine","parent":"hips","position":[0,0.3,0],"rotation":[0,0,0,1]},{"name":"head","parent":"spine","position":[0,0.4,0],"rotation":[0,0,0,1]}]}""";

    private static async Task<ToolHarness> StartAsync()
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        return carve;
    }

    private static async Task<string> DescriptionAsync(ToolHarness carve, string tool) =>
        (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == tool).GetProperty("description").GetString()!;

    [Fact]
    public async Task The_worked_example_is_accepted_and_reads_back_with_rest_world_positions()
    {
        using var carve = await StartAsync();
        var description = await DescriptionAsync(carve, "define_skeleton");
        Assert.Contains("Worked example (three bones):", description);
        Assert.EndsWith(Three, description);
        Assert.Contains("LOCAL TO THE PARENT", description);

        var defined = await carve.CallToolAsync("define_skeleton", Three);
        Assert.False(IsError(defined), TextOf(defined));
        var read = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_skeleton", "{}"))).RootElement.GetProperty("bones");
        Assert.Equal(["hips", "spine", "head"], read.EnumerateArray().Select(b => b.GetProperty("name").GetString()));
        Assert.Equal(JsonValueKind.Null, read[0].GetProperty("parent").ValueKind);
        Assert.Equal("spine", read[2].GetProperty("parent").GetString());
        Assert.Equal([0f, 0.4f, 0f], read[2].GetProperty("position").EnumerateArray().Select(x => x.GetSingle()));
        Assert.Equal([0f, 0f, 0f, 1f], read[2].GetProperty("rotation").EnumerateArray().Select(x => x.GetSingle()));
        Assert.Equal(1.7f, read[2].GetProperty("restWorldPosition")[1].GetSingle(), 4);
    }
}
