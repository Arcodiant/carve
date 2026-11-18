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

        // An entry that is not an id is the one problem: the pair had two entries, so its length is not refused too.
        var notId = TextOf(await carve.CallToolAsync("loop_cut", """{"edge":[0,3.5],"at":[0.5]}"""));
        Assert.Contains("the 2nd listed entry: expected an integer id, got 3.5", notId);
        Assert.DoesNotContain("entry", notId.Replace("listed entry", ""));

        var after = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(8, after.GetProperty("vertices").GetArrayLength());
        Assert.Equal(6, after.GetProperty("faces").GetArrayLength());
    }
}
