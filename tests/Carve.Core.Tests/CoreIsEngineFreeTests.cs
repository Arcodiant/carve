using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class CoreIsEngineFreeTests
{
    [Fact]
    public void CoreReferencesNoEngineAssembly()
    {
        var engine = typeof(CoreAssembly).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name!.StartsWith("Empyrean", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(engine);
    }
}
