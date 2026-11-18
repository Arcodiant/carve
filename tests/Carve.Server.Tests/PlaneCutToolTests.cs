using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-65: <c>plane_cut</c> through the client on <c>define_mesh</c>'s cube, its <c>split</c> and <c>deleted</c>, then <c>check</c>.</summary>
[Trait("Tier", "regression")]
public sealed class PlaneCutToolTests() : TempFolderTests("carve-plane-cut")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    [Fact]
    public async Task Keep_both_splits_an_open_mesh_warns_and_bad_input_is_refused()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));

        var both = Ok(await carve.CallToolAsync("plane_cut", """{"point":[0,0.5,0],"normal":[0,1,0]}"""));
        Assert.Equal(12, both.GetProperty("vertices").GetInt32());
        Assert.Equal(10, both.GetProperty("faces").GetInt32());
        Assert.Equal(4, both.GetProperty("split").GetArrayLength());
        Assert.Empty(Ints(both.GetProperty("deleted").GetProperty("faces")));
        Ok(await carve.CallToolAsync("undo", "{}"));

        Ok(await carve.CallToolAsync("delete", """{"faces":[1]}"""));
        var open = Ok(await carve.CallToolAsync("plane_cut", """{"point":[0,0.5,0],"normal":[0,1,0],"keep":"below"}"""));
        Assert.Contains(open.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith("fill: the cut from vertex 11 to vertex 10", StringComparison.Ordinal));
        Assert.False(open.GetProperty("closed").GetBoolean());

        foreach (var (args, mention) in new[]
        {
            ("""{"point":[0,0.5,0],"normal":[0,0,0]}""", "normal: must be a finite, non-zero vector"),
            ("""{"point":[0,5,0],"normal":[0,1,0]}""", "the plane touches no face"),
            ("""{"point":[0,0.25,0],"normal":[0,1,0],"keep":"below","faces":[0]}""", "faces: a cut limited"),
            ("""{"point":[0,0.25,0],"normal":[0,1,0],"keep":"left"}""", "keep: expected both, above or below"),
            ("""{"normal":[0,1,0]}""", "point: required but missing"),
        })
        {
            var r = await carve.CallToolAsync("plane_cut", args);
            Assert.True(IsError(r), args);
            Assert.Contains(mention, TextOf(r));
        }
    }
}
