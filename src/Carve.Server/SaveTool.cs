using System.Globalization;
using System.Text;
using System.Text.Json;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class SaveTool(ModelStore store)
{
    public const string Extension = ".carve";

    /// <summary>The worked example's calls; the regression test runs them as written.</summary>
    public const string WorkedFile = """{"path":"crate.carve"}""";
    public const string WorkedExport = """{"path":"<game>/Content/models/crate.glb"}""";

    /// <summary>
    /// What save writes for the unit cube (T-15's file), as <c>CarveFile.Write</c> gives it: LF line ends whatever the
    /// checkout's, which is why it is not a raw string. A test holds it equal to the writer's output.
    /// </summary>
    public const string CubeFile =
        "{\n" +
        "  \"carve\": 1,\n" +
        "  \"vertices\": [\n" +
        "    [0, 0, 0],\n" +
        "    [1, 0, 0],\n" +
        "    [1, 1, 0],\n" +
        "    [0, 1, 0],\n" +
        "    [0, 0, 1],\n" +
        "    [1, 0, 1],\n" +
        "    [1, 1, 1],\n" +
        "    [0, 1, 1]\n" +
        "  ],\n" +
        "  \"faces\": [\n" +
        "    [0, 3, 2, 1],\n" +
        "    [4, 5, 6, 7],\n" +
        "    [0, 1, 5, 4],\n" +
        "    [3, 7, 6, 2],\n" +
        "    [0, 4, 7, 3],\n" +
        "    [1, 2, 6, 5]\n" +
        "  ]\n" +
        "}\n";

    public const string Text =
        "Writes the current mesh to a .carve file, so the work outlives this session; open reads it back, in this carve process or a later one. Takes path, the file to write. "
        + "The .carve file is the one to keep working in: it holds the polygons exactly, every vertex and face in its id order and every coordinate to the bit, so after open the ids you used before still name the same vertices and faces. "
        + "It does not hold the undo history (open starts from the file as one step), a current file name (carve remembers none: give the path every time) or any render setting. "
        + "The .glb that export writes is only for the game, and has no faces to edit. "
        + "The file is versioned JSON, one vertex or face per line, and the same mesh always gives the same bytes. The unit cube from define_mesh is saved as:\n"
        + CubeFile
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; missing folders are created; "
        + "an existing file is replaced and the result says (replaced); the path must end in .carve, in any case. "
        + "Errors, each a tool error that writes nothing: no mesh yet (call define_mesh or open first); an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .carve; a write that fails (access denied, a locked file), naming the path and the system's message, "
        + "which leaves any file already there as it was and no partial file. "
        + "save changes nothing: not the mesh, its ids, or what undo steps back to. "
        + "Returns one line: saved <file> to <absolute path> (replaced, when it was): <n> vertices, <n> faces.\n"
        + "Worked example, from an empty file to a game's content folder (put the game's folder for <game>); each numbered line is a call, and the game then loads the model as "
        + "models.LoadModel(new ContentPath(\"models/crate.glb\")):\n"
        + "1. define_mesh with the cube from its description.\n"
        + "2. Edit it: move and extrude.\n"
        + "3. save " + WorkedFile + "\n"
        + "4. In a later session, open " + WorkedFile + "\n"
        + "5. export " + WorkedExport;

    public static McpServerTool Create() => MeshToolSupport.Create<SaveTool>("save", Text, PathSchema("The .carve file to write, absolute or relative to carve's working directory; replaced if it exists."));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null)
    {
        if (PathArguments("save", context, path, out var text) is { } refused) return refused;

        var mesh = store.Current.Mesh;
        if (mesh is null) return Error("save: " + NoMesh);

        var target = ToolPaths.Resolve("save", text, Extension, out var pathError);
        if (target is null) return Error(pathError!);

        var bytes = new UTF8Encoding(false).GetBytes(CarveFile.Write(mesh));
        if (!ToolPaths.Write("save", target, bytes, out var replaced, out var writeError)) return Error(writeError!);

        var inv = CultureInfo.InvariantCulture;
        var report = MeshReport.Create(mesh);
        return Line($"saved {target.Written(replaced)}: {report.VertexCount.ToString(inv)} vertices, {report.FaceCount.ToString(inv)} faces");
    }
}
