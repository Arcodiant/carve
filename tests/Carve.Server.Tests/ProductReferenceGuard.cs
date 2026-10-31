using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Carve.Server.Tests;

/// <summary>What the product may not reference, checked in a project file and in a built assembly.</summary>
internal static partial class ProductReferenceGuard
{
    /// <summary>The only Carve assemblies the product may reference.</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Carve.Core",
        "Carve.Extensibility",
    };

    [GeneratedRegex(@"Carve\.Debug|Carve\.Scenario|(^|[\\/])(extensions|scenarios)[\\/]", RegexOptions.IgnoreCase)]
    private static partial Regex Forbidden();

    /// <summary>Every attribute or text value in the project that names something forbidden.</summary>
    public static IReadOnlyList<string> FindInProject(string projectXml) =>
        XDocument.Parse(projectXml).Descendants()
            .SelectMany(e => e.Attributes().Select(a => a.Value)
                .Concat(e.HasElements ? [] : [e.Value]))
            .Where(value => Forbidden().IsMatch(value))
            .ToList();

    /// <summary>Every referenced assembly that is an extension, or any Carve assembly not allowed.</summary>
    /// <param name="assemblyPath">The built assembly.</param>
    /// <param name="extensionAssemblies">Names of projects under extensions/ and scenarios/.</param>
    public static IReadOnlyList<string> FindInAssembly(string assemblyPath, IEnumerable<string> extensionAssemblies)
    {
        var extensions = new HashSet<string>(extensionAssemblies, StringComparer.OrdinalIgnoreCase);

        using var pe = new PEReader(File.OpenRead(assemblyPath));
        var metadata = pe.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .Where(name => extensions.Contains(name)
                || Forbidden().IsMatch(name)
                || (name.StartsWith("Carve.", StringComparison.OrdinalIgnoreCase) && !Allowed.Contains(name)))
            .ToList();
    }

    /// <summary>The assembly names of whatever now lives under extensions/ and scenarios/.</summary>
    public static IEnumerable<string> ExtensionProjects(string repoRoot) =>
        new[] { "extensions", "scenarios" }
            .Select(folder => Path.Combine(repoRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetFileNameWithoutExtension(path)!);
}
