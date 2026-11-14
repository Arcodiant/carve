using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class ProjectUvsTool(ModelStore store)
{
    private static readonly string[] Methods = ["planar", "box", "cylindrical"];

    public const string Text =
        "Gives faces UVs by projecting them: project_uvs(method, faces, into, mesh, then the method's options). method is planar, box or cylindrical. "
        + "faces are the face ids to map (default every live face); other faces keep the UVs they have. into is [u0, v0, u1, v1], the rectangle of UV space the result fills (default [0, 0, 1, 1]); u1 must exceed u0 and v1 exceed v0. "
        + "UVs run as in glTF: (0, 0) is the texture's top-left, u runs right and v runs down. Every method gives one UV per corner of each face (a face with holes, one for every vertex of the loop and of each hole), and a face seen from its own outside comes out unflipped: its corners run counterclockwise on screen with v down. "
        + "planar suits one side of a thing (a wall, a sign, a lid): it lays the faces out as render would show them from view (front, back, +x, -x, +y, -y, +z or -z; default front) or from direction [x, y, z] (from the point looked at toward the eye, any length), u to the image's right and v down, with render's up (+Y, or -Z within 1 degree of vertical). Give view or direction, not both. One uniform scale fits the projected faces into into keeping their aspect, centred; faces seen edge-on come out with no area. "
        + "box suits crates, rooms and buildings: each face goes to the axis its normal leans on most (a tie goes to x, then y, then z) and is laid out as that axis's head-on view shows it. The six groups sit in a 3 x 2 grid of equal cells inside into, +x, -x, +y on the top row and -y, +z, -z below, all at one scale so texel density is even, each centred in its cell; a group with no faces leaves its cell empty. box takes no options. "
        + "cylindrical suits barrels, posts and limbs: axis [x, y, z] (default [0, 1, 0]), centre [x, y, z] a point on the axis (default the middle of the faces' bounds) and seam [x, y, z] a direction across the axis (default [0, 0, -1], the back). u is the angle round the axis from the seam, increasing to the right as seen from outside with the axis pointing up; v is the distance along the axis, downward from the faces' highest point. Arc length (at the faces' mean radius) and height share one scale, fitted into into keeping aspect. A face that crosses the seam keeps its corners together, so it may run past the right edge of into. A face square to the axis (a cap) comes out with no area: project caps planar, along the axis. "
        + "One call is one step for undo, and a call that fails changes nothing. "
        + "Returns JSON {mesh, method, mapped, bounds, axes}: mapped is the ids of the faces given UVs, bounds is [u0, v0, u1, v1] of the UVs written, and for box axes says which faces went to which axis, e.g. {\"+x\": [5], \"-z\": [0], ...} with every axis listed. get_mesh reads the UVs back. "
        + "Errors, each a tool error that changes nothing and names the element: no mesh yet, an unknown method, a face that is not a face of the mesh or is deleted (or listed twice, or no faces to map), an option the method does not take, view together with direction, a view that is not one of the eight names, a direction, axis or seam of zero, a seam along the axis, an into that is not four finite numbers or has no area, an unknown argument. "
        + "Worked examples on define_mesh's unit cube: box every face into the whole texture, then lay the top face (face 3, which faces +Y) flat as seen from above into the left half:\n"
        + ExampleMarker + """{"method":"box"}""" + "\n"
        + ExampleMarker + """{"method":"planar","view":"+y","faces":[3],"into":[0,0,0.5,1]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<ProjectUvsTool>("project_uvs", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["method"] = new JsonObject { ["description"] = "planar, box or cylindrical.", ["type"] = "string", ["enum"] = new JsonArray("planar", "box", "cylindrical") },
            ["faces"] = new JsonObject { ["description"] = "Face ids to map; default every live face.", ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } },
            ["into"] = Numbers("[u0, v0, u1, v1]: the rectangle of UV space to fill; default [0, 0, 1, 1]. u1 > u0 and v1 > v0.", 4),
            ["view"] = new JsonObject { ["description"] = "planar: a render view name (front, back, +x, -x, +y, -y, +z, -z); default front. Not with direction.", ["type"] = "string" },
            ["direction"] = Numbers("planar: [x, y, z] from the point looked at toward the eye, any length. Not with view.", 3),
            ["axis"] = Numbers("cylindrical: [x, y, z] direction of the axis; default [0, 1, 0].", 3),
            ["centre"] = Numbers("cylindrical: [x, y, z] a point on the axis; default the middle of the faces' bounds.", 3),
            ["seam"] = Numbers("cylindrical: [x, y, z] a direction across the axis where u is 0; default [0, 0, -1].", 3),
        },
        ["required"] = new JsonArray("method"),
    }));

    private static JsonObject Numbers(string description, int count) => new()
    {
        ["description"] = description,
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "number" },
        ["minItems"] = count,
        ["maxItems"] = count,
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? method = null, JsonElement? faces = null, JsonElement? into = null,
        JsonElement? view = null, JsonElement? direction = null, JsonElement? axis = null, JsonElement? centre = null, JsonElement? seam = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("project_uvs", context, "method", "faces", "into", "view", "direction", "axis", "centre", "seam", "mesh");
        var meshName = MeshName(mesh, errors);
        var chosen = RequiredString("method", method, errors);
        if (chosen is not null && !Methods.Contains(chosen))
        {
            errors.Add($"method: '{chosen}' is not a method; the methods are {string.Join(", ", Methods)}");
            chosen = null;
        }
        var ids = Ids("faces", faces, errors);
        if (ids is not null)
        {
            var seen = new HashSet<int>();
            foreach (var f in ids.Where(f => !seen.Add(f)).Distinct()) errors.Add($"faces[{f}]: face {f} is listed more than once");
            if (ids.Count == 0) errors.Add("faces: no faces to map; leave faces out to map every face");
        }

        var rect = UvProjection.Rect.Unit;
        if (!Absent(into))
        {
            if (into!.Value.ValueKind != JsonValueKind.Array || into.Value.GetArrayLength() != 4)
                errors.Add("into: " + UvArguments.IntoShape(into.Value.ValueKind == JsonValueKind.Array ? into.Value.GetArrayLength().ToString(System.Globalization.CultureInfo.InvariantCulture) : Describe(into.Value)));
            else
            {
                var n = new float?[4];
                for (var k = 0; k < 4; k++) n[k] = IntoNumber($"into[{k}]", into.Value[k], errors);
                if (n.All(x => x is not null))
                {
                    rect = new UvProjection.Rect(n[0]!.Value, n[1]!.Value, n[2]!.Value, n[3]!.Value);
                    if (UvArguments.IntoArea(n[0]!.Value, n[1]!.Value, n[2]!.Value, n[3]!.Value) is { } flat) errors.Add("into: " + flat);
                }
            }
        }

        // An option the method does not take.
        var takes = chosen switch
        {
            "planar" => new[] { "view", "direction" },
            "cylindrical" => ["axis", "centre", "seam"],
            _ => System.Array.Empty<string>(),
        };
        if (chosen is not null)
            foreach (var (name, given) in new[] { ("view", view), ("direction", direction), ("axis", axis), ("centre", centre), ("seam", seam) })
                if (!Absent(given) && !takes.Contains(name))
                    errors.Add($"{name}: not an option of {chosen}; {chosen} takes {(takes.Length == 0 ? "no options" : string.Join(", ", takes))}");

        var projectDirection = ViewFraming.NamedViews["front"];
        if (chosen == "planar")
        {
            if (!Absent(view) && !Absent(direction)) errors.Add("view and direction: give one, not both");
            else if (!Absent(view))
            {
                if (view!.Value.ValueKind != JsonValueKind.String) errors.Add($"view: expected a string, got {Describe(view.Value)}");
                else if (!ViewFraming.NamedViews.TryGetValue(view.Value.GetString()!, out projectDirection))
                    errors.Add($"view: '{view.Value.GetString()}' is not a view; the views are {string.Join(", ", ViewFraming.NamedViews.Keys)}");
            }
            else if (!Absent(direction) && Vec3("direction", direction, errors) is { } d)
            {
                if (Usable(d)) projectDirection = d;
                else errors.Add("direction: must not be zero, and must be a usable size");
            }
        }

        var cylinderAxis = Vector3.UnitY;
        var cylinderSeam = -Vector3.UnitZ;
        Vector3? cylinderCentre = null;
        if (chosen == "cylindrical")
        {
            if (!Absent(axis) && Vec3("axis", axis, errors) is { } a)
            {
                if (Usable(a)) cylinderAxis = a;
                else errors.Add("axis: must not be zero, and must be a usable size");
            }
            if (!Absent(seam) && Vec3("seam", seam, errors) is { } s)
            {
                if (Usable(s)) cylinderSeam = s;
                else errors.Add("seam: must not be zero, and must be a usable size");
            }
            if (!Absent(centre)) cylinderCentre = Vec3("centre", centre, errors);
            if (errors.Count == 0 && UvProjection.SeamAlongAxis(cylinderAxis, cylinderSeam))
                errors.Add("seam: lies along the axis; give a direction across it");
        }
        if (errors.Count > 0) return Failed("project_uvs", errors);

        IReadOnlyDictionary<string, IReadOnlyList<int>>? axes = null;
        List<int> mapped = [];
        var snapshot = store.Rework("project_uvs", meshName, m =>
        {
            var problems = new List<string>();
            var live = Enumerable.Range(0, m.Faces.Count).Where(f => !m.Faces[f].IsDeleted).ToList();
            List<int> chosenFaces;
            if (ids is null)
            {
                if (live.Count == 0) problems.Add("faces: the mesh has no faces to map");
                chosenFaces = live;
            }
            else
            {
                chosenFaces = [.. ids];
                foreach (var f in ids)
                {
                    if (UvArguments.FaceProblem(m, f) is { } why) problems.Add($"faces[{f}]: {why}");
                }
            }
            if (problems.Count > 0) return (null, problems);
            chosenFaces.Sort();

            IReadOnlyDictionary<int, Vector2[]> uvs;
            switch (chosen)
            {
                case "planar":
                    uvs = UvProjection.Planar(m, chosenFaces, projectDirection, rect);
                    break;
                case "box":
                    var box = UvProjection.Box(m, chosenFaces, rect);
                    uvs = box.Uvs;
                    axes = box.Axes;
                    break;
                default:
                    uvs = UvProjection.Cylindrical(m, chosenFaces, cylinderAxis, cylinderCentre, cylinderSeam, rect);
                    break;
            }
            var next = (m.Uvs ?? MeshUvs.None).WithFaces(uvs.Select(p => KeyValuePair.Create(p.Key, (IReadOnlyList<Vector2>?)p.Value)));
            var result = m.WithUvs(next);
            problems.AddRange(MeshUvs.Check(result, next).Select(e => $"{e.Element}: {e.Message}"));
            if (problems.Count > 0) return (null, problems);
            mapped = chosenFaces;
            return (result, problems);
        }, out var refused, out var problem);
        if (problem is not null) return Failed("project_uvs", [problem]);
        if (refused.Count > 0) return Failed("project_uvs", refused);
        if (snapshot is null) return Error("project_uvs: " + NoMesh);

        var target = snapshot.Model.Target(meshName, out _)!;
        var written = mapped.SelectMany(f => target.Mesh.Uvs!.Of(f)!).ToArray();
        float[] bounds = [written.Min(p => p.X), written.Min(p => p.Y), written.Max(p => p.X), written.Max(p => p.Y)];
        return axes is null
            ? Ok(new { mesh = target.Name, method = chosen, mapped, bounds })
            : Ok(new { mesh = target.Name, method = chosen, mapped, bounds, axes });
    }

    private static bool Usable(Vector3 v)
    {
        var length = v.Length();
        return float.IsFinite(length) && length > 0 && float.IsFinite(Vector3.Normalize(v).X);
    }
}
