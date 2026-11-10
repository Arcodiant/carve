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
        + "The new quads are generally not flat, which is expected here, so this result folds the not-planar warnings into one line, \"N faces are not planar (expected after subdivide)\", after the other warnings; get_mesh, check and export still list each face, and select faces where non_planar names them. "
        + "Returns JSON {vertices, faces, edges, triangles, closed, bounds:{min,max}, warnings:[], levels, created:{vertices:{first, count}, faces:{first, count}}}, triangles counting the whole model; "
        + "a warning is added when the model has more than 5000 triangles, the most a game character should keep to. "
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

        var snapshot = store.Edit("subdivide", name, m => MeshEdits.Subdivide(m, n!.Value), out var result, out var problem);
        if (problem is not null) return Failed("subdivide", [problem]);
        if (snapshot is null) return Error("subdivide: " + NoMesh);
        if (!result!.Succeeded) return Failed("subdivide", result.Errors.Select(x => x.ToString()));

        var edited = snapshot.Model.Find(name ?? snapshot.Model.Meshes[0].Name)!;
        var triangles = snapshot.Model.Meshes.Sum(m => m.Mesh.Faces.Sum(f => f.Count - 2));
        var body = Report(edited);
        var warnings = Warnings(edited);
        if (triangles > SkinnedExport.MaxTriangles)
            warnings.Insert(0, string.Create(CultureInfo.InvariantCulture,
                $"the model has {triangles} triangles, above the {SkinnedExport.MaxTriangles} a game character should keep to"));
        var ordered = new Dictionary<string, object>
        {
            ["vertices"] = body["vertices"],
            ["faces"] = body["faces"],
            ["edges"] = body["edges"],
            ["triangles"] = triangles,
            ["closed"] = body["closed"],
            ["bounds"] = body["bounds"],
            ["warnings"] = warnings,
            ["levels"] = n!.Value,
            ["created"] = new
            {
                vertices = new { first = edited.Vertices.Count - result.NewVertices.Count, count = result.NewVertices.Count },
                faces = new { first = edited.Faces.Count - result.NewFaces.Count, count = result.NewFaces.Count },
            },
        };
        return Ok(ordered);
    }

    /// <summary>
    /// The report's warnings with the not-planar ones folded into one line at the end: Catmull-Clark quads are
    /// almost never flat, and one line per face would bury the rest (the person's ruling, 2026-11-10). Only this
    /// result folds them; get_mesh, check and export list each face as before.
    /// </summary>
    internal static List<string> Warnings(PolygonMesh mesh)
    {
        var all = MeshReport.Create(mesh).Warnings;
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
