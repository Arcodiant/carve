using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The bones as the caller gives them and as they are read back, for <c>define_skeleton</c> and <c>get_skeleton</c>.</summary>
internal static class SkeletonJson
{
    public const string ExampleMarker = "Worked example (three bones):";

    /// <summary>A hip at 1 m up, a spine 0.3 m above the hip, a head 0.4 m above the spine.</summary>
    public const string Example =
        """{"bones":[{"name":"hips","parent":null,"position":[0,1,0],"rotation":[0,0,0,1]},{"name":"spine","parent":"hips","position":[0,0.3,0],"rotation":[0,0,0,1]},{"name":"head","parent":"spine","position":[0,0.4,0],"rotation":[0,0,0,1]}]}""";

    public const string RestText =
        "Rest transforms are LOCAL TO THE PARENT, as a glTF node's are: a bone's position is its offset from its parent's origin, in the parent's rotated frame, and its rotation is relative to the parent's; "
        + "a root bone's (parent null or left out) are relative to the model. No scale. Rotation is a unit quaternion [x, y, z, w] ([0,0,0,1] is no rotation); position is metres, Y up, as the meshes are. ";

    public static object Bone(Skeleton skeleton, int i)
    {
        var b = skeleton.Bones[i];
        return new
        {
            name = b.Name,
            parent = b.Parent < 0 ? null : skeleton.Bones[b.Parent].Name,
            position = Array(b.Position),
            rotation = new[] { b.Rotation.X, b.Rotation.Y, b.Rotation.Z, b.Rotation.W },
            restWorldPosition = Array(skeleton.RestWorldPosition(i)),
        };
    }
}

public sealed class DefineSkeletonTool(ModelStore store)
{
    public const string ExampleMarker = SkeletonJson.ExampleMarker;
    public const string Example = SkeletonJson.Example;

    private const string Text =
        "Defines the skeleton of the model: bones, each {name, parent, position, rotation}, in exactly the order given, with the names given. "
        + "A call REPLACES the whole skeleton and is one step for undo; a call that fails leaves the model unchanged. "
        + "A parent is named by its bone's name and must come EARLIER in the list than its child (a root has parent null or none). "
        + "Names are unique and non-empty, any text (e.g. mixamorig:Hips). At most 255 bones. "
        + SkeletonJson.RestText
        + "Bad input (more than 255 bones, an empty or duplicate name, a parent that is unknown or comes after its child, a rotation that is not a unit quaternion within 0.001 of length 1, a bone that is not {name, parent, position, rotation}) is a tool error that lists every problem by bone, e.g. bones[2]. "
        + "Returns JSON {bones: the count, names:[in order]}; get_skeleton reads the bones back. "
        + SkeletonJson.ExampleMarker + " a hip 1 m up, a spine 0.3 m above the hip, a head 0.4 m above the spine, so the head rests at y = 1.7: " + SkeletonJson.Example;

    private const string BonesText =
        "The bones in order, each {name: string, parent: string or null (a root), position: [x, y, z] local to the parent, rotation: [x, y, z, w] unit quaternion local to the parent}. A bone's parent must come before it.";

    public static McpServerTool Create() => MeshToolSupport.Create<DefineSkeletonTool>("define_skeleton", Text, Schema());

    private static JsonObject Schema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["bones"] = new JsonObject
            {
                ["description"] = BonesText,
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject { ["type"] = "string" },
                        ["parent"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
                        ["position"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "number" },
                            ["minItems"] = 3,
                            ["maxItems"] = 3,
                        },
                        ["rotation"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "number" },
                            ["minItems"] = 4,
                            ["maxItems"] = 4,
                        },
                    },
                    ["required"] = new JsonArray("name", "position", "rotation"),
                },
            },
        },
        ["required"] = new JsonArray("bones"),
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? bones = null)
    {
        var errors = UnknownArguments("define_skeleton", context, "bones");
        if (Absent(bones)) errors.Add("bones: required but missing");
        else if (bones!.Value.ValueKind != JsonValueKind.Array) errors.Add($"bones: expected an array, got {Describe(bones.Value)}");

        var specs = new List<BoneSpec>();
        var shapeBad = new HashSet<string>();
        if (errors.Count == 0)
        {
            var i = 0;
            foreach (var b in bones!.Value.EnumerateArray())
            {
                specs.Add(ReadBone(i, b, errors, shapeBad));
                i++;
            }
        }

        if (errors.Count == 0 || specs.Count > 0)
        {
            var (skeleton, problems) = Skeleton.Create(specs);
            errors.AddRange(problems.Where(p => !shapeBad.Contains(p.Element)).Select(p => p.ToString()));
            if (errors.Count == 0)
            {
                store.DefineSkeleton(skeleton!, "define_skeleton");
                return Ok(new { bones = skeleton!.Bones.Count, names = skeleton.Names.ToArray() });
            }
        }
        return Failed("define_skeleton", errors);
    }

    private static BoneSpec ReadBone(int i, JsonElement b, List<string> errors, HashSet<string> shapeBad)
    {
        var at = $"bones[{i}]";
        var bad = new BoneSpec("x", null, Vector3.Zero, Quaternion.Identity);
        if (b.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{at}: expected an object {{name, parent, position, rotation}}, got {Describe(b)}");
            shapeBad.Add(at);
            return bad;
        }

        var before = errors.Count;
        foreach (var p in b.EnumerateObject())
            if (p.Name is not ("name" or "parent" or "position" or "rotation"))
                errors.Add($"{at}.{p.Name}: not a field of a bone; its fields are name, parent, position, rotation");

        string name = "";
        if (!b.TryGetProperty("name", out var n) || n.ValueKind == JsonValueKind.Null) errors.Add($"{at}.name: required but missing");
        else if (n.ValueKind != JsonValueKind.String) errors.Add($"{at}.name: expected a string, got {Describe(n)}");
        else name = n.GetString()!;

        string? parent = null;
        if (b.TryGetProperty("parent", out var par) && par.ValueKind != JsonValueKind.Null)
        {
            if (par.ValueKind != JsonValueKind.String) errors.Add($"{at}.parent: expected a bone name or null, got {Describe(par)}");
            else parent = par.GetString();
        }

        var position = Floats($"{at}.position", b, "position", 3, errors);
        var rotation = Floats($"{at}.rotation", b, "rotation", 4, errors);

        if (errors.Count > before)
        {
            shapeBad.Add(at);
            return bad;
        }
        return new BoneSpec(name, parent, new Vector3(position![0], position[1], position[2]),
            new Quaternion(rotation![0], rotation[1], rotation[2], rotation[3]));
    }

    private static float[]? Floats(string at, JsonElement bone, string field, int count, List<string> errors)
    {
        if (!bone.TryGetProperty(field, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            errors.Add($"{at}: required but missing");
            return null;
        }
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != count)
        {
            errors.Add($"{at}: expected {count} numbers, got {(v.ValueKind == JsonValueKind.Array ? v.GetArrayLength() + " element(s)" : Describe(v))}");
            return null;
        }
        var result = new float[count];
        var ok = true;
        var k = 0;
        foreach (var c in v.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Number)
            {
                errors.Add($"{at}[{k}]: expected a number, got {Describe(c)}");
                ok = false;
            }
            else
            {
                var d = c.GetDouble();
                result[k] = (float)d;
                if (!float.IsFinite(result[k]))
                {
                    errors.Add($"{at}[{k}]: {c.GetRawText()} is out of range");
                    ok = false;
                }
            }
            k++;
        }
        return ok ? result : null;
    }
}

public sealed class GetSkeletonTool(ModelStore store)
{
    private const string Text =
        "Reads back the skeleton: the bones in the order they were defined, each {name, parent (a name, or null for a root), position, rotation} exactly as given to define_skeleton, plus restWorldPosition, "
        + "where the bone rests in model space (its parent's rest world position plus its own position turned by the parents' rotations). "
        + SkeletonJson.RestText
        + "Takes no arguments; any argument is a tool error. With no skeleton yet it is a tool error saying to call define_skeleton. "
        + "Returns JSON {bones:[{name, parent, position, rotation, restWorldPosition}]}. "
        + "Worked example, after define_skeleton's three bones (hips, spine, head):"
        + " the third bone reads back as "
        + """{"name":"head","parent":"spine","position":[0,0.4,0],"rotation":[0,0,0,1],"restWorldPosition":[0,1.7,0]}""";

    public static McpServerTool Create() => MeshToolSupport.Create<GetSkeletonTool>("get_skeleton", Text, null);

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        if (UnknownArguments("get_skeleton", context) is { Count: > 0 } unknown) return Failed("get_skeleton", unknown);
        if (store.Current.Model.Skeleton is not { } skeleton)
            return Error("get_skeleton: there is no skeleton yet; call define_skeleton first");
        return Ok(new { bones = Enumerable.Range(0, skeleton.Bones.Count).Select(i => SkeletonJson.Bone(skeleton, i)).ToArray() });
    }
}
