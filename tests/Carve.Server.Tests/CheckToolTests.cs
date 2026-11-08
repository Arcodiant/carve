using System.Text.Json;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>check through the real server.</summary>
[Trait("Tier", "regression")]
public class CheckToolTests
{
    private static async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        return (carve, await carve.ListToolsAsync());
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return JsonDocument.Parse(CarveProcess.TextOf(result)).RootElement;
    }

    private static string ShedArguments()
    {
        var shed = TestMeshes.LeakyShed();
        return JsonSerializer.Serialize(new { vertices = shed.Vertices.Select(v => new[] { v.X, v.Y, v.Z }), faces = shed.Faces });
    }

    private static void ExpectShed(JsonElement body)
    {
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Equal(
            TestMeshes.LeakyShedProblems,
            body.GetProperty("problems").EnumerateArray().Select(p => (p.GetProperty("check").GetString()!, p.GetProperty("element").GetString()!, p.GetProperty("message").GetString()!)));
        Assert.Equal(TestMeshes.LeakyShedWarnings, body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!));
        Assert.Equal(TestMeshes.LeakyShedNotes, body.GetProperty("notes").EnumerateArray().Select(n => n.GetString()!));

        var stats = body.GetProperty("stats");
        Assert.Equal(
            """{"vertices":25,"faces":18,"edges":38,"triangles":36,"shells":2,"closed":false,"bounds":{"min":[0,0,0],"max":[4,3.9,3]},"size":[4,3.9,3],"area":60.09371484553011,"volume":null}""",
            stats.GetRawText());
    }

    [Fact]
    public async Task Check_on_the_cube_is_the_worked_example_to_the_character()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));

        const string marker = "Output: ";
        var output = Description(tools, "check").Split('\n').Single(l => l.StartsWith(marker, StringComparison.Ordinal))[marker.Length..];
        var result = await carve.CallToolAsync("check", Examples(tools, "check").Single());

        Assert.Equal(output, CarveProcess.TextOf(result));
        Assert.Contains("\"triangles\":12", output);
        Assert.Contains("\"area\":6", output);
        Assert.Contains("\"volume\":1", output);
    }

    [Fact]
    public async Task The_shed_through_define_mesh_gives_the_exact_findings()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        Ok(await carve.CallToolAsync("define_mesh", ShedArguments()));

        ExpectShed(Ok(await carve.CallToolAsync("check", "{}")));
    }

    [Fact]
    public async Task The_shed_opened_from_samples_gives_the_same_findings()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        var path = Path.Combine(Paths.RepoRoot, "samples", "leaky-shed.carve");
        Ok(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path })));

        ExpectShed(Ok(await carve.CallToolAsync("check", "{}")));
    }

    [Fact]
    public async Task Before_define_mesh_it_is_a_tool_error_saying_so()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;

        var result = await carve.CallToolAsync("check", "{}");

        Assert.True(CarveProcess.IsError(result));
        Assert.Contains("define_mesh", CarveProcess.TextOf(result));
    }

    [Fact]
    public async Task Any_argument_is_a_tool_error_and_the_schema_is_closed()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));

        var result = await carve.CallToolAsync("check", """{"deep":true}""");

        Assert.True(CarveProcess.IsError(result));
        Assert.Contains("deep: not an argument of check, which takes none", CarveProcess.TextOf(result));
        var schema = Tool(tools, "check").GetProperty("inputSchema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Empty(schema.GetProperty("properties").EnumerateObject());
    }

    [Fact]
    public async Task Tools_list_has_check_and_its_description_teaches_the_fixes()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;

        var text = Description(tools, "check");

        foreach (var phrase in new[] { "inside-out", "coincident-vertices", "duplicate-face", "invisible", "collision", "z-fight", "reverse", "move", "define_mesh", "no delete yet", "1 unit = 1 m", "y = 0", "Worked example" })
            Assert.Contains(phrase, text);
    }

    [Fact]
    public async Task It_changes_nothing_the_mesh_and_undo_are_as_they_were()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        Ok(await carve.CallToolAsync("move", Examples(tools, "move").Single()));
        var before = CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        Ok(await carve.CallToolAsync("check", "{}"));
        Ok(await carve.CallToolAsync("check", "{}"));

        Assert.Equal(before, CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        // A check that pushed a version would be the step undone here.
        Assert.Equal("move", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
        Assert.True(CarveProcess.IsError(await carve.CallToolAsync("undo", "{}")));
    }
}
