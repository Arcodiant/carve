using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Carve.Extensibility;
using Microsoft.Extensions.Configuration;

namespace Carve.Server;

/// <summary>A listed extension could not be used. Fatal: the message names the path.</summary>
internal sealed class ExtensionLoadException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Loads the DLLs listed in <c>Carve:Extensions</c>, in order. No scanning.
/// </summary>
/// <remarks>
/// Everything loads into the default context, with each extension's folder as a fallback. So
/// the host's copy of a shared assembly always wins, which keeps <see cref="ICarveExtension"/>
/// and the abstractions one type, and an extension stacked on another sees the same copy of it.
/// </remarks>
internal static class ExtensionLoader
{
    public const string ConfigurationKey = "Carve:Extensions";

    public static IReadOnlyList<ICarveExtension> Load(IConfiguration configuration)
    {
        var paths = ListedPaths(configuration);
        var resolvers = new List<AssemblyDependencyResolver>();
        if (paths.Count > 0)
        {
            InstallFallback(resolvers);
        }

        var extensions = new List<ICarveExtension>();
        foreach (var path in paths)
        {
            extensions.AddRange(LoadOne(path, resolvers));
        }

        return extensions;
    }

    private static List<string> ListedPaths(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationKey);
        return section.Value is { } single
            ? [single]
            : section.GetChildren().Select(c => c.Value ?? string.Empty).ToList();
    }

    private static void InstallFallback(List<AssemblyDependencyResolver> resolvers)
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
            resolvers.Select(r => r.ResolveAssemblyToPath(name)).FirstOrDefault(p => p is not null) is { } path
                ? context.LoadFromAssemblyPath(path)
                : null;

        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
            resolvers.Select(r => r.ResolveUnmanagedDllToPath(name)).FirstOrDefault(p => p is not null) is { } path
                ? NativeLibrary.Load(path)
                : IntPtr.Zero;
    }

    private static List<ICarveExtension> LoadOne(string path, List<AssemblyDependencyResolver> resolvers)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            resolvers.Add(new AssemblyDependencyResolver(fullPath));
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);

            var extensions = assembly.GetExportedTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ICarveExtension).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .Select(t => (ICarveExtension)Activator.CreateInstance(t)!)
                .ToList();

            return extensions.Count > 0
                ? extensions
                : throw new ExtensionLoadException($"extension '{path}' holds no ICarveExtension");
        }
        catch (Exception e) when (e is not ExtensionLoadException)
        {
            var reason = e is ReflectionTypeLoadException { LoaderExceptions: [{ } first, ..] }
                ? first.Message
                : e.GetBaseException().Message;
            throw new ExtensionLoadException($"extension '{path}' did not load: {reason}", e);
        }
    }
}
