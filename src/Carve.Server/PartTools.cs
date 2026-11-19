using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The sentences the part tree (M-9, T-93) adds to existing tools' descriptions, each typed once here.</summary>
public static class PartTexts
{
    /// <summary>Every edit tool's and select's: what <c>mesh</c> names in a part tree, and the space its coordinates are in.</summary>
    public const string PartSpace =
        "In a part tree (define_part, get_parts), mesh names a part with geometry, and every coordinate here is in that part's own local space, before its translation, rotation and scale place it; an instance is refused, naming its source to use instead, and an empty part is refused as having no geometry. ";

    /// <summary>The same for the tools that take mesh but no coordinates (RV-71): mesh names a part with geometry.</summary>
    public const string PartTarget =
        "In a part tree (define_part, get_parts), mesh names a part with geometry; an instance is refused, naming its source to use instead, and an empty part is refused as having no geometry. ";

    /// <summary>define_mesh's.</summary>
    public const string DefineMesh =
        "In a part tree (define_part), name may be an existing part: an empty part gets this geometry and keeps its placement, a part with geometry has it replaced and keeps its placement and kept levels (keep_level), and an instance is refused, naming its source; a new name is a top-level part at the origin. ";

    /// <summary>check's.</summary>
    public const string Check =
        "Envelope warnings, after all of those and for the whole model, whichever mesh is checked, as get_parts gives them part by part: a part's geometry leaving its own envelope (define_part), or a child's envelope, placed in its parent, leaving the parent's, e.g. \"'Axle_F': its envelope, placed in 'Chassis', goes below the envelope of 'Chassis' on y by 0.3 m\"; move the part or widen the envelope with define_part. A model with no envelopes has none. ";
}

/// <summary>What the part tools share: reading their raw arguments and one part's entry as get_parts reports it.</summary>
internal static class PartToolSupport
{
    /// <summary>The arguments as sent, a JSON null kept as one (so <c>parent: null</c> differs from leaving parent out).</summary>
    public static IReadOnlyDictionary<string, JsonElement> Arguments(RequestContext<CallToolRequestParams> context) =>
        context.Params?.Arguments is { } given ? given.ToDictionary(p => p.Key, p => p.Value) : new Dictionary<string, JsonElement>();

    /// <summary>Exactly <paramref name="count"/> finite numbers, or null with the problem added.</summary>
    public static float[]? Numbers(string name, JsonElement value, int count, string shape, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != count)
        {
            errors.Add($"{name}: expected {count} numbers {shape}, got {(value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() + " element(s)" : Describe(value))}");
            return null;
        }
        var c = new float[count];
        var ok = true;
        for (var k = 0; k < count; k++)
        {
            var x = value[k];
            if (!IsFinite(x))
            {
                errors.Add($"{name}[{k}]: expected a finite number, got {Describe(x)}");
                ok = false;
            }
            else c[k] = (float)x.GetDouble();
        }
        return ok ? c : null;
    }

    public static Vector3? Vector(string name, JsonElement value, List<string> errors) =>
        Numbers(name, value, 3, "[x, y, z]", errors) is { } c ? new Vector3(c[0], c[1], c[2]) : null;

    /// <summary>One part as get_parts reports it.</summary>
    public static object Entry(Model model, Part part, IReadOnlyList<EnvelopeWarning> warnings)
    {
        var mesh = model.Shown(part);
        var report = mesh is null ? null : MeshReport.Create(mesh);
        var levels = model.FindPart(part.InstanceOf ?? part.Name)?.Levels.Select(l => l.Number).ToArray() ?? [];
        object? local = null, world = null;
        if (mesh is not null && mesh.LiveVertices.Any())
        {
            local = Box(mesh.LiveVertices.Select(v => mesh.Vertices[v]));
            var placed = model.WorldTransform(part.Name)!.Value;
            world = Box(mesh.LiveVertices.Select(v => Vector3.Transform(mesh.Vertices[v], placed)));
        }
        var q = part.Rotation;
        return new
        {
            name = part.Name,
            parent = part.Parent,
            translation = Array(part.Translation),
            rotation = new[] { q.X, q.Y, q.Z, q.W },
            scale = Array(part.Scale),
            envelope = part.Envelope is { } e ? new { min = Array(e.Min), max = Array(e.Max) } : null,
            instance_of = part.InstanceOf,
            moves = part.Moves,
            geometry = model.Find(part.Name) is not null,
            vertices = report?.VertexCount ?? 0,
            faces = report?.FaceCount ?? 0,
            levels,
            local_bounds = local,
            world_bounds = world,
            warnings = warnings.Where(w => w.Part == part.Name).Select(w => w.Message).ToArray(),
        };
    }

    private static object Box(IEnumerable<Vector3> points)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new { min = Array(min), max = Array(max) };
    }

    /// <summary>A Core problem's field as the tool's argument: a part's <c>name</c> is the argument <c>part</c>.</summary>
    public static string Argument(string field) => field == "name" ? "part" : field;
}

public sealed class DefinePartTool(ModelStore store)
{
    public const string Example =
        """{"part":"Wheel_FR","parent":"Axle_F","translation":[0,0,-0.72],"rotation":[0,1,0,0],"instance_of":"Wheel_FL","moves":true}""";

    public const string Text =
        "Places a part of the model's part tree, or changes one. Every named mesh is a part (a top-level part at the origin until placed); define_part also makes parts with no geometry of their own: "
        + "an empty part (a pivot or a group, such as an axle) or an instance, which shows the geometry and kept levels of the part it is instance_of, so editing that part changes every instance. "
        + "part is the name, unique across every part and mesh. A new name makes a part: an instance when instance_of is given, otherwise an empty part (define_mesh name=<part> later gives it geometry and keeps its placement). "
        + "An existing name changes only the arguments given and keeps the rest, its geometry and kept levels included. "
        + "parent is the part this one hangs under, or null to make it top-level; a part's transform is relative to its parent, so moving or turning a parent carries its whole subtree. "
        + "translation [x, y, z] is where the part's origin, its pivot, sits in its parent's space, in metres, Y up. rotation is a unit quaternion [x, y, z, w], as define_skeleton takes it (length 1 within 1e-4), turning about that pivot; [0, 1, 0, 0] is half a turn about +Y. "
        + "scale [x, y, z] is per axis, each > 0: a mirroring scale is refused, because the game's split load refuses a mirrored node. They apply scale first, then rotation, then translation. "
        + "envelope {min: [x, y, z], max: [x, y, z]} is the box the part and its whole subtree are meant to stay inside, in the part's own space, or null to clear it; leaving it is a warning in get_parts and check, never a refusal. "
        + "moves true marks a part that moves in a game, so the export keeps it as its own node; moves false merges it into its nearest moving ancestor. "
        + "Edits (move, extrude and the rest) take mesh=<part> and work in the part's own space, so placement never changes vertex or face ids. "
        + "Refused, as a tool error naming the argument, with the model unchanged: a parent that is not a part or would make the part its own ancestor, a rotation that is not a unit quaternion, a scale component not > 0, an envelope whose min is above its max, "
        + "instance_of on a part that owns geometry, naming a part with none or an instance, or changing an existing part's instance_of, and an argument define_part does not take. "
        + "There is no removing or renaming a part; undo removes a part just made. One step for undo. "
        + "Returns JSON {created, part}, created true for a new part and part its entry as get_parts gives it. "
        + "Worked example on the rover, the front right wheel as an instance of the front left one, turned half a turn so its hub faces outward:\n"
        + ExampleMarker + Example;

    public static McpServerTool Create() => MeshToolSupport.Create<DefinePartTool>("define_part", Text, Schema());

    private static JsonObject Vec(string description, int count) => new()
    {
        ["description"] = description,
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "number" },
        ["minItems"] = count,
        ["maxItems"] = count,
    };

    private static JsonObject Schema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["part"] = new JsonObject { ["description"] = "The part's name: an existing part to change, or a new name to make one.", ["type"] = "string" },
            ["parent"] = new JsonObject { ["description"] = "The part this one hangs under, or null for top-level. Left out, a new part is top-level and an existing one keeps its parent.", ["type"] = new JsonArray("string", "null") },
            ["translation"] = Vec("[x, y, z] in metres in the parent's space: where the part's origin (its pivot) sits. Default [0, 0, 0].", 3),
            ["rotation"] = Vec("A unit quaternion [x, y, z, w] in the parent's space, about the part's origin. Default [0, 0, 0, 1].", 4),
            ["scale"] = Vec("[x, y, z], each > 0. Default [1, 1, 1].", 3),
            ["envelope"] = new JsonObject
            {
                ["description"] = "{min, max} in the part's own space, covering its whole subtree, or null to clear it.",
                ["type"] = new JsonArray("object", "null"),
                ["properties"] = new JsonObject
                {
                    ["min"] = Vec("[x, y, z]", 3),
                    ["max"] = Vec("[x, y, z]", 3),
                },
                ["required"] = new JsonArray("min", "max"),
                ["additionalProperties"] = false,
            },
            ["instance_of"] = new JsonObject { ["description"] = "For a new part only: the part with geometry whose geometry and kept levels this one shows.", ["type"] = "string" },
            ["moves"] = new JsonObject { ["description"] = "Whether the part moves in a game, so the export keeps its own node. Default false.", ["type"] = "boolean" },
        },
        ["required"] = new JsonArray("part"),
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        const string tool = "define_part";
        var errors = UnknownArguments(tool, context, "part", "parent", "translation", "rotation", "scale", "envelope", "instance_of", "moves");
        var given = PartToolSupport.Arguments(context);
        string? name = null;
        if (!given.TryGetValue("part", out var p) || p.ValueKind == JsonValueKind.Null) errors.Add("part: required but missing");
        else if (p.ValueKind != JsonValueKind.String) errors.Add($"part: expected a string, got {Describe(p)}");
        else name = p.GetString();

        var setParent = given.TryGetValue("parent", out var parentArg);
        string? parent = null;
        if (setParent && parentArg.ValueKind == JsonValueKind.String) parent = parentArg.GetString();
        else if (setParent && parentArg.ValueKind != JsonValueKind.Null) errors.Add($"parent: expected a part name or null, got {Describe(parentArg)}");

        Vector3? translation = null, scale = null;
        Quaternion? rotation = null;
        if (given.TryGetValue("translation", out var t)) translation = PartToolSupport.Vector("translation", t, errors);
        if (given.TryGetValue("rotation", out var r) && PartToolSupport.Numbers("rotation", r, 4, "[x, y, z, w]", errors) is { } q)
            rotation = new Quaternion(q[0], q[1], q[2], q[3]);
        if (given.TryGetValue("scale", out var s)) scale = PartToolSupport.Vector("scale", s, errors);

        var setEnvelope = given.TryGetValue("envelope", out var e);
        PartEnvelope? envelope = null;
        if (setEnvelope && e.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in e.EnumerateObject().Select(x => x.Name).Where(k => k is not ("min" or "max")))
                errors.Add($"envelope.{key}: an envelope takes only min and max");
            Vector3? min = e.TryGetProperty("min", out var mn) ? PartToolSupport.Vector("envelope.min", mn, errors) : Missing("envelope.min", errors);
            Vector3? max = e.TryGetProperty("max", out var mx) ? PartToolSupport.Vector("envelope.max", mx, errors) : Missing("envelope.max", errors);
            if (min is { } a && max is { } b) envelope = new PartEnvelope(a, b);
        }
        else if (setEnvelope && e.ValueKind != JsonValueKind.Null) errors.Add($"envelope: expected {{min, max}} or null, got {Describe(e)}");

        string? instanceOf = null;
        if (given.TryGetValue("instance_of", out var i))
        {
            if (i.ValueKind == JsonValueKind.String) instanceOf = i.GetString();
            else errors.Add($"instance_of: expected a part name, got {Describe(i)}");
        }
        bool? moves = null;
        if (given.TryGetValue("moves", out var m))
        {
            if (m.ValueKind is JsonValueKind.True or JsonValueKind.False) moves = m.GetBoolean();
            else errors.Add($"moves: expected true or false, got {Describe(m)}");
        }
        if (errors.Count > 0) return Failed(tool, errors);

        var created = false;
        var snapshot = store.Change(tool, model =>
        {
            var existing = model.FindPart(name!);
            created = existing is null;
            var part = existing ?? new Part(name!) { InstanceOf = instanceOf };
            part = part with
            {
                // Core refuses a changed instance_of on an existing part (Model.PartProblems); the tool says how to make one.
                InstanceOf = instanceOf ?? part.InstanceOf,
                Parent = setParent ? parent : part.Parent,
                Translation = translation ?? part.Translation,
                Rotation = rotation ?? part.Rotation,
                Scale = scale ?? part.Scale,
                Envelope = setEnvelope ? envelope : part.Envelope,
                Moves = moves ?? part.Moves,
            };
            if (model.PartProblems(part) is [var (field, problem), ..])
                return (null, $"{PartToolSupport.Argument(field)}: {problem}"
                    + (field == "instance_of" && existing is { InstanceOf: null } && instanceOf is not null ? "; give a new name to make an instance" : ""));
            return (model.WithPart(part), null);
        }, out var refusal);
        if (snapshot is null) return Failed(tool, [refusal!]);
        var now = snapshot.Model;
        return Ok(new { created, part = PartToolSupport.Entry(now, now.FindPart(name!)!, now.EnvelopeWarnings()) });
    }

    private static Vector3? Missing(string name, List<string> errors)
    {
        errors.Add($"{name}: required but missing");
        return null;
    }
}

public sealed class GetPartsTool(ModelStore store)
{
    /// <summary>One entry of get_parts on the rover, as the worked example gives it; a test finds it in the real result.</summary>
    public const string ExampleEntry =
        """{"name":"Wheel_FR","parent":"Axle_F","translation":[0,0,-0.72],"rotation":[0,1,0,0],"scale":[1,1,1],"envelope":null,"instance_of":"Wheel_FL","moves":true,"geometry":false,"vertices":16,"faces":10,"levels":[0],"local_bounds":{"min":[-0.4,-0.4,-0.1],"max":[0.4,0.4,0.1]},"world_bounds":{"min":[0.29999998,0,-0.82000005],"max":[1.1,0.8,-0.62]},"warnings":[]}""";

    public const string Text =
        "Reads the model's part tree; changes nothing. Takes no arguments. "
        + "Returns JSON {parts:[...]}, every part in pre-order (each top-level part in the order made, each followed by its children, and theirs), and an empty list for an empty model. Each part is "
        + "{name, parent, translation, rotation, scale, envelope, instance_of, moves, geometry, vertices, faces, levels, local_bounds, world_bounds, warnings}: "
        + "parent null for a top-level part; translation, rotation [x, y, z, w] and scale as define_part takes them; envelope {min, max} in the part's own space, or null; instance_of the part whose geometry it shows, or null; "
        + "geometry true when the part owns geometry (an instance and an empty part do not); vertices and faces the counts of the geometry it shows (an instance's source's, 0 for an empty part); "
        + "levels the kept level numbers it shows, ascending (keep_level; an instance shows its source's); local_bounds {min, max} of that geometry's vertices in the part's own space, the space mesh=<part> edits work in, "
        + "and world_bounds {min, max} of the same vertices placed in the model by the part and every ancestor, both null for an empty part; "
        + "warnings the part's envelope warnings: its geometry leaving its own envelope, or its envelope, placed in its parent, leaving the parent's (warnings only, never refused; check gives the same ones). "
        + "world_bounds are placed in single precision, so 0.3 may read 0.29999998. "
        + "Worked example on the rover after keep_level 0, the entry of the front right wheel, an instance of Wheel_FL turned half a turn about +Y:\n"
        + "Output entry: " + ExampleEntry;

    public static McpServerTool Create() => MeshToolSupport.Create<GetPartsTool>("get_parts", Text, null);

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        var errors = UnknownArguments("get_parts", context);
        if (errors.Count > 0) return Failed("get_parts", errors);
        var model = store.Current.Model;
        var warnings = model.EnvelopeWarnings();
        return Ok(new { parts = model.Parts.Select(p => PartToolSupport.Entry(model, p, warnings)).ToArray() });
    }
}

public sealed class KeepLevelTool(ModelStore store)
{
    public const string Example = """{"level":0}""";

    public const string Text =
        "Keeps the live geometry of parts as a numbered level: a full snapshot, so a blockout (level 0) stays while the parts are refined, and level N later means each part's newest kept level at or below N. "
        + "level is a whole number, 0 or more. parts lists the parts to keep; left out, it is every part that owns geometry. "
        + "A level must be at least the part's newest kept level; the same number replaces that level with the live geometry, and a higher one adds a level. "
        + "Edits and define_mesh always change the live geometry, never a kept level. An instance shows its source's levels, and an empty part has none. "
        + "Refused, as a tool error naming each problem, with the model unchanged: an unknown part, an instance or an empty part (by name, an instance's refusal naming its source to keep a level of instead), a part named twice, a level below a part's newest kept level, a negative or fractional level, and an argument keep_level does not take. One step for undo. "
        + "Returns JSON {level, kept:[{part, levels}]}, one entry per part kept, in part-tree order, with every level number it now holds, ascending. "
        + "Worked example on the rover's blockout, keeping every part with geometry as level 0:\n"
        + ExampleMarker + Example
        + "\nOutput: {\"level\":0,\"kept\":[{\"part\":\"Chassis\",\"levels\":[0]},{\"part\":\"Cab\",\"levels\":[0]},{\"part\":\"Mast\",\"levels\":[0]},{\"part\":\"Lamp\",\"levels\":[0]},{\"part\":\"Wheel_FL\",\"levels\":[0]}]}";

    public static McpServerTool Create() => MeshToolSupport.Create<KeepLevelTool>("keep_level", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["level"] = new JsonObject { ["description"] = "The level number, a whole number 0 or more, at least each part's newest kept level.", ["type"] = "integer" },
            ["parts"] = new JsonObject
            {
                ["description"] = "The parts to keep, each owning geometry. Left out, every part that owns geometry.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
        },
        ["required"] = new JsonArray("level"),
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        const string tool = "keep_level";
        var errors = UnknownArguments(tool, context, "level", "parts");
        var given = PartToolSupport.Arguments(context);
        int level = 0;
        if (!given.TryGetValue("level", out var l) || l.ValueKind == JsonValueKind.Null) errors.Add("level: required but missing");
        else if (!TryIndex(l, out level)) errors.Add($"level: expected a whole number, got {Describe(l)}");
        else if (level < 0) errors.Add($"level: {level} is negative; a level number is 0 or more");
        List<string>? named = null;
        if (given.TryGetValue("parts", out var ps))
        {
            if (ps.ValueKind != JsonValueKind.Array) errors.Add($"parts: expected a list of part names, got {Describe(ps)}");
            else if (ps.GetArrayLength() == 0) errors.Add("parts: empty; name at least one part, or leave parts out to keep every part that owns geometry");
            else
            {
                named = [];
                var k = 0;
                foreach (var x in ps.EnumerateArray())
                {
                    if (x.ValueKind != JsonValueKind.String) errors.Add($"parts[{k}]: expected a part name, got {Describe(x)}");
                    else if (named.Contains(x.GetString()!)) errors.Add($"parts[{k}]: '{x.GetString()}' is named twice");
                    else named.Add(x.GetString()!);
                    k++;
                }
            }
        }
        if (errors.Count > 0) return Failed(tool, errors);

        string[] kept = [];
        var problems = new List<string>();
        var snapshot = store.Change(tool, model =>
        {
            if (model.IsEmpty && named is null) return (null, $"{tool}: {NoMesh}");
            var names = named ?? [.. model.Parts.Where(p => model.Find(p.Name) is not null).Select(p => p.Name)];
            var next = model;
            for (var k = 0; k < names.Count; k++)
            {
                var after = next.WithLevel(names[k], level, out var problem);
                if (after is null)
                    problems.Add(problem!.StartsWith("part: ", StringComparison.Ordinal) && named is not null ? $"parts[{k}]: " + problem["part: ".Length..] : problem!);
                else next = after;
            }
            if (problems.Count > 0) return (null, null);
            // Reported in part-tree order, whatever order parts was given in.
            kept = [.. next.Parts.Select(p => p.Name).Where(names.Contains)];
            return (next, null);
        }, out var refusal);
        if (snapshot is null) return refusal is { } bare && problems.Count == 0 ? Error(bare) : Failed(tool, problems.Count > 0 ? problems : [refusal!]);
        return Ok(new
        {
            level,
            kept = kept.Select(n => new { part = n, levels = snapshot.Model.FindPart(n)!.Levels.Select(x => x.Number).ToArray() }).ToArray(),
        });
    }
}
