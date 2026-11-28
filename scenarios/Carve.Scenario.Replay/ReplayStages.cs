using Carve.Debug;

namespace Carve.Scenario.Replay;

/// <summary>One stage of the replay scenario: a class in its own file, starting from its own <c>clear</c>.</summary>
internal interface IReplayStage
{
    Task RunAsync(ScenarioDriver driver);
}

/// <summary>The stages, in the order they run. Stage 5 replays stage 1's script, so stage 1 is first.</summary>
internal static class ReplayStages
{
    public static readonly IReadOnlyList<IReplayStage> All =
    [
        new EveryToolStage(),
        new JournalStage(),
        new NumbersPathsStage(),
        new AtomicStage(),
        new BreaksStage(),
    ];
}
