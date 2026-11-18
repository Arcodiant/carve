using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class UvToolDefinitionTests() : TempFolderTests("carve-uvs")
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static async Task<ToolHarness> StartAsync(string mesh = Cube, string? root = null)
    {
        var carve = new ToolHarness(workingDirectory: root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", mesh));
        return carve;
    }

    private static string Pairs(JsonElement list) => string.Join(" ", list.EnumerateArray().Select(p => $"{p[0]}-{p[1]}"));

    [Fact]
    public async Task The_described_examples_run_and_read_back()
    {
        using var carve = await StartAsync();
        var tools = await carve.ListToolsAsync();
        var set = Ok(await carve.CallToolAsync("set_uvs", ToolList.Examples(tools, "set_uvs").Single()));
        Assert.Equal(1, set.GetProperty("set").GetInt32());
        foreach (var example in ToolList.Examples(tools, "mark_edges")) Ok(await carve.CallToolAsync("mark_edges", example));
        Assert.Equal(2, ToolList.Examples(tools, "mark_edges").Length);
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.Equal("0-3", Pairs(mesh.GetProperty("seams")));
    }

    [Fact]
    public async Task The_schemas_and_descriptions_name_the_new_arguments_and_the_rules()
    {
        using var carve = await StartAsync();
        var tools = await carve.ListToolsAsync();
        var set = ToolList.Tool(tools, "set_uvs");
        var properties = set.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(properties.TryGetProperty("faces", out _));
        Assert.True(properties.TryGetProperty("clear", out _));
        Assert.True(properties.TryGetProperty("mesh", out _));
        Assert.True(ToolList.Tool(tools, "mark_edges").GetProperty("inputSchema").GetProperty("properties").TryGetProperty("seam", out _));
        foreach (var tool in new[] { "set_uvs", "mark_edges" })
        {
            var text = ToolList.Description(tools, tool);
            Assert.Contains("top-left", text);
            Assert.Contains("v down", text);
            Assert.Contains("needs no seam", text);
            Assert.Contains("Example: ", text);
        }
        var setText = ToolList.Description(tools, "set_uvs");
        Assert.Contains("corner", setText);
        Assert.Contains("tiles", setText);
        Assert.Contains("unwrap cuts", ToolList.Description(tools, "mark_edges"));
        Assert.Contains("uvs:", ToolList.Description(tools, "get_mesh"));
        Assert.Contains("seams:", ToolList.Description(tools, "get_mesh"));
    }
}
