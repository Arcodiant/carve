using System.Text.RegularExpressions;
using Carve.Debug;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Replay;

/// <summary>What the stages share: failure notes, calls, reading a script back, and the run folder's layout.</summary>
internal static class Replay
{
    public static readonly List<string> Failures = [];

    public static void Note(string message) => Failures.Add(message);

    public static void Equal<T>(string what, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) Note($"{what}: expected {expected}, got {actual}");
    }

    public static string Text(CallToolResult r) => r.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";

    /// <summary>A call that must succeed; a tool error is noted and the run goes on. Returns the text.</summary>
    public static async Task<string> Ok(ScenarioDriver d, string tool, object args)
    {
        var r = await d.CallAsync(tool, args);
        if (r.IsError is true) Note($"{tool}: {Text(r)}");
        return Text(r);
    }

    /// <summary>A call that must be a tool error naming each of <paramref name="mentions"/>. Returns the text.</summary>
    public static async Task<string> Refused(ScenarioDriver d, string tool, object args, params string[] mentions)
    {
        var r = await d.CallAsync(tool, args);
        var text = Text(r);
        if (r.IsError is not true) Note($"{tool}: expected a tool error, got: {text}");
        foreach (var m in mentions)
            if (!text.Contains(m)) Note($"{tool}: the error does not name '{m}': {text}");
        return text;
    }

    public static string Dir(ScenarioDriver d, string name)
    {
        var p = Path.Combine(d.RunFolder, name);
        Directory.CreateDirectory(p);
        return p;
    }

    /// <summary>Saves the session's script to <paramref name="path"/> and reads it back.</summary>
    public static async Task<string> Script(ScenarioDriver d, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Ok(d, "save_script", new { path });
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }

    /// <summary>The tool of each statement: a line that starts at the left edge with a name and a bracket.</summary>
    public static List<string> Tools(string script) =>
        Regex.Matches(script, @"^([a-z_]+)\(", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();

    /// <summary>The script cut into its statements, each with its own line ends.</summary>
    public static List<string> Statements(string script)
    {
        var starts = Regex.Matches(script, @"^[a-z_]+\(", RegexOptions.Multiline).Select(m => m.Index).Append(script.Length).ToList();
        return Enumerable.Range(0, starts.Count - 1).Select(i => script[starts[i]..starts[i + 1]]).ToList();
    }

    public static async Task<byte[]> Save(ScenarioDriver d, string path)
    {
        await Ok(d, "save", new { path });
        return File.Exists(path) ? await File.ReadAllBytesAsync(path) : [];
    }

    public static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);

    /// <summary>The repo's samples folder, found above the scenario's binaries.</summary>
    public static string Sample(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Carve.slnx not found above " + AppContext.BaseDirectory), "samples", relative);
    }

    public const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    public const string Squares =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,1,2,3],[4,7,6,5]]}""";

    /// <summary>A call's arguments as raw JSON, so every number goes out as written.</summary>
    public static System.Text.Json.JsonElement Json(string text) => System.Text.Json.JsonDocument.Parse(text).RootElement.Clone();

    /// <summary>define_mesh of <see cref="Cube"/> under <paramref name="name"/>.</summary>
    public static Task<string> DefineCube(ScenarioDriver d, string name) =>
        Ok(d, "define_mesh", Json(Cube.Replace("{\"vertices\"", $"{{\"name\":\"{name}\",\"vertices\"")));
}
