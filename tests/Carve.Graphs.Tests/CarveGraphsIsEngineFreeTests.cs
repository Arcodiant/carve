using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Xunit;

namespace Carve.Graphs.Tests;

/// <summary>Carve.Graphs references Carve.Core and Patina.Graphs only: no engine or Arcodiant assembly, no package, no System.Random and no clock.</summary>
[Trait("Tier", "unit")]
public class CarveGraphsIsEngineFreeTests
{
    private static readonly System.Reflection.Assembly Project = typeof(MeshNodes).Assembly;

    [Fact]
    public void ReferencesOnlyTheCoreThePackageAndTheFramework()
    {
        var framework = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var others = Project.GetReferencedAssemblies()
            .Where(name => !File.Exists(Path.Combine(framework, name.Name + ".dll")))
            .Select(name => name.Name!).Order().ToArray();
        Assert.Equal(["Carve.Core", "Patina.Graphs"], others);
    }

    [Fact]
    public void TheProjectFileDeclaresNoPackage()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Carve.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("Carve.slnx not found above the test folder");
        var project = XDocument.Load(Path.Combine(root, "src", "Carve.Graphs", "Carve.Graphs.csproj"));
        Assert.DoesNotContain(project.Descendants(), e => e.Name.LocalName == "PackageReference");
        var references = project.Descendants().Where(e => e.Name.LocalName == "ProjectReference").Select(e => Path.GetFileName((string)e.Attribute("Include")!)).Order();
        Assert.Equal(["Carve.Core.csproj", "Patina.Graphs.csproj"], references);
    }

    [Fact]
    public void UsesNoRandomAndNoClock()
    {
        using var pe = new PEReader(File.OpenRead(Project.Location));
        var md = pe.GetMetadataReader();
        string Name(EntityHandle h) => h.Kind == HandleKind.TypeReference
            ? md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Namespace) + "." + md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name)
            : "";
        var types = md.TypeReferences.Select(h => Name(h)).ToHashSet();
        string[] bannedTypes = ["System.Random", "System.Diagnostics.Stopwatch", "System.TimeProvider"];
        Assert.Equal([], bannedTypes.Where(types.Contains).ToArray());
        var members = md.MemberReferences.Select(h => md.GetMemberReference(h)).Select(m => Name(m.Parent) + "::" + md.GetString(m.Name)).ToHashSet();
        string[] bannedMembers =
        [
            "System.DateTime::get_Now", "System.DateTime::get_UtcNow", "System.DateTime::get_Today",
            "System.DateTimeOffset::get_Now", "System.DateTimeOffset::get_UtcNow",
            "System.Environment::get_TickCount", "System.Environment::get_TickCount64",
        ];
        Assert.Equal([], bannedMembers.Where(members.Contains).ToArray());
        Assert.Contains("Patina.Graphs.Nodes.NodeType", types); // the scan sees the references it should
    }
}
