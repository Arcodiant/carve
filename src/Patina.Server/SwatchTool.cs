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
    public static readonly string[] Views = ["sheet", "flat", "tiled", "sphere", "cube"];

    /// <summary>The worked example: the planks' tiled base colour at 128.</summary>
    public const string Worked = """{"view":"tiled","output":"base_colour","size":128}""";

    /// <summary>The metal plate's named output, flat.</summary>
    public const string WorkedEdge = """{"view":"flat","output":"edge"}""";

    public const string WorkedEdgeResult = "swatch flat: edge of the open graph, 64 x 64 px, exact texels";

    /// <summary>What it returns for the planks, beside the image.</summary>
    public const string WorkedResult = "swatch tiled: base_colour of planks.texgraph, 3 x 3 of 128 px, 384 x 384 px, exact texels";

    public const string Text =
        "Shows the open graph's outputs as images. view: sheet (default) is one image: a column per output, the roles in role order (base_colour, normal, roughness, metallic, occlusion, height; a role the graph lacks has no column) and then the named outputs a to z, "
        + "each output flat on top and tiled 3 x 3 below in 256 px cells, beside a 512 px sphere wearing base colour, normal, roughness with metallic, and occlusion (height and named outputs are shown, not worn), under a title strip with the file, size and seed. "
        + "flat is one output's texels exactly, size px a side; tiled is its 3 x 3 repeat (3 x size, scaled down to at most 1536 px), to show seams; "
        + "sphere is the engine's UV sphere (u once around, v from the top pole) drawn with a PBR material made of the outputs (base colour sRGB, roughness as the metallic-roughness image with the graph's metallic in B and the material's metallic factor 1, or metallic 0 when the graph has none, normal and occlusion linear, mipmaps on; a missing role uses the material's default), "
        + "lit as Carve's render lights a model, on Carve's dark slate, from a fixed camera, 512 px. "
        + "metallic, height and greyscale named outputs are drawn grey; roughness's column is roughness alone. "
        + "output: a role or a named output's name, for flat and tiled (default the first role the graph has). size: the evaluation size, a power of two from 32 to 2048 (default the graph's size, at most 2048; for larger PNGs use export). "
        + "cube is a lit cube (edge 0.7, each face its own UVs 0 to 1, u to the right and v down as the face is seen) wearing the same material, turned so the front, the left and the top show, to judge a material on flat faces and the edges between them; lit and framed as the sphere is, 512 px. "
        + "tiling: a whole number from 1 to 8 (default 1), for sheet, sphere and cube: the sphere or cube wears every map repeated that many times in u and v, to judge a material at a texel density nearer a game's; the sheet's title and the result name it when it is not 1. "
        + "angle: the sphere's or cube's turn about y in degrees, for sheet, sphere and cube (default 0; counter-clockwise seen from above). "
        + "parameters: the graph's exposed parameters to set for this call only, by name (get_graph lists them and their ranges); the graph is not changed, a value out of range is refused, and the sheet's title and the result name them. "
        + "Refused while the graph has a fault. Changes nothing.\n"
        + "Worked examples, with samples/graphs/planks.texgraph open, each returning an image and a line: swatch " + Worked + " returns:\n" + WorkedResult + "\n"
        + "swatch " + WorkedCube + " returns:\n" + WorkedCubeResult + "\n"
        + "swatch " + WorkedTiling + " returns:\n" + WorkedTilingResult + "\n"
        + "After define_graph of the metal plate (nodes' output example): swatch " + WorkedEdge + " returns an image and:\n" + WorkedEdgeResult + "\n"
        + "With samples/graphs/bricks.texgraph open: swatch " + WorkedParameters + " shows grey stone bricks' base colour and returns:\n" + WorkedParametersResult + "\n"
        + "and swatch " + WorkedParametersRefused + " returns:\n" + WorkedParametersRefusedResult + "\n"
        + "and swatch " + WorkedTilingRefused + " returns:\n" + WorkedTilingRefusedResult;

    public static McpServerTool Create() => Create<SwatchTool>("swatch", Text, new JsonObject
    {
        ["view"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. Views.Select(v => (JsonNode)v)]) },
        ["output"] = new JsonObject { ["type"] = "string", ["description"] = "A role (" + string.Join(", ", OutputRoles.Names) + ") or a named output's name, for flat and tiled." },
        ["size"] = new JsonObject { ["type"] = "integer" },
        ["angle"] = new JsonObject { ["type"] = "number" },
        ["tiling"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = MaxTiling, ["description"] = "Whole repeats of every map over the sphere or cube in u and v, 1 to 8 (default 1), for sheet, sphere and cube." },
        ["parameters"] = new JsonObject { ["type"] = "object", ["description"] = ParameterOverrides.Description },
    });

    /// <summary>The overrides' worked example (T-139): the bricks as grey stone, without editing the graph.</summary>
    public const string WorkedParameters = """{"view":"flat","output":"base_colour","parameters":{"mortar":0.08,"tint":"#7a7a70"}}""";

    public const string WorkedParametersResult =
        "swatch flat: base_colour of bricks.texgraph, 256 x 256 px, exact texels; parameters mortar 0.08, tint \"#7a7a70\"";

    /// <summary>The sphere's tiling's largest value.</summary>
    public const int MaxTiling = 8;

    /// <summary>The tiling's worked example (T-156): the planks' sphere with every map repeated 3 x 3.</summary>
    public const string WorkedTiling = """{"view":"sphere","tiling":3}""";

    public const string WorkedTilingResult = "swatch sphere: planks.texgraph at 256 px on the engine's UV sphere, angle 0, tiling 3, 512 x 512 px";

    /// <summary>The cube's worked example (T-157): the planks' cube with every map repeated 3 x 3.</summary>
    public const string WorkedCube = """{"view":"cube","tiling":3}""";

    public const string WorkedCubeResult = "swatch cube: planks.texgraph at 256 px on a cube, each face 0 to 1 in u and v, angle 0, tiling 3, 512 x 512 px";

    public const string WorkedTilingRefused = """{"view":"sphere","tiling":9}""";

    public const string WorkedTilingRefusedResult = "swatch: tiling 9 is out of range; give a whole number from 1 to 8";

    public const string WorkedParametersRefused = """{"parameters":{"mortar":0.5}}""";

    public const string WorkedParametersRefusedResult = "swatch: parameters['mortar']: is 0.5, but must be a number from 0 to 0.45";

    public async Task<CallToolResult> Run(RequestContext<CallToolRequestParams> context, CancellationToken cancellationToken)
    {
        if (Unknown("swatch", context, "view", "output", "size", "angle", "tiling", "parameters") is { } unknown) return unknown;
        var arguments = context.Params?.Arguments ?? new Dictionary<string, JsonElement>();
        JsonElement? Arg(string name) => arguments.TryGetValue(name, out var v) ? v : null;

        if (!TryText("swatch", "view", Arg("view"), out var view, out var error)) return error!;
        view ??= "sheet";
        if (!Views.Contains(view)) return Error($"swatch: view is '{view}'; give sheet, flat, tiled, sphere or cube");
        if (!TryText("swatch", "output", Arg("output"), out var outputName, out error)) return error!;
        if (outputName is not null && view is "sheet" or "sphere" or "cube")
            return Error($"swatch: output is for flat and tiled; the {view} shows every output");
        if (Arg("angle") is { ValueKind: not JsonValueKind.Null } && view is "flat" or "tiled")
            return Error($"swatch: angle turns the sphere or cube, for sheet, sphere and cube; {view} has none");

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
        if (!ParameterOverrides.TryApply("swatch", graph, Arg("parameters"), registry, out graph, out var overrides, out var overrideError)) return Error(overrideError!);
        var set = overrides.Count == 0 ? "" : "parameters " + GraphParameters.Describe(overrides);
        var mesh = MeshNote(graph);
        var meshTitle = mesh == "" ? "" : "   mesh inputs neutral";
        CallToolResult Show(byte[] rgba, int width, int height, string caption) => Image(rgba, width, height, caption + (mesh == "" ? "" : "; " + mesh));

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
            // A double past float's range becomes infinity, which would turn the sphere by a NaN rotation.
            if (!float.IsFinite(angle)) return Error($"swatch: angle {angleArg.GetRawText()} is out of range; angle is a number of degrees");
        }

        var tiling = 1;
        if (Arg("tiling") is { ValueKind: not JsonValueKind.Null } tilingArg)
        {
            if (view is "flat" or "tiled") return Error($"swatch: tiling repeats the maps over the sphere or cube, for sheet, sphere and cube; {view} has none");
            if (tilingArg.ValueKind != JsonValueKind.Number || !tilingArg.TryGetInt32(out tiling))
                return Error($"swatch: tiling is a whole number from 1 to {MaxTiling}, not {Kind(tilingArg)} {tilingArg.GetRawText()}");
            if (tiling < 1 || tiling > MaxTiling) return Error($"swatch: tiling {tiling} is out of range; give a whole number from 1 to {MaxTiling}");
        }
        var tiled3 = tiling == 1 ? "" : $"tiling {tiling}";

        var evaluator = new GraphEvaluator(graph, registry);
        var roles = evaluator.Outputs;
        var name = path is null ? "the open graph" : System.IO.Path.GetFileName(path);

        if (view is "flat" or "tiled")
        {
            var role = roles[0];
            if (outputName is not null)
            {
                if (!GraphNames.IsValid(outputName))
                    return Error($"swatch: output is '{outputName}'; give a role ({string.Join(", ", OutputRoles.Names)}) or a named output's name");
                if (!roles.Contains(outputName))
                    return Error($"swatch: the graph has no {outputName} output; its outputs are {string.Join(", ", roles)}");
                role = outputName;
            }
            var texels = OutputImage.Rgba8(evaluator, role, size);
            if (view == "flat")
            {
                return Show(texels, size, size, $"swatch flat: {role} of {name}, {size} x {size} px, exact texels" + (set == "" ? "" : "; " + set));
            }
            var side = SwatchImages.TiledSide(size);
            var tiled = SwatchImages.Tiled(texels, size, side);
            var scaled = side == 3 * size ? "exact texels" : $"nearest-scaled from {3 * size} px";
            return Show(tiled, side, side, $"swatch tiled: {role} of {name}, 3 x 3 of {size} px, {side} x {side} px, {scaled}" + (set == "" ? "" : "; " + set));
        }

        // Each column as the sheet shows it (roughness with B 0, metallic, height and greyscale named outputs grey); the
        // sphere wears base colour, normal, the packed metallic-roughness image and occlusion.
        var images = roles.ToDictionary(r => r, r => OutputImage.Rgba8(evaluator, r, size), StringComparer.Ordinal);
        var metallic = evaluator.Has(OutputRoles.Name(OutputRole.Metallic));
        var worn = new Dictionary<OutputRole, byte[]>();
        foreach (var r in new[] { OutputRole.BaseColour, OutputRole.Normal, OutputRole.Roughness, OutputRole.Occlusion }.Where(r => evaluator.Has(OutputRoles.Name(r))))
            worn[r] = r == OutputRole.Roughness && metallic ? OutputImage.FileRgba8(evaluator, OutputRoles.Name(r), size) : images[OutputRoles.Name(r)];
        var title = $"{(path is null ? "unsaved graph" : name)}   {size} px   seed {graph.Seed.ToString(CultureInfo.InvariantCulture)}" + (set == "" ? "" : "   " + set) + (tiled3 == "" ? "" : "   " + tiled3) + meshTitle;
        var columns = roles.ToList();
        var labels = view == "sheet" ? SwatchSheet.Labels(title, columns, angle, tiling) : null;

        await status.WhenSettled.WaitAsync(cancellationToken);
        if (status.FaultMessage is { } fault) return Error("swatch: " + fault);
        var request = new SwatchRequest(new SwatchSpec(worn, size, angle, labels, metallic, tiling, view == "cube"));
        queue.Enqueue(request);
        await Task.WhenAny(request.Result, status.WhenStopped).WaitAsync(cancellationToken);
        if (!request.Result.IsCompleted) return Error("swatch: " + (status.FaultMessage ?? "the engine has stopped, so nothing can be drawn"));
        if (!request.Result.IsCompletedSuccessfully) return Error("swatch: the render failed: " + request.Result.Exception?.GetBaseException().Message);
        var frame = request.Result.Result;
        var angleText = angle.ToString("0.##", CultureInfo.InvariantCulture);

        if (view == "sphere")
        {
            return Show(frame.Sphere, SwatchImages.SphereCell, SwatchImages.SphereCell,
                $"swatch sphere: {name} at {size} px on the engine's UV sphere, angle {angleText}, {(tiling == 1 ? "" : $"tiling {tiling}, ")}{SwatchImages.SphereCell} x {SwatchImages.SphereCell} px" + (set == "" ? "" : "; " + set));
        }
        if (view == "cube")
        {
            return Show(frame.Sphere, SwatchImages.SphereCell, SwatchImages.SphereCell,
                $"swatch cube: {name} at {size} px on a cube, each face 0 to 1 in u and v, angle {angleText}, {(tiling == 1 ? "" : $"tiling {tiling}, ")}{SwatchImages.SphereCell} x {SwatchImages.SphereCell} px" + (set == "" ? "" : "; " + set));
        }
        var (sheet, layout) = SwatchSheet.Compose([.. columns.Select(r => images[r])], size, frame.Sphere, frame.Atlas!, frame.Extents!);
        return Show(sheet, layout.Width, layout.Height,
            $"swatch sheet: {name}, {size} px, seed {graph.Seed.ToString(CultureInfo.InvariantCulture)}{(set == "" ? "" : ", " + set)}; columns {string.Join(", ", columns)}, flat over tiled 3 x 3; sphere at angle {angleText}{(tiling == 1 ? "" : $", tiling {tiling}")}; {layout.Width} x {layout.Height} px");
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
