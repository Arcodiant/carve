using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>samples/graphs/planks.texgraph (T-112) and its four committed PNGs: the PNGs equal the graph, and the graph keeps the barrel's stave rule.</summary>
[Trait("Tier", "unit")]
public class PlanksGraphTests
{
    private const int Size = 256;
    private const int Staves = 8;
    private const int StaveWidth = Size / Staves;   // the barrel's, TextureSet.StaveWidth

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Carve.slnx not found above the test folder");
    }

    private static string GraphPath() => Path.Combine(Root(), "samples", "graphs", "planks.texgraph");

    private static TextureGraph Planks()
    {
        var read = TexGraphFile.ReadFile(GraphPath());
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    [Fact]
    public void T117sPlantInThePlanksGivesItsOneFault()
    {
        // T-117's starter: grained takes a mask from a new grain2 noise, misnamed grain_2. Every other input is still kind-checked, and holds.
        var text = File.ReadAllText(GraphPath())
            .Replace("\"inputs\": { \"a\": \"toned\", \"b\": \"grain\" }", "\"inputs\": { \"a\": \"toned\", \"b\": \"grain\", \"mask\": \"grain_2\" }")
            .Replace("\"wood\": {", "\"grain2\": { \"type\": \"noise\" }, \"wood\": {");
        var read = TexGraphFile.Read(text);
        Assert.True(read.Loaded, read.Error?.ToString());
        var fault = Assert.Single(read.Faults);
        Assert.StartsWith("node 'grained' input 'mask': names 'grain_2', which is not a node; the nodes are ", fault.Message);
        Assert.Contains(", grain2, ", fault.Message);
    }

    private static byte[] Committed(OutputRole role)
    {
        var path = Path.Combine(Root(), "samples", "graphs", "planks", OutputRoles.Name(role) + ".png");
        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((Size, Size), (image.Width, image.Height));
        return image.Data;
    }

    // The built-in nodes, height_to_normal's radius among them, give the same bytes in any tile size as in one tile.
    [Theory]
    [InlineData(OutputRole.BaseColour)]
    [InlineData(OutputRole.Normal)]
    [InlineData(OutputRole.Roughness)]
    [InlineData(OutputRole.Occlusion)]
    public void ThePlanksGiveTheSameBytesTiledAsWhole(OutputRole role)
    {
        const int size = 64;
        var g = Planks();
        var whole = OutputImage.Rgba8(new GraphEvaluator(g), role, size, tileSize: size);
        foreach (var side in new[] { 1, 4, 16, 32 })
            Assert.True(whole.AsSpan().SequenceEqual(OutputImage.Rgba8(new GraphEvaluator(g), role, size, tileSize: side)), $"{OutputRoles.Name(role)} in tiles of {side} differs from one tile");
    }

    [Fact]
    public void TheGraphIsSizedAt256WithAllFourOutputs()
    {
        var g = Planks();
        Assert.Equal(Size, g.Size);
        Assert.Equal(OutputRoles.All, new GraphEvaluator(g).Roles);
    }

    [Fact]
    public void TheGraphFileIsInTheFormatsOwnCanonicalText()
    {
        var text = File.ReadAllText(GraphPath()).Replace("\r\n", "\n");
        Assert.Equal(TexGraphFile.Write(Planks()), text);
    }

    [Theory]
    [InlineData(OutputRole.BaseColour)]
    [InlineData(OutputRole.Normal)]
    [InlineData(OutputRole.Roughness)]
    [InlineData(OutputRole.Occlusion)]
    public void TheCommittedPngEqualsTheGraphPixelForPixel(OutputRole role)
    {
        var made = OutputImage.Rgba8(new GraphEvaluator(Planks()), role, Size);
        Assert.True(made.AsSpan().SequenceEqual(Committed(role)),
            $"samples/graphs/planks/{OutputRoles.Name(role)}.png differs from the graph; remake with dotnet run --project tools/Patina.SampleGraphs");
    }

    [Fact]
    public void TheEncodedPngBytesAreAlsoTheCommittedBytes()
    {
        var evaluator = new GraphEvaluator(Planks());
        foreach (var role in OutputRoles.All)
        {
            var path = Path.Combine(Root(), "samples", "graphs", "planks", OutputRoles.Name(role) + ".png");
            Assert.Equal(File.ReadAllBytes(path), OutputImage.Make(evaluator, role, Size).Png);
        }
    }

    private static int Px(byte[] rgba, int x, int y, int channel) => rgba[(y * Size + x) * 4 + channel];

    [Fact]
    public void StaveBoundariesAreAtKOver8AndEachGapIsSixTexelsWide()
    {
        var colour = Committed(OutputRole.BaseColour);
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var m = x % StaveWidth;
                var r = Px(colour, x, y, 0);
                var inGap = m is 0 or 1 or 2 or 29 or 30 or 31;   // three texels each side of a boundary at u = k / 8
                if (inGap) Assert.True(r < 90, $"({x}, {y}) should be gap, red {r}");
                else if (m is >= 4 and <= 27) Assert.InRange(r, 138, 202);
                else Assert.InRange(r, 90, 202);                  // the one texel of bevel on each side, between gap and stave
            }
    }

    [Fact]
    public void TheStaveColourStaysNearT77sTimesPointEightTwoToOnePointOneEight()
    {
        // T-77: (170, 112, 64) x 0.82..1.18 (the graph's tone and grain give 0.817..1.176), which passed M-7's lit checks.
        var colour = Committed(OutputRole.BaseColour);
        double[] mean = new double[Staves];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                if (x % StaveWidth is < 4 or > 27) continue;
                Assert.InRange(Px(colour, x, y, 0), 170 * 0.81, 170 * 1.19);
                Assert.InRange(Px(colour, x, y, 1), 112 * 0.81, 112 * 1.19);
                Assert.InRange(Px(colour, x, y, 2), 64 * 0.81, 64 * 1.19);
                Assert.Equal(255, Px(colour, x, y, 3));
                mean[x / StaveWidth] += Px(colour, x, y, 0) / (double)(Size * (StaveWidth - 8));
            }
        // a tone per stave: the staves' mean reds differ, and the darkest and lightest are clearly apart
        Assert.True(mean.Max() / mean.Min() > 1.08, $"stave means {string.Join(", ", mean.Select(m => m.ToString("F1")))}");
    }

    [Fact]
    public void TheGrainRunsAlongVSoColumnsVaryLessThanRows()
    {
        var colour = Committed(OutputRole.BaseColour);
        double alongV = 0, alongU = 0;
        var n = 0;
        for (var x = 0; x < Size; x++)
        {
            if (x % StaveWidth is < 5 or > 26) continue;
            for (var y = 0; y < Size - 1; y++)
            {
                alongV += Math.Abs(Px(colour, x, y + 1, 0) - Px(colour, x, y, 0));
                if (x + 1 < Size && (x + 1) % StaveWidth <= 26) alongU += Math.Abs(Px(colour, x + 1, y, 0) - Px(colour, x, y, 0));
                n++;
            }
        }
        Assert.True(alongV * 3 < alongU, $"mean step along v {alongV / n:F2}, along u {alongU / n:F2}");
    }

    [Fact]
    public void TheNormalIsFlatOnTheStaveAndTiltsAtItsEdges()
    {
        var normal = Committed(OutputRole.Normal);
        for (var y = 0; y < Size; y += 17)
            for (var k = 0; k < Staves; k++)
            {
                for (var m = 5; m <= 26; m++)
                    Assert.Equal((128, 128, 255), (Px(normal, k * StaveWidth + m, y, 0), Px(normal, k * StaveWidth + m, y, 1), Px(normal, k * StaveWidth + m, y, 2)));
                Assert.True(Px(normal, k * StaveWidth + 2, y, 0) < 128, "the stave's left bevel leans left (height rises to the right)");
                Assert.True(Px(normal, k * StaveWidth + 29, y, 0) > 128, "the stave's right bevel leans right");
            }
    }

    [Fact]
    public void RoughnessAndOcclusionStayInTheirRanges()
    {
        var rough = Committed(OutputRole.Roughness);
        var occlusion = Committed(OutputRole.Occlusion);
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                Assert.Equal(255, Px(rough, x, y, 0));                              // the packing's occlusion channel, unused
                Assert.InRange(Px(rough, x, y, 1), 140, 204);                       // 0.55 .. 0.8
                Assert.Equal(0, Px(rough, x, y, 2));
                Assert.InRange(Px(occlusion, x, y, 0), 140, 255);                   // 0.55 .. 1
                Assert.Equal(Px(occlusion, x, y, 0), Px(occlusion, x, y, 1));
            }
        Assert.Equal(255, Px(occlusion, 10, 3, 0));                                 // mid stave: open
        Assert.True(Px(occlusion, 0, 3, 0) < 170, "occluded at a stave edge");
    }
}
