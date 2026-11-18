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
        + "Replacing a mesh that has morph targets (see define_target) is refused, naming them, because the new mesh would lose them; give drop_targets: true to replace it and drop them anyway. "
        + "The other tools take mesh, the name of the mesh to work on, which may be left out while the model has one mesh; render draws every mesh together. "
        + "Coordinates are in metres (1 unit = 1 m, as export writes them) and Y is up. "
        + "Each face lists vertex indices (0-based) wound counter-clockwise when seen from outside the solid, "
        + "so the face normal points outward. A face may have any number of sides, three or more; it does not need to be a triangle. "
        + "A face may have holes: give it as {\"loop\": [...], \"holes\": [[...], ...]} instead of a plain list. "
        + "loop is the outer outline, counter-clockwise seen from outside as for any face; each hole is a list of 3 or more vertex indices "
        + "wound the other way, CLOCKWISE seen from outside, lying strictly inside the loop (not touching it) and apart from the other holes, "
        + "and no vertex is used twice in one face. A hole is an opening: the face is the loop less its holes, and nothing is drawn in a hole unless another face fills it. "
        + "Its edges are edges like any other: a hole no face fills shows as open edges. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], deleted:{vertices:[], faces:[]}, split:[]}, deleted and split always empty and no created, as nothing was there before; the geometry is not echoed back. "
        + "closed is true when every edge is shared by exactly two faces. Warnings (open edges, edges shared by more than two faces, "
        + "inconsistent winding, non-planar faces, unused vertices) mean the mesh was accepted but is probably not what you meant. "
        + "Bad input (a missing or out-of-range index, a face with fewer than 3 vertices, a repeated index, a zero-area face, "
        + "a self-intersecting or folded-back face outline, a non-finite coordinate, a vertex without exactly 3 numbers, an argument define_mesh does not take) is a tool error that lists every problem by element, e.g. faces[2][1]. "
        + "Vertex and face ids are the positions given here (vertex 0 is the first in vertices, face 0 the first in faces), and move and extrude keep them; get_mesh reads them back and undo steps back. "
        + TwoMeshMarker + " a 1 m crate called crate and a lid called lid on top of it, then the lid raised by 0.1 m; each line is a call:\n"
        + "1. define_mesh " + CrateExample + "\n"
        + "2. define_mesh " + LidExample + "\n"
        + "3. move " + LidMove + " (the lid alone moves; leaving mesh out here is a tool error that lists 'crate', 'lid')\n"
        + HoleMarker + " a 2 x 2 plate facing +Z with a 1 x 1 square hole in its middle, one face: " + PlateExample + "\n"
        + ExampleMarker + " " + CubeExample;

    /// <summary>Opens the holed-face worked example; a test parses the call after it, up to the end of its line.</summary>
    public const string HoleMarker = "Worked example (a face with a hole):";

    public const string PlateExample =
        """{"vertices":[[-1,-1,0],[1,-1,0],[1,1,0],[-1,1,0],[-0.5,-0.5,0],[-0.5,0.5,0],[0.5,0.5,0],[0.5,-0.5,0]],"faces":[{"loop":[0,1,2,3],"holes":[[4,5,6,7]]}]}""";

    /// <summary>Opens the two-mesh worked example; a test parses the calls after it.</summary>
    public const string TwoMeshMarker = "Worked example (two meshes):";

    public const string CrateExample =
        """{"name":"crate","vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    public const string LidExample =
        """{"name":"lid","vertices":[[-0.1,1,-0.1],[1.1,1,-0.1],[1.1,1.1,-0.1],[-0.1,1.1,-0.1],[-0.1,1,1.1],[1.1,1,1.1],[1.1,1.1,1.1],[-0.1,1.1,1.1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    public const string LidMove = """{"mesh":"lid","faces":[0,1,2,3,4,5],"by":[0,0.1,0]}""";

    private const string NameText =
        "The name of the mesh, non-empty and without a colon (a skinned export names materials mesh:group, so such a name is refused); left out, it is mesh. An existing name replaces that mesh in its place; a new one adds a mesh after the others.";

    private const string DropTargetsText = "Default false. Replacing a mesh that has morph targets is refused unless this is true, which drops the targets with the old mesh. Has no effect on a new name or a mesh with no targets.";

    private const string VerticesText = "Vertex positions, each exactly three numbers [x, y, z]. In metres (1 unit = 1 m); Y is up.";
    private const string FacesText = "Faces, each a list of at least 3 indices into vertices, counter-clockwise seen from outside; "
        + "or, for a face with holes, {loop: [...], holes: [[...], ...]}: the outer loop counter-clockwise and each hole clockwise, seen from outside.";

    private static JsonObject IndexList() => new()
    {
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "integer" },
    };

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
                    ["anyOf"] = new JsonArray(
                        IndexList(),
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["loop"] = IndexList(),
                                ["holes"] = new JsonObject { ["type"] = "array", ["items"] = IndexList() },
                            },
                            ["required"] = new JsonArray("loop", "holes"),
                            ["additionalProperties"] = false,
                        }),
                },
            },
            ["drop_targets"] = new JsonObject
            {
                ["description"] = DropTargetsText,
                ["type"] = "boolean",
            },
        },
        ["required"] = new JsonArray("vertices", "faces"),
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? vertices = null, JsonElement? faces = null, JsonElement? name = null, JsonElement? drop_targets = null)
    {
        var errors = UnknownArguments("define_mesh", context, "name", "vertices", "faces", "drop_targets");
        var dropTargets = OptionalBool("drop_targets", drop_targets, errors);
        var meshName = DefinedName(name, errors);
        var shapeBad = new HashSet<string>();
        var points = new List<Vector3>();
        var indices = new List<Face>();

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
        // The refusal is made under the store's write lock, with the push, so a define_target landing between them is not dropped unasked.
        if (store.Define(meshName!, mesh, "define_mesh",
                model => !dropTargets && model.Find(meshName!)?.Targets is { } targets
                    ? $"name: the mesh '{meshName}' has the morph targets {targets.ListNames()}, which replacing it would drop; give drop_targets: true to replace it anyway, or a new name to add a mesh"
                    : null,
                out var refusal) is null)
            return Failed("define_mesh", [refusal!]);
        var body = Report(mesh);
        body["deleted"] = Deleted(DeletedElements.None);
        body["split"] = Split([]);
        return Ok(body);
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

    /// <summary>A face: a plain list of indices, or the object form {"loop": [...], "holes": [[...], ...]}.</summary>
    private static Face ReadFace(int f, JsonElement face, List<string> errors, HashSet<string> shapeBad)
    {
        var element = $"faces[{f}]";
        if (face.ValueKind == JsonValueKind.Array) return new Face(ReadLoop(element, face, errors, shapeBad));
        if (face.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{element}: expected a list of vertex indices or {{\"loop\", \"holes\"}}, got {Describe(face)}");
            shapeBad.Add(element);
            return new Face([]);
        }

        var ok = true;
        foreach (var p in face.EnumerateObject())
        {
            if (p.Name is "loop" or "holes") continue;
            errors.Add($"{element}.{p.Name}: a face takes only loop and holes");
            ok = false;
        }
        int[] loop = [];
        if (!face.TryGetProperty("loop", out var l) || l.ValueKind != JsonValueKind.Array)
        {
            errors.Add(l.ValueKind == JsonValueKind.Undefined
                ? $"{element}.loop: required but missing"
                : $"{element}.loop: expected a list of vertex indices, got {Describe(l)}");
            ok = false;
        }
        else loop = ReadLoop(element, l, errors, shapeBad);

        var holes = new List<int[]>();
        if (!face.TryGetProperty("holes", out var hs) || hs.ValueKind != JsonValueKind.Array)
        {
            errors.Add(hs.ValueKind == JsonValueKind.Undefined
                ? $"{element}.holes: required but missing (a face with no holes may be a plain list)"
                : $"{element}.holes: expected a list of holes, got {Describe(hs)}");
            ok = false;
        }
        else
        {
            var k = 0;
            foreach (var h in hs.EnumerateArray())
            {
                var name = Face.LoopName(f, k);
                if (h.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"{name}: expected a list of vertex indices, got {Describe(h)}");
                    ok = false;
                }
                else holes.Add(ReadLoop(name, h, errors, shapeBad));
                k++;
            }
        }

        if (ok) return new Face(loop, holes);
        // The shape is wrong: what Create says about the stand-in is derived, so the whole face is left out.
        shapeBad.Add(element);
        return new Face([]);
    }

    private static int[] ReadLoop(string element, JsonElement face, List<string> errors, HashSet<string> shapeBad)
    {
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
