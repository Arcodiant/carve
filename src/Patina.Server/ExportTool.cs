using System.Globalization;
using System.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

/// <summary>T-115: writes the open graph's outputs as PNGs at a size the agent names, one output at a time so memory stays bounded.</summary>
public sealed class ExportTool(GraphStore store, NodeRegistry registry, ToolFolder toolFolder)
{
    public const string Worked = """{"folder":"textures/planks","size":1024,"outputs":["base_colour","normal"]}""";

    /// <summary>export's first line for the worked example on a graph with all four outputs, with &lt;folder&gt; for the absolute folder.</summary>
    public const string WorkedHead = "export: wrote 2 files to <folder> at size 1024";

    /// <summary>export on define_graph's metal plate: metallic goes into roughness.png, and the named output edge is a file of its own.</summary>
    public const string WorkedMetal = """{"folder":"textures/plate"}""";

    public const string WorkedMetalResult =
        "export: wrote 3 files to <folder> at size 64\n- base_colour.png: 64x64, <bytes> bytes, slot base_colour_texture\n"
        + "- roughness.png: 64x64, <bytes> bytes, slot metallic_roughness_texture, metallic in B\n- edge.png: 64x64, <bytes> bytes, no Carve slot (a named output)";

    /// <summary>The Carve material slot each role's file belongs in, by the define_material argument that takes it; null for height (Carve does not use it yet).</summary>
    public static string? Slot(OutputRole role) => role switch
    {
        OutputRole.BaseColour => "base_colour_texture",
        OutputRole.Normal => "normal_texture",
        OutputRole.Roughness or OutputRole.Metallic => "metallic_roughness_texture",
        OutputRole.Occlusion => "occlusion_texture",
        _ => null,
    };

    public const string Text =
        "Writes the open graph's outputs as PNG files, ready for a Carve material's texture slots. Takes folder (required; absolute or relative to patina's working directory; created if missing), "
        + "size (a power of two from 32 to 8192; default the graph's own size) and outputs (a list of roles from base_colour, normal, roughness, occlusion, height, and named outputs by their names; default every output the graph has). "
        + "Each file is <folder>/<role>.png or <folder>/<name>.png, overwriting a file of that name (the result says replaced), written as the graph's outputs are packed for glTF: base_colour is sRGB; normal is tangent space, green up; "
        + "roughness is a glTF metallic-roughness image (R 255, roughness in G, the graph's metallic in B, 0 when it has none), so define_material's metallic_roughness_texture takes it as it is; metallic has no file of its own (name roughness to write it); occlusion and height are grey; "
        + "a named output is grey if greyscale, sRGB if colour. "
        + "The result lists each file with its size, its bytes and the Carve material slot it belongs in: base_colour in base_colour_texture, normal in normal_texture, roughness in metallic_roughness_texture, occlusion in occlusion_texture; height and named outputs have no slot. "
        + "Exporting the graph at its own size gives the same bytes every time, and equals the PNGs beside samples/graphs/planks.texgraph. "
        + "Outputs are evaluated and written one at a time, in tiles, so memory stays bounded at 8192 (the planks peaked at 0.7 GB), but 8192 is slow, mostly evaluation: the planks' four outputs took 28 to 31 s in release, 3 to 4 min in debug, each PNG 0.3 to 1 MB. A client with a short call timeout should ask for one or two outputs at a time. "
        + "To look at a graph, use swatch (at 2048 or below); export is for files. "
        + "Refused, writing nothing, while the graph has a fault (the error lists them; fix them with edit_graph); no open graph; a size that is not a power of two from 32 to 8192; an output the graph has not; metallic named alone; "
        + "and a folder that cannot be written (the error names the folder and the system's reason).\n"
        + "Worked example, after open of samples/graphs/planks.texgraph (<folder> is the absolute path of textures/planks under patina's working directory):\n"
        + "export " + Worked + "\nreturns, a line for each file:\n"
        + WorkedHead + "\n- base_colour.png: 1024x1024, <bytes> bytes, slot base_colour_texture\n- normal.png: 1024x1024, <bytes> bytes, slot normal_texture\n"
        + "parameters: the graph's exposed parameters to set for this export only, by name (get_graph lists them and their ranges); the graph is not changed, the first line names them, and a value out of range is refused naming the parameter and its range, writing nothing. "
        + "The files equal those of the same values set in the graph with edit_graph's set_parameters, byte for byte.\n"
        + "Worked example, after open of samples/graphs/bricks.texgraph: export " + WorkedParameters + " writes its four outputs as grey stone bricks (equal to samples/graphs/bricks/variants/grey/) and its first line is:\n" + WorkedParametersHead + "\n"
        + "Worked example, after define_graph of the metal plate (nodes' output example) (<folder> is the absolute path of textures/plate):\n"
        + "export " + WorkedMetal + "\nreturns:\n" + WorkedMetalResult;

    public static McpServerTool Create() => Create<ExportTool>("export", Text, new JsonObject
    {
        ["folder"] = new JsonObject { ["type"] = "string", ["description"] = "The folder to write <role>.png and <name>.png files into, absolute or relative to patina's working directory; created if missing." },
        ["size"] = new JsonObject { ["type"] = "integer", ["description"] = "A power of two from 32 to 8192; default the graph's own size." },
        ["outputs"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string" },
            ["description"] = "The outputs to write, each a role (base_colour, normal, roughness, occlusion, height; metallic is written in roughness.png) or a named output's name; default every output the graph has.",
        },
        ["parameters"] = new JsonObject { ["type"] = "object", ["description"] = ParameterOverrides.Description },
    }, "folder");

    /// <summary>The overrides' worked example (T-139), on the bricks: grey stone bricks written without editing the graph.</summary>
    public const string WorkedParameters = """{"folder":"textures/bricks_grey","parameters":{"mortar":0.08,"tint":"#7a7a70"}}""";

    public const string WorkedParametersHead = "export: wrote 4 files to <folder> at size 256, parameters mortar 0.08, tint \"#7a7a70\"";

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? folder = null, JsonElement? size = null, JsonElement? outputs = null, JsonElement? parameters = null, CancellationToken cancellationToken = default)
    {
        if (Unknown("export", context, "folder", "size", "outputs", "parameters") is { } unknown) return unknown;
        if (!TryText("export", "folder", folder, out var folderText, out var bad)) return bad!;
        if (string.IsNullOrWhiteSpace(folderText)) return Error("export: folder is missing; give the folder to write the PNGs into, such as textures/planks");
        TextureGraph graph;
        lock (store.Gate)
        {
            if (store.Graph is not { } open) return Error("export: " + NoGraph);
            graph = open;
        }
        // The graph is a value, so it is evaluated outside the store's lock and the other tools answer while an export
        // runs (minutes at 8192). Exports take turns, as two could write the same files. A cancelled call
        // (notifications/cancelled) stops between tiles, writing nothing more, and gives up its turn.
        Exports.Wait(cancellationToken);
        try
        {
            var faults = GraphValidator.Validate(graph, registry);
            if (faults.Count > 0)
                return Error($"export: refused: the graph has {Faults(faults)}\nAn export needs a graph that evaluates; fix them with edit_graph, then export again. Nothing was written.");
            if (!ParameterOverrides.TryApply("export", graph, parameters, registry, out graph, out var overrides, out var overrideError))
                return Error(overrideError! + ". Nothing was written.");

            var pixels = graph.Size;
            if (size is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } given)
            {
                if (!(given.ValueKind == JsonValueKind.Number && given.TryGetInt32(out pixels) && TextureGraph.IsValidSize(pixels)))
                    return Error($"export: size {given.GetRawText()} is not allowed; size is {TextureGraph.SizesText}");
            }

            var evaluator = new GraphEvaluator(graph, registry);
            var metallic = OutputRoles.Name(OutputRole.Metallic);
            // The outputs with a file of their own, in sheet order: metallic goes into roughness.png.
            IReadOnlyList<string> roles = [.. evaluator.Outputs.Where(OutputRoles.HasOwnFile)];
            if (outputs is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } list)
            {
                var known = $"the roles are {string.Join(", ", OutputRoles.Names)}, and a named output is given by its name";
                if (list.ValueKind != JsonValueKind.Array) return Error($"export: outputs is an array of roles and names, not {Kind(list)}; {known}");
                var chosen = new List<string>();
                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || !GraphNames.IsValid(item.GetString()!))
                        return Error($"export: outputs holds {item.GetRawText()}, which is not a role or a name; {known}");
                    var output = item.GetString()!;
                    if (!evaluator.Has(output))
                        return Error($"export: the graph has no {output} output; it has {string.Join(", ", evaluator.Outputs)}");
                    if (!chosen.Contains(output)) chosen.Add(output);
                }
                // metallic has no file of its own; named beside roughness it is written in roughness.png, named alone it is refused (RV-76).
                if (chosen.Any(o => !OutputRoles.HasOwnFile(o)))
                {
                    if (!chosen.Contains(OutputRoles.Name(OutputRole.Roughness)))
                        return Error("export: metallic has no file of its own: it is packed into roughness.png's blue channel, so name roughness to write it");
                    chosen.RemoveAll(o => !OutputRoles.HasOwnFile(o));
                }
                if (chosen.Count == 0) return Error("export: outputs is empty; name at least one output, or leave it out for every output");
                roles = [.. roles.Where(chosen.Contains)];
            }

            string target;
            try
            {
                target = toolFolder.Full(folderText);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Error($"export: could not write to folder {JsonSerializer.Serialize(folderText)}: {e.Message}");
            }
            try
            {
                Directory.CreateDirectory(target);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return Error($"export: could not write to folder {target}: {e.Message}");
            }

            var lines = new List<string>();
            // One output at a time, in one RGBA8 buffer reused for each (256 MB at 8192) and encoded straight into its
            // file, so no output's PNG is ever held whole; the memory is given back before the next.
            var rgba = new byte[(long)pixels * pixels * 4];
            foreach (var role in roles)
            {
                var path = Path.Combine(target, OutputRoles.FileOf(role));
                var packedAs = OutputRoles.PackedAs(role);
                var withMetallic = role == OutputRoles.Name(OutputRole.Roughness) && evaluator.Has(metallic);
                var replaced = File.Exists(path);
                var temporary = path + ".writing";
                long length;
                try
                {
                    evaluator.Evaluate(role, pixels, tile =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        OutputPacking.PackTile(packedAs, tile, rgba, pixels);
                    });
                    if (withMetallic)
                    {
                        // The same between roughness's evaluation and metallic's, so their two peaks never stack (RV-76).
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                        evaluator.Evaluate(metallic, pixels, tile =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            OutputPacking.PackMetallicTile(tile, rgba, pixels);
                        });
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    // The evaluation's cache and every tile it dropped are garbage once it returns: given back before
                    // the encoder allocates, so the two never peak together (T-155: the strong minify at 8192 pins its
                    // whole 768 MiB input, and held it through the encode).
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                    using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        PngWriter.Write(stream, rgba, pixels, pixels);
                        length = stream.Length;
                    }
                    File.Move(temporary, path, overwrite: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    try { File.Delete(temporary); } catch (IOException) { }
                    return Error($"export: could not write {path} in folder {target}: {e.Message}" + (lines.Count > 0 ? $"\nAlready written: {string.Join(", ", lines.Select(l => l.Split(':')[0][2..]))}" : ""));
                }
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                var slot = OutputRoles.TryParse(role, out var r)
                    ? Slot(r) is { } named ? $"slot {named}" + (withMetallic ? ", metallic in B" : "") : "no Carve slot (Carve does not use height yet)"
                    : "no Carve slot (a named output)";
                lines.Add($"- {OutputRoles.FileOf(role)}: {pixels}x{pixels}, {length.ToString(CultureInfo.InvariantCulture)} bytes{(replaced ? " (replaced)" : "")}, {slot}");
            }
            return Line($"export: wrote {Count(lines.Count, "file")} to {target} at size {pixels}{(overrides.Count == 0 ? "" : ", parameters " + GraphParameters.Describe(overrides))}\n" + string.Join("\n", lines));
        }
        finally
        {
            Exports.Release();
        }
    }

    private static readonly SemaphoreSlim Exports = new(1, 1);
}
