using Carve.Debug;

namespace Carve.Scenario.Bakes;

/// <summary>One stage of the bakes: a class in its own file.</summary>
internal interface IBakesStage
{
    Task RunAsync(ScenarioDriver driver);
}

/// <summary>The stages, in the order they run. A later bake adds its own file and one line here.</summary>
internal static class BakesStages
{
    public static readonly IReadOnlyList<IBakesStage> All =
    [
        new SurfaceStage(),
        new CurvatureStage(),
        new OcclusionStage(),
        new BevelStage(),
    ];
}
