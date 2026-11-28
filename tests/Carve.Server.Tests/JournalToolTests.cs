extern alias carve;
using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.VerbosityCalls;

namespace Carve.Server.Tests;

/// <summary>
/// T-231: the journal <c>ModelStore</c> keeps of the calls that made a step, and <c>clear</c>. A call is journalled when,
/// and only when, it pushed an undo step; undo takes its entry back; a whole-model open and clear start a new journal.
/// </summary>
[Trait("Tier", "unit")]
public sealed class JournalToolTests() : TempFolderTests("carve-journal")
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>The rows of <see cref="VerbosityToolTests.Tools"/> whose call makes no step: they write or look, or (undo) take one back.</summary>
    private static readonly string[] NoStep = ["save", "save_script", "check", "export", "bake"];

    private static string Call(carve::Carve.Server.JournalEntry e) =>
        e.Tool + " {" + string.Join(",", e.Arguments.Select(a => $"{JsonSerializer.Serialize(a.Key)}:{a.Value.GetRawText()}")) + "}";

    private static string[] Calls(ToolHarness carve) => [.. carve.Store.Journal.Select(Call)];

    private async Task<ToolHarness> Start(params string[] setup) => await VerbosityToolTests.Start(Root, setup);

    private static async Task Ok(ToolHarness carve, string tool, string arguments)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        Assert.False(IsError(result), $"{tool}: {Text(result)}");
    }

    [Fact]
    public async Task Every_tool_in_tools_list_journals_its_call_only_when_it_makes_a_step()
    {
        await using var list = new ToolHarness();
        await list.InitializeAsync();
        var rows = VerbosityToolTests.Tools.Cast<ITheoryDataRow>().Select(r => r.GetData()).ToDictionary(d => (string)d[0]!);
        // unwrap's verbosity is tested on its own (UnwrapToolDefinitionTests), so its row is here: one face of the cube, a disc with no seams.
        rows["unwrap"] = ["unwrap", new[] { "define_mesh " + Cube }, """{"faces":[0]}""", """{"margin":"x"}"""];
        var wrong = new List<string>();
        foreach (var tool in (await list.ListToolsAsync()).EnumerateArray().Select(t => t.GetProperty("name").GetString()!))
        {
            var folder = Directory.CreateDirectory(Path.Combine(Root, tool)).FullName;
            if (VerbosityToolTests.Readers.Contains(tool))
            {
                await using var reader = await VerbosityToolTests.Start(folder, ["define_mesh " + Cube]);
                await reader.CallToolAsync(tool, "{}");
                if (reader.Store.Journal.Count != 1) wrong.Add($"{tool}: a reader, but the journal has {reader.Store.Journal.Count} entries");
                continue;
            }
            if (!rows.TryGetValue(tool, out var row)) { wrong.Add($"{tool}: in neither column (a row in VerbosityToolTests.Tools, or a reader)"); continue; }
            var (setup, good, bad) = ((string[])row[1]!, (string)row[2]!, (string)row[3]!);
            await using var carve = await VerbosityToolTests.Start(folder, setup);
            var before = Calls(carve);
            var result = await carve.CallToolAsync(tool, good);
            if (IsError(result)) { wrong.Add($"{tool}: {Text(result)}"); continue; }
            var after = Calls(carve);
            string[] expected = tool switch
            {
                _ when NoStep.Contains(tool) => before,
                "undo" => before[..^1],
                "open" or "clear" => [$"{tool} {good}"],
                _ => [.. before, $"{tool} {good}"],
            };
            if (!after.SequenceEqual(expected)) wrong.Add($"{tool}: journal [{string.Join(" | ", after)}], expected [{string.Join(" | ", expected)}]");
            var refused = await carve.CallToolAsync(tool, bad);
            if (!IsError(refused)) wrong.Add($"{tool}: {bad} was not refused");
            if (!Calls(carve).SequenceEqual(after)) wrong.Add($"{tool}: the refused {bad} changed the journal");
        }
        Assert.Empty(wrong);
    }

    [Fact]
    public async Task Undo_drops_the_last_entry_and_with_nothing_to_undo_leaves_the_journal()
    {
        await using var carve = await Start("define_mesh " + Cube);
        Assert.True(IsError(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(["define_mesh " + Cube], Calls(carve));
        await Ok(carve, "move", """{"faces":[3],"by":[0,0.5,0]}""");
        await Ok(carve, "undo", "{}");
        Assert.Equal(["define_mesh " + Cube], Calls(carve));
    }

    [Theory]
    [InlineData("open", """{"path":"a.carve"}""")]
    [InlineData("clear", "{}")]
    public async Task Open_and_clear_start_a_new_journal_and_undo_of_them_brings_the_earlier_back(string tool, string arguments)
    {
        await using var carve = await Start("define_mesh " + Cube, """save {"path":"a.carve"}""", """move {"faces":[3],"by":[0,0.5,0]}""");
        var earlier = Calls(carve);
        Assert.Equal(2, earlier.Length);
        await Ok(carve, tool, arguments);
        Assert.Equal([$"{tool} {arguments}"], Calls(carve));
        await Ok(carve, "define_mesh", Cube);
        await Ok(carve, "undo", "{}");
        await Ok(carve, "undo", "{}");
        Assert.Equal(earlier, Calls(carve));
    }

    [Fact]
    public async Task Clear_empties_the_model_as_a_step_even_when_empty_and_answers_as_undo_does()
    {
        await using var carve = await Start("define_mesh " + Cube, """define_skeleton {"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]}]}""");
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "clear").GetProperty("description").GetString()!;
        Assert.EndsWith("Example: " + carve::Carve.Server.ClearTool.Example, description);
        var result = await carve.CallToolAsync("clear", carve::Carve.Server.ClearTool.Example);
        Assert.Equal("""{"meshes":[]}""", Text(result));
        Assert.Empty(carve.Store.Current.Model.Meshes);
        Assert.Null(carve.Store.Current.Model.Skeleton);
        var version = carve.Store.Current.Version;
        Assert.Equal("""{"ok":true}""", Text(await carve.CallToolAsync("clear", """{"verbosity":"silent"}""")));
        Assert.Equal(version + 1, carve.Store.Current.Version);
        Assert.Equal(["""clear {"verbosity":"silent"}"""], Calls(carve));
        Assert.Contains("\"undone\":\"clear\"", Text(await carve.CallToolAsync("undo", "{}")));
        Assert.Contains("\"undone\":\"clear\"", Text(await carve.CallToolAsync("undo", "{}")));
        Assert.NotNull(carve.Store.Current.Model.Skeleton);
        Assert.Contains("not an argument of clear", Text(await carve.CallToolAsync("clear", """{"mesh":"x"}""")));
    }

    [Fact]
    public async Task Open_of_one_part_appends()
    {
        await using var carve = await Start("""define_mesh {"name":"a","vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""", """save {"path":"a.carve"}""");
        await Ok(carve, "open", """{"path":"a.carve","mesh":"a"}""");
        Assert.Equal(["define_mesh", "open"], carve.Store.Journal.Select(e => e.Tool));
    }

    [Fact]
    public async Task The_journal_is_not_capped_at_the_undo_depth()
    {
        await using var carve = await Start("define_mesh " + Cube);
        for (var i = 1; i < 105; i++) await Ok(carve, "move", """{"faces":[3],"by":[0,0.01,0]}""");
        Assert.True(105 > carve::Carve.Server.ModelStore.UndoDepth);
        Assert.Equal(105, carve.Store.Journal.Count);
        await Ok(carve, "undo", "{}");
        Assert.Equal(104, carve.Store.Journal.Count);
    }

    [Fact]
    public async Task Arguments_come_back_in_the_order_and_raw_text_sent()
    {
        await using var carve = await Start("define_mesh " + Cube);
        const string arguments = """{"by":[0,0.30000000000000004,-0],"verbosity":"silent","faces":[3]}""";
        await Ok(carve, "move", arguments);
        var entry = carve.Store.Journal[^1];
        Assert.Equal(["by", "verbosity", "faces"], entry.Arguments.Select(a => a.Key));
        Assert.Equal("[0,0.30000000000000004,-0]", entry.Arguments[0].Value.GetRawText());
        Assert.Equal("move " + arguments, Call(entry));
        Assert.Empty(entry.Files);
    }

    [Fact]
    public async Task A_file_argument_keeps_its_absolute_path_resolved_against_the_tool_folder()
    {
        Directory.CreateDirectory(Path.Combine(Root, "in"));
        await using var carve = await Start("define_mesh " + Cube, """save {"path":"in/a.carve"}""");
        await Ok(carve, "open", """{"path":"in/a.carve"}""");
        var entry = Assert.Single(carve.Store.Journal);
        var full = Assert.Single(entry.Files);
        Assert.Equal("path", full.Key);
        Assert.True(Path.IsPathFullyQualified(full.Value));
        Assert.Equal(Path.GetFullPath(Path.Combine(Root, "in", "a.carve")), full.Value);
    }

    /// <summary>Every model-changing tool's file-reading arguments, declared in its Create: a new one is a deliberate row here.</summary>
    [Fact]
    public async Task The_arguments_each_tool_reads_a_file_through_are_these()
    {
        string[] textures = ["base_colour_texture", "metallic_roughness_texture", "normal_texture", "emissive_texture", "occlusion_texture"];
        var expected = new Dictionary<string, string[]>
        {
            ["open"] = ["path"],
            ["define_target"] = ["file"],
            ["define_material"] = [.. textures.SelectMany(t => new[] { t, t + ".graph" })],
            ["displace"] = ["source.graph"],
        };
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var declared = carve.Tools.OfType<carve::Carve.Server.JournalledTool>().Where(t => t.Reads.Count > 0)
            .ToDictionary(t => t.ProtocolTool.Name, t => t.Reads.ToArray());
        Assert.Equal(expected.OrderBy(k => k.Key).Select(k => $"{k.Key}: {string.Join(",", k.Value)}"),
            declared.OrderBy(k => k.Key).Select(k => $"{k.Key}: {string.Join(",", k.Value)}"));
    }
}
