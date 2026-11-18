namespace Carve.Server.Tests;

[Trait("Tier", "unit")]
public class EngineHostOptionsTests
{
    [Fact]
    public void TheEngineDefaultsCannotBeWrittenThroughACast()
    {
        // A writable dictionary behind the read-only type would let one caller change every later RunAsync's defaults.
        var defaults = Assert.IsAssignableFrom<IDictionary<string, string?>>(EngineHostOptions.EngineDefaults);
        Assert.True(defaults.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => defaults["Empyrean:Time:FixedDeltaTime"] = "1");
        Assert.Equal("0.05", EngineHostOptions.EngineDefaults["Empyrean:Time:FixedDeltaTime"]);
    }
}
