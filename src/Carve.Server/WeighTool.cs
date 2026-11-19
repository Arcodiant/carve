using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>weigh</c> tool (D-6, Weights; T-37): automatic bone weights for a whole mesh in one call.</summary>
public sealed class WeighTool(ModelStore store)
{
    public const string Text =
        "Weights a whole mesh to the skeleton automatically, by distance to the bones: weigh(mesh, bones, replace). Needs a skeleton (define_skeleton) first. "
        + "Each bone is a segment from its head to each child's head (a bone with no children runs on along its parent's direction for half its parent's length). "
        + "A vertex's weight for a bone falls off as (nearest distance / this bone's distance)^4, the largest four are kept (ties to the lower bone index) and normalised to sum to 1, "
        + "so a vertex on a bone is wholly that bone's and one midway between two bones is shared evenly. The same mesh and skeleton always give the same weights. "
        + "By default only vertices with no weight yet are weighted: any weight already there, from set_weights or an earlier weigh, is left alone. replace: true weighs every vertex again. "
        + "bones (optional) limits it to those bone names: only the vertices whose nearest bone is one of them are weighted, and among those bones alone (e.g. bones [\"Shoulder_L\", \"Elbow_L\", \"Hand_L\"] with replace: true redoes the left arm). "
        + "Use set_weights afterwards to fix single vertices by hand. "
        + PartTargetText
        + "Returns JSON {mesh, weighed: vertices weighted by this call, kept: vertices in scope left with their weights, unweighted: vertices of the mesh still without weights}. "
        + "Errors, each a tool error that changes nothing: no mesh yet, no skeleton yet, an unknown bone name, an empty bones list. "
        + "Worked example on a model with a skeleton:\n"
        + ExampleMarker + """{"replace":true}""";

    public static McpServerTool Create() => MeshToolSupport.Create<WeighTool>("weigh", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["bones"] = new JsonObject
            {
                ["description"] = "Bone names to limit the weighing to; leave out for every bone.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["minItems"] = 1,
            },
            ["replace"] = new JsonObject { ["description"] = "true weighs vertices that already have weights too (default false: only unweighted ones).", ["type"] = "boolean" },
        },
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? bones = null, JsonElement? replace = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("weigh", context, "bones", "replace", "mesh");
        var meshName = MeshName(mesh, errors);
        var again = OptionalBool("replace", replace, errors);
        List<string>? names = null;
        if (!Absent(bones))
        {
            if (bones!.Value.ValueKind != JsonValueKind.Array) errors.Add($"bones: expected an array of bone names, got {Describe(bones.Value)}");
            else
            {
                names = [];
                foreach (var b in bones.Value.EnumerateArray())
                {
                    if (b.ValueKind == JsonValueKind.String) names.Add(b.GetString()!);
                    else errors.Add($"bones: expected bone names, got {Describe(b)}");
                }
                if (names.Count == 0 && bones.Value.GetArrayLength() == 0) errors.Add("bones: an empty list weighs nothing; leave bones out for every bone");
            }
        }
        if (errors.Count > 0) return Failed("weigh", errors);

        var snapshot = store.Weigh("weigh", meshName, names, again, out var change, out var refused);
        if (refused.Count > 0) return Failed("weigh", refused);
        if (snapshot is null || change is null) return Error("weigh: " + NoMesh);

        var (name, outcome) = change.Value;
        return Ok(new { mesh = name, weighed = outcome.Weighed, kept = outcome.Kept, unweighted = outcome.Unweighted });
    }
}
