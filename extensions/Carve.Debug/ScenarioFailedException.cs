namespace Carve.Debug;

/// <summary>What <see cref="ScenarioDriver.Fail"/> throws. A scenario has no reason to catch it.</summary>
public sealed class ScenarioFailedException(string message) : Exception(message);
