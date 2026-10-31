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
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[]}; the geometry is not echoed back. "
        + "closed is true when every edge is shared by exactly two faces. Warnings (open edges, edges shared by more than two faces, "
        + "inconsistent winding, non-planar faces, unused vertices) mean the mesh was accepted but is probably not what you meant. "
        + "Bad input (a missing or out-of-range index, a face with fewer than 3 vertices, a repeated index, a zero-area face, "
        + "a vertex without exactly 3 numbers) is a tool error that lists every problem by element, e.g. faces[2][1]. "
        + ExampleMarker + " " + CubeExample;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>
    /// The tool, built by hand rather than found by <c>WithToolsFromAssembly</c> so the schema can say
    /// each vertex has exactly three numbers (the attribute route cannot add minItems/maxItems).
    /// </summary>
    public static McpServerTool Create() => McpServerTool.Create(
        typeof(DefineMeshTool).GetMethod(nameof(Run))!,
        request => new DefineMeshTool(request.Services!.GetRequiredService<ModelStore>()),
        new McpServerToolCreateOptions
        {
            Name = "define_mesh",
            Description = Text,
            SchemaCreateOptions = new AIJsonSchemaCreateOptions
            {
                TransformSchemaNode = (context, node) =>
                {
                    // An array of numbers is a vertex (the only one in this tool's arguments).
                    if (node is JsonObject vertex
                        && IsType(vertex["type"], "array")
                        && IsType(vertex["items"]?["type"], "number"))
                    {
                        vertex["minItems"] = 3;
                        vertex["maxItems"] = 3;
                    }

                    return node;
                },
            },
        });

    public CallToolResult Run(
        [Description("Vertex positions, each exactly three numbers [x, y, z]. Unitless; Y is up.")] double[][] vertices,
        [Description("Faces, each a list of at least 3 indices into vertices, counter-clockwise seen from outside.")] int[][] faces)
    {
        var errors = new List<string>();
        var shapeBad = new HashSet<string>();
        var points = new List<Vector3>();

        for (var i = 0; i < (vertices.Length); i++)
        {
            var v = vertices[i];
            if (v is null || v.Length != 3)
            {
                var element = $"vertices[{i}]";
                errors.Add($"{element}: expected 3 numbers [x, y, z], got {(v is null ? "null" : v.Length.ToString(CultureInfo.InvariantCulture))}");
                shapeBad.Add(element);
                points.Add(new Vector3(float.NaN));
            }
            else
            {
                points.Add(new Vector3((float)v[0], (float)v[1], (float)v[2]));
            }
        }

        var indices = new List<int[]>();
        for (var f = 0; f < (faces.Length); f++)
        {
            if (faces[f] is null)
            {
                errors.Add($"faces[{f}]: expected a list of vertex indices, got null");
                indices.Add([]);
            }
            else
            {
                indices.Add(faces[f]);
            }
        }

        var result = PolygonMesh.Create(points, indices);
        errors.AddRange(result.Errors.Where(e => !shapeBad.Contains(e.Element)).Select(e => e.ToString()));
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
        return new CallToolResult { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(body, Json) }] };
    }

    private static bool IsType(JsonNode? node, string type) =>
        node is JsonValue value && value.TryGetValue<string>(out var found) && found == type;

    private static float[] Array(Vector3 v) => [v.X, v.Y, v.Z];
}
