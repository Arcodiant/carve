using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Checks;

/// <summary>Where a <see cref="CheckSet"/>'s texels came from: a graph's evaluated floats, or decoded PNG bytes (8-bit, so the rules allow for the rounding).</summary>
public enum CheckSource { Graph, Png }

/// <summary>
/// One output as the rules see it: <see cref="Data"/> is row-major, <see cref="Channels"/> floats a texel (1 greyscale, 3
/// colour), in the values the graph evaluates to (a colour's sRGB 0..1, a normal's vector components, a grey's 0..1). A
/// PNG's bytes are read back into the same values, so one rule judges both.
/// </summary>
public sealed class CheckOutput(string name, int width, int height, int channels, float[] data, bool derived = false)
{
    /// <summary>A role's name or a named output's.</summary>
    public string Name { get; } = name;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int Channels { get; } = channels;
    public float[] Data { get; } = data;

    /// <summary>True for a reading made of another file (the metallic in a roughness PNG's blue channel), which measures for the rules and is not an output of its own.</summary>
    public bool Derived { get; } = derived;

    public long Texels => (long)Width * Height;
    public bool IsGrey => Channels == 1;
    public bool IsRole(OutputRole role) => OutputRoles.TryParse(Name, out var r) && r == role;

    /// <summary>A value as the byte it packs to in the output's file (<see cref="OutputPacking"/>'s rounding).</summary>
    public byte Level(float v) => OutputPacking.Byte(IsRole(OutputRole.Normal) ? 127.5f + 127.5f * v : 255f * v);

    /// <summary>The packed byte of channel <paramref name="c"/> of the texel at (x, y).</summary>
    public byte Level(int x, int y, int c) => Level(Data[((long)y * Width + x) * Channels + c]);

    /// <summary>A greyscale value at (x, y); a colour's first channel.</summary>
    public float Grey(int x, int y) => Data[((long)y * Width + x) * Channels];

    /// <summary>The graph's evaluated <paramref name="output"/> at <paramref name="size"/>, copied out tile by tile.</summary>
    public static CheckOutput Evaluate(GraphEvaluator evaluator, string output, int size)
    {
        var channels = PortKinds.Channels(evaluator.KindOf(output));
        var data = new float[(long)size * size * channels];
        evaluator.Evaluate(output, size, tile =>
        {
            for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                tile.Row(y).CopyTo(data.AsSpan((int)(((long)y * size + tile.Rect.X) * channels), tile.Rect.Width * channels));
        });
        return new(output, size, size, channels, data);
    }

    /// <summary>
    /// A PNG's RGBA8 bytes as the output <paramref name="name"/>. With <paramref name="channel"/> it is that one channel
    /// as a greyscale (roughness's G, the metallic in its B). Otherwise a role that is grey reads R, a colour role and a
    /// normal read R, G and B, and a named output is grey when every texel has R = G = B. A normal's bytes are
    /// 127.5 + 127.5 * component.
    /// </summary>
    public static CheckOutput FromRgba8(string name, int width, int height, byte[] rgba, int? channel = null, bool derived = false)
    {
        if (rgba.LongLength != (long)width * height * 4) throw new ArgumentException("an RGBA8 image is four bytes a texel", nameof(rgba));
        var isRole = OutputRoles.TryParse(name, out var role);
        var normal = isRole && role == OutputRole.Normal;
        var grey = channel is not null || (isRole ? OutputRoles.Takes(role) == PortKind.Grey : IsGreyImage(rgba));
        var channels = grey ? 1 : 3;
        var data = new float[(long)width * height * channels];
        for (long i = 0; i < (long)width * height; i++)
            for (var c = 0; c < channels; c++)
            {
                var b = rgba[i * 4 + (channel ?? c)];
                data[i * channels + c] = normal ? (b - 127.5f) / 127.5f : b / 255f;
            }
        return new(name, width, height, channels, data, derived);
    }

    private static bool IsGreyImage(byte[] rgba)
    {
        for (var i = 0; i < rgba.Length; i += 4)
            if (rgba[i] != rgba[i + 1] || rgba[i] != rgba[i + 2]) return false;
        return true;
    }
}

/// <summary>What the rules judge: the outputs of a graph or of some PNG files, and how they were read.</summary>
public sealed class CheckSet(CheckSource source, IReadOnlyList<CheckOutput> outputs, IReadOnlyList<string>? notes = null)
{
    public CheckSource Source { get; } = source;
    public IReadOnlyList<CheckOutput> Outputs { get; } = outputs;

    /// <summary>What the reading adds that is not a problem, such as which file a reading came from.</summary>
    public IReadOnlyList<string> Notes { get; } = notes ?? [];

    public CheckOutput? Find(string name) => Outputs.FirstOrDefault(o => o.Name == name);

    /// <summary>The metallic reading that judges <paramref name="output"/>'s texels as metal or not: the set's own metallic of the same size, or none.</summary>
    public CheckOutput? MetallicFor(CheckOutput output) =>
        Find(OutputRoles.Name(OutputRole.Metallic)) is { IsGrey: true } m && m.Width == output.Width && m.Height == output.Height ? m : null;

    /// <summary>A set from a graph's evaluator at <paramref name="size"/>: every output it has, in sheet order.</summary>
    public static CheckSet OfGraph(GraphEvaluator evaluator, int size) =>
        new(CheckSource.Graph, [.. evaluator.Outputs.Select(o => CheckOutput.Evaluate(evaluator, o, size))]);

    /// <summary>
    /// A set from decoded PNGs, each with its role or output name. A <c>roughness</c> file is a glTF metallic-roughness
    /// image: its G is the roughness and its B a metallic reading, added as <c>metallic</c> when no file of that role was given.
    /// </summary>
    public static CheckSet OfPngs(IReadOnlyList<(string Role, int Width, int Height, byte[] Rgba)> files)
    {
        var rough = OutputRoles.Name(OutputRole.Roughness);
        var metallic = OutputRoles.Name(OutputRole.Metallic);
        var outputs = new List<CheckOutput>();
        var notes = new List<string>();
        foreach (var (role, w, h, rgba) in files)
            outputs.Add(CheckOutput.FromRgba8(role, w, h, rgba, channel: role == rough ? 1 : null));
        var packed = files.Where(f => f.Role == rough).ToList();
        if (packed.Count == 1 && !files.Any(f => f.Role == metallic))
        {
            outputs.Add(CheckOutput.FromRgba8(metallic, packed[0].Width, packed[0].Height, packed[0].Rgba, channel: 2, derived: true));
            notes.Add("metallic read from roughness's blue channel");
        }
        return new(CheckSource.Png, outputs, notes);
    }
}
