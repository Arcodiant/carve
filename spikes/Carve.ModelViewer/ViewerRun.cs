namespace Carve.ModelViewer;

/// <summary>How the run ended, read by <c>Program</c> once the loop has returned.</summary>
internal sealed class ViewerRun
{
    public int ExitCode { get; private set; }

    /// <summary>Records the first failure, on stderr.</summary>
    public void Fail(string message)
    {
        if (ExitCode == 0)
        {
            ExitCode = 1;
            Console.Error.WriteLine($"carve-viewer: {message}");
        }
    }
}
