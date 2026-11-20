using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Patina.Graphs.Nodes;

namespace Patina.Graphs;

/// <summary>
/// One exposed parameter of a graph (T-139), as the file's <c>parameters</c> holds it: its value, the node parameters it
/// drives (<c>"&lt;node&gt;.&lt;param&gt;"</c>, in the order given) and an optional <c>min</c> and <c>max</c> narrowing
/// the range the driven parameters share. Every driven parameter takes <see cref="Value"/>; its own value in its node is
/// ignored while it is driven, and the canonical writer leaves it out.
/// </summary>
public sealed record GraphParameter(ParamValue Value, ImmutableArray<string> Drives, double? Min = null, double? Max = null)
{
    public static GraphParameter Of(ParamValue value, params string[] drives) => new(value, [.. drives]);

    /// <summary>Splits a drive, <c>"node.param"</c>; false when it has not that shape.</summary>
    public static bool TryParseDrive(string drive, out string node, out string param)
    {
        var dot = drive.IndexOf('.');
        node = dot < 0 ? drive : drive[..dot];
        param = dot < 0 ? "" : drive[(dot + 1)..];
        return dot > 0 && GraphNames.IsValid(node) && GraphNames.IsValid(param);
    }
}

/// <summary>An exposed parameter as validation reads it: its kind, its range as a <see cref="ParamSpec"/> named after it (so <c>Range.Check</c> and <c>Range.Describe</c> give its words), its value and what it drives.</summary>
public sealed record ExposedParameter(string Name, ParamKind Kind, ParamSpec Range, ParamValue Value, ImmutableArray<string> Drives)
{
    /// <summary>The kind in words: "a number", "a whole number", "a choice", "two whole numbers" or "a colour".</summary>
    public string KindText => GraphParameters.KindText(Kind);
}

/// <summary>The outcome of <see cref="GraphParameters.WithOverrides"/>: the graph with the values set, or what is wrong with the first override at fault.</summary>
public sealed record OverrideResult(TextureGraph? Graph, string? Error)
{
    public bool Ok => Graph is not null;
}

/// <summary>
/// A graph's exposed parameters (T-139): reading them against the nodes they drive, and setting them by name. A
/// parameter drives node parameters of one kind (<see cref="ParamKind.Int"/>, <see cref="ParamKind.Float"/>,
/// <see cref="ParamKind.Enum"/>, <see cref="ParamKind.IntPair"/> or <see cref="ParamKind.Colour"/>; a list or a name
/// cannot be driven) and takes the narrowest range of them: the intersection of the numbers' ranges, narrowed again by its
/// own <c>min</c> and <c>max</c>, or an enum's common choices in the first's order.
/// <para><see cref="WithOverrides"/> is the one function swatch, export and check (T-141) set values through; T-140's
/// <c>graph</c> node checks a value set on it against <see cref="Of"/>'s <see cref="ExposedParameter.Range"/>.</para>
/// </summary>
public static class GraphParameters
{
    /// <summary>The graph's parameters that are not at fault, by name. A parameter at fault (see <see cref="GraphValidator"/>) is left out.</summary>
    public static IReadOnlyDictionary<string, ExposedParameter> Of(TextureGraph graph, NodeRegistry? registry = null) =>
        Analyse(graph, registry ?? NodeRegistry.BuiltIn).Exposed;

    /// <summary>
    /// <paramref name="graph"/> with each parameter in <paramref name="overrides"/> set to its value, the graph itself
    /// unchanged: the one way a tool sets parameters without editing the graph. An override naming no parameter, a
    /// parameter at fault, or a value out of its range gives <see cref="OverrideResult.Error"/>, in validation's words:
    /// <c>parameters['mortar']: is 0.5, but must be a number from 0 to 0.2</c>. Overrides are taken in name order.
    /// </summary>
    public static OverrideResult WithOverrides(TextureGraph graph, IReadOnlyDictionary<string, ParamValue> overrides, NodeRegistry? registry = null)
    {
        if (overrides.Count == 0) return new(graph, null);
        var analysis = Analyse(graph, registry ?? NodeRegistry.BuiltIn);
        var result = graph;
        foreach (var (name, value) in overrides.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!graph.Parameters.TryGetValue(name, out var declared))
                return new(null, $"{Where(name)}: the graph has no such parameter; its parameters are {(graph.Parameters.Count == 0 ? "none" : string.Join(", ", graph.Parameters.Keys))}");
            if (!analysis.Exposed.TryGetValue(name, out var exposed))
                return new(null, $"{Where(name)}: is at fault, so it cannot be set: {string.Join("; ", analysis.Faults.Where(f => f.GraphParameter == name).Select(f => f.Message))}");
            if (exposed.Range.Check(value) is { } wrong) return new(null, $"{Where(name)}: {wrong}");
            result = result.WithParameter(name, declared with { Value = value });
        }
        return new(result, null);
    }

    /// <summary>The overrides in words, for a title or a caption: <c>mortar 0.08, tint "#7a7a70"</c> in name order.</summary>
    public static string Describe(IReadOnlyDictionary<string, ParamValue> overrides) =>
        string.Join(", ", overrides.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"));

    /// <summary>A parameter's place in a message: <c>parameters['mortar']</c>.</summary>
    public static string Where(string name) => $"parameters['{name}']";

    public static string KindText(ParamKind kind) => kind switch
    {
        ParamKind.Int => "a whole number",
        ParamKind.Float => "a number",
        ParamKind.Enum => "a choice",
        ParamKind.IntPair => "two whole numbers",
        ParamKind.Colour => "a colour",
        ParamKind.Floats => "a list of numbers",
        ParamKind.Colours => "a list of colours",
        _ => "a name",
    };

    private static bool CanDrive(ParamKind kind) => kind is ParamKind.Int or ParamKind.Float or ParamKind.Enum or ParamKind.IntPair or ParamKind.Colour;

    private static bool IsNumeric(ParamKind kind) => kind is ParamKind.Int or ParamKind.Float or ParamKind.IntPair;

    internal static ParameterAnalysis Analyse(TextureGraph graph, NodeRegistry registry)
    {
        var a = new ParameterAnalysis();
        foreach (var (name, parameter) in graph.Parameters)
        {
            var before = a.Faults.Count;
            void Fault(string message) => a.Faults.Add(new(null, null, null, $"{Where(name)}: {message}", name));

            if (!GraphNames.IsValid(name))
                Fault($"a parameter's name is {GraphNames.Pattern} (lower case letters, digits and _, starting with a letter)");
            if (parameter.Drives.Length == 0)
                Fault("drives nothing; drives is a list of the node parameters it sets, each \"<node>.<param>\"");

            var driven = new List<(string Drive, string Node, ParamSpec Spec)>();
            var complete = true; // false when a drive names a node of unknown type, whose own fault says so
            foreach (var drive in parameter.Drives)
            {
                if (!GraphParameter.TryParseDrive(drive, out var id, out var param))
                {
                    Fault($"drives '{drive}', which is not \"<node>.<param>\"");
                    continue;
                }
                if (!graph.Nodes.TryGetValue(id, out var node))
                {
                    Fault($"drives '{drive}', but there is no node '{id}'; the nodes are {(graph.Nodes.Count == 0 ? "none" : string.Join(", ", graph.Nodes.Keys))}");
                    continue;
                }
                if (registry.Find(node.Type) is not { } type) { complete = false; continue; }
                if (type.Parameter(param) is not { } spec)
                {
                    Fault($"drives '{drive}', but '{id}', a node of type {type.Name}, has no parameter '{param}'; its parameters are {string.Join(", ", type.Parameters.Select(p => p.Name))}");
                    continue;
                }
                if (a.DrivenBy.TryGetValue((id, param), out var other))
                {
                    Fault(other == name
                        ? $"drives '{drive}' twice; name each node parameter once"
                        : $"drives '{drive}', which {Where(other)} drives already; a node parameter has one driver");
                    continue;
                }
                a.DrivenBy[(id, param)] = name;
                if (!CanDrive(spec.Kind))
                {
                    Fault($"drives '{drive}', {KindText(spec.Kind)}, which a graph parameter cannot drive; it drives {KindText(ParamKind.Int)}, {KindText(ParamKind.Float)}, {KindText(ParamKind.Enum)}, {KindText(ParamKind.IntPair)} or {KindText(ParamKind.Colour)}");
                    continue;
                }
                driven.Add((drive, id, spec));
            }

            if (driven.Count > 0 && driven.FirstOrDefault(d => d.Spec.Kind != driven[0].Spec.Kind) is { Drive: not null } odd)
                Fault($"drives '{driven[0].Drive}', {KindText(driven[0].Spec.Kind)}, and '{odd.Drive}', {KindText(odd.Spec.Kind)}; the parameters it drives are all of one kind");
            else if (driven.Count > 0)
            {
                var kind = driven[0].Spec.Kind;
                if ((parameter.Min is not null || parameter.Max is not null) && !IsNumeric(kind))
                    Fault($"min and max narrow a number's range, but it drives {KindText(kind)}");
                else if (Range(name, kind, driven, parameter) is not { } range)
                    Fault($"the parameters it drives have no value in common: {string.Join("; ", driven.Select(d => $"{d.Drive} is {d.Spec.Describe()}"))}"
                        + (parameter.Min is not null || parameter.Max is not null ? $"; and it gives {MinMax(parameter)}" : ""));
                else if (range.Check(parameter.Value) is { } wrong)
                    Fault(wrong);
                else if (a.Faults.Count == before && complete)
                    a.Exposed[name] = new ExposedParameter(name, kind, range, parameter.Value, parameter.Drives);
            }

            // Each driven parameter takes the value where it fits the node parameter's own range: so a parameter at fault
            // does not also fault the nodes it drives.
            foreach (var (_, id, spec) in driven)
                if (spec.Check(parameter.Value) is null) a.Values[(id, spec.Name)] = parameter.Value;
        }
        return a;
    }

    private static string MinMax(GraphParameter p) =>
        string.Join(" and ", new[] { p.Min is { } lo ? "min " + TexGraphFile.FormatNumber(lo) : null, p.Max is { } hi ? "max " + TexGraphFile.FormatNumber(hi) : null }.Where(s => s is not null));

    /// <summary>The narrowest range of <paramref name="driven"/>, all of <paramref name="kind"/>, as a spec named <paramref name="name"/>; null when it is empty.</summary>
    private static ParamSpec? Range(string name, ParamKind kind, List<(string Drive, string Node, ParamSpec Spec)> driven, GraphParameter parameter)
    {
        switch (kind)
        {
            case ParamKind.Enum:
            {
                var choices = driven[0].Spec.Choices.Where(c => driven.All(d => d.Spec.Choices.Contains(c))).ToArray();
                return choices.Length == 0 ? null : ParamSpec.Enum(name, choices[0], choices);
            }
            case ParamKind.Colour:
                return ParamSpec.Colour(name, "#000000");
            default:
            {
                var lo = Math.Max(driven.Max(d => d.Spec.Min), parameter.Min ?? double.NegativeInfinity);
                var hi = Math.Min(driven.Min(d => d.Spec.Max), parameter.Max ?? double.PositiveInfinity);
                if (kind == ParamKind.Float) return lo <= hi ? ParamSpec.Float(name, lo, hi, lo) : null;
                var (from, to) = (Math.Ceiling(lo), Math.Floor(hi));
                if (from > to) return null;
                var (i, j) = ((int)from, (int)to);
                return kind == ParamKind.Int ? ParamSpec.Int(name, i, j, i) : ParamSpec.IntPair(name, i, j, i, i);
            }
        }
    }
}

/// <summary>What <see cref="GraphParameters.Analyse"/> learns: the parameters' faults, each driven node parameter's driver and value, and the parameters not at fault.</summary>
internal sealed class ParameterAnalysis
{
    public List<GraphFault> Faults { get; } = [];
    public Dictionary<(string Node, string Param), string> DrivenBy { get; } = [];
    public Dictionary<(string Node, string Param), ParamValue> Values { get; } = [];
    public Dictionary<string, ExposedParameter> Exposed { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The file's <c>parameters</c> field (version 2): an object of parameters by name, one a line in name order, each
/// <c>{ "value": v, "drives": ["node.param", ...], "min": n, "max": n }</c> with min and max left out when not given.
/// </summary>
public sealed class ParametersField : TexGraphField
{
    public static ParametersField Instance { get; } = new();

    public override string Name => "parameters";

    public override int Version => 2;

    public override bool IsIn(TextureGraph graph) => graph.Parameters.Count > 0;

    public override string Write(TextureGraph graph)
    {
        var s = new StringBuilder("{\n");
        var i = 0;
        foreach (var (name, p) in graph.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            s.Append("    ").Append(TexGraphFile.Quote(name)).Append(": { \"value\": ").Append(TexGraphFile.Value(p.Value))
                .Append(", \"drives\": [").Append(string.Join(", ", p.Drives.Select(TexGraphFile.Quote))).Append(']');
            if (p.Min is { } min) s.Append(", \"min\": ").Append(TexGraphFile.FormatNumber(min));
            if (p.Max is { } max) s.Append(", \"max\": ").Append(TexGraphFile.FormatNumber(max));
            s.Append(" }").Append(++i < graph.Parameters.Count ? ",\n" : "\n");
        }
        return s.Append("  }").ToString();
    }

    public override Func<TextureGraph, TextureGraph> Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new FormatException("parameters is an object of parameters by name");
        var parameters = ImmutableSortedDictionary.CreateBuilder<string, GraphParameter>(StringComparer.Ordinal);
        while (Next(ref reader) == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            if (parameters.ContainsKey(name)) throw new FormatException($"'{name}' is given twice");
            Next(ref reader);
            parameters[name] = Parameter(ref reader, name);
        }
        var read = parameters.ToImmutable();
        return graph => graph with { Parameters = read };
    }

    private static GraphParameter Parameter(ref Utf8JsonReader reader, string name)
    {
        var where = $"'{name}'";
        if (reader.TokenType != JsonTokenType.StartObject) throw new FormatException($"{where} is an object with value and drives, and optionally min and max");
        ParamValue? value = null;
        List<string>? drives = null;
        double? min = null, max = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (Next(ref reader) == JsonTokenType.PropertyName)
        {
            var key = reader.GetString()!;
            if (!seen.Add(key)) throw new FormatException($"{where}: '{key}' is given twice");
            Next(ref reader);
            switch (key)
            {
                case "value":
                    value = Value(ref reader, where);
                    break;
                case "drives":
                    if (reader.TokenType != JsonTokenType.StartArray) throw new FormatException($"{where}: drives is an array of \"<node>.<param>\" strings");
                    drives = [];
                    while (Next(ref reader) != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType != JsonTokenType.String) throw new FormatException($"{where}: drives is an array of \"<node>.<param>\" strings");
                        drives.Add(reader.GetString()!);
                    }
                    break;
                case "min":
                case "max":
                    if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out var n) || !double.IsFinite(n))
                        throw new FormatException($"{where}: {key} is a number");
                    if (key == "min") min = n; else max = n;
                    break;
                default:
                    throw new FormatException($"{where}: unknown key '{key}': a parameter has value, drives, min and max");
            }
        }
        if (value is null) throw new FormatException($"{where} has no value");
        if (drives is null) throw new FormatException($"{where} has no drives");
        return new GraphParameter(value, [.. drives], min, max);
    }

    /// <summary>A value: a number, a string, or an array of numbers.</summary>
    private static ParamValue Value(ref Utf8JsonReader reader, string where)
    {
        var what = $"{where}: value is a number, a string or an array of numbers";
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetDouble(out var d) && double.IsFinite(d) ? ParamValue.Of(d) : throw new FormatException(what);
            case JsonTokenType.String:
                return ParamValue.Of(reader.GetString()!);
            case JsonTokenType.StartArray:
                var numbers = new List<double>();
                while (Next(ref reader) != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out var x) || !double.IsFinite(x)) throw new FormatException(what);
                    numbers.Add(x);
                }
                return ParamValue.Of([.. numbers]);
            default:
                throw new FormatException(what);
        }
    }

    private static JsonTokenType Next(ref Utf8JsonReader reader)
    {
        if (!reader.Read()) throw new FormatException("the file ends early");
        return reader.TokenType;
    }
}
