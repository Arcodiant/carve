// T-94: the Cuts ray test (CutsGeometry.cs) and the shed's camera (ShedGeometry.cs) are linked here to judge the rover's
// frames. Each has a ReadAsync that reads get_mesh through a scenario's driver; these tests build their Solid from
// Model.CombinedOf instead and never call it, so the driver and the call it makes are stubs that only let the files compile.

namespace Carve.Debug
{
    /// <summary>Stands in for the debug extension's driver, which these tests do not use.</summary>
    internal sealed class ScenarioDriver;
}

namespace Carve.Scenario.Shed
{
    /// <summary>The one member of the shed's checks the linked files name; never called here.</summary>
    internal static class Checks
    {
        public static Task<string> CallTextAsync(Carve.Debug.ScenarioDriver driver, string tool, object arguments) =>
            throw new NotSupportedException("the server tests build the Solid from Model.CombinedOf, not from a scenario's get_mesh");

    }
}
