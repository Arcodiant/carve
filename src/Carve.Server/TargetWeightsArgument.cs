using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

/// <summary>Reads the <c>targets</c> argument of <c>render</c> and <c>check</c> into <see cref="TargetWeights"/>.</summary>
internal static class TargetWeightsArgument
{
    public const string Description =
        "Target weights: an object of morph target name to weight, 0 to 1 (define_target makes targets). Each mesh with a target of that name is morphed by it, "
        + "before any pose or clip skins it, so the vertices are base + the weight times the target's delta; a mesh without it is unchanged. "
        + "Refused: a name no mesh has (the targets are listed), a weight below 0 or above 1 or not a number. Carve stores no range; this one is for looking.";

    public static JsonObject Schema() => new()
    {
        ["description"] = Description,
        ["type"] = "object",
        ["additionalProperties"] = new JsonObject { ["type"] = "number" },
    };

    /// <summary>The weights given, or null when none (absent or <c>{}</c>); problems are added to <paramref name="errors"/>.</summary>
    public static TargetWeights? Read(JsonElement? targets, Model model, List<string> errors)
    {
        if (Absent(targets)) return null;
        if (targets!.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"targets: expected an object of target name to weight, got {Describe(targets.Value)}");
            return null;
        }

        var found = new Dictionary<string, float>(StringComparer.Ordinal);
        var before = errors.Count;
        var names = model.TargetNames;
        foreach (var t in targets.Value.EnumerateObject())
        {
            var at = $"targets.{t.Name}";
            if (t.Value.ValueKind != JsonValueKind.Number)
            {
                errors.Add($"{at}: expected a number, got {Describe(t.Value)}");
                continue;
            }

            if (!names.Contains(t.Name))
            {
                errors.Add(names.Count == 0
                    ? $"{at}: no mesh has a target named '{t.Name}'; the model has no targets (define_target makes one)"
                    : $"{at}: no mesh has a target named '{t.Name}'; the targets are {NameList.Quoted(names)}");
                continue;
            }

            var weight = t.Value.TryGetDouble(out var d) ? d : double.NaN;
            if (!double.IsFinite(weight) || !float.IsFinite((float)weight))
                errors.Add($"{at}: the weight is not a finite number");
            else if (weight < 0 || weight > 1)
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{at}: weight {weight:R} is outside 0 to 1"));
            else
                found[t.Name] = (float)weight;
        }

        return errors.Count > before || found.Count == 0 ? null : new TargetWeights(found);
    }
}
