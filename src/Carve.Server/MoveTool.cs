using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class MoveTool(ModelStore store)
{
    public const string Text =
        "Moves parts of the current mesh by an offset. Give any mix of vertices, edges and faces; every vertex they touch moves once, "
        + "even when it is reached twice (a face and one of its edges). "
        + "Ids are the ones get_mesh reports, and they never change: vertices and faces are integers, positions in define_mesh's lists; "
        + "an edge is a [a, b] pair of vertex ids (either order) and must be an edge of the mesh. A pair that is not an edge of the mesh is refused, and the error names the pair. select with kind edges lists the real edges. "
        + "by is exactly three numbers [dx, dy, dz], in the same coordinates as define_mesh (1 unit = 1 m), Y up; a positive dy moves up. "
        + "At least one of vertices, edges, faces is required, and by must not be zero. "
        + "Moving adds and removes nothing, so the counts stay the same, but moving some corners of a face off its plane gives a non-planar warning (past 3 faces, one line: the count, the worst, three lowest ids and select faces where non_planar). "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[]}, deleted:{vertices:[], faces:[]}, split:[], moved:[vertex ids moved, ascending]}; "
        + "created, deleted and split are always empty for a move. "
        + "Bad input (an unknown vertex or face id, a pair that is not an edge, nothing listed, a zero or non-finite by, an argument move does not take) is a tool error "
        + "that lists every problem by element, e.g. faces[6]; the mesh is left unchanged and nothing is added to undo. "
        + "So is a move after which a face would cross itself (a bow-tie), which the call checks as a whole, not corner by corner: "
        + "when reshaping a face by its corners, move whole edges or sets of corners that keep their order around the face, rather than one corner at a time past its neighbour. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to move. "
        + PartSpaceText
        + "Worked example on define_mesh's unit cube, raising the top face (face 3, y = 1) by half a unit:\n"
        + ExampleMarker + """{"faces":[3],"by":[0,0.5,0]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<MoveTool>("move", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["vertices"] = new JsonObject
            {
                ["description"] = "Vertex ids to move.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
            },
            ["edges"] = new JsonObject
            {
                ["description"] = "Edges to move, each a [a, b] pair of vertex ids joined by an edge; both ends move.",
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "integer" },
                    ["minItems"] = 2,
                    ["maxItems"] = 2,
                },
            },
            ["faces"] = new JsonObject
            {
                ["description"] = "Face ids to move; every corner moves.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
            },
            ["by"] = new JsonObject
            {
                ["description"] = "The offset [dx, dy, dz], exactly three numbers. Y is up.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "number" },
                ["minItems"] = 3,
                ["maxItems"] = 3,
            },
        },
        ["required"] = new JsonArray("by"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? vertices = null, JsonElement? edges = null, JsonElement? faces = null, JsonElement? by = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("move", context, "vertices", "edges", "faces", "by", "mesh");
        var name = MeshName(mesh, errors);
        var v = Ids("vertices", vertices, errors);
        var e = Pairs("edges", edges, errors);
        var f = Ids("faces", faces, errors);
        var offset = Vec3("by", by, errors);
        if (errors.Count > 0) return Failed("move", errors);

        return Edit(store, "move", name, mesh => MeshEdits.Move(mesh, v, e, f, offset!.Value),
            (body, result) => body["moved"] = result.MovedVertices);
    }
}
