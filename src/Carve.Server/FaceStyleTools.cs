using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>What the two face-style tools share: the face-id schema and the check of each id against the mesh.</summary>
internal static class FaceArguments
{
    public static void CheckIds(PolygonMesh mesh, IEnumerable<int> faces, List<string> errors)
    {
        foreach (var f in faces.Distinct())
        {
            if (FaceStyle.FaceProblem(mesh, f) is { } problem)
                errors.Add($"faces[{f}]: {problem}");
        }
    }

    public static JsonObject FacesSchema(string description) => new()
    {
        ["description"] = description,
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "integer" },
        ["minItems"] = 1,
    };
}

public sealed class DefineGroupTool(ModelStore store)
{
    public const string Text =
        "Names a set of faces of a mesh, so set_colour can colour them together: define_group(name, faces). "
        + "A face may be in several groups. A group is kept with its mesh: faces keep their ids through every edit, and an extrude's walls join the groups of the face they came from. "
        + "Defining a name that exists replaces that group. A new define_mesh of the same name starts the mesh again with no groups and no colours. "
        + "Returns JSON {mesh, group, faces:[ids], groups:[{name, faces}]}. "
        + "Errors, each a tool error that changes nothing: no mesh yet, an empty name, no faces, a face id the mesh does not have. "
        + "Worked example on define_mesh's unit cube, naming the four sides:\n"
        + ExampleMarker + """{"name":"sides","faces":[0,1,2,3]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<DefineGroupTool>("define_group", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["description"] = "The group's name; not empty.", ["type"] = "string" },
            ["faces"] = FaceArguments.FacesSchema("Face ids of the mesh, as get_mesh reports them."),
        },
        ["required"] = new JsonArray("name", "faces"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? name = null, JsonElement? faces = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("define_group", context, "name", "faces", "mesh");
        var meshName = MeshName(mesh, errors);
        var group = RequiredString("name", name, errors);
        if (group is not null && FaceStyle.GroupNameProblem(group) is { } badName) errors.Add("name: " + badName);
        var unknown = errors.Count;
        var f = Ids("faces", faces, errors);
        if (f is null && errors.Count == unknown) errors.Add("faces: required but missing");
        else if (f is { Count: 0 }) errors.Add("faces: nothing to group: list at least one face");
        if (errors.Count > 0) return Failed("define_group", errors);

        var snapshot = store.Restyle("define_group", meshName, m =>
        {
            var problems = new List<string>();
            FaceArguments.CheckIds(m, f!, problems);
            return problems.Count > 0 ? (null, problems) : ((m.Style ?? FaceStyle.None).WithGroup(group!, f!), problems);
        }, out var refused, out var problem);
        if (problem is not null) return Failed("define_group", [problem]);
        if (refused.Count > 0) return Failed("define_group", refused);
        if (snapshot is null) return Error("define_group: " + NoMesh);

        var target = snapshot.Model.Target(meshName, out _)!;
        return Ok(new
        {
            mesh = target.Name,
            group,
            faces = target.Mesh.Style!.FacesOf(group!),
            groups = target.Mesh.Style.Groups.Select(g => new { name = g.Name, faces = g.Faces }).ToArray(),
        });
    }
}

public sealed class SetColourTool(ModelStore store)
{
    public const string Text =
        "Colours faces of a mesh: set_colour(colour, and either faces or group). colour is sRGB hex, #RRGGBB (digits in either case), kept as given and converted to linear light for drawing and export. "
        + "Give exactly one of faces (face ids) or group (a name from define_group). A face has one colour; the last call wins. Faces never coloured keep Carve's grey. A face has a colour or a material, never both: this clears the material of the faces it colours (set_material clears a colour the same way). "
        + "Colours follow a face through extrude: the walls take the colour of the face they came from. "
        + "render shows the colours in the shaded look (the wireframe and normals looks are unlit and ignore them); export writes one opaque matte material per colour, metallic 0 and roughness 1, as baseColorFactor. "
        + "get_mesh reports the colours and groups. A new define_mesh of the same name starts again uncoloured. "
        + "Returns JSON {mesh, colour, faces:[ids coloured]}. "
        + "Errors, each a tool error that changes nothing: no mesh yet, a colour that is not #RRGGBB, both or neither of faces and group, an unknown group, a face id the mesh does not have. "
        + "Worked example on define_mesh's unit cube:\n"
        + ExampleMarker + """{"colour":"#C8503C","faces":[0,1]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<SetColourTool>("set_colour", Text, WithMesh(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["colour"] = new JsonObject { ["description"] = "sRGB hex, #RRGGBB.", ["type"] = "string" },
            ["faces"] = FaceArguments.FacesSchema("Face ids to colour. Give this or group, not both."),
            ["group"] = new JsonObject { ["description"] = "A group name from define_group. Give this or faces, not both.", ["type"] = "string" },
        },
        ["required"] = new JsonArray("colour"),
    }));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? colour = null, JsonElement? faces = null, JsonElement? group = null, JsonElement? mesh = null)
    {
        var errors = UnknownArguments("set_colour", context, "colour", "faces", "group", "mesh");
        var meshName = MeshName(mesh, errors);
        var hex = RequiredString("colour", colour, errors);
        if (hex is not null && FaceStyle.ColourProblem(hex) is { } bad) errors.Add("colour: " + bad);
        var f = Ids("faces", faces, errors);
        var groupName = OptionalString("group", group, errors);
        if (f is not null && groupName is not null) errors.Add("faces, group: give faces or group, not both");
        else if (f is null && groupName is null && errors.Count == 0) errors.Add("faces, group: give one of faces or group");
        else if (f is { Count: 0 }) errors.Add("faces: nothing to colour: list at least one face");
        if (errors.Count > 0) return Failed("set_colour", errors);

        IReadOnlyList<int>? coloured = null;
        var snapshot = store.Restyle("set_colour", meshName, m =>
        {
            var problems = new List<string>();
            var style = m.Style ?? FaceStyle.None;
            IReadOnlyList<int>? ids = f;
            if (groupName is not null)
            {
                ids = style.FacesOf(groupName);
                if (ids is null) problems.Add(NoGroup(style, groupName));
            }
            else FaceArguments.CheckIds(m, ids!, problems);
            if (problems.Count > 0) return (null, problems);
            coloured = ids!.Distinct().Order().ToArray();
            return (style.WithColour(ids!, hex!), problems);
        }, out var refused, out var problem);
        if (problem is not null) return Failed("set_colour", [problem]);
        if (refused.Count > 0) return Failed("set_colour", refused);
        if (snapshot is null) return Error("set_colour: " + NoMesh);

        return Ok(new { mesh = snapshot.Model.Target(meshName, out _)!.Name, colour = hex, faces = coloured });
    }
}
