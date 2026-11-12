using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>delete</c> tool: <see cref="MeshDelete.Delete"/> on the store's mesh, reported as every edit is.</summary>
public sealed class DeleteTool(ModelStore store)
{
    public const string Text =
        "Deletes vertices and faces of the current mesh. Deleting a vertex deletes every face that uses it (in its outer loop or in a hole: a face is deleted whole, never reshaped); "
        + "deleting a face also deletes every vertex of that face that no live face uses afterwards. A vertex no face used before the delete is kept unless you list it. "
        + "Ids never change and are never reused: a deleted vertex or face keeps its slot as a tombstone, null in get_mesh and in the saved file, and new elements are still appended after the last slot. "
        + "Counts in every report are live counts; render, select, check and export skip tombstones, and export writes the live elements only. "
        + "Naming a deleted id in any later edit is a tool error saying it was deleted, e.g. faces[3]: deleted. undo restores what a delete removed. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[]}, deleted:{vertices:[ids], faces:[ids]}}, everything removed listed in deleted, ids ascending. "
        + "Bad input (no vertices and no faces, an unknown or already deleted id, a delete that would take every face, since a mesh keeps at least one face as define_mesh requires, an argument delete does not take) "
        + "is a tool error that lists every problem by element; the mesh is left unchanged and nothing is added to undo. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to delete from. "
        + "Worked example on define_mesh's unit cube: deleting the top face (face 3, y = 1) leaves an open box, deleted:{vertices:[], faces:[3]}:\n"
        + ExampleMarker + """{"faces":[3]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<DeleteTool>("delete", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["vertices"] = new JsonObject
            {
                ["description"] = "Vertex ids to delete; each takes every face that uses it.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
            },
            ["faces"] = new JsonObject
            {
                ["description"] = "Face ids to delete; each takes its vertices that no live face uses afterwards.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
            },
        },
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? vertices = null, JsonElement? faces = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("delete", context, "vertices", "faces", "mesh");
        var name = MeshName(mesh, errors);
        var v = Ids("vertices", vertices, errors);
        var f = Ids("faces", faces, errors);
        if (errors.Count > 0) return Failed("delete", errors);

        return Edit(store, "delete", name, m => MeshDelete.Delete(m, v, f), (_, _) => { });
    }
}
