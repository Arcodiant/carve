using System.Text.Json;

namespace Carve.Server.Tests;

public class StdioTests
{
    [Fact]
    public async Task Stdout_carries_only_protocol_and_closing_stdin_exits_zero()
    {
        using var carve = new CarveProcess([]);

        var initialize = await carve.InitializeAsync();
        Assert.Equal("carve", initialize.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

        var tools = await carve.ListToolsAsync();
        Assert.Contains("define_mesh", tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()));

        carve.CloseStdin();
        Assert.Equal(0, await carve.WaitForExitAsync(Exit));

        Assert.NotEmpty(carve.StdoutLines);
        Assert.All(carve.StdoutLines, line =>
        {
            using var document = JsonDocument.Parse(line);
            Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
        });
    }

    [Fact]
    public async Task Without_a_vulkan_device_mcp_stays_up_and_holds_the_fault()
    {
        using var carve = new CarveProcess([], new Dictionary<string, string>
        {
            ["VK_LOADER_DRIVERS_SELECT"] = "nonexistent",
        });

        await carve.InitializeAsync();

        await carve.ListToolsAsync();

        carve.CloseStdin();
        Assert.Equal(0, await carve.WaitForExitAsync(Exit));
        Assert.Contains("Engine faulted", carve.Stderr);
    }

    [Fact]
    public async Task Stdin_closed_at_once_still_exits_zero()
    {
        using var carve = new CarveProcess([]);

        carve.CloseStdin();

        Assert.Equal(0, await carve.WaitForExitAsync(Exit));
    }
}
