using Patina.Graphs.Evaluation;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// samples/graphs/grime.texgraph and old-wall.texgraph (T-140) and their committed PNGs: the PNGs equal the graphs
/// (grime at its defaults), old-wall's bricks are bricks.texgraph at mortar 0.06 with grime on the base colour only, and
/// the figures are worked against check's rules (M-28's planning) before check (T-141) exists: base colour by brightest
/// channel 20..245 in 99% or more of texels; normals unit within 0.03 (8-bit) in 99.9%; no output of one value; no seam
/// across the wrap.
/// </summary>
[Trait("Tier", "unit")]
public class NestedSamplesTests(ITestOutputHelper output)
{
    private const int Size = 256;

    private static readonly Dictionary<string, Lazy<GraphEvaluator>> Evaluators = new()
    {
        ["grime"] = new(() => new GraphEvaluator(Load("grime"))),
        ["old-wall"] = new(() => new GraphEvaluator(Load("old-wall"))),
    };

    private static TextureGraph Load(string name)
    {
        var read = TexGraphFile.ReadFile(Path.Combine(Samples.Folder, name + ".texgraph"));
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static byte[] Committed(string name, string file)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(Samples.Folder, name, file)), ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((Size, Size), (image.Width, image.Height));
        return image.Data;
    }

    [Fact]
    public void The_outputs_are_the_bricks_own()
    {
        Assert.Equal(["base_colour"], Evaluators["grime"].Value.Outputs);
        Assert.Equal(["base_colour", "normal", "roughness", "occlusion"], Evaluators["old-wall"].Value.Outputs);
        Assert.Equal(
            [Path.Combine(Samples.Folder, "grime.texgraph"), Path.Combine(Samples.Folder, "bricks.texgraph")], // dirt, then wall
            GraphValidator.Uses(Load("old-wall")).SelectMany(u => u.Files));
    }

    [Theory]
    [InlineData("grime", "base_colour")]
    [InlineData("old-wall", "base_colour")]
    [InlineData("old-wall", "normal")]
    [InlineData("old-wall", "roughness")]
    [InlineData("old-wall", "occlusion")]
    public void The_committed_png_equals_the_graph(string name, string role)
    {
        var made = OutputImage.Make(Evaluators[name].Value, role, Size);
        var committed = File.ReadAllBytes(Path.Combine(Samples.Folder, name, role + ".png"));
        Assert.True(made.Png.AsSpan().SequenceEqual(committed), $"{name}/{role}.png differs from the graph; run tools/Patina.SampleGraphs");
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("roughness")]
    [InlineData("occlusion")]
    public void Old_wall_is_the_bricks_at_mortar_0_06_with_grime_on_the_base_colour_only(string role)
    {
        var bricks = GraphParameters.WithOverrides(Load("bricks"), new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(0.06) }).Graph!;
        Assert.Equal(OutputImage.Rgba8(new GraphEvaluator(bricks), role, Size), OutputImage.Rgba8(Evaluators["old-wall"].Value, role, Size));
    }

    [Fact]
    public void The_grime_darkens_and_desaturates_the_bricks()
    {
        var bricks = GraphParameters.WithOverrides(Load("bricks"), new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(0.06) }).Graph!;
        var clean = OutputImage.Rgba8(new GraphEvaluator(bricks), "base_colour", Size);
        var dirty = Committed("old-wall", "base_colour.png");
        static (double Light, double Chroma) Mean(byte[] px)
        {
            double light = 0, chroma = 0;
            for (var i = 0; i < px.Length; i += 4)
            {
                light += (px[i] + px[i + 1] + px[i + 2]) / 3.0;
                chroma += Math.Max(px[i], Math.Max(px[i + 1], px[i + 2])) - Math.Min(px[i], Math.Min(px[i + 1], px[i + 2]));
            }
            return (light / (px.Length / 4), chroma / (px.Length / 4));
        }
        var (cl, cc) = Mean(clean);
        var (dl, dc) = Mean(dirty);
        output.WriteLine($"old-wall base_colour: mean level {dl:0.0} (clean bricks {cl:0.0}), mean chroma {dc:0.0} (clean {cc:0.0})");
        Assert.True(dl < cl - 3, "the grime darkens");
        Assert.True(dc < cc - 3, "the grime desaturates");
    }

    [Theory]
    [InlineData("grime")]
    [InlineData("old-wall")]
    public void The_base_colour_is_in_range(string name)
    {
        var px = Committed(name, "base_colour.png");
        int outside = 0, min = 255, max = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            var brightest = Math.Max(px[i], Math.Max(px[i + 1], px[i + 2]));
            min = Math.Min(min, brightest); max = Math.Max(max, brightest);
            if (brightest < 20 || brightest > 245) outside++;
        }
        output.WriteLine($"{name} base_colour: brightest channel {min}..{max}, {outside} of {px.Length / 4} outside 20..245 (a fault over 1%)");
        Assert.True(outside <= px.Length / 4 / 100, $"{outside} texels outside 20..245");
    }

    [Fact]
    public void The_normals_are_unit_length()
    {
        var px = Committed("old-wall", "normal.png");
        int off = 0; double worst = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            double x = px[i] / 255.0 * 2 - 1, y = px[i + 1] / 255.0 * 2 - 1, z = px[i + 2] / 255.0 * 2 - 1;
            var error = Math.Abs(Math.Sqrt(x * x + y * y + z * z) - 1);
            worst = Math.Max(worst, error);
            if (error > 0.03) off++;
        }
        output.WriteLine($"old-wall normal: worst |length - 1| {worst:0.0000}, {off} texels over 0.03 (a fault over 0.1%)");
        Assert.True(off <= px.Length / 4 / 1000, $"{off} texels off unit length");
    }

    [Theory]
    [InlineData("grime", "base_colour.png")]
    [InlineData("old-wall", "base_colour.png")]
    [InlineData("old-wall", "normal.png")]
    [InlineData("old-wall", "roughness.png")]
    [InlineData("old-wall", "occlusion.png")]
    public void No_output_is_one_value_and_no_seam_shows_across_the_wrap(string name, string file)
    {
        var px = Committed(name, file);
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
        output.WriteLine($"{name}/{file}: {values.Count} distinct values; step across u wrap {wrapX:0.00} (interior {interiorX:0.00}), across v wrap {wrapY:0.00} (interior {interiorY:0.00})");
        Assert.True(wrapX <= 2 * interiorX + 2, "a seam across the u wrap");
        Assert.True(wrapY <= 2 * interiorY + 2, "a seam across the v wrap");
    }
}
