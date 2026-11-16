namespace Carve.SampleTextures;

/// <summary>
/// The textured barrel's eight 256 x 256 RGBA8 textures, every texel worked from its coordinates and an integer hash
/// with a fixed seed (no System.Random, no clock). Row 0 is the top of the image, v = 0 (glTF's origin).
/// </summary>
public static class TextureSet
{
    public const int Size = 256;
    public const int Staves = 8;
    public const int StaveWidth = Size / Staves;   // 32 texels

    // project_uvs cylindrical on the barrel's side keeps aspect: arc length at the mean radius (0.38 m) and height share one
    // scale, so the side fills the full width and the middle 0.419 of v, centred. A texel is the same size in both directions.
    const double MeanRadius = 0.38;
    static readonly double SideScale = 1 / (2 * Math.PI * MeanRadius);              // uv per metre
    static readonly double SideTop = (1 - SideScale) / 2;                            // v of y = 1 (the barrel is 1 m tall)
    static readonly double MetresPerTexel = 1 / (SideScale * Size);

    /// <summary>
    /// The rune's rectangle in uv, (u0, v0, u1, v1): the bounds of faces 14 and 22 under project_uvs cylindrical on the
    /// barrel's side (u from the -Z seam, column k = 6, the fifth stave; v = SideTop + (1 - y) * scale). A test works it
    /// again from the Core.
    /// </summary>
    public static readonly (double U0, double V0, double U1, double V1) RuneRect =
        (0.5, SideTop + 0.1 * SideScale, 0.625, SideTop + 0.9 * SideScale);

    const double StrokeHalfWidth = 0.02;

    public static IEnumerable<(string Name, byte[] Rgba)> Build()
    {
        yield return ("staves-base.png", StavesBase());
        yield return ("staves-normal.png", StavesNormal());
        yield return ("staves-occlusion.png", StavesOcclusion());
        yield return ("iron-base.png", IronBase());
        yield return ("iron-mr.png", IronMr());
        yield return ("end-grain-base.png", EndGrainBase());
        yield return ("end-grain-normal.png", EndGrainNormal());
        yield return ("rune-emissive.png", RuneEmissive());
    }

    // ---- hash noise -------------------------------------------------------------------------------------------

    public static uint Hash(int x, int y, uint seed)
    {
        var h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA6Bu ^ seed * 0xC2B2AE35u;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return h;
    }

    static double Unit(int x, int y, uint seed) => (Hash(x, y, seed) & 0xFFFF) / 65535.0;

    /// <summary>Value noise in 0..1 on a lattice of cellX x cellY texels.</summary>
    static double Noise(int x, int y, int cellX, int cellY, uint seed)
    {
        int ix = Math.DivRem(x, cellX, out var rx), iy = Math.DivRem(y, cellY, out var ry);
        double fx = (double)rx / cellX, fy = (double)ry / cellY;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        var a = Unit(ix, iy, seed) + (Unit(ix + 1, iy, seed) - Unit(ix, iy, seed)) * fx;
        var b = Unit(ix, iy + 1, seed) + (Unit(ix + 1, iy + 1, seed) - Unit(ix, iy + 1, seed)) * fx;
        return a + (b - a) * fy;
    }

    static byte B(double v) => (byte)Math.Clamp((int)Math.Round(v, MidpointRounding.AwayFromZero), 0, 255);
    static double Smooth(double lo, double hi, double v) { var t = Math.Clamp((v - lo) / (hi - lo), 0, 1); return t * t * (3 - 2 * t); }
    static double Lerp(double a, double b, double t) => a + (b - a) * t;
    /// <summary>Rounds to a multiple of step: smooth gradients become runs, which keeps the PNGs under 64 KB.</summary>
    static double Q(double v, double step) => Math.Round(v / step, MidpointRounding.AwayFromZero) * step;

    static byte[] Make(Func<int, int, (double R, double G, double B)> texel)
    {
        var rgba = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b) = texel(x, y);
                var i = (y * Size + x) * 4;
                rgba[i] = B(r); rgba[i + 1] = B(g); rgba[i + 2] = B(b); rgba[i + 3] = 255;
            }
        return rgba;
    }

    // ---- staves -----------------------------------------------------------------------------------------------

    /// <summary>Texels from the nearest stave edge, 0 for the two gap texels (x % 32 = 0 and 31).</summary>
    static int EdgeDistance(int x) { var m = x % StaveWidth; return Math.Min(m, StaveWidth - 1 - m); }

    static byte[] StavesBase() => Make((x, y) =>
    {
        if (EdgeDistance(x) == 0) return (28, 18, 11);
        var tone = 0.82 + 0.36 * Unit(x / StaveWidth, 0, 11);               // each stave its own tone
        var grain = 0.5 * Unit(x, 0, 12) + 0.5 * Noise(x, y, 2, 96, 13);    // runs along v
        var k = tone * (0.9 + 0.2 * Math.Floor(grain * 4) / 3);             // four steps of grain keep the PNG small
        return (170 * k, 112 * k, 64 * k);
    });

    static byte[] StavesNormal() => Make((x, y) =>
    {
        var d = EdgeDistance(x);
        var tilt = d < 5 ? 0.6 * (5 - d) / 5 : 0;
        var nx = (x % StaveWidth < StaveWidth / 2 ? -1 : 1) * tilt;         // toward the gap
        return (127.5 + 127.5 * nx, 128, 127.5 + 127.5 * Math.Sqrt(1 - nx * nx));
    });

    static byte[] StavesOcclusion() => Make((x, y) =>
    {
        var v = 255 - 115 * Math.Max(0, 1 - EdgeDistance(x) / 6.0);
        return (v, v, v);
    });

    // ---- iron -------------------------------------------------------------------------------------------------

    /// <summary>The rust mask, 0 clean to 1 rust: two octaves of hash noise, stretched along u (the hoops run round, in bands about 11 texels tall at rows 74 to 85 and 171 to 182).</summary>
    static double Rust(int x, int y) => Smooth(0.56, 0.64, 0.65 * Noise(x, y, 40, 6, 21) + 0.35 * Noise(x, y, 12, 3, 22));

    static byte[] IronBase() => Make((x, y) =>
    {
        var m = Rust(x, y);
        var speck = Math.Floor(Noise(x, y, 4, 4, 23) * 3) / 2 * 6;
        return (Q(Lerp(58 + speck, 176 + speck * 2, m), 6), Q(Lerp(61 + speck, 84 + speck, m), 6), Q(Lerp(66 + speck, 34, m), 6));
    });

    static byte[] IronMr() => Make((x, y) =>
    {
        var m = Rust(x, y);
        return (255, Q(255 * Lerp(0.35, 0.9, m), 8), Q(255 * (1 - m), 8));
    });

    // ---- end grain --------------------------------------------------------------------------------------------

    const double RingPeriod = 9;

    static (double Phase, double R, double Dx, double Dy) Ring(int x, int y)
    {
        double dx = x - 128, dy = y - 128, r = Math.Sqrt(dx * dx + dy * dy);
        var wobble = 3 * Noise(x, y, 48, 48, 31);
        var p = (r + wobble) / RingPeriod;
        return (p - Math.Floor(p), r, dx, dy);
    }

    static byte[] EndGrainBase() => Make((x, y) =>
    {
        var (p, _, _, _) = Ring(x, y);
        var late = Smooth(0.55, 0.75, p) * (1 - Smooth(0.95, 1.0, p));      // the dark latewood band of each ring
        var k = 1 - 0.28 * late;
        return (Q(206 * k, 6), Q(152 * k, 6), Q(92 * k, 6));
    });

    static byte[] EndGrainNormal() => Make((x, y) =>
    {
        var (p, r, dx, dy) = Ring(x, y);
        // A triangle-wave height: it rises over the first 0.7 of a ring and falls over the rest.
        var slope = r < 1 ? 0 : p < 0.7 ? 0.18 : -0.18 * 0.7 / 0.3;
        var nx = -slope * dx / Math.Max(r, 1);
        var ny = slope * dy / Math.Max(r, 1);                               // image y runs down; the normal's G is up
        var nz = Math.Sqrt(1 - nx * nx - ny * ny);
        return (Q(127.5 + 127.5 * nx, 6), Q(127.5 + 127.5 * ny, 6), Q(127.5 + 127.5 * nz, 6));
    });

    // ---- rune -------------------------------------------------------------------------------------------------

    /// <summary>A Fehu-like glyph: a stem and two arms, as segments in metres from the rectangle's centre (x right, y up).</summary>
    static readonly (double X0, double Y0, double X1, double Y1)[] Strokes =
    [
        (0, -0.30, 0, 0.30),
        (0, 0.30, 0.10, 0.17),
        (0, 0.12, 0.10, -0.01),
    ];

    static double SegmentDistance(double px, double py, (double X0, double Y0, double X1, double Y1) s)
    {
        double vx = s.X1 - s.X0, vy = s.Y1 - s.Y0;
        var t = Math.Clamp(((px - s.X0) * vx + (py - s.Y0) * vy) / (vx * vx + vy * vy), 0, 1);
        double ex = px - (s.X0 + t * vx), ey = py - (s.Y0 + t * vy);
        return Math.Sqrt(ex * ex + ey * ey);
    }

    static byte[] RuneEmissive()
    {
        var (u0, v0, u1, v1) = RuneRect;
        double cx = (u0 + u1) / 2 * Size, cy = (v0 + v1) / 2 * Size;
        return Make((x, y) =>
        {
            double px = (x + 0.5 - cx) * MetresPerTexel, py = -(y + 0.5 - cy) * MetresPerTexel;
            var on = Strokes.Any(s => SegmentDistance(px, py, s) <= StrokeHalfWidth);
            return on ? (255, 255, 255) : (0, 0, 0);
        });
    }
}
