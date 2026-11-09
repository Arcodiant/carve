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

    /// <summary>The version the file was read as, 1 or 2, or 0 when the file was refused.</summary>
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
/// <para><see cref="Write"/> always gives version 2, byte for byte: the keys <c>carve</c>, <c>meshes</c>, <c>skeleton</c>
/// and <c>clips</c> in that order; in a mesh <c>name</c>, <c>vertices</c>, <c>faces</c>, <c>colours</c>, <c>groups</c> and
/// <c>weights</c>; two-space indent, one vertex, face, colour, group, weighted vertex, bone or clip key per line, <c>\n</c>
/// line ends and a newline at the end. Each float is the shortest text that reads back to the same <see cref="float"/>,
/// each weight the shortest that reads back to the same <see cref="double"/>.</para>
/// <para>Reading takes version 1 (one mesh, named <see cref="Model.DefaultName"/>) and version 2, keys in any order and
/// any whitespace, and holds the file to exactly what the tools accept: geometry to <see cref="PolygonMesh.Create"/>, bones
/// to <see cref="Skeleton.Create"/>, weights to <see cref="VertexWeights.Raw"/>, clips to <see cref="Clip.Create"/>, the
/// whole to <see cref="Model.Create"/>. An unknown key or another version is refused, so a newer file is never half-read.
/// <c>"meshes": []</c> is read only beside a skeleton: a clip library, a skeleton and clips with no mesh (the person,
/// 2026-11-09); with no skeleton it is refused.</para>
/// </remarks>
public static class CarveFile
{
    /// <summary>The version <see cref="Write"/> writes.</summary>
    public const int Version = 2;

    /// <summary>The versions <see cref="Read"/> reads.</summary>
    public static IReadOnlyList<int> ReadableVersions { get; } = [1, 2];

    private const string VersionKey = "carve";
    private const string VerticesKey = "vertices";
    private const string FacesKey = "faces";

    private static readonly string[] FileKeys = [VersionKey, "meshes", "skeleton", "clips"];
    private static readonly string[] MeshKeys = ["name", VerticesKey, FacesKey, "colours", "groups", "weights"];
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

    /// <summary>The file's text for a model, version 2. The same model always gives the same text.</summary>
    public static string Write(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var w = new Writer();
        w.Line(0, "{");
        w.Line(1, $"\"{VersionKey}\": {Version.ToString(CultureInfo.InvariantCulture)},");
        w.Open(1, "\"meshes\": [", model.Meshes.Count == 0, ",");
        for (var i = 0; i < model.Meshes.Count; i++)
        {
            WriteMesh(w, model.Meshes[i], model.Skeleton);
            w.Line(2, i < model.Meshes.Count - 1 ? "}," : "}");
        }
        w.Close(1, "],", model.Meshes.Count == 0);

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

    private static void WriteMesh(Writer w, NamedMesh named, Skeleton? skeleton)
    {
        var (name, mesh) = named;
        w.Line(2, "{");
        w.Line(3, $"\"name\": {Text(name)},");
        w.Line(3, $"\"{VerticesKey}\": [");
        w.Items(4, mesh.Vertices.Count, i => Vector(mesh.Vertices[i]));
        w.Line(3, "],");
        w.Line(3, $"\"{FacesKey}\": [");
        w.Items(4, mesh.Faces.Count, i => Ids(mesh.Faces[i]));
        w.Line(3, "],");

        var style = mesh.Style ?? FaceStyle.None;
        var colours = style.Colours.GroupBy(c => c.Value, StringComparer.Ordinal)
            .Select(g => (Colour: g.Key, Faces: g.Select(c => c.Key).Order().ToArray()))
            .OrderBy(c => c.Faces[0]).ToArray();
        w.List(3, "colours", colours.Length, i => $"{{\"colour\": {Text(colours[i].Colour)}, \"faces\": {Ids(colours[i].Faces)}}}", ",");
        var groups = style.Groups;
        w.List(3, "groups", groups.Count, i => $"{{\"name\": {Text(groups[i].Name)}, \"faces\": {Ids(groups[i].Faces)}}}", ",");

        // A weighted mesh is only ever in a model with a skeleton (Model.Create, ModelStore.SetWeights).
        var weights = mesh.Weights ?? VertexWeights.None;
        var vertices = weights.Vertices.ToArray();
        w.List(3, "weights", vertices.Length, i =>
        {
            var bones = string.Join(", ", weights.Of(vertices[i]).Select(x => $"{Text(skeleton!.Bones[x.Bone].Name)}: {Weight(x.Weight)}"));
            return $"{{\"vertex\": {vertices[i].ToString(CultureInfo.InvariantCulture)}, \"bones\": {{{bones}}}}}";
        }, "");
    }

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

    private static string Ids(IEnumerable<int> ids) => "[" + string.Join(", ", ids.Select(i => i.ToString(CultureInfo.InvariantCulture))) + "]";

    /// <summary>A JSON string: only <c>"</c>, <c>\</c> and control characters escaped, so names read as written.</summary>
    private static string Text(string s) => JsonSerializer.Serialize(s, StringOptions);

    /// <summary>The shortest text that reads back to the same float; <c>-0</c> stays <c>-0</c>.</summary>
    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>The shortest text that reads back to the same double.</summary>
    private static string Weight(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    // ---- Reading ---------------------------------------------------------------------------------------------------

    /// <summary>Reads a file's text into a model, or every error that stops one, each named by where it is in the file.</summary>
    public static CarveFileResult Read(string text)
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
            return VersionOf(root) == 2 ? ReadVersion2(root) : ReadVersion1(root);
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
    private static CarveFileResult ReadVersion2(JsonElement root)
    {
        var errors = new List<MeshError>();
        var fields = Fields(root, "", "a version 2 Carve file", FileKeys, errors)!;

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
        if (!fields.TryGetValue("meshes", out var m)) errors.Add(new MeshError("meshes", "missing: the file has no meshes array"));
        else if (m.ValueKind != JsonValueKind.Array) errors.Add(new MeshError("meshes", $"meshes is {Describe(m)}, not an array"));
        else if (m.GetArrayLength() == 0)
        {
            // A skeleton and clips with no mesh, a clip library, is a whole file (the person, 2026-11-09).
            if (!fields.TryGetValue("skeleton", out var rig) || rig.ValueKind == JsonValueKind.Null)
                errors.Add(new MeshError("meshes", "the file has no meshes; a Carve file holds at least one"));
        }
        else
        {
            var i = 0;
            foreach (var item in m.EnumerateArray())
            {
                if (ReadMesh(item, Invariant($"meshes[{i}]"), skeleton, skeletonGood, errors) is { } mesh) meshes.Add(mesh);
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

        if (errors.Count > 0) return Refused(errors);
        var (model, problems) = Model.Create(meshes, skeleton, clips);
        if (model is null) return Refused(problems);
        return new CarveFileResult(model, [], model.Meshes.SelectMany(x => MeshReport.Create(x.Mesh).Warnings).ToArray(), 2);
    }

    private static NamedMesh? ReadMesh(JsonElement item, string at, Skeleton? skeleton, bool skeletonGood, List<MeshError> errors)
    {
        var before = errors.Count;
        if (Fields(item, at, "a mesh", MeshKeys, errors) is not { } f) return null;
        var name = Required(f, at, "name", "a mesh", errors) is { } n ? Str(n, $"{at}.name", errors) : null;
        f.TryGetValue(VerticesKey, out var vs);
        f.TryGetValue(FacesKey, out var fs);
        var verts = ReadVertices(at + ".", "the mesh", f.ContainsKey(VerticesKey) ? vs : null, errors);
        var faces = ReadFaces(at + ".", "the mesh", f.ContainsKey(FacesKey) ? fs : null, errors);
        if (errors.Count > before) return null;

        var created = PolygonMesh.Create(verts!, faces!);
        if (!created.Succeeded)
        {
            errors.AddRange(created.Errors.Select(e => new MeshError($"{at}.{e.Element}", e.Message)));
            return null;
        }
        var mesh = created.Mesh!;

        var style = FaceStyle.None;
        if (f.TryGetValue("colours", out var colours) && List(colours, $"{at}.colours", errors) is { } cl)
        {
            var coloured = new Dictionary<int, string>();
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
                    if (VertexWeights.VertexProblem(mesh.Vertices.Count, id) is { } bad) errors.Add(new MeshError($"{e}.vertex", bad));
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

        if (errors.Count > before || name is null) return null;
        return new NamedMesh(name, mesh.WithStyle(style).WithWeights(VertexWeights.Of(weights)));
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
            if (FaceStyle.FaceProblem(mesh.Faces.Count, id) is { } bad) errors.Add(new MeshError(e, bad));
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

    private static Vector3[]? ReadVertices(string prefix, string owner, JsonElement? element, List<MeshError> errors)
    {
        if (ArrayAt(prefix, owner, VerticesKey, element, errors) is not { } array)
            return null;

        var result = new Vector3[array.GetArrayLength()];
        var i = 0;
        foreach (var vertex in array.EnumerateArray())
        {
            var name = Invariant($"{prefix}vertices[{i}]");
            if (vertex.ValueKind != JsonValueKind.Array)
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

    private static int[][]? ReadFaces(string prefix, string owner, JsonElement? element, List<MeshError> errors)
    {
        if (ArrayAt(prefix, owner, FacesKey, element, errors) is not { } array)
            return null;

        var result = new int[array.GetArrayLength()][];
        var f = 0;
        foreach (var face in array.EnumerateArray())
        {
            var name = Invariant($"{prefix}faces[{f}]");
            if (face.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new MeshError(name, $"face is {Describe(face)}; a face is an array of vertex ids"));
                result[f++] = [];
                continue;
            }

            var loop = new int[face.GetArrayLength()];
            var k = 0;
            foreach (var index in face.EnumerateArray())
            {
                if (index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out loop[k]))
                    errors.Add(new MeshError(Invariant($"{prefix}faces[{f}][{k}]"), $"index is {Describe(index)}, not an integer vertex id"));
                k++;
            }
            result[f++] = loop;
        }
        return result;
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
