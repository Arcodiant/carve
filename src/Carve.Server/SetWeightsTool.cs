using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>set_weights</c> tool (D-6, Weights): explicit bone weights on vertices, by bone name.</summary>
public sealed class SetWeightsTool(ModelStore store)
{
    public const string Text =
        "Sets the bone weights of vertices of a mesh: set_weights(vertices or group, weights, mesh). Needs a skeleton (define_skeleton) first. "
        + "weights is an object of bone NAME to weight, e.g. {\"Head\": 1} binds the vertices wholly to Head, {\"Arm\": 0.5, \"Forearm\": 0.5} shares them. "
        + "Give exactly one of vertices (vertex ids) or group (a face group from define_group: every vertex of its faces, so a hat is all Head in one call). "
        + "Every vertex listed gets exactly these weights, replacing any it had. Weights are 0 or more and need not sum to 1: they are normalised to sum to 1. "
        + "A vertex keeps at most four influences: given more, the largest four are kept (ties go to the lower bone index), and the result says so. "
        + "Weights are kept with the mesh by vertex id through move and extrude (an extrude's new vertices start unweighted); a new define_mesh of the same name starts them again. "
        + "Changing the skeleton once weights exist is a tool error unless the bone names and their order stay the same. "
        + "A vertex with no weight stays where it is when the model is posed. "
        + "render's pose argument draws the model posed. "
        + PartTargetText
        + "Returns JSON {mesh, set: vertices set, capped: how many of them lost influences to the cap of four (0 if none), unweighted: vertices of the mesh still without weights, weights: {bone: normalised weight}}. "
        + "Errors, each a tool error that changes nothing: no mesh yet, no skeleton yet, both or neither of vertices and group, an unknown group, a vertex id the mesh does not have, an unknown bone name, a negative or non-numeric weight, no weight above 0. "
        + "Worked example on a model with bones Hips and Head (a box on the head):\n"
        + ExampleMarker + """{"group":"hat","weights":{"Head":1}}""";

    public static McpServerTool Create() => MeshToolSupport.Create<SetWeightsTool>("set_weights", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["vertices"] = new JsonObject
            {
                ["description"] = "Vertex ids to weight. Give this or group, not both.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
                ["minItems"] = 1,
            },
            ["group"] = new JsonObject { ["description"] = "A face group from define_group: its faces' vertices are weighted. Give this or vertices, not both.", ["type"] = "string" },
            ["weights"] = new JsonObject
            {
                ["description"] = "Bone name to weight, e.g. {\"Head\": 1}. Normalised to sum to 1; at most four bones are kept.",
                ["type"] = "object",
                ["additionalProperties"] = new JsonObject { ["type"] = "number" },
            },
        },
        ["required"] = new JsonArray("weights"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? vertices = null, JsonElement? group = null, JsonElement? weights = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("set_weights", context, "vertices", "group", "weights", "mesh");
        var meshName = MeshName(mesh, errors);
        var before = errors.Count;
        var ids = Ids("vertices", vertices, errors);
        var groupName = OptionalString("group", group, errors);
        if (ids is not null && groupName is not null) errors.Add("vertices, group: give vertices or group, not both");
        else if (ids is null && groupName is null && errors.Count == before) errors.Add("vertices, group: give one of vertices or group");
        else if (ids is { Count: 0 }) errors.Add("vertices: nothing to weight: list at least one vertex");

        var given = new Dictionary<string, double>();
        if (Absent(weights)) errors.Add("weights: required but missing");
        else if (weights!.Value.ValueKind != JsonValueKind.Object) errors.Add($"weights: expected an object of bone name to weight, got {Describe(weights.Value)}");
        else
        {
            foreach (var p in weights.Value.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Number) errors.Add($"weights.{p.Name}: expected a number, got {Describe(p.Value)}");
                else given[p.Name] = p.Value.GetDouble();
            }
        }
        if (errors.Count > 0) return Failed("set_weights", errors);

        var snapshot = store.SetWeights("set_weights", meshName, m =>
        {
            var problems = new List<string>();
            if (ids is not null)
            {
                foreach (var v in ids.Distinct())
                    if (VertexWeights.VertexProblem(m, v) is { } bad)
                        problems.Add($"vertices[{v}]: {bad}");
                return (problems.Count > 0 ? null : ids, problems);
            }
            var faces = m.Style?.FacesOf(groupName!);
            if (faces is null)
            {
                problems.Add(NoGroup(m.Style, groupName!));
                return (null, problems);
            }
            return (faces.SelectMany(f => m.Faces[f].Corners).Distinct().Order().ToArray(), problems);
        }, given, out var change, out var refused);
        if (refused.Count > 0) return Failed("set_weights", refused);
        if (snapshot is null || change is null) return Error("set_weights: " + NoMesh);

        return Ok(new
        {
            mesh = change.Mesh,
            set = change.Set,
            capped = change.Capped,
            unweighted = change.Unweighted,
            weights = change.Influences.ToDictionary(
                i => snapshot.Model.Skeleton!.Bones[i.Bone].Name, i => i.Weight),
        });
    }
}
