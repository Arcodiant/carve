using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class ExportTool(ModelStore store)
{
    public const string Extension = ".glb";

    public const string Text =
        "Writes the current mesh to a .glb file (binary glTF 2.0) that a game on the Empyrean engine loads as it is. Takes path, the file to write. "
        + "What is written: one mesh on one node, both named after the file name without its extension (crate.glb gives crate), the node's transform the identity; "
        + "the triangles render draws, with flat normals: each face keeps its own normal, so every edge is hard; "
        + "one matte grey material named 'carve' (base colour 0.72, 0.72, 0.70, metallic 0, roughness 1, single-sided), and for faces coloured with set_colour one more opaque matte material per colour, named 'carve #RRGGBB', its baseColorFactor the linear form of the colour (metallic 0, roughness 1), a primitive for each; "
        + "positions and normals only, with no UVs, colours or textures. "
        + "Axes and units are the mesh's own: +Y up, +Z front, 1 unit = 1 m, the origin where you authored it. Nothing is scaled, turned, recentred or converted, "
        + "so build the model at the size and place the game wants it. "
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; missing folders are created; "
        + "an existing file is replaced and the result says (replaced); the path must end in .glb, in any case. "
        + "Errors, each a tool error that writes nothing: no mesh yet (call define_mesh or open first); an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .glb; a write that fails (access denied, a locked file), naming the path and the system's message, "
        + "which leaves any file already there as it was and no partial file. "
        + "A model of several meshes and no skeleton is written as one mesh, every mesh merged in order, as above. "
        + "With a skeleton (define_skeleton) the file is skinned, for a character: one glTF mesh named after the file holding every mesh of the model as primitives, "
        + "one per mesh and colour (meshes in order, colours by first face), each with its own material: '<mesh>' for uncoloured faces, '<mesh>:<group>' when a face group holds exactly that colour's faces, else '<mesh>:<rrggbb>'; "
        + "positions in the rest pose with JOINTS_0 and WEIGHTS_0 from set_weights; a node 'Armature' (identity, not a bone) with the bones under it in your order; skins[0] over every bone, "
        + "its inverse bind matrices the inverse of each bone's rest world transform; and one animation per clip (define_clip), LINEAR or STEP, every animated channel keyed at 0 and at the clip's duration. "
        + "A vertex with no weight is refused, the vertices listed and nothing written; allow_unweighted: true exports anyway, binding them to the first root bone, and the result says so. "
        + "A skinned export warns, and still writes, above 5000 triangles, above 8 submeshes or colours, and, for a skeleton with the Synty humanoid bones (Hips, Spine_01, Head, Shoulder/Elbow/Hand and UpperLeg/LowerLeg/Ankle _L and _R), a rest height outside 1.4 to 2.4 m. "
        + "A mesh with warnings still exports, and the result repeats them. export changes nothing: not the mesh, its ids, or what undo steps back to. "
        + "Returns one line: exported <file> to <absolute path> (replaced, when it was): <n> triangles, 1 material 'carve' (or each material, when there are several), size <x> x <y> x <z> m, "
        + "bounds (<min x, y, z>) to (<max x, y, z>), then any warnings; a skinned export's line names its pieces, materials, bones and clips instead of the one material. "
        + "mode: 'model' (the default) writes all of the above; 'clips' writes the armature, skins[0] and the animations with no mesh, no node holding a mesh and no material, "
        + "which is one file of clips for every character that shares the skeleton: a game loads it with IModelGeometryLoader.LoadAnimationSet (the same bones in the same order and the same clips as the model file) and retargets it, "
        + "and the model loader refuses it as 'glTF contains no meshes.'. The skin stays in a clips file because without it the loader reorders bones and the tracks line up with the wrong ones. "
        + "Clips mode is refused, writing nothing, for a model with no skeleton or no clips. "
        + "A role map (*.humanoid.json) and clip events (*.events.json) are sidecars the game writes beside the file; carve writes none. Clips mode repeats no mesh limits as it has no mesh. "
        + "The .glb is for the game: it keeps no faces, only triangles, so the .carve file is the one to keep working in. "
        + "The whole sequence from an empty file to a game, with save and open, is the worked example in save's description. "
        + "Worked example, writing into a game's content folder (put the game's folder for <game>); the game then loads it as "
        + "models.LoadModel(new ContentPath(\"models/crate.glb\")):\n"
        + ExampleMarker + SaveTool.WorkedExport;

    public static McpServerTool Create()
    {
        var schema = PathSchema("The .glb file to write, absolute or relative to carve's working directory; replaced if it exists.");
        schema["properties"]!.AsObject()["allow_unweighted"] = new JsonObject
        {
            ["description"] = "With a skeleton: export even when some vertices have no weight, binding them wholly to the first root bone, for work in progress. Default false.",
            ["type"] = "boolean",
        };
        schema["properties"]!.AsObject()["mode"] = new JsonObject
        {
            ["description"] = "model (default): the meshes, and with a skeleton the skin and clips. clips: the armature, skin and animations with no mesh, for a skeleton with clips.",
            ["type"] = "string",
            ["enum"] = new JsonArray("model", "clips"),
        };
        return MeshToolSupport.Create<ExportTool>("export", Text, schema);
    }

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null, JsonElement? allow_unweighted = null, JsonElement? mode = null)
    {
        var errors = UnknownArguments("export", context, "path", "allow_unweighted", "mode");
        var text = RequiredString("path", path, errors);
        var allowUnweighted = false;
        if (!Absent(allow_unweighted))
        {
            if (allow_unweighted!.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) allowUnweighted = allow_unweighted.Value.GetBoolean();
            else errors.Add($"allow_unweighted: expected true or false, got {Describe(allow_unweighted.Value)}");
        }
        var clips = false;
        if (!Absent(mode))
        {
            var value = mode!.Value.ValueKind == JsonValueKind.String ? mode.Value.GetString() : null;
            if (value is "model" or "clips") clips = value == "clips";
            else errors.Add($"mode: expected \"model\" or \"clips\", got {Describe(mode.Value)}");
        }
        if (errors.Count > 0) return Failed("export", errors);

        var model = store.Current.Model;
        var mesh = model.Combined;
        if (mesh is null) return Error("export: " + NoMesh);

        var target = ToolPaths.Resolve("export", text, Extension, out var pathError);
        if (target is null) return Error(pathError!);

        if (clips) return Clips(model, target);
        if (model.Skeleton is not null) return Skinned(model, target, allowUnweighted);

        var triangles = FlatShading.Triangles(mesh);
        var bytes = GlbExport.Write(mesh, triangles, target.Stem);
        if (!ToolPaths.Write("export", target, bytes, out var replaced, out var writeError)) return Error(writeError!);

        return Line(Summary(mesh, target, replaced, triangles.Count));
    }

    /// <summary>Clips mode: the armature, skin and animations with no mesh, or a refusal that writes nothing.</summary>
    private static CallToolResult Clips(Model model, ToolPath target)
    {
        if (ClipsExport.Refusal(model) is { } refusal) return Error($"export: mode clips: {refusal}. Nothing was written.");
        var bytes = ClipsExport.Write(model, target.Stem);
        if (!ToolPaths.Write("export", target, bytes, out var replaced, out var writeError)) return Error(writeError!);

        var skeleton = model.Skeleton!;
        static string N(int x) => x.ToString(CultureInfo.InvariantCulture);
        static string List(IEnumerable<string> names) => string.Join(", ", names.Select(n => $"'{n}'"));
        return Line($"exported {target.Written(replaced)} as clips, with no mesh: "
            + $"{N(skeleton.Bones.Count)} bone(s) {List(skeleton.Names)}, {N(model.Clips.Count)} clip(s) {List(model.Clips.Select(c => c.Name))}");
    }

    /// <summary>A model with a skeleton: the skinned file, or a refusal listing unweighted vertices that writes nothing.</summary>
    private static CallToolResult Skinned(Model model, ToolPath target, bool allowUnweighted)
    {
        static string N(int x) => x.ToString(CultureInfo.InvariantCulture);
        var unweighted = SkinnedExport.Unweighted(model);
        if (unweighted.Count > 0 && !allowUnweighted)
        {
            return Error($"export: {N(unweighted.Sum(u => u.Vertices.Count))} vertices have no weight, and a skinned export needs every vertex weighted: "
                + string.Join("; ", unweighted.Select(u => $"mesh '{u.Mesh}' vertices {string.Join(", ", u.Vertices.Select(N))}"))
                + ". Weight them with set_weights, or pass allow_unweighted: true to bind them to the first root bone for now. Nothing was written.");
        }

        var glb = SkinnedExport.Write(model, target.Stem);
        if (!ToolPaths.Write("export", target, glb.Bytes, out var replaced, out var writeError)) return Error(writeError!);

        var skeleton = model.Skeleton!;
        static string List(IEnumerable<string> names) => string.Join(", ", names.Select(n => $"'{n}'"));
        var all = model.Meshes.SelectMany(m => m.Mesh.Vertices).ToList();
        var min = all.Aggregate(Vector3.Min);
        var max = all.Aggregate(Vector3.Max);
        static string F(float x) => x.ToString(CultureInfo.InvariantCulture);
        static string Size(float lo, float hi) => F((float)((double)hi - lo));
        static string Point(Vector3 v) => $"({F(v.X)}, {F(v.Y)}, {F(v.Z)})";

        var line = $"exported {target.Written(replaced)} skinned: "
            + $"{N(glb.Pieces.Count)} piece(s) {List(glb.Pieces)}, {N(glb.Materials.Count)} material(s) {List(glb.Materials)}, "
            + $"{N(skeleton.Bones.Count)} bone(s) {List(skeleton.Names)}, {N(model.Clips.Count)} clip(s)"
            + (model.Clips.Count > 0 ? " " + List(model.Clips.Select(c => c.Name)) : "")
            + $", {N(glb.Triangles)} triangles, size {Size(min.X, max.X)} x {Size(min.Y, max.Y)} x {Size(min.Z, max.Z)} m, bounds {Point(min)} to {Point(max)}";
        if (glb.BoundToRoot > 0)
            line += $"; allow_unweighted: {N(glb.BoundToRoot)} vertices with no weight were bound to the first root bone '{skeleton.Bones[0].Name}'";

        var warnings = glb.Warnings.ToList();
        foreach (var piece in model.Meshes)
            warnings.AddRange(MeshReport.Create(piece.Mesh).Warnings.Select(w => $"mesh '{piece.Name}': " + w.ToString().ReplaceLineEndings(" ")));
        if (warnings.Count > 0)
            line += $"; {N(warnings.Count)} warning(s): " + string.Join("; ", warnings);
        return Line(line);
    }

    /// <summary>The result line; numbers are culture-invariant.</summary>
    private static string Materials(PolygonMesh mesh)
    {
        var names = GlbExport.MaterialNames(mesh);
        return names.Length == 1
            ? $"1 material '{names[0]}'"
            : $"{names.Length.ToString(CultureInfo.InvariantCulture)} materials {string.Join(", ", names.Select(n => $"'{n}'"))}";
    }

    private static string Summary(PolygonMesh mesh, ToolPath target, bool replaced, int triangles)
    {
        var report = MeshReport.Create(mesh);
        var (min, max) = (report.BoundsMin, report.BoundsMax);
        static string N(float x) => x.ToString(CultureInfo.InvariantCulture);
        static string Size(float lo, float hi) => N((float)((double)hi - lo));
        static string Point(Vector3 v) => $"({N(v.X)}, {N(v.Y)}, {N(v.Z)})";

        var line = $"exported {target.Written(replaced)}: "
            + $"{triangles.ToString(CultureInfo.InvariantCulture)} triangles, {Materials(mesh)}, "
            + $"size {Size(min.X, max.X)} x {Size(min.Y, max.Y)} x {Size(min.Z, max.Z)} m, bounds {Point(min)} to {Point(max)}";
        if (report.Warnings.Count > 0)
            line += $"; {report.Warnings.Count.ToString(CultureInfo.InvariantCulture)} warning(s): "
                + string.Join("; ", report.Warnings.Select(w => w.ToString().ReplaceLineEndings(" ")));
        return line;
    }
}
