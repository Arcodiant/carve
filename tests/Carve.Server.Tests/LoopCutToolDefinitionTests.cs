using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class LoopCutToolDefinitionTests() : TempFolderTests("carve-loop-cut")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    [Fact]
    public async Task The_worked_example_loops_the_four_sides_closed_in_one_undo_step()
    {
        using var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));

        var body = Ok(await carve.CallToolAsync("loop_cut", ToolList.Examples(tools, "loop_cut").Single()));
        Assert.Equal(12, body.GetProperty("vertices").GetInt32());
        Assert.Equal(10, body.GetProperty("faces").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal([8, 9, 10, 11], Ints(body.GetProperty("created").GetProperty("vertices")));
        Assert.Equal([6, 7, 8, 9], Ints(body.GetProperty("created").GetProperty("faces")));
        var split = body.GetProperty("split").EnumerateArray()
            .Select(s => (s.GetProperty("face").GetInt32(), Ints(s.GetProperty("pieces")))).ToArray();
        Assert.Equal([0, 1, 4, 5], split.Select(s => s.Item1));
        Assert.All(split, s => Assert.Equal(2, s.Item2.Length));
        Assert.All(split, s => Assert.Equal(s.Item1, s.Item2[0]));
        Assert.Empty(Ints(body.GetProperty("deleted").GetProperty("faces")));

        Assert.Contains("\"closed\":true", TextOf(await carve.CallToolAsync("check", "{}")));

        Assert.Contains("loop_cut", TextOf(await carve.CallToolAsync("undo", "{}")));
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(8, mesh.GetProperty("vertices").GetArrayLength());
        Assert.Equal(6, mesh.GetProperty("faces").GetArrayLength());
    }

    [Fact]
    public async Task The_description_names_the_refusal_of_a_cut_along_or_over_an_edge_of_a_concave_quad()
    {
        // RV-33: the refusal RV-32 added is a bad input the description lists, in the words the tool error uses.
        using var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Assert.Contains("a cut across a quad that runs along or crosses one of its edges (a concave quad can make one)",
            ToolList.Description(tools, "loop_cut"));

        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[4,0,0],[1,1,0],[0,4,0]],"faces":[[0,1,2,3]]}"""));
        var refused = await carve.CallToolAsync("loop_cut", """{"edge":[0,1],"at":[0.5]}""");
        Assert.True(IsError(refused));
        Assert.Contains("the cut at 0.5 across faces[0] crosses its edge [1, 2]; loop_cut cuts only across a quad", TextOf(refused));
    }
}
