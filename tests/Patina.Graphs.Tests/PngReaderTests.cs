using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>PNGs made by hand for the reader's tests (T-240): any colour type, bit depth and interlace flag, each row under a filter chosen for it.</summary>
internal static class TestPng
{
    public static int Channels(int colourType) => colourType switch { 0 or 3 => 1, 2 => 3, 4 => 2, _ => 4 };

    public static byte[] Chunk(string type, ReadOnlySpan<byte> data)
    {
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
        Encoding.ASCII.GetBytes(type, chunk.AsSpan(4));
        data.CopyTo(chunk.AsSpan(8));
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), PngWriter.Crc32(chunk.AsSpan(4, 4 + data.Length)));
        return chunk;
    }

    /// <summary>
    /// A PNG of <paramref name="raw"/> (the unfiltered rows, <c>width * channels * depth / 8</c> bytes each), row y filtered
    /// by <paramref name="filter"/>(y): 0 to 4 as PNG's filters, any other value written as that filter byte over the row unfiltered.
    /// </summary>
    public static byte[] Make(int width, int height, int colourType, byte[] raw, Func<int, int> filter, int depth = 8, int interlace = 0, params byte[][] extraChunks)
    {
        var bpp = Math.Max(1, Channels(colourType) * depth / 8);
        var stride = width * Channels(colourType) * depth / 8;
        var filtered = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            var f = filter(y);
            filtered.WriteByte((byte)f);
            for (var i = 0; i < stride; i++)
            {
                int Raw(int yy, int ii) => yy < 0 || ii < 0 ? 0 : raw[yy * stride + ii];
                int a = Raw(y, i - bpp), b = Raw(y - 1, i), c = Raw(y - 1, i - bpp);
                var predictor = f switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                };
                filtered.WriteByte((byte)(Raw(y, i) - predictor));
            }
        }
        var zlib = new MemoryStream();
        using (var deflate = new ZLibStream(zlib, CompressionLevel.Optimal, leaveOpen: true)) filtered.WriteTo(deflate);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)height);
        (ihdr[8], ihdr[9], ihdr[12]) = ((byte)depth, (byte)colourType, (byte)interlace);
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Chunk("IHDR", ihdr), .. extraChunks.SelectMany(c => c), .. Chunk("IDAT", zlib.ToArray()), .. Chunk("IEND", [])];
    }

    // Paeth written out from the PNG specification, apart from the reader's.
    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    /// <summary>Bytes that vary in every position, by a hash, so a byte read from the wrong place shows.</summary>
    public static byte[] Bytes(int count, uint seed = 1)
    {
        var bytes = new byte[count];
        for (var i = 0; i < count; i++) bytes[i] = (byte)(TestNodes.Hash(i, (int)seed, 99) >> 8);
        return bytes;
    }
}

/// <summary>
/// T-240's PNG reader: every byte of what <see cref="PngWriter"/> writes, hand-made PNGs of each colour type under each
/// row filter (held to StbImageSharp's decode too), and each refusal saying why.
/// </summary>
[Trait("Tier", "unit")]
public class PngReaderTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 3)]
    [InlineData(256, 256)]
    [InlineData(3, 2100)] // more than 2048 rows: two IDAT chunks
    public void Decodes_every_byte_of_what_PngWriter_writes(int width, int height)
    {
        var rgba = TestPng.Bytes(width * height * 4);
        var image = PngReader.Decode(PngWriter.Encode(rgba, width, height));
        Assert.Equal((width, height), (image.Width, image.Height));
        Assert.True(rgba.AsSpan().SequenceEqual(image.Rgba), "the PNG does not decode to its texels");
    }

    public static TheoryData<int, int> ColourTypesAndFilters()
    {
        var data = new TheoryData<int, int>();
        foreach (var colourType in new[] { 0, 2, 4, 6 })
            for (var filter = 0; filter <= 5; filter++) data.Add(colourType, filter); // 5: each row its own filter, y % 5
        return data;
    }

    [Theory]
    [MemberData(nameof(ColourTypesAndFilters))]
    public void Reads_each_colour_type_under_each_filter(int colourType, int filter)
    {
        const int Width = 7, Height = 6;
        var channels = TestPng.Channels(colourType);
        var raw = TestPng.Bytes(Width * Height * channels, (uint)(colourType * 10 + filter));
        var png = TestPng.Make(Width, Height, colourType, raw, y => filter == 5 ? y % 5 : filter);

        var expected = new byte[Width * Height * 4];
        for (var t = 0; t < Width * Height; t++)
        {
            var s = raw.AsSpan(t * channels, channels);
            (expected[t * 4], expected[t * 4 + 1], expected[t * 4 + 2], expected[t * 4 + 3]) = channels switch
            {
                1 => (s[0], s[0], s[0], (byte)255),
                2 => (s[0], s[0], s[0], s[1]),
                3 => (s[0], s[1], s[2], (byte)255),
                _ => (s[0], s[1], s[2], s[3]),
            };
        }
        var image = PngReader.Decode(png);
        Assert.Equal((Width, Height), (image.Width, image.Height));
        Assert.Equal(expected, image.Rgba);
        Assert.Equal(ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha).Data, image.Rgba); // the hand-made PNG is one another reader reads alike
    }

    [Fact]
    public void Skips_an_ancillary_chunk()
    {
        var raw = TestPng.Bytes(4 * 4 * 3);
        var png = TestPng.Make(4, 4, 2, raw, _ => 1, extraChunks: TestPng.Chunk("tEXt", "Software\0test"u8));
        Assert.Equal(raw[..3], PngReader.Decode(png).Rgba[..3]);
    }

    private static byte[] Rgb(int depth = 8, int colourType = 2, int interlace = 0, Func<int, int>? filter = null) =>
        TestPng.Make(4, 4, colourType, new byte[4 * 4 * TestPng.Channels(colourType) * depth / 8], filter ?? (_ => 0), depth, interlace);

    public static TheoryData<string, byte[], string> Refused()
    {
        var good = Rgb();
        var badCrc = (byte[])good.Clone();
        badCrc[^20] ^= 1; // inside the IDAT chunk's data
        var idat = TestPng.Chunk("IDAT", [1, 2, 3, 4, 5, 6]);
        byte[] garbage = [.. good[..33], .. idat, .. TestPng.Chunk("IEND", [])];
        return new()
        {
            { "16-bit", Rgb(depth: 16), "it is a 16-bit PNG; it reads 8-bit greyscale, grey + alpha, RGB and RGBA PNGs, not interlaced" },
            { "palette", Rgb(colourType: 3), "it is a palette PNG; it reads 8-bit greyscale" },
            { "interlaced", Rgb(interlace: 1), "it is an interlaced PNG; it reads 8-bit greyscale" },
            { "4-bit grey", TestPng.Make(4, 4, 0, new byte[8], _ => 0, depth: 4), "it is a 4-bit PNG; it reads 8-bit greyscale" },
            { "not a PNG", "{ \"version\": 1 }"u8.ToArray(), "it is not a PNG (no PNG signature)" },
            { "cut short", good[..^20], "the file is cut short" },
            { "a wrong CRC", badCrc, "its IDAT chunk's CRC is wrong (the file is damaged)" },
            { "image data that does not inflate", garbage, "its image data does not inflate (the file is damaged)" },
            { "image data too short", [.. good[..33], .. TestPng.Chunk("IDAT", ZLib(new byte[13])), .. TestPng.Chunk("IEND", [])], "its image data ends at row 1 of 4 (the file is damaged)" },
            { "a filter PNG does not have", Rgb(filter: y => y == 2 ? 7 : 0), "its row 2 has filter type 7, which PNG does not have" },
            { "too large", TestPng.Make(8193, 1, 0, new byte[8193], _ => 0), "it is 8193x1, larger than 8192 on a side" },
            { "a wrong Adler-32 (the CRC right)", [.. good[..33], .. TestPng.Chunk("IDAT", BadAdler(ZLib(new byte[4 * 13]))), .. TestPng.Chunk("IEND", [])], "its image data does not inflate (the file is damaged)" },
            { "an unknown critical chunk", [.. good[..33], .. TestPng.Chunk("ZZZZ", [1]), .. good[33..]], "it has a critical chunk ZZZZ that PNG readers must understand, and this one does not" },
        };
    }

    private static byte[] BadAdler(byte[] zlib)
    {
        zlib[^1] ^= 1;
        return zlib;
    }

    private static byte[] ZLib(byte[] data)
    {
        var zlib = new MemoryStream();
        using (var deflate = new ZLibStream(zlib, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(data);
        return zlib.ToArray();
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void Refuses_saying_why(string what, byte[] png, string message)
    {
        var e = Assert.Throws<PngReadException>(() => PngReader.Decode(png));
        Assert.True(e.Message.Contains(message, StringComparison.Ordinal), $"{what}: {e.Message}");
    }
}
