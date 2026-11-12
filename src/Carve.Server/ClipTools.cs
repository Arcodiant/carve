using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>The <c>define_clip</c> tool (D-6, clips): a named clip of rotation and translation keys per bone.</summary>
public sealed class DefineClipTool(ModelStore store)
{
    public const string ExampleMarker = "Worked example (the two-bone chain's wave):";

    /// <summary>
    /// On a chain of bones base (at the origin) and joint (1 m above it, parent base): the joint turns from rest to a
    /// quarter turn about +Z at 0.5 s (the upper part swings to lie along -X) and back by 1 s, looping.
    /// </summary>
    public const string Example =
        """{"name":"wave","duration":1,"loop":true,"channels":{"joint":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0,0,0.70710677,0.70710677]},{"time":1,"value":[0,0,0,1]}]}}}}""";

    public const string Text =
        "Defines a named animation clip: define_clip(name, duration, loop, channels). Needs a skeleton (define_skeleton) first. "
        + "A name already used REPLACES that clip; a call is one step for undo, and a call that fails changes nothing. "
        + "duration is the clip's length in seconds, greater than 0. loop (default false) says the clip is meant to repeat. "
        + "channels is an object of bone NAME to {rotation, translation}, each part optional: {interpolation: 'LINEAR' (the default) or 'STEP', keys: [{time, value}, ...]}. "
        + "Key values are ABSOLUTE and LOCAL TO THE PARENT, as render's pose and a glTF node's are, replacing the rest value, not added to it: rotation a unit quaternion [x, y, z, w], translation [x, y, z] in metres. "
        + "A bone, or a part of one, with no keys holds its rest value. Key times are seconds in [0, duration], strictly increasing. "
        + "Between two keys LINEAR moves translation in a straight line and turns rotation by the shortest arc (spherical interpolation; q and -q are the same turn, so a key's sign never sends the bone the long way round); "
        + "STEP holds the earlier key until the time reaches the next. Before the first key and after the last a channel holds that key; it does not wrap. "
        + "render(clip, time) draws the clip at a moment. "
        + "Returns JSON {name, duration, loop, bones: [animated bone names], replaced: true if a clip of that name was there before, clips: [every clip name], warnings: [strings]}. warnings never refuse the clip: a looping clip whose rotation ends more than 1 degree from where it starts (q and -q count as equal), or whose translation ends more than 1 mm away on X/Z; a clip whose root bone's translation ends more than 1 mm from where it starts on X/Z (clips are in place). Each names the clip, bone and difference. "
        + "Errors, each a tool error that changes nothing and lists every problem by place (e.g. channels.joint.rotation.keys[1]): no skeleton yet; an empty name; a duration not greater than 0; "
        + "a bone the skeleton lacks; a bone with neither part; an interpolation other than LINEAR or STEP; a part with no keys; a key time outside [0, duration], the same as or earlier than the key before it; "
        + "a rotation that is not a unit quaternion (within 0.001); a value that is not 3 or 4 finite numbers. "
        + "Once a clip exists, define_skeleton with other bone names or another order is a tool error. "
        + ExampleMarker + " on bones base (at the origin) and joint (1 m above it, parent base), the joint turns a quarter about +Z by 0.5 s, so the part above it swings to lie along -X, and back by 1 s: " + Example;

    public static McpServerTool Create() => MeshToolSupport.Create<DefineClipTool>("define_clip", Text, Schema());

    private static JsonObject Channel(string what, int count) => new()
    {
        ["description"] = what,
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["interpolation"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("LINEAR", "STEP") },
            ["keys"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["time"] = new JsonObject { ["type"] = "number" },
                        ["value"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "number" }, ["minItems"] = count, ["maxItems"] = count },
                    },
                    ["required"] = new JsonArray("time", "value"),
                },
            },
        },
        ["required"] = new JsonArray("keys"),
    };

    private static JsonObject Schema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["description"] = "The clip's name; a name already used replaces that clip.", ["type"] = "string" },
            ["duration"] = new JsonObject { ["description"] = "The length in seconds, greater than 0. Every key time lies in [0, duration].", ["type"] = "number" },
            ["loop"] = new JsonObject { ["description"] = "Whether the clip is meant to repeat. Default false.", ["type"] = "boolean" },
            ["channels"] = new JsonObject
            {
                ["description"] = "Bone name to {rotation, translation}, each optional; a bone left out holds rest.",
                ["type"] = "object",
                ["additionalProperties"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["rotation"] = Channel("Rotation keys: value is a unit quaternion [x, y, z, w], local to the parent and absolute.", 4),
                        ["translation"] = Channel("Translation keys: value is [x, y, z] in metres, local to the parent and absolute.", 3),
                    },
                },
            },
        },
        ["required"] = new JsonArray("name", "duration", "channels"),
    };

    public CallToolResult Run(RequestContext<CallToolRequestParams> context,
        JsonElement? name = null, JsonElement? duration = null, JsonElement? loop = null, JsonElement? channels = null)
    {
        var errors = UnknownArguments("define_clip", context, "name", "duration", "loop", "channels");
        string clipName = "";
        if (Absent(name)) errors.Add("name: required but missing");
        else if (name!.Value.ValueKind != JsonValueKind.String) errors.Add($"name: expected a string, got {Describe(name.Value)}");
        else clipName = name.Value.GetString()!;

        float length = 0;
        if (Absent(duration)) errors.Add("duration: required but missing");
        else if (duration!.Value.ValueKind != JsonValueKind.Number) errors.Add($"duration: expected a number of seconds, got {Describe(duration.Value)}");
        else length = (float)duration.Value.GetDouble();

        var loops = OptionalBool("loop", loop, errors);

        var specs = new List<BoneChannelsSpec>();
        if (Absent(channels)) errors.Add("channels: required but missing");
        else if (channels!.Value.ValueKind != JsonValueKind.Object) errors.Add($"channels: expected an object of bone name to {{rotation, translation}}, got {Describe(channels.Value)}");
        else
            foreach (var bone in channels.Value.EnumerateObject())
                if (ReadBone(bone, errors) is { } spec) specs.Add(spec);

        if (errors.Count > 0) return Failed("define_clip", errors);
        if (store.DefineClip("define_clip", skeleton => Clip.Create(clipName, length, loops, specs, skeleton), out var clip, out var replaced, out var problems) is not { } done)
            return Failed("define_clip", problems);
        return Ok(new
        {
            name = clip!.Name,
            duration = clip.Duration,
            loop = clip.Loop,
            bones = clip.Channels.Select(c => c.Bone).ToArray(),
            replaced,
            clips = done.Model.Clips.Select(c => c.Name).ToArray(),
            warnings = ClipChecks.Check(clip, done.Model.Skeleton!),
        });
    }

    private static BoneChannelsSpec? ReadBone(JsonProperty bone, List<string> errors)
    {
        var at = $"channels.{bone.Name}";
        if (bone.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{at}: expected an object {{rotation, translation}}, got {Describe(bone.Value)}");
            return null;
        }
        var before = errors.Count;
        ChannelSpec<Quaternion>? rotation = null;
        ChannelSpec<Vector3>? translation = null;
        foreach (var part in bone.Value.EnumerateObject())
        {
            switch (part.Name)
            {
                case "rotation":
                    rotation = ReadChannel($"{at}.rotation", part.Value, 4, v => new Quaternion(v[0], v[1], v[2], v[3]), errors);
                    break;
                case "translation":
                    translation = ReadChannel($"{at}.translation", part.Value, 3, v => new Vector3(v[0], v[1], v[2]), errors);
                    break;
                default:
                    errors.Add($"{at}.{part.Name}: not a part of a bone's channels; its parts are rotation, translation");
                    break;
            }
        }
        return errors.Count > before ? null : new BoneChannelsSpec(bone.Name, rotation, translation);
    }

    private static ChannelSpec<T>? ReadChannel<T>(string at, JsonElement channel, int count, Func<float[], T> make, List<string> errors)
    {
        if (channel.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{at}: expected an object {{interpolation, keys}}, got {Describe(channel)}");
            return null;
        }
        var before = errors.Count;
        string? interpolation = null;
        var keys = new List<ClipKey<T>>();
        var sawKeys = false;
        foreach (var field in channel.EnumerateObject())
        {
            switch (field.Name)
            {
                case "interpolation":
                    if (field.Value.ValueKind == JsonValueKind.String) interpolation = field.Value.GetString();
                    else errors.Add($"{at}.interpolation: expected 'LINEAR' or 'STEP', got {Describe(field.Value)}");
                    break;
                case "keys":
                    sawKeys = true;
                    if (field.Value.ValueKind != JsonValueKind.Array)
                    {
                        errors.Add($"{at}.keys: expected an array of {{time, value}}, got {Describe(field.Value)}");
                        break;
                    }
                    var i = 0;
                    foreach (var key in field.Value.EnumerateArray())
                    {
                        if (ReadKey($"{at}.keys[{i}]", key, count, errors) is { } k) keys.Add(new ClipKey<T>(k.Time, make(k.Value)));
                        i++;
                    }
                    break;
                default:
                    errors.Add($"{at}.{field.Name}: not a field of a channel; its fields are interpolation, keys");
                    break;
            }
        }
        if (!sawKeys) errors.Add($"{at}.keys: required but missing");
        return errors.Count > before ? null : new ChannelSpec<T>(interpolation, keys);
    }

    private static (float Time, float[] Value)? ReadKey(string at, JsonElement key, int count, List<string> errors)
    {
        if (key.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{at}: expected an object {{time, value}}, got {Describe(key)}");
            return null;
        }
        var before = errors.Count;
        foreach (var field in key.EnumerateObject())
            if (field.Name is not ("time" or "value")) errors.Add($"{at}.{field.Name}: not a field of a key; its fields are time, value");
        float time = 0;
        if (!key.TryGetProperty("time", out var t)) errors.Add($"{at}.time: required but missing");
        else if (!IsFinite(t)) errors.Add($"{at}.time: expected a finite number of seconds, got {Describe(t)}");
        else time = (float)t.GetDouble();

        var value = new float[count];
        if (!key.TryGetProperty("value", out var v)) errors.Add($"{at}.value: required but missing");
        else if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != count
            || v.EnumerateArray().Any(n => !IsFinite(n)))
            errors.Add($"{at}.value: expected {count} finite numbers");
        else
        {
            var i = 0;
            foreach (var n in v.EnumerateArray()) value[i++] = (float)n.GetDouble();
        }
        return errors.Count > before ? null : (time, value);
    }
}

/// <summary>The <c>delete_clip</c> tool: removes a clip by name.</summary>
public sealed class DeleteClipTool(ModelStore store)
{
    public const string Text =
        "Deletes the clip of that name: delete_clip(name). One step for undo. "
        + "Returns JSON {deleted: the name, clips: [the clip names left]}. "
        + "Errors, changing nothing: no clip of that name (the error lists the clips), a name that is not a string. "
        + ExampleMarker + """{"name":"wave"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<DeleteClipTool>("delete_clip", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["description"] = "The clip to delete, as named to define_clip.", ["type"] = "string" },
        },
        ["required"] = new JsonArray("name"),
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? name = null)
    {
        var errors = UnknownArguments("delete_clip", context, "name");
        if (Absent(name)) errors.Add("name: required but missing");
        else if (name!.Value.ValueKind != JsonValueKind.String) errors.Add($"name: expected a string, got {Describe(name.Value)}");
        if (errors.Count > 0) return Failed("delete_clip", errors);
        var clip = name!.Value.GetString()!;
        if (store.DeleteClip("delete_clip", clip, out var refusal) is not { } done) return Failed("delete_clip", [refusal!]);
        return Ok(new { deleted = clip, clips = done.Model.Clips.Select(c => c.Name).ToArray() });
    }
}

/// <summary>The <c>get_clips</c> tool: reads the clips back as given, with their warnings.</summary>
public sealed class GetClipsTool(ModelStore store)
{
    private const string Text =
        "Reads back the clips: get_clips(name?). With no name every clip, in the order defined; with one, that clip. "
        + "Returns JSON {clips: [{name, duration, loop, channels: {bone: {rotation: {interpolation, keys: [{time, value}]}, translation: {...}}}, warnings: [strings]}]}, "
        + "the keys exactly as given to define_clip (a part the clip does not key is left out). warnings are define_clip's: a looping clip that does not close, or a root bone that ends elsewhere on the ground plane. "
        + "With no skeleton or no clips the list is empty. Errors: a name no clip has (the error lists the clips), a name that is not a string. "
        + DefineClipTool.ExampleMarker + """ {"name":"wave"}""";

    public static McpServerTool Create() => MeshToolSupport.Create<GetClipsTool>("get_clips", Text, new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["description"] = "One clip to read, as named to define_clip. Left out, every clip.", ["type"] = "string" },
        },
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? name = null)
    {
        var errors = UnknownArguments("get_clips", context, "name");
        string? wanted = null;
        if (!Absent(name))
        {
            if (name!.Value.ValueKind != JsonValueKind.String) errors.Add($"name: expected a string, got {Describe(name.Value)}");
            else wanted = name.Value.GetString();
        }
        if (errors.Count > 0) return Failed("get_clips", errors);
        var model = store.Current.Model;
        var clips = model.Clips.ToList();
        if (wanted is not null)
        {
            clips = clips.Where(c => c.Name == wanted).ToList();
            if (clips.Count == 0)
                return Error($"get_clips: no clip named '{wanted}'; the clips are " + (model.Clips.Count == 0 ? "none yet" : NameList.Quoted(model.Clips.Select(c => c.Name))));
        }
        return Ok(new
        {
            clips = clips.Select(c => new
            {
                name = c.Name,
                duration = c.Duration,
                loop = c.Loop,
                channels = c.Channels.ToDictionary(b => b.Bone, b => new
                {
                    rotation = Part(b.Rotation, q => new[] { q.X, q.Y, q.Z, q.W }),
                    translation = Part(b.Translation, v => new[] { v.X, v.Y, v.Z }),
                }),
                warnings = ClipChecks.Check(c, model.Skeleton!),
            }).ToArray(),
        });
    }

    private static object? Part<T>(ClipChannel<T>? channel, Func<T, float[]> value) => channel is null ? null : new
    {
        interpolation = channel.Interpolation == ClipInterpolation.Step ? "STEP" : "LINEAR",
        keys = channel.Keys.Select(k => new { time = k.Time, value = value(k.Value) }).ToArray(),
    };
}
