using System.Numerics;
using Carve.Debug;
using Carve.SampleTextures;

namespace Carve.Scenario.Materials;

/// <summary>The classes of a texture's region the pixel checks sample, as the generator (<c>TextureSet</c>) labels them.</summary>
internal enum Look { RuneOn, RuneOff, StaveMid, Clean, Rust, RingLight, Colour }

/// <summary>The texels' classes, read from the generator's own pixels (the committed PNGs equal them, as <c>SampleTexturesTests</c> shows), never from a frame.</summary>
internal static class Texels
{
    private static readonly Dictionary<string, byte[]> Rgba = TextureSet.Build().ToDictionary(t => t.Name, t => t.Rgba);

    private static int Channel(string file, int x, int y, int channel) => Rgba[file][(y * TextureSet.Size + x) * 4 + channel];

    /// <summary>Texels from the nearest stave edge, 0 at the two gap texels.</summary>
    public static int EdgeDistance(int x) { var m = x % TextureSet.StaveWidth; return Math.Min(m, TextureSet.StaveWidth - 1 - m); }

    /// <summary>Wood well clear of a gap (6 texels or more: flat normal, full occlusion).</summary>
    public static Look? Stave(int x, int y) => EdgeDistance(x) >= 6 ? Look.StaveMid : null;

    /// <summary>The glyph (emissive white) or the plain wood beside it (emissive black, clear of a gap).</summary>
    public static Look? Rune(int x, int y) => Channel("rune-emissive.png", x, y, 0) switch { >= 128 => Look.RuneOn, <= 16 when EdgeDistance(x) >= 6 => Look.RuneOff, _ => null };

    /// <summary>Clean iron (metallic channel about 255) or rust (about 0).</summary>
    public static Look? Iron(int x, int y) => Channel("iron-mr.png", x, y, 2) switch { >= 247 => Look.Clean, <= 16 => Look.Rust, _ => null };

    /// <summary>The early wood of a ring (red 198 or more), the pale part of the end grain.</summary>
    public static Look? Ring(int x, int y) => Channel("end-grain-base.png", x, y, 0) >= 198 ? Look.RingLight : null;

    public static Func<int, int, Look?>? For(string material) => material switch
    {
        "staves" => Stave,
        "rune-stave" => Rune,
        "iron" => Iron,
        "end-grain" => Ring,
        _ => null,
    };
}

internal sealed record Sample(Hit Hit, Look Look, bool FlipV, bool MirrorU);

/// <summary>
/// The P2 samples of one frame (or one tile of a sheet) by class, and T-78's pixel rules over them (P3 relations, P4 emissive, P5 orientation, P6 a
/// floor), each worked from the geometry and the textures; the frame is read only at the sites they name.
/// </summary>
internal sealed class Pool
{
    public const int M = 12, T = 80;
    public static readonly List<string> Log = [];

    private readonly ScenarioDriver _driver;
    private readonly ScenarioFrame _frame;
    private readonly Shot _shot;
    private readonly string _what;
    private readonly Dictionary<Look, List<Sample>> _by = Enum.GetValues<Look>().ToDictionary(l => l, _ => new List<Sample>());

    public Pool(ScenarioDriver driver, ScenarioFrame frame, Shot shot, string what, IReadOnlyDictionary<int, string> materials, IReadOnlySet<int> colours)
    {
        (_driver, _frame, _shot, _what) = (driver, frame, shot, what);
        var scale = Math.Max(1, shot.Size / 512f);
        foreach (var hit in shot.Sites((int)(520 * scale), (int)(28 * scale)))
        {
            if (colours.Contains(hit.Face.Face))
            {
                _by[Look.Colour].Add(new Sample(hit, Look.Colour, false, false));
                continue;
            }

            if (!materials.TryGetValue(hit.Face.Face, out var name) || Texels.For(name) is not { } classify) continue;
            if (Shot.Class(hit.Texel, hit.Half, classify) is not { } look) continue;
            var (flipped, mirrored) = Flips(hit, look, classify);
            _by[look].Add(new Sample(hit, look, flipped, mirrored));
        }

        Log.Add($"{_what}\tsamples\t{string.Join(", ", _by.Select(e => $"{e.Key} {e.Value.Count}"))}");
    }

    /// <summary>Whether the hit's class would be another under a v-flipped texture, and under a u-mirrored one (P5).</summary>
    public static (bool FlipV, bool MirrorU) Flips(Hit hit, Look look, Func<int, int, Look?> classify)
    {
        var flipped = Shot.Class(new Vector2(hit.Texel.X, Shot.TextureSize - hit.Texel.Y), hit.Half, classify);
        var mirrored = Shot.Class(new Vector2(Shot.TextureSize - hit.Texel.X, hit.Texel.Y), hit.Half, classify);
        return (flipped != look, mirrored != look);
    }

    public IReadOnlyList<Sample> Of(Look look) => _by[look];

    public IEnumerable<Sample> All => _by.Values.SelectMany(s => s);

    public (int R, int G, int B) Pixel(Sample s)
    {
        var (x, y) = _shot.InFrame(s.Hit.X, s.Hit.Y);
        var p = _frame.Pixel(x, y);
        return ((int)p.R, (int)p.G, (int)p.B);
    }

    public static int Max((int R, int G, int B) p) => Math.Max(p.R, Math.Max(p.G, p.B));

    /// <summary>R, G and B ordered with at least M between neighbours: rust, wood and end grain have it, clean iron does not.</summary>
    public static bool Warm((int R, int G, int B) p) => p.R >= p.G + M && p.G >= p.B + M;

    /// <summary>P4: R of 230 or more and R over G + M over B + 2M, the orange of #FF8020 at full strength.</summary>
    public static bool Glowing((int R, int G, int B) p) => p.R >= 230 && p.R > p.G + M && p.G + M > p.B + 2 * M;

    private void Fail(string message) => _driver.Fail($"{_frame.Name}: {_what}: {message}");

    /// <summary>P6: at least <paramref name="count"/> samples of the class, or the check fails as not in sight rather than passing empty.</summary>
    public void Need(Look look, int count = 8)
    {
        if (_by[look].Count < count) Fail($"only {_by[look].Count} P2 samples of {look}, at least {count} needed");
    }

    /// <summary>P5: at least three of the class's samples would be another class under a v-flipped texture and three under a u-mirrored one.</summary>
    public void Orientation(params Look[] looks)
    {
        var samples = looks.SelectMany(l => _by[l]).ToArray();
        int v = samples.Count(s => s.FlipV), u = samples.Count(s => s.MirrorU);
        Log.Add($"{_what}\torientation {string.Join("+", looks)}\tv-flip {v}\tu-mirror {u}");
        if (v < 3) Fail($"only {v} {string.Join(" or ", looks)} samples change class under a v-flipped texture, at least 3 needed");
        if (u < 3) Fail($"only {u} {string.Join(" or ", looks)} samples change class under a u-mirrored texture, at least 3 needed");
    }

    /// <summary>The rune glows and is brighter than the plain wood of the same face (P4), and the glyph's strokes tell a flipped texture.</summary>
    public void RuneGlows(int floor = 8)
    {
        // At 256 px the glyph's strokes resolve at few pixels: as many as exist, at least 3 (ruling 4); the count is in the log.
        Need(Look.RuneOn, floor);
        if (floor >= 8) Need(Look.RuneOff);
        foreach (var on in _by[Look.RuneOn])
        {
            var p = Pixel(on);
            if (!Glowing(p)) Fail($"rune texel on face {on.Hit.Face.Face} at ({on.Hit.X}, {on.Hit.Y}) is {p}, not glowing orange");
            foreach (var off in _by[Look.RuneOff].Where(o => o.Hit.Face.Face == on.Hit.Face.Face))
            {
                var q = Pixel(off);
                if (Max(p) < Max(q) + M) Fail($"rune texel on face {on.Hit.Face.Face} is {p}, not {M} brighter than plain wood {q} of the same face");
            }
        }

        Orientation(Look.RuneOn);
        if (floor < 8 && _by[Look.RuneOn].Count < 8) Log.Add($"{_what}	below P6's floor of 8	RuneOn {_by[Look.RuneOn].Count}");
    }

    /// <summary>No P2 sample of the frame glows, and more than a floor of them are lit wood or iron, so it is not empty.</summary>
    public void NoRune()
    {
        var all = All.ToArray();
        if (all.Length < 8) Fail($"only {all.Length} P2 samples at all");
        foreach (var s in all)
        {
            var p = Pixel(s);
            if (Glowing(p)) Fail($"{s.Look} at ({s.Hit.X}, {s.Hit.Y}) on face {s.Hit.Face.Face} is {p}, glowing: the rune is meant to be out of sight");
        }

        if (_by[Look.RuneOn].Count + _by[Look.RuneOff].Count != 0) Fail("a rune face is in sight");
    }

    /// <summary>Clean iron lit and not rust-ordered, rust rust-ordered and redder than every clean sample (P3), and the mask tells a flipped texture.</summary>
    public void IronAndRust()
    {
        Need(Look.Clean);
        Need(Look.Rust);
        int cleanRb = int.MinValue, rustRb = int.MaxValue, cleanMin = int.MaxValue;
        foreach (var s in _by[Look.Clean])
        {
            var p = Pixel(s);
            if (Max(p) < T) Fail($"clean iron at ({s.Hit.X}, {s.Hit.Y}) is {p}, near-black (below {T}): the sky should light it");
            if (Warm(p)) Fail($"clean iron at ({s.Hit.X}, {s.Hit.Y}) is {p}, ordered R > G > B like rust");
            cleanRb = Math.Max(cleanRb, p.R - p.B);
            cleanMin = Math.Min(cleanMin, Max(p));
        }

        foreach (var s in _by[Look.Rust])
        {
            var p = Pixel(s);
            if (!Warm(p)) Fail($"rust at ({s.Hit.X}, {s.Hit.Y}) is {p}, not ordered R > G > B by {M}");
            rustRb = Math.Min(rustRb, p.R - p.B);
        }

        Log.Add($"{_what}\tiron\tclean brightest channel down to {cleanMin} (T = {T})\tclean R-B up to {cleanRb}\trust R-B down to {rustRb}");
        if (rustRb < cleanRb + M) Fail($"the least rust-red sample (R-B {rustRb}) is not {M} redder than the reddest clean iron (R-B {cleanRb})");
        Orientation(Look.Clean, Look.Rust);
    }

    /// <summary>Wood well clear of a gap: lit and warm, R > G > B (P3).</summary>
    public void Wood()
    {
        Need(Look.StaveMid);
        foreach (var s in _by[Look.StaveMid])
        {
            var p = Pixel(s);
            if (Max(p) < T || !Warm(p)) Fail($"stave at ({s.Hit.X}, {s.Hit.Y}) on face {s.Hit.Face.Face} is {p}, not lit and warm (R > G > B by {M})");
        }
    }

    /// <summary>The cap's pale rings: lit and warm, and on the cap <paramref name="face"/> (33 the top, 32 the bottom). The rings are not resolved at +y (P2), so nothing finer is checked there (ruling 4).</summary>
    public void Rings(int face)
    {
        Need(Look.RingLight);
        foreach (var s in _by[Look.RingLight])
        {
            if (s.Hit.Face.Face != face) Fail($"end grain sample at ({s.Hit.X}, {s.Hit.Y}) is on face {s.Hit.Face.Face}, not the cap {face} in sight");
            var p = Pixel(s);
            if (Max(p) < T || !Warm(p)) Fail($"end grain at ({s.Hit.X}, {s.Hit.Y}) is {p}, not lit and warm (R > G > B by {M})");
        }
    }

    /// <summary>The colour faces' samples: #C8503C is red over green and blue.</summary>
    public void Coloured(params int[] faces)
    {
        Need(Look.Colour);
        foreach (var s in _by[Look.Colour])
        {
            var p = Pixel(s);
            if (!(p.R >= p.G + 2 * M && p.R >= p.B + 2 * M)) Fail($"colour face {s.Hit.Face.Face} at ({s.Hit.X}, {s.Hit.Y}) is {p}, not red over green and blue");
        }

        foreach (var f in faces)
        {
            if (!_by[Look.Colour].Any(s => s.Hit.Face.Face == f)) Fail($"no P2 sample of colour face {f}");
        }
    }
}
