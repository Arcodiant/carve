using System.Buffers.Binary;
using System.IO.Compression;

namespace Patina.Graphs.Imaging;

/// <summary>A PNG <see cref="PngReader"/> does not read: not a PNG, damaged, or of a kind it refuses (16-bit, palette, interlaced). The message says which.</summary>
public sealed class PngReadException(string message) : Exception(message);

/// <summary>A decoded PNG (T-240): its size and RGBA8 texels, rows top to bottom; grey is R = G = B, and alpha 255 where the PNG has none.</summary>
public sealed record PngImage(int Width, int Height, byte[] Rgba);

/// <summary>
/// The PNG reader (T-240), engine-free on .NET's zlib: 8-bit greyscale, grey + alpha, RGB and RGBA, not interlaced,
/// every row filter. 16-bit, palette and interlaced images, and the 1-, 2- and 4-bit greys, are refused with a message
/// saying so; a file that is not a PNG or is damaged (a chunk's CRC, a missing chunk, image data that does not inflate
/// to its rows) is refused saying what is wrong. Ancillary chunks are skipped; an unknown critical chunk is refused.
/// </summary>
public static class PngReader
{
    /// <summary>The longest side read: the largest graph's, so a decode holds at most 256 MiB of texels.</summary>
    public const int MaxSide = TextureGraph.MaxSize;

    /// <summary>What it reads, for a refusal.</summary>
    public const string Reads = "it reads 8-bit greyscale, grey + alpha, RGB and RGBA PNGs, not interlaced";

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static PngImage Decode(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature)) throw new PngReadException("it is not a PNG (no PNG signature)");
        var at = 8;
        int width = 0, height = 0, channels = 0;
        var idat = new MemoryStream();
        var sawHeader = false;
        while (true)
        {
            if (at + 12 > png.Length) throw new PngReadException("it ends before its IEND chunk (the file is cut short)");
            var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at));
            if (length > png.Length - at - 12) throw new PngReadException("a chunk runs past the end of the file (the file is cut short)");
            var type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            var data = png.AsSpan(at + 8, (int)length);
            var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at + 8 + (int)length));
            if (PngWriter.Crc32(png.AsSpan(at + 4, 4 + (int)length)) != crc) throw new PngReadException($"its {type} chunk's CRC is wrong (the file is damaged)");
            at += 12 + (int)length;
            if (!sawHeader && type != "IHDR") throw new PngReadException("its first chunk is not IHDR");
            switch (type)
            {
                case "IHDR":
                    if (sawHeader) throw new PngReadException("it has two IHDR chunks");
                    if (length != 13) throw new PngReadException("its IHDR chunk is not 13 bytes");
                    sawHeader = true;
                    (width, height, channels) = Header(data);
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
                case "IEND":
                    return Rows(idat, width, height, channels);
                case "PLTE":
                    break; // a suggested palette beside RGB; a palette image is refused by its colour type
                default:
                    if (char.IsUpper(type[0])) throw new PngReadException($"it has a critical chunk {type} that PNG readers must understand, and this one does not");
                    break;
            }
        }
    }

    private static (int Width, int Height, int Channels) Header(ReadOnlySpan<byte> data)
    {
        var width = BinaryPrimitives.ReadUInt32BigEndian(data);
        var height = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        int depth = data[8], colour = data[9];
        if (width == 0 || height == 0) throw new PngReadException("its IHDR gives a width or height of 0");
        if (data[10] != 0 || data[11] != 0) throw new PngReadException("its IHDR names a compression or filter method PNG does not have");
        var channels = colour switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
        if (colour == 3) throw new PngReadException($"it is a palette PNG; {Reads}");
        if (channels == 0) throw new PngReadException($"its IHDR names colour type {colour}, which PNG does not have");
        if (depth == 16) throw new PngReadException($"it is a 16-bit PNG; {Reads}");
        if (depth != 8) throw new PngReadException($"it is a {depth}-bit PNG; {Reads}");
        if (data[12] == 1) throw new PngReadException($"it is an interlaced PNG; {Reads}");
        if (data[12] != 0) throw new PngReadException($"its IHDR names interlace method {data[12]}, which PNG does not have");
        if (width > MaxSide || height > MaxSide) throw new PngReadException($"it is {width}x{height}, larger than {MaxSide} on a side, the largest it reads");
        return ((int)width, (int)height, channels);
    }

    /// <summary>The rows inflated and unfiltered, one at a time, into RGBA8.</summary>
    private static PngImage Rows(MemoryStream idat, int width, int height, int channels)
    {
        if (width == 0) throw new PngReadException("it has no IHDR chunk");
        var stride = width * channels;
        var rgba = new byte[(long)width * height * 4];
        var row = new byte[stride + 1];
        var previous = new byte[stride];
        var current = new byte[stride];
        idat.Position = 0;
        try
        {
            using var inflate = new ZLibStream(idat, CompressionMode.Decompress);
            for (var y = 0; y < height; y++)
            {
                if (inflate.ReadAtLeast(row, row.Length, throwOnEndOfStream: false) < row.Length)
                    throw new PngReadException($"its image data ends at row {y} of {height} (the file is damaged)");
                Unfilter(row[0], row.AsSpan(1), previous, current, channels, y);
                var o = (long)y * width * 4;
                for (var x = 0; x < width; x++, o += 4)
                {
                    var i = x * channels;
                    (rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]) = channels switch
                    {
                        1 => (current[i], current[i], current[i], (byte)255),
                        2 => (current[i], current[i], current[i], current[i + 1]),
                        3 => (current[i], current[i + 1], current[i + 2], (byte)255),
                        _ => (current[i], current[i + 1], current[i + 2], current[i + 3]),
                    };
                }
                (previous, current) = (current, previous);
            }
            inflate.ReadByte(); // to the stream's end, so zlib checks its Adler-32
        }
        catch (InvalidDataException e)
        {
            throw new PngReadException($"its image data does not inflate (the file is damaged): {e.Message}");
        }
        return new(width, height, rgba);
    }

    /// <summary>One row's filter undone (PNG's five: none, sub, up, average, Paeth), a texel <paramref name="bpp"/> bytes.</summary>
    private static void Unfilter(byte filter, ReadOnlySpan<byte> row, ReadOnlySpan<byte> above, Span<byte> out_, int bpp, int y)
    {
        for (var i = 0; i < row.Length; i++)
        {
            int a = i >= bpp ? out_[i - bpp] : 0, b = above[i], c = i >= bpp ? above[i - bpp] : 0;
            out_[i] = (byte)(row[i] + filter switch
            {
                0 => 0,
                1 => a,
                2 => b,
                3 => (a + b) >> 1,
                4 => Paeth(a, b, c),
                _ => throw new PngReadException($"its row {y} has filter type {filter}, which PNG does not have (the file is damaged)"),
            });
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
