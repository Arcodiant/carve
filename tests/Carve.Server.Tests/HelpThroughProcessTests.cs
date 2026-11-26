extern alias carve;

namespace Carve.Server.Tests;

/// <summary>
/// T-207 through the real <c>carve</c> process: the <c>tools/list</c> it serves holds <c>help</c>, every description in it is
/// within <see cref="HelpToolTests.Limit"/>, and <c>help {"tool":"render"}</c> answers with render's reference.
/// </summary>
[Trait("Tier", "regression")]
public sealed class HelpThroughProcessTests
{
    [Fact]
    public async Task The_process_serves_help_and_keeps_every_description_under_the_cut()
    {
        using var process = new CarveProcess([]);
        await process.InitializeAsync();
        var tools = await process.ListToolsAsync();
        var lengths = tools.EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("description").GetString()!.Length);
        Assert.Contains("help", lengths.Keys);
        var over = lengths.Where(t => t.Value > HelpToolTests.Limit).Select(t => $"{t.Key} {t.Value}").ToList();
        Assert.True(over.Count == 0, $"over {HelpToolTests.Limit}: {string.Join(", ", over)}");

        var result = await process.CallToolAsync("help", """{"tool":"render"}""");
        Assert.False(CarveProcess.IsError(result));
        Assert.Equal(carve::Carve.Server.HelpTool.Entry("render"), CarveProcess.TextOf(result));

        var topic = await process.CallToolAsync("help", """{"topic":"warnings"}""");
        Assert.False(CarveProcess.IsError(topic));
        Assert.Equal(carve::Carve.Server.HelpTool.Topic("warnings"), CarveProcess.TextOf(topic));
    }
}
