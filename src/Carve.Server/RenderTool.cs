using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StbImageWriteSharp;

namespace Carve.Server;

[McpServerToolType]
internal static class RenderTool
{
    private const string Text =
        "Renders the current mesh, as set by define_mesh, and returns one PNG image of 512x512 pixels followed by one caption line, "
        + "e.g. '512x512, view front, 8 vertices, 6 faces'. The image is shaded by Empyrean, the engine games built on this model use. "
        + "No file is written. Nothing is drawn but the mesh: no grid or axes, a dark background, about 8% margin around the mesh. "
        + "Views: 'front' (the default) looks from the +X +Y +Z corner, showing the mesh's top, its +Z side on the left and its +X side on the right; "
        + "'back' looks from the opposite corner, -X -Y -Z, upward from below, showing its underside, -Z side and -X side. "
        + "The camera frames the whole mesh each time, so a render shows shape, not size. "
        + "The light turns with the view, and faces turned different ways show as different brightnesses. "
        + "Faces wound the wrong way (clockwise seen from outside) show as MISSING: you see through them to whatever is behind. "
        + "An EMPTY result, an image of only the dark background with no error, means no face points at the camera: "
        + "the faces are wound the wrong way, or the mesh is an open surface seen from behind. "
        + "Try the other view, or fix the winding and call define_mesh again. "
        + "Calling render before define_mesh is a tool error saying so. A machine with no working Vulkan device gives a tool error naming Vulkan; "
        + "define_mesh still works there.";

    [McpServerTool(Name = "render"), Description(Text)]
    public static async Task<CallToolResult> Run(
        EngineStatus status,
        ModelStore store,
        RenderQueue queue,
        [Description("Which corner to look from: 'front' or 'back'."), AllowedValues("front", "back")] string view = "front",
        CancellationToken cancellationToken = default)
    {
        if (view is not ("front" or "back"))
        {
            return Error($"view: '{view}' is not a view; use 'front' or 'back'.");
        }

        await status.WhenSettled.WaitAsync(cancellationToken);
        if (status.FaultMessage is { } fault)
        {
            return Error(fault);
        }

        if (store.Current.Mesh is null)
        {
            return Error("There is no mesh yet: call define_mesh first, then render.");
        }

        var rendering = queue.Post(view == "front" ? RenderView.Front : RenderView.Back);

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
                new TextContentBlock
                {
                    Text = $"{frame.Pixels.Width}x{frame.Pixels.Height}, view {view}, {frame.Vertices} vertices, {frame.Faces} faces",
                },
            ],
        };
    }

    // Interim until engine need 2: the engine encodes a PNG only into a file (WritePngAsync), so this
    // calls an encoder library directly, on the bytes that method would write.
    private static byte[] EncodePng(RenderedFrame frame)
    {
        var image = frame.Pixels.ToDecodedImage();
        using var png = new MemoryStream();
        new ImageWriter().WritePng(image.Pixels, image.Width, image.Height, ColorComponents.RedGreenBlueAlpha, png);
        return png.ToArray();
    }

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };
}
