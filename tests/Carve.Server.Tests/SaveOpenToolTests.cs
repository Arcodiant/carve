using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Carve.Core.Tests;
using Empyrean;
using Microsoft.Extensions.DependencyInjection;

namespace Carve.Server.Tests;

/// <summary>save and open through the real server, in a temp working directory.</summary>
[Trait("Tier", "regression")]
public sealed class SaveOpenToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"carve-saveopen-{Guid.NewGuid():N}");

    public SaveOpenToolTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync(string? mesh = "cube")
    {
        var carve = new CarveProcess([], workingDirectory: _root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (mesh is not null) Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh == "cube" ? Cube(tools) : mesh)));
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

    private static string Example(JsonElement tools, string name, string marker = "Example: ") =>
        Description(tools, name).Split('\n').First(l => l.StartsWith(marker, StringComparison.Ordinal))[marker.Length..];

    private static string Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static string LBlockJson()
    {
        var mesh = TestMeshes.LBlock();
        static string N(float x) => x.ToString("R", CultureInfo.InvariantCulture);
        var vertices = string.Join(",", mesh.Vertices.Select(v => $"[{N(v.X)},{N(v.Y)},{N(v.Z)}]"));
        var faces = string.Join(",", mesh.Faces.Select(f => "[" + string.Join(",", f) + "]"));
        return $$"""{"vertices":[{{vertices}}],"faces":[{{faces}}]}""";
    }

    private static async Task Crate(CarveProcess carve)
    {
        Ok(await carve.CallToolAsync("extrude", """{"faces":[1],"distance":-0.2}"""));
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
    }

    [Theory]
    [InlineData("crate")]
    [InlineData("lblock")]
    public async Task Open_brings_back_the_saved_mesh_bit_for_bit_in_one_undoable_step(string which)
    {
        var (carve, _) = await StartAsync(which == "crate" ? "cube" : LBlockJson());
        using var _carve = carve;
        if (which == "crate") await Crate(carve);
        var saved = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        var line = Ok(await carve.CallToolAsync("save", """{"path":"out/mesh.carve"}"""));
        var full = Path.Combine(_root, "out", "mesh.carve");
        var counts = JsonDocument.Parse(saved).RootElement;
        Assert.Equal($"saved mesh.carve to {full}: {counts.GetProperty("vertices").GetArrayLength()} vertices, {counts.GetProperty("faces").GetArrayLength()} faces", line);
        Assert.Equal(saved, TextOf(await carve.CallToolAsync("get_mesh", "{}"))); // save changed nothing

        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[2,0,0],[0,2,0]],"faces":[[0,1,2]]}"""));
        Assert.NotEqual(saved, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        var other = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        var opened = JsonDocument.Parse(Ok(await carve.CallToolAsync("open", """{"path":"out/mesh.carve"}"""))).RootElement;
        Assert.Equal(full, opened.GetProperty("path").GetString());
        Assert.Equal("path", opened.EnumerateObject().First().Name);
        Assert.Equal(counts.GetProperty("vertices").GetArrayLength(), opened.GetProperty("vertices").GetInt32());
        Assert.Equal(saved, TextOf(await carve.CallToolAsync("get_mesh", "{}")));

        // One undo takes back the open, to the mesh before it.
        Assert.Contains("\"undone\":\"open\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(other, TextOf(await carve.CallToolAsync("get_mesh", "{}")));

        // Saving the reopened mesh gives the same bytes.
        Ok(await carve.CallToolAsync("open", """{"path":"out/mesh.carve"}"""));
        var first = File.ReadAllBytes(full);
        var again = Ok(await carve.CallToolAsync("save", """{"path":"out/MESH.CARVE"}"""));
        Assert.Contains(" (replaced)", again.Replace("MESH.CARVE", "x")); // same file on a case-insensitive disk, else a new one
        Assert.Equal(first, File.ReadAllBytes(Path.Combine(_root, "out", "MESH.CARVE")));
    }

    [Fact]
    public async Task A_later_session_opens_what_an_earlier_process_saved()
    {
        string saved;
        var (first, _) = await StartAsync();
        using (first)
        {
            await Crate(first);
            saved = TextOf(await first.CallToolAsync("get_mesh", "{}"));
            Ok(await first.CallToolAsync("save", """{"path":"crate.carve"}"""));
        }

        var (second, _) = await StartAsync(mesh: null);
        using var _second = second;
        Assert.Contains("call define_mesh first", Refused(await second.CallToolAsync("get_mesh", "{}")));
        Ok(await second.CallToolAsync("open", """{"path":"crate.carve"}"""));
        Assert.Equal(saved, TextOf(await second.CallToolAsync("get_mesh", "{}")));
        // The ids still mean the same elements: the same move lands on the same face.
        Ok(await second.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
    }

    [Fact]
    public async Task Save_before_define_mesh_a_replaced_file_and_a_wrong_extension()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        Assert.Contains("save: there is no mesh yet; call define_mesh first", Refused(await carve.CallToolAsync("save", """{"path":"a.carve"}""")));
        Assert.Empty(Directory.GetFileSystemEntries(_root));

        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Assert.DoesNotContain("(replaced)", Ok(await carve.CallToolAsync("save", """{"path":"a.carve"}""")));
        Assert.Contains("(replaced)", Ok(await carve.CallToolAsync("save", """{"path":"a.carve"}""")));

        // A later save writes the mesh as it is then, not the one before.
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[2,0,0],[0,2,0],[2,2,0]],"faces":[[0,1,3,2]]}"""));
        var square = TextOf(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Contains("(replaced)", Ok(await carve.CallToolAsync("save", """{"path":"a.carve"}""")));
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Ok(await carve.CallToolAsync("open", """{"path":"a.carve"}"""));
        Assert.Equal(square, TextOf(await carve.CallToolAsync("get_mesh", "{}")));

        Assert.Contains("must end in .carve", Refused(await carve.CallToolAsync("save", """{"path":"a.glb"}""")));
        Assert.Contains("must end in .carve", Refused(await carve.CallToolAsync("save", """{"path":"a"}""")));
        Assert.Contains("open: path \"a.glb\" ends in \".glb\"; open reads .carve files, so the path must end in .carve",
            Refused(await carve.CallToolAsync("open", """{"path":"a.glb"}""")));
        Assert.Contains("path is empty", Refused(await carve.CallToolAsync("open", """{"path":""}""")));
        Assert.Equal(["a.carve"], Directory.GetFileSystemEntries(_root).Select(f => Path.GetFileName(f)!).ToArray());
    }

    [Fact]
    public async Task A_missing_file_names_the_path_and_lists_the_carve_files_beside_it()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
        var folder = Path.Combine(_root, "models");
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        Assert.Equal($"open: no file at {Path.Combine(folder, "crate.carve")}; the folder {folder} does not exist",
            Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}""")));

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "x");
        Assert.Equal($"open: no file at {Path.Combine(folder, "crate.carve")}; there are no .carve files in {folder}",
            Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}""")));

        Ok(await carve.CallToolAsync("save", """{"path":"models/b.carve"}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"models/a.carve"}"""));
        Assert.Equal($"open: no file at {Path.Combine(folder, "crate.carve")}; the .carve files in {folder} (2) are: a.carve, b.carve",
            Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}""")));

        for (var i = 0; i < 23; i++) File.WriteAllText(Path.Combine(folder, $"m{i:00}.carve"), "x");
        var many = Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}"""));
        Assert.Contains("(25) are: a.carve, b.carve, m00.carve", many);
        Assert.EndsWith("m17.carve, and 5 more", many);

        await NothingChanged(carve, before);
    }

    [Fact]
    public async Task A_file_that_cannot_be_opened_changes_nothing()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        File.WriteAllText(Path.Combine(_root, "bad.carve"), "{\n  \"carve\": 1,\n  \"vertices\": [[0, 0, 0],,\n");
        var bad = Refused(await carve.CallToolAsync("open", """{"path":"bad.carve"}"""));
        Assert.Contains("file: the text is not JSON: at line 3, column ", bad);
        await NothingChanged(carve, before);

        File.WriteAllText(Path.Combine(_root, "future.carve"), """{"carve": 9, "vertices": [], "faces": []}""");
        Assert.Contains("carve: version 9 is not one this build reads; it reads version 1", Refused(await carve.CallToolAsync("open", """{"path":"future.carve"}""")));
        await NothingChanged(carve, before);

        File.WriteAllText(Path.Combine(_root, "face.carve"), """{"carve": 1, "vertices": [[0,0,0],[1,0,0],[0,1,0]], "faces": [[0,1,5]]}""");
        var face = Refused(await carve.CallToolAsync("open", """{"path":"face.carve"}"""));
        Assert.Contains("faces[0][2]: index 5 is out of range 0..2", face);
        Assert.Contains("the current mesh is unchanged", face);
        await NothingChanged(carve, before);

        Directory.CreateDirectory(Path.Combine(_root, "dir.carve"));
        Assert.Contains("is a folder", Refused(await carve.CallToolAsync("open", """{"path":"dir.carve"}""")));
        await NothingChanged(carve, before);
    }

    /// <summary>The mesh is as it was, and undo steps back over the move before it: no step was added.</summary>
    private static async Task NothingChanged(CarveProcess carve, string mesh)
    {
        Assert.Equal(mesh, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Contains("\"undone\":\"move\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        // Put the move back so the next check starts from the same state.
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
        Assert.Equal(mesh, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
    }

    [Fact]
    public async Task The_descriptions_worked_example_runs_as_written_from_an_empty_file_to_a_game_folder()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _carve = carve;
        var save = Description(tools, "save");
        Assert.Contains("models.LoadModel(new ContentPath(\"models/crate.glb\"))", save);
        Assert.Contains("\"carve\": 1,", save); // the cube's file text
        Assert.Contains("save's description", Description(tools, "export"));
        Assert.Contains("save's description", Description(tools, "open"));

        var steps = Regex.Matches(save, @"^\d\. (?:In a later session, )?(\w+) (\{.*\})$", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        Assert.Equal(["save", "open", "export"], steps.Keys.ToArray());
        Assert.Equal(steps["open"], Example(tools, "open"));

        var game = Path.Combine(_root, "game").Replace('\\', '/');
        // 1 and 2: the cube from its description, then an edit.
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        await Crate(carve);
        // 3: save; 4: open (as a later session would, after a different mesh was current).
        Assert.StartsWith("saved crate.carve to ", Ok(await carve.CallToolAsync("save", steps["save"])));
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Ok(await carve.CallToolAsync("open", steps["open"]));
        // 5: export into the game's content folder.
        var line = Ok(await carve.CallToolAsync("export", steps["export"].Replace("<game>", game)));
        var glb = Path.Combine(_root, "game", "Content", "models", "crate.glb");
        Assert.StartsWith($"exported crate.glb to {glb.Replace('/', Path.DirectorySeparatorChar)}: ", line);
        Assert.True(File.Exists(Path.Combine(_root, "crate.carve")));
        GlbParse.AssertReadsBack(File.ReadAllBytes(glb));
    }

    [Fact]
    public async Task Tools_list_lists_save_and_open_with_closed_schemas()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _carve = carve;
        foreach (var name in new[] { "save", "open" })
        {
            var schema = Tool(tools, name).GetProperty("inputSchema");
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(["path"], schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Contains($"unknown: not an argument of {name}", Refused(await carve.CallToolAsync(name, """{"path":"a.carve","unknown":1}""")));
            Assert.Contains("path: required but missing", Refused(await carve.CallToolAsync(name, "{}")));
        }
    }
}

/// <summary>Reads a .glb back with the engine's own parser (as <c>GlbExportTests</c> does) and checks it holds triangles.</summary>
internal static class GlbParse
{
    private static readonly IModelGeometryLoader Parser = Build();

    private static IModelGeometryLoader Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmpyreanCore();
        services.AddEmpyreanGeometry();
        return services.BuildServiceProvider().GetRequiredService<IModelGeometryLoader>();
    }

    public static void AssertReadsBack(byte[] bytes)
    {
        Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.NotNull(Parser.Parse(bytes, ModelFormat.Gltf));
    }
}
