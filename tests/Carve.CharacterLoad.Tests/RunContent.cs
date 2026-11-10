using Empyrean;
using Microsoft.Extensions.DependencyInjection;
using CarveFile = Carve.Core.CarveFile;
using CarveModel = Carve.Core.Model;

namespace Carve.LoadChecks;

/// <summary>
/// What the character and soft load checks share: a scenario run's content folder mounted as a game would mount it, a saved
/// Carve file read back, and a sample found by walking up from the test's folder. Linked into <c>Carve.SoftLoad.Tests</c>.
/// </summary>
internal static class RunContent
{
    /// <summary>The engine's geometry loader with <c>game/Content</c> under <paramref name="runFolder"/> mounted, no sidecar.</summary>
    public static IModelGeometryLoader Loader(string runFolder)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmpyreanCore();
        services.AddEmpyreanGeometry();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IContentRegistry>().Mount(ContentSources.Directory(Path.Combine(runFolder, "game", "Content")));
        return provider.GetRequiredService<IModelGeometryLoader>();
    }

    public static CarveModel Saved(string path) => CarveFile.Read(File.ReadAllText(path)).Model!;

    /// <summary>The path of <c>samples/<paramref name="fileName"/></c>, the nearest one above the test's folder.</summary>
    public static string SamplePath(string fileName)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "samples", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("samples/" + fileName);
    }
}
