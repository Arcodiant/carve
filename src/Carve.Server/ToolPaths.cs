namespace Carve.Server;

/// <summary>A file path a tool was given, resolved: the absolute path and its file name.</summary>
public sealed record ToolPath(string FullPath, string FileName)
{
    /// <summary>The file name without its extension (<c>crate.glb</c> gives <c>crate</c>).</summary>
    public string Stem => Path.GetFileNameWithoutExtension(FileName);

    /// <summary>Where a write went, as save's and export's results say it: the name, the absolute path, and whether it replaced a file.</summary>
    public string Written(bool replaced) => $"{FileName} to {FullPath}{(replaced ? " (replaced)" : "")}";
}

/// <summary>
/// The path rules every tool that writes or reads a file keeps (export, save, open). A relative path resolves against
/// carve's working directory and every result gives the absolute path; the extension is checked, case-insensitively;
/// an empty path or one with a character no file path may hold is a tool error. A write creates missing parent
/// folders, replaces an existing file (and says so), and goes through a temporary file in the same folder moved into
/// place, so a failed write leaves no partial file. Every error is one line that starts with the tool's name.
/// </summary>
public static class ToolPaths
{
    /// <summary>
    /// Resolves <paramref name="path"/> against <paramref name="baseDirectory"/> (carve's working directory when null)
    /// and checks that it ends in <paramref name="extension"/> (with its dot, e.g. <c>.glb</c>). Null, with
    /// <paramref name="error"/> set, when the path is empty, holds a character not allowed in a path, names a folder or
    /// has another extension.
    /// </summary>
    public static ToolPath? Resolve(string tool, string? path, string extension, out string? error, string? baseDirectory = null, string verb = "writes")
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = $"{tool}: path is empty; give a file path ending in {extension}";
            return null;
        }

        var head = $"{tool}: path \"{path}\"";
        string Holds(string bad) => $"{head} holds {bad}, which a file path may not";

        string full;
        try
        {
            if (BadPathCharacter(path) is { } bad)
            {
                error = Holds(bad);
                return null;
            }
            full = Path.GetFullPath(path, baseDirectory ?? Environment.CurrentDirectory);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"{head} is not a valid file path: {e.Message}";
            return null;
        }

        var name = Path.GetFileName(full);
        if (name.Length == 0 || path.EndsWith('/') || path.EndsWith('\\'))
        {
            error = $"{head} names a folder; give a file path ending in {extension}";
            return null;
        }
        if (BadSegmentCharacter(full) is { } badSegment)
        {
            error = Holds(badSegment);
            return null;
        }

        var actual = Path.GetExtension(name);
        if (!string.Equals(actual, extension, StringComparison.OrdinalIgnoreCase))
        {
            var found = actual.Length == 0 ? "has no extension" : $"ends in \"{actual}\"";
            error = $"{head} {found}; {tool} {verb} {extension} files, so the path must end in {extension}";
            return null;
        }

        return new ToolPath(full, name);
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> to the path: missing parent folders are created, an existing file is replaced
    /// (<paramref name="replaced"/> says so), and the bytes go to a temporary file in the same folder that is then moved
    /// into place, so a failure leaves the old file, or none, and never a partial one. False, with
    /// <paramref name="error"/> naming the path and the system's message, on an I/O failure. With
    /// <paramref name="overwrite"/> false an existing file is refused, naming the path and <c>overwrite</c>, and left
    /// byte for byte as it was; that holds for a file that appears between the check and the move too (another call or
    /// another process writing the same path), since the move itself then refuses to replace it. Each write has a temp
    /// file of its own, so two writes at once never share one.
    /// </summary>
    public static bool Write(string tool, ToolPath path, ReadOnlySpan<byte> bytes, out bool replaced, out string? error, bool overwrite = true)
    {
        replaced = false;
        error = null;
        var folder = Path.GetDirectoryName(path.FullPath)!;
        var temp = Path.Combine(folder, $".{path.FileName}.{Environment.ProcessId}.{Interlocked.Increment(ref _writes)}.tmp");
        try
        {
            if (Directory.Exists(path.FullPath))
            {
                error = $"{tool}: could not write {path.FullPath}: it is a folder";
                return false;
            }
            replaced = File.Exists(path.FullPath);
            if (replaced && !overwrite)
            {
                replaced = false;
                error = Exists(tool, path);
                return false;
            }
            Directory.CreateDirectory(folder);
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true); // on disk before the move, so a crash after it cannot leave the new name empty
                }

                File.Move(temp, path.FullPath, overwrite);
            }
            catch (IOException) when (!overwrite && File.Exists(path.FullPath))
            {
                // Another writer made the file after the check: refused as if it had been there, and left as it is.
                TryDelete(temp);
                replaced = false;
                error = Exists(tool, path);
                return false;
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            replaced = false;
            error = $"{tool}: could not write {path.FullPath}: {e.Message}";
            return false;
        }
    }

    /// <summary>Counts writes, to give each its own temp file.</summary>
    private static long _writes;

    private static string Exists(string tool, ToolPath path) =>
        $"{tool}: {path.FullPath} already exists; pass overwrite: true to replace it. Nothing was written.";

    private static void TryDelete(string file)
    {
        try { File.Delete(file); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Why <paramref name="folder"/>, resolved against <paramref name="baseDirectory"/> as <see cref="Resolve"/> resolves a file's
    /// path, cannot be a folder: "holds the character '*', which a folder path may not" (in the words <see cref="Resolve"/> uses
    /// for a file) or "is not a valid folder path: ...", or null. For a tool that writes several files into one folder, so a bad
    /// folder is one problem and not one per file.
    /// </summary>
    public static string? FolderProblem(string folder, string? baseDirectory = null)
    {
        try
        {
            if (BadPathCharacter(folder) is { } bad) return $"holds {bad}, which a folder path may not";
            var full = Path.GetFullPath(folder, baseDirectory ?? Environment.CurrentDirectory);
            return BadSegmentCharacter(full) is { } badSegment ? $"holds {badSegment}, which a folder path may not" : null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"is not a valid folder path: {e.Message}";
        }
    }

    /// <summary>The first character a path may not hold, described, or null.</summary>
    private static string? BadPathCharacter(string path) => Describe(path.IndexOfAny(Path.GetInvalidPathChars()), path);

    /// <summary>
    /// The same, with each folder and the file name past the root of an absolute path judged as a file name, so <c>*</c>,
    /// <c>?</c> or a second <c>:</c> is caught too.
    /// </summary>
    private static string? BadSegmentCharacter(string path)
    {
        var rest = path[(Path.GetPathRoot(path)?.Length ?? 0)..];
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var segment in rest.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (Describe(segment.IndexOfAny(invalid), segment) is { } bad) return bad;
        }
        return null;
    }

    private static string? Describe(int at, string text)
    {
        if (at < 0) return null;
        var c = text[at];
        return char.IsControl(c) ? $"the control character U+{(int)c:X4}" : $"the character '{c}'";
    }
}
