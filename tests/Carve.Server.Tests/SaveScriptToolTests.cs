extern alias carve;
using System.Text.Json;
using Carve.Core.Scripts;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.VerbosityCalls;

namespace Carve.Server.Tests;

/// <summary>T-232: save_script writes the session's journal as a .carvescript, file arguments relative to the script.</summary>
[Trait("Tier", "unit")]
public sealed class SaveScriptToolTests() : TempFolderTests("carve-save-script")
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private static string Fixture { get; } = Path.Combine(Paths.RepoRoot, "tests", "Carve.Server.Tests", "Fixtures", "swatch.png");

    private async Task<ToolHarness> Start(params string[] setup) => await VerbosityToolTests.Start(Root, setup);

    private string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static async Task<string> Say(ToolHarness carve, string tool, string arguments, bool refused = false)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        Assert.Equal(refused, IsError(result));
        return Text(result);
    }

    [Fact]
    public async Task The_worked_example_writes_the_file_it_quotes()
    {
        await using var carve = await Start("define_mesh " + Cube);
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "save_script").GetProperty("description").GetString()!;
        Assert.Contains(carve::Carve.Server.SaveScriptTool.WorkedFile, description);
        Assert.EndsWith("Example: " + carve::Carve.Server.SaveScriptTool.WorkedCall + "\nMore: help {\"topic\":\"scripts\"} gives the statement format, the path rule and what is written.", description);
        await Say(carve, "extrude", carve::Carve.Server.SaveScriptTool.WorkedExtrude);
        var result = await Say(carve, "save_script", carve::Carve.Server.SaveScriptTool.WorkedCall);
        Assert.Contains("2 statements, " + carve::Carve.Server.SaveScriptTool.WorkedFile.Count(c => c == '\n') + " lines", result);
        Assert.Equal(carve::Carve.Server.SaveScriptTool.WorkedFile, Read("cube.carvescript"));
        Assert.DoesNotContain("\r", Read("cube.carvescript"));
        Assert.Contains(carve::Carve.Server.SaveScriptTool.WorkedFile, carve::Carve.Server.HelpTool.Topic("scripts"));
    }

    [Fact]
    public async Task A_session_of_every_family_of_edit_reads_back_to_the_journals_entries()
    {
        string[] session =
        [
            "define_mesh " + Cube, """loop_cut {"edge":[0,3],"at":[0.5]}""", """extrude {"faces":[1],"distance":0.2}""", """move {"faces":[3],"by":[0,0.5,0]}""",
            """define_group {"name":"g","faces":[0,1]}""", """set_colour {"colour":"#C8503C","faces":[1]}""", """define_material {"name":"m","roughness":0.25}""",
            """set_material {"material":"m","faces":[2]}""", """set_shading {"shading":"smooth"}""", """mark_edges {"edges":[[0,1]]}""",
            """project_uvs {"method":"box"}""", """define_skeleton {"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]}]}""",
            """set_weights {"vertices":[0,1,2,3],"weights":{"base":1}}""", """define_part {"part":"p","translation":[0,1,0]}""", """subdivide {"levels":1,"verbosity":"silent"}""",
        ];
        await using var carve = await Start(session);
        await Say(carve, "save_script", """{"path":"all.carvescript"}""");
        var read = ScriptReader.Read(Read("all.carvescript"));
        Assert.Null(read.Error);
        var statements = read.Script!.Statements;
        Assert.Equal(session.Length, carve.Store.Journal.Count);
        Assert.Equal(carve.Store.Journal.Count, statements.Count);
        for (var i = 0; i < statements.Count; i++)
        {
            var entry = carve.Store.Journal[i];
            Assert.Equal(entry.Tool, statements[i].Tool);
            Assert.Equal(entry.Arguments.Select(a => a.Key), statements[i].Arguments.Select(a => a.Name));
            Assert.Equal(entry.Arguments.Select(a => Normal(a.Value)), statements[i].Arguments.Select(a => Normal(a.Value)));
        }
    }

    private static string Normal(JsonElement value) => JsonSerializer.Serialize(JsonDocument.Parse(value.GetRawText()).RootElement);

    [Fact]
    public async Task Awkward_numbers_are_written_as_sent()
    {
        await using var carve = await Start("define_mesh " + Cube);
        await Say(carve, "move", """{"faces":[3],"by":[0.30000000000000004,-0,1E2]}""");
        await Say(carve, "save_script", """{"path":"n.carvescript"}""");
        Assert.Contains("move(faces: [3], by: [0.30000000000000004, -0, 1E2])", Read("n.carvescript"));
    }

    [Fact]
    public async Task Open_of_a_file_in_a_sibling_folder_is_written_relative_to_the_script()
    {
        await using var carve = await Start("define_mesh " + Cube);
        await Say(carve, "save", """{"path":"models/x.carve"}""");
        await Say(carve, "open", """{"path":"models/x.carve"}""");
        await Say(carve, "save_script", """{"path":"scripts/s.carvescript"}""");
        Assert.Equal("open(path: \"../models/x.carve\")\n", Read("scripts/s.carvescript"));
    }

    [Fact]
    public async Task A_texture_path_is_written_relative_to_a_script_in_a_subfolder()
    {
        Directory.CreateDirectory(Path.Combine(Root, "tex"));
        File.Copy(Fixture, Path.Combine(Root, "tex", "a.png"));
        await using var carve = await Start("define_mesh " + Cube);
        await Say(carve, "define_material", """{"name":"wood","base_colour_texture":"tex/a.png"}""");
        await Say(carve, "save_script", """{"path":"out/deep/s.carvescript"}""");
        Assert.Contains("""define_material(name: "wood", base_colour_texture: "../../tex/a.png")""", Read("out/deep/s.carvescript"));
    }

    [Fact]
    public async Task A_file_on_another_drive_refuses_the_whole_save_and_writes_nothing()
    {
        var here = Path.GetPathRoot(Root)!;
        var other = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady && !string.Equals(d.RootDirectory.FullName, here, StringComparison.OrdinalIgnoreCase)).Select(d => d.RootDirectory.FullName).FirstOrDefault();
        if (other is null) { Assert.Skip("this machine has one drive, so no file has no path relative to the script"); return; }
        var folder = Path.Combine(other, $"carve-save-script-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(folder);
            await using var carve = await Start("define_mesh " + Cube);
            var far = JsonSerializer.Serialize(new { path = Path.Combine(folder, "x.carve") });
            await Say(carve, "save", far);
            await Say(carve, "open", far);
            var text = await Say(carve, "save_script", """{"path":"far.carvescript"}""", refused: true);
            Assert.Contains("statement 1 (open), argument path", text);
            Assert.Contains("another drive", text);
            Assert.False(File.Exists(Path.Combine(Root, "far.carvescript")));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            Assert.Skip("the other drive is not writable: " + e.Message);
        }
        finally { try { Directory.Delete(folder, true); } catch (Exception) { } }
    }

    [Fact]
    public async Task An_empty_journal_writes_an_empty_file_and_says_so()
    {
        await using var carve = await Start();
        var result = await Say(carve, "save_script", """{"path":"e.carvescript"}""");
        Assert.Contains("no statements", result);
        Assert.Equal("", Read("e.carvescript"));
    }

    [Fact]
    public async Task Undo_and_clear_shape_what_is_written_and_save_script_is_not_journalled()
    {
        await using var carve = await Start("define_mesh " + Cube, """move {"faces":[3],"by":[0,0.5,0]}""", "undo {}");
        await Say(carve, "save_script", """{"path":"a.carvescript"}""");
        Assert.Equal(["define_mesh"], ScriptReader.Read(Read("a.carvescript")).Script!.Statements.Select(s => s.Tool));
        await Say(carve, "clear", "{}");
        await Say(carve, "define_mesh", Cube);
        await Say(carve, "save_script", """{"path":"b.carvescript"}""");
        Assert.Equal(["clear", "define_mesh"], ScriptReader.Read(Read("b.carvescript")).Script!.Statements.Select(s => s.Tool));
        Assert.Equal(2, carve.Store.Journal.Count);
    }

    [Fact]
    public async Task The_path_refusals_save_has_and_a_silent_result()
    {
        await using var carve = await Start("define_mesh " + Cube);
        Assert.Contains("must end in .carvescript", await Say(carve, "save_script", """{"path":"s.carve"}""", refused: true));
        Assert.Contains("path is empty", await Say(carve, "save_script", """{"path":""}""", refused: true));
        Assert.Contains("names a folder", await Say(carve, "save_script", """{"path":"dir/"}""", refused: true));
        Assert.Contains("path: required but missing", await Say(carve, "save_script", "{}", refused: true));
        Assert.Contains("not an argument of save_script", await Say(carve, "save_script", """{"path":"s.carvescript","mesh":"x"}""", refused: true));
        Assert.Equal("""{"ok":true}""", await Say(carve, "save_script", """{"path":"s.carvescript","verbosity":"silent"}"""));
        Assert.Contains("(replaced)", await Say(carve, "save_script", """{"path":"s.carvescript"}"""));
        Assert.Single(carve.Store.Journal);
    }
}
