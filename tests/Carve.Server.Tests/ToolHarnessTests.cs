using System.Diagnostics;
using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>The unit harness (T-87): carve's tools in this process, with no engine.</summary>
[Trait("Tier", "unit")]
public sealed class ToolHarnessTests : IDisposable
{
    internal const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"carve-harness-{Guid.NewGuid():N}");

    public ToolHarnessTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ListsEveryToolWithAClosedSchema()
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();

        var tools = await carve.ListToolsAsync();

        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("define_mesh", names);
        Assert.Contains("render", names);
        Assert.Equal(43, names.Count); // 38, T-207's help, T-93's define_part, get_parts, keep_level, and T-122's bake
        Assert.All(tools.EnumerateArray(), t => Assert.False(t.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean()));
    }

    [Fact]
    public async Task DefinesAMeshAndReadsItBack()
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();

        var defined = await carve.CallToolAsync("define_mesh", Cube);
        var mesh = await carve.CallToolAsync("get_mesh", "{}");

        Assert.False(IsError(defined), TextOf(defined));
        Assert.False(IsError(mesh), TextOf(mesh));
        using var read = JsonDocument.Parse(TextOf(mesh));
        Assert.Equal(8, read.RootElement.GetProperty("vertices").GetArrayLength());
        Assert.Equal(6, read.RootElement.GetProperty("faces").GetArrayLength());
        Assert.Equal(8, carve.Store.Current.Model.Meshes.Single().Mesh.LiveVertexCount);
    }

    [Fact]
    public async Task ATestsBadArgumentIsAToolErrorAsOnTheProcess()
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();

        var result = await carve.CallToolAsync("get_mesh", """{"nonsense":1}""");

        Assert.True(IsError(result));
        Assert.Contains("nonsense", TextOf(result));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"mode":"wireframe"}""")]
    [InlineData("""{"mode":"uvs"}""")]
    public async Task RenderRefusesAtOnceWithNoEngine(string arguments)
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var clock = Stopwatch.StartNew();
        var result = await carve.CallToolAsync("render", arguments);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"render took {clock.Elapsed}");
        Assert.True(IsError(result));
        Assert.Equal(ToolHarness.NoEngine, TextOf(result));
    }

    [Fact]
    public async Task ARelativePathResolvesAgainstTheHarnessFolder()
    {
        using var carve = new ToolHarness(workingDirectory: _root);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var saved = await carve.CallToolAsync("save", """{"path":"sub/cube.carve"}""");
        var opened = await carve.CallToolAsync("open", """{"path":"sub/cube.carve"}""");

        Assert.False(IsError(saved), TextOf(saved));
        Assert.False(IsError(opened), TextOf(opened));
        Assert.True(File.Exists(Path.Combine(_root, "sub", "cube.carve")));
        Assert.False(File.Exists(Path.Combine(Environment.CurrentDirectory, "sub", "cube.carve")));
    }
}

/// <summary>The harness against the real process: the same tool list and the same results, and how long each takes to start.</summary>
[Trait("Tier", "regression")]
public sealed class ToolHarnessMatchesProcessTests
{
    [Fact]
    public async Task ListsTheSameToolsAndAnswersTheSame()
    {
        var clock = Stopwatch.StartNew();
        using var process = new CarveProcess([]);
        await process.InitializeAsync();
        var processStart = clock.Elapsed;

        clock.Restart();
        using var harness = new ToolHarness();
        await harness.InitializeAsync();
        var harnessStart = clock.Elapsed;

        clock.Restart();
        using (var again = new ToolHarness())
        {
            await again.InitializeAsync();
        }
        var harnessAgain = clock.Elapsed;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"start: carve process {processStart.TotalMilliseconds:F0} ms, harness {harnessStart.TotalMilliseconds:F0} ms first, {harnessAgain.TotalMilliseconds:F0} ms again");

        Assert.Equal((await process.ListToolsAsync()).GetRawText(), (await harness.ListToolsAsync()).GetRawText());
        foreach (var (tool, arguments) in new[] { ("define_mesh", ToolHarnessTests.Cube), ("move", """{"faces":[1],"by":[0,0.5,0]}"""), ("get_mesh", "{}"), ("check", "{}"), ("undo", "{}") })
        {
            Assert.Equal((await process.CallToolAsync(tool, arguments)).GetRawText(), (await harness.CallToolAsync(tool, arguments)).GetRawText());
        }
    }
}
