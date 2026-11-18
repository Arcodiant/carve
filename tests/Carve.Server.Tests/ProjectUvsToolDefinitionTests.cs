using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class ProjectUvsToolDefinitionTests
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static async Task<ToolHarness> StartAsync(string mesh = Cube)
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", mesh));
        return carve;
    }

    private static float[] Floats(JsonElement list) => [.. list.EnumerateArray().Select(x => x.GetSingle())];

    [Fact]
    public async Task The_described_examples_run_and_read_back()
    {
        using var carve = await StartAsync();
        var examples = ToolList.Examples(await carve.ListToolsAsync(), "project_uvs");
        Assert.Equal(2, examples.Length);
        var box = Ok(await carve.CallToolAsync("project_uvs", examples[0]));
        Assert.Equal(6, box.GetProperty("mapped").GetArrayLength());
        Assert.Equal([5], box.GetProperty("axes").GetProperty("+x").EnumerateArray().Select(x => x.GetInt32()));
        var planar = Ok(await carve.CallToolAsync("project_uvs", examples[1]));
        Assert.Equal([3], planar.GetProperty("mapped").EnumerateArray().Select(x => x.GetInt32()));
        Assert.False(planar.TryGetProperty("axes", out _));
        Assert.Equal([0f, 0.25f, 0.5f, 0.75f], Floats(planar.GetProperty("bounds")));
    }
}
