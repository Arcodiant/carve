using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Patina.Graphs;

/// <summary>
/// A texture graph as the <c>.texgraph</c> file holds it: a default size, a seed and nodes keyed by id. It is a value:
/// every edit gives a new graph. It holds what the file says, faults and all; <see cref="GraphValidator"/> says what is
/// wrong with it. Compare two graphs by content through <see cref="TexGraphFile.Write"/>.
/// </summary>
public sealed record TextureGraph(int Size, uint Seed, ImmutableSortedDictionary<string, GraphNode> Nodes)
{
    public const int Version = 1;
    public const int MinSize = 32;
    public const int MaxSize = 8192;

    /// <summary>A graph with no nodes.</summary>
    public static TextureGraph Empty(int size = 256, uint seed = 0) =>
        new(size, seed, ImmutableSortedDictionary.Create<string, GraphNode>(StringComparer.Ordinal));

    /// <summary>True for a power of two from 32 to 8192, the sizes a graph is evaluated at.</summary>
    public static bool IsValidSize(int size) => size is >= MinSize and <= MaxSize && (size & (size - 1)) == 0;

    /// <summary>The sizes allowed, for a message.</summary>
    public const string SizesText = "a power of two from 32 to 8192 (32, 64, 128, 256, 512, 1024, 2048, 4096 or 8192)";

    public TextureGraph WithNode(string id, GraphNode node) => this with { Nodes = Nodes.SetItem(id, node) };

    public TextureGraph WithoutNode(string id) => this with { Nodes = Nodes.Remove(id) };

    /// <summary>The exposed parameters by name (T-139, the file's <c>parameters</c>, version 2): none unless the graph declares some.</summary>
    public ImmutableSortedDictionary<string, GraphParameter> Parameters { get; init; } = ImmutableSortedDictionary.Create<string, GraphParameter>(StringComparer.Ordinal);

    public TextureGraph WithParameter(string name, GraphParameter parameter) => this with { Parameters = Parameters.SetItem(name, parameter) };

    public TextureGraph WithoutParameter(string name) => this with { Parameters = Parameters.Remove(name) };

    /// <summary>
    /// The folder a <c>graph</c> node's <c>path</c> resolves against (T-140): the graph file's own folder once read from or
    /// saved to one, the tool folder for a graph never saved, the working directory when null. Never written to the file.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>The exposed parameter driving <paramref name="node"/>'s <paramref name="param"/>, or null (the first in name order, should two claim it).</summary>
    public string? DriverOf(string node, string param) =>
        Parameters.FirstOrDefault(p => p.Value.Drives.Contains($"{node}.{param}")).Key;
}

/// <summary>
/// One node: its type's name, its inputs (input port to <c>"&lt;id&gt;"</c> or <c>"&lt;id&gt;.&lt;port&gt;"</c>) and the
/// parameters the file gives (a missing one takes its default).
/// </summary>
public sealed record GraphNode(string Type, ImmutableSortedDictionary<string, string> Inputs, ImmutableSortedDictionary<string, ParamValue> Params)
{
    public static GraphNode Of(string type, IEnumerable<KeyValuePair<string, string>>? inputs = null,
        IEnumerable<KeyValuePair<string, ParamValue>>? parameters = null) =>
        new(type,
            ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, inputs ?? []),
            ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, parameters ?? []));

    public GraphNode WithInput(string port, string source) => this with { Inputs = Inputs.SetItem(port, source) };

    public GraphNode WithParam(string name, ParamValue value) => this with { Params = Params.SetItem(name, value) };
}

/// <summary>Ids and port names: <c>[a-z][a-z0-9_]*</c>.</summary>
public static partial class GraphNames
{
    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex NameRegex();

    public const string Pattern = "[a-z][a-z0-9_]*";

    public static bool IsValid(string name) => NameRegex().IsMatch(name);

    /// <summary>An expanded node's id (T-140): names joined by <c>/</c>, <c>wall/layout</c>; a plain id is one too.</summary>
    public static bool IsValidPath(string id) => id.Split('/').All(IsValid);

    /// <summary>As <see cref="TryParseSource(string, out string, out string?)"/>, the node part an expanded id (<c>wall/layout.out</c>).</summary>
    public static bool TryParsePathSource(string source, out string node, out string? port)
    {
        var dot = source.IndexOf('.');
        node = dot < 0 ? source : source[..dot];
        port = dot < 0 ? null : source[(dot + 1)..];
        return IsValidPath(node) && (port is null || IsValid(port));
    }

    /// <summary>Splits an input's source, <c>"id"</c> or <c>"id.port"</c>; false when it has neither shape.</summary>
    public static bool TryParseSource(string source, out string node, out string? port)
    {
        var dot = source.IndexOf('.');
        node = dot < 0 ? source : source[..dot];
        port = dot < 0 ? null : source[(dot + 1)..];
        return IsValid(node) && (port is null || IsValid(port));
    }
}

/// <summary>What a parameter holds in the file: a number, a string, an array of numbers or an array of strings (an empty array is <see cref="Numbers"/>).</summary>
public enum ParamValueKind { Number, Text, Numbers, Texts }

/// <summary>A parameter's value as written, before its type's <c>ParamSpec</c> reads it.</summary>
public sealed class ParamValue : IEquatable<ParamValue>
{
    private ParamValue(ParamValueKind kind, double number, string? text, ImmutableArray<double> numbers, ImmutableArray<string> texts = default)
    {
        Kind = kind; Number = number; Text = text; Numbers = numbers; Texts = texts.IsDefault ? [] : texts;
    }

    public ParamValueKind Kind { get; }
    public double Number { get; }
    public string? Text { get; }
    public ImmutableArray<double> Numbers { get; }
    public ImmutableArray<string> Texts { get; }

    public static ParamValue Of(double number) =>
        double.IsFinite(number) ? new(ParamValueKind.Number, number, null, []) : throw new ArgumentOutOfRangeException(nameof(number), "a parameter is a finite number");

    public static ParamValue Of(string text) => new(ParamValueKind.Text, 0, text, []);

    public static ParamValue Of(params double[] numbers) =>
        numbers.All(double.IsFinite) ? new(ParamValueKind.Numbers, 0, null, [.. numbers]) : throw new ArgumentOutOfRangeException(nameof(numbers), "a parameter is a finite number");

    /// <summary>An array of strings, at least one (an empty array is <see cref="Of(double[])"/> of none).</summary>
    public static ParamValue OfTexts(params string[] texts) =>
        texts.Length > 0 ? new(ParamValueKind.Texts, 0, null, [], [.. texts]) : throw new ArgumentException("an empty array is a list of numbers", nameof(texts));

    public bool Equals(ParamValue? other) =>
        other is not null && Kind == other.Kind && Number.Equals(other.Number) && Text == other.Text && Numbers.SequenceEqual(other.Numbers)
        && Texts.SequenceEqual(other.Texts, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ParamValue);

    public override int GetHashCode() => HashCode.Combine(Kind, Number, Text, Numbers.Length);

    public override string ToString() => Kind switch
    {
        ParamValueKind.Number => TexGraphFile.FormatNumber(Number),
        ParamValueKind.Text => $"\"{Text}\"",
        ParamValueKind.Texts => "[" + string.Join(", ", Texts.Select(t => $"\"{t}\"")) + "]",
        _ => "[" + string.Join(", ", Numbers.Select(TexGraphFile.FormatNumber)) + "]",
    };
}
