using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Carve.Core.Scripts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>save_script (M-34, T-232): writes the session's journal as a .carvescript, file arguments relative to the script.</summary>
public sealed class SaveScriptTool(ModelStore store, ToolFolder toolFolder)
{
    public const string Extension = ".carvescript";

    /// <summary>The worked example's calls, on define_mesh's unit cube; a test runs them as written.</summary>
    public const string WorkedExtrude = """{"mesh":"mesh","faces":[1],"distance":0.5}""";
    public const string WorkedCall = """{"path":"cube.carvescript"}""";

    /// <summary>What the worked example writes, LF line ends whatever the checkout's.</summary>
    public const string WorkedFile =
        "define_mesh(\n" +
        "  vertices: [\n" +
        "    [0, 0, 0],\n" +
        "    [1, 0, 0],\n" +
        "    [1, 1, 0],\n" +
        "    [0, 1, 0],\n" +
        "    [0, 0, 1],\n" +
        "    [1, 0, 1],\n" +
        "    [1, 1, 1],\n" +
        "    [0, 1, 1]\n" +
        "  ],\n" +
        "  faces: [\n" +
        "    [0, 3, 2, 1],\n" +
        "    [4, 5, 6, 7],\n" +
        "    [0, 1, 5, 4],\n" +
        "    [3, 7, 6, 2],\n" +
        "    [0, 4, 7, 3],\n" +
        "    [1, 2, 6, 5]\n" +
        "  ]\n" +
        ")\n" +
        "extrude(mesh: \"mesh\", faces: [1], distance: 0.5)\n";

    public const string Text =
        "Writes what this session did to the model as a .carvescript file, one statement per call that made an undo step (define_mesh, the edits, open, clear, the looks that change the model; never save, export, bake, check or a query), in order, tool(name: value, ...) with each value the JSON the call sent. "
        + "A person or an agent can read and edit it. Takes path (required, ending in .carvescript, resolved and written as save's is) and verbosity. "
        + "It changes nothing in the model and is not itself written. "
        + "File arguments (open's path, define_target's file, define_material's textures and graphs, displace's graph) are written as the path from the script's folder to the file, with / separators; a file with no such path (another drive) refuses the whole save, naming the call, its place in the list and the argument, and writes nothing. "
        + "Ids are literal, as the calls gave them: an earlier edit that changes the topology shifts the ids a later statement names. undo takes its call out of the list; clear and a whole-model open start the list again, so a script after one starts with it, and a fresh session's script starts with no clear(). "
        + "An empty list writes an empty file and the result says so. Returns the path, the number of statements and of lines ({\"ok\":true} at silent). "
        + "Worked example, on define_mesh's unit cube, then extrude of face 1, then this call, which writes:\n"
        + WorkedFile
        + ExampleMarker + WorkedCall + "\n"
        + """More: help {"topic":"scripts"} gives the statement format, the path rule and what is written.""";

    public static McpServerTool Create() => MeshToolSupport.Create<SaveScriptTool>("save_script", Text, WithVerbosity(PathSchema("The .carvescript file to write, absolute or relative to carve's working directory; replaced if it exists.")));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("save_script", context, "path", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var text = RequiredString("path", path, errors);
        if (errors.Count > 0) return Failed("save_script", errors);

        var target = ToolPaths.Resolve("save_script", text, Extension, out var pathError, toolFolder.Directory);
        if (target is null) return Error(pathError!);

        var folder = Path.GetDirectoryName(target.FullPath)!;
        var journal = store.Journal;
        var statements = new List<ScriptStatement>();
        for (var i = 0; i < journal.Count; i++)
        {
            if (Relative(journal[i], folder, i + 1, out var statement, out var problem)) statements.Add(statement!);
            else return Error(problem!);
        }

        var script = ScriptWriter.Write(new CarveScript(statements));
        if (!ToolPaths.Write("save_script", target, new UTF8Encoding(false).GetBytes(script), out var replaced, out var writeError)) return Error(writeError!);

        var lines = script.Count(c => c == '\n');
        var inv = CultureInfo.InvariantCulture;
        var counts = statements.Count == 0 ? "no statements (the session has made no step), an empty file" : $"{statements.Count.ToString(inv)} statements, {lines.ToString(inv)} lines";
        return Quiet(Line($"saved {target.Written(replaced)}: {counts}"), level);
    }

    /// <summary>The entry as a statement with each file argument written relative to <paramref name="folder"/>; false, with the refusal, when one has no relative path.</summary>
    private static bool Relative(JournalEntry entry, string folder, int place, out ScriptStatement? statement, out string? problem)
    {
        var arguments = entry.Arguments.Select(a => (a.Key, a.Value)).ToList();
        statement = null;
        problem = null;
        foreach (var (at, full) in entry.Files)
        {
            var relative = Path.GetRelativePath(folder, full);
            if (Path.IsPathRooted(relative))
            {
                problem = $"save_script: statement {place.ToString(CultureInfo.InvariantCulture)} ({entry.Tool}), argument {at}: the file '{full}' has no path relative to '{folder}' (it is on another drive); save the script on the file's drive, or copy the file next to the script and call again";
                return false;
            }
            relative = relative.Replace(Path.DirectorySeparatorChar, '/');
            var parts = at.Split('.');
            var index = arguments.FindIndex(a => a.Key == parts[0]);
            arguments[index] = (parts[0], parts.Length == 1 ? Json(w => w.WriteStringValue(relative)) : Json(w => Replace(arguments[index].Value, parts[1], relative, w)));
        }
        statement = new ScriptStatement(entry.Tool, arguments.Select(a => (a.Key, a.Value)).ToList());
        return true;
    }

    private static void Replace(JsonElement obj, string property, string value, Utf8JsonWriter w)
    {
        w.WriteStartObject();
        foreach (var p in obj.EnumerateObject())
        {
            w.WritePropertyName(p.Name);
            if (p.Name == property) w.WriteStringValue(value); else p.Value.WriteTo(w);
        }
        w.WriteEndObject();
    }

    private static JsonElement Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer)) write(w);
        return JsonDocument.Parse(buffer.WrittenMemory).RootElement.Clone();
    }
}
