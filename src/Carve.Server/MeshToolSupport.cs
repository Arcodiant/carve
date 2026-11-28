using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Carve.Server;

/// <summary>What the mesh tools share: the report and result JSON, tool errors, raw-argument reading and registration.</summary>
internal static class MeshToolSupport
{
    public const string NoMesh = "there is no mesh yet; call define_mesh first, or open to read a saved .carve file";

    /// <summary>Opens each example call in a tool's description; a test parses from here to the end of the line.</summary>
    public const string ExampleMarker = "Example: ";

    /// <summary>A result that is one block of plain text.</summary>
    public static CallToolResult Line(string text) =>
        new() { Content = [new TextContentBlock { Text = text }] };

    /// <summary>A result line's warnings: "; N warning(s): a; b", or nothing when there are none.</summary>
    public static string WarningsSuffix(IReadOnlyCollection<string> warnings) => warnings.Count > 0
        ? $"; {warnings.Count.ToString(CultureInfo.InvariantCulture)} warning(s): " + string.Join("; ", warnings)
        : "";

    public static CallToolResult Ok(object body) => Line(JsonSerializer.Serialize(body));

    public static CallToolResult Error(string text) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = text }] };

    public static CallToolResult Failed(string tool, IEnumerable<string> problems)
    {
        var list = problems.ToList();
        return Error($"{tool} failed with {list.Count} problem(s); the current mesh is unchanged:\n" + string.Join("\n", list));
    }

    /// <summary>The input schema of a file tool: <c>path</c>, required, and nothing else.</summary>
    public static JsonObject PathSchema(string description) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject
            {
                ["description"] = description,
                ["type"] = "string",
            },
        },
        ["required"] = new JsonArray("path"),
    };

    /// <summary>
    /// Reads the arguments of a file tool aimed at one mesh, <c>path</c> and the optional <c>mesh</c>: null with
    /// <paramref name="text"/> and <paramref name="meshName"/> set, or the failed result naming every problem.
    /// </summary>
    public static CallToolResult? PathAndMeshArguments(string tool, RequestContext<CallToolRequestParams> context, JsonElement? path, JsonElement? mesh,
        JsonElement? verbosity, out string? text, out string? meshName, out Verbosity level)
    {
        var errors = UnknownArguments(tool, context, "path", "mesh", "verbosity");
        level = ReadVerbosity(verbosity, errors);
        text = RequiredString("path", path, errors);
        meshName = MeshName(mesh, errors);
        return errors.Count > 0 ? Failed(tool, errors) : null;
    }

    /// <summary>A required string argument; null, with the problem added to <paramref name="errors"/>, when it is missing or not a string.</summary>
    public static string? RequiredString(string name, JsonElement? value, List<string> errors)
    {
        if (Absent(value))
        {
            errors.Add($"{name}: required but missing");
            return null;
        }

        if (value!.Value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"{name}: expected a string, got {Describe(value.Value)}");
            return null;
        }

        return value.Value.GetString();
    }

    /// <summary>The text of the <c>mesh</c> argument every tool aimed at one mesh takes.</summary>
    public const string MeshText =
        "The name of the mesh to work on, as given to define_mesh. May be left out when the model has one mesh; "
        + "left out while it has several, the call is a tool error that lists the names.";

    /// <summary>
    /// The sentence every edit tool and select share (T-93): in a part tree, <c>mesh</c> names a part with geometry, and
    /// coordinates are in that part's own space.
    /// </summary>
    public const string PartSpaceText = PartTexts.PartSpace;

    /// <summary>The sentence of the tools that take <c>mesh</c> but no coordinates (RV-71): in a part tree, <c>mesh</c> names a part with geometry.</summary>
    public const string PartTargetText = PartTexts.PartTarget;

    /// <summary>The schema of the <c>mesh</c> argument, added to <paramref name="properties"/>; returns the schema it was added to.</summary>
    public static JsonObject WithMesh(JsonObject? schema, string text = MeshText)
    {
        schema ??= new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
        schema["properties"]!.AsObject()["mesh"] = new JsonObject { ["description"] = text, ["type"] = "string" };
        return schema;
    }

    /// <summary>The optional <c>mesh</c> argument: null when absent; a problem when it is not a string or is empty.</summary>
    public static string? MeshName(JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return null;
        if (value!.Value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"mesh: expected a string, got {Describe(value.Value)}");
            return null;
        }
        var name = value.Value.GetString()!;
        if (Model.NameProblem(name) is { } problem)
        {
            errors.Add($"mesh: {problem}");
            return null;
        }
        return name;
    }

    /// <summary>
    /// The mesh a reading tool is aimed at in the current model, or the tool error to return: no mesh yet, or a name
    /// that picks none (an unknown name, or none given while there are several).
    /// </summary>
    public static PolygonMesh? Target(ModelStore store, string tool, string? name, out CallToolResult? error) =>
        Target(store.Current, tool, name, out error);

    /// <summary>As <see cref="Target(ModelStore, string, string?, out CallToolResult?)"/>, in a model already read.</summary>
    public static PolygonMesh? Target(Model current, string tool, string? name, out CallToolResult? error)
    {
        var target = current.Target(name, out var problem);
        error = problem is not null ? Failed(tool, [problem])
            : target is null ? Error($"{tool}: " + NoMesh)
            : null;
        return target?.Mesh;
    }

    /// <summary>As <see cref="Target(ModelStore, string, string?, out CallToolResult?)"/>, in a snapshot already read.</summary>
    public static PolygonMesh? Target(ModelSnapshot current, string tool, string? name, out CallToolResult? error) =>
        Target(current.Model, tool, name, out error);

    public static float[] Array(Vector3 v) => [v.X, v.Y, v.Z];

    /// <summary>The <c>deleted</c> field of every edit's report: the vertex and face ids the edit tombstoned, ascending.</summary>
    public static object Deleted(DeletedElements deleted) => new { vertices = deleted.Vertices, faces = deleted.Faces };

    /// <summary>
    /// The <c>split</c> field of every edit's report (T-65): one entry per face the edit split, <c>{face, pieces}</c>, its
    /// pieces ascending with the kept id (the face's own) first; empty for an edit that splits nothing.
    /// </summary>
    public static object[] Split(IReadOnlyList<SplitFace> split) =>
        [.. split.Select(s => new { face = s.Face, pieces = s.Pieces })];

    /// <summary><see cref="Split(IReadOnlyList{SplitFace})"/> with each face's pieces short at summary, as <see cref="Moved"/> (RV-100's ruling, 2026-11-27).</summary>
    public static object[] Split(IReadOnlyList<SplitFace> split, Verbosity level) =>
        [.. split.Select(s => new { face = s.Face, pieces = Moved(s.Pieces, level) })];

    /// <summary>The levels of the <c>verbosity</c> argument (M-23's ruling 3), read by <see cref="ReadVerbosity"/>.</summary>
    public enum Verbosity { Summary, Silent, Full }

    /// <summary>The schema description of <c>verbosity</c>, which the client does not cut; no tool description grows for it (T-200).</summary>
    public const string VerbositySchemaText =
        "How much the result says. \"summary\", the default: the result as always, a kind of warning with 4 or more listed as one line "
        + "(its count, the worst case, three samples and the select query that names them all). "
        + "\"full\": the same, with every warning one per line, and long id lists (moved, tessellate's split pieces), {count, first} with the first 10 ids at summary, whole. "
        + "\"silent\": only {\"ok\":true}, no counts, ids or warnings, and the change is made all the same (one undo step); "
        + "check answers {\"ok\":<its own ok>}, and export answers that JSON though its result is otherwise a line of text. "
        + "A tool error is returned whole at every level. Any other value is a tool error.";

    /// <summary>Adds the <c>verbosity</c> argument to <paramref name="schema"/> (an empty object schema when null); returns it.</summary>
    public static JsonObject WithVerbosity(JsonObject? schema)
    {
        schema ??= new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
        schema["properties"]!.AsObject()["verbosity"] = new JsonObject
        {
            ["description"] = VerbositySchemaText,
            ["type"] = "string",
            ["enum"] = new JsonArray("silent", "summary", "full"),
        };
        return schema;
    }

    /// <summary>The optional <c>verbosity</c> argument: summary when absent; anything but the three names is a problem.</summary>
    public static Verbosity ReadVerbosity(JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return Verbosity.Summary;
        switch (value!.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null)
        {
            case "silent": return Verbosity.Silent;
            case "summary": return Verbosity.Summary;
            case "full": return Verbosity.Full;
        }
        var got = value.Value.ValueKind == JsonValueKind.String ? JsonSerializer.Serialize(value.Value.GetString()) : Describe(value.Value);
        errors.Add($"verbosity: expected \"silent\", \"summary\" or \"full\", got {got}");
        return Verbosity.Summary;
    }

    /// <summary>A long id list (<c>moved</c>, tessellate's <c>split</c> pieces): the whole list at full, otherwise <c>{count, first}</c> with the first 10 ids.</summary>
    public static object Moved(IReadOnlyList<int> ids, Verbosity level) =>
        level == Verbosity.Full ? ids : new { count = ids.Count, first = ids.Take(10).ToArray() };

    /// <summary>A mesh's warnings as result lines: grouped by kind (T-197), or at <c>full</c> every warning one per line.</summary>
    public static IReadOnlyList<string> WarningLines(IReadOnlyList<MeshWarning> warnings, Verbosity level) =>
        level == Verbosity.Full ? [.. warnings.Select(w => w.ToString())] : MeshWarningGroups.Lines(warnings);

    /// <summary>What a success answers at <c>silent</c>.</summary>
    private const string SilentOk = """{"ok":true}""";

    /// <summary>The result at <paramref name="level"/>: at <c>silent</c> a success is <c>{"ok":true}</c>; a tool error is always whole.</summary>
    public static CallToolResult Quiet(CallToolResult result, Verbosity level) =>
        level == Verbosity.Silent && result.IsError != true ? Line(SilentOk) : result;

    /// <summary>The report fields every tool returns: vertices, faces, edges, closed, bounds, warnings.</summary>
    public static Dictionary<string, object> Report(PolygonMesh mesh, Verbosity level = Verbosity.Summary)
    {
        var report = MeshReport.Create(mesh);
        return new Dictionary<string, object>
        {
            ["vertices"] = report.VertexCount,
            ["faces"] = report.FaceCount,
            ["edges"] = report.EdgeCount,
            ["closed"] = report.Closed,
            ["bounds"] = new { min = Array(report.BoundsMin), max = Array(report.BoundsMax) },
            ["warnings"] = WarningLines(report.Warnings, level).ToArray(),
        };
    }

    /// <summary>
    /// Runs an edit on the store's mesh and answers as every edit tool does: no mesh, the edit's refusal, or the report
    /// plus <c>created</c> and whatever <paramref name="addIds"/> adds.
    /// </summary>
    public static CallToolResult Edit(ModelStore store, string tool, string? mesh, Func<PolygonMesh, EditResult> edit,
        Action<Dictionary<string, object>, EditResult> addIds, Verbosity level = Verbosity.Summary)
    {
        var snapshot = store.Edit(tool, mesh, edit, out var result, out var problem);
        if (problem is not null) return Failed(tool, [problem]);
        if (snapshot is null) return Error($"{tool}: " + NoMesh);
        if (!result!.Succeeded) return Failed(tool, result.Errors.Select(x => x.ToString()));

        if (level == Verbosity.Silent) return Line(SilentOk);
        var body = Report(snapshot.Model.Find(mesh ?? snapshot.Model.Meshes[0].Name)!, level);
        body["created"] = new { vertices = result.NewVertices, faces = result.NewFaces };
        body["deleted"] = Deleted(result.Deleted);
        body["split"] = Split(result.Split);
        addIds(body, result);
        return Ok(body);
    }

    /// <summary>
    /// Registers a tool whose arguments are bound raw, with a hand-written schema (an empty object when null, for a tool
    /// that takes no arguments). The schema is closed: <c>additionalProperties: false</c>.
    /// </summary>
    public static McpServerTool Create<T>(string name, string description, JsonObject? schema) where T : class
    {
        var tool = McpServerTool.Create(
            typeof(T).GetMethod("Run")!,
            request => ActivatorUtilities.CreateInstance<T>(request.Services!),
            new McpServerToolCreateOptions { Name = name, Description = description });
        schema ??= new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
        schema["additionalProperties"] = false;
        tool.ProtocolTool.InputSchema = JsonSerializer.SerializeToElement(schema);
        return tool;
    }

    /// <summary>The same schema with <c>additionalProperties: false</c>, for a tool whose schema the SDK generated.</summary>
    public static JsonElement Closed(JsonElement schema)
    {
        var node = JsonNode.Parse(schema.GetRawText())!.AsObject();
        node["additionalProperties"] = false;
        return JsonSerializer.SerializeToElement(node);
    }

    /// <summary>
    /// One problem for each argument the call sent that <paramref name="tool"/> does not take, in the order sent, so that a
    /// misspelt name is a tool error rather than an argument silently dropped. Names are matched exactly.
    /// </summary>
    public static List<string> UnknownArguments(string tool, RequestContext<CallToolRequestParams>? context, params string[] known)
    {
        var errors = new List<string>();
        foreach (var key in context?.Params?.Arguments?.Keys ?? [])
        {
            if (System.Array.IndexOf(known, key) >= 0) continue;
            errors.Add(known.Length == 0
                ? $"{key}: not an argument of {tool}, which takes none"
                : $"{key}: not an argument of {tool}; its arguments are {string.Join(", ", known)}");
        }
        return errors;
    }

    public static string Describe(JsonElement x) => x.ValueKind switch
    {
        JsonValueKind.Number => x.GetRawText(),
        JsonValueKind.String => "a string",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        _ => "null",
    };

    public static string Ordinal(int index)
    {
        var n = index + 1;
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return string.Create(CultureInfo.InvariantCulture, $"{n}{suffix}");
    }

    public static bool Absent(JsonElement? v) => v is null || v.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

    /// <summary>An optional boolean: false when absent; any other kind is a problem added to <paramref name="errors"/>.</summary>
    public static bool OptionalBool(string name, JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return false;
        if (value!.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.Value.GetBoolean();
        errors.Add($"{name}: expected true or false, got {Describe(value.Value)}");
        return false;
    }

    /// <summary>An optional string: null when absent; any other kind is a problem added to <paramref name="errors"/>.</summary>
    public static string? OptionalString(string name, JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return null;
        if (value!.Value.ValueKind == JsonValueKind.String) return value.Value.GetString();
        errors.Add($"{name}: expected a string, got {Describe(value.Value)}");
        return null;
    }

    /// <summary>Whether a JSON value is a number that is finite as a float.</summary>
    public static bool IsFinite(JsonElement n) => n.ValueKind == JsonValueKind.Number && float.IsFinite((float)n.GetDouble());

    /// <summary>The <c>group</c> problem when a mesh with <paramref name="style"/> has no face group named <paramref name="name"/>.</summary>
    public static string NoGroup(FaceStyle? style, string name) => style is null || style.Groups.Count == 0
        ? $"group: no group named '{name}'; the mesh has no groups yet, define one with define_group"
        : $"group: no group named '{name}'; the mesh's groups are {style.GroupNames}";

    /// <summary>
    /// The faces an edit given <c>faces</c> or <c>group</c> works on, read inside the store's lock on the mesh the edit is
    /// given: <paramref name="faces"/>, or the group's. An unknown group sets <paramref name="noGroup"/> and gives an empty
    /// list, so the Core refuses without trying the edit.
    /// </summary>
    public static List<int>? FacesOrGroup(PolygonMesh mesh, List<int>? faces, string? groupName, ref string? noGroup)
    {
        if (groupName is null) return faces;
        if (mesh.Style?.FacesOf(groupName) is { } grouped) return [.. grouped];
        noGroup = NoGroup(mesh.Style, groupName);
        return [];
    }

    /// <summary>An optional list of integer ids; null when absent.</summary>
    public static List<int>? Ids(string name, JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return null;
        if (value!.Value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{name}: expected an array of integer ids, got {Describe(value.Value)}");
            return null;
        }
        var list = new List<int>();
        var k = 0;
        foreach (var x in value.Value.EnumerateArray())
        {
            // Named by its place in the list, in words: {name}[k] would read as the id k.
            if (TryIndex(x, out var id)) list.Add(id);
            else errors.Add($"{name}: the {Ordinal(k)} listed entry: expected an integer id, got {Describe(x)}");
            k++;
        }
        return list;
    }

    /// <summary>An optional list of [a, b] vertex pairs; null when absent.</summary>
    public static List<(int A, int B)>? Pairs(string name, JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return null;
        if (value!.Value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{name}: expected an array of [a, b] pairs, got {Describe(value.Value)}");
            return null;
        }
        var list = new List<(int, int)>();
        var k = 0;
        foreach (var x in value.Value.EnumerateArray())
        {
            if (x.ValueKind == JsonValueKind.Array && x.GetArrayLength() == 2
                && TryIndex(x[0], out var a) && TryIndex(x[1], out var b)) list.Add((a, b));
            else errors.Add($"{name}: the {Ordinal(k)} listed entry: expected a pair [a, b] of integer vertex ids, got {Describe(x)}");
            k++;
        }
        return list;
    }

    /// <summary>A finite number; reports a missing one only when required.</summary>
    public static float? Number(string name, JsonElement? value, List<string> errors, bool required = true)
    {
        if (Absent(value))
        {
            if (required) errors.Add($"{name}: required but missing");
            return null;
        }
        if (value!.Value.ValueKind != JsonValueKind.Number)
        {
            errors.Add($"{name}: expected a number, got {Describe(value.Value)}");
            return null;
        }
        return Single(name, value.Value, errors);
    }

    /// <summary>A JSON number as a float, or null with an error when it lies beyond float range.</summary>
    private static float? Single(string element, JsonElement x, List<string> errors)
    {
        if (x.TryGetDouble(out var d) && float.IsFinite((float)d)) return (float)d;
        errors.Add($"{element}: {x.GetRawText()} is out of range: a number must lie within ±{float.MaxValue.ToString("G9", CultureInfo.InvariantCulture)}");
        return null;
    }

    /// <summary>One number of a set_uvs UV pair: a float, or null with an error naming <paramref name="element"/>.</summary>
    public static float? UvNumber(string element, JsonElement x, List<string> errors)
    {
        if (x.ValueKind != JsonValueKind.Number) { errors.Add($"{element}: expected a number, got {Describe(x)}"); return null; }
        if (x.TryGetDouble(out var d) && float.IsFinite((float)d)) return (float)d;
        errors.Add($"{element}: {x.GetRawText()} is not finite: a UV must lie within ±{float.MaxValue.ToString("G9", CultureInfo.InvariantCulture)}");
        return null;
    }

    /// <summary>One element of <c>into</c> (project_uvs and unwrap alike): a float, or null with the error Core words once.</summary>
    public static float? IntoNumber(string element, JsonElement x, List<string> errors)
    {
        if (x.ValueKind != JsonValueKind.Number) { errors.Add($"{element}: expected a number, got {Describe(x)}"); return null; }
        if (x.TryGetDouble(out var d) && float.IsFinite((float)d)) return (float)d;
        errors.Add(UvArguments.IntoElementRange(element, x.GetRawText()));
        return null;
    }

    /// <summary>A required [x, y, z].</summary>
    public static Vector3? Vec3(string name, JsonElement? value, List<string> errors)
    {
        if (Absent(value)) { errors.Add($"{name}: required but missing"); return null; }
        if (value!.Value.ValueKind != JsonValueKind.Array || value.Value.GetArrayLength() != 3)
        {
            errors.Add($"{name}: expected 3 numbers [x, y, z], got {(value.Value.ValueKind == JsonValueKind.Array ? value.Value.GetArrayLength() + " element(s)" : Describe(value.Value))}");
            return null;
        }
        var c = new float[3];
        var ok = true;
        for (var k = 0; k < 3; k++)
        {
            var x = value.Value[k];
            if (x.ValueKind != JsonValueKind.Number)
            {
                errors.Add($"{name}[{k}]: expected a number, got {Describe(x)}");
                ok = false;
            }
            else if (Single($"{name}[{k}]", x, errors) is { } f) c[k] = f;
            else ok = false;
        }
        return ok ? new Vector3(c[0], c[1], c[2]) : null;
    }

    /// <summary>
    /// A JSON integer that fits an int, judged from the number's own text so that no rounding to a double can make a
    /// fraction whole: a whole number written with a fraction or exponent (<c>1.0</c>, <c>1e0</c>) is accepted,
    /// <c>1.00000000000000001</c> and <c>1e-400</c> are not.
    /// </summary>
    public static bool TryIndex(JsonElement x, out int index)
    {
        index = 0;
        if (x.ValueKind != JsonValueKind.Number) return false;
        if (x.TryGetInt32(out index)) return true;

        var text = x.GetRawText();
        var p = 0;
        var negative = text[p] == '-';
        if (negative) p++;
        var digits = new System.Text.StringBuilder();
        var scale = 0L; // the value is digits * 10^scale
        for (; p < text.Length && char.IsAsciiDigit(text[p]); p++) digits.Append(text[p]);
        if (p < text.Length && text[p] == '.')
        {
            for (p++; p < text.Length && char.IsAsciiDigit(text[p]); p++) { digits.Append(text[p]); scale--; }
        }
        if (p < text.Length && (text[p] == 'e' || text[p] == 'E'))
        {
            var exponent = text[(p + 1)..];
            var sign = exponent.StartsWith('-') ? -1 : 1;
            if (!long.TryParse(exponent.TrimStart('+', '-'), NumberStyles.None, CultureInfo.InvariantCulture, out var e)) e = 1_000_000_000;
            scale += sign * Math.Min(e, 1_000_000_000);
        }

        var significant = digits.ToString().TrimStart('0');
        if (significant.Length == 0) return true; // zero, index stays 0

        var trimmed = significant.TrimEnd('0');
        scale += significant.Length - trimmed.Length;
        if (scale < 0) return false; // a nonzero fractional part
        if (trimmed.Length + scale > 10) return false;
        var value = System.Numerics.BigInteger.Parse(trimmed, CultureInfo.InvariantCulture) * System.Numerics.BigInteger.Pow(10, (int)scale);
        if (negative) value = -value;
        if (value < int.MinValue || value > int.MaxValue) return false;
        index = (int)value;
        return true;
    }
}
