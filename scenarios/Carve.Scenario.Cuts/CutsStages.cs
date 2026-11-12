using Carve.Debug;

namespace Carve.Scenario.Cuts;

/// <summary>One stage of the cuts: a class in its own file, starting from its own <c>define_mesh</c>.</summary>
internal interface ICutsStage
{
    Task RunAsync(ScenarioDriver driver);
}

/// <summary>The stages, in the order they run. A later cut adds its own file and one line here.</summary>
internal static class CutsStages
{
    public static readonly IReadOnlyList<ICutsStage> All =
    [
        new HolesStage(),
        new DeleteStage(),
        new MergeBridgeStage(),
        new PlaneCutStage(),
        new LoopCutStage(),
        new KnifeStage(),
    ];
}
