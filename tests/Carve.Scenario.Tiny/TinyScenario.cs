using Carve.Debug;
using Carve.Extensibility;
using Empyrean;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Tiny;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class TinyExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, TinyScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>Calls <c>tools/list</c>; under <c>Tiny:Mode</c> it exercises one thing the driver promises.</summary>
public sealed class TinyScenario(IConfiguration configuration, SeededRandom random, IServiceProvider services) : IScenario
{
    private static readonly object Quad = new
    {
        vertices = new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { 1, 1, 0 }, new[] { 0, 1, 0 } },
        faces = new[] { new[] { 0, 1, 2, 3 } },
    };

    public async Task RunAsync(ScenarioDriver driver)
    {
        var tools = await driver.ListToolsAsync();
        Console.Error.WriteLine($"tiny: tools {string.Join(' ', tools.Select(t => t.Name))}");

        switch (configuration["Tiny:Mode"])
        {
            case "fail":
                driver.Fail("tiny failed on purpose");
                break;

            case "swallowed-fail":
                try
                {
                    driver.Fail("tiny failed and caught it");
                }
                catch (ScenarioFailedException)
                {
                }

                break;

            case "throw":
                throw new InvalidOperationException("tiny threw on purpose");

            case "unknown-tool":
                await driver.CallAsync("no_such_tool");
                break;

            case "seed":
                Console.Error.WriteLine($"tiny: seed {random.Seed} first {random.Random.Next()}");
                File.WriteAllText(Path.Combine(driver.UserDataFolder, "left-by-tiny.txt"), "stale");
                break;

            case "tool-error":
                var error = await driver.CallAsync("render");
                Console.Error.WriteLine($"tiny: tool error {error.IsError}");
                await driver.CaptureAsync("no-mesh");
                break;

            case "capture":
                await CaptureAsync(driver);
                break;

            case "bytes":
                driver.AssertBytesEqual("tiny bytes", [1, 2, 3], [1, 2, 4]);
                break;

            case "clock":
                await ClockAsync(driver);
                break;
        }
    }

    private static async Task CaptureAsync(ScenarioDriver driver)
    {
        await driver.CallAsync("define_mesh", Quad);
        var first = await driver.CaptureAsync("first", new { view = "front" });
        var second = await driver.CaptureAsync("second", new { view = "front" });
        driver.AssertBytesEqual("repeat render", first.Pixels, second.Pixels);
        Console.Error.WriteLine($"tiny: frame {first.Width}x{first.Height} corner {first.Pixel(2, 2)} caption {first.Caption}");
    }

    // A render first, so that the frame loop has run and the delta is the engine's own reading.
    private async Task ClockAsync(ScenarioDriver driver)
    {
        await driver.CallAsync("define_mesh", Quad);
        var result = await driver.CallAsync("render");
        if (result.IsError is true)
        {
            driver.Fail($"render: {result.Content.OfType<TextContentBlock>().First().Text}");
        }

        var options = services.GetRequiredService<IOptions<TimeOptions>>().Value;
        var time = services.GetRequiredService<ITimeService>();
        Console.Error.WriteLine(FormattableString.Invariant(
            $"tiny: clock option {options.FrameDeltaTime} delta {time.DeltaTime} pacing {options.PaceToFixedStep}"));
    }
}
