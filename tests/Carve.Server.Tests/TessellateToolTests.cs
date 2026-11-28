using System.Text.Json;
using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.ToolList;

namespace Carve.Server.Tests;

/// <summary>T-237: tessellate through the tools, on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class TessellateToolTests() : TempFolderTests("carve-tessellate")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        try
        {
            await carve.InitializeAsync();
            return (carve, await carve.ListToolsAsync());
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    [Fact]
    public async Task The_example_cuts_the_cubes_top_in_one_step_and_one_undo_takes_it_back()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        var body = Ok(await carve.CallToolAsync("tessellate", Examples(tools, "tessellate").Single()));

        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Empty(body.GetProperty("warnings").EnumerateArray());
        var created = body.GetProperty("created");
        Assert.Equal(8, created.GetProperty("vertices").GetProperty("first").GetInt32());
        Assert.Equal(body.GetProperty("vertices").GetInt32() - 8, created.GetProperty("vertices").GetProperty("count").GetInt32());
        Assert.Equal(6, created.GetProperty("faces").GetProperty("first").GetInt32());
        Assert.Equal(body.GetProperty("faces").GetInt32() - 6, created.GetProperty("faces").GetProperty("count").GetInt32());
        Assert.Equal(1, body.GetProperty("split").GetProperty("count").GetInt32());
        var split = body.GetProperty("split").GetProperty("first").EnumerateArray().Single();
        Assert.Equal(3, split.GetProperty("face").GetInt32());
        var pieces = split.GetProperty("pieces");
        Assert.Equal(1 + created.GetProperty("faces").GetProperty("count").GetInt32(), pieces.GetProperty("count").GetInt32());
        Assert.Equal([3, 6, 7, 8, 9, 10, 11, 12, 13, 14], pieces.GetProperty("first").EnumerateArray().Select(i => i.GetInt32()));

        // The four side faces took three points each on their top edge, and the bottom none.
        var mesh = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_mesh", "{}"))).RootElement;
        var faces = mesh.GetProperty("faces").EnumerateArray().ToArray();
        foreach (var f in new[] { 0, 1, 4, 5 }) Assert.Equal(7, faces[f].GetProperty("vertices").GetArrayLength());
        Assert.Equal(4, faces[2].GetProperty("vertices").GetArrayLength());

        var undone = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("tessellate", undone.GetProperty("undone").GetString());
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
    }

    [Theory]
    [InlineData("""{}""", "max_edge: required but missing")]
    [InlineData("""{"max_edge":"x"}""", "max_edge: expected a number")]
    [InlineData("""{"max_edge":0}""", "max_edge: 0 is not above 0")]
    [InlineData("""{"max_edge":-1}""", "max_edge: -1 is not above 0")]
    [InlineData("""{"max_edge":0.5,"faces":[99]}""", "99")]
    [InlineData("""{"max_edge":0.5,"group":"nope"}""", "nope")]
    [InlineData("""{"max_edge":0.5,"group":"top","faces":[3]}""", "group: give group or faces, not both")]
    [InlineData("""{"max_edge":0.5,"levels":2}""", "levels")]
    [InlineData("""{"max_edge":0.5,"faces":[]}""", "faces: list at least one face")]
    [InlineData("""{"max_edge":0.001,"faces":[3]}""", "over the budget of 200,000")]
    [InlineData("""{"max_edge":1e-14,"faces":[3]}""", "new vertices, over the budget")]
    public async Task Bad_input_is_a_tool_error_that_changes_nothing(string arguments, string says)
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        Ok(await carve.CallToolAsync("define_group", """{"name":"top","faces":[3]}"""));
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        var result = await carve.CallToolAsync("tessellate", arguments);

        Assert.True(IsError(result), TextOf(result));
        Assert.Contains(says, TextOf(result));
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Equal("define_group", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
    }

    /// <summary>RV-101: a whole mesh of 40 faces at summary is within the 2,000-character budget, split short as the filter answered.</summary>
    [Fact]
    public async Task A_whole_mesh_of_40_faces_at_summary_is_within_the_result_budget()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        var vertices = Enumerable.Range(0, 41).SelectMany(i => new[] { $"[{i},0,0]", $"[{i},1,0]" });
        var faces = Enumerable.Range(0, 40).Select(i => $"[{2 * i},{2 * i + 2},{2 * i + 3},{2 * i + 1}]");
        Ok(await carve.CallToolAsync("define_mesh", $$"""{"vertices":[{{string.Join(",", vertices)}}],"faces":[{{string.Join(",", faces)}}]}"""));

        var result = await carve.CallToolAsync("tessellate", """{"max_edge":0.3}""");

        var text = TextOf(result);
        Assert.True(text.Length <= DenseMeshBudgetTests.Budget, $"{text.Length} characters: {text[..Math.Min(600, text.Length)]}");
        var split = Ok(result).GetProperty("split");
        Assert.Equal(40, split.GetProperty("count").GetInt32());
        Assert.Equal(Enumerable.Range(0, 10), split.GetProperty("first").EnumerateArray().Select(s => s.GetProperty("face").GetInt32()));
    }

    /// <summary>M-41's Done-when, the tessellate half, through the tools: the dais's group wear at 0.75.</summary>
    [Fact]
    public async Task The_dais_group_tessellates_closed_with_no_warning_and_only_its_faces_split()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        Ok(await carve.CallToolAsync("define_mesh", TestMeshes.DaisDefineMesh()));
        Ok(await carve.CallToolAsync("define_group", """{"name":"wear","faces":[0,1]}"""));
        var before = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_mesh", "{}"))).RootElement;

        var body = Ok(await carve.CallToolAsync("tessellate", """{"max_edge":0.75,"group":"wear"}"""));

        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Empty(body.GetProperty("warnings").EnumerateArray());
        Assert.Equal([0, 1], body.GetProperty("split").GetProperty("first").EnumerateArray().Select(s => s.GetProperty("face").GetInt32()));
        var after = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_mesh", "{}"))).RootElement;
        var (was, now) = (before.GetProperty("vertices").EnumerateArray().ToArray(), after.GetProperty("vertices").EnumerateArray().ToArray());
        for (var v = 0; v < was.Length; v++) Assert.Equal(was[v].GetRawText(), now[v].GetRawText());
        Assert.Equal(before.GetProperty("faces").GetArrayLength(), after.GetProperty("faces").GetArrayLength() - body.GetProperty("created").GetProperty("faces").GetProperty("count").GetInt32());
        Assert.True(Ok(await carve.CallToolAsync("check", "{}")).GetProperty("warnings").GetArrayLength() == 0);
    }
}
