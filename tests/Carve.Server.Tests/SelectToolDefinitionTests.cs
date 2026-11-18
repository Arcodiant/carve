using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public class SelectToolDefinitionTests
{
    private const string Example = "Example: ";

    private const string Result = "Result: ";

    private static async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync(bool define = true)
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (define) await OkAsync(carve, "define_mesh", ToolList.Cube(tools));
        return (carve, tools);
    }

    private static async Task<string> OkAsync(ToolHarness carve, string tool, string arguments)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        Assert.False(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return CarveProcess.TextOf(result);
    }

    private static string Select(string kind, string where) => JsonSerializer.Serialize(new { kind, where });

    /// <summary>A result's list as raw JSON, to be passed on untouched.</summary>
    private static string List(string result, string name) => JsonDocument.Parse(result).RootElement.GetProperty(name).GetRawText();

    private static int[] Ints(string result, string name) =>
        JsonDocument.Parse(result).RootElement.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToArray();

    [Fact]
    public async Task The_worked_examples_run_as_written_and_give_the_written_results()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        var lines = ToolList.Description(tools, "select").Split('\n');
        var examples = ToolList.Examples(tools, "select");
        Assert.Equal(5, examples.Length);

        var pairs = lines.Zip(lines.Skip(1)).Where(p => p.First.StartsWith(Example, StringComparison.Ordinal) && p.Second.StartsWith(Result, StringComparison.Ordinal))
            .Select(p => (Call: p.First[Example.Length..], Written: p.Second[Result.Length..])).ToArray();
        Assert.Equal(examples[..4], pairs.Select(p => p.Call));

        Assert.Equal("""{"kind":"faces","faces":[3],"count":1,"of":6}""", pairs[0].Written);
        Assert.Equal("""{"kind":"vertices","vertices":[3,7],"count":2,"of":8}""", pairs[1].Written);
        Assert.Equal("""{"kind":"edges","edges":[],"count":0,"of":12}""", pairs[2].Written);
        foreach (var (call, written) in pairs) Assert.Equal(written, await OkAsync(carve, "select", call));
    }

    [Fact]
    public async Task The_worked_chain_moves_the_ids_the_select_returned()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        var examples = ToolList.Examples(tools, "select");

        var selected = await OkAsync(carve, "select", examples[3]);
        Assert.Equal($$"""{"faces":{{List(selected, "faces")}},"by":[0,0.5,0]}""", examples[4]);
        var moved = await OkAsync(carve, "move", examples[4]);

        Assert.Equal(Ints(await OkAsync(carve, "select", Select("vertices", "y == 1.5")), "vertices"), Ints(moved, "moved"));
        Assert.Equal([2, 3, 6, 7], Ints(moved, "moved"));
    }

    [Fact]
    public async Task Tools_list_lists_select_with_a_closed_schema()
    {
        var (carve, tools) = await StartAsync(define: false);
        using var _ = carve;
        var schema = ToolList.Tool(tools, "select").GetProperty("inputSchema");

        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["kind", "where", "mesh"], schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(["kind", "where"], schema.GetProperty("required").EnumerateArray().Select(p => p.GetString()!).ToArray());
        Assert.Equal(["vertices", "edges", "faces"], schema.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(p => p.GetString()!).ToArray());
    }

    [Fact]
    public async Task The_description_gives_the_grammar_the_rules_and_every_error()
    {
        var (carve, tools) = await StartAsync(define: false);
        using var _ = carve;
        var text = ToolList.Description(tools, "select");

        string[] said =
        [
            // The grammar, and the rules an agent could not guess.
            "atom := '(' expr ')' | 'all' | 'none' | flag | comparison",
            "op := '<' | '<=' | '==' | '!=' | '>=' | '>'",
            "not binds tighter than and, which binds tighter than or",
            "a vertex's position, an edge's midpoint, a face's centre",
            "Comparisons are made in float, both sides",
            "move takes vertices, edges and faces, and extrude takes faces",
            "Ids never change and no edit removes anything yet, so a selection names the same elements after an edit",
            "the server keeps no selection",
            // Every error, in the words the tool uses.
            "where is empty",
            "'>>' is not an operator; use <, <=, ==, !=, >= or >",
            "at character 1: 'centre' is not a property of faces; faces have id, x, y, z, min, max, normal, vertices, area, non_planar, open",
            "at character 8: 'normal.w' is not an axis; use .x, .y or .z",
            "'min' needs an axis; use .x, .y or .z",
            "a flag cannot be compared with a number",
            "'area' is a number; compare it with <, <=, ==, !=, >= or >",
            "expected a number",
            "1e99 is out of range: a number must lie within ±3.40282347E+38",
            "expected ')'",
            "expected a property, a flag, all, none, not or '('",
            "expected 'and', 'or' or the end of the expression",
            "nested too deeply; 'not' and '(' go 200 levels deep at most",
        ];
        foreach (var words in said) Assert.Contains(words, text);

        (string Kind, string[] Words)[] kinds =
        [
            ("vertices", ["id, x, y, z, min.*, max.*, faces", "edges (", "unattached (", "boundary ("]),
            ("edges", ["x, y, z, min.*, max.*, a and b", "length", "faces (", "open (", "manifold (", "nonmanifold ("]),
            ("faces", ["id, x, y, z, min.*, max.*, normal.x, normal.y, normal.z", "vertices (", "area", "non_planar (", "open ("]),
        ];
        foreach (var (kind, words) in kinds)
        {
            var line = text.Split(Convert.ToChar(10)).Single(l => l.StartsWith($"{kind} have the numbers ", StringComparison.Ordinal));
            foreach (var word in words) Assert.Contains(word, line);
        }
    }
}
