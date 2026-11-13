using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>get_weights</c> tool (D-6, Weights; T-38): what a rig has, by vertex or by bone, with the gaps reported.</summary>
public sealed class GetWeightsTool(ModelStore store)
{
    public const string Text =
        "Reads back the bone weights of a mesh: get_weights(mesh, vertices or bone). Needs a skeleton (define_skeleton) first. "
        + "vertices (vertex ids) gives each one's influences by bone name; bone (a bone name) gives every vertex that bone moves and how hard; "
        + "give neither to read every vertex of the mesh; both is a tool error. A vertex with no weight reads as an empty object. "
        + "The summary and warnings always cover the whole mesh, whatever was asked: warnings list the unweighted vertices by id with their count "
        + "(they stay where they are when posed, and export refuses them), and note each bone no vertex follows. "
        + "Returns JSON {mesh, vertices: [{vertex, weights: {bone: weight}}] (by vertex), or bone, followers: [{vertex, weight}] (by bone), "
        + "summary: {vertices, weighted, unweighted, unweightedVertices, boneVertices: {bone: how many vertices follow it}}, warnings: [text]}. "
        + "Errors, each a tool error: no mesh yet, no skeleton yet, both of vertices and bone, an unknown bone name, a vertex id the mesh does not have. "
        + "Worked example, after set_weights binds vertices 0 and 1 of a cube wholly to a bone Head, with the bones Hips, Head and Tail:\n"
        + ExampleMarker + """{"vertices":[0,7]}""" + "\n"
        + ResultMarker + """{"mesh":"cube","vertices":[{"vertex":0,"weights":{"Head":1}},{"vertex":7,"weights":{}}]"""
        + ""","summary":{"vertices":8,"weighted":2,"unweighted":6,"unweightedVertices":[2,3,4,5,6,7],"boneVertices":{"Hips":0,"Head":2,"Tail":0}}"""
        + ""","warnings":["6 vertices have no weight: 2, 3, 4, 5, 6, 7; they stay where they are when posed and export refuses them (weigh or set_weights)","""
        + """ "note: no vertex follows 'Hips', 'Tail'"]}""";

    /// <summary>Starts the line of the worked example's result, as select's does.</summary>
    public const string ResultMarker = SelectTool.ResultMarker;

    public static McpServerTool Create() => MeshToolSupport.Create<GetWeightsTool>("get_weights", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["vertices"] = new JsonObject
            {
                ["description"] = "Vertex ids to read. Give this or bone, or neither for every vertex.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
                ["minItems"] = 1,
            },
            ["bone"] = new JsonObject { ["description"] = "A bone name: read every vertex it moves. Give this or vertices.", ["type"] = "string" },
        },
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? vertices = null, JsonElement? bone = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("get_weights", context, "vertices", "bone", "mesh");
        var meshName = MeshName(mesh, errors);
        var before = errors.Count;
        var ids = Ids("vertices", vertices, errors);
        var boneName = OptionalString("bone", bone, errors);
        if (ids is not null && boneName is not null) errors.Add("vertices, bone: give vertices or bone, not both");
        else if (ids is { Count: 0 } && errors.Count == before) errors.Add("vertices: list at least one vertex");
        if (errors.Count > 0) return Failed("get_weights", errors);

        var model = store.Current.Model;
        var target = model.Target(meshName, out var problem);
        if (problem is not null) return Failed("get_weights", [problem]);
        if (target is null) return Error("get_weights: " + NoMesh);
        if (model.Skeleton is not { } skeleton)
            return Error("get_weights: the model has no skeleton yet; call define_skeleton first");

        var m = target.Mesh;
        var weights = m.Weights ?? VertexWeights.None;
        var names = skeleton.Names.ToList();
        var problems = new List<string>();
        var boneIndex = -1;
        if (boneName is not null)
        {
            boneIndex = names.IndexOf(boneName);
            if (boneIndex < 0) problems.Add($"bone: no bone named '{boneName}'; the bones are {NameList.Quoted(names)}");
        }
        if (ids is not null)
            foreach (var v in ids.Distinct())
                if (VertexWeights.VertexProblem(m, v) is { } bad) problems.Add($"vertices[{v}]: {bad}");
        if (problems.Count > 0) return Failed("get_weights", problems);

        var unweighted = m.LiveVertices.Where(v => !weights.Has(v)).ToArray();
        var counts = names.Select(_ => 0).ToArray();
        foreach (var v in weights.Vertices)
            foreach (var i in weights.Of(v)) counts[i.Bone]++;

        var warnings = new List<string>();
        if (unweighted.Length > 0)
            warnings.Add($"{unweighted.Length} vertices have no weight: {string.Join(", ", unweighted)}; they stay where they are when posed and export refuses them (weigh or set_weights)");
        var idle = names.Where((_, i) => counts[i] == 0).ToArray();
        if (idle.Length > 0)
            warnings.Add($"note: no vertex follows {NameList.Quoted(idle)}");

        var summary = new
        {
            vertices = m.LiveVertexCount,
            weighted = m.LiveVertexCount - unweighted.Length,
            unweighted = unweighted.Length,
            unweightedVertices = unweighted,
            boneVertices = names.Select((n, i) => (n, c: counts[i])).ToDictionary(p => p.n, p => p.c),
        };

        if (boneName is not null)
        {
            var followers = weights.Vertices
                .SelectMany(v => weights.Of(v).Where(i => i.Bone == boneIndex).Select(i => new { vertex = v, weight = i.Weight }))
                .ToArray();
            return Ok(new { mesh = target.Name, bone = boneName, followers, summary, warnings });
        }

        var read = (ids?.Distinct().Order() ?? m.LiveVertices).Select(v => new
        {
            vertex = v,
            weights = weights.Of(v).ToDictionary(i => names[i.Bone], i => i.Weight),
        }).ToArray();
        return Ok(new { mesh = target.Name, vertices = read, summary, warnings });
    }
}
