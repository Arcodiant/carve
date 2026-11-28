using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>T-237: tessellate, an edit that cuts the selected faces into flat triangles no longer than max_edge, moving nothing.</summary>
public sealed class TessellateTool(ModelStore store)
{
    public const string Example = """{"max_edge":0.25,"faces":[3]}""";

    public const string Text =
        "Makes part of a mesh dense without changing its shape, as one edit that one undo takes back: cuts each selected face into triangles lying on the face's own plane, none with an edge longer than max_edge (metres, above 0; how fine is held by a budget, below), and moves no existing vertex. "
        + "Use it before displace on a hard-surface model: displace a group of triangles and only that group roughens, where subdivide rounds every edge of the whole mesh. "
        + "Give faces or group (a face group from define_group), not both; neither means every face. "
        + "Every edge of a selected face is cut into the fewest equal pieces no longer than max_edge, by its own length, so a long thin face is cut along its length only; points are added inside at about max_edge apart, and the face is triangulated with them, a hole staying a hole. "
        + "An edge shared with an unselected face puts the same new points into that face's loop, in order, so the mesh stays closed: that face is not split and none of its vertices move. "
        + "Output is triangles, not quads, because a triangle stays flat when displace moves its corners by different amounts. "
        + "Ids: every vertex keeps its id; the edge points, then each face's inside points, are appended; each face keeps its id on the triangle at its first corner, its other triangles appended. "
        + "Every triangle keeps its face's groups, colour and material; UVs at new points are interpolated from the face's own corners; a cut hard edge or seam stays hard or a seam along its pieces. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:{first, count}, faces:{first, count}}, deleted:{vertices:[], faces:[]}, split}, created being the blocks appended (they can run to thousands); split the faces cut, at summary {count, first} with the first 10 entries {face, pieces:{count, first}} (the first 10 piece ids), at verbosity full every entry [{face, pieces:[...]}] with the whole list. "
        + "Errors, each a tool error that changes nothing: max_edge missing, not a number or not above 0; an unknown face or group; both faces and group; an empty faces []; a face too thin for max_edge; a call predicted to add over 200,000 new vertices (each edge's points, each face's area over 0.866 max_edge squared and 3.5 points per max_edge of its loops, worked before any cut), the error giving the predicted count and the budget, or one that makes more than 200,000 all the same, stopped before anything changes; an argument tessellate does not take. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to tessellate. "
        + PartSpaceText
        + "Worked example on define_mesh's unit cube, its top face (3) cut into triangles no longer than 0.25, the four side faces each taking three points on their top edge:\n"
        + ExampleMarker + Example;

    public static McpServerTool Create() => MeshToolSupport.Create<TessellateTool>("tessellate", Text, WithVerbosity(WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["max_edge"] = new JsonObject { ["description"] = "The longest edge a triangle may have, in metres; above 0.", ["type"] = "number", ["exclusiveMinimum"] = 0 },
            ["faces"] = new JsonObject { ["description"] = "Face ids to tessellate.", ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } },
            ["group"] = new JsonObject { ["description"] = "A face group from define_group to tessellate.", ["type"] = "string" },
        },
        ["required"] = new JsonArray("max_edge"),
    })));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? max_edge = null, JsonElement? faces = null,
        JsonElement? group = null, JsonElement? mesh = null, JsonElement? verbosity = null)
    {
        const string tool = "tessellate";
        var errors = UnknownArguments(tool, context, "max_edge", "faces", "group", "mesh", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var name = MeshName(mesh, errors);
        var limit = Number("max_edge", max_edge, errors);
        if (limit is { } x && !(x > 0)) errors.Add($"max_edge: {x} is not above 0");
        var f = Ids("faces", faces, errors);
        var groupName = OptionalString("group", group, errors);
        if (groupName is not null && f is not null) errors.Add("group: give group or faces, not both");
        if (errors.Count > 0) return Failed(tool, errors);

        var before = (Vertices: 0, Faces: 0);
        string? noGroup = null;
        var snapshot = store.Edit(tool, name, m =>
        {
            before = (m.Vertices.Count, m.Faces.Count);
            return Tessellation.Tessellate(m, limit!.Value, FacesOrGroup(m, f, groupName, ref noGroup));
        }, out var result, out var problem);
        if (problem is not null) return Failed(tool, [problem]);
        if (noGroup is not null) return Failed(tool, [noGroup]);
        if (snapshot is null) return Error($"{tool}: " + NoMesh);
        if (!result!.Succeeded) return Failed(tool, result.Errors.Select(e => e.ToString()));

        var edited = snapshot.Model.Find(name ?? snapshot.Model.Meshes[0].Name)!;
        var body = Report(edited, level);
        body["created"] = new
        {
            vertices = new { first = before.Vertices, count = result.NewVertices.Count },
            faces = new { first = before.Faces, count = result.NewFaces.Count },
        };
        body["deleted"] = Deleted(result.Deleted);
        body["split"] = Split(result.Split, level);
        return Quiet(Ok(body), level);
    }
}
