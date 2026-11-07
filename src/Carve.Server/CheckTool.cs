using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class CheckTool(ModelStore store)
{
    public const string Text =
        "Checks the current mesh's health: what is wrong by element id, what it measures, and what a game will make of it. Call it before you export or call a model done. "
        + "Takes no arguments; any argument is a tool error. It changes nothing: the mesh, its ids and undo are as they were. With no mesh yet it is a tool error saying to call define_mesh or open. "
        + "Returns JSON {ok, problems:[{check, element, message}], warnings:[\"element: message\"], notes:[], stats:{vertices, faces, edges, triangles, shells, closed, bounds:{min,max}, size:[x,y,z], area, volume}}. "
        + "ok is true when there are no problems and no warnings; notes never make it false. "
        + "A shell is a set of faces joined by shared edges; shells[k] counts from the shell with the lowest face id. "
        + "Problems, with what each means for a game and the fix. "
        + "inside-out: a closed shell wound the wrong way round, so a game culls its faces and it is invisible; reverse every face loop of that shell and call define_mesh again. "
        + "coincident-vertices: two vertices at exactly the same position, compared bit for bit with no tolerance; the surfaces look joined but are not, so they z-fight and waste vertices; call define_mesh again with one vertex and every face using it. "
        + "duplicate-face: two faces with the same vertices in any order; they z-fight and waste triangles, and the doubled edges also show among the warnings below; call define_mesh again without the extra face. "
        + "Warnings, the same ones define_mesh and the edits report. "
        + "\"edge belongs to only one face\": an open edge, a hole; collision built from the mesh breaks later. "
        + "\"edge is shared by N faces\": a non-manifold edge, which breaks collision the same way; call define_mesh again without the extra face. "
        + "\"two faces cross this edge in the same direction\": inconsistent winding, so a face points the wrong way and is culled; reverse that face's loop and call define_mesh again. An edge with this warning and a non-manifold one usually means a duplicate face, not a flipped one. "
        + "\"face is not planar\": a lifted corner, so the face folds along a diagonal the game chooses; move the vertex back onto the plane with move. "
        + "\"no face uses this vertex\": a stray that a game loads for nothing; call define_mesh again without it. "
        + "There is no delete yet, so removing a vertex or a face means define_mesh with the lists you want, and the ids after it close up. "
        + "Notes. 1 unit = 1 m, so size is the model's size in metres. A prop stands on y = 0, so the base note says where this one stands. The origin is where a game places the model, so it belongs inside the bounds (on them counts). More than one shell is noted because a model is usually one, though a detached part may be meant. "
        + "Stats. bounds and size are of the vertices faces use, closed is true when every edge has exactly two faces, triangles are what a game draws, area is the sum of the face areas in square metres, and volume is in cubic metres: the sum of the shells' signed volumes, null unless every shell is closed and consistently wound, and an inside-out shell's counts as negative. "
        + "Worked example on define_mesh's unit cube:\n"
        + ExampleMarker + "{}\n"
        + """Output: {"ok":true,"problems":[],"warnings":[],"notes":["size 1 x 1 x 1 m","base at y = 0","origin inside the bounds"],"stats":{"vertices":8,"faces":6,"edges":12,"triangles":12,"shells":1,"closed":true,"bounds":{"min":[0,0,0],"max":[1,1,1]},"size":[1,1,1],"area":6,"volume":1}}""";

    public static McpServerTool Create() => MeshToolSupport.Create<CheckTool>("check", Text, null);

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        if (UnknownArguments("check", context) is { Count: > 0 } unknown) return Failed("check", unknown);
        var mesh = store.Current.Mesh;
        if (mesh is null) return Error("check: " + NoMesh);

        var health = MeshHealth.Create(mesh);
        var stats = health.Stats;
        return Ok(new
        {
            ok = health.Ok,
            problems = health.Problems.Select(p => new { check = p.Check, element = p.Element, message = p.Message }).ToArray(),
            warnings = health.Warnings.Select(w => w.ToString()).ToArray(),
            notes = health.Notes,
            stats = new
            {
                vertices = stats.Vertices,
                faces = stats.Faces,
                edges = stats.Edges,
                triangles = stats.Triangles,
                shells = stats.Shells,
                closed = stats.Closed,
                bounds = new { min = Array(stats.BoundsMin), max = Array(stats.BoundsMax) },
                size = Array(stats.Size),
                area = stats.Area,
                volume = stats.Volume,
            },
        });
    }
}
