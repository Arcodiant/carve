using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class OpenTool(ModelStore store, ToolFolder toolFolder)
{
    /// <summary>How many .carve files a missing-file error lists before it counts the rest.</summary>
    internal const int ListLimit = 20;

    private const string OpenMeshText =
        "The one mesh to take from the file, by its name there (a version 1 file's single mesh is taken whatever the name, and placed under it, so long as it is a name define_mesh takes: not empty and with no ':'): it replaces the model's mesh of that name, or is added when the name is new, and the rest of the model stays. "
        + "Of a part tree, the part's own geometry and kept levels: a part of that name keeps its place in the tree, and a new one is a top-level part at the origin; an instance or an empty, in the file or the model, is refused. "
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
        + "Returns define_mesh's JSON report ({vertices, faces, edges, closed, bounds, warnings}) preceded by path, the absolute path read; when the whole model has several meshes, {meshes:[{name, and that report}]} instead, and meshes:[] for a file of a skeleton and clips with no mesh (a clip library); "
        + "and when the model now has a skeleton, bones (how many) and clips (their names). "
        + "Errors, each a tool error that leaves the model, its ids and undo as they were: an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .carve; a file that does not exist, which names the absolute path looked for and lists the .carve files in that folder "
        + "(at most 20, sorted, and a count of the rest), or says there are none or that the folder does not exist, so correct the path from that list; "
        + "a file that cannot be read (access denied, a locked file); a file that is not a Carve file: not JSON (with line and column), an unknown version, "
        + "a file with no meshes and no skeleton, or anything the tools themselves would refuse (a bad vertex or face, colour, group, bone, weight or clip), each problem listed by where it is, e.g. meshes[0].faces[3][3]; "
        + "with mesh, a name that is empty or holds ':' (\"mesh: a mesh name must not contain ':' ...\"); a name a version 2 file has no mesh of (the file's meshes are listed, or it is said to have none), or weights on bones the model's skeleton lacks (they are listed); "
        + "an open that would replace a mesh that has morph targets (with mesh, the model's mesh of that name; without, any mesh of the model), naming the targets, unless drop_targets is true (undo brings them back either way). A file's morph targets come with its meshes, bit-exact. "
        + "A version 7 file holds a part tree, and a version 9 file a part tree beside morph targets (save says how): open without mesh brings back every part, its parent, transform, envelope, instance_of, moves mark and kept levels, with every mesh's and level's targets in version 9, bit-exact. "
        + "With mesh on a part tree, open takes that part's own geometry and kept levels as a piece and nothing of its place in the file: it replaces the geometry and levels of the model's part of that name, which keeps its parent, transform, envelope, children and moves mark, "
        + "or adds it as a new top-level part at the origin. mesh naming an instance or an empty, in the file or in the model, is a tool error that says which it is and names the source part an instance shows. "
        + "Part tree example: with the rover open, open {\"path\":\"wheel.carve\",\"mesh\":\"Wheel_FL\"} replaces Wheel_FL's polygons and levels with the file's; Wheel_FL stays at (0, 0, 0.72) under Axle_F, "
        + "and its instances Wheel_FR, Wheel_RL and Wheel_RR show the new wheel; mesh Wheel_FR is refused, since it is an instance of Wheel_FL. "
        + "The whole sequence from an empty file to a game is the worked example in save's description.\n"
        + ExampleMarker + SaveTool.WorkedFile;

    private const string DropTargetsText =
        "true to replace a mesh that has morph targets, dropping them (undo brings them back). Left out or false, an open that would replace a mesh with targets is refused, naming them.";

    public static McpServerTool Create()
    {
        var schema = WithMesh(PathSchema("The .carve file to read, absolute or relative to carve's working directory."), OpenMeshText);
        schema["properties"]!.AsObject()["drop_targets"] = new JsonObject { ["description"] = DropTargetsText, ["type"] = "boolean" };
        return MeshToolSupport.Create<OpenTool>("open", Text, schema);
    }

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null, JsonElement? mesh = null, JsonElement? drop_targets = null)
    {
        var errors = UnknownArguments("open", context, "path", "mesh", "drop_targets");
        var text = RequiredString("path", path, errors);
        var name = MeshName(mesh, errors);
        var drop = OptionalBool("drop_targets", drop_targets, errors);
        if (errors.Count > 0) return Failed("open", errors);

        var target = ToolPaths.Resolve("open", text, SaveTool.Extension, out var pathError, toolFolder.Directory, verb: "reads");
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
        Func<Model, string?> refuse = current => drop ? null : TargetsReplaced(current, name);
        if (name is null)
        {
            if ((opened = store.OpenModel(file, "open", refuse, out var problem)) is null) return Failed("open", [problem!]);
        }
        else if ((opened = store.OpenPiece(file, name, "open", refuse, out var problem)) is null) return Failed("open", [problem!]);

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

    /// <summary>
    /// Why the open would lose morph targets (T-101), or null: the model's mesh <paramref name="name"/> has targets, or, with no
    /// name, some mesh of the model has. Undo brings them back after an open with drop_targets, so nothing is lost for good.
    /// </summary>
    internal static string? TargetsReplaced(Model current, string? name)
    {
        var lost = current.Meshes.Where(m => (name is null || m.Name == name) && m.Mesh.Targets is not null).ToArray();
        if (lost.Length == 0) return null;
        if (name is not null)
            return $"drop_targets: the model's mesh '{name}' has the morph target(s) {lost[0].Mesh.Targets!.ListNames()}, which opening mesh '{name}' would replace; "
                + "pass drop_targets: true to replace it and drop them (undo brings them back)";
        var listed = string.Join("; ", lost.Select(m => $"mesh '{m.Name}': {m.Mesh.Targets!.ListNames()}"));
        return $"drop_targets: opening the whole file replaces the model, and its morph targets would be lost ({listed}); "
            + "pass drop_targets: true to replace them (undo brings them back), or open one mesh with mesh";
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
