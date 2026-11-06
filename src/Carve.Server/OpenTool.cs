using System.Globalization;
using System.Text.Json;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class OpenTool(ModelStore store)
{
    /// <summary>How many .carve files a missing-file error lists before it counts the rest.</summary>
    private const int ListLimit = 20;

    public const string Text =
        "Reads a .carve file (written by save) and makes its mesh the current mesh, in one step like define_mesh: it REPLACES the current mesh, "
        + "it does not add to it, and one undo takes you back to the mesh before. Takes path, the file to read. "
        + "The file holds the polygons exactly, with the ids they were saved with: vertex 0 is the file's first vertex and face 0 its first face, "
        + "so the ids you used before the save still name the same vertices and faces. It does not hold the undo history, so undo cannot go back past the open, "
        + "and carve keeps no current file: give the path to open and to save every time. "
        + "The .carve file is the one to keep working in; the .glb that export writes is only for the game and cannot be opened. "
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; the path must end in .carve, in any case. "
        + "Returns define_mesh's JSON report ({vertices, faces, edges, closed, bounds, warnings}) preceded by path, the absolute path read. "
        + "Errors, each a tool error that leaves the current mesh, its ids and undo as they were: an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .carve; a file that does not exist, which names the absolute path looked for and lists the .carve files in that folder "
        + "(at most 20, sorted, and a count of the rest), or says there are none or that the folder does not exist, so correct the path from that list; "
        + "a file that cannot be read (access denied, a locked file); a file that is not a Carve file: not JSON (with line and column), an unknown version, "
        + "or a bad vertex or face, each problem listed by element, e.g. faces[3][3]. "
        + "The whole sequence from an empty file to a game is the worked example in save's description.\n"
        + ExampleMarker + SaveTool.WorkedFile;

    public static McpServerTool Create() => MeshToolSupport.Create<OpenTool>("open", Text, PathSchema("The .carve file to read, absolute or relative to carve's working directory."));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null)
    {
        if (PathArguments("open", context, path, out var text) is { } refused) return refused;

        var target = ToolPaths.Resolve("open", text, SaveTool.Extension, out var pathError, verb: "reads");
        if (target is null) return Error(pathError!);

        if (Directory.Exists(target.FullPath)) return Error($"open: {target.FullPath} is a folder, not a file");
        if (!File.Exists(target.FullPath)) return Error(Missing(target));

        string content;
        try { content = File.ReadAllText(target.FullPath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Error($"open: could not read {target.FullPath}: {e.Message}");
        }

        var read = CarveFile.Read(content);
        if (!read.Succeeded)
        {
            var problems = read.Errors.Select(x => x.ToString()).ToList();
            return Error($"open failed with {problems.Count} problem(s) in {target.FullPath}; the current mesh is unchanged:\n" + string.Join("\n", problems));
        }

        store.Push(read.Mesh!, "open");
        var body = new Dictionary<string, object> { ["path"] = target.FullPath };
        foreach (var (key, value) in Report(read.Mesh!)) body[key] = value;
        return Ok(body);
    }

    /// <summary>The missing-file error: the resolved path and the .carve files that are in its folder.</summary>
    private static string Missing(ToolPath target)
    {
        var folder = Path.GetDirectoryName(target.FullPath)!;
        var head = $"open: no file at {target.FullPath}; ";
        if (!Directory.Exists(folder)) return head + $"the folder {folder} does not exist";

        List<string> names;
        try
        {
            names = Directory.EnumerateFiles(folder)
                .Select(f => Path.GetFileName(f)!)
                .Where(n => n.EndsWith(SaveTool.Extension, StringComparison.OrdinalIgnoreCase) && !n.StartsWith('.'))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return head + $"the folder {folder} could not be listed: {e.Message}";
        }

        var inv = CultureInfo.InvariantCulture;
        if (names.Count == 0) return head + $"there are no {SaveTool.Extension} files in {folder}";
        var shown = string.Join(", ", names.Take(ListLimit));
        var rest = names.Count > ListLimit ? $", and {(names.Count - ListLimit).ToString(inv)} more" : "";
        return head + $"the {SaveTool.Extension} files in {folder} ({names.Count.ToString(inv)}) are: {shown}{rest}";
    }
}
