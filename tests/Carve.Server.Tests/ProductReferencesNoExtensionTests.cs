using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Carve.Server.Tests;

[Trait("Tier", "unit")]
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

    [Fact]
    public void The_product_reaches_no_extension_through_its_project_references()
    {
        var found = ProductReferenceGuard.FindInProjectClosure(
            Path.Combine(Paths.RepoRoot, "src/Carve.Server/Carve.Server.csproj"),
            ProductReferenceGuard.ExtensionProjects(Paths.RepoRoot));

        Assert.Empty(found);
    }

    [Fact]
    public void The_built_product_carries_no_extension_in_its_deps_or_output_folder()
    {
        var extensions = ProductReferenceGuard.ExtensionProjects(Paths.RepoRoot).ToList();

        Assert.Empty(ProductReferenceGuard.FindInDeps(Path.ChangeExtension(Paths.CarveDll, ".deps.json"), extensions));
        Assert.Empty(ProductReferenceGuard.FindInOutputFolder(Path.GetDirectoryName(Paths.CarveDll)!, extensions));
    }

    [Fact]
    public void The_closure_check_fails_on_an_extension_reached_through_another_project()
    {
        var root = Directory.CreateTempSubdirectory("carve-guard-closure-").FullName;
        try
        {
            Write(root, "server/Server.csproj", """<Project><ItemGroup><ProjectReference Include="../core/Core.csproj" /></ItemGroup></Project>""");
            Write(root, "core/Core.csproj", """<Project><ItemGroup><ProjectReference Include="../ext/Odd.csproj" /></ItemGroup></Project>""");
            Write(root, "ext/Odd.csproj", "<Project />");

            Assert.Equal(["Odd"], ProductReferenceGuard.FindInProjectClosure(Path.Combine(root, "server/Server.csproj"), ["Odd"]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_closure_check_fails_when_a_project_in_it_names_the_debug_extension()
    {
        var root = Directory.CreateTempSubdirectory("carve-guard-closure-").FullName;
        try
        {
            Write(root, "server/Server.csproj", """<Project><ItemGroup><ProjectReference Include="..\core\Core.csproj" /></ItemGroup></Project>""");
            Write(root, "core/Core.csproj", """<Project><ItemGroup><ProjectReference Include="../../extensions/Carve.Debug/Carve.Debug.csproj" /></ItemGroup></Project>""");

            Assert.NotEmpty(ProductReferenceGuard.FindInProjectClosure(Path.Combine(root, "server/Server.csproj"), []));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_deps_and_output_checks_fail_on_a_forbidden_assembly_and_pass_the_allowed_ones()
    {
        var root = Directory.CreateTempSubdirectory("carve-guard-output-").FullName;
        try
        {
            string[] allowed = ["carve/1.0.0", "Carve.Core/1.0.0", "Carve.Extensibility/1.0.0"];
            Write(root, "ok.deps.json", Deps(allowed));
            Write(root, "bad.deps.json", Deps([.. allowed, "Carve.Debug/1.0.0", "Odd/1.0.0"]));
            File.WriteAllText(Path.Combine(root, "Carve.Core.dll"), "");

            Assert.Empty(ProductReferenceGuard.FindInDeps(Path.Combine(root, "ok.deps.json"), ["Odd"]));
            Assert.Equal(["Carve.Debug", "Odd"], ProductReferenceGuard.FindInDeps(Path.Combine(root, "bad.deps.json"), ["Odd"]));
            Assert.Empty(ProductReferenceGuard.FindInOutputFolder(root, ["Odd"]));

            File.WriteAllText(Path.Combine(root, "Carve.Debug.dll"), "");
            File.WriteAllText(Path.Combine(root, "Odd.dll"), "");
            Assert.Equal(["Carve.Debug", "Odd"], ProductReferenceGuard.FindInOutputFolder(root, ["Odd"]).Order());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Deps(IEnumerable<string> libraries) =>
        "{\"libraries\":{" + string.Join(",", libraries.Select(l => $"\"{l}\":{{}}")) + "}}";

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
