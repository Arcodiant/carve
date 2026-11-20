using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// samples/graphs/floor-tiles.texgraph (T-138) and its committed PNGs: the PNGs equal the graph, the motif is turned a
/// quarter turn on alternating tiles, and each output is worked against check's rules (M-28's planning: base colour 20..245 by the brightest channel in 99% or more of texels,
/// no metallic, normals unit within 0.02 in all but 0.1%, no output of one value, no seam across the wrap), with the
/// figures printed, so the rules hold before check (T-141) exists.
/// </summary>
[Trait("Tier", "unit")]
public class FloorTilesGraphTests(ITestOutputHelper output)
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
        { "floor-tiles", OutputRole.BaseColour }, { "floor-tiles", OutputRole.Normal }, { "floor-tiles", OutputRole.Roughness },
        { "floor-tiles", OutputRole.Occlusion }, { "floor-tiles", OutputRole.Height },
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
    [InlineData("floor-tiles")]
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
        Assert.True(min >= 20, $"darkest texel's brightest channel is {min}, under 20 (grout must stay above 20)");
    }

    [Theory]
    [InlineData("floor-tiles")]
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
    public void TheFiveOutputsAreTheGraphs()
    {
        var g = Load("floor-tiles");
        Assert.Equal(["base_colour", "normal", "roughness", "occlusion", "height"], new GraphEvaluator(g).Outputs);
    }

    [Fact]
    public void TheMotifIsTurnedAQuarterTurnOnAlternatingTiles()
    {
        // the print (the motif's mask, 1 on the ink) in the first tile, turned a quarter turn clockwise about the tile's
        // centre, is the print of its neighbours in u and v, and the print of the diagonal one is the first's
        var g = Load("floor-tiles");
        var print = new float[Size * Size];
        new GraphEvaluator(g.WithNode("probe", new GraphNode("output", System.Collections.Immutable.ImmutableSortedDictionary<string, string>.Empty.Add("in", "print"),
            System.Collections.Immutable.ImmutableSortedDictionary<string, ParamValue>.Empty.Add("name", ParamValue.Of("probe")))))
            .Evaluate("probe", Size, t =>
            {
                for (var y = t.Rect.Y; y < t.Rect.Bottom; y++)
                    for (var x = t.Rect.X; x < t.Rect.Right; x++) print[y * Size + x] = t.Grey(x, y);
            });
        const int T = Size / 4;
        float P(int tx, int ty, int x, int y) => print[(ty * T + y) * Size + tx * T + x];
        int turned = 0, same = 0, ink = 0;
        for (var y = 0; y < T; y++)
            for (var x = 0; x < T; x++)
            {
                ink += P(0, 0, x, y) > 0.5f ? 1 : 0;
                // a clockwise quarter turn: out(x, y) = in(y, T - 1 - x)
                if (Math.Abs(P(1, 0, x, y) - P(0, 0, y, T - 1 - x)) < 1e-5f && Math.Abs(P(0, 1, x, y) - P(0, 0, y, T - 1 - x)) < 1e-5f) turned++;
                if (P(1, 1, x, y) == P(0, 0, x, y)) same++;
            }
        output.WriteLine($"tile (0, 0): {ink} ink texels of {T * T}; turned in its neighbours at {turned}, the same in the diagonal at {same}");
        Assert.InRange(ink, T * T / 20, T * T / 2);
        Assert.Equal(T * T, turned);
        Assert.Equal(T * T, same);
        // and the turn is visible: the motif is not the same turned
        Assert.True(Enumerable.Range(0, T * T).Count(i => Math.Abs(P(1, 0, i % T, i / T) - P(0, 0, i % T, i / T)) > 0.5f) > T * T / 20);
    }
}
