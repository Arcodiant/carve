using System.Globalization;

namespace Carve.Debug;

/// <summary>
/// Where a run keeps what it makes: <c>Carve:Debug:RunFolder</c>, or else the next number under
/// <c>artifacts/scenarios/&lt;scenario&gt;/</c>, relative to the working directory.
/// </summary>
internal sealed class RunFolder
{
    private RunFolder(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Frames = System.IO.Path.Combine(Path, "frames");
        UserData = System.IO.Path.Combine(Path, "user-data");
        Log = System.IO.Path.Combine(Path, "run.json");
    }

    public string Path { get; }

    public string Frames { get; }

    public string UserData { get; }

    public string Log { get; }

    /// <summary>
    /// Makes the folder. A folder named outright may be one a run has used, so what a run makes in
    /// it is cleared: only that, and anything else in it is left alone.
    /// </summary>
    public static RunFolder Create(string? configured, string scenario)
    {
        var folder = new RunFolder(configured is { Length: > 0 } ? configured : NextNumbered(scenario));
        foreach (var made in new[] { folder.Frames, folder.UserData })
        {
            if (Directory.Exists(made))
            {
                Directory.Delete(made, recursive: true);
            }

            Directory.CreateDirectory(made);
        }

        return folder;
    }

    private static string NextNumbered(string scenario)
    {
        var root = System.IO.Path.Combine("artifacts", "scenarios", scenario);
        var last = Directory.Exists(root)
            ? Directory.EnumerateDirectories(root)
                .Select(d => int.TryParse(System.IO.Path.GetFileName(d), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max()
            : 0;

        return System.IO.Path.Combine(root, (last + 1).ToString(CultureInfo.InvariantCulture));
    }
}
