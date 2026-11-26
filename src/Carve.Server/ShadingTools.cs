using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class SetShadingTool(ModelStore store)
{
    public const string Text =
        "Sets how a mesh is shaded: set_shading(shading, mesh), where shading is flat (the default: every face its own normal, every edge a crease) or smooth "
        + "(a vertex's normal is the average of the faces that meet at it across soft edges, each face weighted by its corner angle there, so a character reads soft). "
        + "Under smooth an edge is soft unless mark_edges made it hard; an open edge (one face) is a plain boundary with nothing to smooth across, and an edge shared by three or more faces is always hard. "
        + "Shading is per mesh, kept with it through move and extrude, saved in the Carve file (version 3, written only when a mesh is smooth or has hard edges), and render and export draw the same normals. "
        + "A new define_mesh of the same name starts flat again. "
        + PartTargetText
        + "Returns JSON {mesh, shading, hard_edges:[[a,b]...]}. "
        + "Errors, each a tool error that changes nothing: no mesh yet, a shading that is not flat or smooth. "
        + "Worked example on define_mesh's unit cube:\n"
        + ExampleMarker + """{"shading":"smooth"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<SetShadingTool>("set_shading", Text, WithVerbosity(WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["shading"] = new JsonObject { ["description"] = "flat or smooth.", ["type"] = "string", ["enum"] = new JsonArray("flat", "smooth") },
        },
        ["required"] = new JsonArray("shading"),
    })));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? shading = null, JsonElement? mesh = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("set_shading", context, "shading", "mesh", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var meshName = MeshName(mesh, errors);
        var text = RequiredString("shading", shading, errors);
        if (text is not null && text != "flat" && text != "smooth") errors.Add($"shading: '{text}' is not a shading: it is flat or smooth");
        if (errors.Count > 0) return Failed("set_shading", errors);

        var mode = text == "smooth" ? ShadingMode.Smooth : ShadingMode.Flat;
        var snapshot = store.Reshade("set_shading", meshName, m => ((m.Shade ?? MeshShade.Flat).WithMode(mode), []), out var refused, out var problem);
        if (problem is not null) return Failed("set_shading", [problem]);
        if (refused.Count > 0) return Failed("set_shading", refused);
        if (snapshot is null) return Error("set_shading: " + NoMesh);

        var target = snapshot.Model.Target(meshName, out _)!;
        return Quiet(Ok(new { mesh = target.Name, shading = text, hard_edges = target.Mesh.HardEdges.Select(e => new[] { e.Lo, e.Hi }).ToArray() }), level);
    }
}

public sealed class MarkEdgesTool(ModelStore store)
{
    public const string Text =
        "Marks edges of a mesh hard (a crease) or as a seam, or clears either mark: mark_edges(edges, hard, seam, mesh), each edge a [a, b] pair of vertex ids (either order) that must be an edge of the mesh; select with kind edges lists them. "
        + "hard and seam are independent flags, each true to mark and false to clear; a flag left out leaves that mark alone. With neither given, hard defaults to true, as before seam existed; with only seam given, hard edges are left alone. A hard edge keeps the shading of the two faces apart under set_shading smooth, and does nothing while the mesh is flat (it is kept for when it is smooth). "
        + "An open edge or one shared by three or more faces may be marked too: the first has nothing to smooth across, and the second is hard anyway. "
        + "A seam is an edge where the UV layout is cut: unwrap cuts the mesh open along its seams (and its open edges) so it can lie flat, and the layout look draws them. A seam does not make an edge sharp, and a split in the UVs between two faces needs no seam to exist (set_uvs gives each face its own UVs at a shared vertex). UVs run as in glTF (origin top-left, v down; see set_uvs). "
        + "A hard edge changes shading, in renders and in the normals export writes; a seam changes neither: it only guides unwrap and is drawn in the uvs layout look. "
        + "Both marks are kept with the mesh through move, extrude, subdivide and the cuts (plane_cut, loop_cut and knife keep the mark on every piece of an edge they split; a mark on an edge an edit removes is dropped), and saved in the Carve file. "
        + PartTargetText
        + "Returns JSON {mesh, shading, marked:[[a,b]...], hard_edges:[[a,b]...]} with every hard edge of the mesh, and, when seam was given, seams:[[a,b]...] with every seam of the mesh (get_mesh reads them back as seams). "
        + "Errors, each a tool error that changes nothing: no mesh yet, a pair that is not an edge of the mesh (named), an entry that is not a pair. "
        + "Worked example on define_mesh's unit cube, creasing the edge between vertices 0 and 1 and cutting a seam along the edge between 0 and 3 (the left edge of face 0's four corners [0,3,2,1]):\n"
        + ExampleMarker + """{"edges":[[0,1]],"hard":true}""" + "\n"
        + ExampleMarker + """{"edges":[[0,3]],"seam":true}""";

    public static McpServerTool Create() => MeshToolSupport.Create<MarkEdgesTool>("mark_edges", Text, WithVerbosity(WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["edges"] = new JsonObject
            {
                ["description"] = "Edges to mark, each a [a, b] pair of vertex ids joined by an edge.",
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "integer" },
                    ["minItems"] = 2,
                    ["maxItems"] = 2,
                },
                ["minItems"] = 1,
            },
            ["hard"] = new JsonObject { ["description"] = "true to make the edges hard, false to make them soft. The default is true when seam is not given; when seam is given alone, hard edges are left alone.", ["type"] = "boolean" },
            ["seam"] = new JsonObject { ["description"] = "true to mark the edges as UV seams, false to clear them. A call with only seam leaves hard edges alone.", ["type"] = "boolean" },
        },
        ["required"] = new JsonArray("edges"),
    })));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? edges = null, JsonElement? hard = null, JsonElement? seam = null, JsonElement? mesh = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("mark_edges", context, "edges", "hard", "seam", "mesh", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var meshName = MeshName(mesh, errors);
        var unknown = errors.Count;
        var pairs = Pairs("edges", edges, errors);
        if (pairs is null && errors.Count == unknown) errors.Add("edges: required but missing");
        else if (pairs is { Count: 0 }) errors.Add("edges: nothing to mark: list at least one edge");
        // hard keeps its old default (true) unless seam is given; a flag given alone leaves the other mark alone.
        var isHard = Absent(hard) ? Absent(seam) : OptionalBool("hard", hard, errors);
        var changesHard = !Absent(hard) || Absent(seam);
        var isSeam = !Absent(seam) && OptionalBool("seam", seam, errors);
        if (errors.Count > 0) return Failed("mark_edges", errors);

        var snapshot = store.Rework("mark_edges", meshName, m =>
        {
            var shade = Shading.MarkEdges(m, pairs!, isHard, out var notEdges);
            if (notEdges.Count > 0)
                return (null, [.. notEdges.Select(e => $"edges[{e.Lo}-{e.Hi}]: not an edge of the mesh; select with kind edges lists the real edges")]);
            var result = changesHard ? m.WithShade(shade) : m;
            if (!Absent(seam))
            {
                var uvs = (m.Uvs ?? MeshUvs.None).WithSeams(pairs!, isSeam);
                result = result.WithUvs(uvs.IsEmpty ? null : uvs);
            }
            return (result, []);
        }, out var refused, out var problem);
        if (problem is not null) return Failed("mark_edges", [problem]);
        if (refused.Count > 0) return Failed("mark_edges", refused);
        if (snapshot is null) return Error("mark_edges: " + NoMesh);

        var target = snapshot.Model.Target(meshName, out _)!;
        var body = new Dictionary<string, object>
        {
            ["mesh"] = target.Name,
            ["shading"] = target.Mesh.ShadingMode == ShadingMode.Smooth ? "smooth" : "flat",
            ["marked"] = pairs!.Select(p => (Lo: Math.Min(p.A, p.B), Hi: Math.Max(p.A, p.B))).Distinct().Select(p => new[] { p.Lo, p.Hi }).ToArray(),
            ["hard_edges"] = target.Mesh.HardEdges.Select(e => new[] { e.Lo, e.Hi }).ToArray(),
        };
        if (!Absent(seam)) body["seams"] = target.Mesh.Seams.Select(e => new[] { e.Lo, e.Hi }).ToArray();
        return Quiet(Ok(body), level);
    }
}
