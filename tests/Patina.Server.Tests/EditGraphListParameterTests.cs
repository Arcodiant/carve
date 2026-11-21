using Xunit;

namespace Patina.Server.Tests;

/// <summary>T-171: edit_graph's set_params and set_node take list parameters as define_graph reads them, so a gradient_map's stops are edited in place.</summary>
[Trait("Tier", "unit")]
public class EditGraphListParameterTests
{
    private static string Map(string positions, string colours) =>
        DefineGraphTool.WorkedMap.Replace("[0,0.5,1]", positions).Replace("[\"#1c2b24\",\"#4a6b52\",\"#b9c9a8\"]", colours);

    private static string Set(string positions, string colours) =>
        "{\"operations\":[{\"op\":\"set_params\",\"node\":\"tint\",\"params\":{\"positions\":" + positions + ",\"colours\":" + colours + "}}]}";

    [Fact]
    public async Task The_worked_example_changes_one_stop_in_place_and_equals_the_same_graph_defined()
    {
        Assert.Contains("edit_graph " + EditGraphTool.WorkedList + "\nreturns: " + EditGraphTool.WorkedListResult, EditGraphTool.Text);
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.WorkedMap);
        Assert.Equal(EditGraphTool.WorkedListResult, await patina.Ok("edit_graph", EditGraphTool.WorkedList));

        await using var defined = await PatinaHarness.StartAsync();
        await defined.Ok("define_graph", Map("[0,0.4,1]", """["#1c2b24","#6b8f55","#b9c9a8"]"""));
        Assert.Equal(await defined.GraphText(), await patina.GraphText());
    }

    [Fact]
    public async Task Set_node_takes_the_lists_too_and_the_edit_leaves_the_other_nodes()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.WorkedMap);
        var before = await patina.GraphText();
        await patina.Ok("edit_graph", """{"operations":[{"op":"set_node","node":"tint","type":"gradient_map","inputs":{"in":"soft"},"params":{"positions":[0,1],"colours":["#000000","#ffffff"]}}]}""");

        await using var defined = await PatinaHarness.StartAsync();
        await defined.Ok("define_graph", Map("[0,0.5,1]", """["#1c2b24","#4a6b52","#b9c9a8"]""").Replace("""[0,0.5,1]""", "[0,1]"));
        Assert.NotEqual(before, await patina.GraphText());
        Assert.Contains("\"cloud\"", await patina.GraphText());
    }

    [Theory]
    [InlineData("[0,0.5,1]", "[0.1,0.2,0.3]", "node 'tint' parameter 'colours' is [0.1, 0.2, 0.3], but must be a list of 2 to 8 colours")] // wrong kind: numbers, not colours
    [InlineData("[0,0.5,2]", """["#000000","#888888","#ffffff"]""", "node 'tint' parameter 'positions'")]
    [InlineData("[0,0.5,1]", """["#000000","#ffffff"]""", "positions has 3 and colours has 2")]
    [InlineData("[0,0.8,0.5]", """["#000000","#888888","#ffffff"]""", "node 'tint' input 'in': parameter 'positions' must run from low to high")]
    [InlineData("[0,0.5,1]", """["#000000","red","#ffffff"]""", "node 'tint' parameter 'colours'")]
    public async Task A_bad_list_is_refused_naming_the_node_and_parameter_and_changes_nothing(string positions, string colours, string expected)
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.WorkedMap);
        var before = await patina.GraphText();

        var said = await patina.Refused("edit_graph", Set(positions, colours));

        Assert.Contains(expected, said);
        Assert.Equal(before, await patina.GraphText());
    }

    [Fact]
    public async Task A_mixed_array_is_refused_as_the_file_words_it()
    {
        await using var patina = await PatinaHarness.StartAsync();
        await patina.Ok("define_graph", DefineGraphTool.WorkedMap);
        var said = await patina.Refused("edit_graph", Set("[0,0.5,1]", """["#000000",1,"#ffffff"]"""));
        Assert.Contains("operation 1 (set_params): parameter 'colours': an array is all numbers or all strings", said);
    }
}
