using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-66: <c>loop_cut</c> through the client on <c>define_mesh</c>'s cube, its <c>split</c>, then <c>check</c> and <c>undo</c>.</summary>
[Trait("Tier", "regression")]
public sealed class LoopCutToolTests() : TempFolderTests("carve-loop-cut")
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
        using var carve = new CarveProcess([], workingDirectory: Root);
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
    public async Task Two_fractions_from_the_other_end_and_bad_input_is_refused_leaving_the_mesh_alone()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));

        var two = Ok(await carve.CallToolAsync("loop_cut", """{"edge":[3,0],"at":[0.25,0.5]}"""));
        Assert.Equal(16, two.GetProperty("vertices").GetInt32());
        Assert.All(two.GetProperty("split").EnumerateArray(), s => Assert.Equal(3, s.GetProperty("pieces").GetArrayLength()));
        var verts = Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("vertices");
        // From vertex 3 (y = 1): 0.25 and 0.5 along the edge are y = 0.75 and 0.5.
        Assert.Equal([0.5f, 0.5f, 0.5f, 0.5f, 0.75f, 0.75f, 0.75f, 0.75f],
            Enumerable.Range(8, 8).Select(i => verts[i].GetProperty("at")[1].GetSingle()).Order());
        Ok(await carve.CallToolAsync("undo", "{}"));

        foreach (var (args, mention) in new[]
        {
            ("""{"edge":[0,2],"at":[0.5]}""", "[0, 2] is not an edge of the mesh"),
            ("""{"edge":[0,2],"at":[0.5]}""", "select with kind edges lists the real edges"),
            ("""{"edge":[0,3],"at":[]}""", "at: list at least one fraction"),
            ("""{"edge":[0,3],"at":[0.6,0.4]}""", "strictly ascending"),
            ("""{"edge":[0,3],"at":[1]}""", "not strictly between 0 and 1"),
            ("""{"edge":[0,3]}""", "at: required but missing"),
            ("""{"at":[0.5]}""", "edge: required but missing"),
            ("""{"edge":[0],"at":[0.5]}""", "expected a pair"),
        })
        {
            var r = await carve.CallToolAsync("loop_cut", args);
            Assert.True(IsError(r), args);
            Assert.Contains(mention, TextOf(r));
        }

        var after = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(8, after.GetProperty("vertices").GetArrayLength());
        Assert.Equal(6, after.GetProperty("faces").GetArrayLength());
    }
}
