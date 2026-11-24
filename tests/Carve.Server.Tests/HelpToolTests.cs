extern alias carve;
using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-207: every carve tool description fits Claude Code's cut, and what was cut from a description is served by <c>help</c>.
/// Adding an entry: a line in <c>HelpTool</c>'s table, a <c>Reference</c> on the tool's class, the tool's description ending
/// with <c>More: help {"tool":"name"} gives ...</c>, and the tool leaves <see cref="Pending"/>.
/// </summary>
[Trait("Tier", "unit")]
public class HelpToolTests : FileToolTests
{
    /// <summary>
    /// Claude Code (2.1.329) cuts each MCP tool description at 4,096 characters and appends "… [truncated]", measured by
    /// a probe session quoting the end of each description (T-170's As built, for patina's; carve's are held the same).
    /// </summary>
    public const int ClientCut = 4096;

    /// <summary>The margin, approved by the person (2026-11-20): 10% under the cut, so at most 3,686 characters.</summary>
    public const int Limit = ClientCut * 9 / 10;

    /// <summary>
    /// T-207's to-do: the tools still over <see cref="Limit"/> that the members after the first have yet to trim, each
    /// removed from here as it is. Nothing else is exempt, and a name listed here that is already under fails, so the
    /// list shrinks to nothing.
    /// </summary>
    private static readonly string[] Pending = ["bake", "check", "open", "select", "define_mesh", "get_mesh", "define_material", "project_uvs", "unwrap"];

    /// <summary>
    /// A description and its tool's help entry as one text, for a test that pins a sentence or an example that moved from the
    /// description to the entry (T-207): the pin keeps holding, wherever the sentence now is.
    /// </summary>
    public static string WithReference(string description, string tool) => description + "\n" + carve::Carve.Server.HelpTool.Entry(tool);

    public HelpToolTests() : base("help")
    {
    }

    [Fact]
    public async Task Every_description_is_under_the_limit_but_the_tools_still_to_trim()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _ = carve;
        var lengths = tools.EnumerateArray().Select(t => (Name: t.GetProperty("name").GetString()!, Length: t.GetProperty("description").GetString()!.Length)).ToList();

        Assert.Contains(lengths, t => t.Name == "help");
        var over = lengths.Where(t => t.Length > Limit && !Pending.Contains(t.Name)).Select(t => $"{t.Name} {t.Length}").ToList();
        Assert.True(over.Count == 0, $"over {Limit} characters (Claude Code cuts at {ClientCut}): {string.Join(", ", over)}");
        var already = Pending.Where(p => lengths.Single(t => t.Name == p).Length <= Limit).ToList();
        Assert.True(already.Count == 0, $"already under {Limit}, so no longer pending: {string.Join(", ", already)}");
    }

    [Fact]
    public async Task Each_entrys_tool_ends_with_the_pointer_and_help_lists_every_entry()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _ = carve;
        var list = Ok(await carve.CallToolAsync("help", "{}"));
        foreach (var tool in carve::Carve.Server.HelpTool.Tools)
        {
            var description = Description(tools, tool);
            var at = description.LastIndexOf($"More: help {{\"tool\":\"{tool}\"}} gives ", StringComparison.Ordinal);
            Assert.True(at >= 0 && description.EndsWith('.') && !description[at..].Contains('\n'), $"{tool}: no pointer sentence at the end");
            Assert.Contains(tool + ": ", list);
            Assert.Contains(tool, Description(tools, "help"));
        }
        Assert.True(Description(tools, "help").Length < 1000);
    }

    [Fact]
    public async Task One_entry_is_returned_whole_and_an_unknown_name_or_a_tool_with_no_entry_is_refused_listing_them()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _ = carve;
        Assert.Equal(carve::Carve.Server.HelpTool.Entry("render"), Ok(await carve.CallToolAsync("help", """{"tool":"render"}""")));
        var entries = string.Join(", ", carve::Carve.Server.HelpTool.Tools);
        foreach (var name in new[] { "move", "nonesuch" })
            Assert.Equal($"help: there is no entry for '{name}'; the entries are {entries}", TextOf(await carve.CallToolAsync("help", $$"""{"tool":"{{name}}"}""")));
        Assert.True(IsError(await carve.CallToolAsync("help", """{"tool":3}""")));
        Assert.True(IsError(await carve.CallToolAsync("help", """{"other":1}""")));
    }

    [Fact]
    public async Task Help_needs_no_mesh_and_is_not_an_undo_step()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("move", ToolList.Examples(tools, "move")[0]));
        Ok(await carve.CallToolAsync("help", """{"tool":"render"}"""));
        Assert.Equal("move", JsonDocument.Parse(Ok(await carve.CallToolAsync("undo", "{}"))).RootElement.GetProperty("undone").GetString());
    }

    [Fact]
    public async Task Helps_own_example_runs_as_written()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _ = carve;
        foreach (var example in ToolList.Examples(tools, "help")) Ok(await carve.CallToolAsync("help", example));
    }
}
