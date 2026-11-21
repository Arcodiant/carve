using Carve.Debug;

namespace Carve.Scenario.Patina;

/// <summary>One stage of patina's scenario: a class in its own file, starting from its own define_graph or open.</summary>
internal interface IPatinaStage
{
    Task RunAsync(ScenarioDriver driver, PatinaChecks checks);
}

/// <summary>The stages, in the order they run. A later task adds its own file and one line here.</summary>
internal static class PatinaStages
{
    public static readonly IReadOnlyList<IPatinaStage> All =
    [
        new GraphStage(),
        new ExportStage(),
        new SwatchStage(),
        new NestedGraphStage(),
        new WideSheetStage(),
        new LibraryStage(),
    ];

    /// <summary>The key of the configuration (<c>--Patina:Stages=</c> on the command line) that picks a suite's share of the run (T-172); unset runs everything.</summary>
    public const string SelectionKey = "Patina:Stages";

    /// <summary>The stages of one suite: <c>base</c> every stage but the library's, <c>library-1</c> and <c>library-2</c> half of its samples each (the second with the planted faults); none or <c>all</c> every stage; anything else is refused.</summary>
    public static IReadOnlyList<IPatinaStage> For(string? selection) => selection switch
    {
        "base" => [.. All.Where(s => s is not LibraryStage)],
        "library-1" => [new LibraryStage(1)],
        "library-2" => [new LibraryStage(2)],
        null or "" or "all" => All,
        _ => throw new ArgumentException($"{SelectionKey} is '{selection}': one of base, library-1, library-2, all"),
    };
}
