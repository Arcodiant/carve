namespace Carve.Debug;

/// <summary>
/// One scripted situation. A scenario extension registers exactly one in its
/// <c>ConfigureHost</c>; a run with none, or with more than one, is fatal.
/// </summary>
/// <remarks>
/// The run ends when this returns, passing, or when it throws or calls
/// <see cref="ScenarioDriver.Fail"/>, failing. The run's name, which names its folder, is the last
/// dotted part of the scenario's assembly name, lower-cased: <c>Carve.Scenario.Cube</c> is
/// <c>cube</c>.
/// </remarks>
public interface IScenario
{
    Task RunAsync(ScenarioDriver driver);
}
