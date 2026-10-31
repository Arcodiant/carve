using System.IO.Pipelines;
using System.Reflection;
using Carve.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Carve.Server.Tests.Extension;

/// <summary>
/// Says on stderr which hook ran, so a test can see the order from outside the process. Under
/// <c>TestExtension:Mode</c> it also exercises one of the freedoms the API promises.
/// </summary>
public sealed class TestExtension : ICarveExtension
{
    private string? _mode;

    public void ConfigureHost(CarveHostBuilderContext context)
    {
        Console.Error.WriteLine("test-extension: pre-build");
        _mode = context.Configuration["TestExtension:Mode"];

        if (_mode == "transport")
        {
            var toServer = new Pipe();
            var fromServer = new Pipe();
            context.Mcp.WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream());
        }

        if (_mode == "tool")
        {
            context.Mcp.WithTools([McpServerTool.Create(() => "pong", new() { Name = "test_ping" })]);
        }

        if (_mode == "direct")
        {
            Console.Error.WriteLine($"test-extension: hook reads Direct={context.Configuration["TestExtension:Direct"]}");
            Console.Error.WriteLine($"test-extension: hook reads Person={context.Configuration["TestExtension:Person"]}");
            context.Configuration["TestExtension:Direct"] = "set";
            context.Configuration["TestExtension:Person"] = "set";
        }

        if (_mode == "config")
        {
            context.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestExtension:FromExtension"] = "extension",
                ["TestExtension:OverCommandLine"] = "extension",
                ["Empyrean:Time:FixedDeltaTime"] = "0.125",
            });
        }
    }

    public void OnStarted(IServiceProvider services)
    {
        Console.Error.WriteLine("test-extension: started");

        // An assembly the host does not carry, so it can only come from beside this extension.
        Console.Error.WriteLine($"test-extension: dependency {typeof(MetadataLoadContext).Assembly.GetName().Name}");

        if (_mode == "transport")
        {
            Console.Error.WriteLine($"test-extension: transport {services.GetRequiredService<ITransport>().GetType().Name}");
            services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }

        if (_mode == "config")
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            foreach (var key in new[] { "TestExtension:FromExtension", "TestExtension:OverCommandLine", "Empyrean:Time:FixedDeltaTime" })
            {
                Console.Error.WriteLine($"test-extension: {key}={configuration[key]}");
            }

            services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }

        if (_mode == "direct")
        {
            services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }

        if (_mode == "fail")
        {
            Console.Error.WriteLine("test-extension: failing the run");
            Environment.ExitCode = 1;
            services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }
    }
}
