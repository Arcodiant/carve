using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class SetUvsTool(ModelStore store)
{
    public const string Text =
        "Gives faces UVs by coordinates, or makes them unmapped: set_uvs(faces, clear, mesh). faces is a list of {\"face\": id, \"uv\": [[u, v], ...]} with one [u, v] pair per corner of the face, and clear is a list of face ids to make unmapped; give either or both. "
        + "A corner is one entry of a face's loop, in the order get_mesh lists the face's vertices: the outer loop first, then each hole's loop in turn, so a face with holes takes one pair for every vertex of the loop and of every hole. "
        + "The pair at a corner is that vertex's place on the texture as seen from that face, so two faces that share a vertex may give it different UVs, and a split needs no seam. "
        + "UVs run as in glTF (origin top-left, v down): (0, 0) is the texture's top-left, u runs right and v runs down, so (1, 1) is the bottom-right. A value outside 0..1 is allowed and tiles the texture; any finite number is legal. "
        + "A face with no UVs is unmapped; an edit keeps a face's UVs (move keeps them, an extrude's cap keeps its own and its new walls are unmapped, subdivide interpolates them linearly, undo restores them), and the Carve file keeps them. "
        + "One call is one step for undo, and a call that fails changes nothing. "
        + "Returns JSON {mesh, set, cleared, unmapped}: how many faces were given UVs, how many were cleared, and how many faces of the mesh are still unmapped; get_mesh reads the UVs back as uvs. "
        + "Errors, each a tool error that changes nothing and names the element: no mesh yet, a face that is not a face of the mesh or is deleted, a pair count that is not the face's corner count (it says how many corners the face has), a value that is not a finite number, a face both set and cleared (or listed twice), an entry that is not {face, uv}, an unknown argument. "
        + "Worked example on define_mesh's unit cube: face 0 is [0,3,2,1], four corners, here mapped to the whole texture, corner 0 at the top-left and the others following round the loop:\n"
        + ExampleMarker + """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<SetUvsTool>("set_uvs", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["faces"] = new JsonObject
            {
                ["description"] = "Faces to give UVs: each {face, uv}, uv one [u, v] pair per corner of the face in loop order (outer loop, then each hole). glTF axes: (0,0) top-left, v down. Values outside 0..1 tile.",
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["face"] = new JsonObject { ["type"] = "integer" },
                        ["uv"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "number" },
                                ["minItems"] = 2,
                                ["maxItems"] = 2,
                            },
                        },
                    },
                    ["required"] = new JsonArray("face", "uv"),
                },
            },
            ["clear"] = new JsonObject
            {
                ["description"] = "Ids of faces to make unmapped.",
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "integer" },
            },
        },
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? faces = null, JsonElement? clear = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("set_uvs", context, "faces", "clear", "mesh");
        var meshName = MeshName(mesh, errors);
        var given = new List<(int Face, Vector2[] Uv)>();
        var seen = new HashSet<int>();
        if (!Absent(faces))
        {
            if (faces!.Value.ValueKind != JsonValueKind.Array)
                errors.Add($"faces: expected an array of {{face, uv}} entries, got {Describe(faces.Value)}");
            else
            {
                var k = 0;
                foreach (var entry in faces.Value.EnumerateArray())
                {
                    ParseEntry(entry, k++, given, seen, errors);
                }
            }
        }
        var cleared = Ids("clear", clear, errors) ?? [];
        cleared = [.. cleared.Distinct()];
        if (errors.Count == 0 && given.Count == 0 && cleared.Count == 0) errors.Add("faces: nothing to do: give faces to set, clear to unmap, or both");
        foreach (var f in cleared.Where(seen.Contains))
            errors.Add($"faces[{f}]: face {f} is both set and cleared; give it in faces or in clear, not both");
        if (errors.Count > 0) return Failed("set_uvs", errors);

        var snapshot = store.Rework("set_uvs", meshName, m =>
        {
            var problems = new List<string>();
            foreach (var f in cleared)
            {
                if (f >= m.Faces.Count) problems.Add($"clear[{f}]: face {f} is not a face of the mesh, which has {m.Faces.Count}");
                else if (m.Faces[f].IsDeleted) problems.Add($"clear[{f}]: face {f} is deleted");
            }
            var uvs = (m.Uvs ?? MeshUvs.None)
                .WithFaces(given.Select(g => KeyValuePair.Create(g.Face, (IReadOnlyList<Vector2>?)g.Uv)));
            problems.AddRange(MeshUvs.Check(m, uvs).Where(e => given.Any(g => e.Element.StartsWith($"faces[{g.Face}]", StringComparison.Ordinal)))
                .Select(e => $"{e.Element}: {e.Message}"));
            if (problems.Count > 0) return (null, problems);
            uvs = uvs.WithFaces(cleared.Select(f => KeyValuePair.Create(f, (IReadOnlyList<Vector2>?)null)));
            return (m.WithUvs(uvs.IsEmpty ? null : uvs), problems);
        }, out var refused, out var problem);
        if (problem is not null) return Failed("set_uvs", [problem]);
        if (refused.Count > 0) return Failed("set_uvs", refused);
        if (snapshot is null) return Error("set_uvs: " + NoMesh);

        var target = snapshot.Model.Target(meshName, out _)!;
        var unmapped = target.Mesh.LiveFaces.Count(f => target.Mesh.Uvs?.IsMapped(f) != true);
        return Ok(new { mesh = target.Name, set = given.Count, cleared = cleared.Count, unmapped });
    }

    private static void ParseEntry(JsonElement entry, int k, List<(int Face, Vector2[] Uv)> given, HashSet<int> seen, List<string> errors)
    {
        var at = $"faces: the {Ordinal(k)} listed entry";
        if (entry.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{at}: expected {{\"face\": id, \"uv\": [[u, v], ...]}}, got {Describe(entry)}");
            return;
        }
        foreach (var p in entry.EnumerateObject())
            if (p.Name is not ("face" or "uv"))
                errors.Add($"{at}: {p.Name}: not a key of an entry; an entry is {{face, uv}}");
        if (!entry.TryGetProperty("face", out var faceElement)) { errors.Add($"{at}: face: required but missing"); return; }
        if (!TryIndex(faceElement, out var face)) { errors.Add($"{at}: face: expected an integer id, got {Describe(faceElement)}"); return; }
        var name = $"faces[{face}]";
        if (!seen.Add(face)) errors.Add($"{name}: face {face} is listed more than once");
        if (!entry.TryGetProperty("uv", out var uv)) { errors.Add($"{name}.uv: required but missing"); return; }
        if (uv.ValueKind != JsonValueKind.Array) { errors.Add($"{name}.uv: expected an array of [u, v] pairs, got {Describe(uv)}"); return; }
        var corners = new List<Vector2>();
        var ok = true;
        var c = 0;
        foreach (var pair in uv.EnumerateArray())
        {
            var cname = $"{name}.uv[{c}]";
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2)
            {
                errors.Add(pair.ValueKind == JsonValueKind.Array
                    ? $"{cname}: a UV is a pair of numbers, [u, v]; got {pair.GetArrayLength()} numbers"
                    : $"{cname}: a UV is a pair of numbers, [u, v]; got {Describe(pair)}");
                ok = false;
            }
            else
            {
                var xy = new float[2];
                for (var j = 0; j < 2; j++)
                {
                    if (UvNumber($"{cname}[{j}]", pair[j], errors) is { } x) xy[j] = x;
                    else ok = false;
                }
                corners.Add(new Vector2(xy[0], xy[1]));
            }
            c++;
        }
        if (ok) given.Add((face, [.. corners]));
    }
}
