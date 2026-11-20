using System.Text.Json;
using Patina.Graphs;

namespace Patina.Server.Tests;

/// <summary>check (T-141) through the harness: its worked examples as written, a graph or PNG files, and what it refuses.</summary>
[Trait("Tier", "unit")]
public class CheckToolTests
{
    private static string Sample(string relative) => Path.Combine(Paths.RepoRoot, "samples", "graphs", relative);

    private static string Open(string relative) => JsonSerializer.Serialize(new { path = Sample(relative) });

    [Fact]
    public async Task The_worked_examples_run_as_written_and_are_held_to_the_description()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        var text = CheckTool.Text;

        await patina.Ok("open", Open("bricks.texgraph"));
        Assert.Equal(CheckTool.WorkedResult, await patina.Ok("check", CheckTool.Worked));
        Assert.Contains("check " + CheckTool.Worked + " returns, clean:\n" + CheckTool.WorkedResult, text, StringComparison.Ordinal);

        await patina.Ok("open", Open("faults/chrome-in-paint.texgraph"));
        Assert.Equal(CheckTool.WorkedFaultResult, await patina.Ok("check", CheckTool.WorkedFault));
        Assert.Contains("check " + CheckTool.WorkedFault + " returns:\n" + CheckTool.WorkedFaultResult, text, StringComparison.Ordinal);

        Assert.Equal(CheckTool.WorkedFilesResult, await patina.Ok("check", CheckTool.WorkedFiles));
        Assert.Contains("check " + CheckTool.WorkedFiles + " returns:\n" + CheckTool.WorkedFilesResult, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_clean_graph_is_ok_and_a_planted_fault_is_a_problem_not_an_error()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        await patina.Ok("open", Open("faults/flat-mask.texgraph"));
        var report = JsonDocument.Parse(await patina.Ok("check", "{}")).RootElement;
        Assert.False(report.GetProperty("ok").GetBoolean());
        var problem = Assert.Single(report.GetProperty("problems").EnumerateArray());
        Assert.Equal(("one-value", "wear"), (problem.GetProperty("check").GetString(), problem.GetProperty("output").GetString()));
        Assert.True(report.GetProperty("stats").GetProperty("outputs").TryGetProperty("wear", out _));
    }

    [Fact]
    public async Task Size_and_parameters_are_used_for_a_graph_and_ignored_with_a_note_for_files()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        await patina.Ok("open", Open("bricks.texgraph"));
        var small = JsonDocument.Parse(await patina.Ok("check", """{"size":32}""")).RootElement;
        Assert.Equal(32, small.GetProperty("stats").GetProperty("size").GetInt32());

        var files = """{"size":64,"parameters":{"mortar":0.1},"files":[{"path":"samples/graphs/bricks/base_colour.png","role":"base_colour"}]}""";
        var report = JsonDocument.Parse(await patina.Ok("check", files)).RootElement;
        Assert.Equal(256, report.GetProperty("stats").GetProperty("size").GetInt32());
        Assert.Contains(report.GetProperty("notes").EnumerateArray(), n => n.GetString()!.StartsWith("size and parameters were not used", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Files_need_no_graph_and_the_errors_say_what_to_do()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        Assert.Contains("no open graph", await patina.Refused("check", "{}"));
        Assert.Contains("does not exist", await patina.Refused("check", """{"files":[{"path":"nope.png","role":"normal"}]}"""));
        Assert.Contains("twice", await patina.Refused("check", """{"files":[{"path":"samples/graphs/bricks/normal.png","role":"normal"},{"path":"samples/graphs/bricks/normal.png","role":"normal"}]}"""));
        await patina.Ok("open", Open("bricks.texgraph"));
        Assert.Contains("is not allowed", await patina.Refused("check", """{"size":4096}"""));
        Assert.Contains("unknown", (await patina.Refused("check", """{"sise":64}""")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_graph_with_a_fault_is_an_error_naming_it_and_changes_nothing()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "planted.texgraph"),
            "{\"version\":1,\"size\":64,\"seed\":5,\"nodes\":{\"c\":{\"type\":\"colour\"},\"out\":{\"type\":\"output\",\"inputs\":{\"in\":\"cc\"}}}}");
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("open", """{"path":"planted"}""");
        var before = await patina.GraphText();
        Assert.Contains("fix them with edit_graph", await patina.Refused("check", "{}"));
        Assert.Equal(before, await patina.GraphText());
    }
}
