using System.Globalization;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// A kind of node, registered in a <see cref="NodeRegistry"/> rather than switched on: its ports, its parameters, the
/// rectangle of each input it reads and its evaluation. One class per type, in a file of its own. A type made outside this package
/// (M-29's mesh nodes, a test's) is registered the same way, with <see cref="NodeRegistry.With"/>.
/// <para>Every parameter must be relative to the image (cells across, repeats across, slope per image width), so that
/// a graph gives the same picture at every size; and evaluation must be deterministic: integer hashing, <c>float</c>
/// arithmetic, no <c>Math.Pow</c> or <c>Math.Exp</c>, no <c>System.Random</c>, no clock.</para>
/// </summary>
public abstract class NodeType
{
    /// <summary>The <c>type</c> the file names, <c>[a-z][a-z0-9_]*</c>.</summary>
    public abstract string Name { get; }

    /// <summary>The input ports, in the order a message lists them.</summary>
    public abstract IReadOnlyList<InputPort> Inputs { get; }

    /// <summary>The output ports. A node with one is named by its id alone; with several, as <c>"id.port"</c>. The output node has none.</summary>
    public virtual IReadOnlyList<string> Outputs { get; } = ["out"];

    public abstract IReadOnlyList<ParamSpec> Parameters { get; }

    /// <summary>
    /// The rectangle of input <paramref name="port"/> that filling <paramref name="output"/> reads, in logical texels
    /// (it may run past the image on any side; reads wrap). By default <paramref name="output"/> itself: a pointwise
    /// node. A neighbourhood node returns <paramref name="output"/> widened by its radius
    /// (<see cref="TexelRect.Inflate(int)"/>); a transform the bounding box of the output's corners mapped back
    /// (<see cref="TexelRect.Bounding"/>). Reading outside it throws.
    /// </summary>
    public virtual TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) => output;

    /// <summary>
    /// The inputs whose values decide which rectangles of input <paramref name="port"/> a tile reads (T-153). Empty by
    /// default: the port's rectangle is <see cref="InputRect"/>'s, known before anything is evaluated. A node that names
    /// inputs here for a port is asked <see cref="InputRects"/> for it instead, once those inputs' tiles are made. A
    /// named input must itself have a static rectangle (no chains), and must not be the port itself.
    /// </summary>
    public virtual IReadOnlyList<string> DependsOn(string port, NodeParameters parameters) => [];

    /// <summary>
    /// For a port with <see cref="DependsOn"/>: the rectangles of input <paramref name="port"/> that filling
    /// <see cref="InputRequest.Output"/> reads, worked from <see cref="InputRequest.Input"/>'s reads of the inputs it
    /// depends on (each holding that input's <see cref="InputRect"/>). One rectangle or a few; each may run past the
    /// image, and a texel in any of them is readable. Reading outside them all throws, as for a static rectangle. It is
    /// called for every tile the node makes, a dropped tile again, so it must give the same answer from the same values.
    /// By default <see cref="InputRect"/>'s rectangle alone.
    /// </summary>
    public virtual IReadOnlyList<TexelRect> InputRects(string port, InputRequest request) =>
        [InputRect(port, request.Output, request.Parameters, request.Size)];

    /// <summary>What an output port carries, given what each connected input carries (an absent optional input is not in <paramref name="inputs"/>).</summary>
    public abstract PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters);

    /// <summary>
    /// Faults about what the inputs carry that a port's own <see cref="InputPort.Kind"/> cannot say (the output node's
    /// <c>in</c> depends on its role): the port and a message, which the validator prefixes with the node and port.
    /// </summary>
    public virtual IEnumerable<(string Port, string Message)> CheckInputKinds(IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => [];

    /// <summary>
    /// Fills every texel of <see cref="NodeEvaluation.Rect"/>, a tile inside the image, in each output port, from input
    /// reads that hold what <see cref="InputRect"/> asked for. A node works each texel from its image coordinates and
    /// <see cref="NodeEvaluation.Size"/>, never from its place in the tile: tiles come in no promised order, of any size,
    /// and a tile may be asked for more than once (it must come out the same); a type is one instance shared by every
    /// node and call, so it keeps no state between calls.
    /// </summary>
    public abstract void Evaluate(NodeEvaluation evaluation);

    /// <summary>The input port name under which <see cref="EvaluateColumns"/> reads the row pass's tiles.</summary>
    public const string RowsPort = "rows";

    /// <summary>
    /// Opting in to separable passes (T-155): non-null when, for these parameters and size, the node runs as a row pass
    /// (<see cref="EvaluateRows"/>, reading every input over the tile widened by <see cref="SeparablePasses.RowReach"/>
    /// in x only) and a column pass (<see cref="EvaluateColumns"/>, reading the row pass's tiles, as
    /// <see cref="RowsPort"/>, over the tile widened by <see cref="SeparablePasses.ColumnReach"/> in y only). The
    /// evaluator caches the row pass's tiles as a kind of their own, of the node's output kind, evicted and made again
    /// like any other, so the node's cost grows with its radius rather than its square. Only a node with the one output
    /// <c>out</c> and no <see cref="DependsOn"/> may opt in. Null by default: the node is made by <see cref="Evaluate"/>,
    /// which an opted-in node still implements (both passes in one) so that tests can hold the two equal.
    /// </summary>
    public virtual SeparablePasses? Separable(NodeParameters parameters, int size) => null;

    /// <summary>
    /// The row pass of a node that opts in through <see cref="Separable"/>: fills <see cref="NodeEvaluation.Rect"/> of
    /// <c>Output()</c>, the intermediate, from its inputs read over the tile widened in x. The same rules as
    /// <see cref="Evaluate"/>: worked from image coordinates, the same bytes every time it is asked.
    /// </summary>
    public virtual void EvaluateRows(NodeEvaluation evaluation) =>
        throw new NotSupportedException($"node type {Name} does not run as separable passes");

    /// <summary>
    /// The column pass of a node that opts in through <see cref="Separable"/>: fills <see cref="NodeEvaluation.Rect"/>
    /// of <c>Output()</c> from <c>Input(RowsPort)</c>, the row pass's tiles over the tile widened in y. It reads no
    /// other input.
    /// </summary>
    public virtual void EvaluateColumns(NodeEvaluation evaluation) =>
        throw new NotSupportedException($"node type {Name} does not run as separable passes");

    public ParamSpec? Parameter(string name) => Parameters.FirstOrDefault(p => p.Name == name);

    public InputPort? Input(string name) => Inputs.FirstOrDefault(p => p.Name == name);
}

/// <summary>How far a separable node's row pass reads its inputs in x, and its column pass the row pass's tiles in y, in texels (T-155).</summary>
public sealed record SeparablePasses(int RowReach, int ColumnReach);

/// <summary>An input port: its name, what it takes, and whether a graph must connect it.</summary>
public sealed record InputPort(string Name, PortKind Kind, bool Required = true);

public enum ParamKind { Int, Float, Enum, IntPair, Colour, Floats, Colours, Name, GreyOrColour, FilePath }

/// <summary>
/// A parameter: its name, kind, range or choices, and default. <see cref="Check"/> says what is wrong with a value in
/// words that give the range. Make one with <see cref="Int"/>, <see cref="Float"/>, <see cref="Enum"/>,
/// <see cref="IntPair"/>, <see cref="Colour"/>, the lists <see cref="Floats"/> and <see cref="Colours"/> (a count range,
/// each in a range), or <see cref="Identifier"/> (optional: it has no default, and a node that leaves it out has none).
/// <para>In the file a list is a JSON array: <c>[0, 0.25, 1]</c> of numbers, <c>["#202020", "#c0a080"]</c> of colours; an
/// empty list, <c>[]</c>, reads as numbers and is taken by either kind.</para>
/// </summary>
public sealed class ParamSpec
{
    private ParamSpec(string name, ParamKind kind, double min, double max, IReadOnlyList<string> choices, ParamValue @default,
        int minCount = 0, int maxCount = 0, bool optional = false)
    {
        Name = name; Kind = kind; Min = min; Max = max; Choices = choices; Default = @default;
        MinCount = minCount; MaxCount = maxCount; Optional = optional;
        if (minCount < 0 || maxCount < minCount) throw new ArgumentException($"parameter '{name}': a count range is 0 <= min <= max");
        if (!optional && Check(@default) is { } wrong) throw new ArgumentException($"parameter '{name}': its default {wrong}");
    }

    public string Name { get; }
    public ParamKind Kind { get; }
    public double Min { get; }
    public double Max { get; }
    public IReadOnlyList<string> Choices { get; }
    public ParamValue Default { get; }

    /// <summary>The fewest and most items of a list (<see cref="ParamKind.Floats"/>, <see cref="ParamKind.Colours"/>).</summary>
    public int MinCount { get; }
    public int MaxCount { get; }

    /// <summary>True for a parameter with no default (<see cref="ParamKind.Name"/>): <see cref="NodeParameters.Has"/> says whether a node gives it.</summary>
    public bool Optional { get; }

    /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static ParamSpec Int(string name, int min, int max, int @default) => new(name, ParamKind.Int, min, max, [], ParamValue.Of(@default));

    /// <summary>A number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
    public static ParamSpec Float(string name, double min, double max, double @default) => new(name, ParamKind.Float, min, max, [], ParamValue.Of(@default));

    /// <summary>One of <paramref name="choices"/>, a string.</summary>
    public static ParamSpec Enum(string name, string @default, params string[] choices) => new(name, ParamKind.Enum, 0, 0, choices, ParamValue.Of(@default));

    /// <summary>Two whole numbers <c>[x, y]</c>, each from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static ParamSpec IntPair(string name, int min, int max, int defaultX, int defaultY) => new(name, ParamKind.IntPair, min, max, [], ParamValue.Of(defaultX, defaultY));

    /// <summary>An sRGB colour, <c>"#rrggbb"</c>.</summary>
    public static ParamSpec Colour(string name, string @default) => new(name, ParamKind.Colour, 0, 0, [], ParamValue.Of(@default));

    /// <summary>A list of <paramref name="minCount"/> to <paramref name="maxCount"/> numbers, each from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static ParamSpec Floats(string name, int minCount, int maxCount, double min, double max, params double[] @default) =>
        new(name, ParamKind.Floats, min, max, [], ParamValue.Of(@default), minCount, maxCount);

    /// <summary>A list of <paramref name="minCount"/> to <paramref name="maxCount"/> sRGB colours, each <c>"#rrggbb"</c>.</summary>
    public static ParamSpec Colours(string name, int minCount, int maxCount, params string[] @default) =>
        new(name, ParamKind.Colours, 0, 0, [], @default.Length == 0 ? ParamValue.Of(Array.Empty<double>()) : ParamValue.OfTexts(@default), minCount, maxCount);

    /// <summary>An optional name, <c>[a-z][a-z0-9_]*</c>, with no default.</summary>
    public static ParamSpec Identifier(string name) => new(name, ParamKind.Name, 0, 0, [], ParamValue.Of(""), optional: true);

    /// <summary>An optional grey value from 0 to 1 or a colour <c>"#rrggbb"</c>, with no default (an <c>input</c>'s <c>default</c>, T-140).</summary>
    public static ParamSpec GreyOrColour(string name) => new(name, ParamKind.GreyOrColour, 0, 1, [], ParamValue.Of(0), optional: true);

    /// <summary>A path to a <c>.texgraph</c> file (a <c>graph</c> node's <c>path</c>, T-140), with no default.</summary>
    public static ParamSpec FilePath(string name) => new(name, ParamKind.FilePath, 0, 0, [], ParamValue.Of(""), optional: true);

    /// <summary>What a value must be, for a message: "a whole number from 1 to 4".</summary>
    public string Describe() => Kind switch
    {
        ParamKind.Int => $"a whole number from {N(Min)} to {N(Max)}",
        ParamKind.Float => $"a number from {N(Min)} to {N(Max)}",
        ParamKind.Enum => "one of " + string.Join(", ", Choices),
        ParamKind.IntPair => $"two whole numbers [x, y], each from {N(Min)} to {N(Max)}",
        ParamKind.Floats => $"a list of {Count()} numbers [a, b, ...], each from {N(Min)} to {N(Max)}",
        ParamKind.Colours => $"a list of {Count()} colours [\"#rrggbb\", ...]",
        ParamKind.Name => $"a name, {GraphNames.Pattern} (lower case letters, digits and _, starting with a letter)",
        ParamKind.GreyOrColour => $"a number from {N(Min)} to {N(Max)} for a grey input, or a colour \"#rrggbb\" for a colour one",
        ParamKind.FilePath => "a path to a .texgraph file, relative to this graph's file",
        _ => "a colour \"#rrggbb\"",
    };

    private string Count() => MinCount == MaxCount ? N(MinCount) : $"{N(MinCount)} to {N(MaxCount)}";

    /// <summary>Null when <paramref name="value"/> is right; otherwise what is wrong, giving the range.</summary>
    public string? Check(ParamValue value)
    {
        var wrong = $"is {value}, but must be {Describe()}";
        return Kind switch
        {
            ParamKind.Int => value.Kind == ParamValueKind.Number && IsWhole(value.Number) && InRange(value.Number) ? null : wrong,
            ParamKind.Float => value.Kind == ParamValueKind.Number && InRange(value.Number) ? null : wrong,
            ParamKind.Enum => value.Kind == ParamValueKind.Text && Choices.Contains(value.Text) ? null : wrong,
            ParamKind.IntPair => value.Kind == ParamValueKind.Numbers && value.Numbers.Length == 2 && value.Numbers.All(n => IsWhole(n) && InRange(n)) ? null : wrong,
            ParamKind.Floats => value.Kind == ParamValueKind.Numbers && InCount(value.Numbers.Length) && value.Numbers.All(InRange) ? null : wrong,
            ParamKind.Colours => (value.Kind == ParamValueKind.Texts && InCount(value.Texts.Length) && value.Texts.All(t => TryParseColour(t, out _)))
                || (value.Kind == ParamValueKind.Numbers && value.Numbers.Length == 0 && InCount(0)) ? null : wrong,
            ParamKind.Name => value.Kind == ParamValueKind.Text && GraphNames.IsValid(value.Text!) ? null : wrong,
            ParamKind.GreyOrColour => (value.Kind == ParamValueKind.Number && InRange(value.Number)) || (value.Kind == ParamValueKind.Text && TryParseColour(value.Text!, out _)) ? null : wrong,
            ParamKind.FilePath => value.Kind == ParamValueKind.Text && IsRelativePath(value.Text!) ? null : wrong,
            _ => value.Kind == ParamValueKind.Text && TryParseColour(value.Text!, out _) ? null : wrong,
        };
    }

    /// <summary>
    /// A <see cref="ParamKind.FilePath"/> is relative to the using graph's file (T-140), <c>..</c> allowed: one that is
    /// empty or rooted (absolute, a drive's own (<c>C:x</c>), from the root (<c>/x</c>) or UNC (<c>//server/share</c>)) is
    /// not, on any system, since a file handed on would otherwise open what it names elsewhere (RV-76).
    /// </summary>
    public static bool IsRelativePath(string text) =>
        text.Length > 0 && text[0] is not ('/' or (char)92) && !(text.Length >= 2 && text[1] == ':' && char.IsAsciiLetter(text[0])) && !Path.IsPathRooted(text);

    /// <summary>Parses <c>"#rrggbb"</c> (lower or upper case hex).</summary>
    public static bool TryParseColour(string text, out (byte R, byte G, byte B) colour)
    {
        colour = default;
        if (text.Length != 7 || text[0] != '#' || !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v))
            return false;
        colour = ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    private bool InRange(double v) => v >= Min && v <= Max;

    private bool InCount(int n) => n >= MinCount && n <= MaxCount;

    private static bool IsWhole(double v) => v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;

    private static string N(double v) => TexGraphFile.FormatNumber(v);
}

/// <summary>
/// A node's parameters as its evaluation reads them: the file's value where it is right, the default otherwise (a
/// wrong value is a fault, so a graph that evaluates has none).
/// </summary>
public sealed class NodeParameters
{
    private readonly NodeType _type;
    private readonly IReadOnlyDictionary<string, ParamValue> _values;

    public NodeParameters(NodeType type, IReadOnlyDictionary<string, ParamValue> values)
    {
        _type = type;
        _values = values;
    }

    public ParamValue Value(string name)
    {
        var spec = _type.Parameter(name) ?? throw new ArgumentException($"type '{_type.Name}' has no parameter '{name}'", nameof(name));
        return _values.TryGetValue(name, out var v) && spec.Check(v) is null ? v : spec.Default;
    }

    public int Int(string name) => (int)Value(name).Number;

    public float Float(string name) => (float)Value(name).Number;

    public string Text(string name) => Value(name).Text!;

    public (int X, int Y) IntPair(string name)
    {
        var n = Value(name).Numbers;
        return ((int)n[0], (int)n[1]);
    }

    public (byte R, byte G, byte B) Colour(string name)
    {
        ParamSpec.TryParseColour(Value(name).Text!, out var c);
        return c;
    }

    /// <summary>A <see cref="ParamKind.Floats"/> list, as <c>float</c>s.</summary>
    public float[] Floats(string name) => [.. Value(name).Numbers.Select(n => (float)n)];

    /// <summary>A <see cref="ParamKind.Colours"/> list, each as sRGB bytes.</summary>
    public (byte R, byte G, byte B)[] Colours(string name) =>
        [.. Value(name).Texts.Select(t => { ParamSpec.TryParseColour(t, out var c); return c; })];

    /// <summary>True when the node gives the parameter, and gives it rightly; for an optional parameter, whether it has one at all.</summary>
    public bool Has(string name) =>
        _type.Parameter(name) is { } spec ? _values.TryGetValue(name, out var v) && spec.Check(v) is null
            : throw new ArgumentException($"type '{_type.Name}' has no parameter '{name}'", nameof(name));
}
