using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

/// <summary>
/// <c>swatch</c> (T-114): the open graph seen as a sheet of every output flat and tiled 3 x 3 beside a sphere wearing them
/// all, or one of those views alone. Flat and tiled are the evaluated texels; the sphere is the engine's.
/// </summary>
public sealed class SwatchTool(GraphStore store, NodeRegistry registry, EngineStatus status, OffscreenQueue<SwatchRequest> queue)
{
    public static readonly string[] Views = ["sheet", "flat", "tiled", "sphere"];

    /// <summary>The worked example: the planks' tiled base colour at 128.</summary>
    public const string Worked = """{"view":"tiled","output":"base_colour","size":128}""";

    /// <summary>What it returns for the planks, beside the image.</summary>
    public const string WorkedResult = "swatch tiled: base_colour of planks.texgraph, 3 x 3 of 128 px, 384 x 384 px, exact texels";

    public const string Text =
        "Shows the open graph's outputs as images. view: sheet (default) is one image: a column per output in role order (base_colour, normal, roughness, occlusion; a role the graph lacks has no column), "
        + "each output flat on top and tiled 3 x 3 below in 256 px cells, beside a 512 px sphere wearing all of them, under a title strip with the file, size and seed. "
        + "flat is one output's texels exactly, size px a side; tiled is its 3 x 3 repeat (3 x size, scaled down to at most 1536 px), to show seams; "
        + "sphere is the engine's UV sphere (u once around, v from the top pole) drawn with a PBR material made of the outputs (base colour sRGB, roughness as metallic-roughness with metallic 0, normal and occlusion linear, mipmaps on; a missing role uses the material's default), "
        + "lit by Carve's fixed sky alone on Carve's dark slate, from a fixed camera, 512 px. "
        + "output: a role, for flat and tiled (default the first role the graph has). size: the evaluation size, a power of two from 32 to 2048 (default the graph's size, at most 2048; for larger PNGs use export). "
        + "angle: the sphere's turn about y in degrees, for sheet and sphere (default 0; counter-clockwise seen from above). Refused while the graph has a fault. Changes nothing.\n"
        + "Worked example, with the planks open (samples/graphs/planks.texgraph): swatch " + Worked + " returns an image and:\n" + WorkedResult;

    public static McpServerTool Create() => Create<SwatchTool>("swatch", Text, new JsonObject
    {
        ["view"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. Views.Select(v => (JsonNode)v)]) },
        ["output"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. OutputRoles.Names.Select(v => (JsonNode)v)]) },
        ["size"] = new JsonObject { ["type"] = "integer" },
        ["angle"] = new JsonObject { ["type"] = "number" },
    });

    public async Task<CallToolResult> Run(RequestContext<CallToolRequestParams> context, CancellationToken cancellationToken)
    {
        if (Unknown("swatch", context, "view", "output", "size", "angle") is { } unknown) return unknown;
        var arguments = context.Params?.Arguments ?? new Dictionary<string, JsonElement>();
        JsonElement? Arg(string name) => arguments.TryGetValue(name, out var v) ? v : null;

        if (!TryText("swatch", "view", Arg("view"), out var view, out var error)) return error!;
        view ??= "sheet";
        if (!Views.Contains(view)) return Error($"swatch: view is '{view}'; give sheet, flat, tiled or sphere");
        if (!TryText("swatch", "output", Arg("output"), out var outputName, out error)) return error!;
        if (outputName is not null && view is "sheet" or "sphere")
            return Error($"swatch: output is for flat and tiled; the {view} shows every output");
        if (Arg("angle") is { ValueKind: not JsonValueKind.Null } && view is "flat" or "tiled")
            return Error($"swatch: angle turns the sphere, for sheet and sphere; {view} has none");

        TextureGraph graph;
        string? path;
        lock (store.Gate)
        {
            if (store.Graph is not { } open) return Error("swatch: " + NoGraph);
            graph = open;
            path = store.Path;
        }
        var faults = GraphValidator.Validate(graph, registry);
        if (faults.Count > 0) return Error("swatch: the graph does not evaluate; fix it first. " + Faults(faults));

        int size;
        if (Arg("size") is { ValueKind: not JsonValueKind.Null } sizeArg)
        {
            if (sizeArg.ValueKind != JsonValueKind.Number || !sizeArg.TryGetInt32(out size))
                return Error($"swatch: size is a whole number, a power of two from 32 to {SwatchImages.MaxSize}, not {Kind(sizeArg)} {sizeArg.GetRawText()}");
            if (size > SwatchImages.MaxSize)
                return Error($"swatch: size {size} is above {SwatchImages.MaxSize}, the largest swatch draws; use export for larger PNGs");
            if (size < 32 || (size & (size - 1)) != 0)
                return Error($"swatch: size {size} is not a power of two from 32 to {SwatchImages.MaxSize}");
        }
        else
        {
            size = Math.Min(graph.Size, SwatchImages.MaxSize);
        }

        var angle = 0f;
        if (Arg("angle") is { ValueKind: not JsonValueKind.Null } angleArg)
        {
            if (angleArg.ValueKind != JsonValueKind.Number || !double.IsFinite(angleArg.GetDouble()))
                return Error($"swatch: angle is a number of degrees, not {Kind(angleArg)}");
            angle = (float)angleArg.GetDouble();
        }

        var evaluator = new GraphEvaluator(graph, registry);
        var roles = evaluator.Roles;
        var name = path is null ? "the open graph" : System.IO.Path.GetFileName(path);

        if (view is "flat" or "tiled")
        {
            var role = roles[0];
            if (outputName is not null)
            {
                if (!OutputRoles.TryParse(outputName, out role))
                    return Error($"swatch: output is '{outputName}'; give base_colour, normal, roughness or occlusion");
                if (!roles.Contains(role))
                    return Error($"swatch: the graph has no {outputName} output; its outputs are {string.Join(", ", roles.Select(OutputRoles.Name))}");
            }
            var texels = OutputImage.Rgba8(evaluator, role, size);
            if (view == "flat")
            {
                return Image(texels, size, size, $"swatch flat: {OutputRoles.Name(role)} of {name}, {size} x {size} px, exact texels");
            }
            var side = SwatchImages.TiledSide(size);
            var tiled = SwatchImages.Tiled(texels, size, side, SwatchBreak.TiledShift ? size / 2 : 0);
            var scaled = side == 3 * size ? "exact texels" : $"nearest-scaled from {3 * size} px";
            return Image(tiled, side, side, $"swatch tiled: {OutputRoles.Name(role)} of {name}, 3 x 3 of {size} px, {side} x {side} px, {scaled}");
        }

        var images = roles.ToDictionary(r => r, r => OutputImage.Rgba8(evaluator, r, size));
        var title = $"{(path is null ? "unsaved graph" : name)}   {size} px   seed {graph.Seed.ToString(CultureInfo.InvariantCulture)}";
        var columns = SwatchBreak.RoleOrder ? roles.Reverse().ToList() : roles.ToList();
        var labels = view == "sheet" ? SwatchSheet.Labels(title, columns, angle) : null;

        await status.WhenSettled.WaitAsync(cancellationToken);
        if (status.FaultMessage is { } fault) return Error("swatch: " + fault);
        var request = new SwatchRequest(new SwatchSpec(images, size, angle, labels));
        queue.Enqueue(request);
        await Task.WhenAny(request.Result, status.WhenStopped).WaitAsync(cancellationToken);
        if (!request.Result.IsCompleted) return Error("swatch: " + (status.FaultMessage ?? "the engine has stopped, so nothing can be drawn"));
        if (!request.Result.IsCompletedSuccessfully) return Error("swatch: the render failed: " + request.Result.Exception?.GetBaseException().Message);
        var frame = request.Result.Result;
        var angleText = angle.ToString("0.##", CultureInfo.InvariantCulture);

        if (view == "sphere")
        {
            return Image(frame.Sphere, SwatchImages.SphereCell, SwatchImages.SphereCell,
                $"swatch sphere: {name} at {size} px on the engine's UV sphere, angle {angleText}, {SwatchImages.SphereCell} x {SwatchImages.SphereCell} px");
        }
        var (sheet, layout) = SwatchSheet.Compose([.. columns.Select(r => images[r])], size, frame.Sphere, frame.Atlas!, frame.Extents!,
            SwatchBreak.TiledShift ? size / 2 : 0);
        return Image(sheet, layout.Width, layout.Height,
            $"swatch sheet: {name}, {size} px, seed {graph.Seed.ToString(CultureInfo.InvariantCulture)}; columns {string.Join(", ", columns.Select(OutputRoles.Name))}, flat over tiled 3 x 3; sphere at angle {angleText}; {layout.Width} x {layout.Height} px");
    }

    private static CallToolResult Image(byte[] rgba, int width, int height, string caption) => new()
    {
        Content =
        [
            ImageContentBlock.FromBytes(Png.Encode(rgba, width, height), "image/png"),
            new TextContentBlock { Text = caption },
        ],
    };
}
