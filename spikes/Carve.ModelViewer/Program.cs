using System.Globalization;
using Carve.ModelViewer;
using Empyrean;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;

ViewerOptions options;
string[] engineArgs;
try
{
    options = ViewerOptions.Parse(args, out engineArgs);
}
catch (ArgumentException e)
{
    Console.Error.WriteLine($"carve-viewer: {e.Message}");
    Console.Error.WriteLine(ViewerOptions.Usage);
    return 2;
}

var run = new ViewerRun();
try
{
    EmpyreanHostBuilder.CreateDefault(engineArgs)
        // Lowest of all the sources, so --Empyrean:Time:... still beats it. The fixed clock is the
        // only time the scene sees, and nothing in it is random, so the engine has no seed to fix.
        .ConfigureAppConfiguration(configuration => configuration.Sources.Insert(0, new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string?>
            {
                ["Empyrean:Time:FrameDeltaTime"] = (1.0 / 60.0).ToString("R", CultureInfo.InvariantCulture),
            },
        }))
        // Code-level defaults: --Empyrean:Graphics:VirtualSurface=false opens a window for a person to watch.
        .UseStandardHost(graphics =>
        {
            graphics.VirtualSurface = true;
            graphics.WindowWidth = ViewerLook.Size;
            graphics.WindowHeight = ViewerLook.Size;
            graphics.AntiAliasing = AntiAliasing.X4;
        })
        .ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton(run);
            services.UseRenderPipeline(RenderPipelinePresets.StandardHdr);
            services.AddSystem<ViewerSystem>();
        })
        .Build()
        .Run();
}
catch (Exception e)
{
    // No Vulkan device lands here, as does anything else the engine refuses at start.
    run.Fail($"{e.GetType().Name}: {e.Message}");
}

return run.ExitCode;
