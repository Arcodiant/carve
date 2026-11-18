using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Carve.Core;

/// <summary>
/// The outcome of <see cref="CarveFile.Read"/>: a model and the warnings its meshes carry, or the errors that stopped one.
/// On failure <see cref="Model"/> is null and <see cref="Warnings"/> is empty.
/// </summary>
public sealed class CarveFileResult
{
    internal CarveFileResult(Model? model, IReadOnlyList<MeshError> errors, IReadOnlyList<MeshWarning> warnings, int version = 0)
    {
        Model = model;
        Version = model is null ? 0 : version;
        Errors = errors;
        Warnings = warnings;
    }

    /// <summary>The model the file holds, every mesh, vertex, face, bone and clip in its place, or null when the file was refused.</summary>
    public Model? Model { get; }

    /// <summary>The version the file was read as, 1 to 8, or 0 when the file was refused.</summary>
    public int Version { get; }

    /// <summary>The model's only mesh, or null when the file was refused or holds several.</summary>
    public PolygonMesh? Mesh => Model is { Meshes.Count: 1 } m ? m.Meshes[0].Mesh : null;

    /// <summary>Every problem found, each named by where it is in the file: <c>file</c>, <c>carve</c>, <c>meshes[0].faces[2][1]</c>, <c>clips[1].duration</c>, or an unknown key.</summary>
    public IReadOnlyList<MeshError> Errors { get; }

    /// <summary>The meshes' warnings, mesh by mesh in file order, exactly as <see cref="MeshReport"/> gives them for the same mesh made by <c>define_mesh</c>.</summary>
    public IReadOnlyList<MeshWarning> Warnings { get; }

    public bool Succeeded => Model is not null;
}

/// <summary>
/// The Carve file: a <see cref="Model"/> as versioned JSON text (its meshes with their colours, groups and weights, the
/// skeleton and the clips), read back bit-exact with every vertex, face and bone at its id. Undo history is not saved.
/// </summary>
/// <remarks>
/// <para><see cref="Write(Model, string)"/> gives version 8 (T-101) for a model with a morph target on some mesh, else version 6 (T-74) for a model with a material, else version 5 (T-53) for a model with UVs or seams on some mesh, else version 4 (T-63) for a model with a holed face or a tombstone, and otherwise what earlier builds gave
/// (version 3 with a smooth mesh or a hard edge, else version 2, byte for byte): the lowest version that holds the model,
/// as the person ruled (2026-11-11). Version 4 is version 3 with faces that may have holes: a face is
/// either a plain array of vertex ids, its outer loop, or the object <c>{"loop": [...], "holes": [[...], ...]}</c>, as
/// <c>define_mesh</c> takes it; a face with no hole is always written as the plain array, a holed one as the object on one
/// line. Version 4 also holds tombstones (T-64): a <c>null</c> in <c>vertices</c> or <c>faces</c> standing
/// for a deleted element whose id is not reused, read back to the same tombstone; a live face using a null vertex is refused at its corner.
/// Version 5 (T-53) is version 4 with, after <c>hard_edges</c> in a mesh, <c>uvs</c>
/// (<c>[{"face": f, "uv": [[u, v], ...]}, ...]</c>, one per mapped face in face-id order, one UV per <see cref="Face.Corners"/>
/// entry: the outer loop, then each hole) and <c>seams</c> (vertex-id pairs as <c>hard_edges</c>), each left out when empty;
/// UVs are written and read bit-exact, as vertices are, and follow glTF ((0, 0) top-left, v down).
/// Version 6 (T-74) is version 5 with, after <c>carve</c>, <c>materials</c>: one object per line in definition order, its
/// <c>name</c> and then only the fields that differ from their defaults, in record order (<c>base_colour</c>, <c>metallic</c>,
/// <c>roughness</c>, <c>emissive</c>, <c>occlusion</c>, <c>double_sided</c>, then the five <c>*_texture</c> keys, each a path
/// relative to the file's folder with <c>/</c> separators); and, after <c>colours</c> in a mesh, <c>materials</c>
/// (<c>[{"material": name, "faces": [...]}, ...]</c>, grouped by name and ordered by first face), left out when no face has one.
/// It is written only when the model has a material, so every other model is written as before, byte for byte.
/// Version 8 (T-101) is version 6 with, after the last key of a mesh (<c>seams</c> when there are seams), <c>targets</c>: one
/// object per line in definition order, <c>{"name": ..., "deltas": {"&lt;id&gt;": [dx, dy, dz], ...}}</c>, ids ascending, each
/// delta written as positions are and read back bit-exact; a delta of (0, 0, 0) is never written. It is written only when some
/// mesh has a target, so every other model is written as before, byte for byte. Version 7 (T-92) is version 6 with, after <c>meshes</c>,
/// <c>parts</c>: every part's record (<see cref="Model.Parts"/>, pre-order), one per line, <c>{"name": ..., "parent": name or null,
/// "translation": [x, y, z], "rotation": [x, y, z, w], "scale": [x, y, z]}</c> then <c>envelope</c>
/// (<c>{"min": [...], "max": [...]}</c>), <c>instance_of</c> and <c>"moves": true</c>, each left out when absent; and, after the last
/// key of a mesh, <c>levels</c>: <c>[{"level": N, "vertices": ..., ...}, ...]</c>, each kept level a whole mesh with the keys
/// of a version 6 mesh but its name, ascending, left out when none is kept. An empty or an instance has no mesh. It is
/// written only when some part has a parent, a transform, an envelope, <c>instance_of</c>, <c>moves</c> or a kept level, so
/// every other model is written as before, byte for byte; a model with a part tree and a morph target is refused
/// (<see cref="VersionProblem"/>).
/// Version 3 (T-42) added, after <c>weights</c> in every mesh, <c>shading</c> (<c>"flat"</c> or <c>"smooth"</c>) and
/// <c>hard_edges</c> (vertex-id pairs, lower id first, in order).
/// Version 2: the keys <c>carve</c>, <c>meshes</c>, <c>skeleton</c>
/// and <c>clips</c> in that order; in a mesh <c>name</c>, <c>vertices</c>, <c>faces</c>, <c>colours</c>, <c>groups</c> and
/// <c>weights</c>; two-space indent, one vertex, face, colour, group, weighted vertex, bone or clip key per line, <c>\n</c>
/// line ends and a newline at the end. Each float is the shortest text that reads back to the same <see cref="float"/>,
/// each weight the shortest that reads back to the same <see cref="double"/>.</para>
/// <para>Reading takes version 1 (one mesh, named <see cref="Model.DefaultName"/>) and versions 2 to 8, keys in any order and
/// any whitespace, and holds the file to exactly what the tools accept: geometry to <see cref="PolygonMesh.Create"/>, bones
/// to <see cref="Skeleton.Create"/>, weights to <see cref="VertexWeights.Raw"/>, clips to <see cref="Clip.Create"/>, the
/// whole to <see cref="Model.Create"/>. An unknown key or another version is refused, so a newer file is never half-read.
/// <c>"meshes": []</c> is read only beside a skeleton: a clip library, a skeleton and clips with no mesh (the person,
/// 2026-11-09); with no skeleton it is refused. A version 6 texture path is resolved against the folder given to
/// <see cref="Read(string, string)"/> and the PNG read then, every material rule checked, and every face's material must be
/// defined.</para>
/// </remarks>
public static class CarveFile
{
    /// <summary>The version <see cref="Write"/> writes.</summary>
    public const int Version = 8;

    /// <summary>The versions <see cref="Read(string)"/> reads.</summary>
    public static IReadOnlyList<int> ReadableVersions { get; } = [1, 2, 3, 4, 5, 6, 7, 8];

    /// <summary>The version that holds a part tree and kept levels (M-9, T-92).</summary>
    public const int PartTreeVersion = 7;

    private const string VersionKey = "carve";
    private const string VerticesKey = "vertices";
    private const string FacesKey = "faces";

    private static readonly string[] FileKeys = [VersionKey, "meshes", "skeleton", "clips"];
    private static readonly string[] MeshKeys = ["name", VerticesKey, FacesKey, "colours", "groups", "weights"];
    private static readonly string[] MeshKeysV3 = [.. MeshKeys, "shading", "hard_edges"];
    private static readonly string[] MeshKeysV5 = [.. MeshKeysV3, "uvs", "seams"];
    private const string MaterialsKey = "materials";
    private static readonly string[] FileKeysV6 = [VersionKey, MaterialsKey, "meshes", "skeleton", "clips"];
    private static readonly string[] MeshKeysV6 = ["name", VerticesKey, FacesKey, "colours", MaterialsKey, "groups", "weights", "shading", "hard_edges", "uvs", "seams"];
    private const string TargetsKey = "targets";
    private static readonly string[] MeshKeysV8 = [.. MeshKeysV6, TargetsKey];
    private const string PartsKey = "parts";
    private const string LevelsKey = "levels";
    // Version 7 (T-92): version 6 with "parts" after "meshes", and "levels" after the last key of a mesh.
    private static readonly string[] FileKeysV7 = [VersionKey, MaterialsKey, "meshes", PartsKey, "skeleton", "clips"];
    private static readonly string[] MeshKeysV7 = [.. MeshKeysV6, LevelsKey];
    private static readonly string[] LevelKeys = ["level", .. MeshKeysV6.Skip(1)];
    private static readonly string[] PartRecordKeys = ["name", "parent", "translation", "rotation", "scale", "envelope", "instance_of", "moves"];
    private static readonly string[] EnvelopeKeys = ["min", "max"];
    private static readonly string[] TargetKeys = ["name", "deltas"];
    private static readonly string[] MaterialUseKeys = ["material", "faces"];
    private static readonly string[] MaterialFactorKeys = ["base_colour", "metallic", "roughness", "emissive", "occlusion", "double_sided"];
    private static readonly string[] MaterialKeys = ["name", .. MaterialFactorKeys, .. new Material("m").Textures.Select(t => t.Field)];
    private static readonly string[] UvKeys = ["face", "uv"];
    private static readonly string[] ColourKeys = ["colour", "faces"];
    private static readonly string[] GroupKeys = ["name", "faces"];
    private static readonly string[] WeightKeys = ["vertex", "bones"];
    private static readonly string[] BoneKeys = ["name", "parent", "position", "rotation"];
    private static readonly string[] ClipKeys = ["name", "duration", "loop", "channels"];
    private static readonly string[] PartKeys = ["rotation", "translation"];
    private static readonly string[] ChannelKeys = ["interpolation", "keys"];
    private static readonly string[] KeyKeys = ["time", "value"];

    private static readonly JsonSerializerOptions StringOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // ---- Writing ---------------------------------------------------------------------------------------------------

    /// <summary>The file's text for a model of one mesh, named <see cref="Model.DefaultName"/>.</summary>
    public static string Write(PolygonMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return Write(Model.Of(Model.DefaultName, mesh));
    }

    /// <summary>The file's text for a model, version 2 to 8 as the remarks say. The same model always gives the same text.</summary>
    /// <exception cref="ArgumentException">A material has a texture, whose path needs the file's folder: use <see cref="Write(Model, string)"/>.</exception>
    public static string Write(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Materials.Any(m => m.HasTexture))
            throw new ArgumentException("a material has a texture, and a texture's path is written relative to the file's folder; pass the folder", nameof(model));
        return WriteText(model, null);
    }

    /// <summary>
    /// The file's text for a model saved in <paramref name="folder"/>: each texture's path is written relative to it, with
    /// <c>/</c> separators. Otherwise as <see cref="Write(Model)"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A texture has no path relative to the folder (<see cref="TextureProblem"/>).</exception>
    public static string Write(Model model, string folder)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        if (TextureProblem(model, folder) is { } problem) throw new ArgumentException(problem, nameof(model));
        return WriteText(model, folder);
    }

    /// <summary>
    /// Why <paramref name="model"/> cannot be saved in <paramref name="folder"/>, or null when it can: a texture with no path
    /// relative to the folder (on another drive) refuses the save, naming the texture (the person's ruling, 2026-11-16).
    /// </summary>
    public static string? TextureProblem(Model model, string folder)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        for (var i = 0; i < model.Materials.Count; i++)
            foreach (var (field, texture) in model.Materials[i].Textures)
                if (texture is not null && RelativePath(folder, texture.Path) is null)
                    return Invariant($"materials[{i}].{field}: the texture '{texture.Path}' of the material '{model.Materials[i].Name}' has no path relative to '{Path.GetFullPath(folder)}' (it is on another drive); save the file on the texture's drive, or copy the texture there and define the material again");
        return null;
    }

    /// <summary>The path of <paramref name="file"/> relative to <paramref name="folder"/> with <c>/</c> separators, or null when it has none.</summary>
    private static string? RelativePath(string folder, string file)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(folder), file);
        return Path.IsPathRooted(relative) ? null : relative.Replace('\\', '/');
    }

    private static string WriteText(Model model, string? folder)
    {
        var w = new Writer();
        w.Line(0, "{");
        // The lowest version that holds the model (the person's ruling, 2026-11-11): version 4 only when a face has holes or a slot is a
        // tombstone (T-64), so a model a version 2 or 3 file can hold is still written as before, byte for byte (T-42's rule and its tests).
        // Version 5 only when a mesh has UVs or seams (T-53, the person's ruling 2026-11-13).
        // Version 6 only when the model has a material (T-74), so every model without one is written as before, byte for byte.
        // Version 8 only when a mesh has a morph target (T-101). Version 7 only when some part is not a plain top-level mesh at the
        // origin: a parent, a transform, an envelope, instance_of, moves or a kept level (T-92, the person's ruling 2026-11-17).
        if (VersionProblem(model) is { } problem) throw new ArgumentException(problem, nameof(model));
        var version = HasPartTree(model) ? PartTreeVersion
            : model.Meshes.Any(m => m.Mesh.Targets is not null) ? Version
            : model.Materials.Count > 0 ? 6
            : model.Meshes.Any(m => m.Mesh.Uvs is not null) ? 5
            : model.Meshes.Any(m => m.Mesh.HasTombstones || m.Mesh.Faces.Any(f => f.HasHoles)) ? 4
            : model.Meshes.Any(m => m.Mesh.Shade is not null) ? 3 : 2;
        w.Line(1, $"\"{VersionKey}\": {version.ToString(CultureInfo.InvariantCulture)},");
        if (model.Materials.Count > 0)
            w.List(1, MaterialsKey, model.Materials.Count, i => MaterialText(model.Materials[i], folder), ",");
        w.Open(1, "\"meshes\": [", model.Meshes.Count == 0, ",");
        for (var i = 0; i < model.Meshes.Count; i++)
        {
            WriteMesh(w, model.Meshes[i], model.Skeleton, version, version == PartTreeVersion ? model.FindPart(model.Meshes[i].Name)!.Levels : []);
            w.Line(2, i < model.Meshes.Count - 1 ? "}," : "}");
        }
        w.Close(1, "],", model.Meshes.Count == 0);
        // Version 7 (T-92): every part's record, one per line in pre-order, so the tree and its order come back as they were.
        if (version == PartTreeVersion) w.List(1, PartsKey, model.Parts.Count, i => PartText(model.Parts[i]), ",");

        if (model.Skeleton is not { } skeleton) w.Line(1, "\"skeleton\": null,");
        else
        {
            w.Line(1, "\"skeleton\": [");
            w.Items(2, skeleton.Bones.Count, i =>
            {
                var b = skeleton.Bones[i];
                var parent = b.Parent < 0 ? "null" : Text(skeleton.Bones[b.Parent].Name);
                return $"{{\"name\": {Text(b.Name)}, \"parent\": {parent}, \"position\": {Vector(b.Position)}, \"rotation\": {Rotation(b.Rotation)}}}";
            });
            w.Line(1, "],");
        }

        w.Open(1, "\"clips\": [", model.Clips.Count == 0, "");
        for (var i = 0; i < model.Clips.Count; i++)
        {
            WriteClip(w, model.Clips[i]);
            w.Line(2, i < model.Clips.Count - 1 ? "}," : "}");
        }
        w.Close(1, "]", model.Clips.Count == 0);
        w.Line(0, "}");
        return w.ToString();
    }

    private static void WriteMesh(Writer w, NamedMesh named, Skeleton? skeleton, int version, IReadOnlyList<PartLevel> levels)
    {
        var (name, mesh) = named;
        w.Line(2, "{");
        w.Line(3, $"\"name\": {Text(name)},");
        WriteMeshKeys(w, 3, mesh, skeleton, version, levels.Count > 0 ? "," : "");
        if (levels.Count == 0) return;
        // Version 7 (T-92): "levels", after the mesh's last key, each kept level a whole mesh as version 6 writes one, but its name.
        w.Line(3, $"\"{LevelsKey}\": [");
        for (var i = 0; i < levels.Count; i++)
        {
            w.Line(4, "{");
            w.Line(5, $"\"level\": {levels[i].Number.ToString(CultureInfo.InvariantCulture)},");
            WriteMeshKeys(w, 5, levels[i].Mesh, skeleton, version, "");
            w.Line(4, i < levels.Count - 1 ? "}," : "}");
        }
        w.Line(3, "]");
    }

    /// <summary>A mesh's keys after its name, at <paramref name="depth"/>, the last followed by <paramref name="after"/>.</summary>
    private static void WriteMeshKeys(Writer w, int depth, PolygonMesh mesh, Skeleton? skeleton, int version, string after)
    {
        var d = depth;
        w.Line(d, $"\"{VerticesKey}\": [");
        w.Items(d + 1, mesh.Vertices.Count, i => mesh.IsVertexDeleted(i) ? "null" : Vector(mesh.Vertices[i]));
        w.Line(d, "],");
        w.Line(d, $"\"{FacesKey}\": [");
        w.Items(d + 1, mesh.Faces.Count, i => FaceText(mesh.Faces[i]));
        w.Line(d, "],");

        var style = mesh.Style ?? FaceStyle.None;
        var colours = style.Colours.GroupBy(c => c.Value, StringComparer.Ordinal)
            .Select(g => (Colour: g.Key, Faces: g.Select(c => c.Key).Order().ToArray()))
            .OrderBy(c => c.Faces[0]).ToArray();
        w.List(d, "colours", colours.Length, i => $"{{\"colour\": {Text(colours[i].Colour)}, \"faces\": {Ids(colours[i].Faces)}}}", ",");
        // Version 6 (T-74): "materials", left out when no face has one.
        var used = style.Materials.GroupBy(c => c.Value, StringComparer.Ordinal)
            .Select(g => (Material: g.Key, Faces: g.Select(c => c.Key).Order().ToArray()))
            .OrderBy(c => c.Faces[0]).ToArray();
        if (used.Length > 0)
            w.List(d, MaterialsKey, used.Length, i => $"{{\"material\": {Text(used[i].Material)}, \"faces\": {Ids(used[i].Faces)}}}", ",");
        var groups = style.Groups;
        w.List(d, "groups", groups.Count, i => $"{{\"name\": {Text(groups[i].Name)}, \"faces\": {Ids(groups[i].Faces)}}}", ",");

        // A weighted mesh is only ever in a model with a skeleton (Model.Create, ModelStore.SetWeights).
        var weights = mesh.Weights ?? VertexWeights.None;
        var vertices = weights.Vertices.ToArray();
        w.List(d, "weights", vertices.Length, i =>
        {
            var bones = string.Join(", ", weights.Of(vertices[i]).Select(x => $"{Text(skeleton!.Bones[x.Bone].Name)}: {Weight(x.Weight)}"));
            return $"{{\"vertex\": {vertices[i].ToString(CultureInfo.InvariantCulture)}, \"bones\": {{{bones}}}}}";
        }, version >= 3 ? "," : after);
        if (version < 3) return;
        w.Line(d, $"\"shading\": {Text(mesh.ShadingMode == ShadingMode.Smooth ? "smooth" : "flat")},");
        var hard = mesh.HardEdges;
        var uvs = mesh.Uvs ?? MeshUvs.None;
        var mapped = uvs.MappedFaces;
        var seams = uvs.Seams;
        var targets = mesh.Targets;
        w.List(d, "hard_edges", hard.Count, i => Ids([hard[i].Lo, hard[i].Hi]), mapped.Count > 0 || seams.Count > 0 || targets is not null ? "," : after);
        // Version 5 (T-53): "uvs" and "seams", each left out when empty.
        if (mapped.Count > 0)
            w.List(d, "uvs", mapped.Count, i => $"{{\"face\": {mapped[i].ToString(CultureInfo.InvariantCulture)}, \"uv\": [{string.Join(", ", uvs.Of(mapped[i])!.Select(Uv))}]}}",
                seams.Count > 0 || targets is not null ? "," : after);
        if (seams.Count > 0) w.List(d, "seams", seams.Count, i => Ids([seams[i].Lo, seams[i].Hi]), targets is not null ? "," : after);
        // Version 8 (T-101): "targets", one per line in definition order, left out when the mesh has none.
        if (targets is not null) w.List(d, TargetsKey, targets.Count, i => TargetText(targets.All[i]), after);
    }

    /// <summary>
    /// Whether the model needs version 7 (T-92): some part has a parent, a transform, an envelope, <c>instance_of</c>,
    /// <c>moves</c> or a kept level. A model of plain top-level meshes at the origin is written as before, byte for byte.
    /// </summary>
    public static bool HasPartTree(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Parts.Any(p => !p.IsPlain);
    }

    /// <summary>
    /// Why no version can hold <paramref name="model"/>, or null: a part tree beside morph targets, since version 7 holds no
    /// targets and version 8 no parts or levels. Which version holds both is the person's open question (RV-64), so such a
    /// model is refused rather than written without one of them.
    /// </summary>
    public static string? VersionProblem(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!HasPartTree(model)) return null;
        var targeted = model.Meshes.Where(m => m.Mesh.Targets is not null
            || model.FindPart(m.Name)!.Levels.Any(l => l.Mesh.Targets is not null)).Select(m => m.Name).ToArray();
        return targeted.Length == 0 ? null
            : $"the model has a part tree and morph targets (on {NameList.Quoted(targeted)}), and no Carve file version holds both yet: version 7 holds the part tree and kept levels, version 8 the targets (an open question, RV-64); "
                + "save the meshes with targets alone (save mesh=), or remove the targets or the tree";
    }

    /// <summary>A part's record on one line: name, parent, translation, rotation, scale, then envelope, instance_of and moves when it has them.</summary>
    private static string PartText(Part p)
    {
        var text = $"{{\"name\": {Text(p.Name)}, \"parent\": {(p.Parent is null ? "null" : Text(p.Parent))}, \"translation\": {Vector(p.Translation)}, \"rotation\": {Rotation(p.Rotation)}, \"scale\": {Vector(p.Scale)}";
        if (p.Envelope is { } e) text += $", \"envelope\": {{\"min\": {Vector(e.Min)}, \"max\": {Vector(e.Max)}}}";
        if (p.InstanceOf is { } source) text += $", \"instance_of\": {Text(source)}";
        if (p.Moves) text += ", \"moves\": true";
        return text + "}";
    }

    /// <summary>A morph target on one line: its name and its sparse deltas by vertex id, ids ascending, each float by its bits.</summary>
    private static string TargetText(MorphTarget target) =>
        $"{{\"name\": {Text(target.Name)}, \"deltas\": {{{string.Join(", ", target.Deltas.Select(d => $"\"{d.Key.ToString(CultureInfo.InvariantCulture)}\": {Vector(d.Value)}"))}}}}}";

    private static string Uv(Vector2 uv) => $"[{Number(uv.X)}, {Number(uv.Y)}]";

    /// <summary>A material on one line: its name, then each field that is not its default (hex by its text, floats by their bits), in record order.</summary>
    private static string MaterialText(Material m, string? folder)
    {
        var parts = new List<string> { $"\"name\": {Text(m.Name)}" };
        if (!string.Equals(m.BaseColour, Material.DefaultBaseColour, StringComparison.Ordinal)) parts.Add($"\"base_colour\": {Text(m.BaseColour)}");
        if (!SameBits(m.Metallic, Material.DefaultMetallic)) parts.Add($"\"metallic\": {Number(m.Metallic)}");
        if (!SameBits(m.Roughness, Material.DefaultRoughness)) parts.Add($"\"roughness\": {Number(m.Roughness)}");
        if (!string.Equals(m.Emissive, Material.DefaultEmissive, StringComparison.Ordinal)) parts.Add($"\"emissive\": {Text(m.Emissive)}");
        if (!SameBits(m.Occlusion, Material.DefaultOcclusion)) parts.Add($"\"occlusion\": {Number(m.Occlusion)}");
        if (m.DoubleSided) parts.Add("\"double_sided\": true");
        foreach (var (field, texture) in m.Textures)
            if (texture is not null) parts.Add($"\"{field}\": {Text(RelativePath(folder!, texture.Path)!)}");
        return "{" + string.Join(", ", parts) + "}";
    }

    private static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    private static void WriteClip(Writer w, Clip clip)
    {
        w.Line(2, "{");
        w.Line(3, $"\"name\": {Text(clip.Name)},");
        w.Line(3, $"\"duration\": {Number(clip.Duration)},");
        w.Line(3, $"\"loop\": {(clip.Loop ? "true" : "false")},");
        w.Open(3, "\"channels\": {", clip.Channels.Count == 0, "", "{}");
        for (var i = 0; i < clip.Channels.Count; i++)
        {
            var c = clip.Channels[i];
            w.Line(4, $"{Text(c.Bone)}: {{");
            if (c.Rotation is { } r) WriteChannel(w, "rotation", r, Rotation, c.Translation is not null);
            if (c.Translation is { } t) WriteChannel(w, "translation", t, Vector, false);
            w.Line(4, i < clip.Channels.Count - 1 ? "}," : "}");
        }
        w.Close(3, "}", clip.Channels.Count == 0);
    }

    private static void WriteChannel<T>(Writer w, string part, ClipChannel<T> channel, Func<T, string> value, bool more)
    {
        w.Line(5, $"\"{part}\": {{");
        w.Line(6, $"\"interpolation\": \"{(channel.Interpolation == ClipInterpolation.Step ? "STEP" : "LINEAR")}\",");
        w.Line(6, "\"keys\": [");
        w.Items(7, channel.Keys.Count, i => $"{{\"time\": {Number(channel.Keys[i].Time)}, \"value\": {value(channel.Keys[i].Value)}}}");
        w.Line(6, "]");
        w.Line(5, more ? "}," : "}");
    }

    /// <summary>Lines with two-space indent, each ending in <c>\n</c>.</summary>
    private sealed class Writer
    {
        private readonly StringBuilder _sb = new();

        public void Line(int depth, string text) => _sb.Append(' ', depth * 2).Append(text).Append('\n');

        /// <summary>Items one per line, commas between.</summary>
        public void Items(int depth, int count, Func<int, string> item)
        {
            for (var i = 0; i < count; i++) Line(depth, item(i) + (i < count - 1 ? "," : ""));
        }

        /// <summary>A key holding a list of one-line items, written <c>[]</c> when empty.</summary>
        public void List(int depth, string key, int count, Func<int, string> item, string after)
        {
            if (count == 0)
            {
                Line(depth, $"\"{key}\": []{after}");
                return;
            }
            Line(depth, $"\"{key}\": [");
            Items(depth + 1, count, item);
            Line(depth, "]" + after);
        }

        /// <summary>The opening line of a key whose items span lines; when empty, the whole key on one line.</summary>
        public void Open(int depth, string opening, bool empty, string after, string emptyValue = "[]")
        {
            if (empty) Line(depth, opening[..^1] + emptyValue + after);
            else Line(depth, opening);
        }

        public void Close(int depth, string closing, bool empty)
        {
            if (!empty) Line(depth, closing);
        }

        public override string ToString() => _sb.ToString();
    }

    private static string Vector(Vector3 v) => $"[{Number(v.X)}, {Number(v.Y)}, {Number(v.Z)}]";

    private static string Rotation(Quaternion q) => $"[{Number(q.X)}, {Number(q.Y)}, {Number(q.Z)}, {Number(q.W)}]";

    /// <summary>A face as version 4 writes it: its loop as a plain array, with holes the object form, or a tombstone <c>null</c>.</summary>
    private static string FaceText(Face face) => face.IsDeleted ? "null" : face.HasHoles
        ? $"{{\"loop\": {Ids(face.Loop)}, \"holes\": [{string.Join(", ", face.Holes.Select(Ids))}]}}"
        : Ids(face.Loop);

    private static string Ids(IEnumerable<int> ids) => "[" + string.Join(", ", ids.Select(i => i.ToString(CultureInfo.InvariantCulture))) + "]";

    /// <summary>A JSON string: only <c>"</c>, <c>\</c> and control characters escaped, so names read as written.</summary>
    private static string Text(string s) => JsonSerializer.Serialize(s, StringOptions);

    /// <summary>The shortest text that reads back to the same float; <c>-0</c> stays <c>-0</c>.</summary>
    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>The shortest text that reads back to the same double.</summary>
    private static string Weight(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    // ---- Reading ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads a file's text into a model, or every error that stops one, each named by where it is in the file. A material
    /// with a texture is refused, since its path is relative to the file's folder: use <see cref="Read(string, string)"/>.
    /// </summary>
    public static CarveFileResult Read(string text) => Read(text, null);

    /// <summary>
    /// Reads the text of a file in <paramref name="folder"/>: as <see cref="Read(string)"/>, with each texture path resolved
    /// against the folder and its PNG read. A texture that is missing or unreadable is an error naming the path the file
    /// gave and the absolute path tried.
    /// </summary>
    public static CarveFileResult Read(string text, string? folder) => Read(text, folder, null);

    /// <summary>
    /// As <see cref="Read(string, string)"/>, for <c>open mesh=</c>: when <paramref name="mesh"/> names a mesh of a version 6
    /// file, only the materials that mesh's faces use are read and checked (an unused material's missing texture refuses
    /// nothing, T-74's ruling, RV-47), and the model holds that mesh alone, with the file's skeleton and clips. A name the
    /// file has no mesh of reads the whole file, so the caller can list the meshes there are.
    /// </summary>
    public static CarveFileResult Read(string text, string? folder, string? mesh)
    {
        ArgumentNullException.ThrowIfNull(text);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (JsonException e)
        {
            return Refused(new MeshError("file", NotJsonMessage(e)));
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Refused(new MeshError("file", $"the file is a JSON {Kind(root)}; a Carve file is a JSON object"));
            return VersionOf(root) is (2 or 3 or 4 or 5 or 6 or 7 or 8) and { } version ? ReadVersion2(root, version, folder, mesh) : ReadVersion1(root);
        }
    }

    /// <summary>The integer the first <c>carve</c> key holds, or null.</summary>
    private static int? VersionOf(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            try
            {
                if (property.Name != VersionKey) continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            return property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var n) ? n : null;
        }
        return null;
    }

    private const string SurrogateKey = "the key is not valid text: it holds a lone surrogate escape (a \\ud800 to \\udfff with no partner)";

    /// <summary>Version 1, T-15's file of one mesh, and every file whose version is missing, not an integer or not read here.</summary>
    private static CarveFileResult ReadVersion1(JsonElement root)
    {
        var errors = new List<MeshError>();
        JsonElement? version = null, vertices = null, faces = null;
        var position = -1;
        foreach (var property in root.EnumerateObject())
        {
            position++;

            // The parser accepts a lone surrogate escape in a key but cannot hand it back as text: that is a refusal
            // here, named by the key's place in the object, and the file's other problems are still listed.
            string key;
            try
            {
                key = property.Name;
            }
            catch (InvalidOperationException)
            {
                errors.Add(new MeshError(Invariant($"keys[{position}]"), SurrogateKey));
                continue;
            }

            switch (key)
            {
                case VersionKey: Take(ref version, property); break;
                case VerticesKey: Take(ref vertices, property); break;
                case FacesKey: Take(ref faces, property); break;
                default:
                    errors.Add(new MeshError(key,
                        $"unknown key: a version 1 Carve file has only the keys \"{VersionKey}\", \"{VerticesKey}\" and \"{FacesKey}\""));
                    break;
            }
        }

        void Take(ref JsonElement? slot, JsonProperty property)
        {
            if (slot is not null)
                errors.Add(new MeshError(property.Name, "key appears more than once"));
            else
                slot = property.Value;
        }

        // The version first: a file of another version is refused on that alone, since its other keys may mean something else.
        if (version is not { } v)
            errors.Add(new MeshError(VersionKey, $"missing: the file has no format version; this build reads {Versions()}"));
        else if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var number))
            errors.Add(new MeshError(VersionKey, $"the format version is {Describe(v)}, not an integer; this build reads {Versions()}"));
        else if (!ReadableVersions.Contains(number))
            return Refused(new MeshError(VersionKey, Invariant($"version {number} is not one this build reads; it reads {Versions()}")));

        var verts = ReadVertices("", "the file", vertices, errors);
        var fcs = ReadFaces("", "the file", faces, errors);
        if (errors.Count > 0)
            return Refused(errors);

        var created = PolygonMesh.Create(verts!, fcs!);
        if (!created.Succeeded)
            return Refused(created.Errors);
        return new CarveFileResult(Model.Of(Model.DefaultName, created.Mesh!), [], MeshReport.Create(created.Mesh!).Warnings, 1);
    }

    /// <summary>Version 2: the whole model.</summary>
    private static CarveFileResult ReadVersion2(JsonElement root, int version, string? folder, string? only)
    {
        var errors = new List<MeshError>();
        var fields = Fields(root, "", Invariant($"a version {version} Carve file"), version == PartTreeVersion ? FileKeysV7 : version >= 6 ? FileKeysV6 : FileKeys, errors)!;
        var used = only is null || version < 6 ? null : MaterialsUsedBy(fields, only);

        // The materials first, since faces name them (T-74); a name is known even when the rest of its material is not.
        var materials = new List<Material>();
        var materialNames = new List<string>();
        if (fields.TryGetValue(MaterialsKey, out var ms) && List(ms, MaterialsKey, errors) is { } materialList)
        {
            // Keyed by the path exactly as resolved: two spellings of one path differ in case only on a case-insensitive
            // system, where each is read and keeps its own path; on a case-sensitive one they are two files (RV-47).
            var loaded = new Dictionary<string, (MaterialTexture? Texture, string? Problem)>(StringComparer.Ordinal);
            var i = 0;
            foreach (var item in materialList.EnumerateArray())
                if (used is not null && NameOf(item) is { } other && !used.Contains(other))
                {
                    // A material the opened mesh does not use is not brought in, so it is not read: only its name is kept,
                    // so the file's other meshes that name it are not refused.
                    if (!materialNames.Contains(other)) materialNames.Add(other);
                    i++;
                }
                else if (ReadMaterial(item, Invariant($"materials[{i++}]"), folder, loaded, materialNames, errors) is { } material) materials.Add(material);
        }

        // The skeleton is read first, since weights and clips name its bones; its problems are listed after the meshes'.
        var skeletonErrors = new List<MeshError>();
        Skeleton? skeleton = null;
        var skeletonGood = true;
        if (fields.TryGetValue("skeleton", out var s) && s.ValueKind != JsonValueKind.Null)
        {
            skeleton = ReadSkeleton(s, skeletonErrors);
            skeletonGood = skeleton is not null;
        }

        var meshes = new List<NamedMesh>();
        var levels = new Dictionary<string, PartLevel[]>(StringComparer.Ordinal);
        if (!fields.TryGetValue("meshes", out var m)) errors.Add(new MeshError("meshes", "missing: the file has no meshes array"));
        else if (m.ValueKind != JsonValueKind.Array) errors.Add(new MeshError("meshes", $"meshes is {Describe(m)}, not an array"));
        else if (m.GetArrayLength() == 0)
        {
            // A skeleton and clips with no mesh, a clip library, is a whole file (the person, 2026-11-09).
            if (!fields.TryGetValue("skeleton", out var rig) || rig.ValueKind == JsonValueKind.Null)
                errors.Add(new MeshError("meshes", "the file has no meshes and no skeleton; a Carve file holds at least one mesh, or a skeleton (a clip library)"));
        }
        else
        {
            var i = 0;
            foreach (var item in m.EnumerateArray())
            {
                if (ReadMesh(item, Invariant($"meshes[{i}]"), skeleton, skeletonGood, version, materialNames, errors, levels) is { } mesh) meshes.Add(mesh);
                i++;
            }
        }
        errors.AddRange(skeletonErrors);

        var clips = new List<Clip>();
        if (fields.TryGetValue("clips", out var c))
        {
            if (List(c, "clips", errors) is { } list)
            {
                if (skeleton is null && skeletonGood && list.GetArrayLength() > 0)
                    errors.Add(new MeshError("clips", "the file has no skeleton, and clips move its bones"));
                var i = 0;
                foreach (var item in list.EnumerateArray())
                {
                    if (ReadClip(item, Invariant($"clips[{i}]"), skeleton, errors) is { } clip) clips.Add(clip);
                    i++;
                }
            }
        }

        // Version 7 (T-92): every part's record, after the meshes and before the skeleton, as the file writes it.
        var parts = new List<Part>();
        if (version == PartTreeVersion && Required(fields, "", PartsKey, "a version 7 Carve file", errors) is { } ps && List(ps, PartsKey, errors) is { } partList)
        {
            var i = 0;
            foreach (var item in partList.EnumerateArray())
                if (ReadPart(item, Invariant($"{PartsKey}[{i++}]"), errors) is { } part) parts.Add(part);
        }

        if (errors.Count > 0) return Refused(errors);
        if (used is not null && meshes.Count(x => x.Name == only) == 1)
        {
            // open mesh= takes one part's own geometry and levels: not its place in the file's tree (the person's ruling, M-9).
            meshes = [meshes.Single(x => x.Name == only)];
            parts = levels.TryGetValue(only!, out var own) ? [Part.Of(only!) with { Levels = own }] : [];
        }
        else
        {
            // A mesh's levels join its part's record; a mesh with levels and no record is a top-level part, after the records.
            var recorded = parts.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            parts = [.. parts.Select(p => levels.TryGetValue(p.Name, out var own) ? p with { Levels = own } : p),
                .. meshes.Where(x => levels.ContainsKey(x.Name) && !recorded.Contains(x.Name)).Select(x => Part.Of(x.Name) with { Levels = levels[x.Name] })];
        }
        var (model, problems) = Model.Create(meshes, skeleton, clips, materials, parts);
        if (model is null) return Refused(problems);
        return new CarveFileResult(model, [], model.Meshes.SelectMany(x => MeshReport.Create(x.Mesh).Warnings).ToArray(), version);
    }

    /// <summary>A JSON object's <c>name</c> string, or null.</summary>
    private static string? NameOf(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;

    /// <summary>
    /// The names of the materials the faces of the mesh named <paramref name="mesh"/> use, as the file lists them; null when
    /// the file has no mesh of that name (then the whole file is read).
    /// </summary>
    private static HashSet<string>? MaterialsUsedBy(Dictionary<string, JsonElement> fields, string mesh)
    {
        if (!fields.TryGetValue("meshes", out var meshes) || meshes.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in meshes.EnumerateArray())
        {
            if (NameOf(item) != mesh) continue;
            var used = new HashSet<string>(StringComparer.Ordinal);
            void Add(JsonElement holder)
            {
                if (holder.ValueKind == JsonValueKind.Object && holder.TryGetProperty(MaterialsKey, out var uses) && uses.ValueKind == JsonValueKind.Array)
                    foreach (var use in uses.EnumerateArray())
                        if (use.ValueKind == JsonValueKind.Object && use.TryGetProperty("material", out var m) && m.ValueKind == JsonValueKind.String)
                            used.Add(m.GetString()!);
            }
            Add(item);
            // Version 7 (T-92): a kept level's faces may use materials the live mesh no longer does.
            if (item.TryGetProperty(LevelsKey, out var kept) && kept.ValueKind == JsonValueKind.Array)
                foreach (var level in kept.EnumerateArray()) Add(level);
            return used;
        }
        return null;
    }

    /// <summary>
    /// One of version 6's materials (T-74): every field checked as <see cref="Material.Problems"/> checks it, at its key, and
    /// each texture read from the folder, each absolute path once per read. Its name, when it is a usable one, joins
    /// <paramref name="names"/> even when the rest is refused, so faces naming it are not refused as well.
    /// </summary>
    private static Material? ReadMaterial(JsonElement item, string at, string? folder,
        Dictionary<string, (MaterialTexture? Texture, string? Problem)> loaded, List<string> names, List<MeshError> errors)
    {
        var before = errors.Count;
        if (Fields(item, at, "a material", MaterialKeys, errors) is not { } f) return null;
        var name = Required(f, at, "name", "a material", errors) is { } n ? Str(n, $"{at}.name", errors) : null;
        if (name is not null)
        {
            if (Material.NameProblem(name) is { } bad) errors.Add(new MeshError($"{at}.name", bad));
            else if (names.Contains(name)) errors.Add(new MeshError($"{at}.name", $"a material named '{name}' comes earlier; material names must be unique"));
            else names.Add(name);
        }
        string? Hex(string key, string fallback) =>
            f.TryGetValue(key, out var h) ? Str(h, $"{at}.{key}", errors) : fallback;
        float? Factor(string key, float fallback) =>
            f.TryGetValue(key, out var x) ? Float(x, $"{at}.{key}", errors) : fallback;
        var baseColour = Hex("base_colour", Material.DefaultBaseColour);
        var metallic = Factor("metallic", Material.DefaultMetallic);
        var roughness = Factor("roughness", Material.DefaultRoughness);
        var emissive = Hex("emissive", Material.DefaultEmissive);
        var occlusion = Factor("occlusion", Material.DefaultOcclusion);
        var doubleSided = false;
        if (f.TryGetValue("double_sided", out var ds))
        {
            if (ds.ValueKind is JsonValueKind.True or JsonValueKind.False) doubleSided = ds.GetBoolean();
            else errors.Add(new MeshError($"{at}.double_sided", $"double_sided is {Describe(ds)}, not true or false"));
        }
        var textures = new Dictionary<string, MaterialTexture>();
        foreach (var field in MaterialKeys.Skip(1 + MaterialFactorKeys.Length))
        {
            if (!f.TryGetValue(field, out var t) || Str(t, $"{at}.{field}", errors) is not { } given) continue;
            if (folder is null)
            {
                errors.Add(new MeshError($"{at}.{field}", "a texture needs the file's folder, since its path is relative to it"));
                continue;
            }
            string full;
            try
            {
                full = given.Length == 0 ? throw new ArgumentException("the path is empty") : Path.GetFullPath(Path.Combine(folder, given));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add(new MeshError($"{at}.{field}", $"cannot read '{given}': {e.Message}"));
                continue;
            }
            if (!loaded.TryGetValue(full, out var read))
                loaded[full] = read = (MaterialTexture.Load(full, out var why), why);
            if (read.Texture is { } texture) textures[field] = texture;
            else errors.Add(new MeshError($"{at}.{field}", $"cannot read '{given}' ({full}): {read.Problem}"));
        }
        if (errors.Count > before || name is null) return null;

        var material = new Material(name, baseColour!, metallic!.Value, roughness!.Value, emissive!, occlusion!.Value, doubleSided,
            textures.GetValueOrDefault("base_colour_texture"), textures.GetValueOrDefault("metallic_roughness_texture"),
            textures.GetValueOrDefault("normal_texture"), textures.GetValueOrDefault("emissive_texture"),
            textures.GetValueOrDefault("occlusion_texture"));
        var problems = Material.Problems(material).Where(p => p.Field != "name").ToArray();
        errors.AddRange(problems.Select(p => new MeshError($"{at}.{p.Field}", p.Problem)));
        return problems.Length > 0 ? null : material;
    }

    private static NamedMesh? ReadMesh(JsonElement item, string at, Skeleton? skeleton, bool skeletonGood, int version, List<string> materialNames, List<MeshError> errors,
        Dictionary<string, PartLevel[]>? levels = null, bool level = false)
    {
        var before = errors.Count;
        if (Fields(item, at, level ? "a level" : "a mesh", level ? LevelKeys : version == PartTreeVersion ? MeshKeysV7 : version >= 8 ? MeshKeysV8 : version >= 6 ? MeshKeysV6 : version >= 5 ? MeshKeysV5 : version >= 3 ? MeshKeysV3 : MeshKeys, errors) is not { } f) return null;
        var name = level ? "" : Required(f, at, "name", "a mesh", errors) is { } n ? Str(n, $"{at}.name", errors) : null;
        f.TryGetValue(VerticesKey, out var vs);
        f.TryGetValue(FacesKey, out var fs);
        var deleted = new List<int>();
        var verts = ReadVertices(at + ".", "the mesh", f.ContainsKey(VerticesKey) ? vs : null, errors, version, deleted);
        var faces = ReadFaces(at + ".", "the mesh", f.ContainsKey(FacesKey) ? fs : null, errors, version);
        if (errors.Count > before) return null;

        // A live face using a null vertex is refused by Create at its corner, which names the face.
        var created = PolygonMesh.Create(verts!, faces!, deleted);
        if (!created.Succeeded)
        {
            errors.AddRange(created.Errors.Select(e => new MeshError($"{at}.{e.Element}", e.Message)));
            return null;
        }
        var mesh = created.Mesh!;

        var style = FaceStyle.None;
        var coloured = new Dictionary<int, string>();
        if (f.TryGetValue("colours", out var colours) && List(colours, $"{at}.colours", errors) is { } cl)
        {
            var i = 0;
            foreach (var entry in cl.EnumerateArray())
            {
                var e = Invariant($"{at}.colours[{i++}]");
                if (Fields(entry, e, "a colour", ColourKeys, errors) is not { } cf) continue;
                var hex = Required(cf, e, "colour", "a colour", errors) is { } h ? Str(h, $"{e}.colour", errors) : null;
                if (hex is not null && FaceStyle.ColourProblem(hex) is { } bad) errors.Add(new MeshError($"{e}.colour", bad));
                var ids = FaceList(cf, e, "a colour", mesh, "nothing to colour: list at least one face", errors);
                if (hex is null || ids is null) continue;
                foreach (var (face, k) in ids.Select((x, k) => (x, k)))
                    if (!coloured.TryAdd(face, hex))
                        errors.Add(new MeshError(Invariant($"{e}.faces[{k}]"), Invariant($"face {face} is given a colour earlier; give each face one colour")));
                style = style.WithColour(ids, hex);
            }
        }
        if (f.TryGetValue(MaterialsKey, out var uses) && List(uses, $"{at}.{MaterialsKey}", errors) is { } ul)
        {
            // Version 6 (T-74): each face's material, which the file must define; a face has a colour or a material, not both.
            var given = new HashSet<int>();
            var i = 0;
            foreach (var entry in ul.EnumerateArray())
            {
                var e = Invariant($"{at}.{MaterialsKey}[{i++}]");
                if (Fields(entry, e, "a face material", MaterialUseKeys, errors) is not { } uf) continue;
                var used = Required(uf, e, "material", "a face material", errors) is { } u ? Str(u, $"{e}.material", errors) : null;
                if (used is not null && !materialNames.Contains(used))
                {
                    errors.Add(new MeshError($"{e}.material", materialNames.Count == 0
                        ? $"no material named '{used}'; the file defines no materials"
                        : $"no material named '{used}'; the materials are {NameList.Quoted(materialNames)}"));
                    used = null;
                }
                var ids = FaceList(uf, e, "a face material", mesh, "nothing to give the material: list at least one face", errors);
                if (used is null || ids is null) continue;
                foreach (var (face, k) in ids.Select((x, k) => (x, k)))
                {
                    if (!given.Add(face))
                        errors.Add(new MeshError(Invariant($"{e}.faces[{k}]"), Invariant($"face {face} is given a material earlier; give each face one material")));
                    else if (coloured.ContainsKey(face))
                        errors.Add(new MeshError(Invariant($"{e}.faces[{k}]"), Invariant($"face {face} is given a colour too; a face has a colour or a material, not both")));
                }
                style = style.WithMaterial(ids, used);
            }
        }
        if (f.TryGetValue("groups", out var groups) && List(groups, $"{at}.groups", errors) is { } gl)
        {
            var names = new HashSet<string>();
            var i = 0;
            foreach (var entry in gl.EnumerateArray())
            {
                var e = Invariant($"{at}.groups[{i++}]");
                if (Fields(entry, e, "a group", GroupKeys, errors) is not { } gf) continue;
                var group = Required(gf, e, "name", "a group", errors) is { } g ? Str(g, $"{e}.name", errors) : null;
                if (group is not null && FaceStyle.GroupNameProblem(group) is { } bad) { errors.Add(new MeshError($"{e}.name", bad)); group = null; }
                else if (group is not null && !names.Add(group)) { errors.Add(new MeshError($"{e}.name", $"a group named '{group}' comes earlier; group names must be unique")); group = null; }
                var ids = FaceList(gf, e, "a group", mesh, "nothing to group: list at least one face", errors);
                if (group is not null && ids is not null) style = style.WithGroup(group, ids);
            }
        }

        var weights = new Dictionary<int, Influence[]>();
        if (f.TryGetValue("weights", out var ws) && List(ws, $"{at}.weights", errors) is { } wl && wl.GetArrayLength() > 0)
        {
            if (skeleton is null && skeletonGood)
                errors.Add(new MeshError($"{at}.weights", "the file has no skeleton, and weights name its bones"));
            var first = new Dictionary<int, int>();
            var i = 0;
            foreach (var entry in wl.EnumerateArray())
            {
                var k = i++;
                var e = Invariant($"{at}.weights[{k}]");
                if (Fields(entry, e, "a weighted vertex", WeightKeys, errors) is not { } wf) continue;
                int? vertex = null;
                if (Required(wf, e, "vertex", "a weighted vertex", errors) is { } v && Int(v, $"{e}.vertex", "a vertex id", errors) is { } id)
                {
                    if (VertexWeights.VertexProblem(mesh, id) is { } bad) errors.Add(new MeshError($"{e}.vertex", bad));
                    else if (!first.TryAdd(id, k)) errors.Add(new MeshError($"{e}.vertex", Invariant($"vertex {id} is given weights earlier, at weights[{first[id]}]; give each vertex once")));
                    else vertex = id;
                }
                if (Required(wf, e, "bones", "a weighted vertex", errors) is not { } b) continue;
                if (b.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new MeshError($"{e}.bones", $"bones is {Describe(b)}; it is an object of bone name to weight"));
                    continue;
                }
                var given = new List<KeyValuePair<string, double>>();
                var good = true;
                foreach (var (bone, weight) in Pairs(b, $"{e}.bones", errors, ref good))
                {
                    if (Double(weight, $"{e}.bones.{bone}", errors) is { } d) given.Add(KeyValuePair.Create(bone, d));
                    else good = false;
                }
                if (!good || skeleton is null) continue;
                if (VertexWeights.Raw(skeleton, given, $"{e}.bones", errors) is { } raw && vertex is { } vx)
                    weights[vx] = VertexWeights.Kept(raw, out _);
            }
        }

        MeshShade? shade = null;
        if (version >= 3) shade = ReadShade(f, at, mesh, errors);
        MeshUvs? uvs = null;
        if (version >= 5) uvs = ReadUvs(f, at, mesh, errors);
        MorphTargets? targets = null;
        if (version >= 8) targets = ReadTargets(f, at, mesh, errors);
        // Version 7 (T-92): the part's kept levels, each a whole mesh as a version 6 mesh holds it.
        PartLevel[]? kept = null;
        if (!level && version == PartTreeVersion && f.TryGetValue(LevelsKey, out var ls))
            kept = ReadLevels(ls, $"{at}.{LevelsKey}", skeleton, skeletonGood, version, materialNames, errors);

        if (errors.Count > before || name is null) return null;
        if (kept is not null && levels is not null) levels.TryAdd(name, kept);
        return new NamedMesh(name, mesh.WithStyle(style).WithWeights(VertexWeights.Of(weights)).WithShade(shade).WithUvs(uvs).WithTargets(targets));
    }

    /// <summary>
    /// Version 7's <c>levels</c> in a mesh (T-92): each <c>{"level": N, ...}</c> holding the keys of a version 6 mesh but its
    /// name, read as a mesh is, every problem named where it is (<c>meshes[0].levels[1].faces[3]</c>); the numbers 0 or more
    /// and ascending, each once, as <see cref="Part.Problems"/> holds them.
    /// </summary>
    private static PartLevel[]? ReadLevels(JsonElement element, string at, Skeleton? skeleton, bool skeletonGood, int version, List<string> materialNames, List<MeshError> errors)
    {
        if (List(element, at, errors) is not { } list) return null;
        var before = errors.Count;
        var levels = new List<PartLevel>();
        int? previous = null;
        var i = 0;
        foreach (var item in list.EnumerateArray())
        {
            var e = Invariant($"{at}[{i++}]");
            int? number = null;
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (!item.TryGetProperty("level", out var n)) errors.Add(new MeshError($"{e}.level", "missing: a level needs level"));
                else if (Int(n, $"{e}.level", "level number", errors) is { } k)
                {
                    if (k < 0) errors.Add(new MeshError($"{e}.level", Invariant($"level {k} is negative; a level number is 0 or more")));
                    else if (previous is { } p && k <= p)
                        errors.Add(new MeshError($"{e}.level", Invariant($"level {k} comes after level {p}; levels are kept in ascending order, each number once")));
                    else number = k;
                    previous = k;
                }
            }
            var mesh = ReadMesh(item, e, skeleton, skeletonGood, version, materialNames, errors, level: true);
            if (mesh is not null && number is { } kept) levels.Add(new PartLevel(kept, mesh.Mesh));
        }
        return errors.Count > before ? null : [.. levels];
    }

    /// <summary>
    /// One of version 7's <c>parts</c> (T-92): <c>name</c>, <c>parent</c> (a name or null), <c>translation</c>, <c>rotation</c>
    /// <c>[x, y, z, w]</c> and <c>scale</c>, and the optional <c>envelope</c> (<c>{"min": [...], "max": [...]}</c>),
    /// <c>instance_of</c> and <c>moves</c>. Its values are read as given; what <see cref="Model.Create"/> refuses is refused there.
    /// </summary>
    private static Part? ReadPart(JsonElement item, string at, List<MeshError> errors)
    {
        var before = errors.Count;
        if (Fields(item, at, "a part", PartRecordKeys, errors) is not { } f) return null;
        var name = Required(f, at, "name", "a part", errors) is { } n ? Str(n, $"{at}.name", errors) : null;
        string? parent = null;
        if (Required(f, at, "parent", "a part", errors) is { } pe && pe.ValueKind != JsonValueKind.Null)
            parent = Str(pe, $"{at}.parent", errors, "a part name or null");
        var translation = Required(f, at, "translation", "a part", errors) is { } t ? Floats(t, $"{at}.translation", 3, "a translation is [x, y, z]", errors) : null;
        var rotation = Required(f, at, "rotation", "a part", errors) is { } r ? Floats(r, $"{at}.rotation", 4, "a rotation is [x, y, z, w]", errors) : null;
        var scale = Required(f, at, "scale", "a part", errors) is { } sc ? Floats(sc, $"{at}.scale", 3, "a scale is [x, y, z]", errors) : null;
        PartEnvelope? envelope = null;
        if (f.TryGetValue("envelope", out var env) && Fields(env, $"{at}.envelope", "an envelope", EnvelopeKeys, errors) is { } ef)
        {
            var min = Required(ef, $"{at}.envelope", "min", "an envelope", errors) is { } lo ? Floats(lo, $"{at}.envelope.min", 3, "min is [x, y, z]", errors) : null;
            var max = Required(ef, $"{at}.envelope", "max", "an envelope", errors) is { } hi ? Floats(hi, $"{at}.envelope.max", 3, "max is [x, y, z]", errors) : null;
            if (min is not null && max is not null) envelope = new PartEnvelope(new Vector3(min[0], min[1], min[2]), new Vector3(max[0], max[1], max[2]));
        }
        string? instanceOf = null;
        if (f.TryGetValue("instance_of", out var io)) instanceOf = Str(io, $"{at}.instance_of", errors, "a part name");
        var moves = false;
        if (f.TryGetValue("moves", out var mv))
        {
            if (mv.ValueKind is JsonValueKind.True or JsonValueKind.False) moves = mv.GetBoolean();
            else errors.Add(new MeshError($"{at}.moves", $"moves is {Describe(mv)}, not true or false"));
        }
        if (errors.Count > before || name is null) return null;
        return new Part(name)
        {
            Parent = parent,
            Translation = new Vector3(translation![0], translation[1], translation[2]),
            Rotation = new Quaternion(rotation![0], rotation[1], rotation[2], rotation[3]),
            Scale = new Vector3(scale![0], scale[1], scale[2]),
            Envelope = envelope,
            InstanceOf = instanceOf,
            Moves = moves,
        };
    }

    /// <summary>
    /// Version 8's <c>targets</c> (T-101), optional: each <c>{"name": ..., "deltas": {"&lt;id&gt;": [dx, dy, dz], ...}}</c>, checked
    /// as <see cref="MorphTarget.FromDeltas"/> checks it, every fault named by element: a bad or repeated name, a key that is
    /// not a vertex id or names a vertex the mesh lacks or has deleted, a non-finite number, and a zero delta (never written).
    /// </summary>
    private static MorphTargets? ReadTargets(Dictionary<string, JsonElement> f, string at, PolygonMesh mesh, List<MeshError> errors)
    {
        if (!f.TryGetValue(TargetsKey, out var ts) || List(ts, $"{at}.{TargetsKey}", errors) is not { } list) return null;
        var targets = new List<MorphTarget>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var k = 0;
        foreach (var entry in list.EnumerateArray())
        {
            var e = Invariant($"{at}.{TargetsKey}[{k++}]");
            var before = errors.Count;
            if (Fields(entry, e, "a target", TargetKeys, errors) is not { } tf) continue;
            var name = Required(tf, e, "name", "a target", errors) is { } n ? Str(n, $"{e}.name", errors) : null;
            if (name is not null && MorphTarget.NameProblem(name) is { } bad) errors.Add(new MeshError($"{e}.name", bad));
            else if (name is not null && !names.Add(name)) errors.Add(new MeshError($"{e}.name", $"a target named '{name}' comes earlier; target names must be unique in a mesh"));
            if (Required(tf, e, "deltas", "a target", errors) is not { } ds) continue;
            if (ds.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new MeshError($"{e}.deltas", $"deltas is {Describe(ds)}; it is an object of vertex id to [dx, dy, dz]"));
                continue;
            }
            var deltas = new List<KeyValuePair<int, Vector3>>();
            var seen = new HashSet<int>();
            var good = true;
            foreach (var (key, value) in Pairs(ds, $"{e}.deltas", errors, ref good))
            {
                var d = $"{e}.deltas.{key}";
                if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id.ToString(CultureInfo.InvariantCulture) != key)
                {
                    errors.Add(new MeshError(d, $"'{key}' is not a vertex id: each key of deltas is a vertex id written in decimal, as \"12\""));
                    continue;
                }
                if (mesh.VertexProblem(id) is { } problem) { errors.Add(new MeshError(d, problem)); continue; }
                if (!seen.Add(id)) { errors.Add(new MeshError(d, Invariant($"vertex {id} is given a delta earlier in this target; give each vertex once"))); continue; }
                if (Floats(value, d, 3, "a delta is three numbers, [dx, dy, dz]", errors) is not { } xyz) continue;
                var delta = new Vector3(xyz[0], xyz[1], xyz[2]);
                if (delta == Vector3.Zero) errors.Add(new MeshError(d, Invariant($"the delta of vertex {id} is zero, and a zero delta is never written: leave the vertex out")));
                else deltas.Add(KeyValuePair.Create(id, delta));
            }
            if (errors.Count > before || name is null) continue;
            var made = MorphTarget.FromDeltas(mesh, name, deltas);
            if (made.Target is { } target) targets.Add(target);
            else errors.AddRange(made.Errors.Select(x => new MeshError($"{e}.{x.Element}", x.Message)));
        }
        return errors.Count > 0 ? null : MorphTargets.Of(targets);
    }

    /// <summary>Version 5's <c>uvs</c> and <c>seams</c> (T-53), each optional; every fault named by element.</summary>
    private static MeshUvs ReadUvs(Dictionary<string, JsonElement> f, string at, PolygonMesh mesh, List<MeshError> errors)
    {
        var faces = new List<KeyValuePair<int, IReadOnlyList<Vector2>?>>();
        if (f.TryGetValue("uvs", out var us) && List(us, $"{at}.uvs", errors) is { } list)
        {
            var seen = new HashSet<int>();
            var k = 0;
            foreach (var entry in list.EnumerateArray())
            {
                var e = Invariant($"{at}.uvs[{k++}]");
                if (Fields(entry, e, "a face's UVs", UvKeys, errors) is not { } fields) continue;
                var faceAt = Required(fields, e, "face", "a face id", errors);
                var uvAt = Required(fields, e, "uv", "a list of [u, v] pairs", errors);
                if (faceAt is not { } fe || Int(fe, $"{e}.face", "face id", errors) is not { } face) continue;
                if (face < 0 || face >= mesh.Faces.Count)
                {
                    errors.Add(new MeshError($"{e}.face", Invariant($"face {face} is not a face of the mesh, which has {mesh.Faces.Count}")));
                    continue;
                }
                if (mesh.Faces[face].IsDeleted)
                {
                    errors.Add(new MeshError($"{e}.face", Invariant($"face {face} is deleted, and a deleted face holds no UVs")));
                    continue;
                }
                if (!seen.Add(face))
                {
                    errors.Add(new MeshError($"{e}.face", Invariant($"face {face} is listed earlier; list each face once")));
                    continue;
                }
                if (uvAt is not { } ue || List(ue, $"{e}.uv", errors) is not { } corners) continue;
                if (corners.GetArrayLength() != mesh.Faces[face].Corners.Count)
                {
                    errors.Add(new MeshError($"{e}.uv", MeshUvs.CornerCountMessage(mesh.Faces[face], corners.GetArrayLength())));
                    continue;
                }
                var values = new Vector2[corners.GetArrayLength()];
                var good = true;
                var c = 0;
                foreach (var corner in corners.EnumerateArray())
                {
                    if (Floats(corner, Invariant($"{e}.uv[{c}]"), 2, "a UV is a pair of numbers, [u, v]", errors) is { } uv) values[c] = new Vector2(uv[0], uv[1]);
                    else good = false;
                    c++;
                }
                if (good) faces.Add(KeyValuePair.Create(face, (IReadOnlyList<Vector2>?)values));
            }
        }
        var seams = new List<(int, int)>();
        if (f.TryGetValue("seams", out var se) && List(se, $"{at}.seams", errors) is { } pairs)
        {
            var seen = new HashSet<(int, int)>();
            var i = 0;
            foreach (var entry in pairs.EnumerateArray())
            {
                var e = Invariant($"{at}.seams[{i++}]");
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() != 2)
                {
                    errors.Add(new MeshError(e, $"{Describe(entry)} is not an edge: write two vertex ids, [a, b]"));
                    continue;
                }
                var a = Int(entry[0], e + "[0]", "vertex id", errors);
                var b = Int(entry[1], e + "[1]", "vertex id", errors);
                if (a is not { } x || b is not { } y) continue;
                if (!mesh.Topology.TryGetEdge(x, y, out _)) errors.Add(new MeshError(e, Invariant($"{x}-{y} is not an edge of the mesh")));
                else if (!seen.Add(x < y ? (x, y) : (y, x))) errors.Add(new MeshError(e, Invariant($"edge {x}-{y} is listed earlier; list each edge once")));
                else seams.Add((x, y));
            }
        }
        return MeshUvs.None.WithFaces(faces).WithSeams(seams, true);
    }

    private static MeshShade? ReadShade(Dictionary<string, JsonElement> f, string at, PolygonMesh mesh, List<MeshError> errors)
    {
        var mode = ShadingMode.Flat;
        if (f.TryGetValue("shading", out var sh) && Str(sh, $"{at}.shading", errors) is { } text)
        {
            if (text == "smooth") mode = ShadingMode.Smooth;
            else if (text != "flat") errors.Add(new MeshError($"{at}.shading", $"'{text}' is not a shading: it is \"flat\" or \"smooth\""));
        }
        var hard = new List<(int, int)>();
        if (f.TryGetValue("hard_edges", out var he) && List(he, $"{at}.hard_edges", errors) is { } list)
        {
            var seen = new HashSet<(int, int)>();
            var i = 0;
            foreach (var entry in list.EnumerateArray())
            {
                var e = Invariant($"{at}.hard_edges[{i++}]");
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() != 2)
                {
                    errors.Add(new MeshError(e, $"{Describe(entry)} is not an edge: write two vertex ids, [a, b]"));
                    continue;
                }
                var a = Int(entry[0], e + "[0]", "vertex id", errors);
                var b = Int(entry[1], e + "[1]", "vertex id", errors);
                if (a is not { } x || b is not { } y) continue;
                if (!mesh.Topology.TryGetEdge(x, y, out _)) errors.Add(new MeshError(e, Invariant($"{x}-{y} is not an edge of the mesh")));
                else if (!seen.Add(x < y ? (x, y) : (y, x))) errors.Add(new MeshError(e, Invariant($"edge {x}-{y} is listed earlier; list each edge once")));
                else hard.Add((x, y));
            }
        }
        return MeshShade.Of(mode, hard);
    }

    /// <summary>The faces of a colour or group: at least one, each a face of the mesh.</summary>
    private static int[]? FaceList(Dictionary<string, JsonElement> f, string at, string what, PolygonMesh mesh, string none, List<MeshError> errors)
    {
        if (Required(f, at, "faces", what, errors) is not { } list || List(list, $"{at}.faces", errors) is not { } array) return null;
        var before = errors.Count;
        var ids = new List<int>();
        var k = 0;
        foreach (var x in array.EnumerateArray())
        {
            var e = Invariant($"{at}.faces[{k++}]");
            if (Int(x, e, "a face id", errors) is not { } id) continue;
            if (FaceStyle.FaceProblem(mesh, id) is { } bad) errors.Add(new MeshError(e, bad));
            else ids.Add(id);
        }
        if (k == 0) errors.Add(new MeshError($"{at}.faces", none));
        return errors.Count > before ? null : [.. ids];
    }

    private static Skeleton? ReadSkeleton(JsonElement element, List<MeshError> errors)
    {
        if (List(element, "skeleton", errors, "an array of bones, or null for none") is not { } array) return null;
        var before = errors.Count;
        var specs = new List<BoneSpec>();
        var i = 0;
        foreach (var item in array.EnumerateArray())
        {
            var at = Invariant($"skeleton[{i++}]");
            if (Fields(item, at, "a bone", BoneKeys, errors) is not { } f) continue;
            var name = Required(f, at, "name", "a bone", errors) is { } n ? Str(n, $"{at}.name", errors) : null;
            string? parent = null;
            if (f.TryGetValue("parent", out var p) && p.ValueKind != JsonValueKind.Null)
                parent = Str(p, $"{at}.parent", errors, "a bone name or null");
            var position = Required(f, at, "position", "a bone", errors) is { } pos ? Floats(pos, $"{at}.position", 3, "a position is [x, y, z]", errors) : null;
            var rotation = Required(f, at, "rotation", "a bone", errors) is { } rot ? Floats(rot, $"{at}.rotation", 4, "a rotation is [x, y, z, w]", errors) : null;
            if (name is null || position is null || rotation is null) continue;
            specs.Add(new BoneSpec(name, parent, new Vector3(position[0], position[1], position[2]),
                new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3])));
        }
        if (errors.Count > before) return null;

        var (skeleton, problems) = Skeleton.Create(specs);
        errors.AddRange(problems.Select(e => new MeshError(
            e.Element.StartsWith("bones", StringComparison.Ordinal) ? "skeleton" + e.Element["bones".Length..] : e.Element,
            e.Message.Replace("bones[", "skeleton[", StringComparison.Ordinal))));
        return skeleton;
    }

    private static Clip? ReadClip(JsonElement item, string at, Skeleton? skeleton, List<MeshError> errors)
    {
        var before = errors.Count;
        if (Fields(item, at, "a clip", ClipKeys, errors) is not { } f) return null;
        var name = Required(f, at, "name", "a clip", errors) is { } n ? Str(n, $"{at}.name", errors) : null;
        var duration = Required(f, at, "duration", "a clip", errors) is { } d ? Float(d, $"{at}.duration", errors) : null;
        var loop = false;
        if (f.TryGetValue("loop", out var l))
        {
            if (l.ValueKind is JsonValueKind.True or JsonValueKind.False) loop = l.GetBoolean();
            else errors.Add(new MeshError($"{at}.loop", $"loop is {Describe(l)}, not true or false"));
        }

        var specs = new List<BoneChannelsSpec>();
        if (Required(f, at, "channels", "a clip", errors) is { } c)
        {
            if (c.ValueKind != JsonValueKind.Object)
                errors.Add(new MeshError($"{at}.channels", $"channels is {Describe(c)}; it is an object of bone name to {{rotation, translation}}"));
            else
            {
                var good = true;
                foreach (var (bone, channels) in Pairs(c, $"{at}.channels", errors, ref good))
                {
                    var e = $"{at}.channels.{bone}";
                    if (Fields(channels, e, "a bone's channels", PartKeys, errors) is not { } pf) continue;
                    var rotation = pf.TryGetValue("rotation", out var r) ? ReadChannel(r, $"{e}.rotation", 4, "a rotation is [x, y, z, w]", v => new Quaternion(v[0], v[1], v[2], v[3]), errors) : null;
                    var translation = pf.TryGetValue("translation", out var t) ? ReadChannel(t, $"{e}.translation", 3, "a translation is [x, y, z]", v => new Vector3(v[0], v[1], v[2]), errors) : null;
                    specs.Add(new BoneChannelsSpec(bone, rotation, translation));
                }
            }
        }
        if (errors.Count > before || skeleton is null) return null;

        var (clip, problems) = Clip.Create(name!, duration!.Value, loop, specs, skeleton);
        errors.AddRange(problems.Select(e => new MeshError($"{at}.{e.Element}", e.Message)));
        return clip;
    }

    private static ChannelSpec<T>? ReadChannel<T>(JsonElement element, string at, int count, string shape, Func<float[], T> make, List<MeshError> errors)
    {
        var before = errors.Count;
        if (Fields(element, at, "a channel", ChannelKeys, errors) is not { } f) return null;
        string? interpolation = null;
        if (f.TryGetValue("interpolation", out var i)) interpolation = Str(i, $"{at}.interpolation", errors);
        var keys = new List<ClipKey<T>>();
        if (Required(f, at, "keys", "a channel", errors) is { } k && List(k, $"{at}.keys", errors) is { } array)
        {
            var n = 0;
            foreach (var key in array.EnumerateArray())
            {
                var e = Invariant($"{at}.keys[{n++}]");
                if (Fields(key, e, "a key", KeyKeys, errors) is not { } kf) continue;
                var time = Required(kf, e, "time", "a key", errors) is { } t ? Float(t, $"{e}.time", errors) : null;
                var value = Required(kf, e, "value", "a key", errors) is { } v ? Floats(v, $"{e}.value", count, shape, errors) : null;
                if (time is { } tt && value is not null) keys.Add(new ClipKey<T>(tt, make(value)));
            }
        }
        return errors.Count > before ? null : new ChannelSpec<T>(interpolation, keys);
    }

    // ---- Version 2's values ----------------------------------------------------------------------------------------

    /// <summary>
    /// An object's keys, each once and each one of <paramref name="keys"/>, or null with an error when it is not an
    /// object. An unknown, repeated or unreadable key is an error, and the rest are still returned.
    /// </summary>
    private static Dictionary<string, JsonElement>? Fields(JsonElement element, string at, string what, string[] keys, List<MeshError> errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new MeshError(at, $"{Describe(element)} is not {what}; {what} is an object with the keys {KeyList(keys)}"));
            return null;
        }
        var fields = new Dictionary<string, JsonElement>();
        var position = -1;
        foreach (var property in element.EnumerateObject())
        {
            position++;
            string key;
            try
            {
                key = property.Name;
            }
            catch (InvalidOperationException)
            {
                errors.Add(new MeshError(Join(at, Invariant($"keys[{position}]")), SurrogateKey));
                continue;
            }
            if (!keys.Contains(key)) errors.Add(new MeshError(Join(at, key), $"unknown key: {what} has only the keys {KeyList(keys)}"));
            else if (!fields.TryAdd(key, property.Value)) errors.Add(new MeshError(Join(at, key), "key appears more than once"));
        }
        return fields;
    }

    /// <summary>An object's keys and values in file order, for a map of names (bones); an unreadable key is an error.</summary>
    private static List<(string Key, JsonElement Value)> Pairs(JsonElement element, string at, List<MeshError> errors, ref bool good)
    {
        var pairs = new List<(string, JsonElement)>();
        var position = -1;
        foreach (var property in element.EnumerateObject())
        {
            position++;
            try
            {
                pairs.Add((property.Name, property.Value));
            }
            catch (InvalidOperationException)
            {
                errors.Add(new MeshError(Join(at, Invariant($"keys[{position}]")), SurrogateKey));
                good = false;
            }
        }
        return pairs;
    }

    private static JsonElement? Required(Dictionary<string, JsonElement> fields, string at, string key, string what, List<MeshError> errors)
    {
        if (fields.TryGetValue(key, out var value)) return value;
        errors.Add(new MeshError(Join(at, key), $"missing: {what} needs {key}"));
        return null;
    }

    private static JsonElement? List(JsonElement element, string at, List<MeshError> errors, string shape = "an array")
    {
        if (element.ValueKind == JsonValueKind.Array) return element;
        errors.Add(new MeshError(at, $"{Last(at)} is {Describe(element)}, not {shape}"));
        return null;
    }

    private static string? Str(JsonElement element, string at, List<MeshError> errors, string shape = "a string")
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            errors.Add(new MeshError(at, $"{Last(at)} is {Describe(element)}, not {shape}"));
            return null;
        }
        try
        {
            return element.GetString();
        }
        catch (InvalidOperationException)
        {
            errors.Add(new MeshError(at, "the text is not valid: it holds a lone surrogate escape (a \\ud800 to \\udfff with no partner)"));
            return null;
        }
    }

    private static int? Int(JsonElement element, string at, string what, List<MeshError> errors)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value)) return value;
        errors.Add(new MeshError(at, $"{Describe(element)} is not an integer {what}"));
        return null;
    }

    /// <summary>A number's own text parsed straight to float: rounded once, <c>-0</c> kept.</summary>
    private static float? Float(JsonElement element, string at, List<MeshError> errors)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            errors.Add(new MeshError(at, $"{Last(at)} is {Describe(element)}, not a number"));
            return null;
        }
        var raw = element.GetRawText();
        var value = float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (float.IsFinite(value)) return value;
        errors.Add(new MeshError(at, $"{raw} is not finite: it is outside the range of a float"));
        return null;
    }

    /// <summary>A number's own text parsed straight to double.</summary>
    private static double? Double(JsonElement element, string at, List<MeshError> errors)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            errors.Add(new MeshError(at, $"weight is {Describe(element)}, not a number"));
            return null;
        }
        var raw = element.GetRawText();
        var value = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (double.IsFinite(value)) return value;
        errors.Add(new MeshError(at, $"{raw} is not finite: it is outside the range of a double"));
        return null;
    }

    private static float[]? Floats(JsonElement element, string at, int count, string shape, List<MeshError> errors)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new MeshError(at, $"{Last(at)} is {Describe(element)}; {shape}"));
            return null;
        }
        if (element.GetArrayLength() != count)
        {
            errors.Add(new MeshError(at, Invariant($"{Last(at)} has {element.GetArrayLength()} numbers; {shape}")));
            return null;
        }
        var before = errors.Count;
        var result = new float[count];
        var k = 0;
        foreach (var x in element.EnumerateArray())
        {
            if (Float(x, Invariant($"{at}[{k}]"), errors) is { } value) result[k] = value;
            k++;
        }
        return errors.Count > before ? null : result;
    }

    private static string Join(string at, string key) => at.Length == 0 ? key : $"{at}.{key}";

    /// <summary>The last name in a path, for a message: <c>meshes[0].colours</c> gives <c>colours</c>.</summary>
    private static string Last(string at)
    {
        var name = at[(at.LastIndexOf('.') + 1)..];
        var bracket = name.IndexOf('[', StringComparison.Ordinal);
        return bracket > 0 && !name.EndsWith(']') ? name : bracket > 0 ? name[..bracket] + " item" : name;
    }

    private static string KeyList(string[] keys) =>
        string.Join(", ", keys[..^1].Select(k => $"\"{k}\"")) + $" and \"{keys[^1]}\"";

    // ---- Version 1's geometry, also a version 2 mesh's -------------------------------------------------------------

    /// <summary>The value of a key that holds an array, or null with an error when it is missing or something else.</summary>
    private static JsonElement? ArrayAt(string prefix, string owner, string key, JsonElement? element, List<MeshError> errors)
    {
        if (element is not { } array)
            errors.Add(new MeshError(prefix + key, $"missing: {owner} has no {key} array"));
        else if (array.ValueKind != JsonValueKind.Array)
            errors.Add(new MeshError(prefix + key, $"{key} is {Describe(array)}, not an array"));
        else
            return array;
        return null;
    }

    /// <summary>
    /// Reads <c>vertices</c>; from version 4 a <c>null</c> is a deleted vertex's tombstone (T-64), its id added to
    /// <paramref name="deleted"/> and its slot <see cref="Vector3.Zero"/>.
    /// </summary>
    private static Vector3[]? ReadVertices(string prefix, string owner, JsonElement? element, List<MeshError> errors, int version = 1,
        List<int>? deleted = null)
    {
        if (ArrayAt(prefix, owner, VerticesKey, element, errors) is not { } array)
            return null;

        var result = new Vector3[array.GetArrayLength()];
        var i = 0;
        foreach (var vertex in array.EnumerateArray())
        {
            var name = Invariant($"{prefix}vertices[{i}]");
            if (version >= 4 && deleted is not null && vertex.ValueKind == JsonValueKind.Null)
                deleted.Add(i);
            else if (vertex.ValueKind != JsonValueKind.Array)
                errors.Add(new MeshError(name, $"vertex is {Describe(vertex)}; a vertex is [x, y, z]"));
            else if (vertex.GetArrayLength() != 3)
                errors.Add(new MeshError(name, Invariant($"vertex has {vertex.GetArrayLength()} coordinates; a vertex is [x, y, z]")));
            else
            {
                var xyz = new float[3];
                var k = 0;
                foreach (var coordinate in vertex.EnumerateArray())
                {
                    var at = Invariant($"{prefix}vertices[{i}][{k}]");
                    if (coordinate.ValueKind != JsonValueKind.Number)
                        errors.Add(new MeshError(at, $"coordinate is {Describe(coordinate)}, not a number"));
                    else
                    {
                        // The token's own text, parsed straight to float: correctly rounded once, and -0 kept.
                        var raw = coordinate.GetRawText();
                        var value = float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (!float.IsFinite(value))
                            errors.Add(new MeshError(at, $"coordinate {raw} is not finite: it is outside the range of a float"));
                        xyz[k] = value;
                    }
                    k++;
                }
                result[i] = new Vector3(xyz[0], xyz[1], xyz[2]);
            }
            i++;
        }
        return result;
    }

    private static readonly string[] FaceKeys = ["loop", "holes"];

    private static Face[]? ReadFaces(string prefix, string owner, JsonElement? element, List<MeshError> errors, int version = 1)
    {
        if (ArrayAt(prefix, owner, FacesKey, element, errors) is not { } array)
            return null;

        var result = new Face[array.GetArrayLength()];
        var f = 0;
        foreach (var face in array.EnumerateArray())
        {
            var name = Invariant($"{prefix}faces[{f}]");
            if (version >= 4 && face.ValueKind == JsonValueKind.Null)
            {
                result[f++] = Face.Deleted;
                continue;
            }
            else if (version >= 4 && face.ValueKind == JsonValueKind.Object)
            {
                if (Fields(face, name, "a face with holes", FaceKeys, errors) is { } fields
                    && Required(fields, name, "loop", "a face with holes", errors) is { } loop
                    && Required(fields, name, "holes", "a face with holes", errors) is { } holes)
                {
                    var outer = ReadIds(loop, $"{name}.loop", "a loop", errors);
                    var holeLoops = new List<int[]>();
                    if (holes.ValueKind != JsonValueKind.Array)
                        errors.Add(new MeshError($"{name}.holes", $"holes is {Describe(holes)}, not an array of loops"));
                    else
                    {
                        var h = 0;
                        foreach (var hole in holes.EnumerateArray())
                            holeLoops.Add(ReadIds(hole, Invariant($"{name}.holes[{h++}]"), "a hole", errors) ?? []);
                    }
                    result[f++] = new Face(outer ?? [], holeLoops);
                    continue;
                }
            }
            else if (face.ValueKind != JsonValueKind.Array)
                errors.Add(new MeshError(name, version >= 4
                    ? $"face is {Describe(face)}; a face is an array of vertex ids or {{\"loop\": [...], \"holes\": [[...], ...]}}"
                    : $"face is {Describe(face)}; a face is an array of vertex ids"));
            else
            {
                result[f++] = new Face(ReadIds(face, name, "a face", errors)!);
                continue;
            }
            result[f++] = new Face([]);
        }
        return result;
    }

    /// <summary>A JSON array of integer vertex ids at <paramref name="name"/>, each bad one named <c>name[k]</c>; null when it is not an array.</summary>
    private static int[]? ReadIds(JsonElement element, string name, string what, List<MeshError> errors)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new MeshError(name, $"{what} is {Describe(element)}, not an array of vertex ids"));
            return null;
        }
        var loop = new int[element.GetArrayLength()];
        var k = 0;
        foreach (var index in element.EnumerateArray())
        {
            if (index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out loop[k]))
                errors.Add(new MeshError(Invariant($"{name}[{k}]"), $"index is {Describe(index)}, not an integer vertex id"));
            k++;
        }
        return loop;
    }

    private static string NotJsonMessage(JsonException e)
    {
        // The parser's own words, without its zero-based position suffix; the position is given one-based instead.
        var message = e.Message;
        var cut = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        if (cut >= 0) message = message[..cut];
        return e.LineNumber is { } line && e.BytePositionInLine is { } column
            ? Invariant($"the text is not JSON: at line {line + 1}, column {column + 1}: {message}")
            : $"the text is not JSON: {message}";
    }

    private static string Kind(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => "null",
    };

    /// <summary>A short account of a value for an error: a number or literal as written, a string, array or object by kind.</summary>
    private static string Describe(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => e.GetRawText(),
        JsonValueKind.String => "a string",
        JsonValueKind.Array => "an array",
        _ => "an object",
    };

    private static string Versions() =>
        "versions " + string.Join(", ", ReadableVersions.Take(ReadableVersions.Count - 1).Select(n => n.ToString(CultureInfo.InvariantCulture)))
        + " and " + ReadableVersions[^1].ToString(CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private static CarveFileResult Refused(params IReadOnlyList<MeshError> errors) => new(null, errors, []);
}
