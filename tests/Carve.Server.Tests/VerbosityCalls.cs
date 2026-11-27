using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>What the verbosity tests share (M-23): adding the argument to a call, the text of a result, the silent answer, a row of lifted quads.</summary>
internal static class VerbosityCalls
{
    public static string With(string arguments, string verbosity) =>
        arguments.TrimEnd()[..^1] + (arguments.Trim() == "{}" ? "" : ",") + $"\"verbosity\":\"{verbosity}\"}}";

    public static string Text(JsonElement result) =>
        string.Concat(result.GetProperty("content").EnumerateArray().Select(c => c.GetProperty("text").GetString()));

    /// <summary>What <paramref name="tool"/> answers at silent: {"ok":true}, but check answers its own ok.</summary>
    public static string Silent(string tool, bool ok) => tool == "check" ? $"{{\"ok\":{(ok ? "true" : "false")}}}" : """{"ok":true}""";

    /// <summary>A row of <paramref name="quads"/> separate quads, each with one corner lifted off its plane.</summary>
    public static string Row(int quads)
    {
        var vertices = Enumerable.Range(0, quads).SelectMany(i => new[] { [i * 2, 0, 0], [i * 2 + 1, 0, 0], [i * 2 + 1, 1, 0], new float[] { i * 2, 1, 0.5f } }).ToArray();
        var faces = Enumerable.Range(0, quads).Select(i => new[] { i * 4, i * 4 + 1, i * 4 + 2, i * 4 + 3 });
        return JsonSerializer.Serialize(new { vertices, faces });
    }
}
