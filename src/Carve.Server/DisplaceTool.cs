using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>T-223: displace, an edit that moves vertices along their normals by a Patina greyscale output or seeded noise.</summary>
public sealed class DisplaceTool(ModelStore store, ToolFolder toolFolder)
{
    public const string Example = """{"source":{"noise":{"seed":7,"scale":0.35,"octaves":5}},"amount":0.6}""";

    public const string Text =
        "Roughens a mesh in its geometry: moves each selected vertex along its vertex normal (its faces' normals weighted by area, taken before any vertex moves) by amount x (h - midlevel), where h is a height 0 to 1, as one edit that one undo takes back. "
        + "Meant for a mesh with vertices to spare (subdivide it first): a rock, a cliff, bark, a dented hull. "
        + "source is one of: {graph, output}, a Patina .texgraph file (absolute or relative to carve's working directory) and one of its greyscale outputs (default height), evaluated at the graph's own size and sampled bilinearly at the vertex's UVs, wrapping; a vertex on a UV seam takes the mean of its corners' samples, so the seam does not tear; the mesh needs UVs (project_uvs or unwrap). "
        + "Or {noise: {seed, scale, octaves}}: seeded 3D gradient noise summed over octaves (1 to 8, default 4; each twice the detail at half the strength) at the vertex's position in the mesh's own coordinates, scale the size in metres of the largest bumps (default 1), seed a whole number from -2147483648 to 2147483647 (default 0); it needs no UVs and has no seams, and the same arguments always give the same positions. "
        + "amount is in metres and may be negative (dents instead of bumps); midlevel (0 to 1, default 0.5) is the height that does not move; axis [x, y, z], when given, replaces every normal with that one direction (for terrain). "
        + "Give vertices, faces (every corner) or group (a face group from define_group) to displace part of the mesh, or none of them for every vertex. "
        + "Ids, faces, UVs, groups, colours and weights are kept; only positions change. A vertex on no face has no normal: without axis it stays, and a warning names it. "
        + "Returns JSON {vertices, faces, edges, closed, bounds:{min,max}, warnings:[], created:{vertices:[],faces:[]}, deleted:{vertices:[], faces:[]}, split:[], moved:[vertex ids, ascending], largest: the largest offset in metres}; "
        + "the not-planar warnings fold into one line, \"N faces are not planar (expected after displace)\", and a warning says when the mesh is too coarse for the source to show (fewer than two vertices to a bump). "
        + "Errors, each a tool error that changes nothing: no source, or both graph and noise; a graph file that is missing (the error lists the .texgraph files in its folder), will not read or has faults; an output the graph has not, or a colour one; a mesh without UVs for a graph; amount missing or zero; midlevel outside 0 to 1; a zero axis; octaves outside 1 to 8; scale not above 0; an unknown vertex, face or group; a selection that matches no vertex; a face that would cross itself. "
        + "Needs a mesh, from define_mesh or open, first; when the model has several meshes, mesh names the one to displace. "
        + PartSpaceText
        + "Worked example on define_mesh's unit cube after subdivide levels 3, a rough boulder:\n"
        + ExampleMarker + Example;

    public static McpServerTool Create() => MeshToolSupport.Create<DisplaceTool>("displace", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["source"] = new JsonObject
            {
                ["description"] = "Where the heights come from: {\"graph\": \"rock.texgraph\", \"output\": \"height\"} or {\"noise\": {\"seed\": 0, \"scale\": 1, \"octaves\": 4}}.",
                ["type"] = "object",
            },
            ["amount"] = new JsonObject { ["description"] = "Metres moved for a height of 1 above midlevel; negative dents.", ["type"] = "number" },
            ["midlevel"] = new JsonObject { ["description"] = "The height, 0 to 1, that stays where it is; default 0.5.", ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
            ["axis"] = new JsonObject
            {
                ["description"] = "A fixed direction [x, y, z] to move along in place of each vertex's normal.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "number" },
                ["minItems"] = 3,
                ["maxItems"] = 3,
            },
            ["vertices"] = new JsonObject { ["description"] = "Vertex ids to displace.", ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } },
            ["faces"] = new JsonObject { ["description"] = "Face ids whose corners to displace.", ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } },
            ["group"] = new JsonObject { ["description"] = "A face group from define_group whose corners to displace.", ["type"] = "string" },
        },
        ["required"] = new JsonArray("source", "amount"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? source = null, JsonElement? amount = null,
        JsonElement? midlevel = null, JsonElement? axis = null, JsonElement? vertices = null, JsonElement? faces = null,
        JsonElement? group = null, JsonElement? mesh = null)
    {
        const string tool = "displace";
        var errors = UnknownArguments(tool, context, "source", "amount", "midlevel", "axis", "vertices", "faces", "group", "mesh");
        var name = MeshName(mesh, errors);
        var by = Number("amount", amount, errors);
        var mid = Number("midlevel", midlevel, errors, required: false) ?? 0.5f;
        Vector3? direction = Absent(axis) ? null : Vec3("axis", axis, errors);
        var v = Ids("vertices", vertices, errors);
        var f = Ids("faces", faces, errors);
        var groupName = OptionalString("group", group, errors);
        if (groupName is not null && (v is not null || f is not null)) errors.Add("group: give group, or vertices and faces, not both");
        var from = Source(source, errors);
        if (errors.Count > 0) return Failed(tool, errors);

        DisplaceReport? report = null;
        string? noGroup = null;
        // The group is read inside the store's lock, on the mesh the edit is given, as set_weights reads it.
        var snapshot = store.Edit(tool, name, m =>
        {
            var chosen = f;
            if (groupName is not null)
            {
                chosen = m.Style?.FacesOf(groupName) is { } grouped ? [.. grouped] : null;
                if (chosen is null) noGroup = NoGroup(m.Style, groupName);
            }
            // An unknown group is refused below; an empty face list makes the Core refuse without trying the edit.
            return Displacement.Displace(m, v, from!, by!.Value, mid, direction, out report, noGroup is null ? chosen : []);
        }, out var result, out var problem);
        if (problem is not null) return Failed(tool, [problem]);
        if (noGroup is not null) return Failed(tool, [noGroup]);
        if (snapshot is null) return Error($"{tool}: " + NoMesh);
        if (!result!.Succeeded) return Failed(tool, result.Errors.Select(x => x.ToString()));

        var edited = snapshot.Model.Find(name ?? snapshot.Model.Meshes[0].Name)!;
        var body = Report(edited);
        body["warnings"] = SubdivideTool.Warnings(MeshReport.Create(edited), tool).Concat(report!.Warnings).ToArray();
        body["created"] = new { vertices = result.NewVertices, faces = result.NewFaces };
        body["deleted"] = Deleted(result.Deleted);
        body["split"] = Split(result.Split);
        body["moved"] = result.MovedVertices;
        body["largest"] = Math.Round(report.Largest, 6);
        return Ok(body);
    }

    /// <summary>The source argument as a <see cref="DisplaceSource"/>: a graph's output read into a height field, or noise.</summary>
    private DisplaceSource? Source(JsonElement? value, List<string> errors)
    {
        if (Absent(value))
        {
            errors.Add("source: required but missing; give {\"graph\": ..., \"output\": ...} or {\"noise\": {...}}");
            return null;
        }
        var x = value!.Value;
        if (x.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"source: expected an object, got {Describe(x)}");
            return null;
        }
        var keys = x.EnumerateObject().Select(p => p.Name).ToList();
        foreach (var k in keys.Where(k => k is not ("graph" or "output" or "noise")))
            errors.Add($"source.{k}: source takes graph and output, or noise");
        var hasGraph = x.TryGetProperty("graph", out var graph);
        var hasNoise = x.TryGetProperty("noise", out var noise);
        if (hasGraph == hasNoise)
        {
            errors.Add(hasGraph ? "source: give graph or noise, not both" : "source: give graph (and output, default height) or noise");
            return null;
        }
        if (hasNoise)
        {
            if (x.TryGetProperty("output", out _)) errors.Add("source.output: output goes with graph, not noise");
            return Noise(noise, errors);
        }
        x.TryGetProperty("output", out var output);
        return Graph(graph, output, errors);
    }

    private static NoiseSource? Noise(JsonElement noise, List<string> errors)
    {
        if (noise.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"source.noise: expected an object {{seed, scale, octaves}}, got {Describe(noise)}");
            return null;
        }
        var before = errors.Count;
        foreach (var p in noise.EnumerateObject().Where(p => p.Name is not ("seed" or "scale" or "octaves")))
            errors.Add($"source.noise.{p.Name}: noise takes seed, scale and octaves");
        int Whole(string key, int fallback)
        {
            if (!noise.TryGetProperty(key, out var n) || n.ValueKind == JsonValueKind.Null) return fallback;
            if (n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var i)) return i;
            errors.Add($"source.noise.{key}: expected a whole number from {int.MinValue} to {int.MaxValue}, got {n.GetRawText()}");
            return fallback;
        }
        var seed = Whole("seed", 0);
        var octaves = Whole("octaves", 4);
        var scale = 1.0;
        if (noise.TryGetProperty("scale", out var s) && s.ValueKind != JsonValueKind.Null)
        {
            if (s.ValueKind == JsonValueKind.Number && double.IsFinite(s.GetDouble())) scale = s.GetDouble();
            else errors.Add($"source.noise.scale: expected a finite number of metres, got {(s.ValueKind == JsonValueKind.Number ? s.GetRawText() : Describe(s))}");
        }
        return errors.Count == before ? new NoiseSource(seed, scale, octaves) : null;
    }

    private FieldSource? Graph(JsonElement graph, JsonElement output, List<string> errors)
    {
        if (graph.ValueKind != JsonValueKind.String)
        {
            errors.Add($"source.graph: expected a .texgraph path, got {Describe(graph)}");
            return null;
        }
        var outputName = "height";
        if (output.ValueKind == JsonValueKind.String) outputName = output.GetString()!;
        else if (output.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            errors.Add($"source.output: expected an output's name, got {Describe(output)}");
            return null;
        }

        var target = ToolPaths.Resolve("displace", graph.GetString(), TexGraphFile.Extension, out var pathError, toolFolder.Directory, verb: "reads");
        if (target is null)
        {
            errors.Add("source.graph: " + pathError!["displace: ".Length..]);
            return null;
        }
        if (!File.Exists(target.FullPath))
        {
            errors.Add("source.graph: " + DefineMaterialTool.Missing(target, TexGraphFile.Extension));
            return null;
        }

        TexGraphReadResult read;
        try
        {
            read = TexGraphFile.ReadFile(target.FullPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            errors.Add($"source.graph: {target.FullPath} could not be read: {e.Message}");
            return null;
        }
        if (read.Graph is not { } loaded)
        {
            errors.Add($"source.graph: {target.FullPath} is not a texture graph: {read.Error}");
            return null;
        }
        if (read.Faults.Count > 0)
        {
            errors.Add($"source.graph: {target.FullPath} has {read.Faults.Count.ToString(CultureInfo.InvariantCulture)} fault(s), so it does not evaluate: "
                + string.Join("; ", read.Faults.Select(x => x.Message)) + "; fix them in patina");
            return null;
        }

        var evaluator = new GraphEvaluator(loaded);
        if (!evaluator.Has(outputName))
        {
            errors.Add($"source.output: {target.FileName} has no {outputName} output; it has {string.Join(", ", evaluator.Outputs)}");
            return null;
        }
        if (evaluator.KindOf(outputName) != PortKind.Grey)
        {
            var greys = evaluator.Outputs.Where(o => evaluator.KindOf(o) == PortKind.Grey).ToList();
            errors.Add($"source.output: {outputName} is a colour output, and displace needs a greyscale one; "
                + (greys.Count > 0 ? $"{target.FileName}'s greyscale outputs are {string.Join(", ", greys)}" : $"{target.FileName} has none"));
            return null;
        }

        var size = loaded.Size;
        var values = new float[size * size];
        evaluator.Evaluate(outputName, size, tile =>
        {
            for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                tile.Row(y).CopyTo(values.AsSpan(y * size + tile.Rect.X, tile.Rect.Width));
        });
        return new FieldSource(new HeightField(size, size, values), outputName);
    }
}
