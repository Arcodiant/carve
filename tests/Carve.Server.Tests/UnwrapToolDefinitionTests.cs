using System.Text.Json;
using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class UnwrapToolDefinitionTests
{
    [Fact]
    public async Task The_schema_lists_its_arguments()
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var tool = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "unwrap");
        Assert.Equal(["faces", "into", "margin", "mesh", "verbosity"], tool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
        var text = tool.GetProperty("description").GetString()!;
        Assert.Contains("one seam down its side", text);
        Assert.Contains("[[2,10],[10,18],[18,26],[26,34]]", text);
    }
}
