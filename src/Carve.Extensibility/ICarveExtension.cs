namespace Carve.Extensibility;

/// <summary>
/// An extension to the <c>carve</c> host. A public, concrete type with a public parameterless
/// constructor, in a DLL named by <c>Carve:Extensions</c>. Nothing is scanned: a DLL that is not
/// listed is not loaded.
/// </summary>
public interface ICarveExtension
{
    /// <summary>
    /// Runs before the product registers the engine or its default transport, in list order.
    /// Engine services added here win over the engine's own, since those are first-wins. A
    /// transport added here replaces stdio. Configuration added here, as a source or set directly
    /// on <see cref="CarveHostBuilderContext.Configuration"/>, sits above the product's defaults and
    /// below the environment and the command line: fresh copies of those two go on top after each
    /// extension's hook, so a later extension reads the person's value where there is one, and
    /// the earlier extension's value where there is none.
    /// </summary>
    void ConfigureHost(CarveHostBuilderContext context);

    /// <summary>
    /// Runs after the host has started, in list order, on the host's own thread: the MCP server
    /// is up and the engine is still starting on its frame thread. Return promptly; long work
    /// belongs on a task of its own.
    /// </summary>
    void OnStarted(IServiceProvider services);
}
