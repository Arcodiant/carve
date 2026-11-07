using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
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
        tool.ProtocolTool.InputSchema = Closed(tool.ProtocolTool.InputSchema);
        return tool;
    }

    private const string Text =
        "Renders the current mesh, as set by define_mesh or open and changed by move, extrude or undo, and returns one PNG image of 512x512 pixels followed by one caption line, "
        + "e.g. '512x512, view front, 8 vertices, 6 faces'. The image is shaded by Empyrean, the engine games built on this model use. "
        + "No file is written. Nothing is drawn but the mesh: no grid or axes, a dark background, about 8% margin around the mesh. "
        + "Views: 'front' (the default) looks from the +X +Y +Z corner, showing the mesh's top, its +Z side on the left and its +X side on the right; "
        + "'back' looks from the opposite corner, -X -Y -Z, upward from below, showing its underside, -Z side and -X side. "
        + "Or give 'direction' instead of 'view': [x, y, z], pointing from the point looked at toward the eye, in the mesh's own axes, any length. "
        + "Up on the image is +Y, or -Z when the direction is within 1 degree of straight up or down. "
        + "Examples: [1,0,0] looks from +X, [-1,0,0] from -X, [0,1,0] from above, down at the mesh, [0,-1,0] from below, [0,0,1] from +Z, [0,0,-1] from -Z. "
        + "A head-on axis view shows every face pointing one way as one flat shade, so a slight tilt such as [0.4, 2, 0.8] reads better from above. "
        + "'zoom' (1 to 8, default 1) narrows the view about 'target' without moving the camera; 'target' is the point [x, y, z] to look at and zoom on, "
        + "in the mesh's own coordinates and inside its bounds (default: the centre of the bounds). "
        + "The camera frames the whole mesh each time, so a render shows shape, not size; zoom in on a target to see a detail. "
        + "The caption then reads e.g. '512x512, direction (2, 1, 1), zoom 3, target (1.02, 0.4, 0.5), 72 vertices, 70 faces', "
        + "with zoom and target only when you set them. "
        + "The light turns with the view, and faces turned different ways show as different brightnesses. "
        + "Faces wound the wrong way (clockwise seen from outside) show as MISSING: you see through them to whatever is behind. "
        + "An EMPTY result, an image of only the dark background with no error, means no face points at the camera: "
        + "the faces are wound the wrong way, or the mesh is an open surface seen from behind. "
        + "Try the other view, or fix the winding and call define_mesh again (move and extrude keep each face's winding, so they cannot fix it). "
        + "Tool errors, with the mesh untouched: both 'view' and 'direction' given; a direction of zero or not three finite numbers; "
        + "a zoom outside 1 to 8; a target that is not three finite numbers or lies outside the mesh's bounds; an argument render does not take. "
        + "Calling render with no mesh (before define_mesh or open) is a tool error saying so. A machine with no working Vulkan device gives a tool error naming Vulkan; "
        + "define_mesh, open, move, extrude, get_mesh and undo still work there.";

    [McpServerTool(Name = "render"), Description(Text)]
    public static async Task<CallToolResult> Run(
        RequestContext<CallToolRequestParams> context,
        EngineStatus status,
        ModelStore store,
        RenderQueue queue,
        [Description("Which corner to look from: 'front' (the default when no direction is given) or 'back'. Not with direction."), AllowedValues("front", "back")] string? view = null,
        [Description("[x, y, z]: from the point looked at toward the eye. Up is +Y, or -Z within 1 degree of vertical. Not with view.")] double[]? direction = null,
        [Description("1 to 8: how far to zoom in about the target. The default 1 frames the whole mesh.")] double zoom = 1,
        [Description("[x, y, z] in the mesh's coordinates, inside its bounds: the point to look at. Default: the centre.")] double[]? target = null,
        CancellationToken cancellationToken = default)
    {
        if (UnknownArguments("render", context, "view", "direction", "zoom", "target") is { Count: > 0 } unknown)
        {
            return Failed("render", unknown);
        }

        if (view is not null && direction is not null)
        {
            return Error("view and direction: give one, not both.");
        }

        if (view is not (null or "front" or "back"))
        {
            return Error($"view: '{view}' is not a view; use 'front' or 'back'.");
        }

        Vector3 directionVector = CameraSpec.ForView(view == "back" ? RenderView.Back : RenderView.Front).Direction;
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

        var rendering = queue.Post(new CameraSpec(directionVector, (float)zoom, targetVector));

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
                new TextContentBlock { Text = Caption(frame, view, direction, zoom, target) },
            ],
        };
    }

    /// <summary>
    /// <c>view front</c> for a view; <c>direction (x, y, z)</c> as given for a direction; then zoom and
    /// target only where set, then the counts.
    /// </summary>
    internal static string Caption(RenderedFrame frame, string? view, double[]? direction, double zoom, double[]? target)
    {
        var parts = new List<string>
        {
            $"{frame.Pixels.Width}x{frame.Pixels.Height}",
            direction is null ? $"view {view ?? "front"}" : $"direction ({Join(direction)})",
        };

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
