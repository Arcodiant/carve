using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>plane_cut</c> tool (T-65): <see cref="PlaneCut.Cut"/> on the store's mesh, reported as every edit is.</summary>
public sealed class PlaneCutTool(ModelStore store)
{
    public const string Text =
        "Cuts the current mesh with a plane through point with normal; above is the side the normal points to. "
        + "keep both (the default) only splits: every face the plane crosses is split into pieces along it, a new vertex where it crosses an edge (shared by both faces on that edge, which keep their ids). "
        + "keep above or below also deletes every face wholly on the other side, with the vertices left with no face, as delete does; with fill (default true) each closed loop of cut edges is then capped with a new face wound outward, two nested loops giving a cap with a hole. "
        + "A cut loop that is not closed (an open mesh) is left open, with a warning naming its ends. On a smooth mesh the caps' border edges are marked hard. "
        + "A face lying in the plane is not split: with keep it stays, with its id and colour, whichever side is kept, and no cap is laid over it. faces limits the cut to those faces (keep both only). "
        + "A split face keeps its id for the piece holding its first corner (or the next corner off the cut); the other pieces are appended. Pieces take the face's colour and groups, split hard edges stay hard, new edges are soft, and a new vertex blends its edge's ends' weights. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[]}, deleted:{vertices:[], faces:[]}, split:[{face, pieces}]}: split lists each split face with its pieces, its own id first; "
        + "a piece the split appended and keep then removed is in both split and deleted, and created lists only what is left. "
        + "Bad input (a zero or non-finite normal, a non-finite point, a plane that touches no face, a cut that would leave nothing, faces with keep above or below, an unknown or deleted face, an argument plane_cut does not take) "
        + "is a tool error that lists every problem; the mesh is left unchanged and nothing is added to undo. One undo step undoes the whole cut. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to cut. "
        + PartSpaceText
        + "Worked example on define_mesh's unit cube: a cut at y = 0.5 keeping below with fill gives a half-height closed box (8 vertices, 6 faces):\n"
        + ExampleMarker + """{"point":[0,0.5,0],"normal":[0,1,0],"keep":"below"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<PlaneCutTool>("plane_cut", Text, WithVerbosity(WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["point"] = new JsonObject
            {
                ["description"] = "A point on the plane, [x, y, z].",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "number" },
                ["minItems"] = 3,
                ["maxItems"] = 3,
            },
            ["normal"] = new JsonObject
            {
                ["description"] = "The plane's normal, [x, y, z], any non-zero length; it points to the side called above.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "number" },
                ["minItems"] = 3,
                ["maxItems"] = 3,
            },
            ["keep"] = new JsonObject
            {
                ["description"] = "both (default: only split), above (the side the normal points to) or below.",
                ["type"] = "string",
                ["enum"] = new JsonArray("both", "above", "below"),
            },
            ["fill"] = new JsonObject
            {
                ["description"] = "With keep above or below, cap each closed loop of the cut (default true).",
                ["type"] = "boolean",
            },
            ["faces"] = new JsonObject
            {
                ["description"] = "Face ids to limit the cut to (keep both only); every face when left out.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
            },
        },
        ["required"] = new JsonArray("point", "normal"),
    })));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? point = null, JsonElement? normal = null, JsonElement? keep = null, JsonElement? fill = null,
        JsonElement? faces = null, JsonElement? mesh = null, JsonElement? verbosity = null)
    {
        var errors = UnknownArguments("plane_cut", context, "point", "normal", "keep", "fill", "faces", "mesh", "verbosity");
        var level = ReadVerbosity(verbosity, errors);
        var name = MeshName(mesh, errors);
        var p = Vec3("point", point, errors);
        var n = Vec3("normal", normal, errors);
        var side = PlaneKeep.Both;
        switch (OptionalString("keep", keep, errors))
        {
            case null or "both": break;
            case "above": side = PlaneKeep.Above; break;
            case "below": side = PlaneKeep.Below; break;
            case var other: errors.Add($"keep: expected both, above or below, got \"{other}\""); break;
        }
        var doFill = Absent(fill) || OptionalBool("fill", fill, errors);
        var f = Ids("faces", faces, errors);
        if (errors.Count > 0) return Failed("plane_cut", errors);

        IReadOnlyList<string> warnings = [];
        return Edit(store, "plane_cut", name, m =>
        {
            var r = PlaneCut.Cut(m, p!.Value, n!.Value, side, doFill, f);
            warnings = r.Warnings;
            return r.Edit;
        }, (body, _) =>
        {
            if (warnings.Count > 0) body["warnings"] = ((string[])body["warnings"]).Concat(warnings).ToArray();
        }, level);
    }
}
