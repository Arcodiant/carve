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
    /// it is cleared, but only where a <c>run.json</c> shows a run used it: only <c>frames/</c> and
    /// <c>user-data/</c>, and anything else in it is left alone. Without a <c>run.json</c>, either
    /// of them that exists and is not empty is refused, since it may be someone else's.
    /// </summary>
    public static RunFolder Create(string? configured, string scenario)
    {
        var folder = new RunFolder(configured is { Length: > 0 } ? configured : NextNumbered(scenario));
        var used = File.Exists(folder.Log);
        foreach (var made in new[] { folder.Frames, folder.UserData })
        {
            if (Directory.Exists(made))
            {
                if (!used && Directory.EnumerateFileSystemEntries(made).Any())
                {
                    throw new InvalidOperationException(
                        $"the run folder {folder.Path} has a non-empty '{System.IO.Path.GetFileName(made)}' but no run.json, so no run is known to have used it; not clearing it.");
                }

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
