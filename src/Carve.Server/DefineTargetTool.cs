using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>What the target tools share: a target name argument and the refusal for one a mesh does not have.</summary>
internal static class TargetToolSupport
{
    /// <summary>The refusal for a target the mesh does not have: lists the targets it has.</summary>
    public static string NoSuchTarget(string name, PolygonMesh mesh) =>
        $"target: this mesh has no target '{name}'; " + (mesh.Targets is { } targets ? $"its targets are {targets.ListNames()}" : "it has no targets (define_target makes one)");

    /// <summary>An optional target name (get_mesh's <c>target</c>): null when absent; a problem when it is not a non-empty string.</summary>
    public static string? TargetName(string argument, JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return null;
        if (value!.Value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"{argument}: expected a string, got {Describe(value.Value)}");
            return null;
        }
        var text = value.Value.GetString()!;
        if (MorphTarget.NameProblem(text) is { } problem)
        {
            errors.Add($"{argument}: {problem}");
            return null;
        }
        return text;
    }

    /// <summary>A required target name: the same, with a missing one a problem too.</summary>
    public static string? RequiredTargetName(JsonElement? value, List<string> errors)
    {
        if (Absent(value))
        {
            errors.Add("name: required but missing");
            return null;
        }
        return TargetName("name", value, errors);
    }

    public static string[] Names(PolygonMesh mesh) => mesh.Targets?.Names.ToArray() ?? [];
}

public sealed class DefineTargetTool(ModelStore store, ToolFolder toolFolder)
{
    public const string Text =
        "Gives a mesh a named morph target: a second shape of the same vertices, which render and export blend in by weight (the target at weight 1 is the shape you give here, at 0 the mesh as it is). "
        + "A target belongs to one mesh and is stored as each moved vertex's offset from the mesh, so a later move, extrude or other edit of the mesh carries it along. "
        + "Arguments: name (required, non-empty, without a colon); mesh (the mesh to give it to; may be left out while the model has one mesh); and exactly one of positions and file (giving both or neither is a tool error). "
        + "positions is the shape itself as ABSOLUTE positions (where each vertex should be at weight 1, in metres, not offsets), in either of two forms: "
        + "an array of [x, y, z], one for every vertex slot of the mesh in id order (get_mesh's vertices list, deleted slots included, so the count is that list's length), with null at a deleted vertex's slot; "
        + "or an object of \"<vertex id>\": [x, y, z] for just the vertices that move, every other vertex staying where it is. "
        + "file is the path of a .carve file (absolute, or relative to carve's working directory, as open takes it) holding the same mesh in another shape, as made by editing a copy of it: "
        + "its vertices become the target's positions. The two must have the same topology: the same vertex slots, deleted slots and faces in the same order with the same corners; the mesh in the file may be placed differently, never rebuilt. "
        + "file_mesh names the mesh in that file (default the mesh's own name, else the file's only mesh); it needs file. "
        + "A name the mesh already has as a target REPLACES that target in its place, and the result says so (replaced:true); a new name is added after the others. One step for undo. "
        + "Returns JSON {mesh, name, vertices (how many vertices the target moves; one it leaves where it is does not count), largest_move (the longest single move, in metres), replaced, targets:[the mesh's target names, in order]}. "
        + "Bad input is a tool error that lists every problem and leaves the model unchanged: a bad name, giving both or neither of positions and file, an id the mesh does not have or has deleted (positions[7]), "
        + "the wrong number of positions (the count and the mesh's vertex slots are both given), a null at a vertex that exists, a position that is not three finite numbers, "
        + "a file that cannot be read or is not a Carve file (as open says), a file_mesh the file does not have (its meshes are listed), "
        + "or a mesh in the file whose topology differs, which names the FIRST difference (for example faces[12][2]: vertex 40 here, 41 in the other mesh). "
        + "Needs a mesh, from define_mesh or open, first. delete_target removes a target; get_mesh lists a mesh's targets and, with target, reads one back. "
        + PartSpaceText
        + "Worked example on define_mesh's unit cube, a bulge: the four top vertices (2, 3, 6, 7) rise to y = 1.3 and spread out a tenth, the other four stay where they are:\n"
        + ExampleMarker + BulgeExample + "\n"
        + "Worked example from a file, after saving an edited copy of the cube as cube-lean.carve (move the top face, then save that path, then undo): "
        + "the target is made from where the file's vertices are\n"
        + ExampleMarker + FileExample;

    public const string BulgeExample =
        """{"name":"Bulge","positions":{"2":[1.1,1.3,-0.1],"3":[-0.1,1.3,-0.1],"6":[1.1,1.3,1.1],"7":[-0.1,1.3,1.1]}}""";

    public const string FileExample = """{"name":"Lean","file":"cube-lean.carve"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<DefineTargetTool>("define_target", Text, WithVerbosity(WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject
            {
                ["description"] = "The target's name: non-empty and without a colon. A name the mesh already has replaces that target. One name is one dial across the model, so use the same name on the meshes that move together.",
                ["type"] = "string",
            },
            ["positions"] = new JsonObject
            {
                ["description"] = "The shape as ABSOLUTE positions (not offsets), in metres. Either an array with one [x, y, z] for every vertex slot in id order, null at a deleted vertex; "
                    + "or an object of \"<vertex id>\": [x, y, z] for the vertices that move. Give this or file, not both.",
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["anyOf"] = new JsonArray(Position(), new JsonObject { ["type"] = "null" }) } },
                    new JsonObject { ["type"] = "object", ["additionalProperties"] = Position() }),
            },
            ["file"] = new JsonObject
            {
                ["description"] = "A .carve file holding this mesh in another shape with the same topology, absolute or relative to carve's working directory. Give this or positions, not both.",
                ["type"] = "string",
            },
            ["file_mesh"] = new JsonObject
            {
                ["description"] = "With file: the name of the mesh in that file to take the shape from. Default: the mesh's own name, else the file's only mesh.",
                ["type"] = "string",
            },
        },
        ["required"] = new JsonArray("name"),
    })), "file");

    private static JsonObject Position() => new()
    {
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "number" },
        ["minItems"] = 3,
        ["maxItems"] = 3,
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? name = null, JsonElement? positions = null,
        JsonElement? file = null, JsonElement? file_mesh = null, JsonElement? mesh = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("define_target", context, "name", "positions", "file", "file_mesh", "mesh", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var meshName = MeshName(mesh, errors);
        var targetName = TargetToolSupport.RequiredTargetName(name, errors);
        var path = OptionalString("file", file, errors);
        var fileMesh = OptionalString("file_mesh", file_mesh, errors);
        if (fileMesh is not null && Model.NameProblem(fileMesh) is { } fileMeshProblem)
        {
            errors.Add($"file_mesh: {fileMeshProblem}");
            fileMesh = null;
        }

        var hasPositions = !Absent(positions);
        var hasFile = !Absent(file);
        if (hasPositions == hasFile) errors.Add(hasPositions ? "positions and file: give exactly one of them, not both" : "positions and file: give exactly one of them; neither was given");
        if (!hasFile && !Absent(file_mesh)) errors.Add("file_mesh: it names a mesh in file, and no file was given");

        List<Vector3?>? dense = null;
        List<KeyValuePair<int, Vector3>>? sparse = null;
        if (hasPositions) ReadPositions(positions!.Value, errors, out dense, out sparse);
        if (errors.Count > 0) return Failed("define_target", errors);

        var model = store.Current.Model;
        var named = model.Target(meshName, out var problem);
        if (problem is not null) return Failed("define_target", [problem]);
        if (named is null) return Error("define_target: " + NoMesh);

        PolygonMesh? other = null;
        if (hasFile)
        {
            var read = ReadFile(path!, fileMesh, named.Name, out var refused);
            if (read is null) return refused!;
            other = read;
        }

        var replaced = false;
        var made = (MorphTarget?)null;
        var snapshot = store.Rework("define_target", named.Name, current =>
        {
            MorphTargetResult? result;
            if (other is not null) result = MorphTarget.FromMesh(current, targetName, other);
            else if (dense is not null)
            {
                result = FromDense(current, targetName!, dense, out var denseProblems);
                if (result is null) return (null, denseProblems);
            }
            else result = MorphTarget.FromPositions(current, targetName, sparse!);
            if (!result.Succeeded) return (null, result.Errors.Select(e => e.ToString()).ToList());
            made = result.Target!;
            replaced = current.Targets?.Find(made.Name) is not null;
            return (current.WithTargets(MorphTargets.With(current.Targets, made)), []);
        }, out var problems, out var storeProblem);
        if (storeProblem is not null) return Failed("define_target", [storeProblem]);
        if (snapshot is null) return Failed("define_target", problems);

        var now = snapshot.Model.Find(named.Name)!;
        return Quiet(Ok(new Dictionary<string, object>
        {
            ["mesh"] = named.Name,
            ["name"] = made!.Name,
            ["vertices"] = made.Count,
            ["largest_move"] = made.Deltas.Count == 0 ? 0f : made.Deltas.Values.Max(d => d.Length()),
            ["replaced"] = replaced,
            ["targets"] = TargetToolSupport.Names(now),
        }), level);
    }

    /// <summary>The dense form, a null filled in at each slot: null, with the problems, when a live vertex has none.</summary>
    private static MorphTargetResult? FromDense(PolygonMesh mesh, string name, List<Vector3?> given, out List<string> problems)
    {
        problems = [];
        if (given.Count != mesh.Vertices.Count)
        {
            // Let Core say it, with its own count text.
            return MorphTarget.FromPositions(mesh, name, given.Select(p => p ?? Vector3.Zero).ToList());
        }
        for (var v = 0; v < given.Count; v++)
            if (given[v] is null && !mesh.IsVertexDeleted(v))
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"positions[{v}]: null, but vertex {v} exists; give its [x, y, z] (null is for a deleted vertex's slot)"));
        return problems.Count > 0 ? null : MorphTarget.FromPositions(mesh, name, given.Select(p => p ?? Vector3.Zero).ToList());
    }

    private static void ReadPositions(JsonElement value, List<string> errors, out List<Vector3?>? dense, out List<KeyValuePair<int, Vector3>>? sparse)
    {
        dense = null;
        sparse = null;
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                dense = [];
                var i = 0;
                foreach (var item in value.EnumerateArray())
                {
                    dense.Add(item.ValueKind == JsonValueKind.Null ? null : Vec3(string.Create(CultureInfo.InvariantCulture, $"positions[{i}]"), item, errors));
                    i++;
                }
                break;
            case JsonValueKind.Object:
                sparse = [];
                foreach (var p in value.EnumerateObject())
                {
                    if (!int.TryParse(p.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                    {
                        errors.Add($"positions[\"{p.Name}\"]: a key is a vertex id, a whole number such as \"7\"");
                        continue;
                    }
                    if (Vec3(string.Create(CultureInfo.InvariantCulture, $"positions[{id}]"), p.Value, errors) is { } at) sparse.Add(KeyValuePair.Create(id, at));
                }
                break;
            default:
                errors.Add($"positions: expected an array of [x, y, z] or an object of \"<vertex id>\": [x, y, z], got {Describe(value)}");
                break;
        }
    }

    /// <summary>The mesh to take the shape from, read as open reads a file; null with the tool error when the file or mesh cannot be had.</summary>
    private PolygonMesh? ReadFile(string text, string? fileMesh, string ownName, out CallToolResult? refused)
    {
        refused = null;
        var target = ToolPaths.Resolve("define_target", text, SaveTool.Extension, out var pathError, toolFolder.Directory, verb: "reads");
        if (target is null) { refused = Error(pathError!); return null; }
        if (Directory.Exists(target.FullPath)) { refused = Error($"define_target: {target.FullPath} is a folder, not a file"); return null; }
        if (!File.Exists(target.FullPath)) { refused = Error($"define_target: no file at {target.FullPath}"); return null; }

        string content;
        try { content = File.ReadAllText(target.FullPath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            refused = Error($"define_target: could not read {target.FullPath}: {e.Message}");
            return null;
        }

        var read = CarveFile.Read(content, Path.GetDirectoryName(target.FullPath), null);
        if (!read.Succeeded)
        {
            var problems = read.Errors.Select(x => x.ToString()).ToList();
            refused = Error($"define_target failed with {problems.Count} problem(s) in {target.FullPath}; the model is unchanged:\n" + string.Join("\n", problems));
            return null;
        }

        var file = read.Model!;
        var names = NameList.Quoted(file.Meshes.Select(m => m.Name));
        if (fileMesh is not null)
        {
            if (file.Find(fileMesh) is { } given) return given;
            refused = Failed("define_target", [$"file_mesh: {target.FullPath} has no mesh '{fileMesh}'; its meshes are {names}"]);
            return null;
        }
        if (file.Find(ownName) is { } same) return same;
        if (file.Meshes.Count == 1) return file.Meshes[0].Mesh;
        refused = Failed("define_target", [file.Meshes.Count == 0
            ? $"file: {target.FullPath} has no mesh"
            : $"file_mesh: {target.FullPath} has no mesh '{ownName}' and several others; name one of {names}"]);
        return null;
    }
}

public sealed class DeleteTargetTool(ModelStore store)
{
    public const string Text =
        "Removes one morph target from a mesh. Arguments: name (required), the target's name as get_mesh lists it; and mesh, the mesh it is on, which may be left out while the model has one mesh. "
        + "The mesh's own shape and its other targets are unchanged. One step for undo. "
        + PartTargetText
        + "Returns JSON {mesh, deleted:name, targets:[the names left, in order]}. "
        + "A name the mesh has no target of is a tool error that lists the targets it has, and changes nothing. "
        + "Worked example on define_target's bulge:\n"
        + ExampleMarker + """{"name":"Bulge"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<DeleteTargetTool>("delete_target", Text, WithVerbosity(WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["description"] = "The name of the target to remove.", ["type"] = "string" },
        },
        ["required"] = new JsonArray("name"),
    })));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? name = null, JsonElement? mesh = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("delete_target", context, "name", "mesh", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var meshName = MeshName(mesh, errors);
        var targetName = TargetToolSupport.RequiredTargetName(name, errors);
        if (errors.Count > 0) return Failed("delete_target", errors);

        var named = store.Current.Model.Target(meshName, out var problem);
        if (problem is not null) return Failed("delete_target", [problem]);
        if (named is null) return Error("delete_target: " + NoMesh);

        var snapshot = store.Rework("delete_target", named.Name, current =>
            current.Targets?.Find(targetName!) is null
                ? (null, [TargetToolSupport.NoSuchTarget(targetName!, current)])
                : (current.WithTargets(current.Targets.Without(targetName!)), []),
            out var problems, out var storeProblem);
        if (storeProblem is not null) return Failed("delete_target", [storeProblem]);
        if (snapshot is null) return Failed("delete_target", problems);

        return Quiet(Ok(new Dictionary<string, object>
        {
            ["mesh"] = named.Name,
            ["deleted"] = targetName!,
            ["targets"] = TargetToolSupport.Names(snapshot.Model.Find(named.Name)!),
        }), level);
    }
}
