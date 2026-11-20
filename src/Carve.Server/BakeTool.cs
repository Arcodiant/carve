using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using Carve.Core.Baking;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StbImageWriteSharp;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>
/// <c>bake</c> (M-24, T-122): writes a mesh's maps as PNGs for <c>define_material</c>. The Core does the work
/// (<see cref="BakeLayout"/>, <see cref="Bake.Masks"/>, <see cref="Bake.Pad"/>); this is the tool surface, the files
/// and the result. It changes no model state, so it adds no undo entry.
/// </summary>
public sealed class BakeTool(ModelStore store, ToolFolder toolFolder)
{
    public const string MasksMap = "masks";
    public const string BaseColourMap = "base_colour";
    public const string CurvatureMap = "curvature";
    public const string OcclusionMap = "occlusion";
    public const string OrmMap = "orm";
    public const string NormalMap = "normal";

    /// <summary>The maps there are; T-124 to T-126 add theirs.</summary>
    public static readonly string[] Maps = [MasksMap, BaseColourMap, CurvatureMap, OcclusionMap, OrmMap, NormalMap];

    /// <summary>The maps a call with no <c>maps</c> bakes: the ones that need no block.</summary>
    public static readonly string[] DefaultMaps = [MasksMap, BaseColourMap];

    public const string DefaultFolder = "textures";
    public const int DefaultSize = 1024;
    public const int DefaultPadding = 8;

    /// <summary>The example call in the description; a test runs it on <c>samples/wrench.carve</c>.</summary>
    public const string Example = """{"mesh":"wrench","folder":"textures","size":512,"maps":["masks","base_colour","orm"],"surface":{"all":{"base_colour":"#80858A","roughness":0.4,"metallic":1},"rubber":{"base_colour":"#282A2E","roughness":0.88,"metallic":0},"red":{"base_colour":"#B02A22"}},"occlusion":{"rays":32,"reach":0.035}}""";

    public const string Text =
        "Bakes the mesh's maps into PNG files that define_material takes as they come back: the model is not changed (no undo step, nothing to save). "
        + "A bake is worked in the mesh's UV layout, one texel for each point of the square [0, 1] x [0, 1] with (0, 0) at the image's top-left, so the mesh needs UVs (unwrap, project_uvs or set_uvs) and no two faces may share a texel; the error says which faces do. "
        + "Faces with no UVs are skipped, listed in skipped_faces and named in a warning. "
        + "Arguments: mesh (the mesh to bake; required when the model has several, as render's is), folder (where the PNGs go, relative to carve's working folder like export's paths, or absolute; created when missing; default 'textures'), "
        + "size (the image's side in texels, a power of two from 64 to 4096; default 1024), curvature (below), padding (texels of the island's edge colour spread past it so filtering and mip maps never reach the empty background, 0 to size/2; default 8), "
        + "maps (which maps to bake, at least one of 'masks', 'base_colour', 'curvature', 'occlusion', 'orm' and 'normal'; default masks and base_colour), surface and occlusion (below) and bevel ({width}, for 'normal'). "
        + "The maps: 'masks' writes one single-channel grey PNG for each face group, <folder>/<mesh>.mask.<group>.png, and one for each material the mesh's faces use, <folder>/<mesh>.material.<name>.png (a group and a material with one name never collide): a texel is white (255) where its face is in the group or has the material and black (0) elsewhere, then padded; a group with no faces gives an all-black mask. "
        + "'base_colour' writes <folder>/<mesh>.base_colour.png, an RGB PNG of sRGB bytes, so define_material's base_colour_texture reads it as it is: each texel takes its face's colour, which is, in this order, the base_colour of the last surface entry that names a group the face is in or its material; else the face's own colour (set_colour); else its material's base_colour; else white (#FFFFFF). "
        + "'occlusion' writes <folder>/<mesh>.occlusion.png, a single-channel grey PNG of ambient occlusion, linear with no curve: each texel casts rays over the hemisphere about its face's normal against the meshes at rest (their own positions, never a pose; both sides of a face block), a ray meeting geometry at distance t under reach occludes by 1 - (t / reach)^2, so distant geometry only tints, and the texel is 1 - the mean (white 255 open, black 0 closed); the rays are a fixed set, so two bakes are byte-identical. "
        + "'orm' writes <folder>/<mesh>.orm.png, the RGB texture glTF reads for both occlusion_texture (R) and metallic_roughness_texture (G roughness, B metallic), linear: R is the occlusion above, G and B each face's roughness and metallic from surface, resolved as base_colour is (the last entry naming the face's group or material that gives it; else its material's; else roughness 1 and metallic 0). "
        + "occlusion is {rays: 1 to 1024, default 32; reach: metres, required for 'occlusion' and 'orm' and with no default, the distance within which geometry darkens a texel; occluders: the mesh names that occlude, every mesh of the model by default}. "
        + "'normal' writes <folder>/<mesh>.normal.png, an RGB tangent-space normal map that rounds the mesh's hard edges over bevel.width metres, as a modelled bevel would, with no extra geometry: the shading breaks render draws hard (a smooth mesh's marked hard edges, every edge between faces that are not coplanar on a flat one). On a break edge the normal is the mean of the two faces' normals, easing by smoothstep to the shading normal at width from the edge, measured in 3D, so it runs on across a UV seam; elsewhere it is the shading normal itself, (128, 128, 255). It is glTF's normal map, green up, in the engine's own tangent frame (no tangents are exported), so give it to define_material as normal_texture and render shows the rounded edge as the game will. bevel is {width: metres, more than 0}, required with 'normal' (there is no default; a few texels' worth is typical, e.g. 0.0016 on a 0.3 m tool at 1024); given without 'normal' in maps it is ignored with a warning, as the curvature block is without 'curvature'. "
        + "surface is an object of entries, each keyed by a group or material name, applied in the order given, so a later entry wins a face that two of them name: put a group that holds every face first as a default and the specific groups after it. "
        + "An entry is {base_colour: '#RRGGBB', roughness: 0 to 1, metallic: 0 to 1}, each optional and validated; an entry with no base_colour gives no colour to base_colour. roughness and metallic are for maps that read them and make no warning when no map in this call does. A block no asked map reads (occlusion with neither 'occlusion' nor 'orm' in maps) is ignored with a warning. "
        + "'curvature' writes <folder>/<mesh>.curvature.png, a single-channel grey PNG for wear: 128 (0.5) on flat ground, lighter toward a convex edge and darker toward a concave one, over the width given in the curvature block {width} (metres, greater than 0, required when 'curvature' is a map). "
        + "Each break edge (an edge the render draws hard: a marked hard edge on a smooth mesh, any edge between two faces that are not coplanar on a flat one) within width of a texel on its face adds half of its dihedral bend over a right angle (capped at 1), signed by convex (+) or concave (-), faded from full on the edge to nothing at width; the sum is clamped, so a right-angle convex edge reads 255 and a right-angle crease 0. "
        + "A material does not take the curvature map directly (it is not a colour, roughness or normal); it is for wear masks in a texture graph, or a tool of your own. "
        + "Returns JSON {files:[{map, name (a mask's group or material), path, size, replaced, coverage, min, max, mean, ms}], skipped_faces:[face ids], warnings:[]}. "
        + "path is relative to carve's working folder with '/' separators (absolute when folder is), the form define_material takes. replaced is true when a file of that name was there and was overwritten: bake always overwrites. "
        + "ms is the time the file took to bake and write, in milliseconds; work two files share (the masks, the occlusion the orm reads) is counted in the first of them. "
        + "coverage is the fraction of the image's texels the faces cover, before padding. min, max and mean are over those covered texels, from 0 to 1; for base_colour each is [r, g, b]. "
        + "Errors, all listed at once, each a tool error that writes nothing: an unknown mesh (the error lists the meshes); a mesh with no UVs; faces that share a texel (the layout overlaps; the error names the faces); a size that is not a power of two from 64 to 4096; padding outside 0 to size/2; an unknown map (the error lists the maps) or an empty maps; a curvature map with no curvature width, or a width that is not above 0; occlusion or orm with no occlusion.reach, a reach not over 0, rays outside 1 to 1024, an unknown or empty occluders (the error lists the meshes); 'normal' without bevel, or a bevel width that is not a number more than 0; a surface entry naming a group or material the mesh does not have (the error lists the names there are), a bad colour, or a roughness or metallic outside 0 to 1; "
        + "a mesh, group or material name that a file name cannot hold. "
        + "Worked example, from bake to a material on the faces, on the wrench (open samples/wrench.carve): bake with the arguments below writes textures/wrench.base_colour.png, textures/wrench.orm.png and a mask for each group; then define_material {name: 'wrench', base_colour_texture: 'textures/wrench.base_colour.png', metallic_roughness_texture: 'textures/wrench.orm.png', occlusion_texture: 'textures/wrench.orm.png'} (one ORM file in both slots) and set_material {material: 'wrench', group: 'all'} (the wrench's group of every face) show the baked colours on the model:\n"
        + ExampleMarker + Example;

    private static readonly string[] Known = ["mesh", "folder", "size", "padding", "maps", "surface", "curvature", "occlusion", "bevel"];

    private static readonly string[] OcclusionKnown = ["rays", "reach", "occluders"];

    private static readonly string[] SurfaceKnown = ["base_colour", "roughness", "metallic"];

    public static McpServerTool Create() => MeshToolSupport.Create<BakeTool>("bake", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["mesh"] = new JsonObject { ["description"] = "The mesh to bake; required when the model has several meshes.", ["type"] = "string" },
            ["folder"] = new JsonObject { ["description"] = "The folder the PNGs are written to, relative to carve's working folder or absolute; created if missing. Default 'textures'.", ["type"] = "string" },
            ["size"] = new JsonObject { ["description"] = "The image's side in texels: a power of two from 64 to 4096. Default 1024.", ["type"] = "integer", ["minimum"] = 64, ["maximum"] = 4096 },
            ["padding"] = new JsonObject { ["description"] = "Texels of colour spread past each island's edge, 0 to size/2. Default 8.", ["type"] = "integer", ["minimum"] = 0 },
            ["maps"] = new JsonObject
            {
                ["description"] = "The maps to bake, at least one of: masks (one grey PNG per group and per material), base_colour (an RGB PNG), curvature (a grey PNG, needs the curvature block), occlusion (a grey PNG), orm (an RGB PNG: occlusion, roughness, metallic), normal (an RGB tangent-space normal map rounding the hard edges; needs bevel). Default masks and base_colour.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(Maps.Select(m => (JsonNode)m).ToArray()) },
                ["minItems"] = 1,
            },
            ["curvature"] = new JsonObject
            {
                ["description"] = "For the curvature map: {width}, in metres, greater than 0, over which an edge shows; required when maps has curvature.",
                ["type"] = "object",
                ["properties"] = new JsonObject { ["width"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 } },
                ["additionalProperties"] = false,
            },
            ["occlusion"] = new JsonObject
            {
                ["description"] = "For the occlusion and orm maps: {rays: 1 to 1024, default 32; reach: metres, required for those maps; occluders: mesh names, default every mesh}.",
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["rays"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = Bake.MaxRays },
                    ["reach"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["occluders"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["minItems"] = 1 },
                },
                ["additionalProperties"] = false,
            },
            ["surface"] = new JsonObject
            {
                ["description"] = "Per group or material name, applied in the order given (the later entry wins a face): {base_colour: '#RRGGBB', roughness: 0 to 1, metallic: 0 to 1}, each optional.",
                ["type"] = "object",
                ["additionalProperties"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["base_colour"] = new JsonObject { ["type"] = "string" },
                        ["roughness"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["metallic"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                    },
                    ["additionalProperties"] = false,
                },
            },
            ["bevel"] = new JsonObject
            {
                ["description"] = "For the normal map: {width}, how far from each hard edge the rounding reaches, in metres, more than 0. Required with 'normal'; no default.",
                ["type"] = "object",
                ["properties"] = new JsonObject { ["width"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 } },
                ["required"] = new JsonArray("width"),
                ["additionalProperties"] = false,
            },
        },
    });

    /// <summary>One surface entry: the name it is keyed by and the fields it gave.</summary>
    private sealed record SurfaceEntry(string Name, string? BaseColour, float? Roughness, float? Metallic);

    private static string Quoted(IEnumerable<string> names)
    {
        var list = names.Select(n => $"'{n}'").ToList();
        return list.Count == 0 ? "none" : string.Join(", ", list);
    }

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? mesh = null, JsonElement? folder = null,
        JsonElement? size = null, JsonElement? padding = null, JsonElement? maps = null, JsonElement? surface = null, JsonElement? curvature = null,
        JsonElement? occlusion = null, JsonElement? bevel = null)
    {
        var errors = UnknownArguments("bake", context, Known);
        var meshName = MeshName(mesh, errors);
        var folderText = OptionalString("folder", folder, errors) ?? DefaultFolder;
        if (!Absent(folder) && folderText.Trim().Length == 0) errors.Add("folder: is empty; give a folder, or leave it out for 'textures'");
        var side = Whole("size", size, DefaultSize, errors);
        var pad = Whole("padding", padding, DefaultPadding, errors);
        if (side is { } s && BakeLayout.SizeProblem(s) is { } sizeProblem) { errors.Add(sizeProblem); side = null; }
        if (pad is { } p && (p < 0 || (side is { } sz && p > sz / 2)))
        {
            errors.Add(side is { } sz2 ? $"padding: must be from 0 to {sz2 / 2} (half the size), not {p}" : $"padding: must be 0 or more, and at most half the size, not {p}");
            pad = null;
        }

        var wanted = ReadMaps(maps, errors);
        var warnings = new List<string>();
        UnusedBlock("occlusion", occlusion, [OcclusionMap, OrmMap], wanted, warnings);
        UnusedBlock("bevel", bevel, [NormalMap], wanted, warnings);
        UnusedBlock("curvature", curvature, [CurvatureMap], wanted, warnings);
        var bevelWidth = ReadBevel(bevel, wanted.Contains(NormalMap), errors);

        var model = store.Current.Model;
        var named = model.Target(meshName, out var meshProblem);
        if (meshProblem is not null) errors.Add(meshProblem);
        else if (named is null) errors.Add(NoMesh);

        var entries = ReadSurface(surface, errors);
        var curvatureWidth = ReadCurvature(curvature, wanted.Contains(CurvatureMap), errors);
        var cast = ReadOcclusion(occlusion, wanted, model, errors);

        // Names a file name cannot hold, and the surface's names against the mesh's.
        if (named is not null)
        {
            var style = named.Mesh.Style ?? FaceStyle.None;
            var groups = style.Groups.Select(g => g.Name).ToList();
            var materials = Model.MaterialsUsedBy(named.Mesh).ToList();
            var known = groups.Concat(model.Materials.Select(m => m.Name)).Concat(materials).Distinct().ToList();
            foreach (var entry in entries.Where(e => !known.Contains(e.Name)))
                errors.Add($"surface: '{entry.Name}' is not a group or a material; the names are {Quoted(known)}");
            if (FileNameProblem(named.Name) is { } meshBad) errors.Add($"mesh: {meshBad}");
            if (wanted.Contains(MasksMap))
                foreach (var name in groups.Concat(materials).Distinct())
                    if (FileNameProblem(name) is { } bad) errors.Add($"masks: the name {bad}, so its mask has no file name");
        }

        if (errors.Count > 0 && (named is null || side is null)) return Failed("bake", errors);

        // The layout is worked even when an argument is wrong, so one call gives every problem, the overlap included.
        var result = BakeLayout.Create(named!.Mesh, side!.Value);
        if (result.Error is not null) errors.Add(result.Error);
        if (errors.Count > 0) return Failed("bake", errors);
        var layout = result.Layout!;
        var mapped = named.Mesh;

        // Each job works its image when it is written, so its time is its own; work two files share (the masks, worked together,
        // and the occlusion the ORM reads) is counted in the first file that needs it.
        var jobs = new List<(string Map, string? Name, string File, Func<BakeImage> Image, bool Colour)>();
        long masksMs = 0;
        if (wanted.Contains(MasksMap))
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var masks = Bake.Masks(layout, mapped);
            masksMs = clock.ElapsedMilliseconds;
            foreach (var mask in masks)
            {
                var infix = mask.Source == MaskSource.Group ? "mask" : "material";
                jobs.Add((MasksMap, mask.Name, $"{named.Name}.{infix}.{mask.Name}.png", () => mask.Image, false));
            }
        }
        if (wanted.Contains(BaseColourMap)) jobs.Add((BaseColourMap, null, $"{named.Name}.{BaseColourMap}.png", () => BaseColour(layout, model, mapped, entries), true));
        if (wanted.Contains(CurvatureMap) && curvatureWidth is { } width)
            jobs.Add((CurvatureMap, null, $"{named.Name}.{CurvatureMap}.png", () => Bake.Curvature(layout, width), false));
        OcclusionBake? occluded = null;
        BakeImage Occluded() => (occluded ??= Bake.Occlusion(layout, new RayCaster(cast!.Meshes.Select(m => m.Mesh).ToList()),
            cast.Meshes.FindIndex(m => m.Name == named.Name), cast.Rays, cast.Reach)).Image;
        if (wanted.Contains(OcclusionMap)) jobs.Add((OcclusionMap, null, $"{named.Name}.{OcclusionMap}.png", Occluded, false));
        if (wanted.Contains(OrmMap))
            jobs.Add((OrmMap, null, $"{named.Name}.{OrmMap}.png", () => { var (r, m) = Factors(model, mapped, entries); return Bake.Orm(layout, Occluded(), r, m); }, true));
        if (wanted.Contains(NormalMap)) jobs.Add((NormalMap, null, $"{named.Name}.{NormalMap}.png", () => Bake.BevelNormal(layout, bevelWidth!.Value), true));

        var folderPath = folderText.Replace('\\', '/').TrimEnd('/');
        var targets = new List<ToolPath>();
        foreach (var job in jobs)
        {
            var target = ToolPaths.Resolve("bake", $"{folderPath}/{job.File}", ".png", out var pathError, toolFolder.Directory);
            if (target is null) errors.Add(pathError!);
            else targets.Add(target);
        }
        foreach (var clash in targets.GroupBy(t => t.FullPath, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"two maps would write the file {clash.Key}; rename a group or material so their names differ by more than case");
        if (errors.Count > 0) return Failed("bake", errors);

        var baseDirectory = toolFolder.Directory ?? Environment.CurrentDirectory;
        var files = new List<Dictionary<string, object?>>();
        for (var i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var (stats, png) = Encode(job.Image(), layout, pad!.Value, job.Colour);
            if (!ToolPaths.Write("bake", targets[i], png, out var replaced, out var writeError, overwrite: true))
                return Error(writeError!);
            var entry = new Dictionary<string, object?> { ["map"] = job.Map };
            if (job.Name is not null) entry["name"] = job.Name;
            entry["path"] = ReportedPath(baseDirectory, targets[i].FullPath);
            entry["size"] = layout.Size;
            entry["replaced"] = replaced;
            entry["coverage"] = stats.Coverage;
            entry["min"] = stats.Min;
            entry["max"] = stats.Max;
            entry["mean"] = stats.Mean;
            entry["ms"] = clock.ElapsedMilliseconds + (i == 0 && job.Map == MasksMap ? masksMs : 0);
            files.Add(entry);
        }

        if (layout.Unmapped.Count > 0)
            warnings.Add($"{named.Name}: {layout.Unmapped.Count} face(s) have no UVs and were skipped: {string.Join(", ", layout.Unmapped.Take(10))}{(layout.Unmapped.Count > 10 ? ", ..." : "")}");
        return Ok(new { files, skipped_faces = layout.Unmapped, warnings });
    }

    /// <summary>A whole number argument, or its default; null with the problem added to <paramref name="errors"/>.</summary>
    private static int? Whole(string name, JsonElement? value, int fallback, List<string> errors)
    {
        if (Absent(value)) return fallback;
        if (value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var n)) return n;
        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var d) && d == Math.Floor(d) && Math.Abs(d) < int.MaxValue) return (int)d;
        errors.Add($"{name}: expected a whole number, got {Describe(value.Value)}");
        return null;
    }

    private static List<string> ReadMaps(JsonElement? maps, List<string> errors)
    {
        if (Absent(maps)) return [.. DefaultMaps];
        if (maps!.Value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"maps: expected an array of map names, got {Describe(maps.Value)}");
            return [];
        }
        var chosen = new List<string>();
        var index = 0;
        foreach (var item in maps.Value.EnumerateArray())
        {
            var at = $"maps[{index++}]";
            if (item.ValueKind != JsonValueKind.String) errors.Add($"{at}: expected a map name, got {Describe(item)}");
            else if (!Maps.Contains(item.GetString())) errors.Add($"{at}: '{item.GetString()}' is not a map; the maps are {Quoted(Maps)}");
            else if (!chosen.Contains(item.GetString()!)) chosen.Add(item.GetString()!);
        }
        if (index == 0) errors.Add($"maps: is empty; name at least one of {Quoted(Maps)}");
        return chosen;
    }

    /// <summary>The curvature block's width in metres, or null (with the problems added) when it is missing and needed, or wrong.</summary>
    private static double? ReadCurvature(JsonElement? block, bool needed, List<string> errors)
    {
        if (Absent(block))
        {
            if (needed) errors.Add("curvature: the curvature map needs a curvature block {width}, in metres, greater than 0");
            return null;
        }
        if (block!.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"curvature: expected {{width}}, got {Describe(block.Value)}");
            return null;
        }
        double? width = null;
        var bad = false;
        foreach (var field in block.Value.EnumerateObject())
        {
            if (field.Name != "width") { errors.Add($"curvature: '{field.Name}' is not a curvature field; the field is 'width'"); bad = true; continue; }
            if (field.Value.ValueKind != JsonValueKind.Number || !field.Value.TryGetDouble(out var w) || !double.IsFinite(w)) { errors.Add($"curvature.width: expected a number of metres, got {Describe(field.Value)}"); bad = true; }
            else if (!(w > 0)) { errors.Add($"curvature.width: must be greater than 0, not {w.ToString(CultureInfo.InvariantCulture)}"); bad = true; }
            else width = w;
        }
        if (width is null && !bad) errors.Add("curvature.width: required but missing");
        return bad ? null : width;
    }

    /// <summary>bevel's width when <c>normal</c> is asked for; null with the problem added when it is wrong, missing, or given without <c>normal</c>.</summary>
    private static double? ReadBevel(JsonElement? bevel, bool normal, List<string> errors)
    {
        if (Absent(bevel))
        {
            if (normal) errors.Add("bevel: required with map 'normal': give {width: metres, more than 0}, how far from each hard edge the rounding reaches (no default)");
            return null;
        }
        if (bevel!.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"bevel: expected {{width}}, got {Describe(bevel.Value)}");
            return null;
        }
        double? width = null;
        var given = false;
        foreach (var field in bevel.Value.EnumerateObject())
        {
            if (field.Name != "width") { errors.Add($"bevel.{field.Name}: not a field of bevel; its one field is width"); continue; }
            given = true;
            if (field.Value.ValueKind != JsonValueKind.Number || !field.Value.TryGetDouble(out var w) || !double.IsFinite(w)) errors.Add($"bevel.width: expected a number of metres, got {Describe(field.Value)}");
            else if (!(w > 0)) errors.Add($"bevel.width: must be more than 0 metres, not {w.ToString(CultureInfo.InvariantCulture)}");
            else width = w;
        }
        if (!given) errors.Add("bevel.width: required: how far from each hard edge the rounding reaches, in metres, more than 0");
        return normal ? width : null;
    }

    private static List<SurfaceEntry> ReadSurface(JsonElement? surface, List<string> errors)
    {
        var entries = new List<SurfaceEntry>();
        if (Absent(surface)) return entries;
        if (surface!.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"surface: expected an object keyed by group or material name, got {Describe(surface.Value)}");
            return entries;
        }
        foreach (var property in surface.Value.EnumerateObject())
        {
            var at = $"surface['{property.Name}']";
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{at}: expected {{base_colour, roughness, metallic}}, got {Describe(property.Value)}");
                continue;
            }
            string? colour = null;
            float? roughness = null, metallic = null;
            foreach (var field in property.Value.EnumerateObject())
            {
                switch (field.Name)
                {
                    case "base_colour":
                        if (field.Value.ValueKind != JsonValueKind.String) errors.Add($"{at}.base_colour: expected a colour string, got {Describe(field.Value)}");
                        else if (FaceStyle.ColourProblem(field.Value.GetString()) is { } problem) errors.Add($"{at}.base_colour: {problem}");
                        else colour = field.Value.GetString();
                        break;
                    case "roughness":
                    case "metallic":
                        var factor = Number($"{at}.{field.Name}", field.Value, errors);
                        if (factor is { } f && (f < 0 || f > 1)) errors.Add($"{at}.{field.Name}: must be from 0 to 1, not {f.ToString(CultureInfo.InvariantCulture)}");
                        else if (field.Name == "roughness") roughness = factor;
                        else metallic = factor;
                        break;
                    default:
                        errors.Add($"{at}: '{field.Name}' is not a surface field; the fields are {Quoted(SurfaceKnown)}");
                        break;
                }
            }
            entries.Add(new SurfaceEntry(property.Name, colour, roughness, metallic));
        }
        return entries;
    }

    /// <summary>The <c>occlusion</c> block, read: the rays a texel casts, the reach in metres and the meshes that occlude.</summary>
    private sealed record OcclusionArgs(int Rays, float Reach, List<NamedMesh> Meshes);

    /// <summary>
    /// A block given when no map in this call reads it: the call bakes what was asked, and a warning says the block was ignored.
    /// The block is still read, so a wrong one is still an error. For bevel and curvature this is the person's ruling (2026-11-20).
    /// </summary>
    private static void UnusedBlock(string block, JsonElement? value, string[] readers, List<string> wanted, List<string> warnings)
    {
        if (Absent(value) || readers.Any(wanted.Contains)) return;
        var named = readers.Length == 1 ? $"'{readers[0]}' is not" : "neither " + string.Join(" nor ", readers.Select(r => $"'{r}'")) + " is";
        warnings.Add($"{block}: ignored, {named} in maps");
    }

    /// <summary>
    /// The <c>occlusion</c> block, validated whether or not a map reads it; null when it is absent or wrong. <c>reach</c> is
    /// required when <c>occlusion</c> or <c>orm</c> is asked for.
    /// </summary>
    private static OcclusionArgs? ReadOcclusion(JsonElement? block, List<string> wanted, Model model, List<string> errors)
    {
        var needed = wanted.Contains(OcclusionMap) || wanted.Contains(OrmMap);
        const string NeedReach = "occlusion.reach: required for the occlusion and orm maps: the distance in metres within which geometry occludes (it has no default; give the scale of the creases to darken)";
        if (Absent(block))
        {
            if (needed) errors.Add(NeedReach);
            return null;
        }
        if (block!.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"occlusion: expected {{rays, reach, occluders}}, got {Describe(block.Value)}");
            return null;
        }

        var ok = true;
        int rays = Bake.DefaultRays;
        float? reach = null;
        List<NamedMesh> meshes = [.. model.Meshes];
        foreach (var field in block.Value.EnumerateObject())
        {
            switch (field.Name)
            {
                case "rays":
                    if (field.Value.ValueKind == JsonValueKind.Number && field.Value.TryGetDouble(out var r) && r == Math.Floor(r) && r >= 1 && r <= Bake.MaxRays) rays = (int)r;
                    else { errors.Add($"occlusion.rays: must be a whole number from 1 to {Bake.MaxRays}, got {Describe(field.Value)}"); ok = false; }
                    break;
                case "reach":
                    var d = Number("occlusion.reach", field.Value, errors);
                    if (d is not { } v) ok = false;
                    else if (!(v > 0) || !float.IsFinite(v)) { errors.Add($"occlusion.reach: must be over 0 metres, not {v.ToString(CultureInfo.InvariantCulture)}"); ok = false; }
                    else reach = v;
                    break;
                case "occluders":
                    if (field.Value.ValueKind != JsonValueKind.Array) { errors.Add($"occlusion.occluders: expected an array of mesh names, got {Describe(field.Value)}"); ok = false; break; }
                    meshes = [];
                    var index = 0;
                    foreach (var item in field.Value.EnumerateArray())
                    {
                        var at = $"occlusion.occluders[{index++}]";
                        var found = item.ValueKind == JsonValueKind.String ? model.Meshes.FirstOrDefault(m => m.Name == item.GetString()) : null;
                        if (item.ValueKind != JsonValueKind.String) { errors.Add($"{at}: expected a mesh name, got {Describe(item)}"); ok = false; }
                        else if (found is null) { errors.Add($"{at}: '{item.GetString()}' is not a mesh; the meshes are {Quoted(model.Meshes.Select(m => m.Name))}"); ok = false; }
                        else if (!meshes.Contains(found)) meshes.Add(found);
                    }
                    if (index == 0) { errors.Add("occlusion.occluders: is empty; name at least one mesh, or leave it out for every mesh of the model"); ok = false; }
                    break;
                default:
                    errors.Add($"occlusion: '{field.Name}' is not an occlusion field; the fields are {Quoted(OcclusionKnown)}");
                    ok = false;
                    break;
            }
        }
        if (reach is null && needed && !block.Value.TryGetProperty("reach", out _)) errors.Add(NeedReach);
        return ok && reach is { } got ? new OcclusionArgs(rays, got, meshes) : null;
    }

    /// <summary>
    /// Each face's roughness and metallic for the ORM, by base_colour's resolution less the face's own colour: the last surface
    /// entry that names a group the face is in or its material and gives the factor; else its material's; else
    /// <see cref="Material"/>'s defaults (roughness 1, metallic 0).
    /// </summary>
    private static (float[] Roughness, float[] Metallic) Factors(Model model, PolygonMesh mesh, IReadOnlyList<SurfaceEntry> entries)
    {
        var style = mesh.Style ?? FaceStyle.None;
        var roughness = new float?[mesh.Faces.Count];
        var metallic = new float?[mesh.Faces.Count];
        foreach (var entry in entries)
        {
            foreach (var f in FacesNamed(mesh, style, entry.Name))
            {
                if (entry.Roughness is { } r) roughness[f] = r;
                if (entry.Metallic is { } m) metallic[f] = m;
            }
        }
        var outR = new float[mesh.Faces.Count];
        var outM = new float[mesh.Faces.Count];
        for (var f = 0; f < outR.Length; f++)
        {
            var material = style.MaterialOf(f) is { } name ? model.FindMaterial(name) : null;
            outR[f] = roughness[f] ?? material?.Roughness ?? Material.DefaultRoughness;
            outM[f] = metallic[f] ?? material?.Metallic ?? Material.DefaultMetallic;
        }
        return (outR, outM);
    }

    /// <summary>The faces a surface entry's name covers: the group's members on the mesh, then the live faces with that material.</summary>
    private static IEnumerable<int> FacesNamed(PolygonMesh mesh, FaceStyle style, string name) =>
        (style.FacesOf(name) ?? []).Where(f => (uint)f < (uint)mesh.Faces.Count).Concat(mesh.LiveFaces.Where(f => style.MaterialOf(f) == name));

    /// <summary>Why <paramref name="name"/> cannot be in a file name, naming it, or null.</summary>
    private static string? FileNameProblem(string name)
    {
        var bad = name.FirstOrDefault(c => System.Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c is '/' or '\\');
        if (bad != default) return $"'{name}' holds {(char.IsControl(bad) ? $"U+{(int)bad:X4}" : $"'{bad}'")}, which a file name may not";
        if (name.EndsWith('.') || name.EndsWith(' ')) return $"'{name}' ends in a '{name[^1]}', which a file name may not";
        return null;
    }

    private static string ReportedPath(string baseDirectory, string full)
    {
        var relative = Path.GetRelativePath(baseDirectory, full);
        return (Path.IsPathRooted(relative) ? full : relative).Replace('\\', '/');
    }

    /// <summary>The base colour image, unpadded: each covered texel the sRGB bytes (as 0 to 1) of its face's colour.</summary>
    private static BakeImage BaseColour(BakeLayout layout, Model model, PolygonMesh mesh, IReadOnlyList<SurfaceEntry> entries)
    {
        var style = mesh.Style ?? FaceStyle.None;
        var colours = new string?[mesh.Faces.Count];
        foreach (var entry in entries)
        {
            if (entry.BaseColour is null) continue;
            foreach (var f in FacesNamed(mesh, style, entry.Name)) colours[f] = entry.BaseColour;
        }

        var image = layout.NewImage(3);
        var rgb = new Dictionary<string, (float R, float G, float B)>(StringComparer.OrdinalIgnoreCase);
        for (var y = 0; y < layout.Size; y++)
        for (var x = 0; x < layout.Size; x++)
        {
            var f = layout.FaceAt(x, y);
            if (f < 0) continue;
            var hex = colours[f] ?? style.ColourOf(f) ?? (style.MaterialOf(f) is { } m ? model.FindMaterial(m)?.BaseColour : null) ?? Material.DefaultBaseColour;
            if (!rgb.TryGetValue(hex, out var c))
            {
                var (r, g, b) = FaceStyle.ToBytes(hex);
                rgb[hex] = c = (r / 255f, g / 255f, b / 255f);
            }
            image[x, y, 0] = c.R;
            image[x, y, 1] = c.G;
            image[x, y, 2] = c.B;
        }
        return image;
    }

    private sealed record Stats(double Coverage, object Min, object Max, object Mean);

    /// <summary>The statistics of the unpadded image over the covered texels, then the padded image as PNG bytes.</summary>
    private static (Stats Stats, byte[] Png) Encode(BakeImage image, BakeLayout layout, int padding, bool colour)
    {
        var channels = image.Channels;
        var min = new double[channels];
        var max = new double[channels];
        var sum = new double[channels];
        System.Array.Fill(min, 1.0);
        long covered = 0;
        for (var y = 0; y < layout.Size; y++)
        for (var x = 0; x < layout.Size; x++)
        {
            if (!layout.IsCovered(x, y)) continue;
            covered++;
            for (var c = 0; c < channels; c++)
            {
                var v = (double)Quantised(image[x, y, c]) / 255;
                min[c] = Math.Min(min[c], v);
                max[c] = Math.Max(max[c], v);
                sum[c] += v;
            }
        }
        if (covered == 0) System.Array.Clear(min);

        var padded = Bake.Pad(image, layout, padding);
        var bytes = new byte[layout.Size * layout.Size * channels];
        var values = padded.Values;
        for (var i = 0; i < bytes.Length; i++) bytes[i] = Quantised(values[i]);
        using var stream = new MemoryStream();
        new ImageWriter().WritePng(bytes, layout.Size, layout.Size, colour ? ColorComponents.RedGreenBlue : ColorComponents.Grey, stream);

        static object Shape(double[] v, bool colour) => colour ? v.Select(Round).ToArray() : Round(v[0]);
        var mean = sum.Select(s => covered == 0 ? 0 : s / covered).ToArray();
        return (new Stats((double)covered / ((double)layout.Size * layout.Size), Shape(min, colour), Shape(max, colour), Shape(mean, colour)), stream.ToArray());
    }

    private static double Round(double v) => Math.Round(v, 6);

    private static byte Quantised(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f, MidpointRounding.AwayFromZero), 0, 255);
}
