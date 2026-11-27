using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Carve.Debug;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// T-142's stage, the node library on the real patina: each M-28 sample opened (old-wall's nested files named), checked clean,
/// exported at 256 and compared byte for byte with <c>samples/graphs/&lt;name&gt;/*.png</c>, and drawn as a sheet at tiling 3
/// plus its sphere at angles 0 and 90, every pixel worked from the exported texels (flat and tiled cells exact, the sphere by
/// <see cref="LibrarySphere"/>'s geometry and tones). Bricks run twice, the second by the grey <c>parameters</c> override.
/// Then the planted faults (chrome in paint, floor tiles turned 30 degrees through <c>edit_graph</c>) and the breaks, made here
/// (<c>library-breaks.txt</c>), never in patina. Writes <c>library-checks.tsv</c> (what each sphere measured).
/// </summary>
internal sealed class LibraryStage : IPatinaStage
{
    private const string GreyOverrides = """{"mortar":0.08,"tint":"#7a7a70"}""";
    private const int Tiling = 3;
    private const int SheetTop = 24;

    private sealed record Sample(string Name, string Graph, string Committed, string? Overrides, int Part);

    private static readonly Sample[] Samples =
    [
        new("cobblestones", "cobblestones.texgraph", "cobblestones", null, 1),
        new("bricks", "bricks.texgraph", "bricks", null, 1),
        new("bricks-grey", "bricks.texgraph", "bricks/variants/grey", GreyOverrides, 1),
        new("fabric", "fabric.texgraph", "fabric", null, 1),
        new("marble", "marble.texgraph", "marble", null, 1),
        new("painted-metal", "painted-metal.texgraph", "painted-metal", null, 2),
        new("floor-tiles", "floor-tiles.texgraph", "floor-tiles", null, 2),
        new("old-wall", "old-wall.texgraph", "old-wall", null, 2),
        new("grime", "grime.texgraph", "grime", null, 2),
    ];

    private enum Kind { Plain, RoughnessOnly, MetallicOnly }

    private sealed record Column(string Label, string File, Kind Kind);

    private sealed record Done(Sample Sample, IReadOnlyList<Column> Columns, Dictionary<string, byte[]> Files, ScenarioFrame Sheet, ScenarioFrame Sphere0, ScenarioFrame Sphere90);

    private static readonly string[] RoleOrder = ["base_colour", "normal", "roughness", "metallic", "occlusion", "height"];

    private readonly StringBuilder _measured = new("sample\tsphere_pixels\tr_with_metallic\tr_without_metallic\tr_mirrored\tsilhouette_wrong\tslate_inside\tinside\n");

    /// <param name="part">0 runs every sample, the planted faults and every break; 1 or 2 only that half of the samples (T-172: the
    /// stage's own time, not the server's, is what filled the 300 s guard), part 2 with the planted faults.</param>
    public LibraryStage(int part = 0) => _part = part;

    private readonly int _part;

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var samples = Path.Combine(SwatchStage.RepoRoot(), "samples", "graphs");
        var done = new Dictionary<string, Done>();
        foreach (var sample in Samples.Where(s => _part == 0 || s.Part == _part)) done[sample.Name] = await SampleAsync(driver, checks, samples, sample);
        File.WriteAllText(Path.Combine(driver.RunFolder, "library-checks.tsv"), _measured.ToString());

        if (_part != 1) await FaultsAsync(driver, checks, samples);
        Breaks(driver, checks, done);
    }

    private static string Args(Sample sample, params (string Key, object Value)[] more)
    {
        var o = new JsonObject();
        foreach (var (key, value) in more) o[key] = JsonValue.Create(value);
        if (sample.Overrides is not null) o["parameters"] = JsonNode.Parse(sample.Overrides);
        return o.ToJsonString();
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text[text.IndexOf('{')..]).RootElement.Clone();

    /// <summary>The columns of a sheet, from the graph's output nodes: roles in role order, then named outputs a to z.</summary>
    private static List<Column> ColumnsOf(string graphPath)
    {
        var roles = new HashSet<string>();
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var node in JsonDocument.Parse(File.ReadAllText(graphPath)).RootElement.GetProperty("nodes").EnumerateObject())
        {
            if (node.Value.GetProperty("type").GetString() != "output") continue;
            var p = node.Value.TryGetProperty("params", out var ps) ? ps : default;
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var name)) names.Add(name.GetString()!);
            else roles.Add(p.ValueKind == JsonValueKind.Object && p.TryGetProperty("role", out var role) ? role.GetString()! : "base_colour");
        }
        var columns = new List<Column>();
        foreach (var role in RoleOrder.Where(roles.Contains))
            columns.Add(role switch
            {
                "roughness" => new Column(role, "roughness.png", Kind.RoughnessOnly),
                "metallic" => new Column(role, "roughness.png", Kind.MetallicOnly),
                _ => new Column(role, role + ".png", Kind.Plain),
            });
        columns.AddRange(names.Select(n => new Column(n, n + ".png", Kind.Plain)));
        return columns;
    }

    /// <summary>One column's cell as 256 x 256 texels: the file, roughness alone (B 0), or metallic alone (the file's B, spread grey).</summary>
    private static byte[] Cell(Column column, Dictionary<string, byte[]> files, bool mirror = false)
    {
        var file = files[column.File];
        var cell = new byte[file.Length];
        for (var y = 0; y < 256; y++)
        {
            for (var x = 0; x < 256; x++)
            {
                var from = (y * 256 + (mirror ? 255 - x : x)) * 4;
                var to = (y * 256 + x) * 4;
                switch (column.Kind)
                {
                    case Kind.Plain: Buffer.BlockCopy(file, from, cell, to, 4); break;
                    case Kind.RoughnessOnly: Buffer.BlockCopy(file, from, cell, to, 4); cell[to + 2] = 0; break;
                    default: cell[to] = cell[to + 1] = cell[to + 2] = file[from + 2]; cell[to + 3] = 255; break;
                }
            }
        }
        return cell;
    }

    private async Task<Done> SampleAsync(ScenarioDriver driver, PatinaChecks checks, string samples, Sample s)
    {
        var graphPath = Path.Combine(samples, s.Graph);
        var opened = await checks.CallAsync("open", JsonSerializer.Serialize(new { path = graphPath }));
        checks.Check(opened.Contains("no faults: it evaluates", StringComparison.Ordinal), $"library: {s.Name} opened with faults: {opened}");
        if (s.Name == "old-wall")
            checks.Check(opened.Contains("read 2 nested files for its graph and image nodes", StringComparison.Ordinal) && opened.Contains("grime.texgraph", StringComparison.Ordinal) && opened.Contains("bricks.texgraph", StringComparison.Ordinal),
                "library: open did not name old-wall's nested files: " + opened);

        // check: clean.
        var verdict = Json(await checks.CallAsync("check", Args(s)));
        checks.Check(verdict.GetProperty("ok").GetBoolean() && verdict.GetProperty("problems").GetArrayLength() == 0, $"library: {s.Name} check is not clean: {verdict}");

        // export: byte for byte with the committed PNGs, and no file more or fewer.
        var folder = Path.Combine(driver.RunFolder, $"library-{s.Name}-texels");
        await checks.CallAsync("export", Args(s, ("folder", folder), ("size", 256)));
        var committed = Path.Combine(samples, s.Committed.Replace('/', Path.DirectorySeparatorChar));
        var kept = Directory.GetFiles(committed, "*.png").Select(f => Path.GetFileName(f)).Where(f => f != "swatch.png").Order().ToArray();
        var made = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").Select(f => Path.GetFileName(f)).Order().ToArray() : [];
        checks.Check(kept.SequenceEqual(made), $"library: {s.Name} exported [{string.Join(", ", made)}] but {committed} holds [{string.Join(", ", kept)}]");
        var files = new Dictionary<string, byte[]>();
        foreach (var file in kept)
        {
            var mine = Path.Combine(folder, file);
            checks.Check(File.Exists(mine) && File.ReadAllBytes(mine).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(committed, file))), $"library: {s.Name}'s exported {file} is not the committed file byte for byte");
            files[file] = Decode(Path.Combine(committed, file));
        }
        var columns = ColumnsOf(graphPath);
        foreach (var c in columns.Where(c => !files.ContainsKey(c.File))) checks.Check(false, $"library: {s.Name} has an output {c.Label} but no {c.File}");
        columns = [.. columns.Where(c => files.ContainsKey(c.File))];

        // The sheet at tiling 3, and the sphere at 0 and 90 (also at tiling 3).
        var sheet = await driver.CaptureToolAsync("swatch", $"library-{s.Name}-sheet", JsonNode.Parse(Args(s, ("size", 256), ("tiling", Tiling))));
        var sphere0 = await driver.CaptureToolAsync("swatch", $"library-{s.Name}-sphere-0", JsonNode.Parse(Args(s, ("view", "sphere"), ("angle", 0), ("tiling", Tiling))));
        var sphere90 = await driver.CaptureToolAsync("swatch", $"library-{s.Name}-sphere-90", JsonNode.Parse(Args(s, ("view", "sphere"), ("angle", 90), ("tiling", Tiling))));
        var done = new Done(s, columns, files, sheet, sphere0, sphere90);
        CheckSheet(checks, done, mirror: false, shift: 0, sheet);
        CheckSphereView(checks, done, sphere0, 0);
        CheckSphereView(checks, done, sphere90, 90);

        var with = Tones(done, mirror: false, metal: true);
        var (r, n) = LibrarySphere.Correlation(with[0], with[1], sphere0, sphere90);
        var without = Tones(done, false, false);
        var rWithout = LibrarySphere.Correlation(without[0], without[1], sphere0, sphere90).R;
        var mirrored = Tones(done, true, true);
        var rMirrored = LibrarySphere.Correlation(mirrored[0], mirrored[1], sphere0, sphere90).R;
        var (outside, slateInside, inside) = LibrarySphere.Silhouette(sphere0, 0);
        _measured.Append(CultureInfo.InvariantCulture, $"{s.Name}\t{n}\t{r:0.000}\t{rWithout:0.000}\t{rMirrored:0.000}\t{outside}\t{slateInside}\t{inside}\n");
        var least = columns.Any(c => c.Kind == Kind.MetallicOnly) ? LibrarySphere.MetalMin : LibrarySphere.CorrelationMin;
        checks.Check(n > 20000 && r >= least, $"library: {s.Name} sphere: tone difference correlates {r:0.000} with the frames' luma difference over {n} pixels, the rule at least {least}");
        return done;
    }

    private static byte[] Decode(string path) =>
        ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha) is { Width: 256, Height: 256 } image ? image.Data
            : throw new InvalidOperationException($"library: {path} is not 256 x 256");

    private static double[][] Tones(Done d, bool mirror, bool metal)
    {
        var baseColour = d.Files["base_colour.png"];
        var roughness = d.Columns.Any(c => c.Kind == Kind.MetallicOnly) ? d.Files["roughness.png"] : null;
        if (mirror)
        {
            baseColour = Mirror(baseColour);
            if (roughness is not null) roughness = Mirror(roughness);
        }
        var drop = metal ? LibrarySphere.MetalDrop : 0;
        return [LibrarySphere.Tones(baseColour, roughness, 0, Tiling, drop), LibrarySphere.Tones(baseColour, roughness, 90, Tiling, drop)];
    }

    private static byte[] Mirror(byte[] image)
    {
        var mirrored = new byte[image.Length];
        for (var y = 0; y < 256; y++)
            for (var x = 0; x < 256; x++)
                Buffer.BlockCopy(image, (y * 256 + 255 - x) * 4, mirrored, (y * 256 + x) * 4, 4);
        return mirrored;
    }

    private static void CheckSphereView(PatinaChecks checks, Done d, ScenarioFrame frame, float angle)
    {
        if (frame.Width != SwatchSphere.Size || frame.Height != SwatchSphere.Size) { checks.Check(false, $"library: {d.Sample.Name} sphere {angle} is {frame.Width} x {frame.Height}"); return; }
        var (outside, slateInside, inside) = LibrarySphere.Silhouette(frame, angle);
        checks.Check(outside == 0 && slateInside <= inside * SwatchStage.SlateInside,
            $"library: {d.Sample.Name} sphere {angle}: {outside} pixel(s) outside the silhouette are not the slate, {slateInside} of {inside} inside are");
    }

    /// <summary>
    /// The sheet's cells against the exported texels: a column per output, each flat cell 1:1 and each tiled cell 3 x 3 scaled into
    /// 256 (moved <paramref name="shift"/> texels on, for the shift break), the label bands left out, the sphere cell against the
    /// sphere view at angle 0.
    /// </summary>
    private static void CheckSheet(PatinaChecks checks, Done d, bool mirror, int shift, ScenarioFrame sheet)
    {
        var name = d.Sample.Name;
        var width = d.Columns.Count * 258 + 512;
        if (sheet.Width != width || sheet.Height != 538) { checks.Check(false, $"library: the {name} sheet is {sheet.Width} x {sheet.Height}, not {width} x 538"); return; }
        for (var column = 0; column < d.Columns.Count; column++)
        {
            var c = d.Columns[column];
            var cell = Cell(c, d.Files, mirror);
            var left = column * 258;
            SwatchStage.CheckRegion(checks, $"{name} sheet {c.Label} flat", sheet, left, SheetTop, 256, 256, SwatchStage.LabelBand, (x, y) => cell.AsSpan((y * 256 + x) * 4, 4));
            SwatchStage.CheckRegion(checks, $"{name} sheet {c.Label} tiled", sheet, left, 282, 256, 256, SwatchStage.LabelBand,
                (x, y) => cell.AsSpan((((SwatchStage.Under(y, 256, 768) + shift) % 256) * 256 + (SwatchStage.Under(x, 256, 768) + shift) % 256) * 4, 4));
            checks.Check(SwatchStage.Bright(sheet, left, SheetTop, 256, SwatchStage.LabelBand) && SwatchStage.Bright(sheet, left, 282, 256, SwatchStage.LabelBand), $"library: {name} sheet column {c.Label} has no label text");
        }
        checks.Check(SwatchStage.Bright(sheet, 0, 0, 512, SheetTop), $"library: the {name} sheet has no title text");
        var slate = 0;
        for (var column = 0; column < d.Columns.Count; column++)
            for (var y = SheetTop; y < 538; y++)
                for (var x = column * 258 + 256; x < column * 258 + 258; x++) slate += SwatchStage.IsSlate(sheet, x, y) ? 0 : 1;
        checks.Check(slate == 0, $"library: the {name} sheet has {slate} gutter pixel(s) that are not the slate");
        SwatchStage.CheckRegion(checks, $"{name} sheet sphere cell against the sphere view", sheet, d.Columns.Count * 258, 25, 512, 512, SwatchStage.LabelBand,
            (x, y) => d.Sphere0.Pixels.AsSpan((y * 512 + x) * 4, 4));
    }

    /// <summary>
    /// The sheet check for a graph outside the library's samples (RV-76: the nested stage's old-wall and grime at their defaults):
    /// the columns from the graph file, each flat and tiled cell against the texels in <paramref name="texels"/>, the title, the
    /// gutters, and the sphere cell against <paramref name="sphere"/>.
    /// </summary>
    internal static void CheckGraphSheet(PatinaChecks checks, string name, string graphPath, string texels, ScenarioFrame sheet, ScenarioFrame sphere)
    {
        var files = Directory.GetFiles(texels, "*.png").ToDictionary(f => Path.GetFileName(f), Decode);
        var columns = ColumnsOf(graphPath).Where(c => files.ContainsKey(c.File)).ToList();
        checks.Check(columns.Count > 0, $"library: {name} has no output with a file in {texels}");
        var sample = new Sample(name, Path.GetFileName(graphPath), texels, null, 0);
        CheckSheet(checks, new Done(sample, columns, files, sheet, sphere, sphere), mirror: false, shift: 0, sheet);
    }

    /// <summary>chrome-in-paint must report metallic-not-binary; the floor tiles turned 30 degrees through edit_graph must report does-not-tile.</summary>
    private static async Task FaultsAsync(ScenarioDriver driver, PatinaChecks checks, string samples)
    {
        await checks.CallAsync("open", JsonSerializer.Serialize(new { path = Path.Combine(samples, "faults", "chrome-in-paint.texgraph") }));
        var chrome = Json(await checks.CallAsync("check", "{}"));
        checks.Check(!chrome.GetProperty("ok").GetBoolean() && chrome.GetProperty("problems").EnumerateArray().Any(p => p.GetProperty("check").GetString() == "metallic-not-binary"),
            "library: chrome-in-paint did not report metallic-not-binary: " + chrome);

        await checks.CallAsync("open", JsonSerializer.Serialize(new { path = Path.Combine(samples, "floor-tiles.texgraph") }));
        await checks.CallAsync("edit_graph", """{"operations":[{"op":"set_node","node":"base_turned","type":"transform","inputs":{"in":"base"},"params":{"rotation":30}},{"op":"connect","node":"base_out","port":"in","from":"base_turned"}]}""");
        var turned = Json(await checks.CallAsync("check", "{}"));
        checks.Check(!turned.GetProperty("ok").GetBoolean() && turned.GetProperty("problems").EnumerateArray().Any(p => p.GetProperty("check").GetString() == "does-not-tile" && p.GetProperty("output").GetString() == "base_colour"),
            "library: the floor tiles turned 30 degrees did not report does-not-tile on base_colour: " + turned);
        var folder = Path.Combine(driver.RunFolder, "library-floor-tiles-turned-texels");
        await checks.CallAsync("export", JsonSerializer.Serialize(new { folder, size = 256, outputs = new[] { "base_colour" } }));
        var texels = Decode(Path.Combine(folder, "base_colour.png"));
        var tiled = await driver.CaptureToolAsync("swatch", "library-floor-tiles-turned-tiled", new { view = "tiled", output = "base_colour", size = 256 });
        if (tiled.Width != 768 || tiled.Height != 768) checks.Check(false, $"library: the turned floor tiles' tiled view is {tiled.Width} x {tiled.Height}, not 768 x 768");
        else SwatchStage.CheckRegion(checks, "turned floor tiles tiled base_colour", tiled, 0, 0, 768, 768, 0, (x, y) => texels.AsSpan(((y % 256) * 256 + x % 256) * 4, 4));
    }

    /// <summary>The breaks, made here: wrong predictions against frames the product drew. Each must fail the check named for it. Writes library-breaks.txt.</summary>
    private static void Breaks(ScenarioDriver driver, PatinaChecks checks, Dictionary<string, Done> done)
    {
        var log = new StringBuilder();
        void Caught(string name, Action<PatinaChecks> run, params string[] by)
        {
            var probe = new PatinaChecks(driver);
            run(probe);
            log.AppendLine("== " + name);
            foreach (var failure in probe.Failures.Take(12)) log.AppendLine("  " + failure);
            foreach (var pattern in by)
                checks.Check(probe.Failures.Any(f => Regex.IsMatch(f, pattern)), $"library: the break {name} was not caught by /{pattern}/");
        }
        foreach (var d in done.Values)
        {
            var n = Regex.Escape(d.Sample.Name);
            Caught($"u-mirror, {d.Sample.Name} sheet", p => CheckSheet(p, d, mirror: true, shift: 0, d.Sheet), $"{n} sheet base_colour flat", $"{n} sheet base_colour tiled");
            Caught($"half-tile shift, {d.Sample.Name} sheet", p => CheckSheet(p, d, mirror: false, shift: 128, d.Sheet), $"{n} sheet base_colour tiled");
            var mirrored = Tones(d, true, true);
            Caught($"u-mirror, {d.Sample.Name} sphere", p =>
            {
                var (r, count) = LibrarySphere.Correlation(mirrored[0], mirrored[1], d.Sphere0, d.Sphere90);
                p.Check(r >= LibrarySphere.CorrelationMin, $"library: {d.Sample.Name} sphere: tone difference correlates {r:0.000} over {count} pixels, the rule at least {LibrarySphere.CorrelationMin}");
            }, $"{n} sphere: tone difference correlates");
        }
        // The grey variant drawn from the plain bricks' texels.
        if (done.TryGetValue("bricks", out var plain) && done.TryGetValue("bricks-grey", out var grey))
        {
            Caught("bricks variant from the base graph's texels", p => CheckSheet(p, new Done(grey.Sample, grey.Columns, plain.Files, grey.Sheet, grey.Sphere0, grey.Sphere90), mirror: false, shift: 0, grey.Sheet),
                "bricks-grey sheet base_colour flat", "bricks-grey sheet base_colour tiled");
        }
        // The painted metal's sphere predicted without its metallic.
        if (done.TryGetValue("painted-metal", out var metal))
        {
            var without = Tones(metal, false, false);
            Caught("painted-metal sphere predicted without metallic", p =>
            {
                var (r, count) = LibrarySphere.Correlation(without[0], without[1], metal.Sphere0, metal.Sphere90);
                p.Check(r >= LibrarySphere.MetalMin, $"library: painted-metal sphere: predicted without metallic correlates {r:0.000} over {count} pixels, the rule at least {LibrarySphere.MetalMin}");
            }, "painted-metal sphere: predicted without metallic");
        }
        File.WriteAllText(Path.Combine(driver.RunFolder, "library-breaks.txt"), log.ToString());
    }
}
