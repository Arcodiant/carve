namespace Carve.Server.Tests;

// The two names the linked ToolHarness.cs takes from CarveProcess.cs, which this project does not compile.
internal interface IToolCaller
{
    Task<System.Text.Json.JsonElement> CallToolAsync(string name, string arguments);
}

internal static class CarveProcess
{
    public static readonly TimeSpan Answer = TimeSpan.FromSeconds(30);
}
