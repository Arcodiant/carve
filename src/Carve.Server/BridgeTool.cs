using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>bridge</c> tool: <see cref="Bridge.Join"/> on the store's mesh, reported as every edit is.</summary>
public sealed class BridgeTool(ModelStore store)
{
    /// <summary>The worked example's input: two unit squares 1 apart facing each other, face 0 at z = 0 facing +Z and face 1 at z = 1 facing -Z.</summary>
    public const string Squares =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,1,2,3],[4,7,6,5]]}""";

    public const string Text =
        "Joins two open loops of the current mesh with a ring of quads, so a hole can go through a wall: e.g. a hole in a slab's front and the matching hole in its back become a tunnel. "
        + "loops is two loops, each a closed cycle of vertex ids (the last back to the first) whose every edge is open (used by one face); both have the same count, and no vertex is in both. "
        + "The vertices are paired by the least total length over every rotation of the second loop and both of its directions, so the order you list either loop in does not matter "
        + "(a tie goes to the first found: the second loop as listed, rotations 0 onward, then reversed, rotations 0 onward). "
        + "One quad is appended per pair of consecutive pairs, in the first loop's order, each wound so that its edge on each loop runs opposite to the face already there, so the surface closes; "
        + "loops whose faces wind so that no quad can agree with both (the two faces facing the same way) are refused. "
        + "No vertex is created. Each new face takes the colour and groups of the face on the first loop's edge it comes from, as extrude's walls do. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[ids]}, deleted:{vertices:[], faces:[]}}. "
        + "Bad input (a loop that is not a cycle of open edges, loops of unequal count, a vertex in both loops, windings no quad agrees with) "
        + "is a tool error that lists every problem by element; the mesh is left unchanged and nothing is added to undo. One bridge is one undo step. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to bridge in. "
        + "Worked example: two unit squares 1 apart facing each other (face 0 at z = 0 facing +Z, face 1 at z = 1 facing -Z), bridged into a box: four new faces, created:{faces:[2,3,4,5]}, each facing in, as the squares do, so the box is closed and check finds it inside-out (squares facing away from each other give a box facing out):\n"
        + MergeFacesTool.DefineMarker + Squares + "\n"
        + ExampleMarker + """{"loops":[[0,1,2,3],[4,5,6,7]]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<BridgeTool>("bridge", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["loops"] = new JsonObject
            {
                ["description"] = "The two loops to join, each a cycle of vertex ids along open edges, of the same count.",
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "integer" },
                    ["minItems"] = 3,
                },
                ["minItems"] = 2,
                ["maxItems"] = 2,
            },
        },
        ["required"] = new JsonArray("loops"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? loops = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("bridge", context, "loops", "mesh");
        var name = MeshName(mesh, errors);
        List<int>?[] read = [null, null];
        if (Absent(loops))
            errors.Add("loops: required but missing");
        else if (loops!.Value.ValueKind != JsonValueKind.Array || loops.Value.GetArrayLength() != 2)
            errors.Add(loops.Value.ValueKind == JsonValueKind.Array
                ? $"loops: expected two loops, got {loops.Value.GetArrayLength()}"
                : $"loops: expected an array of two loops of vertex ids, got {Describe(loops.Value)}");
        else
            for (var k = 0; k < 2; k++)
            {
                var before = errors.Count;
                read[k] = Ids($"loops[{k}]", loops.Value[k], errors);
                // Ids names a loop that is not an array itself; only a null loop is left to name here.
                if (read[k] is null && errors.Count == before) errors.Add($"loops[{k}]: expected an array of vertex ids, got null");
            }
        if (errors.Count > 0) return Failed("bridge", errors);

        return Edit(store, "bridge", name, m => Bridge.Join(m, read[0]!, read[1]!), (_, _) => { });
    }
}
