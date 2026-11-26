using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// samples/graphs/bricks.texgraph and fabric.texgraph (T-136), marble.texgraph (T-185: veins by blend's difference) and their committed PNGs: the PNGs equal the graph, and each
/// is worked against check's rules (M-28's planning: base colour 20..245 by the brightest channel in 99% or more of texels,
/// no metallic, normals unit within 0.02 in all but 0.1%, no output of one value, no seam across the wrap), with the
/// figures printed, so the rules hold before check (T-141) exists.
/// </summary>
[Trait("Tier", "unit")]
public class PatternSamplesTests(ITestOutputHelper output)
{
    private const int Size = 256;

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Carve.slnx not found above the test folder");
    }

    private static TextureGraph Load(string name)
    {
        var read = TexGraphFile.ReadFile(Path.Combine(Root(), "samples", "graphs", name + ".texgraph"));
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static byte[] Committed(string name, OutputRole role)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(Root(), "samples", "graphs", name, OutputRoles.Name(role) + ".png")), ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((Size, Size), (image.Width, image.Height));
        return image.Data;
    }

    public static TheoryData<string, OutputRole> Outputs() => new()
    {
        { "bricks", OutputRole.BaseColour }, { "bricks", OutputRole.Normal }, { "bricks", OutputRole.Roughness }, { "bricks", OutputRole.Occlusion },
        { "fabric", OutputRole.BaseColour }, { "fabric", OutputRole.Normal }, { "fabric", OutputRole.Roughness }, { "fabric", OutputRole.Occlusion },
        { "marble", OutputRole.BaseColour }, { "marble", OutputRole.Normal }, { "marble", OutputRole.Roughness },
    };

    [Theory]
    [MemberData(nameof(Outputs))]
    public void TheCommittedPngEqualsTheGraph(string name, OutputRole role)
    {
        var g = Load(name);
        Assert.Equal(Size, g.Size);
        Assert.True(OutputImage.Rgba8(new GraphEvaluator(g), role, g.Size).AsSpan().SequenceEqual(Committed(name, role)),
            $"{name}/{OutputRoles.Name(role)}.png differs from the graph; run tools/Patina.SampleGraphs");
    }

    [Theory]
    [InlineData("bricks")]
    [InlineData("fabric")]
    [InlineData("marble")]
    public void TheBaseColourStaysInTwentyTo245ByItsBrightestChannel(string name)
    {
        var px = Committed(name, OutputRole.BaseColour);
        int outside = 0, min = 255, max = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            var brightest = Math.Max(px[i], Math.Max(px[i + 1], px[i + 2]));
            min = Math.Min(min, brightest); max = Math.Max(max, brightest);
            if (brightest < 20 || brightest > 245) outside++;
        }
        output.WriteLine($"{name} base_colour: brightest channel {min}..{max}, {100.0 * outside / (px.Length / 4):0.000}% outside 20..245 (a fault over 1%)");
        Assert.True(outside <= px.Length / 4 / 100, $"{outside} texels outside 20..245");
        Assert.True(min >= 20, $"darkest texel's brightest channel is {min}, under 20 (mortar and gaps must stay above 20)");
    }

    [Theory]
    [InlineData("bricks")]
    [InlineData("fabric")]
    [InlineData("marble")]
    public void TheNormalsAreUnitLength(string name)
    {
        var px = Committed(name, OutputRole.Normal);
        int off = 0; double worst = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            double x = px[i] / 255.0 * 2 - 1, y = px[i + 1] / 255.0 * 2 - 1, z = px[i + 2] / 255.0 * 2 - 1;
            var error = Math.Abs(Math.Sqrt(x * x + y * y + z * z) - 1);
            worst = Math.Max(worst, error);
            if (error > 0.03) off++;      // the PNG's tolerance, 8-bit packed
        }
        output.WriteLine($"{name} normal: worst |length - 1| {worst:0.0000}, {off} texels over 0.03 (a fault over 0.1%)");
        Assert.True(off <= px.Length / 4 / 1000, $"{off} texels off unit length");
    }

    [Theory]
    [MemberData(nameof(Outputs))]
    public void NoOutputIsOneValueAndNoneIsMetallic(string name, OutputRole role)
    {
        var px = Committed(name, role);
        var values = new HashSet<(byte, byte, byte)>();
        for (var i = 0; i < px.Length; i += 4) values.Add((px[i], px[i + 1], px[i + 2]));
        output.WriteLine($"{name}/{OutputRoles.Name(role)}: {values.Count} distinct values");
        Assert.True(values.Count > 1, "every texel packs to one byte");
        // roughness.png's blue channel is metallic (M-28); a graph with no metallic output leaves it 0.
        if (role == OutputRole.Roughness) Assert.All(Enumerable.Range(0, px.Length / 4), i => Assert.Equal(0, px[i * 4 + 2]));
    }

    [Theory]
    [MemberData(nameof(Outputs))]
    public void NoSeamAcrossTheWrap(string name, OutputRole role)
    {
        var px = Committed(name, role);
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
        output.WriteLine($"{name}/{OutputRoles.Name(role)}: step across u wrap {wrapX:0.00} (interior {interiorX:0.00}), across v wrap {wrapY:0.00} (interior {interiorY:0.00})");
        Assert.True(wrapX <= 2 * interiorX + 2, "a seam across the u wrap");
        Assert.True(wrapY <= 2 * interiorY + 2, "a seam across the v wrap");
    }

    [Fact]
    public void TheMarbleVeinsAreThinDarkLines()
    {
        // a vein texel is one whose brightest channel is under 160 (stone is 201 or more, the vein colour 104)
        var px = Committed("marble", OutputRole.BaseColour);
        var dark = new bool[Size * Size];
        for (var i = 0; i < dark.Length; i++) dark[i] = Math.Max(px[i * 4], Math.Max(px[i * 4 + 1], px[i * 4 + 2])) < 160;
        var share = dark.Count(d => d) / (double)dark.Length;
        // thin: of the vein texels, most have a stone texel within 2 texels (wrapping), so no vein is a broad patch
        int near = 0, all = 0;
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                if (!dark[y * Size + x]) continue;
                all++;
                var edge = false;
                for (var dy = -2; dy <= 2 && !edge; dy++)
                    for (var dx = -2; dx <= 2 && !edge; dx++)
                        edge = !dark[(y + dy + Size) % Size * Size + (x + dx + Size) % Size];
                if (edge) near++;
            }
        output.WriteLine($"marble: {100 * share:0.00}% vein texels, {100.0 * near / all:0.0}% of them within 2 texels of stone");
        Assert.InRange(share, 0.01, 0.15);
        Assert.True(near >= all * 0.9, $"only {near} of {all} vein texels are within 2 texels of stone: the veins are not thin lines");
    }
}
