using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class GetMeshTool(ModelStore store)
{
    public const string Text =
        "Reads back the current mesh in full, so you can learn its ids before editing. Takes one optional argument, mesh, the name of the mesh to read, which may be left out while the model has one mesh; any other argument is a tool error. "
        + "Vertex and face ids are the positions given to define_mesh, kept by every edit; the vertices and faces an extrude adds are appended after them. "
        + "Returns JSON {vertices:[{id, at:[x,y,z]}], faces:[{id, vertices:[ids, counter-clockwise seen from outside], normal:[x,y,z] (unit, outward), centre:[x,y,z] (mean of the corners)}], "
        + "edges:[{vertices:[a,b] (a < b), faces:[ids of the faces using the edge]}], "
        + "closed, bounds:{min,max}, warnings:[]}. Here vertices, faces and edges are lists, where define_mesh and the edits return their counts. "
        + "Coordinates are in metres (1 unit = 1 m), Y up. The output is the whole mesh, never trimmed, so it grows with the mesh. "
        + "With no mesh yet it is a tool error saying to call define_mesh or open. "
        + "Worked example on define_mesh's unit cube; the output is shown trimmed, where ... stands for more entries:\n"
        + ExampleMarker + "{}\n"
        + """Output: {"vertices":[{"id":0,"at":[0,0,0]}, ...],"faces":[{"id":3,"vertices":[3,7,6,2],"normal":[0,1,0],"centre":[0.5,1,0.5]}, ...],"edges":[{"vertices":[0,1],"faces":[0,2]}, ...],"closed":true,"bounds":{"min":[0,0,0],"max":[1,1,1]},"warnings":[]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<GetMeshTool>("get_mesh", Text, WithMesh(null));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("get_mesh", context, "mesh");
        var name = MeshName(mesh, errors);
        if (errors.Count > 0) return Failed("get_mesh", errors);
        if (Target(store, "get_mesh", name, out var error) is not { } target) return error!;

        var body = new Dictionary<string, object>
        {
            ["vertices"] = target.Vertices.Select((v, i) => new { id = i, at = Array(v) }).ToArray(),
            ["faces"] = Enumerable.Range(0, target.Faces.Count).Select(f => new
            {
                id = f,
                vertices = target.Faces[f].ToArray(),
                normal = Array(target.FaceNormal(f)),
                centre = Array(target.FaceCentre(f)),
            }).ToArray(),
            ["edges"] = target.Topology.Edges.Select(e => new { vertices = new[] { e.Lo, e.Hi }, faces = e.Faces.Select(u => u.Face).ToArray() }).ToArray(),
        };
        foreach (var (k, v) in Report(target))
        {
            if (k is "vertices" or "faces" or "edges") continue;
            body[k] = v;
        }
        return Ok(body);
    }
}
