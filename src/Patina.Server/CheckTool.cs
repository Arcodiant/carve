using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Checks;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using StbImageSharp;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

/// <summary>
/// <c>check</c> (T-141): measures the open graph's outputs, or PNG files given with their roles, against
/// <see cref="CheckRules"/> and lists what is wrong with the fix in words, as carve's <c>check</c> lists mesh faults. It
/// reports and never refuses a graph that evaluates, and changes nothing.
/// </summary>
public sealed class CheckTool(GraphStore store, NodeRegistry registry, ToolFolder toolFolder)
{
    /// <summary>The largest size check evaluates a graph at.</summary>
    public const int MaxSize = 2048;

    /// <summary>The first worked example: the bricks, clean.</summary>
    public const string Worked = """{"size":64}""";

    /// <summary>What it returns for the bricks open (samples/graphs/bricks.texgraph).</summary>
    public const string WorkedResult = """{"ok":true,"problems":[],"notes":[],"stats":{"size":64,"outputs":{"base_colour":{"min":[108,45,30],"max":[194,186,169],"mean":[160.3,104.4,86.5]},"normal":{"min":[72,72,242],"max":[183,183,255],"mean":[127.9,127.7,250.3]},"roughness":{"min":[167],"max":[255],"mean":[202.6]},"occlusion":{"min":[115],"max":[255],"mean":[183.6]}}}}""";

    /// <summary>The second, run by the tests but not in the description since T-170 (the description is held under Claude Code's 4,096-character cut): samples/graphs/faults/chrome-in-paint.texgraph open, paint with a metallic of about 0.5.</summary>
    public const string WorkedFault = """{}""";

    public const string WorkedFaultResult = """{"ok":false,"problems":[{"check":"metallic-not-binary","output":"metallic","message":"100% of texels are between 0.1 and 0.9; over 5% is a fault. Fix: metallic is 1 on bare metal and 0 on everything else, with a sharp edge between; a middle value is a half-metal that does not exist in a real surface, and renders as a dull chrome. Drive it from a mask pushed to 0 and 1 (a levels node with in_low equal to in_high is a step), or set it to 0 or 1."}],"notes":["metal texels (metallic 0.5 or more): 52%"],"stats":{"size":64,"outputs":{"base_colour":{"min":[145,170,125],"max":[169,191,150],"mean":[157,180.8,137.7]},"normal":{"min":[115,116,254],"max":[141,140,255],"mean":[127.5,127.5,255]},"roughness":{"min":[159],"max":[199],"mean":[178.5]},"metallic":{"min":[121],"max":[134],"mean":[127.5]}}}}""";

    /// <summary>The third: PNG files, read with their roles.</summary>
    public const string WorkedFiles = """{"files":[{"path":"samples/graphs/bricks/base_colour.png","role":"base_colour"},{"path":"samples/graphs/bricks/normal.png","role":"normal"}]}""";

    public const string WorkedFilesResult = """{"ok":true,"problems":[],"notes":["base_colour: base_colour.png, 256x256","normal: normal.png, 256x256"],"stats":{"size":256,"outputs":{"base_colour":{"min":[107,44,30],"max":[196,188,171],"mean":[159.7,102.2,84.3]},"normal":{"min":[62,62,237],"max":[193,193,255],"mean":[127.9,127.8,248.4]}}}}""";

    public const string Text =
        "Measures the open graph's outputs as numbers, rather than judging by a render, and lists what is wrong with each, with the fix in words. "
        + "Changes nothing, and never refuses a graph that evaluates: a fault is a problem in the list. "
        + "Takes size (a power of two from 32 to 2048; default the graph's own, at most 2048), parameters (the graph's exposed parameters, for this call only) "
        + "and files (a list of {path, role} of PNGs, the path absolute or relative to patina's working directory; only those are checked, no graph is needed, and size and parameters are not used). "
        + "Role metallic_roughness is a packed image as export writes roughness.png (G roughness, B metallic); role roughness is roughness only. "
        + "The rules, each with its thresholds and what it means for a game:\n"
        + "- base-colour-range: base colour's brightest channel, in sRGB levels 0 to 255, should be 20 to 245 for a non-metal texel (metallic below 0.5, or no metallic) and 120 or more for a metal texel (metallic 0.5 or more); "
        + "near black is a hole under game lighting, near white blows out, and dark metal, which takes its colour from reflection, shows almost nothing. A fault when over 1% of texels are out. "
        + "Fix: pull the darks up and the lights in with a levels or gradient_map node.\n"
        + "- metallic-not-binary: metallic should be 0 or 1, with a sharp edge between (a half-metal is not a real surface and renders as dull chrome). A fault when over 5% of texels are from 0.1 to 0.9. "
        + "Fix: push the mask to 0 and 1 (a levels node with in_low equal to in_high is a step), or set a constant.\n"
        + "- normal-not-unit: a normal is a unit vector. A fault when over 0.1% of texels' length differs from 1 by more than 0.02 (0.03 for a PNG, for the 8-bit rounding). "
        + "Fix: make it with height_to_normal, which normalises, and do not mix a normal's components as colours.\n"
        + "- one-value: a greyscale output (roughness, occlusion, height, a greyscale named output; not metallic) whose texels all pack to one byte; a mask of one value masks nothing. "
        + "Fix: widen its levels, or drive it from a noise or pattern, or drop the output.\n"
        + "- does-not-tile: the mean step between neighbouring columns (and rows, for v), in packed levels over the channels, is measured at every column boundary, the wrap's between the last and first included; "
        + "the wrap is a fault when its step is more than twice the 99th percentile of the steps inside, plus 2 levels, so the texture shows a seam when it repeats (a pattern's own hard edge that falls on the wrap is not one). "
        + "Fix: whole-number frequencies and counts and quarter turns; swatch's tiled view shows where.\n"
        + "Result: {ok, problems:[{check, output, message}], notes, stats}. ok is true when there are no problems; each message gives the share, the worst texel and the fix; "
        + "notes say how the texels were read (the share of metal texels, a metallic read from a packed file); stats gives each output's min, max and mean per channel, in packed levels 0 to 255 "
        + "(a normal's too: 127.5 is zero), at stats.size (mixed-size files: each output's width and height). "
        + "Errors, writing nothing: no open graph (when no files are given), a graph with a fault (the error lists them; fix them with edit_graph), a size that is not allowed, a parameter that is refused, and a file that is missing or not a PNG.\n"
        + "Worked example, with samples/graphs/bricks.texgraph open: check " + Worked + " returns, clean:\n" + WorkedResult;

    public static McpServerTool Create() => Create<CheckTool>("check", Text, new JsonObject
    {
        ["size"] = new JsonObject { ["type"] = "integer", ["description"] = "A power of two from 32 to 2048; default the graph's own size (at most 2048)." },
        ["parameters"] = new JsonObject { ["type"] = "object", ["description"] = ParameterOverrides.Description },
        ["files"] = new JsonObject
        {
            ["type"] = "array",
            ["description"] = "PNG files to check instead of the open graph, each {path, role}: the role is base_colour, normal, roughness (roughness only), metallic_roughness (a packed image: G roughness, B metallic, as export writes roughness.png), metallic, occlusion, height, or a named output's name.",
            ["items"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["path"] = new JsonObject { ["type"] = "string" }, ["role"] = new JsonObject { ["type"] = "string" } },
                ["required"] = new JsonArray("path", "role"),
            },
        },
    });

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? size = null, JsonElement? parameters = null, JsonElement? files = null)
    {
        if (Unknown("check", context, "size", "parameters", "files") is { } unknown) return unknown;
        var haveFiles = files is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) };
        CheckSet set;
        int? pixels;
        if (haveFiles)
        {
            if (!TryFiles(files!.Value, out set, out var fileError)) return Error(fileError!);
            pixels = OneSize(set);
            var ignored = new List<string>();
            if (size is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) }) ignored.Add("size");
            if (parameters is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) }) ignored.Add("parameters");
            if (ignored.Count > 0)
                set = new(set.Source, set.Outputs, [.. set.Notes, $"{string.Join(" and ", ignored)} {(ignored.Count == 1 ? "was" : "were")} not used: with files only the files are checked"]);
        }
        else
        {
            TextureGraph graph;
            lock (store.Gate)
            {
                if (store.Graph is not { } open) return Error("check: " + NoGraph);
                graph = open;
            }
            var faults = GraphValidator.Validate(graph, registry);
            if (faults.Count > 0)
                return Error($"check: the graph has {Faults(faults)}\nA check needs a graph that evaluates; fix them with edit_graph, then check again.");
            if (!ParameterOverrides.TryApply("check", graph, parameters, registry, out graph, out _, out var overrideError))
                return Error(overrideError!);
            var side = Math.Min(graph.Size, MaxSize);
            if (size is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } given)
            {
                if (!(given.ValueKind == JsonValueKind.Number && given.TryGetInt32(out side) && TextureGraph.IsValidSize(side) && side <= MaxSize))
                    return Error($"check: size {given.GetRawText()} is not allowed; size is a power of two from 32 to {MaxSize} (32, 64, 128, 256, 512, 1024 or 2048)");
            }
            set = CheckSet.OfGraph(new GraphEvaluator(graph, registry), side);
            if (MeshNote(graph, registry) is { Length: > 0 } meshNote) set = new(set.Source, set.Outputs, [.. set.Notes, meshNote]);
            pixels = side;
        }
        return Line(Report(set, pixels).ToJsonString());
    }

    /// <summary>The one square size every output of <paramref name="set"/> has, or null when they differ or one is not square (RV-76).</summary>
    public static int? OneSize(CheckSet set) =>
        set.Outputs.Count > 0 && set.Outputs.All(o => o.Width == set.Outputs[0].Width && o.Height == set.Outputs[0].Width) ? set.Outputs[0].Width : null;

    /// <summary>
    /// The report for <paramref name="set"/>: <c>{ok, problems, notes, stats}</c>. With a <paramref name="size"/> it is
    /// <c>stats.size</c>; with none (files of mixed sizes) each output's stats name its own <c>width</c> and <c>height</c>.
    /// </summary>
    public static JsonObject Report(CheckSet set, int? size)
    {
        var problems = CheckRules.Run(set);
        var notes = new List<string>(set.Notes);
        foreach (var o in set.Outputs.Where(o => o.IsRole(OutputRole.BaseColour) && set.MetallicFor(o) is null && set.Find("metallic") is not null))
            notes.Add("base_colour and metallic are different sizes, so every base colour texel is judged as a non-metal");
        if (set.Find(OutputRoles.Name(OutputRole.Metallic)) is { IsGrey: true } metallic)
        {
            long metal = metallic.Data.LongCount(v => v >= BaseColourRangeRule.MetallicIsMetal);
            notes.Insert(0, $"metal texels (metallic 0.5 or more): {CheckRules.Percent(metal, metallic.Texels)}");
        }
        var outputs = new JsonObject();
        foreach (var o in set.Outputs)
        {
            var stats = Stats(o);
            if (size is null) { stats["width"] = o.Width; stats["height"] = o.Height; }
            outputs[o.Name] = stats;
        }
        var statsObject = size is { } one ? new JsonObject { ["size"] = one, ["outputs"] = outputs } : new JsonObject { ["outputs"] = outputs };
        return new JsonObject
        {
            ["ok"] = problems.Count == 0,
            ["problems"] = new JsonArray([.. problems.Select(p => (JsonNode)new JsonObject { ["check"] = p.Check, ["output"] = p.Output, ["message"] = p.Message })]),
            ["notes"] = new JsonArray([.. notes.Select(n => (JsonNode)n)]),
            ["stats"] = statsObject,
        };
    }

    /// <summary>An output's min, max and mean per channel, in packed levels, the mean to a tenth.</summary>
    private static JsonObject Stats(CheckOutput o)
    {
        var channels = o.Channels;
        var min = new int[channels]; var max = new int[channels]; var sum = new double[channels];
        Array.Fill(min, 255);
        for (long i = 0; i < o.Texels; i++)
            for (var c = 0; c < channels; c++)
            {
                int level = o.Level(o.Data[i * channels + c]);
                if (level < min[c]) min[c] = level;
                if (level > max[c]) max[c] = level;
                sum[c] += level;
            }
        return new JsonObject
        {
            ["min"] = new JsonArray([.. min.Select(v => (JsonNode)v)]),
            ["max"] = new JsonArray([.. max.Select(v => (JsonNode)v)]),
            ["mean"] = new JsonArray([.. sum.Select(s => (JsonNode)Math.Round(s / o.Texels, 1))]),
        };
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private bool TryFiles(JsonElement files, out CheckSet set, out string? error)
    {
        set = null!;
        error = null;
        if (files.ValueKind != JsonValueKind.Array || files.GetArrayLength() == 0)
        {
            error = "check: files is a list of {path, role}, such as [{\"path\":\"textures/planks/base_colour.png\",\"role\":\"base_colour\"}]";
            return false;
        }
        var decoded = new List<(string Role, int Width, int Height, byte[] Rgba)>();
        var notes = new List<string>();
        foreach (var item in files.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String
                || !item.TryGetProperty("role", out var r) || r.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(p.GetString()) || !GraphNames.IsValid(r.GetString()!))
            {
                error = $"check: files holds {item.GetRawText()}, which is not {{path, role}}: a path, and a role (base_colour, normal, roughness, metallic_roughness, metallic, occlusion, height) or a named output's name";
                return false;
            }
            var role = r.GetString()!;
            if (decoded.Any(d => d.Role == role))
            {
                error = $"check: files names the role {role} twice; give each output one file";
                return false;
            }
            if (decoded.Any(d => (d.Role, role) is ("roughness", CheckSet.PackedRole) or (CheckSet.PackedRole, "roughness")))
            {
                error = $"check: files names roughness and {CheckSet.PackedRole}, two roughness files; a {CheckSet.PackedRole} file is the roughness (its G), so give one of them";
                return false;
            }
            string full;
            try
            {
                full = toolFolder.Full(p.GetString()!);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error = $"check: could not read {JsonSerializer.Serialize(p.GetString())}: {e.Message}";
                return false;
            }
            if (!File.Exists(full))
            {
                error = $"check: the file {full} does not exist";
                return false;
            }
            try
            {
                var bytes = File.ReadAllBytes(full);
                // RV-76: StbImageSharp reads JPEG, BMP and others too; only a PNG is checked.
                if (!bytes.AsSpan().StartsWith(PngSignature))
                {
                    error = $"check: {full} is not a PNG (it does not start with the PNG signature)";
                    return false;
                }
                var image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
                decoded.Add((role, image.Width, image.Height, image.Data));
            }
            catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                error = $"check: could not read {full} as a PNG: {e.Message}";
                return false;
            }
            notes.Add($"{role}: {Path.GetFileName(full)}, {decoded[^1].Width}x{decoded[^1].Height}");
        }
        var read = CheckSet.OfPngs(decoded);
        set = new(read.Source, read.Outputs, [.. notes, .. read.Notes]);
        return true;
    }
}
