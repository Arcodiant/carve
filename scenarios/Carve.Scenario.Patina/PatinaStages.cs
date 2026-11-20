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
    ];
}
