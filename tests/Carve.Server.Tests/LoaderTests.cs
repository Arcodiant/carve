namespace Carve.Server.Tests;

public class LoaderTests
{
    private static string[] Extension(params string[] more) =>
        [$"--Carve:Extensions:0={Paths.TestExtensionDll}", .. more];

    [Fact]
    public async Task An_empty_list_loads_nothing()
    {
        using var carve = new CarveProcess([]);

        await carve.InitializeAsync();
        carve.CloseStdin();

        Assert.Equal(0, await carve.WaitForExitAsync(Exit));
        Assert.DoesNotContain("test-extension", carve.Stderr);
    }

    [Fact]
    public async Task A_path_that_does_not_exist_is_fatal_and_named()
    {
        const string bad = "no/such/extension.dll";
        using var carve = new CarveProcess([$"--Carve:Extensions:0={bad}"]);

        Assert.NotEqual(0, await carve.WaitForExitAsync(Answer));
        Assert.Contains(bad, carve.Stderr);
        Assert.Empty(carve.StdoutLines);
    }

    [Fact]
    public async Task A_dll_with_no_extension_is_fatal_and_named()
    {
        var bare = Paths.Output("src/Carve.Server", "Carve.Core.dll");
        using var carve = new CarveProcess([$"--Carve:Extensions:0={bare}"]);

        Assert.NotEqual(0, await carve.WaitForExitAsync(Answer));
        Assert.Contains(bare, carve.Stderr);
        Assert.Contains("no ICarveExtension", carve.Stderr);
    }

    [Fact]
    public async Task A_good_path_after_a_bad_one_does_not_start()
    {
        const string bad = "no/such/extension.dll";
        using var carve = new CarveProcess(
            [$"--Carve:Extensions:0={bad}", $"--Carve:Extensions:1={Paths.TestExtensionDll}"]);

        Assert.NotEqual(0, await carve.WaitForExitAsync(Answer));
        Assert.DoesNotContain("test-extension", carve.Stderr);
    }

    [Fact]
    public async Task An_extensions_hooks_run_in_order_and_mcp_answers()
    {
        using var carve = new CarveProcess(Extension());

        await carve.InitializeAsync();
        carve.CloseStdin();

        Assert.Equal(0, await carve.WaitForExitAsync(Exit));
        var preBuild = carve.Stderr.IndexOf("test-extension: pre-build", StringComparison.Ordinal);
        var started = carve.Stderr.IndexOf("test-extension: started", StringComparison.Ordinal);
        Assert.True(preBuild >= 0, carve.Stderr);
        Assert.True(started > preBuild, carve.Stderr);
    }

    [Fact]
    public async Task An_extensions_dependency_resolves_from_beside_it()
    {
        using var carve = new CarveProcess(Extension());

        await carve.InitializeAsync();
        carve.CloseStdin();

        Assert.Equal(0, await carve.WaitForExitAsync(Exit));
        Assert.Contains("test-extension: dependency System.Reflection.MetadataLoadContext", carve.Stderr);
    }

    [Fact]
    public async Task An_extensions_tool_reaches_tools_list()
    {
        using var carve = new CarveProcess(Extension("--TestExtension:Mode=tool"));

        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();

        // Beside the product's own tools, not instead of them.
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToArray();
        Assert.Contains("test_ping", names);
        Assert.Contains("define_mesh", names);
    }

    [Fact]
    public async Task An_extensions_transport_replaces_stdio()
    {
        using var carve = new CarveProcess(Extension("--TestExtension:Mode=transport"));

        Assert.Equal(0, await carve.WaitForExitAsync(Answer));
        Assert.Contains("test-extension: transport StreamServerTransport", carve.Stderr);
        Assert.Empty(carve.StdoutLines);
    }

    [Fact]
    public async Task An_extensions_configuration_is_a_default_below_the_command_line()
    {
        using var carve = new CarveProcess(Extension(
            "--TestExtension:Mode=config", "--TestExtension:OverCommandLine=commandline"));

        Assert.Equal(0, await carve.WaitForExitAsync(Answer));
        Assert.Contains("test-extension: TestExtension:FromExtension=extension", carve.Stderr);
        Assert.Contains("test-extension: TestExtension:OverCommandLine=commandline", carve.Stderr);
        Assert.Contains("test-extension: Empyrean:Time:FixedDeltaTime=0.125", carve.Stderr); // Over the product's default.
    }

    [Fact]
    public async Task An_extension_can_fail_the_run_through_the_exit_code()
    {
        using var carve = new CarveProcess(Extension("--TestExtension:Mode=fail"));

        Assert.Equal(1, await carve.WaitForExitAsync(Answer));
    }
}
