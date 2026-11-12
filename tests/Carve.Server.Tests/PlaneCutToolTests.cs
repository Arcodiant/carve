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
    public async Task The_worked_example_gives_a_half_height_closed_box_in_one_undo_step()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools)));

        var body = Ok(await carve.CallToolAsync("plane_cut", ToolList.Examples(tools, "plane_cut").Single()));
        Assert.Equal(8, body.GetProperty("vertices").GetInt32());
        Assert.Equal(6, body.GetProperty("faces").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(0.5f, body.GetProperty("bounds").GetProperty("max")[1].GetSingle());
        Assert.Equal(0f, body.GetProperty("bounds").GetProperty("min")[1].GetSingle());
        var split = body.GetProperty("split").EnumerateArray()
            .Select(s => (s.GetProperty("face").GetInt32(), Ints(s.GetProperty("pieces")))).ToArray();
        Assert.Equal([0, 1, 4, 5], split.Select(s => s.Item1));
        Assert.All(split, s => Assert.Equal(2, s.Item2.Length));
        Assert.All(split, s => Assert.Equal(s.Item1, s.Item2[0]));
        var deletedFaces = Ints(body.GetProperty("deleted").GetProperty("faces"));
        Assert.Contains(3, deletedFaces);
        // Each upper piece the split appended is in split and in deleted.
        Assert.All(split, s => Assert.Contains(s.Item2[1], deletedFaces));
        Assert.Equal(5, deletedFaces.Length);
        Assert.Equal(4, Ints(body.GetProperty("deleted").GetProperty("vertices")).Length);
        Assert.Single(Ints(body.GetProperty("created").GetProperty("faces")));
        Assert.Empty(body.GetProperty("warnings").EnumerateArray());

        var check = TextOf(await carve.CallToolAsync("check", "{}"));
        Assert.Contains("\"closed\":true", check);

        Assert.Contains("plane_cut", TextOf(await carve.CallToolAsync("undo", "{}")));
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal(8, mesh.GetProperty("vertices").GetArrayLength());
        Assert.Equal(6, mesh.GetProperty("faces").GetArrayLength());
        Assert.True(mesh.GetProperty("faces").EnumerateArray().All(f => f.ValueKind != JsonValueKind.Null));
    }

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
