using Carve.Debug;
using Carve.Extensibility;
using Empyrean;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Tiny;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class TinyExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context)
    {
        context.Services.AddSingleton<IScenario, TinyScenario>();

        // Read here, after the debug extension's hook, to see whose value is on top.
        if (context.Configuration["Tiny:Mode"] == "configure-pacing")
        {
            Console.Error.WriteLine($"tiny: pacing at configure {context.Configuration["Empyrean:Time:PaceToFixedStep"]}");
        }
    }

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>Calls <c>tools/list</c>; under <c>Tiny:Mode</c> it exercises one thing the driver promises.</summary>
public sealed class TinyScenario(IConfiguration configuration, SeededRandom random, IServiceProvider services, IHostApplicationLifetime lifetime) : IScenario
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

            case "stop-host":
                // Stops the host, then waits on something that is not the driver.
                lifetime.StopApplication();
                await Task.Delay(TimeSpan.FromSeconds(30));
                break;

            case "stdout":
                Console.Out.WriteLine("tiny: this belongs to MCP");
                Console.Out.Flush();
                break;

            case "clock":
                await ClockAsync(driver);
                break;

            case "release":
                await ReleaseAsync(driver);
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

    // One face drawn in material m, redefined between renders: plain, textured from Tiny:TextureA, from Tiny:TextureB, the
    // same drawn in the UV layout (which draws no material), plain again. After each, the engine's own counts, read once a few
    // more renders have waited out the frames in flight.
    private async Task ReleaseAsync(ScenarioDriver driver)
    {
        await driver.CallAsync("define_mesh", Quad);
        await driver.CallAsync("set_uvs", new { faces = new[] { new { face = 0, uv = new[] { new[] { 0, 1 }, new[] { 1, 1 }, new[] { 1, 0 }, new[] { 0, 0 } } } } });
        (object? Material, string Mode)[] stages =
        [
            (new { name = "m", base_colour = "#80A0C0" }, "shaded"),
            (new { name = "m", base_colour_texture = configuration["Tiny:TextureA"] }, "shaded"),
            (new { name = "m", base_colour_texture = configuration["Tiny:TextureB"] }, "shaded"),
            (null, "uvs"),
            (new { name = "m", base_colour = "#80A0C0" }, "shaded"),
        ];
        var resources = services.GetRequiredService<IGraphicsResourceManager>();
        for (var i = 0; i < stages.Length; i++)
        {
            if (stages[i].Material is { } material) await Ok(driver, "define_material", material);
            if (i == 0) await Ok(driver, "set_material", new { faces = new[] { 0 }, material = "m" });
            foreach (var view in new[] { "front", "back", "front", "back", "front", "back" })
                await Ok(driver, "render", stages[i].Mode == "uvs" ? new { mode = "uvs" } : new { view, mode = "shaded" });
            var counts = resources.Residency;
            Console.Error.WriteLine($"tiny: residency {i} textures {counts.TextureCount} table {counts.MaterialTableUsed}");
        }
    }

    private static async Task Ok(ScenarioDriver driver, string tool, object arguments)
    {
        var result = await driver.CallAsync(tool, arguments);
        if (result.IsError is true)
        {
            driver.Fail($"{tool}: {result.Content.OfType<TextContentBlock>().First().Text}");
        }
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
