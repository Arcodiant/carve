using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class SelectTool(ModelStore store)
{
    public const string Text =
        "Asks which vertices, edges or faces of the current mesh match an expression, and returns their ids in the shape the edit tools take. "
        + "It changes nothing: the mesh and undo stay as they were, and the server keeps no selection, so pass the ids on yourself. "
        + "kind is vertices, edges or faces. where is the expression. Both are required.\n"
        + "The grammar (keywords and names in any case, whitespace free, though two words still need a break: y==1and x<2 reads, y==1andx<2 does not):\n"
        + "expr := or\n"
        + "or := and ( 'or' and )*\n"
        + "and := not ( 'and' not )*\n"
        + "not := 'not' not | atom\n"
        + "atom := '(' expr ')' | 'all' | 'none' | flag | comparison\n"
        + "comparison := property op number (the property on the left, the number on the right)\n"
        + "op := '<' | '<=' | '==' | '!=' | '>=' | '>'\n"
        + "property := name | name '.' ('x' | 'y' | 'z')\n"
        + "number := a decimal, with an optional sign and exponent, e.g. 2, -0.5, 1e-3\n"
        + "not binds tighter than and, which binds tighter than or: 'a or b and c' is 'a or (b and c)' and 'not a and b' is '(not a) and b'. "
        + "all matches every element and none matches nothing.\n"
        + "On every kind x, y and z are the element's representative point: a vertex's position, an edge's midpoint, a face's centre (the mean of its corners). "
        + "min.x, max.x (and y, z) are the extent of the element's own vertices; for a vertex they equal its position.\n"
        + "vertices have the numbers id, x, y, z, min.*, max.*, faces (how many faces use it), edges (how many edges meet at it) "
        + "and the flags unattached (faces == 0) and boundary (it is on an open edge).\n"
        + "edges have the numbers x, y, z, min.*, max.*, a and b (the lower and the higher vertex id), length, faces (how many faces use it) "
        + "and the flags open (faces == 1), manifold (exactly two faces, crossing it in opposite directions) and nonmanifold (faces > 2). "
        + "An edge has no id of its own: it is named by its two vertices.\n"
        + "faces have the numbers id, x, y, z, min.*, max.*, normal.x, normal.y, normal.z (the unit normal, by Newell's method), vertices (the corner count), area "
        + "and the flags non_planar (exactly the faces the not-planar warning names) and open (it has an open edge).\n"
        + "Comparisons are made in float, both sides: the number is narrowed to a float first, as define_mesh narrows a coordinate, so y == 1.02 matches a vertex given as 1.02. "
        + "There is no tolerance: == is exact, so for a value that was computed, bracket it (y > 0.99 and y < 1.01).\n"
        + "Returns JSON {kind, vertices|edges|faces, count, of}: the list is named for the kind, ids ascending; edges are [lo, hi] pairs ordered by lo, then hi; "
        + "count is the list's length and of is how many elements of that kind the mesh has. A query matching nothing is a success with count 0, not an error.\n"
        + "The lists are exactly what the edit tools take: move takes vertices, edges and faces, and extrude takes faces. "
        + "Ids never change, so a selection stays valid until an edit removes or reshapes what it names (and undo frees the ids its step created). "
        + "A selection is the answer at the time of the call: after an edit, ask again rather than assume the same elements still match.\n"
        + "Tool errors leave the mesh unchanged and list every problem that can be found: no mesh yet (call define_mesh or open first); "
        + "an unknown kind (use vertices, edges or faces); a missing or non-string kind or where; an argument select does not take; and these, each after the prefix where and a colon:\n"
        + "where is empty\n"
        + "at character 11: '>>' is not an operator; use <, <=, ==, !=, >= or >\n"
        + "at character 1: 'centre' is not a property of faces; faces have id, x, y, z, min, max, normal, vertices, area, non_planar, open (naming the kind asked for, and that kind's list)\n"
        + "at character 8: 'normal.w' is not an axis; use .x, .y or .z\n"
        + "at character 1: 'min' needs an axis; use .x, .y or .z\n"
        + "at character 5: a flag cannot be compared with a number\n"
        + "at character 1: 'area' is a number; compare it with <, <=, ==, !=, >= or >\n"
        + "at character 14: expected a number\n"
        + "at character 5: 1e99 is out of range: a number must lie within ±3.40282347E+38\n"
        + "at character 20: expected ')'\n"
        + "at character 7: expected a property, a flag, all, none, not or '('\n"
        + "at character 7: expected 'and', 'or' or the end of the expression\n"
        + "at character 201: nested too deeply; 'not' and '(' go 200 levels deep at most\n"
        + "Character positions count from 1 and point at the thing at fault: the name, the axis letter, the operator, or where a token was expected (one past the end when the text stops short).\n"
        + "Needs a mesh, from define_mesh or open, first. Worked examples on define_mesh's unit cube.\n"
        + "The top face:\n"
        + ExampleMarker + """{"kind":"faces","where":"normal.y > 0.9"}""" + "\n"
        + ResultMarker + """{"kind":"faces","faces":[3],"count":1,"of":6}""" + "\n"
        + "The two top vertices on x = 0:\n"
        + ExampleMarker + """{"kind":"vertices","where":"y == 1 and x < 0.5"}""" + "\n"
        + ResultMarker + """{"kind":"vertices","vertices":[3,7],"count":2,"of":8}""" + "\n"
        + "The open edges, of which the closed cube has none:\n"
        + ExampleMarker + """{"kind":"edges","where":"open"}""" + "\n"
        + ResultMarker + """{"kind":"edges","edges":[],"count":0,"of":12}""" + "\n"
        + "Select then move, as two calls. First select the top face:\n"
        + ExampleMarker + """{"kind":"faces","where":"normal.y > 0.9"}""" + "\n"
        + ResultMarker + """{"kind":"faces","faces":[3],"count":1,"of":6}""" + "\n"
        + "then call move with the ids from that result, its faces list as it stands:\n"
        + ExampleMarker + """{"faces":[3],"by":[0,0.5,0]}""";

    /// <summary>Starts the line after an example that says what the call returns; the tests read these lines.</summary>
    public const string ResultMarker = "Result: ";

    public static McpServerTool Create() => MeshToolSupport.Create<SelectTool>("select", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["kind"] = new JsonObject
            {
                ["description"] = "What to select: vertices, edges or faces.",
                ["type"] = "string",
                ["enum"] = new JsonArray("vertices", "edges", "faces"),
            },
            ["where"] = new JsonObject
            {
                ["description"] = "The expression, e.g. \"normal.y > 0.9\", \"unattached\" or \"y >= 10 and not open\".",
                ["type"] = "string",
            },
        },
        ["required"] = new JsonArray("kind", "where"),
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? kind = null, JsonElement? where = null)
    {
        var errors = UnknownArguments("select", context, "kind", "where");
        var elements = Kind(kind, errors);
        var query = StringArgument("where", where, errors) is { } text ? MeshQuery.Parse(text) : null;
        if (query is not null)
        {
            var problems = elements is { } known ? query.ErrorsFor(known) : query.Errors;
            errors.AddRange(problems.Select(e => e.ToString()));
        }
        if (errors.Count > 0) return Failed("select", errors);

        var mesh = store.Current.Mesh;
        if (mesh is null) return Error("select: " + NoMesh);

        var selection = MeshQuery.Select(mesh, elements!.Value, query!.Expression!);
        var name = Name(selection.Kind);
        return Ok(new Dictionary<string, object>
        {
            ["kind"] = name,
            [name] = selection.Kind == ElementKind.Edges ? selection.Edges.Select(e => new[] { e.Lo, e.Hi }).ToArray() : selection.Ids,
            ["count"] = selection.Count,
            ["of"] = selection.Of,
        });
    }

    private static string Name(ElementKind kind) => kind switch
    {
        ElementKind.Vertices => "vertices",
        ElementKind.Edges => "edges",
        _ => "faces",
    };

    private static string? StringArgument(string name, JsonElement? value, List<string> errors)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            errors.Add($"{name}: required but missing");
            return null;
        }
        if (value.Value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"{name}: expected a string, got {Describe(value.Value)}");
            return null;
        }
        return value.Value.GetString();
    }

    private static ElementKind? Kind(JsonElement? value, List<string> errors)
    {
        var text = StringArgument("kind", value, errors);
        switch (text)
        {
            case null: return null;
            case "vertices": return ElementKind.Vertices;
            case "edges": return ElementKind.Edges;
            case "faces": return ElementKind.Faces;
            default:
                errors.Add($"kind: '{text}' is not a kind; use vertices, edges or faces");
                return null;
        }
    }
}
