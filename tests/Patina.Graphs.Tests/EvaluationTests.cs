using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

[Trait("Tier", "unit")]
public class EvaluationTests
{
    private static TextureGraph Graph(string nodes, int size = 64, uint seed = 7)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": {{seed}}, "nodes": { {{nodes}} } }""", TestNodes.Registry);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static readonly TextureGraph Blurred = Graph("""
        "h": { "type": "hash", "params": { "offset": 5 } },
        "b1": { "type": "blur", "inputs": { "in": "h" } },
        "b2": { "type": "blur", "inputs": { "in": "b1" } },
        "o": { "type": "output", "inputs": { "in": "b2" }, "params": { "role": "occlusion" } },
        "r": { "type": "output", "inputs": { "in": "b1" }, "params": { "role": "roughness" } }
        """);

    private static byte[] Rgba(TextureGraph g, OutputRole role, int size, int? tileSize = null, long? cacheBytes = null) =>
        OutputImage.Rgba8(new GraphEvaluator(g, TestNodes.Registry), role, size, tileSize, cacheBytes);

    [Fact]
    public void AColourToBaseColourGivesExactlyThatColour()
    {
        var g = TexGraphFile.Read("""{ "version": 1, "size": 32, "seed": 7, "nodes": { "wood": { "type": "colour", "params": { "colour": "#aa7040" } }, "out": { "type": "output", "inputs": { "in": "wood" }, "params": { "role": "base_colour" } } } }""").Graph!;
        var image = OutputImage.Make(new GraphEvaluator(g), OutputRole.BaseColour, 32);
        for (var i = 0; i < image.Rgba.Length; i += 4)
            Assert.Equal((0xaa, 0x70, 0x40, 255), (image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]));
        var decoded = ImageResult.FromMemory(image.Png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((32, 32), (decoded.Width, decoded.Height));
        Assert.Equal(image.Rgba, decoded.Data);
    }

    [Fact]
    public void EachRolePacksAsGltfTakesIt()
    {
        byte[] Pack(OutputRole role, PortKind kind, params float[] texel)
        {
            var tile = new Tile(kind, new TexelRect(0, 0, 1, 1));
            texel.CopyTo(tile.Data, 0);
            var rgba = new byte[4];
            OutputPacking.PackRow(role, tile, 0, rgba);
            return rgba;
        }

        Assert.Equal([170, 112, 64, 255], Pack(OutputRole.BaseColour, PortKind.Colour, 170 / 255f, 112 / 255f, 64 / 255f));
        Assert.Equal([128, 128, 128, 255], Pack(OutputRole.BaseColour, PortKind.Grey, 0.5f)); // 127.5 rounds away from zero
        Assert.Equal([128, 128, 255, 255], Pack(OutputRole.Normal, PortKind.Colour, 0, 0, 1)); // flat: 127.5 + 127.5 n
        Assert.Equal([0, 255, 128, 255], Pack(OutputRole.Normal, PortKind.Colour, -1, 1, 0));
        Assert.Equal([255, 128, 0, 255], Pack(OutputRole.Roughness, PortKind.Grey, 0.5f)); // R 255, G roughness, B metallic 0
        Assert.Equal([64, 64, 64, 255], Pack(OutputRole.Occlusion, PortKind.Grey, 0.25f)); // 63.75 in R, G and B
        Assert.Equal([0, 0, 0, 255], Pack(OutputRole.Occlusion, PortKind.Grey, -3f)); // clamped
        Assert.Equal([255, 255, 255, 255], Pack(OutputRole.BaseColour, PortKind.Colour, 2, 2, 2));
    }

    [Fact]
    public void TheSameGraphGivesTheSameBytesTwiceAndAtAnyTileSizeAndCap()
    {
        var once = Rgba(Blurred, OutputRole.Occlusion, 64);
        Assert.Equal(once, Rgba(Blurred, OutputRole.Occlusion, 64));
        Assert.Equal(once, Rgba(Blurred, OutputRole.Occlusion, 64, tileSize: 8));
        Assert.Equal(once, Rgba(Blurred, OutputRole.Occlusion, 64, tileSize: 16, cacheBytes: 0));
        var png = OutputImage.Make(new GraphEvaluator(Blurred, TestNodes.Registry), OutputRole.Roughness, 64).Png;
        Assert.Equal(png, OutputImage.Make(new GraphEvaluator(Blurred, TestNodes.Registry), OutputRole.Roughness, 64, tileSize: 4).Png);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void ARadiusTwoNodeWrapsAtTileEdgesAsTheWholeImageDoes(int tileSize)
    {
        // Worked here directly, with wrap-around, from the hash's texels: one blur (roughness) and two (occlusion).
        const int n = 64;
        float H(int x, int y) => TestNodes.HashValue(Tile.Wrap(x, n), Tile.Wrap(y, n), 7, 5);
        float Box(Func<int, int, float> f, int x, int y)
        {
            float s = 0;
            for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++) s += f(x + dx, y + dy);
            return s / 25f;
        }
        float B1(int x, int y) => Box(H, x, y);

        var roughness = Rgba(Blurred, OutputRole.Roughness, n, tileSize);
        var occlusion = Rgba(Blurred, OutputRole.Occlusion, n, tileSize, cacheBytes: 0);
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var i = (y * n + x) * 4;
                Assert.Equal(OutputPacking.Byte(255f * B1(x, y)), roughness[i + 1]);
                Assert.Equal(OutputPacking.Byte(255f * Box(B1, x, y)), occlusion[i]);
            }
    }

    [Fact]
    public void ANodeRegisteredFromOutsideEvaluatesAndTheBuiltInsDoNotKnowIt()
    {
        var text = TexGraphFile.Write(Blurred);
        Assert.Contains(TexGraphFile.Read(text).Faults, f => f.Node == "h" && f.Message.Contains("unknown type 'hash'"));
        var rgba = Rgba(Blurred, OutputRole.Roughness, 32);
        Assert.Contains(rgba.Where((_, i) => i % 4 == 1), g => g != rgba[1]); // not a flat image
    }

    [Fact]
    public void ANamedOutputPortIsEvaluated()
    {
        var g = Graph("""
            "paint": { "type": "colour", "params": { "colour": "#aa7040" } },
            "parts": { "type": "split", "inputs": { "in": "paint" } },
            "o": { "type": "output", "inputs": { "in": "parts.g" }, "params": { "role": "occlusion" } }
            """);
        Assert.Equal([0x70, 0x70, 0x70, 255], Rgba(g, OutputRole.Occlusion, 32)[..4]);
    }

    [Fact]
    public void TilesAreBoundedAndTheCacheStaysUnderItsCap()
    {
        Assert.Equal(128, GraphEvaluator.DefaultTileSize);
        Assert.Equal(64L << 20, GraphEvaluator.DefaultCacheBytes);

        // At 2048 a pointwise graph is made in 16 x 16 canonical tiles of 128, top row first, left to right. Nothing is
        // read twice, but 256 greyscale tiles (16 MiB) fit under the default cap, so all are held to the end.
        var flat = new GraphEvaluator(Graph("""
            "h": { "type": "hash" },
            "o": { "type": "output", "inputs": { "in": "h" }, "params": { "role": "occlusion" } }
            """), TestNodes.Registry);
        var seen = new List<TexelRect>();
        flat.Evaluate(OutputRole.Occlusion, 2048, tile => seen.Add(tile.Rect));
        Assert.Equal([.. Enumerable.Range(0, 256).Select(i => new TexelRect(i % 16 * 128, i / 16 * 128, 128, 128))], seen);
        const long grey128 = 128 * 128 * 4;
        Assert.Equal(new EvaluationRun(128, 256, 256 * grey128, 64L << 20, 256), flat.LastRun);
        // With no cache, one tile is held at a time.
        flat.Evaluate(OutputRole.Occlusion, 2048, _ => { }, cacheBytes: 0);
        Assert.Equal(new EvaluationRun(128, 1, grey128, 0, 256), flat.LastRun);
        // A tile size past the image is the image.
        flat.Evaluate(OutputRole.Occlusion, 32, _ => { }, tileSize: 256);
        Assert.Equal(new EvaluationRun(32, 1, 32 * 32 * 4, 64L << 20, 1), flat.LastRun);

        // h feeds b1 (radius 2), which feeds b2 (radius 2). At 256 in tiles of 32 (8 x 8), one output tile needs b2's
        // tile, b1's 3 x 3 around it and h's 5 x 5. Under the default cap each of the 3 x 64 node tiles is made once and
        // all are held; with no cache each output tile makes its 35 again.
        const long grey32 = 32 * 32 * 4;
        var blurred = new GraphEvaluator(Blurred, TestNodes.Registry);
        blurred.Evaluate(OutputRole.Occlusion, 256, _ => { }, tileSize: 32);
        Assert.Equal(new EvaluationRun(32, 192, 192 * grey32, 64L << 20, 192), blurred.LastRun);
        blurred.Evaluate(OutputRole.Occlusion, 256, _ => { }, tileSize: 32, cacheBytes: 0);
        Assert.Equal(new EvaluationRun(32, 35, 35 * grey32, 0, 64 * 35), blurred.LastRun);
    }

    [Fact]
    public void AFaultyGraphIsRefusedAndASizeOutsideTheRangeTooWithTheRange()
    {
        var faulty = TexGraphFile.Read("""{ "version": 1, "size": 32, "seed": 7, "nodes": { "a": { "type": "colour" } } }""").Graph!;
        Assert.Throws<GraphFaultException>(() => new GraphEvaluator(faulty));
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => Rgba(Blurred, OutputRole.Occlusion, 100));
        Assert.Contains("power of two from 32 to 8192", e.Message);
        Assert.Throws<ArgumentException>(() => Rgba(Blurred, OutputRole.Normal, 32));
        Assert.Equal([OutputRole.Roughness, OutputRole.Occlusion], new GraphEvaluator(Blurred, TestNodes.Registry).Roles);
    }
}
