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

    /// <summary>The Carve material slot each role's file belongs in, by the define_material argument that takes it.</summary>
    public static string Slot(OutputRole role) => role switch
    {
        OutputRole.BaseColour => "base_colour_texture",
        OutputRole.Normal => "normal_texture",
        OutputRole.Roughness => "metallic_roughness_texture",
        _ => "occlusion_texture",
    };

    public const string Text =
        "Writes the open graph's outputs as PNG files, ready for a Carve material's texture slots. Takes folder (required; absolute or relative to patina's working directory; created if missing), "
        + "size (a power of two from 32 to 8192; default the graph's own size) and outputs (a list of roles from base_colour, normal, roughness, occlusion; default every output the graph has). "
        + "Each file is <folder>/<role>.png, overwriting a file of that name (the result says replaced), written as the graph's outputs are packed for glTF: base_colour is sRGB; normal is tangent space, green up; "
        + "roughness is a glTF metallic-roughness image (R 255, roughness in G, metallic 0 in B), so define_material's metallic_roughness_texture takes it as it is; occlusion is grey. "
        + "The result lists each file with its size, its bytes and the Carve material slot it belongs in: base_colour in base_colour_texture, normal in normal_texture, roughness in metallic_roughness_texture, occlusion in occlusion_texture. "
        + "Exporting the graph at its own size gives the same bytes every time, and equals the PNGs beside samples/graphs/planks.texgraph. "
        + "Outputs are evaluated and written one at a time, in bands, so memory stays bounded at 8192 (under 2 GB for the process), but 8192 is slow, minutes and not seconds: the planks' four outputs took about 2.5 minutes here (a debug build), of which about 10 s an output is the PNG encode, and each PNG is some 3 MB. A client with a short call timeout should ask for one or two outputs at a time. "
        + "To look at a graph, swatch is the way (it renders at 2048 or below); export is for files. "
        + "Refused, writing nothing, while the graph has a fault (the error lists them; fix them with edit_graph); no open graph; a size that is not a power of two from 32 to 8192; a role the graph has no output for; "
        + "and a folder that cannot be written (the error names the folder and the system's reason).\n"
        + "Worked example, after open of samples/graphs/planks.texgraph (<folder> is the absolute path of textures/planks under patina's working directory):\n"
        + "export " + Worked + "\nreturns, a line for each file:\n"
        + WorkedHead + "\n- base_colour.png: 1024x1024, <bytes> bytes, slot base_colour_texture\n- normal.png: 1024x1024, <bytes> bytes, slot normal_texture";

    public static McpServerTool Create() => Create<ExportTool>("export", Text, new JsonObject
    {
        ["folder"] = new JsonObject { ["type"] = "string", ["description"] = "The folder to write <role>.png files into, absolute or relative to patina's working directory; created if missing." },
        ["size"] = new JsonObject { ["type"] = "integer", ["description"] = "A power of two from 32 to 8192; default the graph's own size." },
        ["outputs"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. OutputRoles.Names.Select(n => (JsonNode)n)]) },
            ["description"] = "The roles to write; default every output the graph has.",
        },
    }, "folder");

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? folder = null, JsonElement? size = null, JsonElement? outputs = null, CancellationToken cancellationToken = default)
    {
        if (Unknown("export", context, "folder", "size", "outputs") is { } unknown) return unknown;
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
        // (notifications/cancelled) stops between bands, writing nothing more, and gives up its turn.
        Exports.Wait(cancellationToken);
        try
        {
            var faults = GraphValidator.Validate(graph, registry);
            if (faults.Count > 0)
                return Error($"export: refused: the graph has {Faults(faults)}\nAn export needs a graph that evaluates; fix them with edit_graph, then export again. Nothing was written.");

            var pixels = graph.Size;
            if (size is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } given)
            {
                if (!(given.ValueKind == JsonValueKind.Number && given.TryGetInt32(out pixels) && TextureGraph.IsValidSize(pixels)))
                    return Error($"export: size {given.GetRawText()} is not allowed; size is {TextureGraph.SizesText}");
            }

            var evaluator = new GraphEvaluator(graph, registry);
            var roles = evaluator.Roles;
            if (outputs is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } list)
            {
                if (list.ValueKind != JsonValueKind.Array) return Error($"export: outputs is an array of roles, not {Kind(list)}; the roles are {string.Join(", ", OutputRoles.Names)}");
                var chosen = new List<OutputRole>();
                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || !OutputRoles.TryParse(item.GetString()!, out var role))
                        return Error($"export: outputs holds {item.GetRawText()}, which is not a role; the roles are {string.Join(", ", OutputRoles.Names)}");
                    if (!evaluator.Roles.Contains(role))
                        return Error($"export: the graph has no {OutputRoles.Name(role)} output; it has {string.Join(", ", evaluator.Roles.Select(OutputRoles.Name))}");
                    if (!chosen.Contains(role)) chosen.Add(role);
                }
                if (chosen.Count == 0) return Error("export: outputs is empty; name at least one role, or leave it out for every output");
                roles = [.. OutputRoles.All.Where(chosen.Contains)];
            }

            string target;
            try
            {
                target = Path.GetFullPath(folderText, toolFolder.Directory ?? Environment.CurrentDirectory);
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
                var path = Path.Combine(target, OutputRoles.Name(role) + ".png");
                var replaced = File.Exists(path);
                var temporary = path + ".writing";
                long length;
                try
                {
                    evaluator.Evaluate(role, pixels, (band, y0, rows) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        for (var y = y0; y < y0 + rows; y++) OutputPacking.PackRow(role, band, y, rgba.AsSpan(y * pixels * 4, pixels * 4));
                    });
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        new StbImageWriteSharp.ImageWriter().WritePng(rgba, pixels, pixels, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
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
                lines.Add($"- {OutputRoles.Name(role)}.png: {pixels}x{pixels}, {length.ToString(CultureInfo.InvariantCulture)} bytes{(replaced ? " (replaced)" : "")}, slot {Slot(role)}");
            }
            return Line($"export: wrote {Count(lines.Count, "file")} to {target} at size {pixels}\n" + string.Join("\n", lines));
        }
        finally
        {
            Exports.Release();
        }
    }

    private static readonly SemaphoreSlim Exports = new(1, 1);
}
