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
        "Writes the current mesh to a .glb file (binary glTF 2.0) that a game on the Empyrean engine loads as it is. Takes path, the file to write, and overwrite (default false), which must be true to replace a file that is already there, in either mode. "
        + "What is written: one mesh on one node, both named after the file name without its extension (crate.glb gives crate), the node's transform the identity; "
        + "the triangles render draws, with flat normals: each face keeps its own normal, so every edge is hard; "
        + "one matte grey material named 'carve' (base colour 0.72, 0.72, 0.70, metallic 0, roughness 1, single-sided), and for faces coloured with set_colour one more opaque matte material per colour, named 'carve #RRGGBB', its baseColorFactor the linear form of the colour (metallic 0, roughness 1), a primitive for each; "
        + "faces given a material with set_material (define_material) are written as glTF pbrMetallicRoughness, one primitive per material under the material's own name, after the colours' primitives, in order of first use by face: "
        + "baseColorFactor the linear form of base_colour (alpha 1), metallicFactor and roughnessFactor, each left out when it equals glTF's default of 1 (so a material at its defaults, metallic 0 and roughness 1, has metallicFactor 0 and no roughnessFactor, and a loader reads roughness 1), emissiveFactor the linear form of emissive and left out when it is black, doubleSided, "
        + "and each texture embedded in the .glb as the PNG bytes it was defined with, unchanged (a PNG used by several materials or channels is embedded once): base_colour_texture as baseColorTexture, metallic_roughness_texture as metallicRoughnessTexture (roughness in G, metallic in B), normal_texture as normalTexture, emissive_texture as emissiveTexture, occlusion_texture as occlusionTexture with the material's occlusion as its strength (occlusion without an occlusion_texture is not written: glTF only scales the map). "
        + "An emissive_texture shows only with a non-black emissive, which multiplies it. No alpha fields and no sampler are written: materials are opaque, and the engine uses its own sampling. "
        + "positions and normals, and when any face has UVs, or any material in use has a texture, a TEXCOORD_0 holding each corner's UV exactly as Carve holds it (no conversion: glTF's axes, (0, 0) at the image's top-left), a vertex split wherever its UV or normal differ; "
        + "a model with no UVs and no textured material in use has no TEXCOORD_0; the corners of a face without UVs are written as (0, 0), and the result warns '<mesh>: N faces have no UVs; their corners are written as (0, 0)' with the first face ids, and for faces of a textured material \"material 'wood' has a texture, but N faces using it have no UVs; their corners are written as (0, 0) (<mesh> faces ...)\" in place of that warning for those faces; no vertex colours. "
        + "Axes and units are the mesh's own: +Y up, +Z front, 1 unit = 1 m, the origin where you authored it. Nothing is scaled, turned, recentred or converted, "
        + "so build the model at the size and place the game wants it. "
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; missing folders are created; "
        + "an existing file is refused unless overwrite: true is given, and then it is replaced and the result says (replaced); the path must end in .glb, in any case. "
        + "Errors, each a tool error that writes nothing: an existing file without overwrite: true (the error names the path and overwrite, and the file is unchanged); no mesh yet (call define_mesh or open first; clips mode needs none); an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .glb; a write that fails (access denied, a locked file), naming the path and the system's message, "
        + "which leaves any file already there as it was and no partial file. "
        + "A model of several meshes and no skeleton is written as one mesh, every mesh merged in order, as above. "
        + "With a skeleton (define_skeleton) the file is skinned, for a character: one glTF mesh named after the file holding every mesh of the model as primitives, "
        + "one per mesh and colour (meshes in order, colours by first face), each with its own material: '<mesh>' for uncoloured faces, '<mesh>:<group>' when a face group holds exactly that colour's faces, else '<mesh>:<rrggbb>' (also when the group's name is another colour's rrggbb in that mesh, so no two materials share a name); then the materials the mesh's faces use, one primitive each named for the material (a material two meshes use is one glTF material and one primitive, with both meshes' faces; a material named like a mesh renames that mesh's uncoloured material to '<mesh>:uncoloured' only when the names clash); "
        + "positions in the rest pose with JOINTS_0 and WEIGHTS_0 from set_weights; a node 'Armature' (identity, not a bone) with the bones under it in your order; skins[0] over every bone, "
        + "its inverse bind matrices the inverse of each bone's rest world transform; and one animation per clip (define_clip), LINEAR or STEP, every animated channel keyed at 0 and at the clip's duration. "
        + "A vertex with no weight is refused, the vertices listed and nothing written; allow_unweighted: true exports anyway, binding them to the first root bone, and the result says so. "
        + "Game limits: export warns, and still writes, past the limits of the game it exports for, read from carve-limits.json in the game's content folder: export looks in the .glb's folder and then each folder above it, and uses the first it finds; limits (a folder holding carve-limits.json, or a .json file) names it instead. "
        + "The file is a JSON object of optional keys: max_triangles (the whole model), max_submeshes (submeshes, or distinct colours), and min_height and max_height in metres (the rest height of every mesh, only for a skeleton with the Synty humanoid bones: Hips, Spine_01, Head, Shoulder/Elbow/Hand and UpperLeg/LowerLeg/Ankle _L and _R); each warns past its value and never at it, a key left out never warns, and with no file there are no limit warnings, which the result says. "
        + "write_limits: true fills in the keys the file is missing with Orcward's starting numbers (max_triangles 5000, max_submeshes 8, min_height 1.4, max_height 2.4), creating the file beside the .glb when none was named or found, and never changes a key already set, though it rewrites the file with the keys in a fixed order (plain JSON, no comments); it is written after the .glb, and the result says what it filled. Edit the numbers to the game's own. "
        + "A limits file that is not a plain JSON object of those keys (a count a whole number of at least 1, a height a number above 0, min_height not above max_height), a limits argument naming no file without write_limits, or one no file could be written at (a file that is not .json, a folder, a character a path may not hold), is a tool error that writes nothing. "
        + "A mesh with warnings still exports, and the result repeats them (a subdivided one carries a not-planar warning per curved quad, tens of thousands of characters that a client may cut; the file is written all the same, and check or select faces where non_planar tells the same); so do a skinned or clips export's clip warnings (see define_clip: a looping clip that does not close, a root that ends elsewhere on the ground plane). export changes nothing: not the mesh, its ids, or what undo steps back to. "
        + "Returns one line: exported <file> to <absolute path> (replaced, when it was): <n> triangles, 1 material 'carve' (or each material, when there are several) (the colours' and each material's name), size <x> x <y> x <z> m, "
        + "bounds (<min x, y, z>) to (<max x, y, z>), then where the limits came from (limits from <file>, or no game limits), then any warnings; a skinned export's line names its pieces, materials, bones and clips instead of the one material. "
        + "mode: 'model' (the default) writes all of the above; 'clips' writes the armature, skins[0] and the animations with no mesh, no node holding a mesh and no material, "
        + "which is one file of clips for every character that shares the skeleton: a game loads it with IModelGeometryLoader.LoadAnimationSet (the same bones in the same order and the same clips as the model file) and retargets it, "
        + "and the model loader refuses it as 'glTF contains no meshes.'. The skin stays in a clips file because without it the loader reorders bones and the tracks line up with the wrong ones. "
        + "Clips mode needs no mesh: a skeleton and clips alone (a clip library) export. It is refused, writing nothing, for a model with no skeleton or no clips. "
        + "A role map (*.humanoid.json) and clip events (*.events.json) are sidecars the game writes beside the file; carve writes none. Clips mode has no mesh, so no limit warnings, though it reads and writes the limits file as model mode does. "
        + "mode: 'piece' with meshes (the names of meshes in the model) writes just those meshes as one costume piece for a character that swaps parts at runtime: the same file layout as the model mode (one glTF mesh named after the file, a primitive per mesh and colour, the same material names), "
        + "with the model's skeleton whole, every bone in the same order with the same inverse binds, even bones no vertex of the piece follows, and no animations. It is never trimmed to the bones used, because a game binds the piece to the body's palette by position. "
        + "A game loads the piece and shares the body's SkinnedPose.Palette: the body is the driver and the only entity holding an Animator, and the piece reads the palette, so it follows the body's animation. "
        + "Example: export {path: 'models/orc.hat.glb', mode: 'piece', meshes: ['Hat']}, loaded beside models/orc.glb. The meshes keep the model's order. "
        + "It is refused, writing nothing, for an empty or missing meshes list, a name that is not a mesh (the error lists the model's mesh names) and a model with no skeleton; a vertex with no weight in a chosen mesh is refused as in the model mode, and allow_unweighted: true applies. "
        + "A piece warns on the game's max_triangles and max_submeshes as the model mode does, not on height, and reads and writes the limits file as model mode does. "
        + "The .glb is for the game: it keeps no faces, only triangles, so the .carve file is the one to keep working in. "
        + "The whole sequence from an empty file to a game, with save and open, is the worked example in save's description. "
        + "Worked example of a material: define_material {name: 'wood', base_colour: '#C8A06E', roughness: 0.6, base_colour_texture: 'wood.png'}, project_uvs, set_material {faces: [0, 1], material: 'wood'}, then export: the .glb holds primitives 'carve' and 'wood', 'wood' with baseColorTexture holding wood.png's bytes, the result line says 2 materials 'carve', 'wood', and the game's loader reads 'wood' back as that material. "
        + "Worked example, writing into a game's content folder (put the game's folder for <game>; the first export for a game can add limits: \"<game>/Content\" and write_limits: true to start <game>/Content/carve-limits.json); the game then loads it as "
        + "models.LoadModel(new ContentPath(\"models/crate.glb\")):\n"
        + ExampleMarker + SaveTool.WorkedExport;

    public static McpServerTool Create()
    {
        var schema = PathSchema("The .glb file to write, absolute or relative to carve's working directory; an existing file is an error unless overwrite is true.");
        schema["properties"]!.AsObject()["allow_unweighted"] = new JsonObject
        {
            ["description"] = "With a skeleton: export even when some vertices have no weight, binding them wholly to the first root bone, for work in progress. Default false.",
            ["type"] = "boolean",
        };
        schema["properties"]!.AsObject()["limits"] = new JsonObject
        {
            ["description"] = "The game's limits file: a .json file, or a folder holding carve-limits.json, absolute or relative to carve's working directory. Left out, the first carve-limits.json in the .glb's folder or a folder above it.",
            ["type"] = "string",
        };
        schema["properties"]!.AsObject()["mode"] = new JsonObject
        {
            ["description"] = "model (default): the meshes, and with a skeleton the skin and clips. clips: the armature, skin and animations with no mesh, for a skeleton with clips. piece: the meshes named in meshes, with the whole skeleton and no animations, for a costume swapped at runtime.",
            ["type"] = "string",
            ["enum"] = new JsonArray("model", "clips", "piece"),
        };
        schema["properties"]!.AsObject()["meshes"] = new JsonObject
        {
            ["description"] = "With mode piece: the names of the model's meshes to write, at least one. Not used by the other modes.",
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string" },
        };
        schema["properties"]!.AsObject()["overwrite"] = new JsonObject
        {
            ["description"] = "Replace the file if it already exists. Default false: an existing file is a tool error and is left unchanged.",
            ["type"] = "boolean",
        };
        schema["properties"]!.AsObject()["write_limits"] = new JsonObject
        {
            ["description"] = "Fill the keys the limits file is missing with Orcward's starting numbers (5000 triangles, 8 submeshes, 1.4 to 2.4 m), creating it beside the .glb when none was named or found. Keys already set are kept. Default false.",
            ["type"] = "boolean",
        };
        return MeshToolSupport.Create<ExportTool>("export", Text, schema);
    }

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null, JsonElement? allow_unweighted = null, JsonElement? limits = null, JsonElement? mode = null, JsonElement? overwrite = null, JsonElement? write_limits = null, JsonElement? meshes = null)
    {
        var errors = UnknownArguments("export", context, "path", "allow_unweighted", "limits", "mode", "meshes", "overwrite", "write_limits");
        var text = RequiredString("path", path, errors);
        var allowUnweighted = OptionalBool("allow_unweighted", allow_unweighted, errors);
        var replace = OptionalBool("overwrite", overwrite, errors);
        var writeLimits = OptionalBool("write_limits", write_limits, errors);
        string? limitsPath = null;
        if (!Absent(limits))
        {
            if (limits!.Value.ValueKind == JsonValueKind.String) limitsPath = limits.Value.GetString();
            else errors.Add($"limits: expected a folder or a .json file as a string, got {Describe(limits.Value)}");
        }
        var kind = "model";
        if (!Absent(mode))
        {
            var value = mode!.Value.ValueKind == JsonValueKind.String ? mode.Value.GetString() : null;
            if (value is "model" or "clips" or "piece") kind = value;
            else errors.Add($"mode: expected \"model\", \"clips\" or \"piece\", got {Describe(mode.Value)}");
        }
        var clips = kind == "clips";
        List<string>? pieceMeshes = null;
        if (!Absent(meshes))
        {
            if (kind != "piece") errors.Add("meshes: only mode piece takes meshes");
            else if (meshes!.Value.ValueKind != JsonValueKind.Array || meshes.Value.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String))
                errors.Add($"meshes: expected an array of mesh names, got {Describe(meshes.Value)}");
            else pieceMeshes = [.. meshes.Value.EnumerateArray().Select(x => x.GetString()!)];
        }
        else if (kind == "piece") pieceMeshes = [];
        if (errors.Count > 0) return Failed("export", errors);

        var model = store.Current.Model;
        var mesh = model.Combined;
        if (mesh is null && !clips) return Error("export: " + NoMesh); // clips mode needs no mesh (the person, 2026-11-09)

        var target = ToolPaths.Resolve("export", text, Extension, out var pathError);
        if (target is null) return Error(pathError!);

        // T-51: the game's limits, read and checked before anything is written.
        var source = LimitsSource.Find("export", limitsPath, writeLimits, target.FullPath, out var limitsError);
        if (source is null) return Error(limitsError!);

        var written = clips ? Clips(model, target, replace, source)
            : kind == "piece" ? Piece(model, pieceMeshes ?? [], target, allowUnweighted, replace, source)
            : model.Skeleton is not null ? Skinned(model, target, allowUnweighted, replace, source)
            : Static(model, mesh!, target, replace, source);
        if (written.IsError == true || !source.NeedsWrite) return written;

        // The .glb is written; the limits file follows it, so a refused export writes neither.
        if (!source.Write("export", out var writeLimitsError))
            return Error($"{writeLimitsError} The .glb was written: {TextOf(written)}");
        return Line(TextOf(written).Replace(source.Describe(written: false), source.Describe(written: true), StringComparison.Ordinal));
    }

    private static string TextOf(CallToolResult result) => ((TextContentBlock)result.Content[0]).Text;

    /// <summary>A model with no skeleton: every mesh merged into one, as T-30 writes it, warned on the game's limits.</summary>
    private static CallToolResult Static(Model model, PolygonMesh mesh, ToolPath target, bool overwrite, LimitsSource source)
    {
        var triangles = Shading.Triangles(mesh);
        var bytes = GlbExport.Write(mesh, triangles, target.Stem, model.Materials);
        if (!ToolPaths.Write("export", target, bytes, out var replaced, out var writeError, overwrite)) return Error(writeError!);

        var warnings = source.Limits.Warnings(model, triangles.Count, GlbExport.MaterialNames(mesh, model.Materials).Length);
        return Line(Summary(mesh, GlbExport.MaterialNames(mesh, model.Materials), target, replaced, triangles.Count, source.Describe(written: false), warnings,
            [.. GlbExport.UvWarnings(model), .. GlbExport.MaterialUvWarnings(model)]));
    }

    /// <summary>Clips mode: the armature, skin and animations with no mesh, or a refusal that writes nothing.</summary>
    private static CallToolResult Clips(Model model, ToolPath target, bool overwrite, LimitsSource source)
    {
        if (ClipsExport.Refusal(model) is { } refusal) return Error($"export: mode clips: {refusal}. Nothing was written.");
        var bytes = ClipsExport.Write(model, target.Stem);
        if (!ToolPaths.Write("export", target, bytes, out var replaced, out var writeError, overwrite)) return Error(writeError!);

        var skeleton = model.Skeleton!;
        static string N(int x) => x.ToString(CultureInfo.InvariantCulture);
        var warnings = ClipChecks.Check(model);
        return Line($"exported {target.Written(replaced)} as clips, with no mesh: "
            + $"{N(skeleton.Bones.Count)} bone(s) {NameList.Quoted(skeleton.Names)}, {N(model.Clips.Count)} clip(s) {NameList.Quoted(model.Clips.Select(c => c.Name))}"
            + source.Describe(written: false)
            + (warnings.Count > 0 ? $"; {N(warnings.Count)} warning(s): " + string.Join("; ", warnings) : ""));
    }

    /// <summary>Piece mode: the named meshes with the whole skeleton and no clips, or a refusal that writes nothing.</summary>
    private static CallToolResult Piece(Model model, List<string> meshes, ToolPath target, bool allowUnweighted, bool overwrite, LimitsSource source)
    {
        if (PieceExport.Refusal(model, meshes) is { } refusal) return Error($"export: mode piece: {refusal}. Nothing was written.");
        return Skinned(PieceExport.Select(model, meshes), target, allowUnweighted, overwrite, source, asPiece: true);
    }

    /// <summary>A model with a skeleton: the skinned file, or a refusal listing unweighted vertices that writes nothing.</summary>
    private static CallToolResult Skinned(Model model, ToolPath target, bool allowUnweighted, bool overwrite, LimitsSource source, bool asPiece = false)
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
        if (!ToolPaths.Write("export", target, glb.Bytes, out var replaced, out var writeError, overwrite)) return Error(writeError!);

        var skeleton = model.Skeleton!;
        var all = model.Meshes.SelectMany(m => m.Mesh.LiveVertices.Select(v => m.Mesh.Vertices[v])).ToList();
        var min = all.Aggregate(Vector3.Min);
        var max = all.Aggregate(Vector3.Max);
        static string F(float x) => x.ToString(CultureInfo.InvariantCulture);
        static string Size(float lo, float hi) => F((float)((double)hi - lo));
        static string Point(Vector3 v) => $"({F(v.X)}, {F(v.Y)}, {F(v.Z)})";

        var line = $"exported {target.Written(replaced)} {(asPiece ? "as a piece" : "skinned")}: "
            + $"{N(glb.Pieces.Count)} piece(s) {NameList.Quoted(glb.Pieces)}, {N(glb.Materials.Count)} material(s) {NameList.Quoted(glb.Materials)}, "
            + $"{N(skeleton.Bones.Count)} bone(s) {NameList.Quoted(skeleton.Names)}, {N(model.Clips.Count)} clip(s)"
            + (model.Clips.Count > 0 ? " " + NameList.Quoted(model.Clips.Select(c => c.Name)) : "")
            + $", {N(glb.Triangles)} triangles, size {Size(min.X, max.X)} x {Size(min.Y, max.Y)} x {Size(min.Z, max.Z)} m, bounds {Point(min)} to {Point(max)}";
        if (glb.BoundToRoot > 0)
            line += $"; allow_unweighted: {N(glb.BoundToRoot)} vertices with no weight were bound to the first root bone '{skeleton.Bones[0].Name}'";

        line += source.Describe(written: false);
        var warnings = source.Limits.Warnings(model, glb.Triangles, glb.Primitives, wholeBody: !asPiece).ToList();
        warnings.AddRange(GlbExport.UvWarnings(model));
        warnings.AddRange(GlbExport.MaterialUvWarnings(model));
        warnings.AddRange(ClipChecks.Check(model));
        foreach (var piece in model.Meshes)
            warnings.AddRange(MeshReport.Create(piece.Mesh).Warnings.Select(w => $"mesh '{piece.Name}': " + w.ToString().ReplaceLineEndings(" ")));
        if (warnings.Count > 0)
            line += $"; {N(warnings.Count)} warning(s): " + string.Join("; ", warnings);
        return Line(line);
    }

    /// <summary>The result line; numbers are culture-invariant.</summary>
    private static string Materials(string[] names)
    {
        return names.Length == 1
            ? $"1 material '{names[0]}'"
            : $"{names.Length.ToString(CultureInfo.InvariantCulture)} materials {NameList.Quoted(names)}";
    }

    private static string Summary(PolygonMesh mesh, string[] materialNames, ToolPath target, bool replaced, int triangles, string limits, IReadOnlyList<string> limitWarnings, IReadOnlyList<string> uvWarnings)
    {
        var report = MeshReport.Create(mesh);
        var (min, max) = (report.BoundsMin, report.BoundsMax);
        static string N(float x) => x.ToString(CultureInfo.InvariantCulture);
        static string Size(float lo, float hi) => N((float)((double)hi - lo));
        static string Point(Vector3 v) => $"({N(v.X)}, {N(v.Y)}, {N(v.Z)})";

        var line = $"exported {target.Written(replaced)}: "
            + $"{triangles.ToString(CultureInfo.InvariantCulture)} triangles, {Materials(materialNames)}, "
            + $"size {Size(min.X, max.X)} x {Size(min.Y, max.Y)} x {Size(min.Z, max.Z)} m, bounds {Point(min)} to {Point(max)}"
            + limits;
        var warnings = limitWarnings.Concat(report.Warnings.Select(w => w.ToString().ReplaceLineEndings(" "))).Concat(uvWarnings).ToList();
        if (warnings.Count > 0)
            line += $"; {warnings.Count.ToString(CultureInfo.InvariantCulture)} warning(s): " + string.Join("; ", warnings);
        return line;
    }
}
