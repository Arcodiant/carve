using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class ExportToolDefinitionTests() : FileToolTests("carve-export")
{
    /// <summary>export's result: not an error, and one line.</summary>
    private static string Line(JsonElement result)
    {
        var text = Ok(result);
        Assert.DoesNotContain('\n', text);
        return text;
    }

    [Fact]
    public async Task The_descriptions_worked_example_runs_as_written_into_a_game_folder()
    {
        var (carve, tools) = await StartAsync();
        using var _carve = carve;
        var example = Assert.Single(Examples(tools, "export"));
        Assert.Contains("<game>", example);
        Assert.Contains("models.LoadModel(new ContentPath(\"models/crate.glb\"))", HelpToolTests.WithReference(Description(tools, "export"), "export"));

        var game = Path.Combine(Root, "game");
        var line = Line(await carve.CallToolAsync("export", example.Replace("<game>", game.Replace('\\', '/'))));
        var full = Path.Combine(game, "Content", "models", "crate.glb");
        Assert.StartsWith($"exported crate.glb to {full}: 12 triangles", line);
        Assert.True(File.Exists(full));
    }

    [Fact]
    public async Task Tools_list_lists_export_with_a_closed_schema()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _carve = carve;
        var schema = Tool(tools, "export").GetProperty("inputSchema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["path", "allow_unweighted", "limits", "mode", "meshes", "overwrite", "write_limits"], schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Contains("unknown: not an argument of export; its arguments are path, allow_unweighted, limits, mode, meshes, overwrite, write_limits", Refused(await carve.CallToolAsync("export", """{"path":"a.glb","unknown":1}""")));
    }
}
