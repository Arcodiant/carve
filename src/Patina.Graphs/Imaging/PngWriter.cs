using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;

namespace Patina.Graphs.Imaging;

/// <summary>
/// PNG bytes of an RGBA8 image on .NET's zlib (M-33, T-187). The format is fixed, since every part of it changes the
/// bytes: bit depth 8, colour type 6, no interlace, chunks <c>IHDR</c>, <c>IDAT</c> and <c>IEND</c> only; every row
/// filtered <c>up</c> against the row above it in the image (row 0 against zeros); rows in bands of
/// <see cref="BandRows"/>, each band deflated by a <see cref="DeflateStream"/> of its own at
/// <see cref="CompressionLevel.Optimal"/> with one <c>Write</c> a filtered row and one <c>Flush</c>; the zlib stream is
/// <c>78 01</c>, the bands in order, the empty final block <c>03 00</c> and the Adler-32 of the filtered rows,
/// combined from the bands'. Bands are deflated on the thread pool <see cref="BandsPerBatch"/> at a time and each batch
/// is one <c>IDAT</c> chunk, so the bytes do not depend on the thread count, and the writer holds one batch's
/// compressed bands, never the whole PNG.
/// </summary>
/// <remarks>
/// The bytes depend on the runtime's zlib: they are .NET 10's, held by <c>PngWriterTests</c>' canary. A runtime whose
/// zlib writes other bytes fails there, and the committed sample PNGs would differ.
/// </remarks>
public static class PngWriter
{
    /// <summary>Rows a band: fixed, so the bytes do not depend on the core count.</summary>
    public const int BandRows = 64;

    /// <summary>Bands deflated together and written as one <c>IDAT</c> chunk.</summary>
    public const int BandsPerBatch = 32;

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The PNG of a <paramref name="width"/> x <paramref name="height"/> RGBA8 image: the bytes <see cref="Write"/> puts on any stream.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height)
    {
        using var stream = new MemoryStream();
        Write(stream, rgba, width, height);
        return stream.ToArray();
    }

    /// <summary>
    /// Writes the PNG of a <paramref name="width"/> x <paramref name="height"/> RGBA8 image, rows top to bottom, to
    /// <paramref name="target"/>. <paramref name="maxDegreeOfParallelism"/> caps the threads that deflate a batch
    /// (-1, the default, leaves it to the pool); it never changes the bytes.
    /// </summary>
    public static void Write(Stream target, ReadOnlySpan<byte> rgba, int width, int height, int maxDegreeOfParallelism = -1)
        => Write(target, rgba, width, height, maxDegreeOfParallelism, PngWriterFault.None);

    /// <summary><see cref="Write(Stream, ReadOnlySpan{byte}, int, int, int)"/> with a <paramref name="fault"/> put in, for the tests' table of broken copies.</summary>
    internal static void Write(Stream target, ReadOnlySpan<byte> rgba, int width, int height, int maxDegreeOfParallelism, PngWriterFault fault)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (rgba.Length != (long)width * height * 4) throw new ArgumentException("an RGBA8 image is four bytes a texel", nameof(rgba));
        if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "a thread limit is -1 or at least 1");

        target.Write(Signature);
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..], (uint)height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // colour type: RGBA
        // compression 0, filter method 0, interlace 0
        WriteChunk(target, "IHDR"u8, ihdr, fault);

        var stride = width * 4;
        var rowBytes = stride + 1;
        var bands = (height + BandRows - 1) / BandRows;
        // A batch's rows are copied (the span cannot reach the pool's threads) after the row above them, zeros above
        // row 0; each band is filtered from the copy on the thread that deflates it.
        var raw = GC.AllocateUninitializedArray<byte>(checked((Math.Min(height, BandRows * BandsPerBatch) + 1) * stride));
        var parts = new byte[BandsPerBatch][];
        var adlers = new uint[BandsPerBatch];
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };
        uint adler = 1;
        Span<byte> word = stackalloc byte[4];

        for (var first = 0; first < bands; first += BandsPerBatch)
        {
            var count = Math.Min(BandsPerBatch, bands - first);
            var y0 = first * BandRows;
            var y1 = Math.Min(height, (first + count) * BandRows);
            if (y0 == 0) raw.AsSpan(0, stride).Clear();
            else rgba.Slice((y0 - 1) * stride, stride).CopyTo(raw);
            rgba.Slice(y0 * stride, (y1 - y0) * stride).CopyTo(raw.AsSpan(stride));

            Parallel.For(0, count, options, b =>
            {
                var start = b * BandRows;
                var rows = Math.Min(BandRows, y1 - y0 - start);
                (parts[b], adlers[b]) = DeflateBand(raw, y0, start, rows, stride, fault);
            });

            var isFirst = first == 0;
            var isLast = first + count == bands;
            for (var b = 0; b < count; b++)
            {
                var length = (long)Math.Min(BandRows, y1 - y0 - b * BandRows) * rowBytes;
                adler = AdlerCombine(adler, adlers[b], fault == PngWriterFault.WrongAdlerCombine ? 0 : length);
            }

            long chunkLength = isFirst ? 2 : 0;
            for (var b = 0; b < count; b++) chunkLength += parts[b].Length;
            if (isLast) chunkLength += fault == PngWriterFault.NoFinalBlock ? 4 : 6;

            BinaryPrimitives.WriteUInt32BigEndian(word, checked((uint)chunkLength));
            target.Write(word);
            target.Write("IDAT"u8);
            var crc = fault == PngWriterFault.WrongCrc ? 0xFFFFFFFF : Crc(0xFFFFFFFF, "IDAT"u8);
            if (isFirst)
            {
                ReadOnlySpan<byte> header = [0x78, 0x01];
                target.Write(header);
                crc = Crc(crc, header);
            }
            for (var b = 0; b < count; b++)
            {
                target.Write(parts[b]);
                crc = Crc(crc, parts[b]);
                parts[b] = null!;
            }
            if (isLast)
            {
                if (fault != PngWriterFault.NoFinalBlock)
                {
                    ReadOnlySpan<byte> finalBlock = [0x03, 0x00]; // an empty final block of fixed Huffman codes
                    target.Write(finalBlock);
                    crc = Crc(crc, finalBlock);
                }
                BinaryPrimitives.WriteUInt32BigEndian(word, adler);
                target.Write(word);
                crc = Crc(crc, word);
            }
            BinaryPrimitives.WriteUInt32BigEndian(word, crc ^ 0xFFFFFFFF);
            target.Write(word);
        }

        WriteChunk(target, "IEND"u8, [], fault);
    }

    /// <summary>A row filtered <c>up</c> into <paramref name="row"/>: its filter byte, then each byte of <paramref name="src"/> less the byte of <paramref name="above"/>.</summary>
    private static void FilterRow(ReadOnlySpan<byte> src, ReadOnlySpan<byte> above, Span<byte> row, PngWriterFault fault)
    {
        row[0] = fault == PngWriterFault.FilterByteNone ? (byte)0 : (byte)2;
        var dst = row[1..];
        var i = 0;
        if (Vector.IsHardwareAccelerated)
            for (; i <= src.Length - Vector<byte>.Count; i += Vector<byte>.Count)
                (new Vector<byte>(src[i..]) - new Vector<byte>(above[i..])).CopyTo(dst[i..]);
        for (; i < src.Length; i++) dst[i] = (byte)(src[i] - above[i]);
    }

    /// <summary>
    /// A band's raw deflate bytes, ending on a byte boundary with no final block, and the Adler-32 of its filtered rows.
    /// The band is <paramref name="rows"/> rows from row <paramref name="start"/> of a batch starting at image row
    /// <paramref name="y0"/>, held in <paramref name="raw"/> after the row above the batch.
    /// </summary>
    private static (byte[] Bytes, uint Adler) DeflateBand(byte[] raw, int y0, int start, int rows, int stride, PngWriterFault fault)
    {
        var rowBytes = stride + 1;
        var filtered = ArrayPool<byte>.Shared.Rent(rows * rowBytes);
        try
        {
            for (var r = 0; r < rows; r++)
            {
                var k = start + r + 1; // the row's place in raw
                ReadOnlySpan<byte> above = fault == PngWriterFault.BandStartAgainstZeros && r == 0 && y0 + start > 0
                    ? new byte[stride]
                    : raw.AsSpan((k - 1) * stride, stride);
                FilterRow(raw.AsSpan(k * stride, stride), above, filtered.AsSpan(r * rowBytes, rowBytes), fault);
            }

            using var stream = new MemoryStream();
            byte[] bytes;
            using (var deflate = new DeflateStream(stream, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (var r = 0; r < rows; r++) deflate.Write(filtered, r * rowBytes, rowBytes);
                deflate.Flush(); // a sync flush: what follows is not part of the format
                bytes = stream.ToArray();
            }
            return (bytes, Adler(1, filtered.AsSpan(0, rows * rowBytes)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(filtered);
        }
    }

    private static uint Adler(uint adler, ReadOnlySpan<byte> data)
    {
        uint a = adler & 0xFFFF, b = adler >> 16;
        while (data.Length > 0)
        {
            var n = Math.Min(data.Length, 5552); // the most bytes before b can overflow
            for (var i = 0; i < n; i++) { a += data[i]; b += a; }
            a %= 65521; b %= 65521;
            data = data[n..];
        }
        return (b << 16) | a;
    }

    /// <summary>The Adler-32 of two runs of bytes joined, from each run's and the second's length, as zlib's <c>adler32_combine</c>. The empty run's Adler-32 is 1, so the first band combines onto it unchanged.</summary>
    private static uint AdlerCombine(uint adler1, uint adler2, long length2)
    {
        const uint Base = 65521;
        var rem = (uint)(length2 % Base);
        var sum1 = adler1 & 0xFFFF;
        var sum2 = rem * sum1 % Base;
        sum1 += (adler2 & 0xFFFF) + Base - 1;
        sum2 += (adler1 >> 16) + (adler2 >> 16) + Base - rem;
        if (sum1 >= Base) sum1 -= Base;
        if (sum1 >= Base) sum1 -= Base;
        if (sum2 >= Base << 1) sum2 -= Base << 1;
        if (sum2 >= Base) sum2 -= Base;
        return sum1 | (sum2 << 16);
    }

    private static readonly uint[] CrcTable = MakeCrcTable();

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static void WriteChunk(Stream target, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data, PngWriterFault fault)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)data.Length);
        target.Write(word);
        target.Write(type);
        target.Write(data);
        var crc = fault == PngWriterFault.WrongCrc ? 0xFFFFFFFF : Crc(0xFFFFFFFF, type);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc(crc, data) ^ 0xFFFFFFFF);
        target.Write(word);
    }
}

/// <summary>A break put into <see cref="PngWriter"/> for the tests' table of broken copies; <see cref="None"/> is the writer.</summary>
internal enum PngWriterFault
{
    None,
    /// <summary>Filter byte 0 (none) on rows whose bytes are filtered <c>up</c>.</summary>
    FilterByteNone,
    /// <summary>A band's first row filtered against zeros instead of the row above it.</summary>
    BandStartAgainstZeros,
    /// <summary>The bands' Adler-32s combined as if each later band were empty.</summary>
    WrongAdlerCombine,
    /// <summary>No empty final block before the Adler-32.</summary>
    NoFinalBlock,
    /// <summary>Every chunk's CRC worked over its data without its type.</summary>
    WrongCrc,
}
