using System.Globalization;
using System.Text.Json;
using Carve.Core.Scripts;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>
/// run_script (M-34, T-233): runs a .carvescript's statements as tool calls on the current model, all or nothing, as one
/// undo step. Each statement is the served <see cref="McpServerTool"/> itself, invoked in-process (<see cref="RunStatements"/>).
/// </summary>
public sealed class RunScriptTool(ModelStore store, ToolFolder toolFolder)
{
    /// <summary>
    /// The tools a statement may not be, each with why: everything that pushes no step (T-231's journalling rule), and
    /// run_script itself. Explicit, since a tool's pushing is only known by running it; a test holds it to tools/list.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Refused = new Dictionary<string, string>
    {
        ["run_script"] = "one script does not run another",
        ["undo"] = "a script's steps are taken back together, by undo after run_script",
        ["save"] = Nothing, ["save_script"] = Nothing, ["export"] = Nothing, ["bake"] = Nothing, ["render"] = Nothing,
        ["render_glb"] = Nothing, ["select"] = Nothing, ["check"] = Nothing, ["help"] = Nothing, ["get_mesh"] = Nothing,
        ["get_parts"] = Nothing, ["get_skeleton"] = Nothing, ["get_weights"] = Nothing, ["get_clips"] = Nothing,
    };

    private const string Nothing = "it changes nothing in the model";

    /// <summary>The worked example's script file, LF line ends; a test writes it beside itself and runs it as written.</summary>
    public const string WorkedFile =
        "define_mesh(vertices: [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]], faces: [[0, 1, 2, 3]])\n" +
        "extrude(faces: [0], distance: 0.5)\n";

    public const string WorkedCall = """{"path":"slab.carvescript"}""";

    public const string WorkedResult = """{"statements":2,"meshes":[{"name":"mesh","vertices":8,"faces":5,"edges":12,"closed":false,"bounds":{"min":[0,0,0],"max":[1,1,0.5]},"warnings":["4 edges belong to only one face: e.g. edges[0-1], edges[0-3], edges[1-2]; select edges where open names them"]}]}""";

    public const string Text =
        "Runs a .carvescript file (the format save_script writes; help {\"topic\":\"scripts\"}) on the current model: each statement is a call of its tool, in order, with the same argument checks and refusals as a call made directly. "
        + "It acts on whatever the model is now, so a script that starts with clear() or open(...) rebuilds the model whole, and one that does not adds to it. "
        + "A relative file argument in a statement (open's path, define_target's file, define_material's textures and graphs, displace's graph) is resolved against the script's folder, not carve's working directory. "
        + "All or nothing: when a statement's tool refuses or fails, the result is a tool error naming its line, the tool and the tool's own message, and the model, undo history, the list of calls save_script writes and the model's version are as before the call. "
        + "A malformed statement is a tool error naming its line and column, and nothing runs. "
        + "A statement may be any tool that changes the model; run_script, undo, save, save_script, export, bake, render, render_glb, select, check, help and the get_ tools are refused, naming the line, before anything runs. "
        + "A run is one undo step (undo answers undone \"run_script\") and the list of calls gets the script's statements, not a run_script call, so save_script afterwards writes them. "
        + "Takes path (required, ending in .carvescript, absolute or relative to carve's working directory) and verbosity. "
        + "Returns statements (how many ran) and meshes, each with name, vertices, faces, edges, closed, bounds and warnings, never the geometry ({\"ok\":true} at silent). "
        + "Worked example, on an empty model, with slab.carvescript holding:\n"
        + WorkedFile
        + "it returns " + WorkedResult + "\n"
        + ExampleMarker + WorkedCall;

    public static McpServerTool Create() => MeshToolSupport.Create<RunScriptTool>("run_script", Text, WithVerbosity(PathSchema("The .carvescript file to run, absolute or relative to carve's working directory.")));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("run_script", context, "path", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var text = RequiredString("path", path, errors);
        if (errors.Count > 0) return Failed("run_script", errors);

        var target = ToolPaths.Resolve("run_script", text, SaveScriptTool.Extension, out var pathError, toolFolder.Directory, verb: "reads");
        if (target is null) return Error(pathError!);
        if (!File.Exists(target.FullPath)) return Error($"run_script: there is no file {target.FullPath}");
        string content;
        try { content = File.ReadAllText(target.FullPath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Error($"run_script: could not read {target.FullPath}: {e.Message}");
        }

        var unchanged = $"; nothing of {target.FullPath} was kept, the model is as before the call";
        var (script, formatError) = ScriptReader.Read(content);
        if (script is null) return Error($"run_script: {target.FullPath}: {formatError}; nothing ran");

        var ran = 0;
        var pushed = store.Batch("run_script", () => RunStatements(context.Services!, context.Server, context.JsonRpcRequest, Path.GetDirectoryName(target.FullPath)!, script, out ran), out var problem);
        if (pushed is null)
            return Error(problem! + unchanged);

        var body = new Dictionary<string, object>
        {
            ["statements"] = ran,
            ["meshes"] = Reports(pushed.Model.Meshes, level),
        };
        return Quiet(Ok(body), level);
    }

    /// <summary>
    /// The runner (for M-35 to build on): checks every statement names a tool a script may run, then calls each in order
    /// as the served <see cref="McpServerTool"/>, with <see cref="ToolFolder"/> set to <paramref name="folder"/> so a
    /// relative file argument resolves against it. Null when all succeeded; otherwise the first failure, naming its line.
    /// <paramref name="ran"/> is how many statements succeeded. Must run inside <see cref="ModelStore.Batch"/> to be all or nothing.
    /// </summary>
    public static string? RunStatements(IServiceProvider services, McpServer server, JsonRpcRequest request, string folder, CarveScript script, out int ran)
    {
        ran = 0;
        var tools = services.GetServices<McpServerTool>().ToDictionary(t => t.ProtocolTool.Name);
        foreach (var s in script.Statements)
        {
            if (Refused.TryGetValue(s.Tool, out var why)) return $"line {Line(s)}: {s.Tool} is not a statement a script can run: {why}";
            if (!tools.ContainsKey(s.Tool)) return $"line {Line(s)}: carve has no tool '{s.Tool}'";
        }

        var scoped = new FolderServices(services, new ToolFolder(folder));
        foreach (var s in script.Statements)
        {
            var tool = tools[s.Tool];
            var call = new RequestContext<CallToolRequestParams>(server, request, new CallToolRequestParams
            {
                Name = s.Tool,
                Arguments = s.Arguments.ToDictionary(a => a.Name, a => JsonSerializer.SerializeToElement(a.Value, Compact)),
            })
            { Services = scoped, MatchedPrimitive = tool };
            CallToolResult result;
            try
            {
                var pending = tool.InvokeAsync(call);
                // The batch holds the store's lock on this thread; every statement tool is synchronous (the async ones, render and render_glb, are refused).
                if (!pending.IsCompleted) throw new InvalidOperationException($"{s.Tool} did not finish synchronously, which a script statement must");
                result = pending.GetAwaiter().GetResult();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return $"line {Line(s)}: {s.Tool}: {e.Message}";
            }
            if (result.IsError == true)
            {
                var message = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(b => b.Text));
                return $"line {Line(s)}: " + (message.StartsWith(s.Tool + " ", StringComparison.Ordinal) || message.StartsWith(s.Tool + ":", StringComparison.Ordinal) ? message : $"{s.Tool}: {message}");
            }
            ran++;
        }
        return null;
    }

    // A value as an MCP client sends it, with the script's spacing and line breaks gone (numbers keep their text), so the journal's entry reads as a direct call's.
    private static readonly JsonSerializerOptions Compact = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Line(ScriptStatement s) => s.Line.ToString(CultureInfo.InvariantCulture);

    /// <summary>The server's services with <see cref="ToolFolder"/> replaced: the one place a statement's file base changes.</summary>
    private sealed class FolderServices(IServiceProvider inner, ToolFolder folder) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ToolFolder) ? folder : inner.GetService(serviceType);
    }
}
