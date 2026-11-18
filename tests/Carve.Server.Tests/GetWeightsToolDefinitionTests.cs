using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class GetWeightsToolDefinitionTests
{
    private static readonly string Cube = JsonSerializer.Serialize(new
    {
        name = "cube",
        vertices = TestMeshes.CubeVertices.Select(v => new[] { v.X, v.Y, v.Z }),
        faces = TestMeshes.CubeFaces,
    });

    private const string Chain =
        """{"bones":[{"name":"Hips","parent":null,"position":[0,-0.5,0],"rotation":[0,0,0,1]},{"name":"Head","parent":"Hips","position":[0,1,0],"rotation":[0,0,0,1]},{"name":"Tail","parent":"Hips","position":[0,0,5],"rotation":[0,0,0,1]}]}""";

    private static void Ok(JsonElement r) => Assert.False(IsError(r), TextOf(r));

    private static async Task<ToolHarness> StartAsync()
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Ok(await carve.CallToolAsync("define_skeleton", Chain));
        return carve;
    }

    [Fact]
    public async Task The_worked_example_runs_as_written_and_gives_the_result_its_description_shows()
    {
        using var carve = await StartAsync();
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "get_weights").GetProperty("description").GetString()!;
        var lines = description.Split('\n');
        var example = lines.Single(l => l.StartsWith("Example: ", StringComparison.Ordinal))["Example: ".Length..];
        Assert.Equal("""{"vertices":[0,7]}""", example);

        // The description's premise: set_weights binds vertices 0 and 1 of the cube wholly to Head.
        Ok(await carve.CallToolAsync("set_weights", """{"vertices":[0,1],"weights":{"Head":1}}"""));
        var result = JsonNode.Parse(TextOf(await carve.CallToolAsync("get_weights", example)));
        var shown = JsonNode.Parse(lines.Single(l => l.StartsWith("Result: ", StringComparison.Ordinal))["Result: ".Length..]);
        Assert.True(JsonNode.DeepEquals(shown, result), result!.ToJsonString());

        // What the result says, named, so a regression reads as one: a weighted vertex by name, an unweighted one empty, the
        // unweighted count and ids, and the idle bones, Head now followed.
        Assert.Equal(1, result["vertices"]![0]!["weights"]!["Head"]!.GetValue<double>());
        Assert.Empty(result["vertices"]![1]!["weights"]!.AsObject());
        Assert.Equal(6, result["summary"]!["unweighted"]!.GetValue<int>());
        Assert.StartsWith("6 vertices have no weight: 2, 3, 4, 5, 6, 7", result["warnings"]![0]!.GetValue<string>());
        Assert.Equal("note: no vertex follows 'Hips', 'Tail'", result["warnings"]![1]!.GetValue<string>());
    }
}
