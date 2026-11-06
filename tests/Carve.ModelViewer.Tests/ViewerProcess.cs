using System.Diagnostics;

namespace Carve.ModelViewer.Tests;

/// <summary>What one run of <c>run-viewer.ps1</c> left.</summary>
internal sealed record ViewerResult(int ExitCode, string Output, string Out, string WorkingFolder);

/// <summary>Runs the viewer as T-19's suite will: through the script, built already.</summary>
internal static class ViewerProcess
{
    public static string RepoRoot { get; } = FindRepoRoot();

    private static string Configuration => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    /// <summary>From an empty working folder of its own, so that anything written there shows.</summary>
    public static ViewerResult Run(string content, string model, string output, params string[] views)
    {
        var working = Directory.CreateTempSubdirectory("carve-viewer-cwd-").FullName;
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = working,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in new[]
                 {
                     "-NoProfile", "-File", Path.Combine(RepoRoot, "scripts/run-viewer.ps1"),
                     content, model, output, "-NoBuild", "-Configuration", Configuration,
                 }.Concat(views))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new ViewerResult(process.ExitCode, stdout.Result + stderr.Result, output, working);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Carve.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("no Carve.slnx above the test output");
    }
}
