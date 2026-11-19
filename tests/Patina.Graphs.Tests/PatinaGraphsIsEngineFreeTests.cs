using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>Patina.Graphs references no engine or Arcodiant assembly, and uses no System.Random and no clock (D-7).</summary>
[Trait("Tier", "unit")]
public class PatinaGraphsIsEngineFreeTests
{
    private static readonly System.Reflection.Assembly Package = typeof(PatinaGraphsAssembly).Assembly;

    [Fact]
    public void ReferencesNoEngineOrArcodiantAssembly()
    {
        var engine = Package.GetReferencedAssemblies()
            .Where(name => name.Name!.StartsWith("Empyrean", StringComparison.OrdinalIgnoreCase)
                        || name.Name!.StartsWith("Arcodiant", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(engine);
    }

    [Fact]
    public void UsesNoRandomAndNoClock()
    {
        using var pe = new PEReader(File.OpenRead(Package.Location));
        var md = pe.GetMetadataReader();
        string Name(EntityHandle h) => h.Kind switch
        {
            HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Namespace) + "." + md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name),
            _ => "",
        };

        var types = md.TypeReferences.Select(h => Name(h)).ToHashSet();
        string[] bannedTypes = ["System.Random", "System.Diagnostics.Stopwatch", "System.TimeProvider"];
        Assert.Equal([], bannedTypes.Where(types.Contains).ToArray());

        var members = md.MemberReferences.Select(h => md.GetMemberReference(h))
            .Select(m => Name(m.Parent) + "::" + md.GetString(m.Name)).ToHashSet();
        string[] bannedMembers =
        [
            "System.DateTime::get_Now", "System.DateTime::get_UtcNow", "System.DateTime::get_Today",
            "System.DateTimeOffset::get_Now", "System.DateTimeOffset::get_UtcNow",
            "System.Environment::get_TickCount", "System.Environment::get_TickCount64",
        ];
        Assert.Equal([], bannedMembers.Where(members.Contains).ToArray());
        Assert.Contains("System.Text.Json.Utf8JsonReader", types); // the scan sees the references it should
    }
}
