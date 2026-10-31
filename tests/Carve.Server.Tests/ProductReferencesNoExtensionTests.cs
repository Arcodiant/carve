using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Carve.Server.Tests;

public class ProductReferencesNoExtensionTests
{
    [Fact]
    public void The_product_project_references_no_extension()
    {
        var project = File.ReadAllText(Path.Combine(Paths.RepoRoot, "src/Carve.Server/Carve.Server.csproj"));

        Assert.Empty(ProductReferenceGuard.FindInProject(project));
    }

    [Fact]
    public void The_built_product_references_no_extension()
    {
        var found = ProductReferenceGuard.FindInAssembly(
            Paths.CarveDll, ProductReferenceGuard.ExtensionProjects(Paths.RepoRoot));

        Assert.Empty(found);
    }

    [Theory]
    [InlineData("""<ProjectReference Include="../../extensions/Carve.Debug/Carve.Debug.csproj" />""")]
    [InlineData("""<ProjectReference Include="..\..\scenarios\Cube\Cube.csproj" />""")]
    [InlineData("""<PackageReference Include="Carve.Debug" Version="1.0.0" />""")]
    [InlineData("""<Reference Include="Carve.Scenario.Cube" />""")]
    [InlineData("""<Reference Include="Anything"><HintPath>../extensions/Anything.dll</HintPath></Reference>""")]
    public void The_project_check_fails_on_a_bad_reference(string reference)
    {
        var project = $"""<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>{reference}</ItemGroup></Project>""";

        Assert.NotEmpty(ProductReferenceGuard.FindInProject(project));
    }

    [Fact]
    public void The_project_check_passes_the_extension_api()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
              <ProjectReference Include="../Carve.Extensibility/Carve.Extensibility.csproj" />
            </ItemGroup></Project>
            """;

        Assert.Empty(ProductReferenceGuard.FindInProject(project));
    }

    [Theory]
    [InlineData("Carve.Debug")]
    [InlineData("Carve.Scenario.Cube")]
    [InlineData("Carve.SomethingElse")]
    [InlineData("OddlyNamed")]
    public void The_assembly_check_fails_on_a_bad_reference(string referenced) =>
        Assert.Equal([referenced], FindInFixtureReferencing(referenced));

    [Fact]
    public void The_assembly_check_passes_the_extension_api() =>
        Assert.Empty(FindInFixtureReferencing("Carve.Extensibility"));

    private static IReadOnlyList<string> FindInFixtureReferencing(string referenced)
    {
        var path = Path.Combine(Path.GetTempPath(), $"carve-guard-fixture-{Guid.NewGuid():N}.dll");
        try
        {
            WriteAssemblyReferencing(path, referenced);

            return ProductReferenceGuard.FindInAssembly(path, ["OddlyNamed"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Emits a minimal real assembly, so the check is proved against the reader it uses.</summary>
    private static void WriteAssemblyReferencing(string path, string referenced)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("fixture.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("fixture"), new Version(1, 0), default, default, 0, AssemblyHashAlgorithm.None);
        metadata.AddAssemblyReference(metadata.GetOrAddString(referenced), new Version(1, 0), default, default, 0, default);
        metadata.AddTypeDefinition(0, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

        var image = new BlobBuilder();
        new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll | Characteristics.ExecutableImage),
            new MetadataRootBuilder(metadata), new BlobBuilder()).Serialize(image);

        using var file = File.Create(path);
        image.WriteContentTo(file);
    }
}
