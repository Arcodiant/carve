using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class DefineMeshTool(ModelStore store)
{
    /// <summary>Opens the worked example at the end of the description; a test parses from here.</summary>
    public const string ExampleMarker = "Worked example (a unit cube):";

    public const string CubeExample =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private const string Text =
        "Defines one mesh of the model: a polygon mesh, given by vertex coordinates and faces that index them, under a name (name, default \"mesh\"). "
        + "A model holds named meshes in the order they were first defined. A call with a name the model already has REPLACES that mesh entirely; a new name ADDS a mesh. "
        + "Either is one step for undo, and a call that fails leaves the model unchanged. "
        + "The other tools take mesh, the name of the mesh to work on, which may be left out while the model has one mesh; render draws every mesh together. "
        + "Coordinates are in metres (1 unit = 1 m, as export writes them) and Y is up. "
        + "Each face lists vertex indices (0-based) wound counter-clockwise when seen from outside the solid, "
        + "so the face normal points outward. A face may have any number of sides, three or more; it does not need to be a triangle. "
        + "A face has no holes; build an opening from several faces around it. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[]}; the geometry is not echoed back. "
        + "closed is true when every edge is shared by exactly two faces. Warnings (open edges, edges shared by more than two faces, "
        + "inconsistent winding, non-planar faces, unused vertices) mean the mesh was accepted but is probably not what you meant. "
        + "Bad input (a missing or out-of-range index, a face with fewer than 3 vertices, a repeated index, a zero-area face, "
        + "a self-intersecting or folded-back face outline, a non-finite coordinate, a vertex without exactly 3 numbers, an argument define_mesh does not take) is a tool error that lists every problem by element, e.g. faces[2][1]. "
        + "Vertex and face ids are the positions given here (vertex 0 is the first in vertices, face 0 the first in faces), and move and extrude keep them; get_mesh reads them back and undo steps back. "
        + TwoMeshMarker + " a 1 m crate called crate and a lid called lid on top of it, then the lid raised by 0.1 m; each line is a call:\n"
        + "1. define_mesh " + CrateExample + "\n"
        + "2. define_mesh " + LidExample + "\n"
        + "3. move " + LidMove + " (the lid alone moves; leaving mesh out here is a tool error that lists 'crate', 'lid')\n"
        + ExampleMarker + " " + CubeExample;

    /// <summary>Opens the two-mesh worked example; a test parses the calls after it.</summary>
    public const string TwoMeshMarker = "Worked example (two meshes):";

    public const string CrateExample =
        """{"name":"crate","vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    public const string LidExample =
        """{"name":"lid","vertices":[[-0.1,1,-0.1],[1.1,1,-0.1],[1.1,1.1,-0.1],[-0.1,1.1,-0.1],[-0.1,1,1.1],[1.1,1,1.1],[1.1,1.1,1.1],[-0.1,1.1,1.1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    public const string LidMove = """{"mesh":"lid","faces":[0,1,2,3,4,5],"by":[0,0.1,0]}""";

    private const string NameText =
        "The mesh's name, non-empty; default 'mesh'. An existing name replaces that mesh in its place; a new one adds a mesh after the others.";

    private const string VerticesText = "Vertex positions, each exactly three numbers [x, y, z]. In metres (1 unit = 1 m); Y is up.";
    private const string FacesText = "Faces, each a list of at least 3 indices into vertices, counter-clockwise seen from outside.";

    /// <summary>
    /// The tool, built by hand rather than found by <c>WithToolsFromAssembly</c> so the schema can say
    /// each vertex has exactly three numbers (the attribute route cannot add minItems/maxItems).
    /// The arguments are bound raw (<see cref="JsonElement"/>) so that <see cref="Run"/> validates them itself and
    /// every problem comes back as one tool error naming the element; the published schema is written out here
    /// and is what a typed <c>double[][]</c>, <c>int[][]</c> signature would have produced.
    /// </summary>
    public static McpServerTool Create() => MeshToolSupport.Create<DefineMeshTool>("define_mesh", Text, Schema());

    private static JsonObject Schema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject
            {
                ["description"] = NameText,
                ["type"] = "string",
            },
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

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? vertices = null, JsonElement? faces = null, JsonElement? name = null)
    {
        var errors = UnknownArguments("define_mesh", context, "name", "vertices", "faces");
        var meshName = DefinedName(name, errors);
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
        if (errors.Count > 0) return Failed("define_mesh", errors);

        var mesh = result.Mesh!;
        store.Define(meshName!, mesh, "define_mesh");
        return Ok(Report(mesh));
    }

    /// <summary>The name argument: the default when absent; null, with the problem added, when it is not a non-empty string.</summary>
    private static string? DefinedName(JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return Model.DefaultName;
        if (value!.Value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"name: expected a string, got {Describe(value.Value)}");
            return null;
        }
        var text = value.Value.GetString()!;
        if (Model.NameProblem(text) is { } problem)
        {
            errors.Add($"name: {problem}");
            return null;
        }
        return text;
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
}
