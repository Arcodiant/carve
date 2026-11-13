using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>knife</c> tool (T-67): <see cref="Knife.Cut"/> on the store's mesh, reported as every edit is.</summary>
public sealed class KnifeTool(ModelStore store)
{
    public const string Text =
        "Cuts a path of points into the current mesh's faces. Each point must lie on the surface, within 1e-4 of the mesh's bounds diagonal (the tolerance). "
        + "Each point snaps in this order: within the tolerance of a vertex it is that vertex; otherwise within it of an edge it is on that edge (a new vertex there, shared by both faces on the edge); otherwise it is inside the face it lies on (a new vertex inside it, on its plane). "
        + "Consecutive points must share a face, and with closed so must the last and the first; the segment between them runs straight across that face. A path crosses from one face to the next only through a point on their shared edge, which you list. "
        + "A path from boundary to boundary of a face (a vertex or an edge point) splits it. An open path must start and end on an edge or a vertex. "
        + "A closed path wholly inside one face, touching no edge, cuts out an inner face (appended) and leaves the face with a hole on the same loop; a closed path that touches the boundary at two or more points splits the face as usual, and one that touches it at one point only is refused. "
        + "A split face keeps its id for the piece holding its first corner (or the next corner off the cut); the other pieces, the inner face among them, are appended. Pieces take the face's colour and groups, split hard edges stay hard, new edges are soft, a new vertex on an edge blends its ends' weights and one inside a face its triangle's corners' weights. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[]}, deleted:{vertices:[], faces:[]}, split:[{face, pieces}]}: split lists each split face with its pieces, its own id first. "
        + "Bad input (a point off the surface, which gives its distance and the nearest surface point; a point inside two overlapping faces; two consecutive points sharing no face; a segment that leaves its face or passes through one of its holes; an open path ending inside a face; too few points; an argument knife does not take) "
        + "is a tool error that lists every problem; the mesh is left unchanged and nothing is added to undo. One undo step undoes the whole cut. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to cut. "
        + "Worked examples on define_mesh's unit cube, whose front face is z = 1: a door outline up from the front face's bottom edge splits the door off it (12 vertices, 7 faces), "
        + "and a closed square inside the front face cuts out an inner face and leaves the front holed (12 vertices, 7 faces):\n"
        + ExampleMarker + """{"points":[[0.35,0,1],[0.35,0.7,1],[0.65,0.7,1],[0.65,0,1]]}""" + "\n"
        + ExampleMarker + """{"points":[[0.25,0.25,1],[0.75,0.25,1],[0.75,0.75,1],[0.25,0.75,1]],"closed":true}""";

    public static McpServerTool Create() => MeshToolSupport.Create<KnifeTool>("knife", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["points"] = new JsonObject
            {
                ["description"] = "The path, [[x, y, z], ...], each point on the surface within the tolerance.",
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "number" },
                    ["minItems"] = 3,
                    ["maxItems"] = 3,
                },
            },
            ["closed"] = new JsonObject
            {
                ["description"] = "Join the last point back to the first (default false).",
                ["type"] = "boolean",
            },
        },
        ["required"] = new JsonArray("points"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? points = null, JsonElement? closed = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("knife", context, "points", "closed", "mesh");
        var name = MeshName(mesh, errors);
        var path = Points(points, errors);
        var isClosed = !Absent(closed) && OptionalBool("closed", closed, errors);
        if (errors.Count > 0) return Failed("knife", errors);
        return Edit(store, "knife", name, m => Knife.Cut(m, path, isClosed).Edit, (_, _) => { });
    }

    private static List<Vector3> Points(JsonElement? value, List<string> errors)
    {
        var result = new List<Vector3>();
        if (Absent(value)) { errors.Add("points: required but missing"); return result; }
        if (value!.Value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"points: expected an array of [x, y, z], got {Describe(value.Value)}");
            return result;
        }
        var i = 0;
        foreach (var item in value.Value.EnumerateArray())
        {
            if (Vec3($"points[{i}]", item, errors) is { } p) result.Add(p);
            i++;
        }
        return result;
    }
}
