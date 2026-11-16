using System.Text.Json;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class CheckTool(ModelStore store)
{
    public const string Text =
        "Checks the current mesh's health: what is wrong by element id, what it measures, and what a game will make of it. Call it before you export or call a model done. "
        + "Takes one optional argument, mesh, the name of the mesh to check, which may be left out while the model has one mesh; any other argument is a tool error. It changes nothing: the mesh, its ids and undo are as they were. With no mesh yet it is a tool error saying to call define_mesh or open. "
        + "Returns JSON {ok, problems:[{check, element, message}], warnings:[\"element: message\"], notes:[], stats:{vertices, faces, edges, triangles, shells, closed, bounds:{min,max}, size:[x,y,z], area, volume, uvs:{mapped, unmapped}}}. "
        + "ok is true when there are no problems and no warnings; notes never make it false. "
        + "A shell is a set of faces joined by shared edges; shells[k] counts from the shell with the lowest face id. "
        + "Problems, with what each means for a game and the fix. "
        + "inside-out: a closed shell wound the wrong way round, so a game culls its faces and it is invisible. It is reported when the shell's signed volume is below -1e-6 times the cube of its bounds' diagonal, so a flat two-sided sheet, whose volume is rounding noise of either sign, is not. Reverse every face loop of that shell and call define_mesh again. "
        + "coincident-vertices: two vertices at exactly the same position, compared as floats with no tolerance, so -0 and 0 are the same, as in select; the surfaces look joined but are not, so they z-fight and waste vertices; call define_mesh again with one vertex and every face using it. "
        + "duplicate-face: two faces with the same vertices in any order; they z-fight and waste triangles, and the doubled edges also show among the warnings below; call define_mesh again without the extra face. "
        + "Warnings, the same ones define_mesh and the edits report. "
        + "\"edge belongs to only one face\": an open edge, a hole; collision built from the mesh breaks later. "
        + "\"edge is shared by N faces\": a non-manifold edge, which breaks collision the same way; call define_mesh again without the extra face. "
        + "\"two faces cross this edge in the same direction\": inconsistent winding, so a face points the wrong way and is culled; reverse that face's loop and call define_mesh again. An edge with this warning and a non-manifold one usually means a duplicate face, not a flipped one. "
        + "\"face is not planar\": a lifted corner, so the face folds along a diagonal the game chooses; move the vertex back onto the plane with move. "
        + "\"no face uses this vertex\": a stray that a game loads for nothing; call define_mesh again without it. "
        + "UV warnings, only for a mesh that has UVs on some face (a mesh with none gets none, and no uvs in stats; uvs counts the faces with and without UVs), each by face id. "
        + "\"no UVs, so the face samples the texture's corner\": the face is unmapped; project_uvs or unwrap it, or set_uvs it. "
        + "\"material 'wood' has a texture but the face has no UVs, so it samples the texture's corner\": the face's material has a texture and the face is unmapped, so the texture is not laid out on it; this one warning replaces the one above for that face, and it is given even in a mesh with no UVs at all (which otherwise gets none). Give the faces UVs with project_uvs, unwrap or set_uvs. "
        + "\"UVs flipped, so the texture is mirrored on the face\": its UVs wind clockwise on screen (u right, v down), the shoelace sum over (u, v) is positive; set_uvs to turn it, or project_uvs it again. "
        + "\"UVs of zero area, so the face is one smeared texel\": its UV area is below 1e-9 times the square of its UV bounds' diagonal, as a zero-area face in space is; project_uvs or unwrap it, or set_uvs corners that span an area. A smeared face is not also called flipped. "
        + "\"UVs overlap faces[m], so the two faces show the same texels\": two faces' UV triangles share more than 1e-6 of UV area (about a fifteenth of a texel of the 256 checker), so painting one paints the other. Each pair is listed once, under the lower id; sharing an edge is not an overlap. Mirrored halves sharing a texture area are deliberate and still warned of, so read the pair before moving anything; otherwise unwrap, which separates islands, or put a seam where islands collide and unwrap again. "
        + "Material warning, by material (materials['wood']): \"occlusion is 0.5 but the material has no occlusion_texture, so it has no effect\": a material the mesh uses has an occlusion strength other than 1 and no occlusion map, and glTF scales only the map, so render and export ignore it; give an occlusion_texture with define_material or leave occlusion at 1. "
        + "There is no delete yet, so removing a vertex or a face means define_mesh with the lists you want, and the ids after it close up. "
        + "Notes. 1 unit = 1 m, so size is the model's size in metres. A prop stands on y = 0, so the base note says where this one stands. The origin is where a game places the model, so it belongs inside the bounds (on them counts). More than one shell is noted because a model is usually one, though a detached part may be meant. "
        + "Stats. bounds and size are of the vertices faces use, closed is true when every edge has exactly two faces, triangles are what a game draws, area is the sum of the face areas in square metres, and volume is in cubic metres: the sum of the shells' signed volumes, null unless every shell is closed and consistently wound, and an inside-out shell's counts as negative. "
        + "Worked example on define_mesh's unit cube:\n"
        + ExampleMarker + "{}\n"
        + """Output: {"ok":true,"problems":[],"warnings":[],"notes":["size 1 x 1 x 1 m","base at y = 0","origin inside the bounds"],"stats":{"vertices":8,"faces":6,"edges":12,"triangles":12,"shells":1,"closed":true,"bounds":{"min":[0,0,0],"max":[1,1,1]},"size":[1,1,1],"area":6,"volume":1}}""";

    public static McpServerTool Create() => MeshToolSupport.Create<CheckTool>("check", Text, WithMesh(null));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("check", context, "mesh");
        var name = MeshName(mesh, errors);
        if (errors.Count > 0) return Failed("check", errors);
        if (Target(store, "check", name, out var error) is not { } target) return error!;

        var health = MeshHealth.Create(target, store.Current.Model.Materials);
        var stats = health.Stats;
        // In this order; uvs is left out for a mesh with none, so its output is what it always was.
        var shown = new Dictionary<string, object?>
        {
            ["vertices"] = stats.Vertices,
            ["faces"] = stats.Faces,
            ["edges"] = stats.Edges,
            ["triangles"] = stats.Triangles,
            ["shells"] = stats.Shells,
            ["closed"] = stats.Closed,
            ["bounds"] = new { min = Array(stats.BoundsMin), max = Array(stats.BoundsMax) },
            ["size"] = Array(stats.Size),
            ["area"] = stats.Area,
            ["volume"] = stats.Volume,
        };
        if (stats.Uvs is { } uvs) shown["uvs"] = new { mapped = uvs.Mapped, unmapped = uvs.Unmapped };
        return Ok(new
        {
            ok = health.Ok,
            problems = health.Problems.Select(p => new { check = p.Check, element = p.Element, message = p.Message }).ToArray(),
            warnings = health.Warnings.Select(w => w.ToString()).ToArray(),
            notes = health.Notes,
            stats = shown,
        });
    }
}
