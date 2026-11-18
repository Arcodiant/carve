using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class ExtrudeTool(ModelStore store)
{
    public const string Text =
        "Extrudes faces of the current mesh: each listed face is pushed along its own outward normal by distance, and the gap is closed with walls. "
        + "Ids are the ones get_mesh reports and they never change. "
        + "For each face the CAP is the face itself, which keeps its id and moves to the new position; the WALLS are new quad faces, one per side of the face, "
        + "appended after the existing faces; the ring is new vertices, one per corner, appended after the existing vertices. "
        + "Wall k runs from corner k to corner k+1 of the face's vertex list, and ring vertex k stands over corner k. "
        + "distance is in coordinate units along the outward normal: a positive distance raises the face out of the solid and a NEGATIVE distance recesses it into the solid (a pocket). "
        + "inset (default 0, never negative) first shrinks the face towards its inside, measured in the face's own plane as a distance from each side; "
        + "with distance 0 and an inset the face is only shrunk, leaving a border ring of walls. "
        + "All the faces are done in one call; list each at most once. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[new vertex ids],faces:[new face ids]}, deleted:{vertices:[], faces:[]}, split:[], "
        + "extruded:[{face, cap, walls:[face ids], vertices:[ring vertex ids]}]}, one extruded entry per listed face in the order listed (cap equals face). "
        + "Bad input (an unknown face id, a face listed twice, no faces, a negative or non-finite inset, a non-finite distance, distance and inset both 0, an argument extrude does not take, an inset too large for the face so it collapses or crosses itself) "
        + "is a tool error that lists every problem by element, e.g. faces[6]; the mesh is left unchanged and nothing is added to undo. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to extrude. "
        + PartSpaceText
        + "Worked example on define_mesh's unit cube, an inset then a recess of the +Z face (face 1, z = 1); run the two calls in order:\n"
        + ExampleMarker + """{"faces":[1],"distance":0,"inset":0.2}""" + "\n"
        + ExampleMarker + """{"faces":[1],"distance":-0.2}""";

    public static McpServerTool Create() => MeshToolSupport.Create<ExtrudeTool>("extrude", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["faces"] = new JsonObject
            {
                ["description"] = "Face ids to extrude, each at most once.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
                ["minItems"] = 1,
            },
            ["distance"] = new JsonObject
            {
                ["description"] = "How far to push each face along its outward normal; negative recesses. Coordinate units.",
                ["type"] = "number",
            },
            ["inset"] = new JsonObject
            {
                ["description"] = "Shrink the face in its own plane first, by this distance from each side. Default 0.",
                ["type"] = "number",
                ["minimum"] = 0,
                ["default"] = 0,
            },
        },
        ["required"] = new JsonArray("faces", "distance"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? faces = null, JsonElement? distance = null, JsonElement? inset = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("extrude", context, "faces", "distance", "inset", "mesh");
        var name = MeshName(mesh, errors);
        var unknown = errors.Count;
        var f = Ids("faces", faces, errors);
        if (f is null && errors.Count == unknown) errors.Add("faces: required but missing");
        var d = Number("distance", distance, errors);
        var i = Number("inset", inset, errors, required: false);
        if (errors.Count > 0) return Failed("extrude", errors);

        return Edit(store, "extrude", name, mesh => MeshEdits.Extrude(mesh, f!, d!.Value, i ?? 0),
            (body, result) => body["extruded"] = result.Extruded
                .Select(x => new { face = x.Face, cap = x.Cap, walls = x.Walls, vertices = x.NewVertices }).ToArray());
    }
}
