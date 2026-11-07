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
    /// Reads a file tool's arguments, whose one argument is <c>path</c>: null with <paramref name="text"/> set, or the
    /// failed result naming every problem (an unknown argument, a missing or non-string path).
    /// </summary>
    public static CallToolResult? PathArguments(string tool, RequestContext<CallToolRequestParams> context, JsonElement? path, out string? text)
    {
        var errors = UnknownArguments(tool, context, "path");
        text = null;
        if (Absent(path)) errors.Add("path: required but missing");
        else if (path!.Value.ValueKind != JsonValueKind.String) errors.Add($"path: expected a string, got {Describe(path.Value)}");
        else text = path.Value.GetString();
        return errors.Count > 0 ? Failed(tool, errors) : null;
    }

    public static float[] Array(Vector3 v) => [v.X, v.Y, v.Z];

    /// <summary>The report fields every tool returns: vertices, faces, edges, closed, bounds, warnings.</summary>
    public static Dictionary<string, object> Report(PolygonMesh mesh)
    {
        var report = MeshReport.Create(mesh);
        return new Dictionary<string, object>
        {
            ["vertices"] = report.VertexCount,
            ["faces"] = report.FaceCount,
            ["edges"] = report.EdgeCount,
            ["closed"] = report.Closed,
            ["bounds"] = new { min = Array(report.BoundsMin), max = Array(report.BoundsMax) },
            ["warnings"] = report.Warnings.Select(w => w.ToString()).ToArray(),
        };
    }

    /// <summary>
    /// Runs an edit on the store's mesh and answers as every edit tool does: no mesh, the edit's refusal, or the report
    /// plus <c>created</c> and whatever <paramref name="addIds"/> adds.
    /// </summary>
    public static CallToolResult Edit(ModelStore store, string tool, Func<PolygonMesh, EditResult> edit,
        Action<Dictionary<string, object>, EditResult> addIds)
    {
        var snapshot = store.Edit(tool, edit, out var result);
        if (snapshot is null) return Error($"{tool}: " + NoMesh);
        if (!result!.Succeeded) return Failed(tool, result.Errors.Select(x => x.ToString()));

        var body = Report(snapshot.Mesh!);
        body["created"] = new { vertices = result.NewVertices, faces = result.NewFaces };
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
