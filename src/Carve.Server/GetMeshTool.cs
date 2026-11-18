using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class GetMeshTool(ModelStore store)
{
    public const string Text =
        "Reads back the current mesh in full, so you can learn its ids before editing. Takes two optional arguments: mesh, the name of the mesh to read, which may be left out while the model has one mesh; and target, the name of one of the mesh's morph targets, to read its absolute positions as well (an unknown name is a tool error that lists the mesh's targets). Any other argument is a tool error. "
        + "Vertex and face ids are the positions given to define_mesh, kept by every edit; the vertices and faces an extrude adds are appended after them. "
        + "Returns JSON {vertices:[{id, at:[x,y,z]}], faces:[{id, vertices:[ids, counter-clockwise seen from outside], or for a face with holes {loop:[ids], holes:[[ids], ...]} as define_mesh takes it, normal:[x,y,z] (unit, outward), centre:[x,y,z] (mean of the outer loop's corners)}], "
        + "edges:[{vertices:[a,b] (a < b), faces:[ids of the faces using the edge]}], "
        + "closed, bounds:{min,max}, warnings:[], "
        + "colours:[{colour:\"#RRGGBB\" as given to set_colour, faces:[ids]}] (the coloured faces only), groups:[{name, faces:[ids]}] (from define_group), and, only when the model has materials, materials:[{name, base_colour, metallic, roughness, emissive, occlusion, double_sided, base_colour_texture, metallic_roughness_texture, normal_texture, emissive_texture, occlusion_texture}] (every material of the model as define_material reported it, each texture null or {path, width, height}) and, only when a face of the mesh has a material, face_materials:[{material:name, faces:[ids]}] (the faces with a material; a face has a colour or a material, never both), and, only when the mesh has them, uvs:[{face, uv:[[u,v], ...]}] (one pair per corner in the face's loop order, outer loop then each hole; glTF axes, origin top-left, v down; the mapped faces only, as set_uvs gave them) and seams:[[a,b]...] (sorted pairs, a < b, from mark_edges seam) and, only when the mesh has morph targets, targets:[{name, vertices}] (in definition order; vertices is how many vertices the target moves) and, with the target argument, target:{name, positions:{\"<vertex id>\":[x,y,z]}} (the absolute positions of the vertices that target moves, sparse, as define_target takes them; every other vertex stays at its at)}. Here vertices, faces and edges are lists, where define_mesh and the edits return their counts. "
        + "A deleted vertex or face (see delete) is null in its slot of vertices or faces: its id is never given out again, so every other id keeps its place. "
        + "Coordinates are in metres (1 unit = 1 m), Y up. The output is the whole mesh, never trimmed, so it grows with the mesh. "
        + "With no mesh yet it is a tool error saying to call define_mesh or open. "
        + "Worked example on define_mesh's unit cube; the output is shown trimmed, where ... stands for more entries:\n"
        + "Worked example reading a target (after define_target's example): " + """{"target":"Bulge"}""" + "\n"
        + ExampleMarker + "{}\n"
        + """Output: {"vertices":[{"id":0,"at":[0,0,0]}, ...],"faces":[{"id":3,"vertices":[3,7,6,2],"normal":[0,1,0],"centre":[0.5,1,0.5]}, ...],"edges":[{"vertices":[0,1],"faces":[0,2]}, ...],"closed":true,"bounds":{"min":[0,0,0],"max":[1,1,1]},"warnings":[]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<GetMeshTool>("get_mesh", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["target"] = new JsonObject
            {
                ["description"] = "The name of one of the mesh's morph targets: its absolute positions for the vertices it moves are returned as target.positions.",
                ["type"] = "string",
            },
        },
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? mesh = null, JsonElement? target = null)
    {
        var errors = UnknownArguments("get_mesh", context, "mesh", "target");
        var name = MeshName(mesh, errors);
        var targetName = TargetToolSupport.TargetName("target", target, errors);
        if (errors.Count > 0) return Failed("get_mesh", errors);
        if (Target(store, "get_mesh", name, out var error) is not { } mesh0) return error!;
        MorphTarget? asked = null;
        if (targetName is not null && (asked = mesh0.Targets?.Find(targetName)) is null)
            return Failed("get_mesh", [TargetToolSupport.NoSuchTarget(targetName, mesh0)]);

        var body = new Dictionary<string, object>
        {
            ["vertices"] = mesh0.Vertices.Select((v, i) => mesh0.IsVertexDeleted(i) ? null : (object)new { id = i, at = Array(v) }).ToArray(),
            ["faces"] = Enumerable.Range(0, mesh0.Faces.Count).Select(f => mesh0.IsFaceDeleted(f) ? null : (object)new
            {
                id = f,
                // A holed face in define_mesh's object form; a plain face as its plain array, as before holes.
                vertices = mesh0.Faces[f].HasHoles
                    ? (object)new { loop = mesh0.Faces[f].Loop.ToArray(), holes = mesh0.Faces[f].Holes.Select(h => h.ToArray()).ToArray() }
                    : mesh0.Faces[f].Loop.ToArray(),
                normal = Array(mesh0.FaceNormal(f)),
                centre = Array(mesh0.FaceCentre(f)),
            }).ToArray(),
            ["edges"] = mesh0.Topology.Edges.Select(e => new { vertices = new[] { e.Lo, e.Hi }, faces = e.Faces.Select(u => u.Face).ToArray() }).ToArray(),
        };
        var style = mesh0.Style;
        var model = store.Current.Model;
        body["colours"] = (style?.Colours ?? new Dictionary<int, string>())
            .GroupBy(c => c.Value)
            .Select(g => new { colour = g.Key, faces = g.Select(c => c.Key).ToArray() }).ToArray();
        body["groups"] = (style?.Groups ?? []).Select(g => new { name = g.Name, faces = g.Faces }).ToArray();
        if (model.Materials.Count > 0) body["materials"] = model.Materials.Select(MaterialJson.Of).ToArray();
        if (style?.Materials is { Count: > 0 } used)
            body["face_materials"] = used.GroupBy(m => m.Value).Select(g => new { material = g.Key, faces = g.Select(m => m.Key).Order().ToArray() }).ToArray();
        if (mesh0.Uvs is { MappedFaces.Count: > 0 } uvs)
            body["uvs"] = uvs.MappedFaces.Select(f => new { face = f, uv = uvs.Of(f)!.Select(p => new[] { p.X, p.Y }).ToArray() }).ToArray();
        if (mesh0.Seams.Count > 0) body["seams"] = mesh0.Seams.Select(e => new[] { e.Lo, e.Hi }).ToArray();
        if (mesh0.Targets is { } targets) body["targets"] = targets.All.Select(t => new { name = t.Name, vertices = t.Count }).ToArray();
        if (asked is not null)
            body["target"] = new
            {
                name = asked.Name,
                // Base plus delta summed in double and rounded once, as MorphTargets.Apply sums them.
                positions = asked.Deltas.ToDictionary(d => d.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    d => new[] { (float)((double)mesh0.Vertices[d.Key].X + d.Value.X), (float)((double)mesh0.Vertices[d.Key].Y + d.Value.Y), (float)((double)mesh0.Vertices[d.Key].Z + d.Value.Z) }),
            };
        foreach (var (k, v) in Report(mesh0))
        {
            if (k is "vertices" or "faces" or "edges") continue;
            body[k] = v;
        }
        return Ok(body);
    }
}
