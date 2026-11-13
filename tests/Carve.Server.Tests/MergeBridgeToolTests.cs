using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-68: <c>merge_faces</c> and <c>bridge</c> through the client, each worked example then <c>check</c>, and one undo step each.</summary>
[Trait("Tier", "regression")]
public sealed class MergeBridgeToolTests() : TempFolderTests("carve-merge-bridge")
{
    private const string DefineMarker = "define_mesh input: ";

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static void Refused(JsonElement result, string mention)
    {
        Assert.True(IsError(result), TextOf(result));
        Assert.True(TextOf(result).Contains(mention, StringComparison.Ordinal), TextOf(result));
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    private static string Input(JsonElement tools, string name) =>
        ToolList.Description(tools, name).Split('\n').Single(l => l.StartsWith(DefineMarker, StringComparison.Ordinal))[DefineMarker.Length..];

    private async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync(string tool)
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", Input(tools, tool)));
        return (carve, tools);
    }

    private static async Task CheckedClean(CarveProcess carve, bool closed, params string[] problems)
    {
        var check = Ok(await carve.CallToolAsync("check", "{}"));
        Assert.Equal(problems, check.GetProperty("problems").EnumerateArray()
            .Select(p => p.GetProperty("check").GetString() + " " + p.GetProperty("element").GetString()).ToArray());
        Assert.Equal(closed, check.GetProperty("stats").GetProperty("closed").GetBoolean());
    }

    [Fact]
    public async Task The_merge_example_puts_the_cut_cubes_front_back_together_in_one_undo_step()
    {
        var (carve, tools) = await StartAsync("merge_faces");
        using var _ = carve;

        var body = Ok(await carve.CallToolAsync("merge_faces", ToolList.Examples(tools, "merge_faces").Single()));
        Assert.Equal([3], Ints(body.GetProperty("deleted").GetProperty("faces")));
        Assert.Empty(Ints(body.GetProperty("deleted").GetProperty("vertices")));
        Assert.Equal(9, body.GetProperty("faces").GetInt32());
        Assert.Equal(12, body.GetProperty("vertices").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Empty(body.GetProperty("warnings").EnumerateArray());

        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var faces = mesh.GetProperty("faces").EnumerateArray().ToArray();
        Assert.Equal(JsonValueKind.Null, faces[3].ValueKind);
        Assert.Equal([3, 2, 6, 10, 11, 7], Ints(faces[2].GetProperty("vertices")));
        await CheckedClean(carve, closed: true);

        // Refusals name the problem and leave the mesh; one undo puts the two pieces back.
        Refused(await carve.CallToolAsync("merge_faces", """{"faces":[0]}"""), "at least two faces");
        Refused(await carve.CallToolAsync("merge_faces", """{"faces":[0,1]}"""), "must be connected");
        Refused(await carve.CallToolAsync("merge_faces", """{"faces":[2,3]}"""), "faces[3]: deleted");
        Refused(await carve.CallToolAsync("merge_faces", "{}"), "faces: required but missing");
        Ok(await carve.CallToolAsync("undo", "{}"));
        mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal([7, 6, 10, 11], Ints(mesh.GetProperty("faces")[3].GetProperty("vertices")));
        Assert.Equal(10, mesh.GetProperty("faces").EnumerateArray().Count(f => f.ValueKind != JsonValueKind.Null));
    }

    [Fact]
    public async Task A_merge_of_another_colour_is_a_warning_naming_it()
    {
        var (carve, _) = await StartAsync("merge_faces");
        using var __ = carve;
        Ok(await carve.CallToolAsync("set_colour", """{"faces":[3],"colour":"#3C78C8"}"""));
        var body = Ok(await carve.CallToolAsync("merge_faces", """{"faces":[3,2]}"""));
        var warning = Assert.Single(body.GetProperty("warnings").EnumerateArray()).GetString()!;
        Assert.StartsWith("faces[3]: ", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_bridge_example_closes_the_box_in_one_undo_step()
    {
        var (carve, tools) = await StartAsync("bridge");
        using var _ = carve;

        var body = Ok(await carve.CallToolAsync("bridge", ToolList.Examples(tools, "bridge").Single()));
        Assert.Equal([2, 3, 4, 5], Ints(body.GetProperty("created").GetProperty("faces")));
        Assert.Empty(Ints(body.GetProperty("created").GetProperty("vertices")));
        Assert.Equal(6, body.GetProperty("faces").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        // The squares face each other, so the walls, wound to agree with them, face in too: check finds the closed box
        // inside-out, and nothing else.
        await CheckedClean(carve, closed: true, "inside-out shells[0]");

        // Any listing of the second loop gives the same faces.
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("faces").GetRawText();
        Ok(await carve.CallToolAsync("undo", "{}"));
        Ok(await carve.CallToolAsync("bridge", """{"loops":[[0,1,2,3],[6,5,4,7]]}"""));
        Assert.Equal(mesh, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("faces").GetRawText());

        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(2, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("faces").GetArrayLength());
        Refused(await carve.CallToolAsync("bridge", """{"loops":[[0,1,2,3],[4,5,6]]}"""), "4 and 3");
        Refused(await carve.CallToolAsync("bridge", """{"loops":[[0,1,2,3],[0,1,2,3]]}"""), "in both loops");
        Refused(await carve.CallToolAsync("bridge", """{"loops":[[0,1,2,3]]}"""), "expected two loops, got 1");
        Refused(await carve.CallToolAsync("bridge", """{"loops":[[0,2,1,3],[4,5,6,7]]}"""), "not joined by an edge");
        // A loop that is not an array is one problem, named once with what was sent.
        var notArray = await carve.CallToolAsync("bridge", """{"loops":[[0,1,2],5]}""");
        Refused(notArray, "loops[1]: expected an array of integer ids, got 5");
        Assert.DoesNotContain("got null", TextOf(notArray));
    }
}
