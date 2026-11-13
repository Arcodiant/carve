using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>merge_faces</c> tool: <see cref="FaceMerge.Merge"/> on the store's mesh, reported as every edit is.</summary>
public sealed class MergeFacesTool(ModelStore store)
{
    /// <summary>The worked example's input, a unit cube split round its middle (y = 0.5) into ten faces, as <c>loop_cut</c> leaves it.</summary>
    public const string CutCube =
        """{"vertices":[[0,0,0],[1,0,0],[1,0,1],[0,0,1],[0,0.5,0],[1,0.5,0],[1,0.5,1],[0,0.5,1],[0,1,0],[1,1,0],[1,1,1],[0,1,1]],"""
        + "\"faces\":[[0,1,2,3],[8,11,10,9],[3,2,6,7],[7,6,10,11],[1,0,4,5],[5,4,8,9],[2,1,5,6],[6,5,9,10],[0,3,7,4],[4,7,11,8]]}";

    /// <summary>The line of a worked example that gives the define_mesh call to make first.</summary>
    public const string DefineMarker = "define_mesh input: ";

    public const string Text =
        "Merges connected faces of the current mesh into one face: the outline of their union, with a hole wherever the union encloses one. "
        + "The listed faces must be live and connected, each sharing at least one edge with another listed face, and wound alike (two of them running a shared edge the same way is mixed winding, refused). "
        + "The lowest listed id survives, reshaped; the others are deleted as tombstones, and so is every vertex of theirs that no live face uses afterwards (a vertex no face used before is kept). "
        + "Every vertex still on the outline stays, even where the outline runs straight through it: no vertex is dissolved. "
        + "The merged face keeps the surviving face's colour and groups; a merged face of another colour is a warning naming it, not an error. A hard edge mark on a dissolved edge goes with the edge. A non-planar result is a warning, as define_mesh's is. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[]}, deleted:{vertices:[ids], faces:[ids]}, split:[]}. "
        + "Bad input (fewer than two faces, an unknown, deleted or repeated id, faces not connected, mixed winding, a result that is not one face, such as faces touching at a vertex only or closing a shell) "
        + "is a tool error that lists every problem by element; the mesh is left unchanged and nothing is added to undo. One merge is one undo step. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to merge in. "
        + "Worked example: a unit cube split round its middle (y = 0.5), as loop_cut leaves it, merging the two pieces of its front (+Z, faces 2 and 3) back into face 2, deleted:{vertices:[], faces:[3]}:\n"
        + DefineMarker + CutCube + "\n"
        + ExampleMarker + """{"faces":[2,3]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<MergeFacesTool>("merge_faces", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["faces"] = new JsonObject
            {
                ["description"] = "Face ids to merge, at least two, each once; the lowest survives.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
                ["minItems"] = 2,
            },
        },
        ["required"] = new JsonArray("faces"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? faces = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("merge_faces", context, "faces", "mesh");
        var name = MeshName(mesh, errors);
        var unknown = errors.Count;
        var f = Ids("faces", faces, errors);
        if (f is null && errors.Count == unknown) errors.Add("faces: required but missing");
        if (errors.Count > 0) return Failed("merge_faces", errors);

        IReadOnlyList<string> warnings = [];
        return Edit(store, "merge_faces", name, m => FaceMerge.Merge(m, f!, out warnings),
            (body, _) => body["warnings"] = ((string[])body["warnings"]).Concat(warnings).ToArray());
    }
}
