using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>export through the real server, in a temp working directory.</summary>
[Trait("Tier", "regression")]
public sealed class ExportToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"carve-export-{Guid.NewGuid():N}");

    public ExportToolTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync(bool define = true)
    {
        var carve = new CarveProcess([], workingDirectory: _root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (define) Assert.False(IsError(await carve.CallToolAsync("define_mesh", Cube(tools))));
        return (carve, tools);
    }

    private static JsonElement Tool(JsonElement tools, string name) =>
        tools.EnumerateArray().Single(t => t.GetProperty("name").GetString() == name);

    private static string Description(JsonElement tools, string name) => Tool(tools, name).GetProperty("description").GetString()!;

    private static string Cube(JsonElement tools)
    {
        const string marker = "Worked example (a unit cube): ";
        var text = Description(tools, "define_mesh");
        return text[(text.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
    }

    private static string Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        var text = TextOf(result);
        Assert.DoesNotContain('\n', text);
        return text;
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    [Fact]
    public async Task Export_writes_the_cube_resolving_a_relative_path_and_says_when_it_replaced()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));
        var full = Path.Combine(_root, "out", "cube.glb");

        var first = Ok(await carve.CallToolAsync("export", """{"path":"out/cube.glb"}"""));
        Assert.Equal($"exported cube.glb to {full}: 12 triangles, 1 material 'carve', size 1 x 1 x 1 m, bounds (0, 0, 0) to (1, 1, 1)", first);
        var bytes = File.ReadAllBytes(full);
        Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var second = Ok(await carve.CallToolAsync("export", """{"path":"out/CUBE.GLB"}"""));
        Assert.StartsWith($"exported CUBE.GLB to {Path.Combine(_root, "out", "CUBE.GLB")} (replaced): 12 triangles", second);

        // export changes nothing: the mesh is the same, and undo still steps back over define_mesh.
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Contains("\"undone\":\"define_mesh\"", TextOf(await carve.CallToolAsync("undo", "{}")));
    }

    [Fact]
    public async Task Export_before_define_mesh_is_an_error_saying_so()
    {
        var (carve, _) = await StartAsync(define: false);
        using var _carve = carve;
        Assert.Contains("call define_mesh first", Refused(await carve.CallToolAsync("export", """{"path":"cube.glb"}""")));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task A_wrong_extension_or_an_empty_path_is_an_error_and_writes_nothing()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Assert.Contains("must end in .glb", Refused(await carve.CallToolAsync("export", """{"path":"models/cube.obj"}""")));
        Assert.Contains("path is empty", Refused(await carve.CallToolAsync("export", """{"path":""}""")));
        Assert.Contains("path: required but missing", Refused(await carve.CallToolAsync("export", "{}")));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task A_mesh_with_warnings_exports_and_the_result_repeats_them()
    {
        var (carve, _) = await StartAsync(define: false);
        using var _carve = carve;
        var defined = TextOf(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Assert.DoesNotContain("\"warnings\":[]", defined);

        var line = Ok(await carve.CallToolAsync("export", """{"path":"open.glb"}"""));
        Assert.Contains(": 1 triangles, 1 material 'carve'", line);
        Assert.Contains("1 warning(s): ", line);
        Assert.True(File.Exists(Path.Combine(_root, "open.glb")));
    }

    [Fact]
    public async Task The_descriptions_worked_example_runs_as_written_into_a_game_folder()
    {
        var (carve, tools) = await StartAsync();
        using var _carve = carve;
        var example = Description(tools, "export").Split('\n').Single(l => l.StartsWith("Example: ", StringComparison.Ordinal))["Example: ".Length..];
        Assert.Contains("<game>", example);
        Assert.Contains("models.LoadModel(new ContentPath(\"models/crate.glb\"))", Description(tools, "export"));

        var game = Path.Combine(_root, "game");
        var line = Ok(await carve.CallToolAsync("export", example.Replace("<game>", game.Replace('\\', '/'))));
        var full = Path.Combine(game, "Content", "models", "crate.glb");
        Assert.StartsWith($"exported crate.glb to {full}: 12 triangles", line);
        Assert.True(File.Exists(full));
    }

    [Fact]
    public async Task Tools_list_lists_export_with_a_closed_schema()
    {
        var (carve, tools) = await StartAsync(define: false);
        using var _carve = carve;
        var schema = Tool(tools, "export").GetProperty("inputSchema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["path"], schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Contains("unknown: not an argument of export", Refused(await carve.CallToolAsync("export", """{"path":"a.glb","unknown":1}""")));
    }
}
