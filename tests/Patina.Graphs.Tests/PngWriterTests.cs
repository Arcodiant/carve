using System.IO.Compression;
using System.Security.Cryptography;
using Patina.Graphs.Imaging;
using StbImageSharp;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// T-187: <see cref="PngWriter"/>'s PNGs decode to their texels, are sound where <c>StbImageSharp</c> does not look
/// (the CRCs, the zlib stream and its Adler-32), give the same bytes however they are written and at any thread count,
/// and are pinned to this runtime's zlib by a canary. A table of broken copies shows each check failing.
/// </summary>
[Trait("Tier", "unit")]
public class PngWriterTests
{
    /// <summary>Texels from integers: smooth gradients with some structure, so the bytes compress.</summary>
    private static byte[] Smooth(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                rgba[i] = (byte)(x * 3 + y);
                rgba[i + 1] = (byte)(y * 2 - x);
                rgba[i + 2] = (byte)((x ^ y) & 0xF0);
                rgba[i + 3] = (byte)(255 - ((x + y) >> 2));
            }
        return rgba;
    }

    /// <summary>Hash noise, which does not compress.</summary>
    private static byte[] Noise(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i++)
        {
            var h = (uint)i * 0x9E3779B1u;
            h ^= h >> 15; h *= 0x85EBCA77u; h ^= h >> 13; h *= 0xC2B2AE3Du; h ^= h >> 16;
            rgba[i] = (byte)h;
        }
        return rgba;
    }

    public static TheoryData<string, int, int> Images => new()
    {
        { "smooth", 1, 1 },
        { "smooth", 1, 300 },
        { "smooth", 300, 1 },
        { "smooth", 64, 63 },
        { "smooth", 64, 64 },
        { "smooth", 64, 65 },
        { "smooth", 97, 211 },
        { "zeros", 50, 130 },
        { "noise", 70, 140 },
        { "smooth", 5, 2100 }, // more than 2048 rows: two IDAT chunks
    };

    private static byte[] Image(string kind, int width, int height) => kind switch
    {
        "smooth" => Smooth(width, height),
        "zeros" => new byte[width * height * 4],
        "noise" => Noise(width, height),
        _ => throw new ArgumentException(kind),
    };

    [Theory]
    [MemberData(nameof(Images))]
    public void DecodesToItsTexels(string kind, int width, int height)
    {
        var rgba = Image(kind, width, height);
        CheckDecodes(PngWriter.Encode(rgba, width, height), rgba, width, height);
    }

    [Theory]
    [MemberData(nameof(Images))]
    public void TheStreamIsSound(string kind, int width, int height)
    {
        var rgba = Image(kind, width, height);
        CheckSound(PngWriter.Encode(rgba, width, height), rgba, width, height);
    }

    /// <summary>
    /// RV-80: a band of a 1024-wide image is 64 x 4097 = 262,208 filtered bytes, past the Adler-32's modulus 65521, so the
    /// combine's reduction of the band's length is reached (the images above have bands of at most 24,896 bytes).
    /// <see cref="ZLibStream"/> checks the combined Adler-32 when it inflates the stream to the filtered rows.
    /// </summary>
    [Fact]
    public void TheAdlerCombineHoldsBandsLongerThanItsModulus()
    {
        const int width = 1024, height = 130; // three bands, the last of two rows
        var rgba = Smooth(width, height);
        CheckSound(PngWriter.Encode(rgba, width, height), rgba, width, height);
    }

    [Fact]
    public void AnImageOfMoreThan2048RowsIsTwoIdatChunks()
    {
        var png = PngWriter.Encode(Smooth(5, 2100), 5, 2100);
        Assert.Equal(["IHDR", "IDAT", "IDAT", "IEND"], Chunks(png).Select(c => c.Type).ToArray());
    }

    [Fact]
    public void GivesTheSameBytesFromEncodeAFileStreamTwoCallsAndAnyThreadCount()
    {
        const int width = 40, height = 4200; // 66 bands, three batches
        var rgba = Smooth(width, height);
        var encoded = PngWriter.Encode(rgba, width, height);

        var path = Path.Combine(Path.GetTempPath(), $"patina-pngwriter-{Guid.NewGuid():N}.png");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew))
                PngWriter.Write(file, rgba, width, height);
            Assert.Equal(encoded, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }

        Assert.Equal(encoded, PngWriter.Encode(rgba, width, height));
        foreach (var threads in new[] { 1, 2, 5, 16, Math.Max(2, Environment.ProcessorCount) })
        {
            using var stream = new MemoryStream();
            PngWriter.Write(stream, rgba, width, height, threads);
            Assert.True(encoded.AsSpan().SequenceEqual(stream.ToArray()), $"the bytes at {threads} threads differ");
        }
    }

    [Theory]
    [InlineData(2, 2, 15)]
    [InlineData(2, 2, 17)]
    [InlineData(3, 1, 8)]
    public void RefusesALengthThatIsNotFourBytesATexel(int width, int height, int length)
    {
        Assert.ThrowsAny<ArgumentException>(() => PngWriter.Encode(new byte[length], width, height));
    }

    /// <summary>
    /// RV-80: 262,144 x 2047 is 2047 MiB of RGBA, but a batch's copy with the row above it is 2^31 bytes, so it is
    /// refused, naming the limit, before a byte is written. The size is checked before the length, so no image of that
    /// size is needed. One row fewer fits, and goes on to the length check.
    /// </summary>
    [Fact]
    public void RefusesASizeItCannotEncodeBeforeWritingAByte()
    {
        using var stream = new MemoryStream();
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => PngWriter.Write(stream, [], 262_144, 2047));
        Assert.Contains("more than an array holds", refused.Message);
        Assert.Equal(0, stream.Length);

        var length = Assert.Throws<ArgumentException>(() => PngWriter.Write(stream, [], 262_144, 2046));
        Assert.Contains("four bytes a texel", length.Message);
        Assert.Equal(0, stream.Length);
    }

    /// <summary>The SHA-256 of <see cref="CanaryImage"/>'s PNG on .NET 10.0.15 (T-187).</summary>
    private const string CanaryHash = "d0024f48b23b60430272f726cb94afb7cdf80bc443630555ab3859e912380dc4";

    private static byte[] CanaryImage() => Smooth(96, 200);

    [Fact]
    public void TheCanaryHoldsThisRuntimesZlibToTheCommittedBytes()
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(PngWriter.Encode(CanaryImage(), 96, 200)));
        Assert.True(hash == CanaryHash,
            $"This runtime's zlib (.NET {Environment.Version}) writes PNG bytes other than those of the committed sample PNGs: " +
            $"the canary's SHA-256 is {hash}, not {CanaryHash}. Do not pin the hash again: stop and ask the person (M-33).");
    }

    /// <summary>
    /// Each break, the check that catches it, and how that check fails (RV-80): the exception's type and a fragment of its
    /// message. <c>TrueException</c> is a named assertion in the check; <c>InvalidDataException</c> is thrown only by
    /// <see cref="ZLibStream"/> inflating the joined <c>IDAT</c> data, which checks the Adler-32 (its message does not
    /// say so, so no fragment is asserted).
    /// </summary>
    public static TheoryData<string, string, string, string> BrokenCopies => new()
    {
        { "FilterByteNone", "decodes", "TrueException", "the PNG does not decode to its texels" },
        { "BandStartAgainstZeros", "decodes", "TrueException", "the PNG does not decode to its texels" },
        { "WrongAdlerCombine", "sound", "InvalidDataException", "" },
        { "NoFinalBlock", "sound", "TrueException", "does not end in an empty final block" },
        { "WrongCrc", "sound", "TrueException", "the IHDR chunk's CRC is wrong" },
    };

    [Theory]
    [MemberData(nameof(BrokenCopies))]
    public void EachBrokenCopyFailsACheck(string fault, string check, string exception, string message)
    {
        const int width = 64, height = 130; // three bands
        var rgba = Smooth(width, height);
        Action<byte[]> run = check switch
        {
            "decodes" => png => CheckDecodes(png, rgba, width, height),
            "sound" => png => CheckSound(png, rgba, width, height),
            _ => throw new ArgumentException(check),
        };

        run(Encode(rgba, width, height, PngWriterFault.None));
        var broken = Encode(rgba, width, height, Enum.Parse<PngWriterFault>(fault));
        var thrown = Record.Exception(() => run(broken));
        Assert.NotNull(thrown);
        Assert.True(thrown.GetType().Name == exception, $"{fault} failed {check} with {thrown.GetType().Name}, not {exception}: {thrown.Message}");
        Assert.Contains(message, thrown.Message);
    }

    private static byte[] Encode(byte[] rgba, int width, int height, PngWriterFault fault)
    {
        using var stream = new MemoryStream();
        PngWriter.Write(stream, rgba, width, height, -1, fault);
        return stream.ToArray();
    }

    private static void CheckDecodes(byte[] png, byte[] rgba, int width, int height)
    {
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal((width, height), (image.Width, image.Height));
        Assert.True(rgba.AsSpan().SequenceEqual(image.Data), "the PNG does not decode to its texels");
    }

    /// <summary>
    /// The signature, the chunks in order with a CRC-32 worked here, the header, one <c>IDAT</c> a batch, and the joined
    /// <c>IDAT</c> data a zlib stream (inflated by <see cref="ZLibStream"/>, which checks the Adler-32) of the filtered rows.
    /// </summary>
    private static void CheckSound(byte[] png, byte[] rgba, int width, int height)
    {
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        var chunks = Chunks(png);
        foreach (var c in chunks)
            Assert.True(c.Crc == BitwiseCrc32([.. c.TypeBytes, .. c.Data]), $"the {c.Type} chunk's CRC is wrong");

        var batches = ((height + 63) / 64 + 31) / 32;
        Assert.Equal(["IHDR", .. Enumerable.Repeat("IDAT", batches), "IEND"], chunks.Select(c => c.Type).ToArray());
        Assert.Equal(new byte[] { 0, 0, (byte)(width >> 8), (byte)width, 0, 0, (byte)(height >> 8), (byte)height, 8, 6, 0, 0, 0 },
            chunks[0].Data); // the tests' sizes are under 65536
        Assert.Empty(chunks[^1].Data);

        var zlib = chunks.Where(c => c.Type == "IDAT").SelectMany(c => c.Data).ToArray();
        Assert.Equal(new byte[] { 0x78, 0x01 }, zlib[..2]);
        Assert.True(zlib[^6] == 0x03 && zlib[^5] == 0x00, "the zlib stream does not end in an empty final block and the Adler-32");

        using var inflate = new ZLibStream(new MemoryStream(zlib), CompressionMode.Decompress);
        using var rows = new MemoryStream();
        inflate.CopyTo(rows);
        Assert.True(Filtered(rgba, width, height).AsSpan().SequenceEqual(rows.ToArray()), "the zlib stream is not the filtered rows");
    }

    /// <summary>Every row filtered <c>up</c> against the row above it, row 0 against zeros.</summary>
    private static byte[] Filtered(byte[] rgba, int width, int height)
    {
        var stride = width * 4;
        var rows = new byte[height * (stride + 1)];
        for (var y = 0; y < height; y++)
        {
            rows[y * (stride + 1)] = 2;
            for (var i = 0; i < stride; i++)
                rows[y * (stride + 1) + 1 + i] = (byte)(rgba[y * stride + i] - (y == 0 ? 0 : rgba[(y - 1) * stride + i]));
        }
        return rows;
    }

    private sealed record Chunk(string Type, byte[] TypeBytes, byte[] Data, uint Crc);

    private static List<Chunk> Chunks(byte[] png)
    {
        var chunks = new List<Chunk>();
        var at = 8;
        while (at < png.Length)
        {
            var length = (int)BigEndian(png, at);
            var type = png[(at + 4)..(at + 8)];
            var data = png[(at + 8)..(at + 8 + length)];
            chunks.Add(new Chunk(System.Text.Encoding.ASCII.GetString(type), type, data, BigEndian(png, at + 8 + length)));
            at += 12 + length;
        }
        Assert.Equal(png.Length, at);
        return chunks;
    }

    private static uint BigEndian(byte[] b, int at) => (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);

    /// <summary>CRC-32 a bit at a time, apart from the writer's table.</summary>
    private static uint BitwiseCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}
