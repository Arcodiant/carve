using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>unwrap: cuts faces into islands at seams and open edges, flattens each by LSCM and shelf-packs them (T-56).</summary>
public sealed class UnwrapTool(ModelStore store)
{
    public const string Text =
        "Unwraps faces into UV space: unwrap(faces, into, margin, mesh). The faces are cut into islands, each island is flattened with as little distortion as it allows (least-squares conformal maps), and the islands are packed side by side into a rectangle of the texture, all deterministically: the same mesh and seams always give the same UVs. "
        + "Islands: two faces stay joined across an edge they share unless it is a seam (mark_edges with seam: true), so the seams you mark decide the islands; open edges, edges of three or more faces, and the border of the faces you chose cut too. "
        + "Each island must be a disc, one piece with one boundary loop, as a sheet of paper is: a seam must cut every closed loop of faces until each island lies flat without tearing. A closed box needs seams round enough edges to unfold it; a cylinder needs one seam down its side, so the side can unroll into a strip, and its caps cut free by seams round both rims. A face's own holes are allowed as extra loops. An island that is not a disc is refused, naming its lowest face. "
        + "Each island keeps its real size, so texel density is even across islands; each is turned so its tightest bounding rectangle is level and wider than tall, never mirrored (every face comes out with its corners counterclockwise on screen, v down), shelf-packed tallest first with margin between islands and to the edge, then scaled to fit into. "
        + "UVs run as in glTF: (0, 0) is the texture's top-left, u runs right and v runs down. faces defaults to every face; faces outside it keep their UVs, and seams are left as they are. into is [u0, v0, u1, v1], default [0, 0, 1, 1]; margin defaults to 1/64 of into's shorter side (four texels of a 256 texture). "
        + "One call is one step for undo, and a call that fails changes nothing. "
        + "Returns JSON {mesh, islands, fill, scale}: islands lists each island by its lowest face as {faces (its face count), lowest_face, rect: [u0, v0, u1, v1], stretch, flipped}, where stretch is the largest ratio of a triangle's two singular values (1 is none; a curved island shows more) and flipped counts folded triangles (normally 0); fill is the share of into the islands cover, and scale is UV units per metre. get_mesh reads the UVs back as uvs. "
        + "Errors, each a tool error that changes nothing and names the element: no mesh yet; a face that is not a face of the mesh, is deleted or is listed twice, or an empty faces list; an island that is not a disc (faces[lowest]: its face count and its boundary loops; mark a seam across it with mark_edges); an island with too few seams to lay flat (faces[lowest]: its face count; mark more seams with mark_edges, e.g. a path round it), told apart from an island with no extent (its corners on one point or one line); a flattening that did not converge; into not four finite numbers or with no area; a margin below 0 or of half into's shorter side or more, or one the islands cannot fit with; an unknown argument. "
        + "Worked example, a barrel (define_mesh with 40 vertices in five rings of 8, ring k at index 8k, the k = 2 column at -Z; faces 0 to 31 the side's quads, 32 the bottom cap, 33 the top cap): first mark_edges with seam: true on the four vertical edges at -Z, [[2,10],[10,18],[18,26],[26,34]], and both rims, [[0,1],[1,2],[2,3],[3,4],[4,5],[5,6],[6,7],[7,0]] and [[32,33],[33,34],[34,35],[35,36],[36,37],[37,38],[38,39],[39,32]]; then unwrap with the defaults, which gives three islands: the side (32 faces, lowest face 0) as a long strip across the top, and the two caps (faces 32 and 33) side by side below it:\n"
        + ExampleMarker + "{}";

    public static McpServerTool Create() => MeshToolSupport.Create<UnwrapTool>("unwrap", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["faces"] = new JsonObject
            {
                ["description"] = "Ids of the faces to unwrap; default every face. The others keep their UVs, and an edge between them and these counts as cut.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
            },
            ["into"] = new JsonObject
            {
                ["description"] = "The UV rectangle [u0, v0, u1, v1] to pack into, glTF axes ((0,0) top-left, v down); default [0, 0, 1, 1].",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "number" },
                ["minItems"] = 4,
                ["maxItems"] = 4,
            },
            ["margin"] = new JsonObject
            {
                ["description"] = "The gap between islands and to into's edges, in UV units; default 1/64 of into's shorter side.",
                ["type"] = "number",
            },
        },
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? faces = null, JsonElement? into = null, JsonElement? margin = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("unwrap", context, "faces", "into", "margin", "mesh");
        var meshName = MeshName(mesh, errors);
        var ids = Ids("faces", faces, errors);
        var box = Into(into, errors);
        var gap = Number("margin", margin, errors, required: false);
        if (errors.Count > 0) return Failed("unwrap", errors);

        UnwrapResult? made = null;
        var snapshot = store.Rework("unwrap", meshName, m =>
        {
            var (result, problems) = Unwrap.Run(m, ids, box, gap);
            made = result;
            return (result?.Mesh, problems.Select(e => $"{e.Element}: {e.Message}").ToList());
        }, out var refused, out var problem);
        if (problem is not null) return Failed("unwrap", [problem]);
        if (refused.Count > 0) return Failed("unwrap", refused);
        if (snapshot is null || made is null) return Error("unwrap: " + NoMesh);

        return Ok(new
        {
            mesh = snapshot.Model.Target(meshName, out _)!.Name,
            islands = made.Islands.Select(i => new
            {
                faces = i.Faces.Count,
                lowest_face = i.LowestFace,
                rect = new[] { i.Rect.U0, i.Rect.V0, i.Rect.U1, i.Rect.V1 },
                stretch = i.Stretch,
                flipped = i.Flipped,
            }),
            fill = made.Fill,
            scale = made.Scale,
        });
    }

    private static (double, double, double, double)? Into(JsonElement? value, List<string> errors)
    {
        if (Absent(value)) return null;
        if (value!.Value.ValueKind != JsonValueKind.Array || value.Value.GetArrayLength() != 4)
        {
            errors.Add("into: " + UvArguments.IntoShape(value.Value.ValueKind == JsonValueKind.Array ? value.Value.GetArrayLength().ToString(System.Globalization.CultureInfo.InvariantCulture) : Describe(value.Value)));
            return null;
        }
        var xs = new double[4];
        var ok = true;
        for (var j = 0; j < 4; j++)
        {
            if (IntoNumber($"into[{j}]", value.Value[j], errors) is { } x) xs[j] = x;
            else ok = false;
        }
        return ok ? (xs[0], xs[1], xs[2], xs[3]) : null;
    }
}
