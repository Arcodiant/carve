using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// samples/graphs/cobblestones.texgraph (T-135) and its four committed PNGs: the PNGs equal the graph, and the graph is
/// worked against check's rules (decided at planning, built by T-141): base colour by brightest channel in 20..245 for
/// 99% of texels or more, every output varying, normals of unit length, no seam across the wrap.
/// </summary>
[Trait("Tier", "unit")]
public class CobblestonesGraphTests
{
    private const int Size = 256;

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Carve.slnx not found above the test folder");
    }

    private static string GraphPath() => Path.Combine(Root(), "samples", "graphs", "cobblestones.texgraph");

    private static TextureGraph Cobblestones()
    {
        var read = TexGraphFile.ReadFile(GraphPath());
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static string PngPath(OutputRole role) => Path.Combine(Root(), "samples", "graphs", "cobblestones", OutputRoles.Name(role) + ".png");

    private static byte[] Committed(OutputRole role)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(PngPath(role)), ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((Size, Size), (image.Width, image.Height));
        return image.Data;
    }

    private static int Px(byte[] rgba, int x, int y, int channel) => rgba[(y * Size + x) * 4 + channel];

    [Fact]
    public void TheGraphIsSizedAt256WithAllFourOutputsAndIsInTheFormatsCanonicalText()
    {
        var g = Cobblestones();
        Assert.Equal(Size, g.Size);
        Assert.Equal(OutputRoles.All, new GraphEvaluator(g).Roles);
        Assert.Equal(TexGraphFile.Write(g), File.ReadAllText(GraphPath()).Replace("\r\n", "\n"));
    }

    [Theory]
    [InlineData(OutputRole.BaseColour)]
    [InlineData(OutputRole.Normal)]
    [InlineData(OutputRole.Roughness)]
    [InlineData(OutputRole.Occlusion)]
    public void TheCommittedPngEqualsTheGraphPixelForPixelAndByteForByte(OutputRole role)
    {
        var evaluator = new GraphEvaluator(Cobblestones());
        var made = OutputImage.Rgba8(evaluator, role, Size);
        Assert.True(made.AsSpan().SequenceEqual(Committed(role)),
            $"samples/graphs/cobblestones/{OutputRoles.Name(role)}.png differs from the graph; remake with dotnet run --project tools/Patina.SampleGraphs");
        Assert.Equal(File.ReadAllBytes(PngPath(role)), OutputImage.Make(evaluator, role, Size).Png);
    }

    [Theory]
    [InlineData(OutputRole.BaseColour)]
    [InlineData(OutputRole.Normal)]
    [InlineData(OutputRole.Roughness)]
    [InlineData(OutputRole.Occlusion)]
    public void TheGraphGivesTheSameBytesInAnyTileSizeUnderAnyCap(OutputRole role)
    {
        var evaluator = new GraphEvaluator(Cobblestones());
        var whole = OutputImage.Rgba8(evaluator, role, Size, tileSize: Size);
        foreach (var side in new[] { 16, 64 })
            foreach (var cap in new[] { 0, GraphEvaluator.DefaultCacheBytes })
                Assert.True(whole.AsSpan().SequenceEqual(OutputImage.Rgba8(evaluator, role, Size, side, cap)), $"{OutputRoles.Name(role)} in tiles of {side} under a cap of {cap} differs from one tile");
    }

    // ---- check's rules (T-141), worked here on the committed PNGs -----------------------------------------------

    [Fact]
    public void BaseColourByBrightestChannelIsIn20To245ForAtLeast99PercentOfTexels()
    {
        var colour = Committed(OutputRole.BaseColour);
        var out_ = 0;
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var brightest = Math.Max(Px(colour, x, y, 0), Math.Max(Px(colour, x, y, 1), Px(colour, x, y, 2)));
                if (brightest is < 20 or > 245) out_++;
            }
        Assert.True(out_ * 100.0 <= Size * Size, $"{out_} of {Size * Size} texels have a brightest channel outside 20..245 (rule: at most 1%)");
    }

    [Fact]
    public void EveryOutputVariesAndNoOutputPacksToOneByte()
    {
        foreach (var role in OutputRoles.All)
        {
            var image = Committed(role);
            var seen = new HashSet<int>();
            for (var i = 0; i < Size * Size; i++) seen.Add(image[i * 4 + (role == OutputRole.Roughness ? 1 : 0)]);
            Assert.True(seen.Count >= 8, $"{OutputRoles.Name(role)} has {seen.Count} distinct bytes");
        }
    }

    [Fact]
    public void NormalsAreOfUnitLengthWithinTwoPercentInAllButATenthOfAPercentOfTexels()
    {
        var normal = Committed(OutputRole.Normal);
        var bad = 0;
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                double nx = (Px(normal, x, y, 0) - 127.5) / 127.5, ny = (Px(normal, x, y, 1) - 127.5) / 127.5, nz = (Px(normal, x, y, 2) - 127.5) / 127.5;
                if (Math.Abs(Math.Sqrt(nx * nx + ny * ny + nz * nz) - 1) > 0.02) bad++;
            }
        Assert.True(bad * 1000.0 <= Size * Size, $"{bad} of {Size * Size} normals are off unit length by more than 0.02 (rule: at most 0.1%)");
    }

    /// <summary>The mean step between neighbouring texels (every channel but alpha) across the wrap on one axis, and inside the image on the same axis.</summary>
    private static (double Seam, double Interior) Steps(byte[] rgba, bool alongU)
    {
        double seam = 0, interior = 0;
        long nSeam = 0, nInterior = 0;
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var (nx, ny) = alongU ? ((x + 1) % Size, y) : (x, (y + 1) % Size);
                var step = 0;
                for (var c = 0; c < 3; c++) step += Math.Abs(Px(rgba, nx, ny, c) - Px(rgba, x, y, c));
                var wraps = alongU ? x == Size - 1 : y == Size - 1;
                if (wraps) { seam += step / 3.0; nSeam++; } else { interior += step / 3.0; nInterior++; }
            }
        return (seam / nSeam, interior / nInterior);
    }

    [Theory]
    [InlineData(OutputRole.BaseColour)]
    [InlineData(OutputRole.Normal)]
    [InlineData(OutputRole.Roughness)]
    [InlineData(OutputRole.Occlusion)]
    public void NoSeamAcrossTheWrap(OutputRole role)
    {
        var image = Committed(role);
        foreach (var alongU in new[] { true, false })
        {
            var (seam, interior) = Steps(image, alongU);
            Assert.True(seam <= 2 * interior + 2, $"{OutputRoles.Name(role)} across the wrap in {(alongU ? "u" : "v")}: mean step {seam:F2} against interior {interior:F2} (rule: at most twice it plus 2)");
        }
    }

    // ---- what the picture should show ----------------------------------------------------------------------------

    [Fact]
    public void StonesAreLightGapsAreDarkRoughAndOccluded()
    {
        var colour = Committed(OutputRole.BaseColour);
        var rough = Committed(OutputRole.Roughness);
        var occlusion = Committed(OutputRole.Occlusion);
        // the darkest tenth of texels by occlusion are the gaps; the lightest half are stone
        var order = Enumerable.Range(0, Size * Size).OrderBy(i => occlusion[i * 4]).ToArray();
        double Mean(IEnumerable<int> texels, Func<int, double> f) => texels.Average(f);
        var gaps = order.Take(Size * Size / 10).ToArray();
        var stone = order.Skip(Size * Size / 2).ToArray();
        Assert.True(Mean(gaps, i => colour[i * 4]) * 1.6 < Mean(stone, i => colour[i * 4]), "gaps are much darker than stone");
        Assert.True(Mean(gaps, i => rough[i * 4 + 1]) > Mean(stone, i => rough[i * 4 + 1]) + 10, "gaps are rougher than stone");
    }

    [Fact]
    public void EachStoneHasItsOwnTone()
    {
        // one value per stone: the mean red inside a 36-texel-spaced grid of stone centres (the stones are about 43 texels across) differs between stones
        var colour = Committed(OutputRole.BaseColour);
        var means = new List<double>();
        for (var gy = 0; gy < 6; gy++)
            for (var gx = 0; gx < 6; gx++)
            {
                double sum = 0;
                for (var y = 0; y < 8; y++)
                    for (var x = 0; x < 8; x++) sum += Px(colour, gx * 43 + 17 + x, gy * 43 + 17 + y, 0);
                means.Add(sum / 64);
            }
        Assert.True(means.Max() / means.Min() > 1.25, $"stone means {string.Join(", ", means.Select(m => m.ToString("F0")))}");
    }
}
