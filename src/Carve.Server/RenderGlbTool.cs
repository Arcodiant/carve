using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empyrean;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>
/// <c>render_glb</c>: a <c>.glb</c> as the game gets it, read by the engine's own loader with the file's own materials and
/// posed by the engine's own sampling and GPU skinning (D-6, two samplers), in render's scene, light and cameras, framed on
/// the file's rest bounds as render frames a posed model (T-32). The model in the store is not touched.
/// </summary>
internal static class RenderGlbTool
{
    public const string Name = "render_glb";

    public static McpServerTool Create(IServiceProvider services)
    {
        var tool = McpServerTool.Create(typeof(RenderGlbTool).GetMethod(nameof(Run))!, target: null,
            new McpServerToolCreateOptions { Services = services, Name = Name });
        var schema = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())!;
        schema["properties"]!["clip"] = new JsonObject
        {
            ["description"] = "The name of one of the file's own clips: draws the file posed by it at 'time', skinned by the engine. Needs a file with a skin. Framing stays on the rest pose.",
            ["type"] = "string",
        };
        schema["properties"]!["time"] = new JsonObject
        {
            ["description"] = "With clip, and required by it: the moment in seconds, from 0 to the clip's duration.",
            ["type"] = "number",
        };
        tool.ProtocolTool.InputSchema = Closed(JsonSerializer.SerializeToElement(schema));
        return tool;
    }

    private const string Text =
        "Draws a .glb file as a game on the Empyrean engine gets it: read by the engine's own model loader with the file's own materials, "
        + "and, given a clip, posed by the engine's own sampling of that clip and drawn with its GPU skinning; with no clip, a skinned file is drawn skinned at its bind pose "
        + "(every bone at its node's rest transform, through the file's inverse binds), as a game draws it with no clip playing. Use it on what export wrote, to see the export and not "
        + "Carve's preview of it. The scene, light, background and cameras are render's, and the frame is fitted to the file's rest pose as render fits a posed model, "
        + "so a render_glb frame and a render frame of the same model, view, size, clip and time line up. A clip can carry a limb past the rest pose's frame, as in render. "
        + "The model being edited is not changed. "
        + "Errors: no file at the path; a clip given for a file with no skin; a clip the file does not have (the clips it has are listed); a time outside the clip; time without clip. "
        + "The loader's own warnings (something it read differently from the file's intent) are repeated in the result.";

    [McpServerTool(Name = Name), Description(Text)]
    public static async Task<CallToolResult> Run(
        RequestContext<CallToolRequestParams> context,
        EngineStatus status,
        RenderQueue queue,
        IModelGeometryLoader loader,
        [Description("The .glb file to draw, absolute or relative to carve's working directory.")] string? path = null,
        JsonElement? clip = null,
        JsonElement? time = null,
        [Description("Where to look from: the corner 'front' (the default when no direction is given) or 'back', or head-on along an axis: '+x', '-x', '+y', '-y', '+z', '-z'. Not with direction."), AllowedValues("front", "back", "+x", "-x", "+y", "-y", "+z", "-z")] string? view = null,
        [Description("[x, y, z]: from the point looked at toward the eye. Up is +Y, or -Z within 1 degree of vertical. Not with view.")] double[]? direction = null,
        [Description("1 to 8: how far to zoom in about the target. The default 1 frames the whole rest pose.")] double zoom = 1,
        [Description("[x, y, z] in the file's coordinates, inside its rest bounds: the point to look at. Default: the centre.")] double[]? target = null,
        [Description("'perspective' (the default), what a game shows, or 'orthographic'."), AllowedValues("perspective", "orthographic")] string? projection = null,
        [Description("The image's width and height: 256, 512 (the default) or 1024."), AllowedValues(256, 512, 1024)] double size = RenderSpec.DefaultSize,
        CancellationToken cancellationToken = default)
    {
        if (UnknownArguments(Name, context, "path", "clip", "time", "view", "direction", "zoom", "target", "projection", "size") is { Count: > 0 } unknown)
        {
            return Failed(Name, unknown);
        }

        if (RenderTool.CameraRefusal(view, direction, zoom, target) is { } refused) return Error(refused);
        if (projection is not (null or "perspective" or "orthographic"))
            return Error($"projection: '{projection}' is not a projection; use 'perspective' or 'orthographic'.");
        if (!RenderComposition.Sizes.Any(s => s == size))
            return Error($"size: {RenderTool.Join([size])} is not a size; use 256, 512 or 1024");

        var problems = new List<string>();
        var (clipName, clipTime) = ReadClip(clip, time, problems);
        if (problems.Count > 0) return Failed(Name, problems);

        var file = ToolPaths.Resolve(Name, path, ".glb", out var pathError, verb: "reads");
        if (file is null) return Error(pathError!);
        if (Directory.Exists(file.FullPath)) return Error($"{Name}: {file.FullPath} is a folder, not a file");
        if (!File.Exists(file.FullPath)) return Error($"{Name}: no file at {file.FullPath}");

        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(file.FullPath, cancellationToken); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Error($"{Name}: could not read {file.FullPath}: {e.Message}");
        }

        var glb = GlbScene.Prepare(loader, bytes, clipName, clipTime, problems);
        if (glb is null) return Failed(Name, problems);

        var targetVector = target is null ? (System.Numerics.Vector3?)null : RenderTool.Vector(target);
        if (targetVector is { } t && TargetCheck.Refusal(t, glb.BoundsMin, glb.BoundsMax) is { } outside) return Error(outside);

        var camera = new CameraSpec(direction is { } d ? RenderTool.Vector(d) : ViewFraming.NamedViews[view ?? "front"], (float)zoom, targetVector);
        var projected = projection == "orthographic" ? Projection.Orthographic : Projection.Perspective;
        var rendering = queue.Post(new RenderSpec(camera, projected, (int)size) { Glb = glb });
        await Task.WhenAny(rendering, status.WhenStopped).WaitAsync(cancellationToken);
        if (!rendering.IsCompleted) return Error(status.FaultMessage ?? "The engine has stopped, so nothing can be rendered.");
        if (!rendering.IsCompletedSuccessfully) return Error($"The render failed: {rendering.Exception?.GetBaseException().Message}");

        var frame = rendering.Result;
        var image = frame.Pixels.ToDecodedImage();
        return new CallToolResult
        {
            Content =
            [
                ImageContentBlock.FromBytes(RenderTool.EncodePng(image.Pixels, image.Width, image.Height), "image/png"),
                new TextContentBlock { Text = Caption(file.FullPath, glb, clipName, clipTime, frame, view, direction, projected, zoom, target) },
            ],
        };
    }

    /// <summary>The clip and time arguments, by render's rules: time only with a clip, and required by it.</summary>
    private static (string? Name, float Time) ReadClip(JsonElement? clip, JsonElement? time, List<string> errors)
    {
        if (Absent(clip))
        {
            if (!Absent(time)) errors.Add("time: only with clip; give the clip to draw at this time");
            return (null, 0);
        }

        var before = errors.Count;
        if (clip!.Value.ValueKind != JsonValueKind.String) errors.Add($"clip: expected a clip name, got {Describe(clip.Value)}");
        var at = 0f;
        if (Absent(time)) errors.Add("time: required with clip; the moment in seconds, from 0 to the clip's duration");
        else if (!IsFinite(time!.Value)) errors.Add($"time: expected a finite number of seconds, got {Describe(time.Value)}");
        else at = (float)time.Value.GetDouble();
        return errors.Count > before ? (null, 0) : (clip.Value.GetString(), at);
    }

    private static string Caption(string path, GlbScene glb, string? clip, float time, RenderedFrame frame, string? view, double[]? direction, Projection projection, double zoom, double[]? target)
    {
        var parts = new List<string>
        {
            $"{frame.Pixels.Width}x{frame.Pixels.Height}",
            Path.GetFileName(path),
            direction is null ? $"view {view ?? "front"}" : $"direction ({RenderTool.Join(direction)})",
        };
        if (projection == Projection.Orthographic) parts.Add("orthographic");
        parts.Add(clip is null ? (glb.Palette is null ? "no clip: the file as loaded, unskinned" : "no clip: skinned at its bind pose") : string.Create(CultureInfo.InvariantCulture, $"clip '{clip}' at {time} s"));
        RenderTool.AddZoomAndTarget(parts, zoom, target);
        var text = $"{string.Join(", ", parts)}; {glb.Vertices} vertices, {glb.Triangles} triangles, as the engine loaded them";
        return text + glb.WarningsText;
    }
}
