using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
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
        "Carve.Hosting", // The shared engine host (T-111), product code, itself checked by the guard's tests.
        "Carve.Graphs", // The engine-free mesh-input nodes (T-161), product code patina reaches (T-162) and render bakes graph slots with (T-163); CarveGraphsIsEngineFreeTests holds it.
    };

    [GeneratedRegex(@"Carve\.Debug|Carve\.Scenario|Carve\.ModelViewer|(^|[\\/])(extensions|scenarios|spikes)[\\/]", RegexOptions.IgnoreCase)]
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
    /// <param name="extensionAssemblies">Names of projects under extensions/, scenarios/ and spikes/.</param>
    public static IReadOnlyList<string> FindInAssembly(string assemblyPath, IEnumerable<string> extensionAssemblies)
    {
        var extensions = new HashSet<string>(extensionAssemblies, StringComparer.OrdinalIgnoreCase);

        return ReferencesOf(assemblyPath)
            .Where(name => extensions.Contains(name)
                || Forbidden().IsMatch(name)
                || (name.StartsWith("Carve.", StringComparison.OrdinalIgnoreCase) && !Allowed.Contains(name)))
            .ToList();
    }

    /// <summary>True for a name the product may not carry: an extension, or any Carve assembly not allowed.</summary>
    private static bool IsForbidden(string name, HashSet<string> extensions) =>
        extensions.Contains(name)
        || Forbidden().IsMatch(name)
        || (name.StartsWith("Carve.", StringComparison.OrdinalIgnoreCase) && !Allowed.Contains(name));

    /// <summary>
    /// Every project in the <c>ProjectReference</c> closure of the product's project that is
    /// forbidden, and every value in any of their files that names something forbidden. This is
    /// what catches an extension reached through Carve.Core.
    /// </summary>
    public static IReadOnlyList<string> FindInProjectClosure(string projectPath, IEnumerable<string> extensionAssemblies)
    {
        var extensions = new HashSet<string>(extensionAssemblies, StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([Path.GetFullPath(projectPath)]);
        var root = pending.Peek();
        while (pending.Count > 0)
        {
            var path = pending.Pop();
            if (!seen.Add(path))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            if (path != root && IsForbidden(name, extensions))
            {
                found.Add(name);
            }

            if (!File.Exists(path))
            {
                continue;
            }

            var xml = File.ReadAllText(path);
            found.AddRange(FindInProject(xml).Select(value => $"{name}: {value}"));
            foreach (var reference in XDocument.Parse(xml).Descendants("ProjectReference").Select(e => e.Attribute("Include")?.Value))
            {
                if (reference is not null)
                {
                    pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference.Replace('\\', '/'))));
                }
            }
        }

        return found;
    }

    /// <summary>Every library in a built <c>.deps.json</c> that is forbidden.</summary>
    public static IReadOnlyList<string> FindInDeps(string depsJsonPath, IEnumerable<string> extensionAssemblies)
    {
        var extensions = new HashSet<string>(extensionAssemblies, StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(File.ReadAllText(depsJsonPath));

        return document.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(library => library.Name.Split('/')[0])
            .Where(name => IsForbidden(name, extensions))
            .ToList();
    }

    /// <summary>Every assembly in a build output folder that is forbidden.</summary>
    public static IReadOnlyList<string> FindInOutputFolder(string folder, IEnumerable<string> extensionAssemblies)
    {
        var extensions = new HashSet<string>(extensionAssemblies, StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(folder, "*.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Where(name => IsForbidden(name, extensions))
            .ToList();
    }

    /// <summary>The names of the assemblies a built assembly references.</summary>
    public static IReadOnlyList<string> ReferencesOf(string assemblyPath)
    {
        using var pe = new PEReader(File.OpenRead(assemblyPath));
        var metadata = pe.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToList();
    }

    /// <summary>The assembly names of whatever now lives under extensions/, scenarios/ and spikes/.</summary>
    public static IEnumerable<string> ExtensionProjects(string repoRoot) =>
        new[] { "extensions", "scenarios", "spikes" }
            .Select(folder => Path.Combine(repoRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetFileNameWithoutExtension(path)!);
}
