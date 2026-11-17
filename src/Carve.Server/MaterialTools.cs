using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>A material as the tools report it: every field, each texture as its absolute path and size, null where there is none.</summary>
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
            body[field] = texture is null ? null : new { path = texture.Path, width = texture.Width, height = texture.Height };
        return body;
    }

    /// <summary>The warning for a material with an occlusion strength and no occlusion map, or null: glTF scales only the map.</summary>
    public static string? OcclusionWarning(Material m) =>
        m.OcclusionHasNoEffect
            ? string.Create(CultureInfo.InvariantCulture, $"occlusion: {m.Occlusion:R} has no effect without an occlusion_texture (glTF scales only the map); give one or leave occlusion at 1")
            : null;
}

public sealed class DefineMaterialTool(ModelStore store, ToolFolder toolFolder)
{
    public const string Text =
        "Defines a named material, the PBR look of a surface: define_material(name, and any of base_colour, metallic, roughness, emissive, occlusion, double_sided and five texture paths). "
        + "Every field you leave out is the default, so a bare name is matte white; defining a name that exists replaces that material whole (fields left out go back to their defaults) and says so. Faces get a material with set_material. "
        + "How the game's PBR shading reads each field: "
        + "base_colour is the surface colour, sRGB hex #RRGGBB, default #FFFFFF, multiplied with base_colour_texture when there is one. "
        + "metallic, 0 to 1, default 0: 1 is a metal, which shows only by reflecting its surroundings and has no diffuse colour of its own; 0 is a dielectric such as wood or plastic. "
        + "roughness, 0 to 1, default 1: 1 is matte, 0 is a mirror. "
        + "emissive is light the surface gives off, sRGB hex, default #000000 (none); it is not lit or shaded. The glow is emissive times emissive_texture, so a default black emissive would hide the texture: when you give an emissive_texture and no emissive, emissive becomes #FFFFFF (the texture shows as it is); an emissive you give, even #000000, is kept. "
        + "occlusion, 0 to 1, default 1, is how strongly the occlusion map darkens crevices; with no occlusion_texture it has no effect and the result warns of that. "
        + "double_sided, default false, draws the back of a face too; leave it false for a closed solid. "
        + "Textures are PNG files (a file that is not a PNG, or is larger than 4096 pixels a side, is refused), given as a path relative to carve's working folder or absolute, read and checked when you call this, so a later change to the file does not reach the model until you define the material again. "
        + "base_colour_texture and emissive_texture are sRGB colour. normal_texture is a tangent-space normal map and is linear: its pixels are directions, not colours. "
        + "metallic_roughness_texture is linear with roughness in the green channel and metallic in the blue channel (red is unused). occlusion_texture is linear with occlusion in the red channel. "
        + "A texture is drawn only on faces that have UVs (project_uvs, unwrap or set_uvs); check warns of a textured face without them. Materials are opaque: there is no transparency. "
        + "A face has either a colour (set_colour) or a material, and giving it one clears the other. One undo step. "
        + "Returns JSON {replaced: bool, material:{name, base_colour, metallic, roughness, emissive, occlusion, double_sided, base_colour_texture, metallic_roughness_texture, normal_texture, emissive_texture, occlusion_texture}, warnings:[]}, "
        + "each texture null or {path (absolute), width, height}. get_mesh lists the model's materials the same way. "
        + "Errors, each a tool error naming the argument, that changes nothing: an empty name, one with ':' or '#', or exactly 'carve'; a colour that is not #RRGGBB; metallic, roughness or occlusion outside 0 to 1; a texture path that does not end in .png, names a folder or does not exist (the error lists the .png files in the folder it looked in); a texture that is not a PNG or is outside 1 to 4096 pixels a side; an argument define_material does not take. "
        + "Worked example, a rough wooden material with a texture; set_material then puts it on faces, as its own example does on define_mesh's unit cube:\n"
        + ExampleMarker + """{"name":"crate","roughness":0.8,"base_colour_texture":"textures/crate.png"}""";

    private static readonly string[] Known =
    [
        "name", "base_colour", "metallic", "roughness", "emissive", "occlusion", "double_sided",
        "base_colour_texture", "metallic_roughness_texture", "normal_texture", "emissive_texture", "occlusion_texture",
    ];

    private static JsonObject Str(string text) => new() { ["description"] = text, ["type"] = "string" };

    private static JsonObject Factor(string text) => new() { ["description"] = text, ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 };

    public static McpServerTool Create() => MeshToolSupport.Create<DefineMaterialTool>("define_material", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = Str("The material's name; not empty, no ':' or '#', not 'carve'. An existing name is replaced."),
            ["base_colour"] = Str("sRGB hex #RRGGBB; default #FFFFFF."),
            ["metallic"] = Factor("0 to 1; default 0. 1 is a metal."),
            ["roughness"] = Factor("0 to 1; default 1. 1 is matte."),
            ["emissive"] = Str("sRGB hex #RRGGBB of light given off; default #000000."),
            ["occlusion"] = Factor("0 to 1; default 1. The occlusion map's strength; no effect without occlusion_texture."),
            ["double_sided"] = new JsonObject { ["description"] = "Draw the back of faces too; default false.", ["type"] = "boolean" },
            ["base_colour_texture"] = Str("A .png path (relative to carve's working folder, or absolute); sRGB colour."),
            ["metallic_roughness_texture"] = Str("A .png path; linear, roughness in G, metallic in B."),
            ["normal_texture"] = Str("A .png path; a linear tangent-space normal map."),
            ["emissive_texture"] = Str("A .png path; sRGB colour."),
            ["occlusion_texture"] = Str("A .png path; linear, occlusion in R."),
        },
        ["required"] = new JsonArray("name"),
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? name = null, JsonElement? base_colour = null, JsonElement? metallic = null, JsonElement? roughness = null,
        JsonElement? emissive = null, JsonElement? occlusion = null, JsonElement? double_sided = null,
        JsonElement? base_colour_texture = null, JsonElement? metallic_roughness_texture = null, JsonElement? normal_texture = null,
        JsonElement? emissive_texture = null, JsonElement? occlusion_texture = null)
    {
        var errors = UnknownArguments("define_material", context, Known);
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
        var loaded = new Dictionary<string, MaterialTexture>();
        foreach (var (field, value) in textures)
        {
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
            foreach (var (field, problem) in Material.Problems(material))
                if (field is not "name" || materialName is not null) errors.Add($"{field}: {problem}");
        if (errors.Count > 0) return Failed("define_material", errors);

        var snapshot = store.DefineMaterial("define_material", material, out var replaced, out var problems);
        if (snapshot is null) return Failed("define_material", problems);

        var warnings = new List<string>();
        if (MaterialJson.OcclusionWarning(material) is { } warning) warnings.Add(warning);
        return Ok(new { replaced, material = MaterialJson.Of(material), warnings });
    }

    /// <summary>Resolves (against <paramref name="baseDirectory"/>, the working directory when null), finds and reads one texture path; false with <paramref name="problem"/> (named by the argument) when it cannot.</summary>
    private static bool LoadTexture(string field, string path, string? baseDirectory, out MaterialTexture? texture, out string? problem)
    {
        texture = null;
        var target = ToolPaths.Resolve("define_material", path, ".png", out var pathError, baseDirectory, verb: "reads");
        if (target is null)
        {
            problem = field + ": " + pathError!["define_material: ".Length..];
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

    /// <summary>The missing-file error: the resolved path and the .png files that are in its folder.</summary>
    private static string Missing(ToolPath target)
    {
        var folder = Path.GetDirectoryName(target.FullPath)!;
        var head = $"no file at {target.FullPath}; ";
        if (!Directory.Exists(folder)) return head + $"the folder {folder} does not exist";

        List<string> names;
        try
        {
            names = Directory.EnumerateFiles(folder).Select(f => Path.GetFileName(f)!)
                .Where(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return head + $"the folder {folder} could not be listed: {e.Message}";
        }

        var inv = CultureInfo.InvariantCulture;
        if (names.Count == 0) return head + $"there are no .png files in {folder}";
        var shown = string.Join(", ", names.Take(OpenTool.ListLimit));
        var rest = names.Count > OpenTool.ListLimit ? $", and {(names.Count - OpenTool.ListLimit).ToString(inv)} more" : "";
        return head + $"the .png files in {folder} ({names.Count.ToString(inv)}) are: {shown}{rest}";
    }
}

public sealed class SetMaterialTool(ModelStore store)
{
    public const string Text =
        "Gives faces of a mesh a material from define_material: set_material(material, and either faces or group). "
        + "Give exactly one of faces (face ids) or group (a name from define_group). A face has one material or one colour, never both: this clears the faces' colours, and set_colour clears a face's material. The last call wins. "
        + "Materials belong to the model, not to a mesh, so any mesh's faces may use any material. Faces keep their material through every edit (an extrude's walls take the material of the face they came from) and through undo, save and open. "
        + "A texture is drawn only on faces that have UVs, so give the faces UVs first with project_uvs, unwrap or set_uvs; the result warns of faces that have none, and check does too. "
        + "Returns JSON {mesh, material, faces:[ids given the material], warnings:[] (only when there are some)}. "
        + "Errors, each a tool error naming the argument, that changes nothing: no mesh yet, a material that is not defined (the defined names are listed), both or neither of faces and group, an unknown group, a face id the mesh does not have, an unknown mesh, an argument set_material does not take. "
        + "Worked example on define_mesh's unit cube: texture its top and front, faces 3 and 2, with the crate material. "
        + "1. project_uvs maps them: {\"method\":\"box\",\"faces\":[2,3]}. "
        + "2. define_material makes the material: {\"name\":\"crate\",\"roughness\":0.8,\"base_colour_texture\":\"textures/crate.png\"}. "
        + "3. set_material is this call, after which get_mesh lists faces 2 and 3 under crate, and check warns only of the other four faces, 0, 1, 4 and 5, which have no UVs (\"faces[0]: no UVs, so the face samples the texture's corner\"); they have no material and no texture, so nothing is drawn wrong:\n"
        + ExampleMarker + """{"material":"crate","faces":[2,3]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<SetMaterialTool>("set_material", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["material"] = new JsonObject { ["description"] = "The name of a material from define_material.", ["type"] = "string" },
            ["faces"] = FaceArguments.FacesSchema("Face ids to give the material. Give this or group, not both."),
            ["group"] = new JsonObject { ["description"] = "A group name from define_group. Give this or faces, not both.", ["type"] = "string" },
        },
        ["required"] = new JsonArray("material"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? material = null, JsonElement? faces = null, JsonElement? group = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("set_material", context, "material", "faces", "group", "mesh");
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
        return warnings.Count == 0
            ? Ok(new { mesh = name, material = materialName, faces = given })
            : Ok(new { mesh = name, material = materialName, faces = given, warnings });
    }
}
