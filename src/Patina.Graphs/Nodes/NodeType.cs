using System.Globalization;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// A kind of node, registered in a <see cref="NodeRegistry"/> rather than switched on: its ports, its parameters, its
/// neighbourhood radius and its evaluation. One class per type, in a file of its own. A type made outside this package
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
    /// How many texels around a texel the node reads from its inputs, in each direction (0 for a pointwise node). The
    /// evaluator asks its inputs for these many more rows above and below the band, wrapping around the image.
    /// </summary>
    public virtual int Radius(NodeParameters parameters, int size) => 0;

    /// <summary>What an output port carries, given what each connected input carries (an absent optional input is not in <paramref name="inputs"/>).</summary>
    public abstract PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters);

    /// <summary>
    /// Faults about what the inputs carry that a port's own <see cref="InputPort.Kind"/> cannot say (the output node's
    /// <c>in</c> depends on its role): the port and a message, which the validator prefixes with the node and port.
    /// </summary>
    public virtual IEnumerable<(string Port, string Message)> CheckInputKinds(IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => [];

    /// <summary>
    /// Fills rows <see cref="NodeEvaluation.Y0"/> to <see cref="NodeEvaluation.Y0"/> + <see cref="NodeEvaluation.Rows"/> - 1,
    /// every column, of each output band, from input bands that hold those rows widened by <see cref="Radius"/>.
    /// </summary>
    public abstract void Evaluate(NodeEvaluation evaluation);

    public ParamSpec? Parameter(string name) => Parameters.FirstOrDefault(p => p.Name == name);

    public InputPort? Input(string name) => Inputs.FirstOrDefault(p => p.Name == name);
}

/// <summary>An input port: its name, what it takes, and whether a graph must connect it.</summary>
public sealed record InputPort(string Name, PortKind Kind, bool Required = true);

public enum ParamKind { Int, Float, Enum, IntPair, Colour }

/// <summary>
/// A parameter: its name, kind, range or choices, and default. <see cref="Check"/> says what is wrong with a value in
/// words that give the range. Make one with <see cref="Int"/>, <see cref="Float"/>, <see cref="Enum"/>,
/// <see cref="IntPair"/> or <see cref="Colour"/>.
/// </summary>
public sealed class ParamSpec
{
    private ParamSpec(string name, ParamKind kind, double min, double max, IReadOnlyList<string> choices, ParamValue @default)
    {
        Name = name; Kind = kind; Min = min; Max = max; Choices = choices; Default = @default;
        if (Check(@default) is { } wrong) throw new ArgumentException($"parameter '{name}': its default {wrong}");
    }

    public string Name { get; }
    public ParamKind Kind { get; }
    public double Min { get; }
    public double Max { get; }
    public IReadOnlyList<string> Choices { get; }
    public ParamValue Default { get; }

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

    /// <summary>What a value must be, for a message: "a whole number from 1 to 4".</summary>
    public string Describe() => Kind switch
    {
        ParamKind.Int => $"a whole number from {N(Min)} to {N(Max)}",
        ParamKind.Float => $"a number from {N(Min)} to {N(Max)}",
        ParamKind.Enum => "one of " + string.Join(", ", Choices),
        ParamKind.IntPair => $"two whole numbers [x, y], each from {N(Min)} to {N(Max)}",
        _ => "a colour \"#rrggbb\"",
    };

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
            _ => value.Kind == ParamValueKind.Text && TryParseColour(value.Text!, out _) ? null : wrong,
        };
    }

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
}
