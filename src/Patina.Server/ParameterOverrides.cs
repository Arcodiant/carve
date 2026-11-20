using System.Text.Json;
using Patina.Graphs;
using Patina.Graphs.Nodes;
using static Patina.Server.ToolSupport;

namespace Patina.Server;

/// <summary>
/// The <c>parameters</c> argument of swatch and export (T-139), and of check (T-141): an object of the graph's exposed
/// parameters by name to values, set on a copy of the graph through <see cref="GraphParameters.WithOverrides"/>. The open
/// graph is not changed.
/// </summary>
public static class ParameterOverrides
{
    /// <summary>The schema's description of the argument.</summary>
    public const string Description =
        "Optional: the graph's exposed parameters to set for this call, by name, such as {\"mortar\": 0.08, \"tint\": \"#7a7a70\"}; the graph is not changed. get_graph lists them with their ranges.";

    /// <summary>
    /// Reads <paramref name="value"/> (absent or null: no overrides) and applies it to <paramref name="graph"/>. Returns
    /// false with <paramref name="error"/> ("&lt;tool&gt;: parameters['mortar']: is 0.5, but must be a number from 0 to
    /// 0.45") when it is not an object of values, names no parameter, or a value is out of its range.
    /// </summary>
    public static bool TryApply(string tool, TextureGraph graph, JsonElement? value, NodeRegistry registry,
        out TextureGraph result, out IReadOnlyDictionary<string, ParamValue> overrides, out string? error)
    {
        result = graph;
        overrides = new Dictionary<string, ParamValue>();
        error = null;
        if (value is not { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } given) return true;
        if (given.ValueKind != JsonValueKind.Object)
        {
            error = $"{tool}: parameters is an object of the graph's parameters by name to values, such as {{\"mortar\": 0.08}}, not {Kind(given)}";
            return false;
        }
        var read = new Dictionary<string, ParamValue>(StringComparer.Ordinal);
        foreach (var p in given.EnumerateObject())
        {
            if (Read(p.Value) is not { } v)
            {
                error = $"{tool}: {GraphParameters.Where(p.Name)}: is {Kind(p.Value)}; a value is a number, a string or an array of numbers";
                return false;
            }
            read[p.Name] = v;
        }
        var applied = GraphParameters.WithOverrides(graph, read, registry);
        if (!applied.Ok)
        {
            error = $"{tool}: {applied.Error}";
            return false;
        }
        result = applied.Graph!;
        overrides = read;
        return true;
    }

    /// <summary>A JSON value as a parameter value: a number, a string or an array of numbers; null for anything else.</summary>
    public static ParamValue? Read(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number when double.IsFinite(v.GetDouble()) => ParamValue.Of(v.GetDouble()),
        JsonValueKind.String => ParamValue.Of(v.GetString()!),
        JsonValueKind.Array when v.EnumerateArray().All(e => e.ValueKind == JsonValueKind.Number) =>
            ParamValue.Of([.. v.EnumerateArray().Select(e => e.GetDouble())]),
        _ => null,
    };
}
