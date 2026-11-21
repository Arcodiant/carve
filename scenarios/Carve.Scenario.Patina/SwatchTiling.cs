using System.Globalization;
using System.Text;
using System.Text.Json;
using Carve.Debug;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// swatch's sphere tiling (T-156), judged on a graph that differs in u and in v: a base colour of 4 stripes across (u) and 2
/// down (v) multiplied, so a texel is white where both are on and black elsewhere (exactly 0 or 255, exported at 64 through
/// patina's own <c>export</c>). The sphere at tiling n wears the image n times in u and v, so a pixel's texel is
/// ((u n) mod 1, (v n) mod 1), with (u, v) where <see cref="SwatchSphere"/> puts the pixel's ray. A pixel is judged only when
/// every texel within its footprint (a pixel's step in texel space, wrapped, plus one) is the same colour and the surface faces
/// the camera, as the planks' check does; it must then read lit-white (luma at least <see cref="WhiteMin"/>) or dark (luma at
/// most <see cref="BlackMax"/>). Frames drawn at tiling 1 and 3 are judged against the matching prediction, and the breaks
/// (tiling ignored, in u only, in v only, 2 for 3, the axes swapped) are the scenario's own wrong predictions against a frame.
/// </summary>
internal static class SwatchTiling
{
    private const int Size = 64;
    private const int N = SwatchSphere.Size;
    private const float Facing = 0.5f;
    public const double WhiteMin = 100;
    public const double BlackMax = 60;

    public const string Grid = """
        {"graph":{"version":1,"size":64,"seed":1,"nodes":{
          "across":{"type":"stripes","params":{"count":4,"axis":"u","width":0.5}},
          "down":{"type":"stripes","params":{"count":2,"axis":"v","width":0.5}},
          "both":{"type":"blend","inputs":{"a":"across","b":"down"},"params":{"mode":"multiply","opacity":1}},
          "out":{"type":"output","inputs":{"in":"both"},"params":{"role":"base_colour"}}}}}
        """;

    /// <summary>Per pixel: +1 predicted white, -1 predicted black, 0 not judged, from the 64 px red channel at tiling (tu, tv).</summary>
    public static sbyte[] Predict(byte[] texels, float angle, int tu, int tv, SwatchSphere.Hit?[,]? given = null, float facing = Facing)
    {
        var hits = given ?? SwatchSphere.HitsAt(angle);
        bool White(int x, int y) => texels[((((y % Size) + Size) % Size) * Size + (((x % Size) + Size) % Size)) * 4] >= 128;
        static float Wrapped(float d) => MathF.Abs(d - MathF.Round(d));
        var judged = new sbyte[N * N];
        for (var y = 0; y < N; y++)
        {
            for (var x = 0; x < N; x++)
            {
                if (hits[x, y] is not { } hit || hit.Facing < facing || hits[x + 1, y] is not { } right || hits[x, y + 1] is not { } below) continue;
                var footprint = MathF.Max(
                    MathF.Max(Wrapped((right.U - hit.U) * tu), Wrapped((below.U - hit.U) * tu)),
                    MathF.Max(Wrapped((right.V - hit.V) * tv), Wrapped((below.V - hit.V) * tv))) * Size;
                var reach = (int)MathF.Ceiling(footprint) + 1;
                var tx = (int)MathF.Floor(hit.U * tu * Size);
                var ty = (int)MathF.Floor(hit.V * tv * Size);
                var kind = White(tx, ty);
                var same = true;
                for (var dy = -reach; dy <= reach && same; dy++)
                    for (var dx = -reach; dx <= reach && same; dx++)
                        same = White(tx + dx, ty + dy) == kind;
                if (same) judged[y * N + x] = (sbyte)(kind ? 1 : -1);
            }
        }
        return judged;
    }

    private static double Luma(byte[] p, int at) => 0.2126 * p[at * 4] + 0.7152 * p[at * 4 + 1] + 0.0722 * p[at * 4 + 2];

    /// <summary>Judges a sphere frame against the prediction; the numbers are appended to <paramref name="measured"/>.</summary>
    public static void Check(PatinaChecks checks, string label, ScenarioFrame frame, byte[] texels, float angle, int tu, int tv, StringBuilder? measured = null, SwatchSphere.Hit?[,]? given = null, float facing = Facing)
    {
        if (frame.Width != N || frame.Height != N)
        {
            checks.Check(false, $"swatch: tiling {label}: the sphere is {frame.Width} x {frame.Height}, not {N} x {N}");
            return;
        }
        var predicted = Predict(texels, angle, tu, tv, given, facing);
        int white = 0, black = 0, wrongWhite = 0, wrongBlack = 0;
        double whiteLow = 255, blackHigh = 0;
        for (var at = 0; at < predicted.Length; at++)
        {
            if (predicted[at] == 0) continue;
            var luma = Luma(frame.Pixels, at);
            if (predicted[at] > 0) { white++; whiteLow = Math.Min(whiteLow, luma); if (luma < WhiteMin) wrongWhite++; }
            else { black++; blackHigh = Math.Max(blackHigh, luma); if (luma > BlackMax) wrongBlack++; }
        }
        measured?.Append(CultureInfo.InvariantCulture, $"{label}\t{angle}\t{tu} x {tv}\t{white}\t{whiteLow:0.0}\t{wrongWhite}\t{black}\t{blackHigh:0.0}\t{wrongBlack}\n");
        checks.Check(white > 3000 && black > 3000, $"swatch: tiling {label}: too few pixels judged ({white} white, {black} black)");
        checks.Check(wrongWhite <= white / 200 && wrongBlack <= black / 200,
            $"swatch: tiling {label}: {wrongWhite} of {white} white-predicted pixels read below luma {WhiteMin} (lowest {whiteLow:0.0}) and {wrongBlack} of {black} black-predicted read above {BlackMax} (highest {blackHigh:0.0}), predicted at {tu} x {tv}");
    }

    public static byte[] Decode(string path) => ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha).Data;

    public static async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        await checks.CallAsync("define_graph", Grid);
        var folder = Path.Combine(driver.RunFolder, "swatch-grid-texels");
        await checks.CallAsync("export", JsonSerializer.Serialize(new { folder, size = 64 }));
        var texels = Decode(Path.Combine(folder, "base_colour.png"));
        checks.Check(texels[0] >= 128 && texels[40 * 4] < 128 && texels[(40 * Size) * 4] < 128, "swatch: the tiling grid's exported texels are not 4 stripes across and 2 down");

        var measured = new StringBuilder("frame\tangle\tpredicted\twhite\twhite_luma_min\twhite_wrong\tblack\tblack_luma_max\tblack_wrong\n");
        var frames = new Dictionary<(float Angle, int Tiling), ScenarioFrame>();
        foreach (var (angle, tiling) in new[] { (0f, 1), (0f, 3), (90f, 3), (0f, 8) })
        {
            var frame = await driver.CaptureToolAsync("swatch", $"swatch-sphere-grid-{angle.ToString(CultureInfo.InvariantCulture)}-tiling-{tiling}", new { view = "sphere", angle, tiling });
            frames[(angle, tiling)] = frame;
            Check(checks, $"{angle} at {tiling}", frame, texels, angle, tiling, tiling, measured);
        }
        // Tiling 1 is the default: the same pixels as no argument.
        var plain = await driver.CaptureToolAsync("swatch", "swatch-sphere-grid-0-default", new { view = "sphere" });
        checks.Check(plain.Pixels.AsSpan().SequenceEqual(frames[(0f, 1)].Pixels), "swatch: the sphere at tiling 1 is not the sphere with no tiling given");

        // The sheet's sphere cell wears the tiling, and the sheet's title and caption name it only when it is not 1.
        var sheet = await driver.CaptureToolAsync("swatch", "swatch-sheet-grid-tiling-3", new { tiling = 3 });
        var caption = await checks.CallAsync("swatch", """{"tiling":3}""");
        checks.Check(caption.Contains("tiling 3", StringComparison.Ordinal), "swatch: the sheet's caption at tiling 3 does not name it: " + caption);
        var plainSheet = await checks.CallAsync("swatch", "{}");
        checks.Check(!plainSheet.Contains("tiling", StringComparison.Ordinal), "swatch: the sheet's caption at the default names a tiling: " + plainSheet);
        checks.Check(sheet.Width > N, "swatch: the sheet at tiling 3 was not drawn");

        // The worked example as written: a sphere at tiling 3 of the open graph, and its refusals.
        var worked = await checks.CallAsync("swatch", """{"view":"sphere","tiling":3}""");
        checks.Check(worked.Contains("angle 0, tiling 3, 512 x 512 px", StringComparison.Ordinal), "swatch: the tiling's worked example said " + worked);
        await checks.CallAsync("swatch", """{"view":"sphere","tiling":9}""", refused: true);
        await checks.CallAsync("swatch", """{"view":"flat","tiling":3}""", refused: true);

        // The breaks: made here, a frame judged against a wrong prediction (the product has no switch for any of them).
        var log = new StringBuilder();
        void Caught(string name, Action<PatinaChecks> run)
        {
            var probe = new PatinaChecks(driver);
            run(probe);
            log.AppendLine("== " + name);
            foreach (var failure in probe.Failures) log.AppendLine("  " + failure);
            checks.Check(probe.Failures.Any(f => f.Contains("white-predicted", StringComparison.Ordinal)), $"swatch: the break {name} was not caught by the tiling check");
        }
        var three = frames[(0f, 3)];
        Caught("tiling-ignored", probe => Check(probe, "ignored", frames[(0f, 1)], texels, 0, 3, 3));
        Caught("tiling-in-u-only", probe => Check(probe, "u only", three, texels, 0, 3, 1));
        Caught("tiling-in-v-only", probe => Check(probe, "v only", three, texels, 0, 1, 3));
        Caught("tiling-2-for-3", probe => Check(probe, "2 for 3", three, texels, 0, 2, 2));
        Caught("tiling-3-for-1", probe => Check(probe, "3 for 1", frames[(0f, 3)], texels, 0, 1, 1));
        Caught("axes-swapped", probe => Check(probe, "swapped", three, SwapAxes(texels), 0, 3, 3));
        await SwatchCubeCheck.RunAsync(driver, checks, measured, log);
        File.WriteAllText(Path.Combine(driver.RunFolder, "swatch-tiling-checks.tsv"), measured + "\n" + log);
    }

    /// <summary>The texels with u and v exchanged (row and column), the break for a prediction made on the wrong axes.</summary>
    private static byte[] SwapAxes(byte[] texels)
    {
        var swapped = new byte[texels.Length];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                Array.Copy(texels, (y * Size + x) * 4, swapped, (x * Size + y) * 4, 4);
        return swapped;
    }
}
