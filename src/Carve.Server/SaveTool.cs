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
    /// What save writes for define_mesh's unit cube, as <c>CarveFile.Write</c> gives it (version 2): LF line ends whatever the
    /// checkout's, which is why it is not a raw string. A test holds it equal to the writer's output.
    /// </summary>
    public const string CubeFile =
        "{\n" +
        "  \"carve\": 2,\n" +
        "  \"meshes\": [\n" +
        "    {\n" +
        "      \"name\": \"mesh\",\n" +
        "      \"vertices\": [\n" +
        "        [0, 0, 0],\n" +
        "        [1, 0, 0],\n" +
        "        [1, 1, 0],\n" +
        "        [0, 1, 0],\n" +
        "        [0, 0, 1],\n" +
        "        [1, 0, 1],\n" +
        "        [1, 1, 1],\n" +
        "        [0, 1, 1]\n" +
        "      ],\n" +
        "      \"faces\": [\n" +
        "        [0, 3, 2, 1],\n" +
        "        [4, 5, 6, 7],\n" +
        "        [0, 1, 5, 4],\n" +
        "        [3, 7, 6, 2],\n" +
        "        [0, 4, 7, 3],\n" +
        "        [1, 2, 6, 5]\n" +
        "      ],\n" +
        "      \"colours\": [],\n" +
        "      \"groups\": [],\n" +
        "      \"weights\": []\n" +
        "    }\n" +
        "  ],\n" +
        "  \"skeleton\": null,\n" +
        "  \"clips\": []\n" +
        "}\n";

    public const string Text =
        "Writes the model to a .carve file, so the work outlives this session; open reads it back, in this carve process or a later one. Takes path, the file to write, and mesh. "
        + "With mesh left out, save writes the WHOLE model: every mesh by name with its colours, groups and weights, the skeleton and the clips. "
        + "With mesh, it writes that one mesh, and when the mesh has weights the skeleton and clips too, so the file opens on its own. "
        + "A skeleton and clips with no mesh (a clip library) are saved too, as \"meshes\": []. "
        + "The .carve file is the one to keep working in: it holds the polygons exactly, every vertex and face in its id order and every number to the bit, so after open the ids you used before still name the same vertices and faces. "
        + "It does not hold the undo history (open starts from the file as one step), a current file name (carve remembers none: give the path every time) or any render setting. "
        + "The .glb that export writes is only for the game, and has no faces to edit. "
        + "Materials (define_material) are saved too, as a version 6 file's materials list and each mesh's face materials; a model with none is written as it always was. A texture is saved as a path relative to the .carve file's folder, so keep the PNG files with the file or below it; save does not copy them, and open reads them again from those paths. "
        + "With mesh, only the materials that mesh's faces use are written. A texture that has no relative path from the file's folder (on another drive) refuses the save, naming the material and the field. "
        + "The file is versioned JSON (version 2), keys always in one order, one vertex, face, colour, group, weighted vertex, bone or clip key per line, so the same model always gives the same bytes and a diff shows what changed: "
        + "meshes [{name, vertices, faces, colours [{colour, faces}], groups [{name, faces}], weights [{vertex, bones {bone: weight}}]}], skeleton [{name, parent, position, rotation}] or null, "
        + "and clips [{name, duration, loop, channels {bone: {rotation, translation: {interpolation, keys [{time, value}]}}}}], as define_skeleton and define_clip take them. "
        + "The unit cube from define_mesh is saved as:\n"
        + CubeFile
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; missing folders are created; "
        + "an existing file is replaced and the result says (replaced); the path must end in .carve, in any case. "
        + "Errors, each a tool error that writes nothing: no mesh and no skeleton yet (call define_mesh or open first); a mesh name the model does not have; an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .carve; a write that fails (access denied, a locked file), naming the path and the system's message, "
        + "which leaves any file already there as it was and no partial file. "
        + "save changes nothing: not the model, its ids, or what undo steps back to. "
        + "Returns one line: saved <file> to <absolute path> (replaced, when it was): <n> vertices, <n> faces, then the meshes when there are several, and the bones and clips when there is a skeleton; "
        + "then a line \"warning: the texture '<path>' (<where it is used>) is no longer at its path; ...\" for each texture whose PNG has been moved or deleted since define_material read it, since open reads the PNG again and refuses the file until it is back (save still writes the file).\n"
        + "Worked example, from an empty file to a game's content folder (put the game's folder for <game>); each numbered line is a call, and the game then loads the model as "
        + "models.LoadModel(new ContentPath(\"models/crate.glb\")):\n"
        + "1. define_mesh with the cube from its description.\n"
        + "2. Edit it: move and extrude.\n"
        + "3. save " + WorkedFile + "\n"
        + "4. In a later session, open " + WorkedFile + "\n"
        + "5. export " + WorkedExport;

    public static McpServerTool Create() => MeshToolSupport.Create<SaveTool>("save", Text, WithMesh(PathSchema("The .carve file to write, absolute or relative to carve's working directory; replaced if it exists."),
        "The one mesh to write, by name, with the skeleton and clips when it has weights. Left out, the whole model is written."));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null, JsonElement? mesh = null)
    {
        if (PathAndMeshArguments("save", context, path, mesh, out var text, out var name) is { } refused) return refused;

        var current = store.Current;
        var model = current.Model;
        if (model.IsEmpty && model.Skeleton is null) return Error("save: " + NoMesh);
        if (name is not null)
        {
            if (Target(current, "save", name, out var error) is null) return error!;
            model = model.Piece(name)!;
        }

        var target = ToolPaths.Resolve("save", text, Extension, out var pathError);
        if (target is null) return Error(pathError!);

        var folder = Path.GetDirectoryName(target.FullPath)!;
        if (CarveFile.TextureProblem(model, folder) is { } textureProblem) return Error("save: " + textureProblem);
        var bytes = new UTF8Encoding(false).GetBytes(CarveFile.Write(model, folder));
        if (!ToolPaths.Write("save", target, bytes, out var replaced, out var writeError)) return Error(writeError!);

        return Line($"saved {target.Written(replaced)}: {Summary(model)}" + string.Concat(MissingTextures(model).Select(w => "\nwarning: " + w)));
    }

    /// <summary>
    /// One warning per texture path of <paramref name="model"/> with no file there now (moved or deleted since define_material
    /// read it), naming where it is used: the save still writes the path, but open reads the PNG again and would refuse the
    /// file (the person's ruling, RV-47).
    /// </summary>
    public static IEnumerable<string> MissingTextures(Model model)
    {
        var uses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var i = 0; i < model.Materials.Count; i++)
            foreach (var (field, texture) in model.Materials[i].Textures)
                if (texture is not null)
                {
                    if (!uses.TryGetValue(texture.Path, out var at)) uses[texture.Path] = at = [];
                    at.Add(string.Create(CultureInfo.InvariantCulture, $"materials[{i}].{field} of '{model.Materials[i].Name}'"));
                }
        foreach (var (path, at) in uses)
            if (!File.Exists(path))
                yield return $"the texture '{path}' ({string.Join(", ", at)}) is no longer at its path; the file is saved with the path, but open refuses it until the PNG is there again";
    }

    /// <summary>What a file of <paramref name="model"/> holds: vertices and faces, and the meshes, bones and clips when there are any to name.</summary>
    public static string Summary(Model model)
    {
        var inv = CultureInfo.InvariantCulture;
        var reports = model.Meshes.Select(m => MeshReport.Create(m.Mesh)).ToArray();
        var line = $"{reports.Sum(r => r.VertexCount).ToString(inv)} vertices, {reports.Sum(r => r.FaceCount).ToString(inv)} faces";
        if (model.Meshes.Count > 1) line += $" in {model.Meshes.Count.ToString(inv)} meshes ({model.ListNames()})";
        if (model.Skeleton is { } skeleton) line += $", {Count(skeleton.Bones.Count, "bone")}, {Count(model.Clips.Count, "clip")}";
        return line;

        static string Count(int n, string what) => $"{n.ToString(CultureInfo.InvariantCulture)} {what}{(n == 1 ? "" : "s")}";
    }
}
