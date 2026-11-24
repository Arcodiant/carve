namespace Carve.Scenario.Weathered;

/// <summary>One stage of the weathered barrel: a class in its own file.</summary>
internal interface IWeatheredStage
{
    Task RunAsync(WeatheredRun run);
}

/// <summary>The stages, in the order they run. A later stage adds its own file and one line here.</summary>
internal static class WeatheredStages
{
    /// <summary>The key of the configuration (<c>--Weathered:Stages=</c> on the command line) that picks a suite's share of the run, as T-172 did for patina.</summary>
    public const string SelectionKey = "Weathered:Stages";

    public static IReadOnlyList<IWeatheredStage> All =>
    [
        new BuildStage(),
        new SeenStage(256, 1024, WeatheredRun.BakeSize),
        new RebakeStage(),
        new ErrorsStage(),
        new SavedStage(),
    ];

    /// <summary>
    /// The stages of one suite: <c>main</c> (suite <c>scenario-weathered</c>) stage 2 at 512 only and every stage after it; <c>sizes</c> (suite
    /// <c>scenario-weathered-sizes</c>) stage 2 at 256 and 1024 alone; none or <c>all</c> everything; anything else is refused.
    /// </summary>
    public static IReadOnlyList<IWeatheredStage> For(string? selection) => selection switch
    {
        "main" => [new BuildStage(), new SeenStage(WeatheredRun.BakeSize), new RebakeStage(), new ErrorsStage(), new SavedStage()],
        "sizes" => [new BuildStage(), new SeenStage(256, 1024)],
        null or "" or "all" => All,
        _ => throw new ArgumentException($"{SelectionKey} is '{selection}': one of main, sizes, all"),
    };
}
