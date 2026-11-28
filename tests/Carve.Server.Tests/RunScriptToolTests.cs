extern alias carve;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.VerbosityCalls;
using RunScriptTool = carve::Carve.Server.RunScriptTool;

namespace Carve.Server.Tests;

/// <summary>T-233: run_script runs a .carvescript on the current model as one undo step, all or nothing.</summary>
[Trait("Tier", "unit")]
public sealed class RunScriptToolTests() : TempFolderTests("carve-run-script")
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private static string Sample(string name) => Path.Combine(Paths.RepoRoot, "samples", name);

    private async Task<ToolHarness> Start(params string[] setup) => await VerbosityToolTests.Start(Root, setup);

    private string Write(string relative, string text)
    {
        var full = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    private static async Task<string> Say(ToolHarness carve, string tool, string arguments, bool refused = false)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        Assert.True(refused == IsError(result), $"{tool} {arguments}: {Text(result)}");
        return Text(result);
    }

    private static string Run(string path) => $$"""{"path":{{System.Text.Json.JsonSerializer.Serialize(path)}}}""";

    private static string[] Calls(ToolHarness carve) =>
        [.. carve.Store.Journal.Select(e => e.Tool + string.Concat(e.Arguments.Select(a => " " + a.Key + "=" + a.Value.GetRawText())))];

    [Fact]
    public async Task The_worked_example_runs_as_written_and_answers_what_it_quotes()
    {
        await using var carve = await Start();
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "run_script").GetProperty("description").GetString()!;
        Assert.Contains(RunScriptTool.WorkedFile + "it returns " + RunScriptTool.WorkedResult + "\n", description);
        Assert.EndsWith("Example: " + RunScriptTool.WorkedCall, description);
        Write("slab.carvescript", RunScriptTool.WorkedFile);
        Assert.Equal(RunScriptTool.WorkedResult, await Say(carve, "run_script", RunScriptTool.WorkedCall));
        Assert.Equal("""{"ok":true}""", await Say(carve, "run_script", """{"path":"slab.carvescript","verbosity":"silent"}"""));
        Assert.Contains("run_script {\"path\":\"x.carvescript\"}", carve::Carve.Server.HelpTool.Topic("scripts"));
        // README's worked call is this one, its result cut before the warnings.
        var readme = File.ReadAllText(Path.Combine(Paths.RepoRoot, "README.md")).ReplaceLineEndings("\n");
        Assert.Contains("```\n" + RunScriptTool.WorkedFile + "```\n\nis `run_script` `" + RunScriptTool.WorkedCall + "`, which returns `" + RunScriptTool.WorkedResult[..RunScriptTool.WorkedResult.IndexOf("[\"", StringComparison.Ordinal)] + "[...]}]}`", readme);
    }

    [Fact]
    public async Task The_gatehouse_script_run_after_clear_saves_the_gatehouse_byte_for_byte()
    {
        await using var carve = await Start("define_mesh " + Cube);
        await Say(carve, "clear", "{}");
        Assert.Contains("\"statements\":14", await Say(carve, "run_script", Run(Sample("gatehouse.carvescript"))));
        await Say(carve, "save", """{"path":"gatehouse.carve"}""");
        Assert.Equal(File.ReadAllBytes(Sample("gatehouse.carve")), File.ReadAllBytes(Path.Combine(Root, "gatehouse.carve")));
    }

    [Fact]
    public async Task A_run_on_a_model_adds_to_it_and_undo_takes_the_whole_run_back_in_one_step()
    {
        await using var carve = await Start("define_mesh " + Cube);
        var before = carve.Store.Current;
        Write("s.carvescript", "extrude(faces: [1], distance: 0.5)\nmove(faces: [1], by: [0, 0, 0.5])\nloop_cut(edge: [0, 1], at: [0.5])\n");
        var result = await Say(carve, "run_script", """{"path":"s.carvescript"}""");
        Assert.Contains("\"statements\":3", result);
        Assert.Equal(before.Version + 1, carve.Store.Current.Version);
        Assert.Equal(["define_mesh", "extrude", "move", "loop_cut"], carve.Store.Journal.Select(e => e.Tool));
        Assert.Contains("\"undone\":\"run_script\"", await Say(carve, "undo", "{}"));
        Assert.Same(before.Model, carve.Store.Current.Model);
        Assert.Equal(["define_mesh"], carve.Store.Journal.Select(e => e.Tool));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task A_failing_statement_names_its_line_and_leaves_model_history_journal_and_version(int bad)
    {
        await using var carve = await Start("define_mesh " + Cube, """extrude {"faces":[1],"distance":0.5}""");
        var (model, version, journal) = (carve.Store.Current.Model, carve.Store.Current.Version, Calls(carve));
        var lines = Enumerable.Range(1, 3).Select(i => i == bad ? "move(faces: [99], by: [0, 1, 0])" : "move(faces: [3], by: [0, 0.1, 0])");
        Write("s.carvescript", string.Join("\n", lines) + "\n");
        var error = await Say(carve, "run_script", """{"path":"s.carvescript"}""", refused: true);
        Assert.StartsWith($"line {bad}: move", error);
        Assert.Contains("99", error);
        Assert.Contains("the model is as before the call", error);
        Assert.Same(model, carve.Store.Current.Model);
        Assert.Equal(version, carve.Store.Current.Version);
        Assert.Equal(journal, Calls(carve));
        // The next call builds on the model as it was, at the next version.
        await Say(carve, "move", """{"faces":[3],"by":[0,0.1,0]}""");
        Assert.Equal(version + 1, carve.Store.Current.Version);
        Assert.Equal([.. journal, "move faces=[3] by=[0,0.1,0]"], Calls(carve));
        Assert.Contains("\"undone\":\"move\"", await Say(carve, "undo", "{}"));
        Assert.Same(model, carve.Store.Current.Model);
        Assert.Contains("\"undone\":\"extrude\"", await Say(carve, "undo", "{}"));
    }

    [Fact]
    public async Task Each_refused_statement_kind_is_refused_by_line_before_anything_runs()
    {
        await using var carve = await Start("define_mesh " + Cube);
        var version = carve.Store.Current.Version;
        var wrong = new List<string>();
        foreach (var (tool, why) in RunScriptTool.Refused.Append(KeyValuePair.Create("no_such_tool", "carve has no tool 'no_such_tool'")))
        {
            Write("s.carvescript", "extrude(faces: [1], distance: 0.5)\n" + tool + "()\n");
            var error = await Say(carve, "run_script", """{"path":"s.carvescript"}""", refused: true);
            var expected = tool == "no_such_tool" ? "line 2: " + why : $"line 2: {tool} is not a statement a script can run: {why}";
            if (!error.StartsWith(expected, StringComparison.Ordinal)) wrong.Add($"{tool}: {error}");
        }
        Assert.Empty(wrong);
        Assert.Equal(version, carve.Store.Current.Version);
        Write("s.carvescript", string.Concat(Enumerable.Repeat("extrude(faces: [1], distance: 0.5)\n", 6)) + "save(path: \"x.carve\")\n");
        Assert.Equal("line 7: save is not a statement a script can run: it changes nothing in the model; nothing of " + Path.Combine(Root, "s.carvescript") + " was kept, the model is as before the call",
            await Say(carve, "run_script", """{"path":"s.carvescript"}""", refused: true));
    }

    [Fact]
    public async Task The_refused_tools_are_the_ones_that_make_no_step_and_every_other_tool_may_run()
    {
        await using var carve = await Start();
        var names = (await carve.ListToolsAsync()).EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToHashSet();
        string[] noStep = [.. VerbosityToolTests.Readers, .. JournalToolTests.NoStep, "undo", "run_script"];
        Assert.Equal(noStep.Order(), RunScriptTool.Refused.Keys.Order());
        Assert.Subset(names, RunScriptTool.Refused.Keys.ToHashSet());
    }

    [Fact]
    public async Task A_format_error_names_line_and_column_and_nothing_runs()
    {
        await using var carve = await Start("define_mesh " + Cube);
        var version = carve.Store.Current.Version;
        Write("s.carvescript", "extrude(faces: [1], distance: 0.5)\nmove(faces [3])\n");
        var error = await Say(carve, "run_script", """{"path":"s.carvescript"}""", refused: true);
        Assert.Contains("line 2, column 12: expected ':' after the argument name 'faces'; nothing ran", error);
        Assert.Equal(version, carve.Store.Current.Version);
    }

    [Fact]
    public async Task A_relative_texture_resolves_against_the_scripts_folder()
    {
        await using var carve = await Start("define_mesh " + Cube);
        var texture = Path.Combine(Root, "sub", "tex", "a.png");
        Directory.CreateDirectory(Path.GetDirectoryName(texture)!);
        File.Copy(Path.Combine(Paths.RepoRoot, "tests", "Carve.Server.Tests", "Fixtures", "swatch.png"), texture);
        Write("sub/s.carvescript", "define_material(name: \"wood\", base_colour_texture: \"tex/a.png\")\n");
        await Say(carve, "run_script", """{"path":"sub/s.carvescript"}""");
        Assert.Equal(texture, carve.Store.Journal[^1].Files["base_colour_texture"]);
        // The same call made directly resolves against carve's folder, where there is no tex/a.png.
        await Say(carve, "define_material", """{"name":"wood","base_colour_texture":"tex/a.png"}""", refused: true);
    }

    [Fact]
    public async Task Save_script_after_a_run_and_more_edits_writes_the_statements_then_the_edits_and_replays_to_the_same_file()
    {
        await using var carve = await Start();
        Write("slab.carvescript", RunScriptTool.WorkedFile);
        await Say(carve, "run_script", RunScriptTool.WorkedCall);
        await Say(carve, "move", """{"faces":[0],"by":[0,0,0.25]}""");
        await Say(carve, "save_script", """{"path":"all.carvescript"}""");
        Assert.Equal(RunScriptTool.WorkedFile + "move(faces: [0], by: [0, 0, 0.25])\n", File.ReadAllText(Path.Combine(Root, "all.carvescript")));
        await Say(carve, "save", """{"path":"a.carve"}""");
        await Say(carve, "clear", "{}");
        await Say(carve, "run_script", """{"path":"all.carvescript"}""");
        await Say(carve, "save", """{"path":"b.carve"}""");
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root, "a.carve")), File.ReadAllBytes(Path.Combine(Root, "b.carve")));
    }

    [Fact]
    public async Task A_clear_in_a_script_restarts_the_journal_there()
    {
        await using var carve = await Start("define_mesh " + Cube);
        Write("s.carvescript", "clear()\n" + RunScriptTool.WorkedFile);
        await Say(carve, "run_script", """{"path":"s.carvescript"}""");
        Assert.Equal(["clear", "define_mesh", "extrude"], carve.Store.Journal.Select(e => e.Tool));
    }
}
