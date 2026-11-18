using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>What <c>tools/list</c> says, for tests that run a description's own examples.</summary>
internal static class ToolList
{
    public static JsonElement Tool(JsonElement tools, string name) =>
        tools.EnumerateArray().Single(t => t.GetProperty("name").GetString() == name);

    public static string Description(JsonElement tools, string name) => Tool(tools, name).GetProperty("description").GetString()!;

    /// <summary>The cube, parsed out of define_mesh's own description.</summary>
    public static string Cube(JsonElement tools)
    {
        const string marker = "Worked example (a unit cube): ";
        var text = Description(tools, "define_mesh");
        return text[(text.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
    }

    /// <summary>The example calls in a description, in order: the rest of each line that starts with "Example: ".</summary>
    public static string[] Examples(JsonElement tools, string name) =>
        Description(tools, name).Split('\n').Where(l => l.StartsWith("Example: ", StringComparison.Ordinal)).Select(l => l["Example: ".Length..]).ToArray();
}

/// <summary>A test class with a temp folder of its own for each test, removed after it.</summary>
public abstract class TempFolderTests : IDisposable
{
    protected TempFolderTests(string prefix)
    {
        Root = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    protected string Root { get; }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }
}

/// <summary>A test class that calls a file tool, with its temp folder as the working directory: on <see cref="ToolHarness"/> through <c>StartAsync</c>, or through the real process through <c>StartProcessAsync</c> for a regression test.</summary>
public abstract class FileToolTests(string prefix) : TempFolderTests(prefix)
{
    /// <summary>Starts the harness and defines <paramref name="mesh"/>: the example cube by default, nothing when null.</summary>
    private protected async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync(string? mesh = "cube")
    {
        var carve = new ToolHarness(workingDirectory: Root);
        try
        {
            await carve.InitializeAsync();
            var tools = await carve.ListToolsAsync();
            if (mesh is not null) Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh == "cube" ? Cube(tools) : mesh)));
            return (carve, tools);
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    /// <summary>As <see cref="StartAsync"/> but through the real process, for a regression test that renders or needs the process.</summary>
    private protected async Task<(CarveProcess Carve, JsonElement Tools)> StartProcessAsync(string? mesh = "cube")
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        if (mesh is not null) Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh == "cube" ? Cube(tools) : mesh)));
        return (carve, tools);
    }

    private protected static string Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private protected static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }
}
