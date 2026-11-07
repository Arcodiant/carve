using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StbImageWriteSharp;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

internal static class RenderTool
{
    /// <summary>
    /// The tool, built here rather than found by <c>WithToolsFromAssembly</c> so that its generated schema can be closed
    /// (<c>additionalProperties: false</c>). <paramref name="services"/> keeps the injected parameters out of the schema.
    /// </summary>
    public static McpServerTool Create(IServiceProvider services)
    {
        var tool = McpServerTool.Create(typeof(RenderTool).GetMethod(nameof(Run))!, target: null,
            new McpServerToolCreateOptions { Services = services });
        var schema = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())!;
        schema["properties"]!["highlight"] = HighlightSchema();
        tool.ProtocolTool.InputSchema = Closed(JsonSerializer.SerializeToElement(schema));
        return tool;
    }

    private static JsonObject HighlightSchema()
    {
        static JsonObject Ints() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } };
        return new JsonObject
        {
            ["description"] = "What to show in orange, in the shape select returns and move takes; each list optional.",
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["vertices"] = Ints(),
                ["edges"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "integer" },
                        ["minItems"] = 2,
                        ["maxItems"] = 2,
                    },
                },
                ["faces"] = Ints(),
            },
        };
    }

    private const string Text =
        "Renders the current mesh, as set by define_mesh or open and changed by move, extrude or undo, and returns one PNG image, 512x512 pixels unless 'size' says otherwise, followed by one caption line, "
        + "e.g. '512x512, view front, 8 vertices, 6 faces'. The image is shaded by Empyrean, the engine games built on this model use. "
        + "No file is written. Nothing is drawn but the mesh: no grid or axes, a dark background, about 8% margin around the mesh. "
        + "Views: 'front' (the default) looks from the +X +Y +Z corner, showing the mesh's top, its +Z side on the left and its +X side on the right; "
        + "'back' looks from the opposite corner, -X -Y -Z, upward from below, showing its underside, -Z side and -X side. "
        + "Six more view names look head-on along an axis, as that direction would: '+x' shows the +X side, +Z on the left; '-x' the -X side, +Z on the right; "
        + "'+z' the +Z side, +X on the right; '-z' the -Z side, +X on the left; all four with +Y up. "
        + "'+y' looks down on the top, +X on the right; '-y' looks up at the underside, +X on the left; both with -Z up. "
        + "Or give 'direction' instead of 'view': [x, y, z], pointing from the point looked at toward the eye, in the mesh's own axes, any length. "
        + "Up on the image is +Y, or -Z when the direction is within 1 degree of straight up or down. "
        + "Examples: [1,0,0] looks from +X, [-1,0,0] from -X, [0,1,0] from above, down at the mesh, [0,-1,0] from below, [0,0,1] from +Z, [0,0,-1] from -Z. "
        + "A head-on axis view shows every face pointing one way as one flat shade, so a slight tilt such as [0.4, 2, 0.8] reads better from above. "
        + "'zoom' (1 to 8, default 1) narrows the view about 'target' without moving the camera; 'target' is the point [x, y, z] to look at and zoom on, "
        + "in the mesh's own coordinates and inside its bounds (default: the centre of the bounds). "
        + "The camera frames the whole mesh each time, so a render shows shape, not size; zoom in on a target to see a detail. "
        + "The caption then reads e.g. '512x512, direction (2, 1, 1), zoom 3, target (1.02, 0.4, 0.5), 72 vertices, 70 faces', "
        + "with zoom and target only when you set them. "
        + "'projection' is 'perspective' (the default, what a game shows) or 'orthographic', which keeps parallel edges parallel and shows true proportions: "
        + "the one for reading dimensions and for a sheet of axis views. It frames the mesh by the same margin; zoom and target work the same. "
        + "The caption says so after the view: '512x512, view +x, orthographic, 8 vertices, 6 faces'. "
        + "'size' is the image's width and height: 256 for a quick look or a sheet tile, 512 (the default), 1024 for a dense mesh with labels. "
        + "The caption opens with it: '256x256, view front, 8 vertices, 6 faces'. "
        + "'mode' is the look: 'shaded' (the default), 'wireframe' or 'normals'. Neither of the last two depends on the light. "
        + "'wireframe' draws every edge in light grey over faces filled near black: use it to count and place edges and see the face loops. It is hidden-line, not see-through: "
        + "an edge behind a face is hidden, so look from another side to see it. "
        + "'normals' colours each face by the way it faces and stands a light grey spike on its centre along its normal: use it to find a face pointing the wrong way, or a non-planar face, whose spike is off. "
        + "The colour code, as linear red, green, blue, is (normal + 1) / 2: +X (1, 0.5, 0.5) pink, -X (0, 0.5, 0.5) teal, +Y (0.5, 1, 0.5) green, -Y (0.5, 0, 0.5) purple, +Z (0.5, 0.5, 1) blue, -Z (0.5, 0.5, 0) olive; "
        + "a face reversed shows the opposite colour. A face wound the wrong way is still culled as in shaded, so from outside it shows as MISSING, and its opposite colour is seen from inside, through the gap. "
        + "The caption says the mode after the projection: '512x512, view front, wireframe, 72 vertices, 70 faces'. "
        + "'highlight' shows what a select picked, before you edit it: {\"vertices\":[ids],\"edges\":[[a,b]],\"faces\":[ids]}, each list optional, the shape select returns and move takes. The server holds no selection, so pass the ids each time. "
        + "All of it is orange: a highlighted face is shaded orange (an orange fill in wireframe; in normals it keeps its normal colour and the edges of its loop turn orange), an edge is an orange line, a vertex a small orange star. "
        + "Faces hide them as they hide anything, so a highlight on the far side needs another view. "
        + "The caption counts it after the mode: '512x512, view front, wireframe, highlight 2 faces, 72 vertices, 70 faces', or 'highlight 3 faces, 2 edges, 1 vertex'. "
        + "The light turns with the view, and faces turned different ways show as different brightnesses. "
        + "Faces wound the wrong way (clockwise seen from outside) show as MISSING: you see through them to whatever is behind. "
        + "An EMPTY result, an image of only the dark background with no error, means no face points at the camera: "
        + "the faces are wound the wrong way, or the mesh is an open surface seen from behind. "
        + "Try the other view, or fix the winding and call define_mesh again (move and extrude keep each face's winding, so they cannot fix it). "
        + "Tool errors, with the mesh untouched: both 'view' and 'direction' given; a view that is not one of the eight names; a direction of zero or not three finite numbers; "
        + "a zoom outside 1 to 8; a target that is not three finite numbers or lies outside the mesh's bounds; a projection other than 'perspective' or 'orthographic'; a size other than 256, 512 or 1024; a mode other than 'shaded', 'wireframe' or 'normals'; a highlight that is not an object, or whose lists are not lists of ids and [a, b] pairs; "
        + "a highlight naming a vertex or face the mesh does not have, or a pair that is not an edge, each listed by element as move does; an argument render does not take. "
        + "Calling render with no mesh (before define_mesh or open) is a tool error saying so. A machine with no working Vulkan device gives a tool error naming Vulkan; "
        + "define_mesh, open, move, extrude, get_mesh and undo still work there.";

    [McpServerTool(Name = "render"), Description(Text)]
    public static async Task<CallToolResult> Run(
        RequestContext<CallToolRequestParams> context,
        EngineStatus status,
        ModelStore store,
        RenderQueue queue,
        [Description("Where to look from: the corner 'front' (the default when no direction is given) or 'back', or head-on along an axis: '+x', '-x', '+y', '-y', '+z', '-z'. Not with direction."), AllowedValues("front", "back", "+x", "-x", "+y", "-y", "+z", "-z")] string? view = null,
        [Description("[x, y, z]: from the point looked at toward the eye. Up is +Y, or -Z within 1 degree of vertical. Not with view.")] double[]? direction = null,
        [Description("1 to 8: how far to zoom in about the target. The default 1 frames the whole mesh.")] double zoom = 1,
        [Description("[x, y, z] in the mesh's coordinates, inside its bounds: the point to look at. Default: the centre.")] double[]? target = null,
        [Description("'perspective' (the default), what a game shows, or 'orthographic': parallel edges stay parallel, true proportions."), AllowedValues("perspective", "orthographic")] string? projection = null,
        [Description("The image's width and height: 256, 512 (the default) or 1024."), AllowedValues(256, 512, 1024)] double size = RenderSpec.DefaultSize,
        [Description("The look: 'shaded' (the default), 'wireframe' (every visible edge over a flat fill) or 'normals' (faces coloured by facing, with spikes)."), AllowedValues("shaded", "wireframe", "normals")] string? mode = null,
        JsonElement? highlight = null,
        CancellationToken cancellationToken = default)
    {
        if (UnknownArguments("render", context, "view", "direction", "zoom", "target", "projection", "size", "mode", "highlight") is { Count: > 0 } unknown)
        {
            return Failed("render", unknown);
        }

        if (view is not null && direction is not null)
        {
            return Error("view and direction: give one, not both.");
        }

        if (view is not null && !ViewFraming.NamedViews.ContainsKey(view))
        {
            return Error($"view: '{view}' is not a view; use 'front', 'back', '+x', '-x', '+y', '-y', '+z' or '-z'.");
        }

        Vector3 directionVector = ViewFraming.NamedViews[view ?? "front"];
        if (direction is not null)
        {
            if (!Finite3(direction))
            {
                return Error("direction: must be three finite numbers [x, y, z].");
            }

            directionVector = new Vector3((float)direction[0], (float)direction[1], (float)direction[2]);
            var length = directionVector.Length();
            if (!float.IsFinite(length) || length < 1e-20f)
            {
                return Error("direction: must not be zero, and must be a usable size.");
            }
        }

        if (!(zoom >= 1 && zoom <= 8))
        {
            return Error($"zoom: {Join([zoom])} is outside 1 to 8.");
        }

        if (target is not null && !Finite3(target))
        {
            return Error("target: must be three finite numbers [x, y, z].");
        }

        if (projection is not (null or "perspective" or "orthographic"))
        {
            return Error($"projection: '{projection}' is not a projection; use 'perspective' or 'orthographic'.");
        }

        if (!RenderComposition.Sizes.Any(s => s == size))
        {
            return Error($"size: {Join([size])} is not a size; use 256, 512 or 1024");
        }

        if (mode is not (null or "shaded" or "wireframe" or "normals"))
        {
            return Error($"mode: '{mode}' is not a mode; use 'shaded', 'wireframe' or 'normals'.");
        }

        var problems = new List<string>();
        var listed = HighlightLists(highlight, problems);
        if (problems.Count > 0)
        {
            return Failed("render", problems);
        }

        var projected = projection == "orthographic" ? Projection.Orthographic : Projection.Perspective;
        var look = mode switch { "wireframe" => Look.Wireframe, "normals" => Look.Normals, _ => Look.Shaded };

        await status.WhenSettled.WaitAsync(cancellationToken);
        if (status.FaultMessage is { } fault)
        {
            return Error(fault);
        }

        if (store.Current.Mesh is not { } mesh)
        {
            return Error("There is no mesh yet: call define_mesh first, or open to read a saved .carve file, then render.");
        }

        // Taken in the mesh's own precision (float), so a target typed as the reported bound is inside (B-1).
        Vector3? targetVector = target is null ? null : new Vector3((float)target[0], (float)target[1], (float)target[2]);
        if (targetVector is { } t)
        {
            var bounds = MeshReport.Create(mesh);
            if (TargetCheck.Refusal(t, bounds.BoundsMin, bounds.BoundsMax) is { } refusal)
            {
                return Error(refusal);
            }
        }

        if (MeshEdits.UnknownElements(mesh, listed.Vertices, listed.Edges, listed.Faces) is { Count: > 0 } missing)
        {
            return Failed("render", missing.Select(e => e.ToString()));
        }

        var picked = Highlight.Of(listed.Vertices, listed.Edges, listed.Faces);
        var rendering = queue.Post(new RenderSpec(new CameraSpec(directionVector, (float)zoom, targetVector), projected, (int)size, look) { Highlight = picked });

        // The frame thread drains the queue; once it has ended, nothing will.
        await Task.WhenAny(rendering, status.WhenStopped).WaitAsync(cancellationToken);
        if (!rendering.IsCompleted)
        {
            return Error(status.FaultMessage ?? "The engine has stopped, so nothing can be rendered.");
        }

        if (!rendering.IsCompletedSuccessfully)
        {
            return Error($"The render failed: {rendering.Exception?.GetBaseException().Message}");
        }

        var frame = rendering.Result;
        return new CallToolResult
        {
            Content =
            [
                ImageContentBlock.FromBytes(EncodePng(frame), "image/png"),
                new TextContentBlock { Text = Caption(frame, view, direction, projected, look, picked, zoom, target) },
            ],
        };
    }

    /// <summary>
    /// The size; <c>view front</c> for a view or <c>direction (x, y, z)</c> as given for a direction; then
    /// each thing asked for beyond the default, only where set: the projection, the mode, the highlight,
    /// zoom, target. The counts close it, so a part added here goes before them.
    /// </summary>
    internal static string Caption(RenderedFrame frame, string? view, double[]? direction, Projection projection, Look look, Highlight highlight, double zoom, double[]? target)
    {
        var parts = new List<string>
        {
            $"{frame.Pixels.Width}x{frame.Pixels.Height}",
            direction is null ? $"view {view ?? "front"}" : $"direction ({Join(direction)})",
        };

        if (projection == Projection.Orthographic)
        {
            parts.Add("orthographic");
        }

        if (look != Look.Shaded)
        {
            parts.Add(look == Look.Wireframe ? "wireframe" : "normals");
        }

        if (!highlight.IsEmpty)
        {
            parts.Add($"highlight {highlight.Counts()}");
        }

        if (zoom != 1)
        {
            parts.Add($"zoom {Join([zoom])}");
        }

        if (target is not null)
        {
            parts.Add($"target ({Join(target)})");
        }

        parts.Add($"{frame.Vertices} vertices");
        parts.Add($"{frame.Faces} faces");
        return string.Join(", ", parts);
    }

    /// <summary>The lists of a <c>highlight</c> as given, so that an error can name an entry by its place; empty when absent.</summary>
    private static (List<int> Vertices, List<(int A, int B)> Edges, List<int> Faces) HighlightLists(JsonElement? highlight, List<string> errors)
    {
        if (highlight is not { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } value)
        {
            return ([], [], []);
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"highlight: expected an object with any of vertices, edges and faces, got {Describe(value)}");
            return ([], [], []);
        }

        JsonElement? Part(string name) => value.TryGetProperty(name, out var part) ? part : null;
        return (Ids("vertices", Part("vertices"), errors) ?? [], Pairs("edges", Part("edges"), errors) ?? [], Ids("faces", Part("faces"), errors) ?? []);
    }

    private static bool Finite3(double[] v) => v.Length == 3 && v.All(double.IsFinite);

    private static string Join(IEnumerable<double> v) =>
        string.Join(", ", v.Select(x => x.ToString("G", CultureInfo.InvariantCulture)));

    private static string Join(IEnumerable<float> v) =>
        string.Join(", ", v.Select(x => x.ToString("G", CultureInfo.InvariantCulture)));

    // Interim until engine need 2: the engine encodes a PNG only into a file (WritePngAsync), so this
    // calls an encoder library directly, on the bytes that method would write.
    private static byte[] EncodePng(RenderedFrame frame)
    {
        var image = frame.Pixels.ToDecodedImage();
        using var png = new MemoryStream();
        new ImageWriter().WritePng(image.Pixels, image.Width, image.Height, ColorComponents.RedGreenBlueAlpha, png);
        return png.ToArray();
    }
}
