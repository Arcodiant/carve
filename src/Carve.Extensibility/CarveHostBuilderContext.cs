using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carve.Extensibility;

/// <summary>What an extension may compose before the host is built.</summary>
public sealed class CarveHostBuilderContext(
    IServiceCollection services,
    IConfigurationManager configuration,
    IMcpServerBuilder mcp)
{
    /// <summary>The one container, holding both the engine and MCP.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>The host's configuration, already holding the product's own sources.</summary>
    public IConfigurationManager Configuration { get; } = configuration;

    /// <summary>The MCP server being composed: transports, tools, handlers.</summary>
    public IMcpServerBuilder Mcp { get; } = mcp;
}
