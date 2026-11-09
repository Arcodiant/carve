using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>save and open through the real server, in a temp working directory.</summary>
[Trait("Tier", "regression")]
public sealed class SaveOpenToolTests() : FileToolTests("carve-saveopen")
{
    /// <summary>define_mesh's arguments for a mesh, every coordinate in the shortest text that reads back to the same float.</summary>
    private static string MeshJson(Carve.Core.PolygonMesh mesh)
    {
        static string N(float x) => x.ToString("R", CultureInfo.InvariantCulture);
        var vertices = string.Join(",", mesh.Vertices.Select(v => $"[{N(v.X)},{N(v.Y)},{N(v.Z)}]"));
        var faces = string.Join(",", mesh.Faces.Select(f => "[" + string.Join(",", f) + "]"));
        return $$"""{"vertices":[{{vertices}}],"faces":[{{faces}}]}""";
    }

    private static async Task ExtrudeAndRaise(CarveProcess carve)
    {
        Ok(await carve.CallToolAsync("extrude", """{"faces":[1],"distance":-0.2}"""));
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
    }

    [Theory]
    [InlineData("crate")]
    [InlineData("lblock")]
    public async Task Open_brings_back_the_saved_mesh_bit_for_bit_in_one_undoable_step(string which)
    {
        var (carve, _) = await StartAsync(MeshJson(which == "crate" ? TestMeshes.Crate() : TestMeshes.LBlock()));
        using var _carve = carve;
        var saved = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        var line = Ok(await carve.CallToolAsync("save", """{"path":"out/mesh.carve"}"""));
        var full = Path.Combine(Root, "out", "mesh.carve");
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
        Assert.Equal(first, File.ReadAllBytes(Path.Combine(Root, "out", "MESH.CARVE")));
    }

    [Fact]
    public async Task A_later_session_opens_what_an_earlier_process_saved()
    {
        string saved, movedAfterSave;
        var (first, _) = await StartAsync();
        using (first)
        {
            await ExtrudeAndRaise(first);
            saved = TextOf(await first.CallToolAsync("get_mesh", "{}"));
            Ok(await first.CallToolAsync("save", """{"path":"crate.carve"}"""));

            // The same move on the live mesh, to compare with the one made on the reopened mesh.
            Ok(await first.CallToolAsync("move", """{"faces":[3],"by":[0,0.25,0]}"""));
            movedAfterSave = TextOf(await first.CallToolAsync("get_mesh", "{}"));
        }

        var (second, _) = await StartAsync(mesh: null);
        using var _second = second;
        Assert.Contains("call define_mesh first", Refused(await second.CallToolAsync("get_mesh", "{}")));
        Ok(await second.CallToolAsync("open", """{"path":"crate.carve"}"""));
        Assert.Equal(saved, TextOf(await second.CallToolAsync("get_mesh", "{}")));
        // The ids still mean the same elements: the same move lands on the same face, so the whole mesh comes out the same.
        Ok(await second.CallToolAsync("move", """{"faces":[3],"by":[0,0.25,0]}"""));
        Assert.Equal(movedAfterSave, TextOf(await second.CallToolAsync("get_mesh", "{}")));
    }

    [Fact]
    public async Task Undo_straight_after_the_first_open_of_a_fresh_process_is_a_tool_error_and_the_mesh_stays()
    {
        var (first, _) = await StartAsync();
        using (first)
        {
            await ExtrudeAndRaise(first);
            Ok(await first.CallToolAsync("save", """{"path":"crate.carve"}"""));
        }

        var (second, _) = await StartAsync(mesh: null);
        using var _second = second;
        Ok(await second.CallToolAsync("open", """{"path":"crate.carve"}"""));
        var opened = TextOf(await second.CallToolAsync("get_mesh", "{}"));

        Assert.Contains("nothing to undo", Refused(await second.CallToolAsync("undo", "{}")));
        Assert.Equal(opened, TextOf(await second.CallToolAsync("get_mesh", "{}")));
    }

    [Fact]
    public async Task Save_before_define_mesh_a_replaced_file_and_a_wrong_extension()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        Assert.Contains("save: there is no mesh yet; call define_mesh first", Refused(await carve.CallToolAsync("save", """{"path":"a.carve"}""")));
        Assert.Empty(Directory.GetFileSystemEntries(Root));

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
        Assert.Equal(["a.carve"], Directory.GetFileSystemEntries(Root).Select(f => Path.GetFileName(f)!).ToArray());
    }

    [Fact]
    public async Task A_missing_file_names_the_path_and_lists_the_carve_files_beside_it()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
        var folder = Path.Combine(Root, "models");
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        Assert.Equal($"open: no file at {Path.Combine(folder, "crate.carve")}; the folder {folder} does not exist",
            Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}""")));

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "x");
        Assert.Equal($"open: no file at {Path.Combine(folder, "crate.carve")}; there are no .carve files in {folder}",
            Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}""")));

        // A name that starts with a dot is a .carve file too: save accepts it, so the listing shows it.
        File.WriteAllText(Path.Combine(folder, ".x.carve"), "x");
        Ok(await carve.CallToolAsync("save", """{"path":"models/b.carve"}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"models/a.carve"}"""));
        Assert.Equal($"open: no file at {Path.Combine(folder, "crate.carve")}; the .carve files in {folder} (3) are: .x.carve, a.carve, b.carve",
            Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}""")));

        for (var i = 0; i < 23; i++) File.WriteAllText(Path.Combine(folder, $"m{i:00}.carve"), "x");
        var many = Refused(await carve.CallToolAsync("open", """{"path":"models/crate.carve"}"""));
        Assert.Contains("(26) are: .x.carve, a.carve, b.carve, m00.carve", many);
        Assert.EndsWith("m16.carve, and 6 more", many);

        await NothingChanged(carve, before);
    }

    [Fact]
    public async Task A_file_that_cannot_be_opened_changes_nothing()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));

        File.WriteAllText(Path.Combine(Root, "bad.carve"), "{\n  \"carve\": 1,\n  \"vertices\": [[0, 0, 0],,\n");
        var bad = Refused(await carve.CallToolAsync("open", """{"path":"bad.carve"}"""));
        Assert.Contains("file: the text is not JSON: at line 3, column ", bad);
        await NothingChanged(carve, before);

        File.WriteAllText(Path.Combine(Root, "future.carve"), """{"carve": 9, "vertices": [], "faces": []}""");
        Assert.Contains("carve: version 9 is not one this build reads; it reads versions 1 and 2", Refused(await carve.CallToolAsync("open", """{"path":"future.carve"}""")));
        await NothingChanged(carve, before);

        File.WriteAllText(Path.Combine(Root, "face.carve"), """{"carve": 1, "vertices": [[0,0,0],[1,0,0],[0,1,0]], "faces": [[0,1,5]]}""");
        var face = Refused(await carve.CallToolAsync("open", """{"path":"face.carve"}"""));
        Assert.Contains("faces[0][2]: index 5 is out of range 0..2", face);
        Assert.Contains("the model is unchanged", face);
        await NothingChanged(carve, before);

        Directory.CreateDirectory(Path.Combine(Root, "dir.carve"));
        Assert.Contains("is a folder", Refused(await carve.CallToolAsync("open", """{"path":"dir.carve"}""")));
        await NothingChanged(carve, before);

        // A lone surrogate escape in a key is a refusal naming the key's place, not an error that leaves the tool.
        File.WriteAllText(Path.Combine(Root, "surrogate.carve"), "{\"\\ud800\": 1}");
        var surrogate = Refused(await carve.CallToolAsync("open", """{"path":"surrogate.carve"}"""));
        Assert.Contains("keys[0]: the key is not valid text", surrogate);
        await NothingChanged(carve, before);

        // A file that is held open by another process cannot be read.
        var locked = Path.Combine(Root, "locked.carve");
        File.WriteAllText(locked, CarveFile.Write(TestMeshes.Cube()));
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var refused = Refused(await carve.CallToolAsync("open", """{"path":"locked.carve"}"""));
            Assert.Contains($"open: could not read {locked}:", refused);
        }

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
        // The cube's file text is what the writer gives, with LF line ends on any checkout.
        Assert.Contains(CarveFile.Write(TestMeshes.Cube()), save);
        Assert.DoesNotContain(Convert.ToChar(13), save);
        Assert.Contains("save's description", Description(tools, "export"));
        Assert.Contains("save's description", Description(tools, "open"));

        var steps = Regex.Matches(save, @"^\d\. (?:In a later session, )?(\w+) (\{.*\})$", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        Assert.Equal(["save", "open", "export"], steps.Keys.ToArray());
        Assert.Equal(steps["open"], Assert.Single(Examples(tools, "open")));

        var game = Path.Combine(Root, "game").Replace('\\', '/');
        // 1 and 2: the cube from its description, then an edit.
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        await ExtrudeAndRaise(carve);
        // 3: save; 4: open (as a later session would, after a different mesh was current).
        Assert.StartsWith("saved crate.carve to ", Ok(await carve.CallToolAsync("save", steps["save"])));
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Ok(await carve.CallToolAsync("open", steps["open"]));
        // 5: export into the game's content folder.
        var line = Ok(await carve.CallToolAsync("export", steps["export"].Replace("<game>", game)));
        var glb = Path.Combine(Root, "game", "Content", "models", "crate.glb");
        Assert.StartsWith($"exported crate.glb to {glb.Replace('/', Path.DirectorySeparatorChar)}: ", line);
        Assert.True(File.Exists(Path.Combine(Root, "crate.carve")));
        var bytes = File.ReadAllBytes(glb);
        Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.NotNull(GlbExportTests.Parser.Parse(bytes, Empyrean.ModelFormat.Gltf));
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
            Assert.Equal(["path", "mesh"], schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Contains($"unknown: not an argument of {name}", Refused(await carve.CallToolAsync(name, """{"path":"a.carve","unknown":1}""")));
            Assert.Contains("path: required but missing", Refused(await carve.CallToolAsync(name, "{}")));
        }
    }
}
