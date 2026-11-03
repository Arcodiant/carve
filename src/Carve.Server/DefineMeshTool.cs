using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Carve.Server;

public sealed class DefineMeshTool(ModelStore store)
{
    /// <summary>Opens the worked example at the end of the description; a test parses from here.</summary>
    public const string ExampleMarker = "Worked example (a unit cube):";

    public const string CubeExample =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private const string Text =
        "Defines the model: one polygon mesh, given by vertex coordinates and faces that index them. "
        + "A call REPLACES the current mesh entirely; there is only one mesh, and a call that fails leaves it unchanged. "
        + "Coordinates are unitless and Y is up. "
        + "Each face lists vertex indices (0-based) wound counter-clockwise when seen from outside the solid, "
        + "so the face normal points outward. A face may have any number of sides, three or more; it does not need to be a triangle. "
        + "A face has no holes; build an opening from several faces around it. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[]}; the geometry is not echoed back. "
        + "closed is true when every edge is shared by exactly two faces. Warnings (open edges, edges shared by more than two faces, "
        + "inconsistent winding, non-planar faces, unused vertices) mean the mesh was accepted but is probably not what you meant. "
        + "Bad input (a missing or out-of-range index, a face with fewer than 3 vertices, a repeated index, a zero-area face, "
        + "a self-intersecting or folded-back face outline, a non-finite coordinate, a vertex without exactly 3 numbers) is a tool error that lists every problem by element, e.g. faces[2][1]. "
        + "Vertex and face ids are the positions given here (vertex 0 is the first in vertices, face 0 the first in faces), and move and extrude keep them; get_mesh reads them back and undo steps back. "
        + ExampleMarker + " " + CubeExample;

    private const string VerticesText = "Vertex positions, each exactly three numbers [x, y, z]. Unitless; Y is up.";
    private const string FacesText = "Faces, each a list of at least 3 indices into vertices, counter-clockwise seen from outside.";

    /// <summary>
    /// The tool, built by hand rather than found by <c>WithToolsFromAssembly</c> so the schema can say
    /// each vertex has exactly three numbers (the attribute route cannot add minItems/maxItems).
    /// The arguments are bound raw (<see cref="JsonElement"/>) so that <see cref="Run"/> validates them itself and
    /// every problem comes back as one tool error naming the element; the published schema is written out here
    /// and is what a typed <c>double[][]</c>, <c>int[][]</c> signature would have produced.
    /// </summary>
    public static McpServerTool Create()
    {
        var tool = McpServerTool.Create(
            typeof(DefineMeshTool).GetMethod(nameof(Run))!,
            request => new DefineMeshTool(request.Services!.GetRequiredService<ModelStore>()),
            new McpServerToolCreateOptions { Name = "define_mesh", Description = Text });
        tool.ProtocolTool.InputSchema = JsonSerializer.SerializeToElement(Schema());
        return tool;
    }

    private static JsonObject Schema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["vertices"] = new JsonObject
            {
                ["description"] = VerticesText,
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "number" },
                    ["minItems"] = 3,
                    ["maxItems"] = 3,
                },
            },
            ["faces"] = new JsonObject
            {
                ["description"] = FacesText,
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "integer" },
                },
            },
        },
        ["required"] = new JsonArray("vertices", "faces"),
    };

    public CallToolResult Run(JsonElement? vertices = null, JsonElement? faces = null)
    {
        var errors = new List<string>();
        var shapeBad = new HashSet<string>();
        var points = new List<Vector3>();
        var indices = new List<int[]>();

        var verticesOk = ArrayOrError("vertices", vertices ?? default, errors);
        if (verticesOk)
        {
            var i = 0;
            foreach (var v in vertices!.Value.EnumerateArray())
            {
                points.Add(ReadVertex(i, v, errors, shapeBad));
                i++;
            }
        }

        var facesOk = ArrayOrError("faces", faces ?? default, errors);
        if (facesOk)
        {
            var f = 0;
            foreach (var face in faces!.Value.EnumerateArray())
            {
                indices.Add(ReadFace(f, face, errors, shapeBad));
                f++;
            }
        }

        var result = PolygonMesh.Create(points, indices);
        // A missing or malformed argument is one problem; what Create then says about its empty stand-in is derived
        // (no faces; indices out of range of no vertices; a vertex nothing can refer to) and is left out.
        errors.AddRange(result.Errors
            .Where(e => !shapeBad.Contains(e.Element))
            .Where(e => facesOk || e.Element.StartsWith("vertices[", StringComparison.Ordinal))
            .Where(e => verticesOk || (e.Element.StartsWith("faces[", StringComparison.Ordinal) && !e.Message.Contains("out of range", StringComparison.Ordinal)))
            .Select(e => e.ToString()));
        if (errors.Count > 0)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"define_mesh failed with {errors.Count} problem(s); the current mesh is unchanged:\n" + string.Join("\n", errors) }],
            };
        }

        var mesh = result.Mesh!;
        store.Replace(mesh);
        var report = MeshReport.Create(mesh);
        var body = new
        {
            vertices = report.VertexCount,
            faces = report.FaceCount,
            edges = report.EdgeCount,
            closed = report.Closed,
            bounds = new { min = Array(report.BoundsMin), max = Array(report.BoundsMax) },
            warnings = report.Warnings.Select(w => w.ToString()).ToArray(),
        };
        return new CallToolResult { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(body) }] };
    }

    private static bool ArrayOrError(string name, JsonElement value, List<string> errors)
    {
        if (value.ValueKind == JsonValueKind.Array) return true;
        errors.Add(value.ValueKind == JsonValueKind.Undefined
            ? $"{name}: required but missing"
            : $"{name}: expected an array, got {Describe(value)}");
        return false;
    }

    private static Vector3 ReadVertex(int i, JsonElement v, List<string> errors, HashSet<string> shapeBad)
    {
        var element = $"vertices[{i}]";
        var bad = new Vector3(float.NaN);
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != 3)
        {
            errors.Add($"{element}: expected 3 numbers [x, y, z], got {(v.ValueKind == JsonValueKind.Array ? v.GetArrayLength().ToString(CultureInfo.InvariantCulture) + " element(s)" : Describe(v))}");
            shapeBad.Add(element);
            return bad;
        }

        Span<float> xyz = stackalloc float[3];
        var k = 0;
        var ok = true;
        foreach (var c in v.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Number)
            {
                errors.Add($"{element}[{k}]: expected a number, got {Describe(c)}");
                ok = false;
            }
            else
            {
                // A number beyond double range is not finite; Core names it.
                xyz[k] = c.TryGetDouble(out var d) ? (float)d : float.NaN;
            }
            k++;
        }

        if (ok) return new Vector3(xyz[0], xyz[1], xyz[2]);
        shapeBad.Add(element);
        return bad;
    }

    private static int[] ReadFace(int f, JsonElement face, List<string> errors, HashSet<string> shapeBad)
    {
        var element = $"faces[{f}]";
        if (face.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{element}: expected a list of vertex indices, got {Describe(face)}");
            shapeBad.Add(element);
            return [];
        }

        var result = new List<int>();
        var k = 0;
        foreach (var x in face.EnumerateArray())
        {
            if (TryIndex(x, out var index))
            {
                result.Add(index);
            }
            else
            {
                errors.Add($"{element}[{k}]: an index must be a JSON integer within 32-bit range, got {Describe(x)}");
                shapeBad.Add($"{element}[{k}]");
                result.Add(int.MinValue);
            }
            k++;
        }
        return result.ToArray();
    }

    /// <summary>
    /// A JSON integer that fits an int, judged from the number's own text so that no rounding to a double can make a
    /// fraction whole: a whole number written with a fraction or exponent (<c>1.0</c>, <c>1e0</c>) is accepted,
    /// <c>1.00000000000000001</c> and <c>1e-400</c> are not.
    /// </summary>
    private static bool TryIndex(JsonElement x, out int index)
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

    private static string Describe(JsonElement x) => x.ValueKind switch
    {
        JsonValueKind.Number => x.GetRawText(),
        JsonValueKind.String => "a string",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        _ => "null",
    };

    private static float[] Array(Vector3 v) => [v.X, v.Y, v.Z];
}
