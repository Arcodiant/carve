extern alias carve;
using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-207: every carve tool description fits Claude Code's cut, and what was cut from a description is served by <c>help</c>.
/// Adding an entry: a line in <c>HelpTool</c>'s table, a <c>Reference</c> on the tool's class, the tool's description ending
/// with <c>More: help {"tool":"name"} gives ...</c>.
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
    /// A description and its tool's help entry as one text, for a test that pins a sentence or an example that moved from the
    /// description to the entry (T-207): the pin keeps holding, wherever the sentence now is.
    /// </summary>
    public static string WithReference(string description, string tool) => description + "\n" + carve::Carve.Server.HelpTool.Entry(tool);

    public HelpToolTests() : base("help")
    {
    }

    [Fact]
    public async Task Every_description_is_under_the_limit_with_no_tool_exempt()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _ = carve;
        var lengths = tools.EnumerateArray().Select(t => (Name: t.GetProperty("name").GetString()!, Length: t.GetProperty("description").GetString()!.Length)).ToList();

        Assert.Contains(lengths, t => t.Name == "help");
        var over = lengths.Where(t => t.Length > Limit).Select(t => $"{t.Name} {t.Length}").ToList();
        Assert.True(over.Count == 0, $"over {Limit} characters (Claude Code cuts at {ClientCut}): {string.Join(", ", over)}");
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

    /// <summary>RV-82: a pointer to an entry that does not exist, and an entry that is empty or not what its tool's own text says, would not be caught by the loop over the table.</summary>
    [Fact]
    public async Task Every_pointer_names_an_entry_and_every_entry_is_served_whole()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _ = carve;
        foreach (var tool in tools.EnumerateArray())
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(tool.GetProperty("description").GetString()!, """More: help \{"tool":"([a-z_]+)"\}"""))
                Assert.Contains(m.Groups[1].Value, carve::Carve.Server.HelpTool.Tools);
        foreach (var name in carve::Carve.Server.HelpTool.Tools)
        {
            var entry = carve::Carve.Server.HelpTool.Entry(name);
            Assert.False(string.IsNullOrWhiteSpace(entry), $"{name}: empty entry");
            Assert.Equal(entry, Ok(await carve.CallToolAsync("help", $$"""{"tool":"{{name}}"}""")));
        }
        Assert.Contains("How the game's PBR shading reads each field", carve::Carve.Server.HelpTool.Entry("define_material"));
        Assert.Contains("And carve keeps no current file", carve::Carve.Server.HelpTool.Entry("open"));
    }

    /// <summary>RV-82: what M-29 added about graphs is in the descriptions themselves, not only in help (T-207's rule).</summary>
    [Fact]
    public async Task The_graph_sentences_are_in_the_descriptions_and_reads_mesh_is_said_where_it_is_answered()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _ = carve;
        var render = Description(tools, "render");
        foreach (var said in new[] { "{graph, output, size} evaluated on this mesh", "real curvature, occlusion, masks and position", "any role or named output may be named", "a power of two from 64 to 4096, not the frame's 256, 512 or 1024" })
            Assert.Contains(said, render);
        Assert.Contains("reads_mesh (whether the graph reads the mesh; null if its file cannot be read now)", Description(tools, "define_material"));
        Assert.Contains("reads_mesh (as define_material reports it)", Description(tools, "get_mesh"));
        Assert.DoesNotContain("no delete yet", Description(tools, "check") + Description(tools, "select"));
        Assert.Contains("own: +Y up", Description(tools, "export"));
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
