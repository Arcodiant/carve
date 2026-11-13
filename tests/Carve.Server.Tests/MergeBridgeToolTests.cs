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
        Assert.Equal([7], Ints(body.GetProperty("deleted").GetProperty("faces")));
        Assert.Empty(Ints(body.GetProperty("deleted").GetProperty("vertices")));
        Assert.Equal(9, body.GetProperty("faces").GetInt32());
        Assert.Equal(12, body.GetProperty("vertices").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Empty(body.GetProperty("warnings").EnumerateArray());

        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var faces = mesh.GetProperty("faces").EnumerateArray().ToArray();
        Assert.Equal(JsonValueKind.Null, faces[7].ValueKind);
        Assert.Equal([4, 5, 11, 6, 7, 10], Ints(faces[1].GetProperty("vertices")));
        await CheckedClean(carve, closed: true);

        // Refusals name the problem and leave the mesh; one undo puts the two pieces back.
        Refused(await carve.CallToolAsync("merge_faces", """{"faces":[0]}"""), "at least two faces");
        Refused(await carve.CallToolAsync("merge_faces", """{"faces":[0,1]}"""), "must be connected");
        Refused(await carve.CallToolAsync("merge_faces", """{"faces":[1,7]}"""), "faces[7]: deleted");
        Refused(await carve.CallToolAsync("merge_faces", "{}"), "faces: required but missing");
        Ok(await carve.CallToolAsync("undo", "{}"));
        mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal([11, 6, 7, 10], Ints(mesh.GetProperty("faces")[7].GetProperty("vertices")));
        Assert.Equal(10, mesh.GetProperty("faces").EnumerateArray().Count(f => f.ValueKind != JsonValueKind.Null));
    }

    [Fact]
    public async Task A_merge_of_another_colour_is_a_warning_naming_it()
    {
        var (carve, _) = await StartAsync("merge_faces");
        using var __ = carve;
        Ok(await carve.CallToolAsync("set_colour", """{"faces":[7],"colour":"#3C78C8"}"""));
        var body = Ok(await carve.CallToolAsync("merge_faces", """{"faces":[7,1]}"""));
        var warning = Assert.Single(body.GetProperty("warnings").EnumerateArray()).GetString()!;
        Assert.StartsWith("faces[7]: ", warning, StringComparison.Ordinal);
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

    /// <summary>The README's <c>Cutting</c> example for <paramref name="tool"/>: the last inline JSON on its bullet.</summary>
    private static string ReadmeExample(string tool)
    {
        var lines = File.ReadAllLines(Path.Combine(Paths.RepoRoot, "README.md"));
        var section = lines.SkipWhile(l => l != "## Cutting").Skip(1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal));
        var bullet = section.Single(l => l.StartsWith($"- `{tool}` ", StringComparison.Ordinal));
        var end = bullet.LastIndexOf("}`", StringComparison.Ordinal);
        var start = bullet.LastIndexOf("`{", end, StringComparison.Ordinal);
        return bullet[(start + 1)..(end + 1)];
    }

    /// <summary>define_mesh's unit cube, from its description's worked example line.</summary>
    private static string CubeInput(JsonElement tools)
    {
        const string marker = "Worked example (a unit cube): ";
        var description = ToolList.Description(tools, "define_mesh");
        return description[(description.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..].Split('\n')[0];
    }

    [Fact]
    public async Task Each_README_cutting_example_works_on_the_input_it_names()
    {
        // RV-31: each on a fresh define_mesh unit cube, merge_faces after the loop_cut, bridge on bridge's own two squares.
        foreach (var tool in new[] { "plane_cut", "loop_cut", "knife", "delete" })
        {
            using var carve = new CarveProcess([], workingDirectory: Root);
            await carve.InitializeAsync();
            var cube = CubeInput(await carve.ListToolsAsync());
            Ok(await carve.CallToolAsync("define_mesh", cube));
            Ok(await carve.CallToolAsync(tool, ReadmeExample(tool)));
        }

        using (var carve = new CarveProcess([], workingDirectory: Root))
        {
            await carve.InitializeAsync();
            var cube = CubeInput(await carve.ListToolsAsync());
            Ok(await carve.CallToolAsync("define_mesh", cube));
            Ok(await carve.CallToolAsync("loop_cut", ReadmeExample("loop_cut")));
            var merged = Ok(await carve.CallToolAsync("merge_faces", ReadmeExample("merge_faces")));
            Assert.Equal([7], Ints(merged.GetProperty("deleted").GetProperty("faces")));
            Assert.Empty(merged.GetProperty("warnings").EnumerateArray());
        }

        var (bridging, _) = await StartAsync("bridge");
        using (bridging)
        {
            var box = Ok(await bridging.CallToolAsync("bridge", ReadmeExample("bridge")));
            Assert.True(box.GetProperty("closed").GetBoolean());
        }
    }

    [Fact]
    public async Task The_merge_examples_input_is_what_loop_cuts_own_example_leaves_of_the_cube()
    {
        // RV-31: the description says its define_mesh input is the cube as loop_cut leaves it; it must be, id for id.
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", CubeInput(tools)));
        Ok(await carve.CallToolAsync("loop_cut", ToolList.Examples(tools, "loop_cut").Single()));
        var cut = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var input = JsonDocument.Parse(Input(tools, "merge_faces")).RootElement;

        Assert.Equal(
            input.GetProperty("vertices").EnumerateArray().Select(v => string.Join(",", v.EnumerateArray().Select(c => c.GetDouble()))).ToArray(),
            cut.GetProperty("vertices").EnumerateArray().Select(v => string.Join(",", v.GetProperty("at").EnumerateArray().Select(c => c.GetDouble()))).ToArray());
        Assert.Equal(
            input.GetProperty("faces").EnumerateArray().Select(f => string.Join(",", f.EnumerateArray().Select(i => i.GetInt32()))).ToArray(),
            cut.GetProperty("faces").EnumerateArray().Select(f => string.Join(",", Ints(f.GetProperty("vertices")))).ToArray());
    }
}
