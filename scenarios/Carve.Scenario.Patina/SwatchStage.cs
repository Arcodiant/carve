using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Carve.Debug;
using ModelContextProtocol.Protocol;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// swatch (T-114): the planks built through the tools, every view drawn, each checked from the evaluated texels and the
/// sphere's geometry; then a graph with a seam, whose tiled view must fail the seam check. Writes <c>swatch-checks.tsv</c>
/// (what each sphere frame measured) into the run folder.
/// </summary>
/// <remarks>
/// The shared check rules (M-5, M-6), named before they are used:
/// <list type="bullet">
/// <item><b>Exact</b>: flat and tiled pixels equal the texels they show, byte for byte, the nearest texel under each pixel's
/// centre; a sheet cell's top <see cref="LabelBand"/> rows, where its label may sit, are left out.</item>
/// <item><b>Seamless</b>: across a tiled view's tile boundaries the mean step between neighbouring pixels is at most
/// <see cref="SeamRatio"/> times the mean step between neighbours inside a tile, plus one level.</item>
/// <item><b>Gap</b> and <b>stave</b> texels: base colour red below 90 (T-112's gap) and at or above 120.</item>
/// <item><b>Edge margin</b>: a sphere pixel is judged only when every ray within <see cref="SilhouetteMargin"/> px of its centre
/// meets the sphere, it faces the camera (cosine at least <see cref="Facing"/>), and every texel within the mip footprint
/// (the texels a pixel step spans, rounded up) plus one, in u and v, is of its one class.</item>
/// <item><b>Dark</b>: a gap pixel's luma (Rec. 709 of the sRGB bytes) is at most <see cref="DarkMax"/>.</item>
/// <item><b>In band</b>: a stave pixel's luma is within [<see cref="BandLow"/>, <see cref="BandHigh"/>].</item>
/// <item><b>Silhouette</b>: a pixel all of whose rays miss is the slate within one level; of those all of whose rays hit, no more
/// than <see cref="SlateInside"/> are (a dark pixel can match it by chance).</item>
/// <item><b>Stave order</b>: between two angles, at the same pixels (the same light, since the sphere is round), the stave
/// that reads lighter is the one whose texels are lighter, for every pair of staves whose tones differ by 3 % or more.</item>
/// </list>
/// The thresholds were probed on the planks' own colours (T-114's As built).
/// </remarks>
internal sealed class SwatchStage : IPatinaStage
{
    public const int LabelBand = 22;
    public const double SeamRatio = 3;
    public const float SilhouetteMargin = 1.5f;
    public const float Facing = 0.5f;
    public const double SlateInside = 0.001;
    public const double DarkMax = 52;
    public const double BandLow = 58;
    public const double BandHigh = 110;
    public const double ToneStep = 0.03;
    /// <summary>Carve's slate, (0.03, 0.035, 0.04) linear, through half floats and sRGB, as a frame shows it.</summary>
    private static readonly byte[] Slate = [.. new[] { 0.03f, 0.035f, 0.04f }.Select(v =>
    {
        var c = (double)(Half)v;
        return (byte)Math.Round(255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055));
    }), (byte)255];

    private readonly StringBuilder _measured = new("frame\tgap_pixels\tgap_luma_max\tstave_pixels\tstave_luma_min\tstave_luma_max\tinside\toutside\toutside_not_slate\tinside_slate\n");

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        // The planks through the tools, saved so the sheet's title names them, and read back equal to the sample by content.
        var sample = File.ReadAllText(Path.Combine(RepoRoot(), "samples", "graphs", "planks.texgraph"));
        await checks.CallAsync("define_graph", JsonSerializer.Serialize(new { graph = JsonDocument.Parse(sample).RootElement }));
        await checks.CallAsync("save", JsonSerializer.Serialize(new { path = Path.Combine(driver.RunFolder, "planks") }));
        var built = await checks.GraphAsync();
        checks.Check(built.TrimEnd('\n') == sample.Replace("\r\n", "\n").TrimEnd('\n'), "swatch: the planks built through the tools are not samples/graphs/planks.texgraph by content");
        var graph = TexGraphFile.Read(built, NodeRegistry.BuiltIn).Graph!;
        var evaluator = new GraphEvaluator(graph, NodeRegistry.BuiltIn);
        var read = new Readback(evaluator);

        // Refusals.
        var big = await checks.CallAsync("swatch", """{"size":4096}""", refused: true);
        checks.Check(big.Contains("use export", StringComparison.Ordinal), "swatch: size 4096 said " + big);
        await checks.CallAsync("swatch", """{"size":100}""", refused: true);
        await checks.CallAsync("swatch", """{"view":"sheet","output":"normal"}""", refused: true);

        // The single views.
        var normal = await driver.CaptureToolAsync("swatch", "swatch-flat-normal-256", new { view = "flat", output = "normal", size = 256 });
        CheckExact(checks, "flat normal 256", normal, 256, 256, (x, y) => Texel(read, OutputRole.Normal, 256, x, y));
        var tiled = await driver.CaptureToolAsync("swatch", "swatch-tiled-base-128", new { view = "tiled", output = "base_colour", size = 128 });
        CheckExact(checks, "tiled base_colour 128", tiled, 384, 384, (x, y) => Texel(read, OutputRole.BaseColour, 128, x % 128, y % 128));
        CheckSeamless(checks, "tiled base_colour 128", tiled, 128, expectSeam: false);

        var spheres = new Dictionary<float, ScenarioFrame>();
        foreach (var angle in new[] { 0f, 90f, 200f })
        {
            var frame = await driver.CaptureToolAsync("swatch", $"swatch-sphere-{angle.ToString(CultureInfo.InvariantCulture)}", new { view = "sphere", angle });
            spheres[angle] = frame;
            CheckSphere(checks, frame, read, 256, angle);
        }
        CheckStaveOrder(checks, read, spheres, 0, 90);
        CheckStaveOrder(checks, read, spheres, 0, 200);
        CheckStaveOrder(checks, read, spheres, 90, 200);

        // The sheet at 256 and 64.
        var sheet = await driver.CaptureToolAsync("swatch", "swatch-sheet-256", new { });
        CheckSheet(checks, sheet, read, 256, spheres[0]);
        var small = await driver.CaptureToolAsync("swatch", "swatch-sheet-64", new { size = 64 });
        CheckSheet(checks, small, read, 64, null);

        File.WriteAllText(Path.Combine(driver.RunFolder, "swatch-checks.tsv"), _measured.ToString() + "\nangle_a\tangle_b\tstave pairs (a/b:pixels:measured difference:texel difference)\n" + Order);

        // A graph with a seam: noise plus a ramp that runs once across, so its right edge does not meet its left.
        await checks.CallAsync("define_graph", """
            {"graph":{"version":1,"size":128,"seed":3,"nodes":{
              "n":{"type":"noise","params":{"cells":[8,8]}},
              "ramp":{"type":"gradient","params":{"axis":"u","repeat":1}},
              "mixed":{"type":"blend","inputs":{"a":"n","b":"ramp"},"params":{"mode":"mix","opacity":0.7}},
              "out":{"type":"output","inputs":{"in":"mixed"},"params":{"role":"base_colour"}}}}}
            """);
        var seam = await driver.CaptureToolAsync("swatch", "swatch-tiled-seam", new { view = "tiled", output = "base_colour" });
        CheckSeamless(checks, "the seam graph", seam, 128, expectSeam: true);

        await BreaksAsync(driver, checks, sample, evaluator, read, spheres, sheet, small, tiled, seam);
    }

    /// <summary>
    /// The breaks (T-114's table), each a fault swatch could have, made here and never in patina: the scenario tampers with a
    /// frame it read back, or with the texels it predicts from, or gives the tools a wrong graph, then runs the same checks on a
    /// <see cref="PatinaChecks"/> of its own. Each break must fail every check named for it (patterns over the failures).
    /// Writes <c>swatch-breaks.txt</c>, what each break failed, into the run folder. No frame is captured.
    /// </summary>
    private async Task BreaksAsync(ScenarioDriver driver, PatinaChecks checks, string sample, GraphEvaluator evaluator, Readback read,
        Dictionary<float, ScenarioFrame> spheres, ScenarioFrame sheet, ScenarioFrame small, ScenarioFrame tiled, ScenarioFrame seam)
    {
        var log = new StringBuilder();
        void Caught(string name, Action<PatinaChecks> run, IEnumerable<string> by)
        {
            var probe = new PatinaChecks(driver);
            run(probe);
            log.AppendLine("== " + name);
            foreach (var failure in probe.Failures) log.AppendLine("  " + failure);
            foreach (var pattern in by)
                checks.Check(probe.Failures.Any(f => Regex.IsMatch(f, pattern)), $"swatch: the break {name} was not caught by /{pattern}/");
        }
        var count = OutputRoles.All.Count;
        var names = OutputRoles.All.Select(OutputRoles.Name).ToList();
        var sheets = new[] { (Frame: sheet, Size: 256), (Frame: small, Size: 64) };

        // role-order: the sheet's columns in reverse role order, labels and all: the cells swapped in the sheets read back.
        foreach (var (frame, size) in sheets)
        {
            var swapped = Tampered(frame, (x, y) => y < SheetTop || x >= count * 258 || x % 258 >= 256 ? (x, y) : ((count - 1 - x / 258) * 258 + x % 258, y));
            Caught($"role-order, sheet {size}", probe => CheckSheet(probe, swapped, read, size, null),
                names.SelectMany(r => new[] { $"sheet {size} {r} flat:", $"sheet {size} {r} tiled:" }));
        }

        // u-flip: the sphere's u the other way round: the texels predicted from mirrored, against the frames as drawn.
        var mirrored = new Readback(evaluator, mirrorU: true);
        Caught("u-flip", probe =>
        {
            CheckStaveOrder(probe, mirrored, spheres, 0, 200);
            CheckStaveOrder(probe, mirrored, spheres, 90, 200);
        }, ["stave order 0 against 200:", "stave order 90 against 200:"]);

        // tiled-shift: the repeat moved half a tile in x and y: the 1:1 tiled views rolled by half a tile, the sheets' tiled
        // cells (three tiles to 256 px) by the whole pixel nearest it, 43.
        Caught("tiled-shift", probe =>
        {
            CheckExact(probe, "tiled base_colour 128", Rolled(tiled, 64), 384, 384, (x, y) => Texel(read, OutputRole.BaseColour, 128, x % 128, y % 128));
            CheckSeamless(probe, "the seam graph", Rolled(seam, 64), 128, expectSeam: true);
            foreach (var (frame, size) in sheets)
            {
                var shifted = Tampered(frame, (x, y) => y < 282 || x >= count * 258 || x % 258 >= 256 ? (x, y)
                    : (x - x % 258 + (x % 258 + 43) % 256, 282 + (y - 282 + 43) % 256));
                CheckSheet(probe, shifted, read, size, null);
            }
        }, [@"^swatch: tiled base_colour 128:", "^swatch: the seam graph:", .. sheets.SelectMany(s => names.Select(r => $"sheet {s.Size} {r} tiled:"))]);

        // roughness-as-base: the roughness image where the sphere's base colour goes: a graph whose base colour output is the
        // planks' roughness, drawn through the tools and judged against the planks.
        var graph = JsonNode.Parse(sample)!;
        graph["nodes"]!["base_out"]!["inputs"]!["in"] = "roughness";
        await checks.CallAsync("define_graph", JsonSerializer.Serialize(new { graph }));
        var wrong = new Dictionary<float, ScenarioFrame>();
        foreach (var angle in spheres.Keys)
        {
            var result = await driver.CallAsync("swatch", new { view = "sphere", angle });
            var image = result.Content.OfType<ImageContentBlock>().FirstOrDefault();
            checks.Check(result.IsError is not true && image is not null, $"swatch: the roughness-as-base sphere at {angle} was not drawn");
            if (image is null) return;
            var png = image.DecodedData.ToArray();
            var decoded = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
            wrong[angle] = new ScenarioFrame($"roughness-as-base-{angle}", "", png, decoded.Width, decoded.Height, decoded.Data, null);
        }
        Caught("roughness-as-base", probe =>
        {
            foreach (var (angle, frame) in wrong) CheckSphere(probe, frame, read, 256, angle);
            CheckStaveOrder(probe, read, wrong, 0, 200);
        }, [.. spheres.Keys.SelectMany(a => new[] { $"sphere {a}: .* not dark", $"sphere {a}: .* out of band" }), "stave order 0 against 200:"]);

        File.WriteAllText(Path.Combine(driver.RunFolder, "swatch-breaks.txt"), log.ToString());
    }

    /// <summary>The sheet's cells start below the title strip.</summary>
    private const int SheetTop = 24;

    /// <summary>A copy of <paramref name="frame"/> whose pixel (x, y) is the frame's at <paramref name="from"/>(x, y).</summary>
    private static ScenarioFrame Tampered(ScenarioFrame frame, Func<int, int, (int X, int Y)> from)
    {
        var pixels = new byte[frame.Pixels.Length];
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var (sx, sy) = from(x, y);
                Buffer.BlockCopy(frame.Pixels, (sy * frame.Width + sx) * 4, pixels, (y * frame.Width + x) * 4, 4);
            }
        }
        return new ScenarioFrame(frame.Name, frame.Path, frame.Png, frame.Width, frame.Height, pixels, frame.Caption);
    }

    /// <summary>A copy of <paramref name="frame"/> rolled by <paramref name="shift"/> pixels in x and y, as the repeat moved.</summary>
    private static ScenarioFrame Rolled(ScenarioFrame frame, int shift) =>
        Tampered(frame, (x, y) => ((x + shift) % frame.Width, (y + shift) % frame.Height));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(SwatchStage).Assembly.Location)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Carve.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Carve.slnx not found above the scenario");
    }

    /// <summary>
    /// What the checks read back and predict from: the open graph's texels, evaluated here, and what each sphere angle is judged
    /// to show. A break tampers with it: <paramref name="mirrorU"/> mirrors every image left to right, as if the sphere's u ran
    /// the other way.
    /// </summary>
    private sealed class Readback(GraphEvaluator evaluator, bool mirrorU = false)
    {
        private readonly Dictionary<(OutputRole, int), byte[]> _images = [];

        public Dictionary<float, Judged[]> Judged { get; } = [];

        public byte[] Image(OutputRole role, int size)
        {
            if (!_images.TryGetValue((role, size), out var image))
            {
                image = OutputImage.Rgba8(evaluator, role, size);
                if (mirrorU)
                {
                    var mirrored = new byte[image.Length];
                    for (var y = 0; y < size; y++)
                        for (var x = 0; x < size; x++)
                            Buffer.BlockCopy(image, (y * size + size - 1 - x) * 4, mirrored, (y * size + x) * 4, 4);
                    image = mirrored;
                }
                _images[(role, size)] = image;
            }
            return image;
        }
    }

    private static byte[] Image(Readback read, OutputRole role, int size) => read.Image(role, size);

    private static ReadOnlySpan<byte> Texel(Readback read, OutputRole role, int size, int x, int y) =>
        Image(read, role, size).AsSpan((y * size + x) * 4, 4);

    /// <summary>The texel under the centre of destination pixel <paramref name="at"/> of <paramref name="to"/>, from <paramref name="from"/>.</summary>
    private static int Under(int at, int to, int from) => (int)Math.Floor((at + 0.5) * from / to);

    private static double Luma(ReadOnlySpan<byte> p) => 0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2];

    private static void CheckExact(PatinaChecks checks, string what, ScenarioFrame frame, int width, int height, Func<int, int, ReadOnlySpan<byte>> texel)
    {
        if (frame.Width != width || frame.Height != height)
        {
            checks.Check(false, $"swatch: {what} is {frame.Width} x {frame.Height}, not {width} x {height}");
            return;
        }
        CheckRegion(checks, what, frame, 0, 0, width, height, 0, texel);
    }

    /// <summary>Exact over a region of the frame, leaving out its top <paramref name="skipRows"/>; reports the first difference and the count.</summary>
    private static void CheckRegion(PatinaChecks checks, string what, ScenarioFrame frame, int left, int top, int width, int height, int skipRows,
        Func<int, int, ReadOnlySpan<byte>> texel)
    {
        var wrong = 0;
        string? first = null;
        for (var y = skipRows; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = ((top + y) * frame.Width + left + x) * 4;
                var want = texel(x, y);
                if (frame.Pixels[at] != want[0] || frame.Pixels[at + 1] != want[1] || frame.Pixels[at + 2] != want[2])
                {
                    wrong++;
                    first ??= $"({x}, {y}) is {frame.Pixels[at]},{frame.Pixels[at + 1]},{frame.Pixels[at + 2]}, not {want[0]},{want[1]},{want[2]}";
                }
            }
        }
        checks.Check(wrong == 0, $"swatch: {what}: {wrong} pixel(s) are not their texel; first {first}");
    }

    /// <summary>Seamless, or (for the seam graph) shown not to be.</summary>
    private static void CheckSeamless(PatinaChecks checks, string what, ScenarioFrame frame, int size, bool expectSeam)
    {
        double Step(int x0, int y0, int x1, int y1)
        {
            var a = (y0 * frame.Width + x0) * 4;
            var b = (y1 * frame.Width + x1) * 4;
            return (Math.Abs(frame.Pixels[a] - frame.Pixels[b]) + Math.Abs(frame.Pixels[a + 1] - frame.Pixels[b + 1]) + Math.Abs(frame.Pixels[a + 2] - frame.Pixels[b + 2])) / 3.0;
        }
        double inside = 0, across = 0;
        int insideCount = 0, acrossCount = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x + 1 < frame.Width; x++)
            {
                var step = Step(x, y, x + 1, y);
                if ((x + 1) % size == 0) { across += step; acrossCount++; }
                else { inside += step; insideCount++; }
            }
        }
        for (var y = 0; y + 1 < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var step = Step(x, y, x, y + 1);
                if ((y + 1) % size == 0) { across += step; acrossCount++; }
                else { inside += step; insideCount++; }
            }
        }
        var meanAcross = across / acrossCount;
        var meanInside = inside / insideCount;
        var seamless = meanAcross <= SeamRatio * meanInside + 1;
        checks.Check(seamless != expectSeam,
            $"swatch: {what}: the mean step across tile boundaries is {meanAcross:0.00} against {meanInside:0.00} inside, so the tiled check {(seamless ? "passed" : "failed")}{(expectSeam ? "; it must fail on a seam" : "")}");
    }

    private void CheckSheet(PatinaChecks checks, ScenarioFrame frame, Readback read, int size, ScenarioFrame? sphere)
    {
        var roles = OutputRoles.All;
        var width = roles.Count * 258 + 512;
        if (frame.Width != width || frame.Height != 538)
        {
            checks.Check(false, $"swatch: the sheet at {size} is {frame.Width} x {frame.Height}, not {width} x 538");
            return;
        }
        for (var column = 0; column < roles.Count; column++)
        {
            var role = roles[column];
            var left = column * 258;
            CheckRegion(checks, $"sheet {size} {OutputRoles.Name(role)} flat", frame, left, 24, 256, 256, LabelBand,
                (x, y) => Texel(read, role, size, Under(x, 256, size), Under(y, 256, size)));
            CheckRegion(checks, $"sheet {size} {OutputRoles.Name(role)} tiled", frame, left, 282, 256, 256, LabelBand,
                (x, y) => Texel(read, role, size, Under(x, 256, 3 * size) % size, Under(y, 256, 3 * size) % size));
            checks.Check(Bright(frame, left, 24, 256, LabelBand) && Bright(frame, left, 282, 256, LabelBand), $"swatch: sheet {size} column {column} has no label text");
        }
        checks.Check(Bright(frame, 0, 0, 512, 24), $"swatch: sheet {size} has no title text");
        // The slate: the title strip right of the title's room, and the gutters between cells.
        var slate = 0;
        for (var y = 0; y < 24; y++) for (var x = 520; x < width; x++) slate += IsSlate(frame, x, y) ? 0 : 1;
        for (var column = 0; column < roles.Count; column++) for (var y = 24; y < 538; y++) for (var x = column * 258 + 256; x < column * 258 + 258; x++) slate += IsSlate(frame, x, y) ? 0 : 1;
        checks.Check(slate == 0, $"swatch: sheet {size}: {slate} pixel(s) of the title strip or gutters are not the slate");
        if (sphere is not null)
        {
            CheckRegion(checks, $"sheet {size} sphere cell against the sphere view", frame, roles.Count * 258, 25, 512, 512, LabelBand,
                (x, y) => sphere.Pixels.AsSpan((y * 512 + x) * 4, 4));
        }
    }

    private static bool IsSlate(ScenarioFrame frame, int x, int y)
    {
        var at = (y * frame.Width + x) * 4;
        return Math.Abs(frame.Pixels[at] - Slate[0]) <= 1 && Math.Abs(frame.Pixels[at + 1] - Slate[1]) <= 1 && Math.Abs(frame.Pixels[at + 2] - Slate[2]) <= 1;
    }

    /// <summary>Whether a region holds label text: some pixel near white.</summary>
    private static bool Bright(ScenarioFrame frame, int left, int top, int width, int height)
    {
        for (var y = top; y < top + height; y++)
            for (var x = left; x < left + width; x++)
                if (Luma(frame.Pixels.AsSpan((y * frame.Width + x) * 4, 4)) > 230) return true;
        return false;
    }

    private enum Kind { None, Gap, Stave }

    /// <summary>Per pixel of a sphere frame: what it can be judged as, and the stave it shows.</summary>
    private sealed record Judged(Kind Kind, int Stave, SwatchSphere.Hit? Hit, bool Inside, bool Outside);

    private static readonly StringBuilder Order = new();

    private static Judged[] Judge(Readback read, int size, float angle)
    {
        if (read.Judged.TryGetValue(angle, out var judged)) return judged;
        var sphere = new SwatchSphere(angle);
        const int n = SwatchSphere.Size;
        var hits = new SwatchSphere.Hit?[n + 1, n + 1];
        for (var y = 0; y <= n; y++) for (var x = 0; x <= n; x++) hits[x, y] = sphere.Trace(x + 0.5f, y + 0.5f);
        var base_ = Image(read, OutputRole.BaseColour, size);
        Kind KindAt(int tx, int ty)
        {
            var red = base_[(ty * size + tx) * 4];
            return red < 90 ? Kind.Gap : red >= 120 ? Kind.Stave : Kind.None;
        }
        judged = new Judged[n * n];
        var m = SilhouetteMargin;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var cx = x + 0.5f;
                var cy = y + 0.5f;
                var ring = new[] { (cx - m, cy - m), (cx + m, cy - m), (cx - m, cy + m), (cx + m, cy + m), (cx - m, cy), (cx + m, cy), (cx, cy - m), (cx, cy + m) }
                    .Select(p => sphere.Trace(p.Item1, p.Item2) is not null).ToArray();
                var hit = hits[x, y];
                var inside = hit is not null && ring.All(h => h);
                var outside = hit is null && ring.All(h => !h);
                var kind = Kind.None;
                var stave = -1;
                if (inside && hit!.Facing >= Facing && hits[x + 1, y] is { } right && hits[x, y + 1] is { } below)
                {
                    static float Wrapped(float d) => MathF.Abs(d - MathF.Round(d));
                    var footprint = MathF.Max(MathF.Max(Wrapped(right.U - hit.U), Wrapped(below.U - hit.U)), MathF.Max(MathF.Abs(right.V - hit.V), MathF.Abs(below.V - hit.V))) * size;
                    var reach = (int)MathF.Ceiling(footprint) + 1;
                    var tx = (int)MathF.Floor(hit.U * size);
                    var ty = Math.Clamp((int)MathF.Floor(hit.V * size), 0, size - 1);
                    tx = ((tx % size) + size) % size;
                    kind = KindAt(tx, ty);
                    for (var dy = -reach; dy <= reach && kind != Kind.None; dy++)
                        for (var dx = -reach; dx <= reach && kind != Kind.None; dx++)
                            if (KindAt(((tx + dx) % size + size) % size, Math.Clamp(ty + dy, 0, size - 1)) != kind) kind = Kind.None;
                    stave = tx * 8 / size;
                }
                judged[y * n + x] = new Judged(kind, stave, hit, inside, outside);
            }
        }
        read.Judged[angle] = judged;
        return judged;
    }

    private void CheckSphere(PatinaChecks checks, ScenarioFrame frame, Readback read, int size, float angle)
    {
        const int n = SwatchSphere.Size;
        if (frame.Width != n || frame.Height != n)
        {
            checks.Check(false, $"swatch: the sphere at {angle} is {frame.Width} x {frame.Height}, not {n} x {n}");
            return;
        }
        var judged = Judge(read, size, angle);
        var faults = new List<string>();
        void Note(int x, int y, string where)
        {
            var at = (y * n + x) * 4;
            if (faults.Count < 4) faults.Add($"({x}, {y}) {where} is {frame.Pixels[at]},{frame.Pixels[at + 1]},{frame.Pixels[at + 2]}");
        }
        int gaps = 0, staves = 0, inside = 0, outside = 0, notDark = 0, outOfBand = 0, silhouette = 0, slateInside = 0;
        double gapMax = 0, staveMin = 255, staveMax = 0;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var j = judged[y * n + x];
                var luma = Luma(frame.Pixels.AsSpan((y * n + x) * 4, 4));
                if (j.Outside) { outside++; if (!IsSlate(frame, x, y)) { silhouette++; Note(x, y, "outside"); } }
                if (j.Inside) { inside++; if (IsSlate(frame, x, y)) { slateInside++; Note(x, y, "inside"); } }
                if (j.Kind == Kind.Gap) { gaps++; gapMax = Math.Max(gapMax, luma); if (luma > DarkMax) notDark++; }
                if (j.Kind == Kind.Stave) { staves++; staveMin = Math.Min(staveMin, luma); staveMax = Math.Max(staveMax, luma); if (luma < BandLow || luma > BandHigh) outOfBand++; }
            }
        }
        _measured.Append(CultureInfo.InvariantCulture, $"{frame.Name}\t{gaps}\t{gapMax:0.0}\t{staves}\t{staveMin:0.0}\t{staveMax:0.0}\t{inside}\t{outside}\t{silhouette}\t{slateInside}\n");
        checks.Check(gaps > 1000 && staves > 10000, $"swatch: sphere {angle}: too few pixels judged ({gaps} gap, {staves} stave)");
        checks.Check(notDark == 0, $"swatch: sphere {angle}: {notDark} of {gaps} gap pixels are not dark (luma up to {gapMax:0.0}, the rule {DarkMax})");
        checks.Check(outOfBand == 0, $"swatch: sphere {angle}: {outOfBand} of {staves} stave pixels are out of band (luma {staveMin:0.0} to {staveMax:0.0}, the band {BandLow} to {BandHigh})");
        // A dark pixel of the far-lit lower limb can read as the slate by chance, so inside allows a few in a thousand.
        silhouette += slateInside > inside * SlateInside ? slateInside : 0;
        checks.Check(silhouette == 0, $"swatch: sphere {angle}: {silhouette} pixel(s) are not where the camera puts the silhouette: {string.Join("; ", faults)}");
    }

    /// <summary>
    /// The stave order rule between angles <paramref name="a"/> and <paramref name="b"/>: at pixels judged stave in both, grouped
    /// by the pair of staves they show, the lighter-reading group in each frame is the one whose texels are lighter.
    /// </summary>
    private static void CheckStaveOrder(PatinaChecks checks, Readback read, Dictionary<float, ScenarioFrame> frames, float a, float b)
    {
        const int n = SwatchSphere.Size;
        var size = 256;
        var ja = Judge(read, size, a);
        var jb = Judge(read, size, b);
        var base_ = Image(read, OutputRole.BaseColour, size);
        // Each stave's tone: the mean luma of its stave texels.
        var tone = new double[8];
        var count = new int[8];
        for (var ty = 0; ty < size; ty++)
            for (var tx = 0; tx < size; tx++)
                if (base_[(ty * size + tx) * 4] >= 120) { tone[tx * 8 / size] += Luma(base_.AsSpan((ty * size + tx) * 4, 4)); count[tx * 8 / size]++; }
        for (var s = 0; s < 8; s++) tone[s] /= Math.Max(1, count[s]);

        var groups = new Dictionary<(int, int), (double A, double B, int N)>();
        for (var at = 0; at < n * n; at++)
        {
            if (ja[at].Kind != Kind.Stave || jb[at].Kind != Kind.Stave) continue;
            var key = (ja[at].Stave, jb[at].Stave);
            groups.TryGetValue(key, out var g);
            groups[key] = (g.A + Luma(frames[a].Pixels.AsSpan(at * 4, 4)), g.B + Luma(frames[b].Pixels.AsSpan(at * 4, 4)), g.N + 1);
        }
        int judged = 0, wrong = 0;
        string? first = null;
        foreach (var ((sa, sb), g) in groups)
        {
            if (g.N < 30) continue;
            var predicted = tone[sa] - tone[sb];
            if (Math.Abs(predicted) < ToneStep * (tone[sa] + tone[sb]) / 2) continue;
            judged++;
            var measured = g.A / g.N - g.B / g.N;
            if (Math.Sign(measured) != Math.Sign(predicted))
            {
                wrong++;
                first ??= $"stave {sa} at {a} reads {g.A / g.N:0.0} and stave {sb} at {b} reads {g.B / g.N:0.0}, but their texels are {tone[sa]:0.0} and {tone[sb]:0.0}";
            }
        }
        Order.AppendLine(CultureInfo.InvariantCulture, $"{a}\t{b}\t{string.Join(" ", groups.Where(g => g.Value.N >= 30).Select(g => $"{g.Key.Item1}/{g.Key.Item2}:{g.Value.N}:{g.Value.A / g.Value.N - g.Value.B / g.Value.N:0.0}:{tone[g.Key.Item1] - tone[g.Key.Item2]:0.0}"))}");
        checks.Check(judged >= 1 && wrong == 0, $"swatch: stave order {a} against {b}: {wrong} of {judged} stave pairs read the wrong way round; first {first}");
    }
}
