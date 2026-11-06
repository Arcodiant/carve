using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class SaveTool(ModelStore store)
{
    public const string Extension = ".carve";

    /// <summary>The worked example's calls; the regression test runs them as written.</summary>
    public const string WorkedSave = """{"path":"crate.carve"}""";
    public const string WorkedOpen = """{"path":"crate.carve"}""";
    public const string WorkedExport = """{"path":"<game>/Content/models/crate.glb"}""";

    /// <summary>What save writes for the unit cube (T-15's file).</summary>
    public const string CubeFile = """
        {
          "carve": 1,
          "vertices": [
            [0, 0, 0],
            [1, 0, 0],
            [1, 1, 0],
            [0, 1, 0],
            [0, 0, 1],
            [1, 0, 1],
            [1, 1, 1],
            [0, 1, 1]
          ],
          "faces": [
            [0, 3, 2, 1],
            [4, 5, 6, 7],
            [0, 1, 5, 4],
            [3, 7, 6, 2],
            [0, 4, 7, 3],
            [1, 2, 6, 5]
          ]
        }
        """;

    public const string Text =
        "Writes the current mesh to a .carve file, so the work outlives this session; open reads it back, in this carve process or a later one. Takes path, the file to write. "
        + "The .carve file is the one to keep working in: it holds the polygons exactly, every vertex and face in its id order and every coordinate to the bit, so after open the ids you used before still name the same vertices and faces. "
        + "It does not hold the undo history (open starts from the file as one step), a current file name (carve remembers none: give the path every time) or any render setting. "
        + "The .glb that export writes is only for the game, and has no faces to edit. "
        + "The file is versioned JSON, one vertex or face per line, and the same mesh always gives the same bytes. The unit cube from define_mesh is saved as:\n"
        + CubeFile + "\n"
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; missing folders are created; "
        + "an existing file is replaced and the result says (replaced); the path must end in .carve, in any case. "
        + "Errors, each a tool error that writes nothing: no mesh yet (call define_mesh first); an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .carve; a write that fails (access denied, a locked file), naming the path and the system's message, "
        + "which leaves any file already there as it was and no partial file. "
        + "save changes nothing: not the mesh, its ids, or what undo steps back to. "
        + "Returns one line: saved <file> to <absolute path> (replaced, when it was): <n> vertices, <n> faces.\n"
        + "Worked example, from an empty file to a game's content folder (put the game's folder for <game>); each numbered line is a call, and the game then loads the model as "
        + "models.LoadModel(new ContentPath(\"models/crate.glb\")):\n"
        + "1. define_mesh with the cube from its description.\n"
        + "2. Edit it: move and extrude.\n"
        + "3. save " + WorkedSave + "\n"
        + "4. In a later session, open " + WorkedOpen + "\n"
        + "5. export " + WorkedExport;

    public static McpServerTool Create() => MeshToolSupport.Create<SaveTool>("save", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject
            {
                ["description"] = "The .carve file to write, absolute or relative to carve's working directory; replaced if it exists.",
                ["type"] = "string",
            },
        },
        ["required"] = new JsonArray("path"),
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null)
    {
        var errors = UnknownArguments("save", context, "path");
        var text = PathArgument(path, errors);
        if (errors.Count > 0) return Failed("save", errors);

        var mesh = store.Current.Mesh;
        if (mesh is null) return Error("save: " + NoMesh);

        var target = ToolPaths.Resolve("save", text, Extension, out var pathError);
        if (target is null) return Error(pathError!);

        var bytes = new UTF8Encoding(false).GetBytes(CarveFile.Write(mesh));
        if (!ToolPaths.Write("save", target, bytes, out var replaced, out var writeError)) return Error(writeError!);

        var inv = CultureInfo.InvariantCulture;
        var report = MeshReport.Create(mesh);
        return new CallToolResult
        {
            Content = [new TextContentBlock
            {
                Text = $"saved {target.FileName} to {target.FullPath}{(replaced ? " (replaced)" : "")}: "
                    + $"{report.VertexCount.ToString(inv)} vertices, {report.FaceCount.ToString(inv)} faces",
            }],
        };
    }
}
