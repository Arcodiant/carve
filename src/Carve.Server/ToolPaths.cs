namespace Carve.Server;

/// <summary>A file path a tool was given, resolved: the absolute path and its file name.</summary>
public sealed record ToolPath(string FullPath, string FileName)
{
    /// <summary>The file name without its extension (<c>crate.glb</c> gives <c>crate</c>).</summary>
    public string Stem => Path.GetFileNameWithoutExtension(FileName);
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

        string full;
        try
        {
            if (BadCharacter(path) is { } bad)
            {
                error = $"{tool}: path \"{path}\" holds {bad}, which a file path may not";
                return null;
            }
            full = Path.GetFullPath(path, baseDirectory ?? Environment.CurrentDirectory);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"{tool}: path \"{path}\" is not a valid file path: {e.Message}";
            return null;
        }

        var name = Path.GetFileName(full);
        if (name.Length == 0 || path.EndsWith('/') || path.EndsWith('\\'))
        {
            error = $"{tool}: path \"{path}\" names a folder; give a file path ending in {extension}";
            return null;
        }
        if (BadCharacter(full, segments: true) is { } badSegment)
        {
            error = $"{tool}: path \"{path}\" holds {badSegment}, which a file path may not";
            return null;
        }

        var actual = Path.GetExtension(name);
        if (!string.Equals(actual, extension, StringComparison.OrdinalIgnoreCase))
        {
            error = actual.Length == 0
                ? $"{tool}: path \"{path}\" has no extension; {tool} {verb} {extension} files, so the path must end in {extension}"
                : $"{tool}: path \"{path}\" ends in \"{actual}\"; {tool} {verb} {extension} files, so the path must end in {extension}";
            return null;
        }

        return new ToolPath(full, name);
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> to the path: missing parent folders are created, an existing file is replaced
    /// (<paramref name="replaced"/> says so), and the bytes go to a temporary file in the same folder that is then moved
    /// into place, so a failure leaves the old file, or none, and never a partial one. False, with
    /// <paramref name="error"/> naming the path and the system's message, on an I/O failure.
    /// </summary>
    public static bool Write(string tool, ToolPath path, ReadOnlySpan<byte> bytes, out bool replaced, out string? error)
    {
        replaced = false;
        error = null;
        var folder = Path.GetDirectoryName(path.FullPath)!;
        var temp = Path.Combine(folder, $".{path.FileName}.{Environment.ProcessId}.tmp");
        try
        {
            if (Directory.Exists(path.FullPath))
            {
                error = $"{tool}: could not write {path.FullPath}: it is a folder";
                return false;
            }
            Directory.CreateDirectory(folder);
            replaced = File.Exists(path.FullPath);
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    stream.Write(bytes);
                File.Move(temp, path.FullPath, overwrite: true);
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

    private static void TryDelete(string file)
    {
        try { File.Delete(file); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The first character a path may not hold, described, or null. With <paramref name="segments"/> each folder and the
    /// file name past the root of an absolute path is judged as a file name, so <c>*</c>, <c>?</c> or a second <c>:</c> is
    /// caught too.
    /// </summary>
    private static string? BadCharacter(string path, bool segments = false)
    {
        if (!segments) return Describe(path.IndexOfAny(Path.GetInvalidPathChars()), path);

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
