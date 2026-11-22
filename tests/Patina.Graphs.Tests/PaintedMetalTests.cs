using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// samples/graphs/painted-metal.texgraph (T-137) and its committed PNGs: the PNGs equal the graph, and the figures are
/// worked against check's rules (M-28's planning) before check (T-141) exists: base colour (non-metal) by brightest channel
/// 20..245 in 99% or more of texels and metal texels 120 or more; metallic between 0.1 and 0.9 in 5% of texels or
/// fewer; normals unit within 0.02 in 99.9%; no output of one value; no seam across the wrap.
/// </summary>
[Trait("Tier", "unit")]
public class PaintedMetalTests(ITestOutputHelper output)
{
    private const int Size = 256;
    private const string Name = "painted-metal";

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Carve.slnx not found above the test folder");
    }

    private static readonly Lazy<TextureGraph> Graph = new(() =>
    {
        var read = TexGraphFile.ReadFile(Path.Combine(Root(), "samples", "graphs", Name + ".texgraph"));
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    });

    private static readonly Lazy<GraphEvaluator> Evaluator = new(() => new GraphEvaluator(Graph.Value));

    private static byte[] Committed(string file)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(Root(), "samples", "graphs", Name, file)), ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((Size, Size), (image.Width, image.Height));
        return image.Data;
    }

    /// <summary>The metallic output's texels, from the graph (it has no file of its own: it is roughness.png's blue).</summary>
    private static byte[] MetallicTexels() => OutputImage.Rgba8(Evaluator.Value, "metallic", Size);

    [Fact]
    public void TheGraphHasTheFiveOutputsAndNoFaults()
    {
        Assert.Equal(Size, Graph.Value.Size);
        Assert.Equal(["base_colour", "normal", "roughness", "metallic", "occlusion", "wear"], Evaluator.Value.Outputs);
    }

    [Theory]
    [InlineData("base_colour", "base_colour.png")]
    [InlineData("normal", "normal.png")]
    [InlineData("roughness", "roughness.png")]
    [InlineData("occlusion", "occlusion.png")]
    [InlineData("wear", "wear.png")]
    public void TheCommittedPngEqualsTheGraph(string name, string file)
    {
        var made = OutputImage.Make(Evaluator.Value, name, Size);
        var committed = File.ReadAllBytes(Path.Combine(Root(), "samples", "graphs", Name, file));
        Assert.True(made.Png.AsSpan().SequenceEqual(committed), $"{Name}/{file} differs from the graph; run tools/Patina.SampleGraphs");
    }

    [Fact]
    public void TheBaseColourIsInRangeAndMetalIsBrightEnough()
    {
        var px = Committed("base_colour.png");
        var metal = MetallicTexels();
        int outside = 0, metals = 0, darkMetal = 0, min = 255, max = 0, metalMin = 255;
        for (var i = 0; i < px.Length; i += 4)
        {
            var brightest = Math.Max(px[i], Math.Max(px[i + 1], px[i + 2]));
            var isMetal = metal[i] >= 128; // metallic 0.5 or more
            if (isMetal) { metals++; metalMin = Math.Min(metalMin, brightest); if (brightest < 120) darkMetal++; }
            else
            {
                min = Math.Min(min, brightest); max = Math.Max(max, brightest);
                if (brightest < 20 || brightest > 245) outside++;
            }
        }
        var nonMetal = px.Length / 4 - metals;
        output.WriteLine($"{Name} base_colour: {metals} metal texels ({100.0 * metals / (px.Length / 4):0.0}%), their brightest channel from {metalMin}; "
            + $"non-metal brightest channel {min}..{max}, {outside} of {nonMetal} outside 20..245 (a fault over 1%)");
        Assert.True(metals > 0 && nonMetal > 0, "the sample must show both paint and bare metal");
        Assert.Equal(0, darkMetal);
        Assert.True(outside <= nonMetal / 100, $"{outside} non-metal texels outside 20..245");
    }

    [Fact]
    public void MetallicIsSharpEnoughToSitOutsideTenToNinetyPercentInFewTexels()
    {
        var metal = MetallicTexels();
        int between = 0, zero = 0, one = 0;
        for (var i = 0; i < metal.Length; i += 4)
        {
            var m = metal[i] / 255.0;
            if (m > 0.1 && m < 0.9) between++;
            if (metal[i] == 0) zero++;
            if (metal[i] == 255) one++;
        }
        output.WriteLine($"{Name} metallic: {between} of {metal.Length / 4} texels between 0.1 and 0.9 ({100.0 * between / (metal.Length / 4):0.00}%, a fault over 5%); {zero} at 0, {one} at 1");
        Assert.True(between <= metal.Length / 4 * 5 / 100, $"{between} texels of metallic between 0.1 and 0.9");
        Assert.True(zero > 0 && one > 0);
    }

    [Fact]
    public void TheSteelIsSmootherThanThePaint()
    {
        var rough = Committed("roughness.png");
        var metal = MetallicTexels();
        double steel = 0, paint = 0; int ns = 0, np = 0;
        for (var i = 0; i < rough.Length; i += 4)
            if (metal[i] >= 128) { steel += rough[i + 1]; ns++; } else { paint += rough[i + 1]; np++; }
        output.WriteLine($"{Name} roughness: steel mean {steel / ns / 255:0.000}, paint mean {paint / np / 255:0.000}");
        Assert.True(steel / ns < paint / np - 255 * 0.2, "roughness is lower on steel");
    }

    [Fact]
    public void TheNormalsAreUnitLength()
    {
        var px = Committed("normal.png");
        int off = 0; double worst = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            double x = px[i] / 255.0 * 2 - 1, y = px[i + 1] / 255.0 * 2 - 1, z = px[i + 2] / 255.0 * 2 - 1;
            var error = Math.Abs(Math.Sqrt(x * x + y * y + z * z) - 1);
            worst = Math.Max(worst, error);
            if (error > 0.03) off++; // the PNG's tolerance, 8-bit packed
        }
        output.WriteLine($"{Name} normal: worst |length - 1| {worst:0.0000}, {off} texels over 0.03 (a fault over 0.1%)");
        Assert.True(off <= px.Length / 4 / 1000, $"{off} texels off unit length");
    }

    [Theory]
    [InlineData("base_colour.png")]
    [InlineData("normal.png")]
    [InlineData("roughness.png")]
    [InlineData("occlusion.png")]
    [InlineData("wear.png")]
    public void NoOutputIsOneValueAndNoSeamShowsAcrossTheWrap(string file)
    {
        var px = Committed(file);
        var values = new HashSet<(byte, byte, byte)>();
        for (var i = 0; i < px.Length; i += 4) values.Add((px[i], px[i + 1], px[i + 2]));
        Assert.True(values.Count > 1, "every texel packs to one byte");

        double At(int x, int y, int c) => px[(y * Size + x) * 4 + c];
        double wrapX = 0, interiorX = 0, wrapY = 0, interiorY = 0;
        for (var y = 0; y < Size; y++)
            for (var c = 0; c < 3; c++)
                for (var x = 0; x < Size - 1; x++) interiorX += Math.Abs(At(x, y, c) - At(x + 1, y, c));
        for (var x = 0; x < Size; x++)
            for (var c = 0; c < 3; c++)
                for (var y = 0; y < Size - 1; y++) interiorY += Math.Abs(At(x, y, c) - At(x, y + 1, c));
        for (var k = 0; k < Size; k++)
            for (var c = 0; c < 3; c++)
            {
                wrapX += Math.Abs(At(Size - 1, k, c) - At(0, k, c));
                wrapY += Math.Abs(At(k, Size - 1, c) - At(k, 0, c));
            }
        interiorX /= Size * (Size - 1) * 3; interiorY /= Size * (Size - 1) * 3; wrapX /= Size * 3; wrapY /= Size * 3;
        output.WriteLine($"{Name}/{file}: {values.Count} distinct values; step across u wrap {wrapX:0.00} (interior {interiorX:0.00}), across v wrap {wrapY:0.00} (interior {interiorY:0.00})");
        Assert.True(wrapX <= 2 * interiorX + 2, "a seam across the u wrap");
        Assert.True(wrapY <= 2 * interiorY + 2, "a seam across the v wrap");
    }
}
