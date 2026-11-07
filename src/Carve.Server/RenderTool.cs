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
        schema["properties"]!["views"] = ViewsSchema();
        schema["properties"]!["columns"] = new JsonObject
        {
            ["description"] = "With views: tiles to a row, 1 to 3. Default: the number of views, at most 3.",
            ["type"] = "integer",
        };
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

    private static JsonObject ViewsSchema()
    {
        static JsonObject Numbers() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "number" } };
        return new JsonObject
        {
            ["description"] = "2 to 6 tiles of one sheet, each a view name or {view or direction, zoom, target}. Not with view, direction, zoom or target.",
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["type"] = "string" },
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["view"] = new JsonObject { ["type"] = "string" },
                            ["direction"] = Numbers(),
                            ["zoom"] = new JsonObject { ["type"] = "number" },
                            ["target"] = Numbers(),
                        },
                    }),
            },
        };
    }

    private const string Text =
        "Renders the current mesh, as set by define_mesh or open and changed by move, extrude or undo, and returns one PNG image, 512x512 pixels unless 'size' says otherwise, followed by one caption line, "
        + "e.g. '512x512, view front, 8 vertices, 6 faces'. The image is shaded by Empyrean, the engine games built on this model use. "
        + "No file is written. Nothing is drawn but the mesh, and the labels and tile captions you ask for: no grid or axes, a dark background, about 8% margin around the mesh. "
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
        + "'labels' writes element ids on the image, to read which face or vertex is which: 'none' (the default), 'faces', 'vertices' or 'both'. A face's id sits on its centre; a vertex is a white dot with its id just above and to the right. Each id is white on a dark backing, and is the id move, extrude and highlight take. "
        + "Only what faces the camera is labelled: a face turned away has no label, and a vertex has one when it is on a face turned toward you. Nearer geometry is not considered, so on a concave mesh a label may sit on a face hidden behind another; look from another side to be sure. Edges have no labels: name an edge by its two vertex ids. "
        + "Labels crowd on a dense mesh: use size 1024, or zoom in on a target. The caption says so after the mode: '512x512, view front, labels both, 8 vertices, 6 faces'. "
        + "'views' asks for a contact sheet instead of one view: one image of 2 to 6 tiles, so one call shows a mesh all round. Each entry is a view name, or an object with 'view' or 'direction' and optional 'zoom' and 'target', e.g. [\"front\", \"+x\", {\"direction\": [2, 1, 1], \"zoom\": 2, \"target\": [1, 0.4, 0.5]}]. "
        + "'views' replaces 'view', 'direction', 'zoom' and 'target'; 'mode', 'labels', 'size', 'projection' and 'highlight' apply to every tile. 'columns' (1 to 3, default the number of views, at most 3) sets the tiles to a row, filled in the order given; a tile left over is background. "
        + "Each tile is a full render at 'size', captioned in its top left corner with its view name or direction as you gave it, then any zoom and target, cut off at the tile's edge if longer. Tiles are 2 pixels apart, so a sheet is columns x size + (columns - 1) x 2 wide and rows x size + (rows - 1) x 2 high. "
        + "The quick all-round look is the six axis views, orthographic, at 256: {\"views\": [\"+x\", \"-x\", \"+y\", \"-y\", \"+z\", \"-z\"], \"projection\": \"orthographic\", \"size\": 256}, captioned '3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, 8 vertices, 6 faces'. "
        + "A sheet's caption gives columns by rows, the tile size, the parts a single caption has, and every tile's caption: '2x2 tiles of 512x512, wireframe, labels faces, views front, (2, 1, 1), +x, -z, 72 vertices, 70 faces'. "
        + "The light turns with the view, and faces turned different ways show as different brightnesses. "
        + "Faces wound the wrong way (clockwise seen from outside) show as MISSING: you see through them to whatever is behind. "
        + "An EMPTY result, an image of only the dark background with no error, means no face points at the camera: "
        + "the faces are wound the wrong way, or the mesh is an open surface seen from behind. "
        + "Try the other view, or fix the winding and call define_mesh again (move and extrude keep each face's winding, so they cannot fix it). "
        + "Tool errors, with the mesh untouched: both 'view' and 'direction' given; a view that is not one of the eight names; a direction of zero or not three finite numbers; "
        + "a zoom outside 1 to 8; a target that is not three finite numbers or lies outside the mesh's bounds; a projection other than 'perspective' or 'orthographic'; a size other than 256, 512 or 1024; a mode other than 'shaded', 'wireframe' or 'normals'; a highlight that is not an object, or whose lists are not lists of ids and [a, b] pairs; "
        + "a highlight naming a vertex or face the mesh does not have, or a pair that is not an edge, each listed by element as move does; labels other than 'none', 'faces', 'vertices' or 'both'; "
        + "views together with view, direction, zoom or target; fewer than 2 views or more than 6; an entry of views that is neither a view name nor an object with 'view' or 'direction', or one with both; an entry's view, direction, zoom or target that would be an error on its own, named by its place, e.g. 'the 3rd listed entry'; columns outside 1 to 3; an argument render does not take. "
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
        [Description("Element ids to write on the image: 'none' (the default), 'faces', 'vertices' or 'both'. Only what faces the camera."), AllowedValues("none", "faces", "vertices", "both")] string? labels = null,
        JsonElement? views = null,
        double? columns = null,
        CancellationToken cancellationToken = default)
    {
        if (UnknownArguments("render", context, "view", "direction", "zoom", "target", "projection", "size", "mode", "highlight", "labels", "views", "columns") is { Count: > 0 } unknown)
        {
            return Failed("render", unknown);
        }

        var sheet = !Absent(views);
        var given = context.Params?.Arguments;
        if (sheet && new[] { "view", "direction", "zoom", "target" }.Any(name => given?.ContainsKey(name) == true))
        {
            return Error("views: not with view, direction, zoom or target; give those in each entry instead.");
        }

        if (columns is { } asked && asked is not (1 or 2 or 3))
        {
            return Error($"columns: {Join([asked])} is outside 1 to 3.");
        }

        if (!sheet && CameraRefusal(view, direction, zoom, target) is { } refused)
        {
            return Error(refused);
        }

        string? tileProblem = null;
        var tiles = sheet ? Tiles(views!.Value, out tileProblem) : [new Tile(view, direction, zoom, target)];
        if (tileProblem is not null)
        {
            return Error(tileProblem);
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

        if (labels is not (null or "none" or "faces" or "vertices" or "both"))
        {
            return Error($"labels: '{labels}' is not a label set; use 'none', 'faces', 'vertices' or 'both'.");
        }

        var problems = new List<string>();
        var listed = HighlightLists(highlight, problems);
        if (problems.Count > 0)
        {
            return Failed("render", problems);
        }

        var projected = projection == "orthographic" ? Projection.Orthographic : Projection.Perspective;
        var look = mode switch { "wireframe" => Look.Wireframe, "normals" => Look.Normals, _ => Look.Shaded };
        var labelled = labels switch { "faces" => LabelSet.Faces, "vertices" => LabelSet.Vertices, "both" => LabelSet.Both, _ => LabelSet.None };

        await status.WhenSettled.WaitAsync(cancellationToken);
        if (status.FaultMessage is { } fault)
        {
            return Error(fault);
        }

        if (store.Current.Mesh is not { } mesh)
        {
            return Error("There is no mesh yet: call define_mesh first, or open to read a saved .carve file, then render.");
        }

        var bounds = MeshReport.Create(mesh);
        for (var k = 0; k < tiles.Count; k++)
        {
            if (tiles[k].TargetVector is { } t && TargetCheck.Refusal(t, bounds.BoundsMin, bounds.BoundsMax) is { } refusal)
            {
                return Error(sheet ? $"views: the {Ordinal(k)} listed entry: {refusal}" : refusal);
            }
        }

        if (MeshEdits.UnknownElements(mesh, listed.Vertices, listed.Edges, listed.Faces) is { Count: > 0 } missing)
        {
            return Failed("render", missing.Select(e => e.ToString()));
        }

        var picked = Highlight.Of(listed.Vertices, listed.Edges, listed.Faces);
        var posted = tiles
            .Select(tile => queue.Post(new RenderSpec(tile.Camera, projected, (int)size, look, labelled) { Highlight = picked, TileCaption = sheet ? tile.Caption : null }))
            .ToList();
        var rendering = Task.WhenAll(posted);

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

        var frames = rendering.Result;
        if (sheet)
        {
            var across = (int)(columns ?? Math.Min(3, tiles.Count));
            var composed = SheetComposer.Compose(
                [.. frames.Select(f => f.Pixels.ToDecodedImage().Pixels)], across, (int)size, SheetComposer.Srgb(RenderComposition.Background));
            return new CallToolResult
            {
                Content =
                [
                    ImageContentBlock.FromBytes(EncodePng(composed.Pixels, composed.Width, composed.Height), "image/png"),
                    new TextContentBlock { Text = SheetCaption(frames[^1], across, tiles, projected, look, labelled, picked) },
                ],
            };
        }

        var frame = frames[0];
        var image = frame.Pixels.ToDecodedImage();
        return new CallToolResult
        {
            Content =
            [
                ImageContentBlock.FromBytes(EncodePng(image.Pixels, image.Width, image.Height), "image/png"),
                new TextContentBlock { Text = Caption(frame, view, direction, projected, look, labelled, picked, zoom, target) },
            ],
        };
    }

    /// <summary>One camera of a render, as given: the whole of a single render, or an entry of <c>views</c>.</summary>
    private sealed record Tile(string? View, double[]? Direction, double Zoom, double[]? Target)
    {
        /// <summary>Taken in the mesh's own precision (float), so a target typed as the reported bound is inside (B-1).</summary>
        public Vector3? TargetVector => Target is null ? null : Vector(Target);

        public CameraSpec Camera => new(
            Direction is { } d ? Vector(d) : ViewFraming.NamedViews[View ?? "front"],
            (float)Zoom,
            TargetVector);

        /// <summary>What the tile says of itself: the name as given or <c>(x, y, z)</c>, then zoom and target as the single caption has them.</summary>
        public string Caption
        {
            get
            {
                var parts = new List<string> { Direction is null ? View ?? "front" : $"({Join(Direction)})" };
                AddZoomAndTarget(parts, Zoom, Target);
                return string.Join(", ", parts);
            }
        }
    }

    /// <summary>What is wrong with one camera's arguments, in the words of the argument; null when nothing is.</summary>
    private static string? CameraRefusal(string? view, double[]? direction, double zoom, double[]? target)
    {
        if (view is not null && direction is not null)
        {
            return "view and direction: give one, not both.";
        }

        if (view is not null && !ViewFraming.NamedViews.ContainsKey(view))
        {
            return $"view: '{view}' is not a view; use 'front', 'back', '+x', '-x', '+y', '-y', '+z' or '-z'.";
        }

        if (direction is not null)
        {
            if (!Finite3(direction))
            {
                return "direction: must be three finite numbers [x, y, z].";
            }

            var length = Vector(direction).Length();
            if (!float.IsFinite(length) || length < 1e-20f)
            {
                return "direction: must not be zero, and must be a usable size.";
            }
        }

        if (!(zoom >= 1 && zoom <= 8))
        {
            return $"zoom: {Join([zoom])} is outside 1 to 8.";
        }

        if (target is not null && !Finite3(target))
        {
            return "target: must be three finite numbers [x, y, z].";
        }

        return null;
    }

    /// <summary>The entries of <c>views</c>; <paramref name="problem"/> is the first wrong one, by its place.</summary>
    private static List<Tile> Tiles(JsonElement views, out string? problem)
    {
        problem = null;
        if (views.ValueKind != JsonValueKind.Array)
        {
            problem = $"views: expected a list of 2 to 6 views, got {Describe(views)}";
            return [];
        }

        var count = views.GetArrayLength();
        if (count is < 2 or > 6)
        {
            problem = $"views: {count} given; a sheet takes 2 to 6.";
            return [];
        }

        var tiles = new List<Tile>();
        foreach (var entry in views.EnumerateArray())
        {
            var tile = Entry(entry, out var wrong);
            wrong ??= CameraRefusal(tile!.View, tile.Direction, tile.Zoom, tile.Target);
            if (wrong is not null)
            {
                problem = $"views: the {Ordinal(tiles.Count)} listed entry: {wrong}";
                return [];
            }

            tiles.Add(tile!);
        }

        return tiles;
    }

    private static Tile? Entry(JsonElement entry, out string? problem)
    {
        const string Shape = "expected a view name or an object with 'view' or 'direction' and optional 'zoom' and 'target'";
        problem = null;
        if (entry.ValueKind == JsonValueKind.String)
        {
            return new Tile(entry.GetString(), null, 1, null);
        }

        if (entry.ValueKind != JsonValueKind.Object)
        {
            problem = $"{Shape}, got {Describe(entry)}";
            return null;
        }

        foreach (var key in entry.EnumerateObject().Select(p => p.Name))
        {
            if (key is not ("view" or "direction" or "zoom" or "target"))
            {
                problem = $"{Shape}, got '{key}'";
                return null;
            }
        }

        JsonElement? Part(string name) => entry.TryGetProperty(name, out var part) && !Absent(part) ? part : null;
        var (view, direction, zoom, target) = (Part("view"), Part("direction"), Part("zoom"), Part("target"));
        if (view is null && direction is null)
        {
            problem = $"{Shape}, got neither 'view' nor 'direction'";
            return null;
        }

        if (view is { ValueKind: not JsonValueKind.String })
        {
            problem = $"view: expected a string, got {Describe(view.Value)}";
            return null;
        }

        if (zoom is { ValueKind: not JsonValueKind.Number })
        {
            problem = $"zoom: expected a number, got {Describe(zoom.Value)}";
            return null;
        }

        var numbers = (Direction: Numbers(direction), Target: Numbers(target));
        if (direction is not null && numbers.Direction is null)
        {
            problem = "direction: must be three finite numbers [x, y, z].";
            return null;
        }

        if (target is not null && numbers.Target is null)
        {
            problem = "target: must be three finite numbers [x, y, z].";
            return null;
        }

        return new Tile(view?.GetString(), numbers.Direction, zoom?.GetDouble() ?? 1, numbers.Target);
    }

    private static double[]? Numbers(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Array } list && list.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Number)
            ? [.. list.EnumerateArray().Select(x => x.GetDouble())]
            : null;

    /// <summary>
    /// The size; <c>view front</c> for a view or <c>direction (x, y, z)</c> as given for a direction; then
    /// each thing asked for beyond the default, only where set: the projection, the mode, the labels, the
    /// highlight, zoom, target. The counts close it, so a part added here goes before them.
    /// </summary>
    internal static string Caption(RenderedFrame frame, string? view, double[]? direction, Projection projection, Look look, LabelSet labels, Highlight highlight, double zoom, double[]? target)
    {
        var parts = new List<string>
        {
            $"{frame.Pixels.Width}x{frame.Pixels.Height}",
            direction is null ? $"view {view ?? "front"}" : $"direction ({Join(direction)})",
        };

        AddLookParts(parts, projection, look, labels, highlight);
        AddZoomAndTarget(parts, zoom, target);
        return WithCounts(parts, frame);
    }

    /// <summary>
    /// <c>3x2 tiles of 256x256</c>, columns by rows; the parts every tile shares, as the single caption has them;
    /// then <c>views</c> and each tile's own caption, so a caption cut off at a tile's edge is whole here; the counts.
    /// </summary>
    private static string SheetCaption(RenderedFrame frame, int columns, IReadOnlyList<Tile> tiles, Projection projection, Look look, LabelSet labels, Highlight highlight)
    {
        var parts = new List<string> { $"{columns}x{SheetComposer.Rows(tiles.Count, columns)} tiles of {frame.Pixels.Width}x{frame.Pixels.Height}" };
        AddLookParts(parts, projection, look, labels, highlight);
        parts.Add($"views {string.Join(", ", tiles.Select(t => t.Caption))}");
        return WithCounts(parts, frame);
    }

    /// <summary>The caption's parts closed by the counts, joined.</summary>
    private static string WithCounts(List<string> parts, RenderedFrame frame)
    {
        parts.Add($"{frame.Vertices} vertices");
        parts.Add($"{frame.Faces} faces");
        return string.Join(", ", parts);
    }

    private static void AddLookParts(List<string> parts, Projection projection, Look look, LabelSet labels, Highlight highlight)
    {
        if (projection == Projection.Orthographic)
        {
            parts.Add("orthographic");
        }

        if (look != Look.Shaded)
        {
            parts.Add(look == Look.Wireframe ? "wireframe" : "normals");
        }

        if (labels != LabelSet.None)
        {
            parts.Add($"labels {labels.ToString().ToLowerInvariant()}");
        }

        if (!highlight.IsEmpty)
        {
            parts.Add($"highlight {highlight.Counts()}");
        }
    }

    private static void AddZoomAndTarget(List<string> parts, double zoom, double[]? target)
    {
        if (zoom != 1)
        {
            parts.Add($"zoom {Join([zoom])}");
        }

        if (target is not null)
        {
            parts.Add($"target ({Join(target)})");
        }
    }

    /// <summary>The lists of a <c>highlight</c> as given, so that an error can name an entry by its place; empty when absent.</summary>
    private static (List<int> Vertices, List<(int A, int B)> Edges, List<int> Faces) HighlightLists(JsonElement? highlight, List<string> errors)
    {
        if (Absent(highlight))
        {
            return ([], [], []);
        }

        var value = highlight!.Value;
        if (value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"highlight: expected an object with any of vertices, edges and faces, got {Describe(value)}");
            return ([], [], []);
        }

        JsonElement? Part(string name) => value.TryGetProperty(name, out var part) ? part : null;
        return (Ids("vertices", Part("vertices"), errors) ?? [], Pairs("edges", Part("edges"), errors) ?? [], Ids("faces", Part("faces"), errors) ?? []);
    }

    private static bool Finite3(double[] v) => v.Length == 3 && v.All(double.IsFinite);

    /// <summary>Three numbers as given, narrowed to the mesh's own precision.</summary>
    private static Vector3 Vector(double[] v) => new((float)v[0], (float)v[1], (float)v[2]);

    private static string Join(IEnumerable<double> v) =>
        string.Join(", ", v.Select(x => x.ToString("G", CultureInfo.InvariantCulture)));

    // Interim until engine need 2: the engine encodes a PNG only into a file (WritePngAsync), so this
    // calls an encoder library directly, on the bytes that method would write.
    private static byte[] EncodePng(byte[] pixels, int width, int height)
    {
        using var png = new MemoryStream();
        new ImageWriter().WritePng(pixels, width, height, ColorComponents.RedGreenBlueAlpha, png);
        return png.ToArray();
    }
}
