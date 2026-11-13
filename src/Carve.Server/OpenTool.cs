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
    internal const int ListLimit = 20;

    private const string OpenMeshText =
        "The one mesh to take from the file, by its name there (a version 1 file's single mesh is taken whatever the name, and placed under it, so long as it is a name define_mesh takes: not empty and with no ':'): it replaces the model's mesh of that name, or is added when the name is new, and the rest of the model stays. "
        + "Left out, the whole model is replaced by the file's.";

    public const string Text =
        "Reads a .carve file (written by save). With mesh left out it REPLACES THE WHOLE MODEL with the file's: its meshes, colours, groups, weights, skeleton and clips, and nothing of the model before is kept. "
        + "With mesh, it takes only that mesh from the file (with its colours, groups and weights) and puts it in the model, replacing the mesh of that name or adding it; the model's skeleton and clips stay, "
        + "and the mesh's weights are matched to the model's skeleton by bone name, so it is refused when they name a bone the model's skeleton does not have. "
        + "A version 1 file (one mesh, from before skeletons) opens as one mesh named mesh, or, with mesh given, as that one mesh under the name given, any name define_mesh takes (not empty, no ':'). "
        + "Either way open is one step like any other, so undo takes it back to the model before it. The first change a process has cannot be undone, whichever tool made it, so undo straight after an open that gave the process its first model is a tool error (the model stays), while a process that had a mesh before the open can undo it; and only the last 100 changes are kept, so once 100 changes have followed an open, the open itself can no longer be undone. Takes path, the file to read. "
        + "A file's materials come with it: each texture is read again from its path relative to the .carve file's folder, so a texture that is missing or is not a PNG refuses the open, naming materials[i].<field>. With mesh, only the materials that mesh's faces use come with it and are read, so a missing texture on another material does not refuse it: one the model lacks is added, one the model already defines identically is kept, and one the model defines differently refuses the open, naming the material (define one of them again under another name, or open the whole file). "
        + "The file holds the polygons exactly, with the ids they were saved with: vertex 0 is the file's first vertex and face 0 its first face, "
        + "so the ids you used before the save still name the same vertices and faces. It does not hold the undo history, so the file brings no older states with it, "
        + "and carve keeps no current file: give the path to open and to save every time. "
        + "The .carve file is the one to keep working in; the .glb that export writes is only for the game and cannot be opened. "
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; the path must end in .carve, in any case. "
        + "On a subdivided model the report lists a not-planar warning for each curved quad, which can run to tens of thousands of characters and be cut by the client (the file is open all the same); get_clips and get_skeleton are short. "
        + "Returns define_mesh's JSON report ({vertices, faces, edges, closed, bounds, warnings}) preceded by path, the absolute path read; when the whole model has several meshes, {meshes:[{name, and that report}]} instead, and meshes:[] for a file of a skeleton and clips with no mesh (a clip library); "
        + "and when the model now has a skeleton, bones (how many) and clips (their names). "
        + "Errors, each a tool error that leaves the model, its ids and undo as they were: an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .carve; a file that does not exist, which names the absolute path looked for and lists the .carve files in that folder "
        + "(at most 20, sorted, and a count of the rest), or says there are none or that the folder does not exist, so correct the path from that list; "
        + "a file that cannot be read (access denied, a locked file); a file that is not a Carve file: not JSON (with line and column), an unknown version, "
        + "a file with no meshes and no skeleton, or anything the tools themselves would refuse (a bad vertex or face, colour, group, bone, weight or clip), each problem listed by where it is, e.g. meshes[0].faces[3][3]; "
        + "with mesh, a name that is empty or holds ':' (\"mesh: a mesh name must not contain ':' ...\"); a name a version 2 file has no mesh of (the file's meshes are listed, or it is said to have none), or weights on bones the model's skeleton lacks (they are listed). "
        + "The whole sequence from an empty file to a game is the worked example in save's description.\n"
        + ExampleMarker + SaveTool.WorkedFile;

    public static McpServerTool Create() => MeshToolSupport.Create<OpenTool>("open", Text, WithMesh(PathSchema("The .carve file to read, absolute or relative to carve's working directory."), OpenMeshText));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null, JsonElement? mesh = null)
    {
        if (PathAndMeshArguments("open", context, path, mesh, out var text, out var name) is { } refused) return refused;

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

        var read = CarveFile.Read(content, Path.GetDirectoryName(target.FullPath), name);
        if (!read.Succeeded)
        {
            var problems = read.Errors.Select(x => x.ToString()).ToList();
            return Error($"open failed with {problems.Count} problem(s) in {target.FullPath}; the model is unchanged:\n" + string.Join("\n", problems));
        }

        var file = read.Model!;
        // A version 1 file holds one unnamed mesh (read as 'mesh'): with a name, that mesh is placed under it.
        if (name is not null && read.Version == 1) file = Model.Of(name, file.Meshes[0].Mesh);
        ModelSnapshot? opened;
        if (name is null) opened = store.OpenModel(file, "open");
        else if ((opened = store.OpenMesh(file, name, "open", out var problem)) is null) return Failed("open", [problem!]);

        var model = opened.Model;
        var body = new Dictionary<string, object> { ["path"] = target.FullPath };
        var shown = name is null ? model.Meshes : [model.Meshes.First(m => m.Name == name)];
        if (shown.Count == 1)
            foreach (var (key, value) in Report(shown[0].Mesh)) body[key] = value;
        else
            body["meshes"] = shown.Select(m =>
            {
                var one = new Dictionary<string, object> { ["name"] = m.Name };
                foreach (var (k, v) in Report(m.Mesh)) one[k] = v;
                return one;
            }).ToArray();
        if (model.Skeleton is { } skeleton)
        {
            body["bones"] = skeleton.Bones.Count;
            body["clips"] = model.Clips.Select(c => c.Name).ToArray();
        }
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
                .Where(n => n.EndsWith(SaveTool.Extension, StringComparison.OrdinalIgnoreCase) )
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
