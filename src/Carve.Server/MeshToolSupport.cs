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
    public const string NoMesh = "there is no mesh yet; call define_mesh first";

    /// <summary>Opens each example call in a tool's description; a test parses from here to the end of the line.</summary>
    public const string ExampleMarker = "Example: ";

    public static CallToolResult Ok(object body) =>
        new() { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(body) }] };

    public static CallToolResult Error(string text) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = text }] };

    public static CallToolResult Failed(string tool, IEnumerable<string> problems)
    {
        var list = problems.ToList();
        return Error($"{tool} failed with {list.Count} problem(s); the current mesh is unchanged:\n" + string.Join("\n", list));
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

    /// <summary>Registers a tool whose arguments are bound raw, with a hand-written schema (or the default one when null).</summary>
    public static McpServerTool Create<T>(string name, string description, JsonNode? schema) where T : class
    {
        var tool = McpServerTool.Create(
            typeof(T).GetMethod("Run")!,
            request => ActivatorUtilities.CreateInstance<T>(request.Services!),
            new McpServerToolCreateOptions { Name = name, Description = description });
        if (schema is not null) tool.ProtocolTool.InputSchema = JsonSerializer.SerializeToElement(schema);
        return tool;
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

    private static bool Absent(JsonElement? v) => v is null || v.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

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
            if (x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out var id)) list.Add(id);
            else errors.Add($"{name}[{k}]: expected an integer id, got {Describe(x)}");
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
                && x[0].ValueKind == JsonValueKind.Number && x[0].TryGetInt32(out var a)
                && x[1].ValueKind == JsonValueKind.Number && x[1].TryGetInt32(out var b)) list.Add((a, b));
            else errors.Add($"{name}[{k}]: expected a pair [a, b] of integer vertex ids, got {Describe(x)}");
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
        if (value!.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetDouble(out var d))
        {
            errors.Add($"{name}: expected a number, got {Describe(value.Value)}");
            return null;
        }
        return (float)d;
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
            if (x.ValueKind != JsonValueKind.Number || !x.TryGetDouble(out var d))
            {
                errors.Add($"{name}[{k}]: expected a number, got {Describe(x)}");
                ok = false;
            }
            else c[k] = (float)d;
        }
        return ok ? new Vector3(c[0], c[1], c[2]) : null;
    }
}
