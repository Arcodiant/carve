using System.Globalization;

namespace Patina.Graphs.Checks;

/// <summary>One fault: the rule's name, the output it is in, and the message with its fix in words.</summary>
public sealed record CheckProblem(string Check, string Output, string Message);

/// <summary>A rule of check. Each is a class with its name and thresholds, and is registered once in <see cref="CheckRules.All"/>; nothing switches on a name.</summary>
public interface ICheckRule
{
    string Name { get; }

    /// <summary>The problems <paramref name="set"/> has under this rule, none when it is clean. A rule says nothing of an output it does not read.</summary>
    IEnumerable<CheckProblem> Run(CheckSet set);
}

/// <summary>The rules, in the order their problems are listed.</summary>
public static class CheckRules
{
    public static readonly IReadOnlyList<ICheckRule> All =
    [
        new BaseColourRangeRule(),
        new MetallicNotBinaryRule(),
        new NormalNotUnitRule(),
        new OneValueRule(),
        new DoesNotTileRule(),
    ];

    /// <summary>A share of texels as the messages write it: 3.2%, 41%, 0.15%.</summary>
    public static string Percent(long count, long total)
    {
        var share = count * 100.0 / total;
        return share.ToString(share < 10 ? "0.##" : "0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Runs every rule on <paramref name="set"/>.</summary>
    public static IReadOnlyList<CheckProblem> Run(CheckSet set) => [.. All.SelectMany(r => r.Run(set))];
}
