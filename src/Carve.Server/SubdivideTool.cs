using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class SubdivideTool(ModelStore store)
{
    public const string Text =
        "Smooths a mesh by Catmull-Clark subdivision, levels times (1 to 3), as one edit that one undo takes back: every face becomes one quad per corner and the shape rounds off towards a smooth surface. "
        + "Each level multiplies the faces by about four (a quad becomes four quads), so a 6-face cube has 24 faces after 1 level, 96 after 2 and 384 after 3. "
        + "Ids never change: every vertex keeps its id and moves to its smoothed position, and each face keeps its id as its first child quad (the one at its first corner). "
        + "Appended after the existing vertices, per level: one face point per face, in face order, then one edge point per edge, ordered by lower vertex id and then higher; "
        + "appended after the existing faces: each face's other children, face by face, child k at corner k. "
        + "Hard edges (mark_edges) stay creased: a hard edge's two halves are hard, and the surface keeps a sharp line along it. "
        + "An open edge, one with a face on one side only, follows the boundary rule, so an open rim stays a rim. A vertex where three or more hard or open edges meet is a corner and stays where it is. "
        + "New faces take their face's colour and groups; new vertices take bone weights blended from the vertices they are made from, at most four bones each. The shading mode is kept: set_shading smooth to see a soft surface. "
        + "The new quads are generally not flat, which is expected here, so this result folds the not-planar warnings into one line, \"N faces are not planar (expected after subdivide)\", after the other warnings; get_mesh, check, export, open and move name the first 10 faces and fold the rest, and select faces where non_planar names them. "
        + "Returns JSON {vertices, faces, edges, triangles, closed, bounds:{min,max}, warnings:[], levels, created:{vertices:{first, count}, faces:{first, count}}, deleted:{vertices:[], faces:[]}, split:[]}, triangles counting the whole model; created.vertices is the block of slots appended after the existing vertices, and when the mesh has a deleted face each level makes a face point for it, so the block holds one deleted slot (null in get_mesh) per deleted face per level; "
        + "triangles is for weighing against a game's budget, which export warns on from the game's carve-limits.json; subdivide gives no game-limit warning of its own. "
        + "Bad input (levels missing, not a whole number or outside 1 to 3, an argument subdivide does not take) is a tool error; the mesh is left unchanged and nothing is added to undo. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to subdivide. "
        + "Worked example on define_mesh's unit cube, one level:\n"
        + ExampleMarker + """{"levels":1}""";

    public static McpServerTool Create() => MeshToolSupport.Create<SubdivideTool>("subdivide", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["levels"] = new JsonObject
            {
                ["description"] = "How many times to subdivide, 1 to 3; each level makes about four times the faces.",
                ["type"] = "integer",
                ["minimum"] = 1,
                ["maximum"] = MeshEdits.MaxSubdivideLevels,
            },
        },
        ["required"] = new JsonArray("levels"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? levels = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("subdivide", context, "levels", "mesh");
        var name = MeshName(mesh, errors);
        var n = Levels(levels, errors);
        if (errors.Count > 0) return Failed("subdivide", errors);

        var before = 0;
        var snapshot = store.Edit("subdivide", name, m => { before = m.Vertices.Count; return MeshEdits.Subdivide(m, n!.Value); }, out var result, out var problem);
        if (problem is not null) return Failed("subdivide", [problem]);
        if (snapshot is null) return Error("subdivide: " + NoMesh);
        if (!result!.Succeeded) return Failed("subdivide", result.Errors.Select(x => x.ToString()));

        var edited = snapshot.Model.Find(name ?? snapshot.Model.Meshes[0].Name)!;
        // The appended block: a deleted face's face point takes a slot in it as a tombstone, so it is not NewVertices.Count.
        var appended = edited.Vertices.Count - before;
        var triangles = snapshot.Model.Meshes.Sum(m => m.Mesh.Faces.Sum(f => f.TriangleCount));
        var report = MeshReport.Create(edited);
        var warnings = Warnings(report);
        var ordered = new Dictionary<string, object>
        {
            ["vertices"] = report.VertexCount,
            ["faces"] = report.FaceCount,
            ["edges"] = report.EdgeCount,
            ["triangles"] = triangles,
            ["closed"] = report.Closed,
            ["bounds"] = new { min = Array(report.BoundsMin), max = Array(report.BoundsMax) },
            ["warnings"] = warnings,
            ["levels"] = n!.Value,
            ["created"] = new
            {
                vertices = new { first = edited.Vertices.Count - appended, count = appended },
                faces = new { first = edited.Faces.Count - result.NewFaces.Count, count = result.NewFaces.Count },
            },
            ["deleted"] = Deleted(result.Deleted),
            ["split"] = Split(result.Split),
        };
        return Ok(ordered);
    }

    /// <summary>
    /// The report's warnings with the not-planar ones folded into one line at the end: Catmull-Clark quads are
    /// almost never flat, and one line per face would bury the rest (the person's ruling, 2026-11-10). Only this
    /// result folds them; the other reports fold at 10 faces (MeshWarningFold).
    /// </summary>
    internal static List<string> Warnings(MeshReport report)
    {
        var all = report.Warnings;
        var warnings = all.Where(w => w.Kind != MeshWarningKind.NonPlanarFace).Select(w => w.ToString()).ToList();
        var nonPlanar = all.Count(w => w.Kind == MeshWarningKind.NonPlanarFace);
        if (nonPlanar > 0)
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"{nonPlanar} {(nonPlanar == 1 ? "face is" : "faces are")} not planar (expected after subdivide)"));
        return warnings;
    }

    private static int? Levels(JsonElement? value, List<string> errors)
    {
        if (value is not { } x || x.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            errors.Add("levels: required but missing");
            return null;
        }
        if (x.ValueKind != JsonValueKind.Number || !x.TryGetInt32(out var n))
        {
            errors.Add($"levels: expected a whole number 1 to {MeshEdits.MaxSubdivideLevels}, got {x.GetRawText()}");
            return null;
        }
        if (n < 1 || n > MeshEdits.MaxSubdivideLevels)
        {
            errors.Add($"levels: {n} is outside 1 to {MeshEdits.MaxSubdivideLevels}");
            return null;
        }
        return n;
    }
}
