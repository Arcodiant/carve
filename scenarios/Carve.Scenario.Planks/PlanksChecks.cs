using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Materials;
using StbImageSharp;

namespace Carve.Scenario.Planks;

/// <summary>The planks' base colour as the graph made it, read from the PNG the scenario binds (never from a frame).</summary>
internal static class PlankTexels
{
    public const int Size = 256, StaveWidth = 32, GapRed = 90;

    private static byte[]? _rgba;

    public static byte[] Rgba => _rgba ??= Read();

    private static byte[] Read()
    {
        var path = Path.Combine(Path.GetDirectoryName(typeof(PlankTexels).Assembly.Location) ?? AppContext.BaseDirectory, "graphs", "planks", "base_colour.png");
        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        if (image.Width != Size || image.Height != Size) throw new InvalidOperationException($"base_colour.png is {image.Width} x {image.Height}, not {Size}");
        return image.Data;
    }

    public static (int R, int G, int B) Colour(int x, int y)
    {
        var i = ((((y % Size) + Size) % Size * Size) + (((x % Size) + Size) % Size)) * 4;
        return (Rgba[i], Rgba[i + 1], Rgba[i + 2]);
    }

    /// <summary>T-112's own definition of a gap texel: red under 90.</summary>
    public static bool? Gap(int x, int y) => Colour(x, y).R < GapRed ? true : null;

    /// <summary>
    /// The research pass's check: in every row, the dark run either side of every stave boundary is at least 3 texels, and no texel of a stave
    /// clear of a boundary (6 texels) is dark, so a P2 footprint (half-width 2) can sit inside a gap and inside a stave.
    /// </summary>
    public static string? GapProblem()
    {
        for (var y = 0; y < Size; y++)
        for (var b = 0; b < Size; b += StaveWidth)
        {
            int left = 0, right = 0;
            while (left < StaveWidth && Colour(b - 1 - left, y).R < GapRed) left++;
            while (right < StaveWidth && Colour(b + right, y).R < GapRed) right++;
            if (left < 3 || right < 3) return $"row {y}, boundary {b}: dark run is {left} texels left and {right} right, 3 each needed";
        }

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            if (Math.Min(x % StaveWidth, StaveWidth - 1 - (x % StaveWidth)) >= 6 && Colour(x, y).R < GapRed) return $"texel ({x}, {y}) is dark more than 6 texels from a boundary";
        }

        return null;
    }
}

/// <summary>The planks' own frame rules, beside M-7's: a gap reads darker than the wood, and the staves differ in tone as the graph's tones predict.</summary>
internal static class PlanksChecks
{
    /// <summary>P2 samples of the gap texels, and the gap rule: every gap pixel is darker (brightest channel) than every stave pixel of the frame.</summary>
    public static void Gap(ScenarioDriver driver, ScenarioFrame frame, Shot shot, Pool woodPool, IReadOnlyDictionary<int, string> materials, string what)
    {
        var gaps = new List<(Hit Hit, int Max)>();
        foreach (var hit in shot.Sites(0, 0))
        {
            if (!materials.TryGetValue(hit.Face.Face, out var name) || name is not ("staves" or "rune-stave")) continue;
            if (Shot.Class(hit.Texel, hit.Half, PlankTexels.Gap) is null) continue;
            var p = frame.Pixel(hit.X, hit.Y);
            gaps.Add((hit, Math.Max(p.R, Math.Max(p.G, p.B))));
        }

        var wood = woodPool.Of(Look.StaveMid).Select(s => Pool.Max(woodPool.Pixel(s))).ToArray();
        var darkestWood = wood.Length == 0 ? 0 : wood.Min();
        Pool.Log.Add($"{what}\tgap samples {gaps.Count}\tgap brightest up to {(gaps.Count == 0 ? -1 : gaps.Max(g => g.Max))}\tstave brightest down to {darkestWood}");
        if (gaps.Count < 8) driver.Fail($"{frame.Name}: {what}: only {gaps.Count} P2 samples of a gap, at least 8 needed");
        if (wood.Length < 8) driver.Fail($"{frame.Name}: {what}: only {wood.Length} P2 samples of stave wood, at least 8 needed");
        foreach (var (hit, max) in gaps.Where(g => g.Max >= darkestWood))
        {
            driver.Fail($"{frame.Name}: {what}: gap texel at ({hit.X}, {hit.Y}) on face {hit.Face.Face} reads {max}, not darker than the darkest stave sample {darkestWood}");
        }
    }

    /// <summary>
    /// Staves differ in tone: per face, the mean red of the stave samples against the mean red the light model predicts from the planks' own texels
    /// (ColourRule.Lit); every pair of faces whose predictions differ by <see cref="Pool.M"/> or more must differ the same way in the frame, and at least one such pair must exist.
    /// </summary>
    public static void Tone(ScenarioDriver driver, Pool pool, Vector3 direction, string what)
    {
        var faces = pool.Of(Look.StaveMid).GroupBy(s => s.Hit.Face.Face).Select(g =>
        {
            var predicted = g.Average(s =>
            {
                var (r, gr, b) = PlankTexels.Colour((int)MathF.Floor(s.Hit.Texel.X), (int)MathF.Floor(s.Hit.Texel.Y));
                return ColourRule.Encode(ColourRule.Lit(ColourRule.Linear(r), ColourRule.Linear(gr), ColourRule.Linear(b), s.Hit.Face.Normal, direction)[0]);
            });
            return (Face: g.Key, Predicted: predicted, Seen: g.Average(s => (double)pool.Pixel(s).R));
        }).ToArray();

        var pairs = 0;
        for (var i = 0; i < faces.Length; i++)
        for (var j = i + 1; j < faces.Length; j++)
        {
            var (p, s) = (faces[i].Predicted - faces[j].Predicted, faces[i].Seen - faces[j].Seen);
            if (Math.Abs(p) < Pool.M) continue;
            pairs++;
            if (Math.Sign(p) != Math.Sign(s)) driver.Fail($"{what}: faces {faces[i].Face} and {faces[j].Face} are predicted {p:F1} apart in red and read {s:F1} apart");
        }

        Pool.Log.Add($"{what}\ttone\t{faces.Length} faces\t{pairs} pairs predicted {Pool.M} or more apart");
        if (pairs < 1) driver.Fail($"{what}: no two staves of the frame are predicted to differ in tone by {Pool.M}");
    }
}
