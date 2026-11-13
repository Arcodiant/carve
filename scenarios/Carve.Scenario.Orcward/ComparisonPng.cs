using System.IO.Compression;
using Carve.Debug;

namespace Carve.Scenario.Orcward;

/// <summary>Orcward's frame rule, and the review image: render | render_glb | difference, side by side.</summary>
internal static class Comparison
{
    /// <summary>Orcward's rule (D-6): a pixel differs past this many of 255 in R, G or B.</summary>
    public const int PixelThreshold = 8;

    public sealed record Result(string Name, string Kind, int Differing, int Total, int Foreground)
    {
        /// <summary>A frame fails past 0.5 % of its pixels.</summary>
        public bool Fails => (long)Differing * 200 > Total;

        public double Percent => 100.0 * Differing / Total;
    }

    public static bool Differs(ScenarioFrame a, ScenarioFrame b, int i) =>
        Math.Abs(a.Pixels[i] - b.Pixels[i]) > PixelThreshold || Math.Abs(a.Pixels[i + 1] - b.Pixels[i + 1]) > PixelThreshold
        || Math.Abs(a.Pixels[i + 2] - b.Pixels[i + 2]) > PixelThreshold;

    public static Result Measure(string name, string kind, ScenarioFrame a, ScenarioFrame b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            throw new InvalidOperationException($"{name}: frames of different sizes");
        }

        var count = 0;
        for (var i = 0; i < a.Pixels.Length; i += 4)
        {
            if (Differs(a, b, i))
            {
                count++;
            }
        }

        var foreground = 0;
        for (var i = 0; i < a.Pixels.Length; i += 4)
        {
            if (!Carve.Scenario.Crate.CrateBuild.IsBackground((a.Pixels[i], a.Pixels[i + 1], a.Pixels[i + 2], a.Pixels[i + 3])))
            {
                foreground++;
            }
        }

        return new Result(name, kind, count, a.Width * a.Height, foreground);
    }

    /// <summary>Writes the triptych as a PNG: the two frames and the difference (the render dimmed, differing pixels red).</summary>
    public static void WriteTriptych(string path, ScenarioFrame a, ScenarioFrame b)
    {
        int w = a.Width, h = a.Height;
        var raw = new byte[h * (1 + 3 * w * 3)];
        for (var y = 0; y < h; y++)
        {
            var row = y * (1 + 9 * w) + 1;
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                for (var c = 0; c < 3; c++)
                {
                    raw[row + x * 3 + c] = a.Pixels[i + c];
                    raw[row + (w + x) * 3 + c] = b.Pixels[i + c];
                }

                var differs = Differs(a, b, i);
                raw[row + (2 * w + x) * 3] = differs ? (byte)255 : (byte)(a.Pixels[i] / 4);
                raw[row + (2 * w + x) * 3 + 1] = differs ? (byte)0 : (byte)(a.Pixels[i + 1] / 4);
                raw[row + (2 * w + x) * 3 + 2] = differs ? (byte)0 : (byte)(a.Pixels[i + 2] / 4);
            }
        }

        using var zipped = new MemoryStream();
        using (var z = new ZLibStream(zipped, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        using var file = new MemoryStream();
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, 3 * w);
        WriteBe(ihdr, 4, h);
        ihdr[8] = 8;
        ihdr[9] = 2;
        Chunk(file, "IHDR", ihdr);
        Chunk(file, "IDAT", zipped.ToArray());
        Chunk(file, "IEND", []);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, file.ToArray());
    }

    private static void WriteBe(byte[] to, int at, int v)
    {
        to[at] = (byte)(v >> 24);
        to[at + 1] = (byte)(v >> 16);
        to[at + 2] = (byte)(v >> 8);
        to[at + 3] = (byte)v;
    }

    private static void Chunk(Stream to, string type, byte[] data)
    {
        var head = new byte[4];
        WriteBe(head, 0, data.Length);
        to.Write(head);
        var body = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++)
        {
            body[i] = (byte)type[i];
        }

        data.CopyTo(body, 4);
        to.Write(body);
        var crc = 0xFFFFFFFFu;
        foreach (var b in body)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        crc ^= 0xFFFFFFFFu;
        WriteBe(head, 0, (int)crc);
        to.Write(head);
    }
}
