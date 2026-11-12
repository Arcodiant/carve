using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>loop_cut</c> tool (T-66): <see cref="LoopCut.Cut"/> on the store's mesh, reported as every edit is.</summary>
public sealed class LoopCutTool(ModelStore store)
{
    public const string Text =
        "Adds edge loops round a shape: name one edge by its two vertex ids and the fractions along it where the loops go. "
        + "The ring starts at that edge and walks both ways, across each quad on the current edge to that quad's opposite edge. "
        + "It stops where it closes (a loop round the sides of a box), at an open edge, or at a face that is not a plain quad: a triangle, an n-gon, or a face with holes. "
        + "A stopped-at face is not split, but takes the new vertex on the ring edge it shares. "
        + "Each fraction in at is one new loop, at that fraction of every ring edge measured from the end on edge's first vertex, and the side carries across each quad by its side edges, so the loops stay parallel. "
        + "at must be strictly ascending, each strictly between 0 and 1, and not too near an end of the shortest ring edge (within the planarity tolerance, 1e-4 of the mesh's bounds diagonal). "
        + "Each quad of the ring is split into pieces, one more than the fractions; a split quad keeps its id for the piece holding its first corner (or the next corner off the cut) and the other pieces are appended; new vertices are appended by edge, low id then high. "
        + "Pieces take the face's colour and groups, split hard edges stay hard, new edges are soft, and a new vertex blends its edge's ends' weights. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[]}, deleted:{vertices:[], faces:[]}, split:[{face, pieces}]}: split lists each quad of the ring with its pieces, its own id first. "
        + "Bad input (a pair that is not an edge of the mesh, an edge with no quad on it, a bad at, an argument loop_cut does not take) is a tool error that lists every problem; select with kind edges lists the real edges. "
        + "The mesh is left unchanged and nothing is added to undo; one undo step undoes the whole cut. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to cut. "
        + "Worked example on define_mesh's unit cube: one loop at 0.5 round the four sides, starting on the vertical edge 0-3, gives 12 vertices and 10 faces:\n"
        + ExampleMarker + """{"edge":[0,3],"at":[0.5]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<LoopCutTool>("loop_cut", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["edge"] = new JsonObject
            {
                ["description"] = "The edge the ring starts at, as a pair [a, b] of vertex ids; the fractions are measured from a.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
                ["minItems"] = 2,
                ["maxItems"] = 2,
            },
            ["at"] = new JsonObject
            {
                ["description"] = "The fractions along each ring edge from a's side, one loop each: strictly ascending, each strictly between 0 and 1.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "number" },
                ["minItems"] = 1,
            },
        },
        ["required"] = new JsonArray("edge", "at"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? edge = null, JsonElement? at = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("loop_cut", context, "edge", "at", "mesh");
        var name = MeshName(mesh, errors);

        (int A, int B)? pair = null;
        if (Absent(edge)) errors.Add("edge: required but missing");
        else
        {
            var ids = Ids("edge", edge, errors);
            if (ids is not null && ids.Count != 2) errors.Add($"edge: expected a pair [a, b] of vertex ids, got {ids.Count} entr{(ids.Count == 1 ? "y" : "ies")}");
            else if (ids is not null) pair = (ids[0], ids[1]);
        }

        List<double>? fractions = null;
        if (Absent(at)) errors.Add("at: required but missing");
        else if (at!.Value.ValueKind != JsonValueKind.Array) errors.Add($"at: expected an array of fractions, got {Describe(at.Value)}");
        else
        {
            fractions = [];
            var k = 0;
            foreach (var x in at.Value.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var d)) fractions.Add(d);
                else errors.Add($"at: the {Ordinal(k)} listed entry: expected a number, got {Describe(x)}");
                k++;
            }
        }

        if (errors.Count > 0) return Failed("loop_cut", errors);
        return Edit(store, "loop_cut", name, m => LoopCut.Cut(m, pair!.Value, fractions!).Edit, (_, _) => { });
    }
}
