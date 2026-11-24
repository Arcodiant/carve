namespace Carve.Scenario.Weathered;

/// <summary>One stage of the weathered barrel: a class in its own file.</summary>
internal interface IWeatheredStage
{
    Task RunAsync(WeatheredRun run);
}

/// <summary>The stages, in the order they run. A later stage adds its own file and one line here.</summary>
internal static class WeatheredStages
{
    public static readonly IReadOnlyList<IWeatheredStage> All =
    [
        new BuildStage(),
        new SeenStage(),
        new RebakeStage(),
    ];
}
