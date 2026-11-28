using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using Carve.Graphs;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>A material as the tools report it: every field, each texture slot as a PNG's absolute path and size or a graph's absolute path, output and size, null where there is none.</summary>
internal static class MaterialJson
{
    public static Dictionary<string, object?> Of(Material m)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = m.Name,
            ["base_colour"] = m.BaseColour,
            ["metallic"] = m.Metallic,
            ["roughness"] = m.Roughness,
            ["emissive"] = m.Emissive,
            ["occlusion"] = m.Occlusion,
            ["double_sided"] = m.DoubleSided,
        };
        foreach (var (field, texture) in m.Textures)
            body[field] = texture switch
            {
                MaterialTexture png => new { path = png.Path, width = png.Width, height = png.Height },
                MaterialGraph graph => (object)new { graph = graph.Path, output = graph.Output, size = graph.Size, reads_mesh = ReadsMesh(graph.Path) },
                _ => null,
            };
        return body;
    }

    /// <summary>Whether the graph at <paramref name="path"/> reads the mesh (a mesh node in it or a nested file), or null when it cannot be read now.</summary>
    internal static bool? ReadsMesh(string path)
    {
        try
        {
            return Carve.Graphs.GraphSlotBake.Read(path).ReadsMesh;
        }
        catch (Exception e) when (GraphSlotProblem.IsCause(e))
        {
            return null;
        }
    }

    /// <summary>
    /// The warnings for <paramref name="model"/>'s graph slots that cannot be baked (T-160, T-166): a file gone or not a texture graph,
    /// a fault in the graph, an output it lacks or the slot cannot take. open keeps the slot and says so, in <see cref="GraphSlotProblem"/>'s
    /// words, so the material can be defined again.
    /// </summary>
    public static IEnumerable<string> BrokenGraphs(Model model) =>
        model.Materials.SelectMany(GraphSlotProblem.Slots)
            .Select(p => p + "; the slot is kept, and render and export fail on it until the file is there and right, or define_material names another");

    /// <summary>The warning for a material with an occlusion strength and no occlusion map, or null: glTF scales only the map.</summary>
    public static string? OcclusionWarning(Material m) =>
        m.OcclusionHasNoEffect
            ? string.Create(CultureInfo.InvariantCulture, $"occlusion: {m.Occlusion:R} has no effect without an occlusion_texture (glTF scales only the map); give one or leave occlusion at 1")
            : null;
}

public sealed class DefineMaterialTool(ModelStore store, ToolFolder toolFolder)
{
    public const string Text =
        "Defines a named material, the PBR look of a surface: define_material(name, and any of base_colour, metallic, roughness, emissive, occlusion, double_sided and five texture slots). "
        + "Every field you leave out is the default, so a bare name is matte white; defining a name that exists replaces that material whole (fields left out go back to their defaults) and says so. Faces get a material with set_material. "
        + "Fields: base_colour (sRGB hex #RRGGBB, default #FFFFFF, multiplied with base_colour_texture), metallic (0 to 1, default 0), roughness (0 to 1, default 1: matte, 0 a mirror), emissive (sRGB hex, default #000000: none; with an emissive_texture and no emissive it becomes #FFFFFF), occlusion (0 to 1, default 1; with no occlusion_texture it has no effect and the result warns of that), double_sided (default false; leave it false for a closed solid). "
        + "A texture is a PNG path, relative to carve's working folder or absolute, read when you call this: a later change to the file does not reach the model until you define the material again. "
        + "base_colour_texture and emissive_texture are sRGB colour; normal_texture is a linear tangent-space normal map; metallic_roughness_texture is linear, roughness in green and metallic in blue; occlusion_texture is linear, occlusion in red. "
        + "Any slot may instead be a texture graph's output, {graph, output, size}: graph a .texgraph path resolved as a PNG's is, output its name, size a power of two from 64 to 4096 (default the graph's own, clamped). Unlike a PNG, a graph is read again at every render and export, so a change saved to it shows there, and one removed or broken later fails them. render draws a graph material, and export bakes it and writes the baked PNGs. "
        + "A texture is drawn only on faces that have UVs (project_uvs, unwrap or set_uvs); check warns of a textured face without them. Materials are opaque: there is no transparency. "
        + "One undo step. "
        + "Returns JSON {replaced: bool, material:{name and every other field above, as taken}, warnings:[]}, "
        + "each texture null, {path, width, height} for a PNG or {graph, output, size (the one taken), reads_mesh (whether the graph reads the mesh; null if its file cannot be read now)} for a graph, paths absolute. get_mesh lists materials the same way. "
        + "Errors, each a tool error naming the argument, that changes nothing: an empty name, one with ':', '#' or '@', or exactly 'carve'; a colour that is not #RRGGBB; metallic, roughness or occlusion outside 0 to 1; a texture path that does not end in .png, names a folder or does not exist (the .png files in its folder are listed); a texture that is not a PNG or is outside 1 to 4096 pixels a side; a graph slot with another key, or without graph or output; a graph that does not end in .texgraph, does not exist (the .texgraph files in its folder are listed), is not a texture graph or has faults (all named); an output not [a-z][a-z0-9_]*, one the graph lacks (its outputs are listed) or one the slot cannot take; any other size; an argument define_material does not take. "
        + "Worked example, a rough wooden material with a texture (set_material's own example puts it on faces):\n"
        + ExampleMarker + """{"name":"crate","roughness":0.8,"base_colour_texture":"textures/crate.png"}"""
        + "\nWorked example of graph slots, graphs/iron.texgraph a graph of size 512 with base_colour and roughness outputs:\n"
        + ExampleMarker + """{"name":"weathered-iron","base_colour_texture":{"graph":"graphs/iron.texgraph","output":"base_colour","size":256},"metallic_roughness_texture":{"graph":"graphs/iron.texgraph","output":"roughness"}}"""
        + "\n"
        + """More: help {"tool":"define_material"} gives how the game's shading reads each field, the glow and occlusion rules, and each texture's colour space and channels.""";

    /// <summary>What left the description (T-207), in the same words: <c>help {"tool":"define_material"}</c> gives it.</summary>
    public const string Reference =
        "How the game's PBR shading reads each field: "
        + "base_colour is the surface colour, sRGB hex #RRGGBB, default #FFFFFF, multiplied with base_colour_texture when there is one. "
        + "metallic, 0 to 1, default 0: 1 is a metal (no diffuse colour: it shows by reflecting its surroundings); 0 is a dielectric such as wood or plastic. "
        + "roughness, 0 to 1, default 1: 1 is matte, 0 is a mirror. "
        + "emissive is light the surface gives off, sRGB hex, default #000000 (none); it is not lit or shaded. The glow is emissive times emissive_texture, so with an emissive_texture and no emissive, emissive becomes #FFFFFF; one you give, even #000000, is kept. "
        + "occlusion, 0 to 1, default 1, is how strongly the occlusion map darkens crevices; with no occlusion_texture it has no effect and the result warns of that. "
        + "double_sided, default false, draws the back of a face too; leave it false for a closed solid. "
        + "base_colour_texture and emissive_texture are sRGB colour. normal_texture is a tangent-space normal map and is linear: its pixels are directions, not colours. "
        + "metallic_roughness_texture is linear with roughness in the green channel and metallic in the blue channel (red is unused). occlusion_texture is linear with occlusion in the red channel. "
        + "A face has either a colour (set_colour) or a material, and giving it one clears the other. ";

    private static readonly string[] Known =
    [
        "name", "base_colour", "metallic", "roughness", "emissive", "occlusion", "double_sided",
        "base_colour_texture", "metallic_roughness_texture", "normal_texture", "emissive_texture", "occlusion_texture", "verbosity",
    ];

    private static JsonObject Str(string text) => new() { ["description"] = text, ["type"] = "string" };

    // A texture slot: a PNG path, or a graph output (T-160).
    private static JsonObject Slot(string text) => new()
    {
        ["description"] = text + " Or a graph output {graph, output, size}.",
        ["anyOf"] = new JsonArray(new JsonObject { ["type"] = "string" }, new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["graph"] = Str("A .texgraph path, resolved as a .png path is."),
                ["output"] = Str("The graph output's name."),
                ["size"] = new JsonObject { ["description"] = "A power of two from 64 to 4096; default the graph's own size.", ["type"] = "integer" },
            },
            ["required"] = new JsonArray("graph", "output"),
            ["additionalProperties"] = false,
        }),
    };

    private static readonly string[] GraphKeys = ["graph", "output", "size"];

    private static JsonObject Factor(string text) => new() { ["description"] = text, ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 };

    public static McpServerTool Create() => MeshToolSupport.Create<DefineMaterialTool>("define_material", Text, WithVerbosity(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = Str("The material's name; not empty, no ':', '#' or '@', not 'carve'. An existing name is replaced."),
            ["base_colour"] = Str("sRGB hex #RRGGBB; default #FFFFFF."),
            ["metallic"] = Factor("0 to 1; default 0. 1 is a metal."),
            ["roughness"] = Factor("0 to 1; default 1. 1 is matte."),
            ["emissive"] = Str("sRGB hex #RRGGBB of light given off; default #000000."),
            ["occlusion"] = Factor("0 to 1; default 1. The occlusion map's strength; no effect without occlusion_texture."),
            ["double_sided"] = new JsonObject { ["description"] = "Draw the back of faces too; default false.", ["type"] = "boolean" },
            ["base_colour_texture"] = Slot("A .png path (relative to carve's working folder, or absolute); sRGB colour."),
            ["metallic_roughness_texture"] = Slot("A .png path; linear, roughness in G, metallic in B."),
            ["normal_texture"] = Slot("A .png path; a linear tangent-space normal map."),
            ["emissive_texture"] = Slot("A .png path; sRGB colour."),
            ["occlusion_texture"] = Slot("A .png path; linear, occlusion in R."),
        },
        ["required"] = new JsonArray("name"),
    }), "base_colour_texture", "base_colour_texture.graph", "metallic_roughness_texture", "metallic_roughness_texture.graph", "normal_texture", "normal_texture.graph", "emissive_texture", "emissive_texture.graph", "occlusion_texture", "occlusion_texture.graph");

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? name = null, JsonElement? base_colour = null, JsonElement? metallic = null, JsonElement? roughness = null,
        JsonElement? emissive = null, JsonElement? occlusion = null, JsonElement? double_sided = null,
        JsonElement? base_colour_texture = null, JsonElement? metallic_roughness_texture = null, JsonElement? normal_texture = null,
        JsonElement? emissive_texture = null, JsonElement? occlusion_texture = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("define_material", context, Known);
        var level = ReadVerbosity(verbosity, errors);
        var materialName = RequiredString("name", name, errors);
        var baseColour = OptionalString("base_colour", base_colour, errors);
        var emissiveColour = OptionalString("emissive", emissive, errors);
        var metal = Number("metallic", metallic, errors, required: false);
        var rough = Number("roughness", roughness, errors, required: false);
        var occ = Number("occlusion", occlusion, errors, required: false);
        var twoSided = OptionalBool("double_sided", double_sided, errors);
        var textures = new (string Field, JsonElement? Value)[]
        {
            ("base_colour_texture", base_colour_texture), ("metallic_roughness_texture", metallic_roughness_texture),
            ("normal_texture", normal_texture), ("emissive_texture", emissive_texture), ("occlusion_texture", occlusion_texture),
        };
        var loaded = new Dictionary<string, MaterialSlot>();
        foreach (var (field, value) in textures)
        {
            if (value is { ValueKind: JsonValueKind.Object } slot)
            {
                if (LoadGraph(field, slot, toolFolder.Directory, errors) is { } graph) loaded[field] = graph;
                continue;
            }
            var path = OptionalString(field, value, errors);
            if (path is null) continue;
            if (LoadTexture(field, path, toolFolder.Directory, out var texture, out var problem)) loaded[field] = texture!;
            else errors.Add(problem!);
        }
        // An emissive_texture with no emissive glows as it is (a default black emissive would hide it).
        var material = new Material(materialName ?? "", baseColour ?? Material.DefaultBaseColour, metal ?? Material.DefaultMetallic,
            rough ?? Material.DefaultRoughness,
            emissiveColour ?? (loaded.ContainsKey("emissive_texture") ? "#FFFFFF" : Material.DefaultEmissive), occ ?? Material.DefaultOcclusion, twoSided,
            loaded.GetValueOrDefault("base_colour_texture"), loaded.GetValueOrDefault("metallic_roughness_texture"),
            loaded.GetValueOrDefault("normal_texture"), loaded.GetValueOrDefault("emissive_texture"), loaded.GetValueOrDefault("occlusion_texture"));
        // Everything that is wrong with the values is reported with the argument problems, not one at a time. The loaded
        // textures were checked as they were read, so they add no problem here.
        if (errors.Count == 0 || materialName is not null)
        {
            // A new name is held to the rule for new names, which refuses '@' too (T-160).
            if (materialName is not null && Material.NameProblem(materialName) is { } bad) errors.Add($"name: {bad}");
            foreach (var (field, problem) in Material.Problems(material))
                if (field is not "name") errors.Add($"{field}: {problem}");
        }
        if (errors.Count > 0) return Failed("define_material", errors);

        var snapshot = store.DefineMaterial("define_material", material, out var replaced, out var problems);
        if (snapshot is null) return Failed("define_material", problems);

        var warnings = new List<string>();
        if (MaterialJson.OcclusionWarning(material) is { } warning) warnings.Add(warning);
        foreach (var (field, slot) in material.Textures)
            if (slot is MaterialGraph { } g && MaterialJson.ReadsMesh(g.Path) == true)
                warnings.Add($"{field}: the graph reads the mesh, so it is baked onto each mesh that wears '{material.Name}' (its UVs, its groups and materials); set_material it on a mesh with UVs that do not overlap, and check lists what stops it");
        return Quiet(Ok(new { replaced, material = MaterialJson.Of(material), warnings }), level);
    }

    /// <summary>Resolves (against <paramref name="baseDirectory"/>, the working directory when null), finds and reads one texture path; false with <paramref name="problem"/> (named by the argument) when it cannot.</summary>
    private static bool LoadTexture(string field, string path, string? baseDirectory, out MaterialTexture? texture, out string? problem) =>
        LoadTexture("define_material", field, path, baseDirectory, out texture, out problem);

    /// <summary>The same read for any tool that takes a PNG path (T-121: render's <c>image</c>), under M-7's rules and in the same words.</summary>
    internal static bool LoadTexture(string tool, string field, string path, string? baseDirectory, out MaterialTexture? texture, out string? problem)
    {
        texture = null;
        var target = ToolPaths.Resolve(tool, path, ".png", out var pathError, baseDirectory, verb: "reads");
        if (target is null)
        {
            problem = field + ": " + pathError![(tool + ": ").Length..];
            return false;
        }
        if (Directory.Exists(target.FullPath))
        {
            problem = $"{field}: {target.FullPath} is a folder, not a file";
            return false;
        }
        if (!File.Exists(target.FullPath))
        {
            problem = $"{field}: {Missing(target)}";
            return false;
        }
        texture = MaterialTexture.Load(target.FullPath, out var load);
        problem = texture is null ? $"{field}: {target.FullPath}: {load}" : null;
        return texture is not null;
    }

    /// <summary>
    /// One graph slot (T-160): its keys, the graph resolved as a texture path is (against <paramref name="baseDirectory"/>),
    /// found and read for its size and to refuse a file that is not a texture graph, then the size given or the graph's own,
    /// clamped into 64 to 4096. Null with each problem, named by the argument, in <paramref name="errors"/>; the output and the
    /// size are then checked by <see cref="Material.Problems"/>.
    /// </summary>
    private static MaterialGraph? LoadGraph(string field, JsonElement slot, string? baseDirectory, List<string> errors)
    {
        if (ParseGraph("define_material", field, slot, baseDirectory, errors, "a graph slot", "a graph slot") is not { } made) return null;
        // The faults, the output and the slot table, here rather than at the first render (T-166); a malformed output name is Material.Problems' to report.
        try
        {
            if (MaterialGraph.OutputProblem(made.Output) is null) GraphSlotBake.Validate(made, field);
        }
        catch (Exception e) when (GraphSlotProblem.IsCause(e))
        {
            errors.Add($"{field}.{(e is GraphOutputException ? "output" : "graph")}: {GraphSlotProblem.Cause(e)}");
            return null;
        }
        return made;
    }

    /// <summary>
    /// A <c>{graph, output, size}</c> argument of <paramref name="tool"/> (define_material's slots, render's image): its keys, the graph
    /// resolved and read, then the size given or the graph's own clamped into 64 to 4096. Null with each problem, named by
    /// <paramref name="field"/>, in <paramref name="errors"/>; <paramref name="takes"/> and <paramref name="needs"/> name the argument in
    /// the unknown-key and missing-key errors.
    /// </summary>
    internal static MaterialGraph? ParseGraph(string tool, string field, JsonElement slot, string? baseDirectory, List<string> errors, string takes, string needs)
    {
        var before = errors.Count;
        foreach (var key in slot.EnumerateObject().Select(p => p.Name).Where(k => !GraphKeys.Contains(k)))
            errors.Add($"{field}.{key}: {takes} takes only graph, output and size");
        string? Text(string key)
        {
            if (!slot.TryGetProperty(key, out var v)) errors.Add($"{field}: {needs} needs {key}");
            else if (v.ValueKind == JsonValueKind.String) return v.GetString();
            else errors.Add($"{field}.{key}: expected a string, got {Describe(v)}");
            return null;
        }
        var path = Text("graph");
        var output = Text("output");
        int? size = null;
        if (slot.TryGetProperty("size", out var s))
        {
            if (s.ValueKind == JsonValueKind.Number && s.TryGetInt32(out var n)) size = n;
            else errors.Add($"{field}.size: expected an integer, got {Describe(s)}");
        }
        if (errors.Count > before) return null;

        var target = ToolPaths.Resolve(tool, path, MaterialGraph.Extension, out var pathError, baseDirectory, verb: "reads");
        if (target is null)
        {
            errors.Add($"{field}.graph: " + pathError![$"{tool}: ".Length..]);
            return null;
        }
        if (Directory.Exists(target.FullPath))
        {
            errors.Add($"{field}.graph: {target.FullPath} is a folder, not a file");
            return null;
        }
        if (!File.Exists(target.FullPath))
        {
            errors.Add($"{field}.graph: {Missing(target, MaterialGraph.Extension)}");
            return null;
        }
        if (ReadGraph(target.FullPath, out var graph) is { } problem)
        {
            errors.Add($"{field}.graph: {target.FullPath} {problem}");
            return null;
        }
        return new MaterialGraph(target.FullPath, output!, size ?? Math.Clamp(graph!.Size, MaterialGraph.MinSize, MaterialTexture.MaxSize));
    }

    /// <summary>
    /// Reads the texture graph at <paramref name="fullPath"/>, or says why it cannot (worded to follow the path): no file,
    /// unreadable, or a load error. Validation faults are not problems here: what a graph's outputs carry is check's (T-166).
    /// </summary>
    internal static string? ReadGraph(string fullPath, out TextureGraph? graph)
    {
        graph = null;
        if (!File.Exists(fullPath)) return "is missing";
        try
        {
            var read = TexGraphFile.ReadFile(fullPath);
            graph = read.Graph;
            return graph is null ? $"is not a texture graph: {read.Error}" : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return $"could not be read: {e.Message}";
        }
    }

    /// <summary>The missing-file error: the resolved path and the files with <paramref name="extension"/> that are in its folder.</summary>
    internal static string Missing(ToolPath target, string extension = ".png") => GraphSlotProblem.Missing(target.FullPath, extension);
}

public sealed class SetMaterialTool(ModelStore store)
{
    public const string Text =
        "Gives faces of a mesh a material from define_material: set_material(material, and either faces or group). "
        + "Give exactly one of faces (face ids) or group (a name from define_group). A face has one material or one colour, never both: this clears the faces' colours, and set_colour clears a face's material. The last call wins. "
        + "Materials belong to the model, not to a mesh, so any mesh's faces may use any material. Faces keep their material through every edit (an extrude's walls take the material of the face they came from) and through undo, save and open. "
        + "A texture is drawn only on faces that have UVs, so give the faces UVs first with project_uvs, unwrap or set_uvs; the result warns of faces that have none, and check does too. A material whose graph reads the mesh also warns when this mesh cannot feed it: no UVs, overlapping UVs, a mesh_mask naming a group or material the mesh lacks. "
        + PartTargetText
        + "Returns JSON {mesh, material, faces:[ids given the material], warnings:[] (only when there are some)}. "
        + "Errors, each a tool error naming the argument, that changes nothing: no mesh yet, a material that is not defined (the defined names are listed), both or neither of faces and group, an unknown group, a face id the mesh does not have, an unknown mesh, an argument set_material does not take. "
        + "Worked example on define_mesh's unit cube: texture its top and front, faces 3 and 2, with the crate material. "
        + "1. project_uvs maps them: {\"method\":\"box\",\"faces\":[2,3]}. "
        + "2. define_material makes the material: {\"name\":\"crate\",\"roughness\":0.8,\"base_colour_texture\":\"textures/crate.png\"}. "
        + "3. set_material is this call, after which get_mesh lists faces 2 and 3 under crate, and check warns only of the other four faces, 0, 1, 4 and 5, which have no UVs (\"faces[0]: no UVs, so the face samples the texture's corner\"); they have no material and no texture, so nothing is drawn wrong:\n"
        + ExampleMarker + """{"material":"crate","faces":[2,3]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<SetMaterialTool>("set_material", Text, WithVerbosity(WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["material"] = new JsonObject { ["description"] = "The name of a material from define_material.", ["type"] = "string" },
            ["faces"] = FaceArguments.FacesSchema("Face ids to give the material. Give this or group, not both."),
            ["group"] = new JsonObject { ["description"] = "A group name from define_group. Give this or faces, not both.", ["type"] = "string" },
        },
        ["required"] = new JsonArray("material"),
    })));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? material = null, JsonElement? faces = null, JsonElement? group = null, JsonElement? mesh = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("set_material", context, "material", "faces", "group", "mesh", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var meshName = MeshName(mesh, errors);
        var materialName = RequiredString("material", material, errors);
        var f = Ids("faces", faces, errors);
        var groupName = OptionalString("group", group, errors);
        if (f is not null && groupName is not null) errors.Add("faces, group: give faces or group, not both");
        else if (f is null && groupName is null && errors.Count == 0) errors.Add("faces, group: give one of faces or group");
        else if (f is { Count: 0 }) errors.Add("faces: nothing to give a material: list at least one face");
        if (errors.Count > 0) return Failed("set_material", errors);

        IReadOnlyList<int>? given = null;
        var warnings = new List<string>();
        var snapshot = store.Restyle("set_material", meshName, (model, m) =>
        {
            var problems = new List<string>();
            var defined = model.FindMaterial(materialName!);
            if (defined is null)
                problems.Add(model.Materials.Count == 0
                    ? $"material: no material named '{materialName}'; the model defines none yet, define one with define_material"
                    : $"material: no material named '{materialName}'; the materials are {model.ListMaterialNames()}");
            var style = m.Style ?? FaceStyle.None;
            IReadOnlyList<int>? ids = f;
            if (groupName is not null)
            {
                ids = style.FacesOf(groupName);
                if (ids is null) problems.Add(NoGroup(style, groupName));
            }
            else FaceArguments.CheckIds(m, ids!, problems);
            if (problems.Count > 0) return (null, problems);
            given = ids!.Distinct().Order().ToArray();
            if (defined!.HasTexture)
            {
                var bare = given.Where(i => m.Uvs?.Of(i) is null).ToArray();
                if (bare.Length > 0)
                    warnings.Add($"faces[{string.Join(", ", bare)}]: no UVs, so the texture of '{materialName}' is not mapped onto them; give them UVs with project_uvs, unwrap or set_uvs");
            }
            return (style.WithMaterial(ids!, materialName!), problems);
        }, out var refused, out var problem);
        if (problem is not null) return Failed("set_material", [problem]);
        if (refused.Count > 0) return Failed("set_material", refused);
        if (snapshot is null) return Error("set_material: " + NoMesh);

        var name = snapshot.Model.Target(meshName, out _)!.Name;
        if (snapshot.Model.FindMaterial(materialName!) is { HasGraph: true } worn)
            warnings.AddRange(GraphSlotProblem.OnMesh(snapshot.Model, worn, name));
        return Quiet(warnings.Count == 0
            ? Ok(new { mesh = name, material = materialName, faces = given })
            : Ok(new { mesh = name, material = materialName, faces = given, warnings }), level);
    }
}
