using System.Diagnostics;
using System.Text.Json;
using Carve.Server.Tests;

namespace Patina.Server.Tests;

/// <summary>The real patina, started as it ships (dotnet exec patina.dll) and spoken to over stdio: tools/list and one round trip.</summary>
[Trait("Tier", "regression")]
public class PatinaProcessTests
{
    [Fact]
    public async Task Patina_over_stdio_names_itself_lists_its_tools_and_round_trips_a_graph_through_a_file()
    {
        using var folder = new TempFolder();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = folder.Path,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add(Paths.PatinaDll);
        using var process = Process.Start(start)!;
        var stderr = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, e) => { lock (stderr) stderr.AppendLine(e.Data); };
        process.BeginErrorReadLine();
        var lines = new List<string>();
        try
        {
            async Task<JsonElement> Send(int? id, string method, string parameters)
            {
                var head = id is { } n ? $"\"id\":{n}," : "";
                await process.StandardInput.WriteLineAsync($"{{\"jsonrpc\":\"2.0\",{head}\"method\":\"{method}\",\"params\":{parameters}}}");
                await process.StandardInput.FlushAsync();
                if (id is null) return default;
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                while (true)
                {
                    var line = await process.StandardOutput.ReadLineAsync(cancel.Token)
                        ?? throw new InvalidOperationException("patina closed stdout. stderr:\n" + stderr);
                    lines.Add(line);
                    using var document = JsonDocument.Parse(line); // Everything on stdout is protocol.
                    if (document.RootElement.TryGetProperty("id", out var got) && got.GetInt32() == id) return document.RootElement.GetProperty("result").Clone();
                }
            }

            var initialize = await Send(1, "initialize", """{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"0"}}""");
            Assert.Equal("patina", initialize.GetProperty("serverInfo").GetProperty("name").GetString());
            await Send(null, "notifications/initialized", "{}");

            var tools = (await Send(2, "tools/list", "{}")).GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
            Assert.Equal(["define_graph", "nodes", "edit_graph", "get_graph", "save", "open", "push_snapshot", "pop_snapshot", "export", "swatch", "check"], tools);

            async Task<string> Call(int id, string tool, string arguments)
            {
                var result = await Send(id, "tools/call", $"{{\"name\":\"{tool}\",\"arguments\":{arguments}}}");
                Assert.False(result.TryGetProperty("isError", out var e) && e.GetBoolean(), $"{tool}: {result}");
                return result.GetProperty("content")[0].GetProperty("text").GetString()!;
            }

            const string flat = """{"graph":{"version":1,"size":64,"seed":5,"nodes":{"c":{"type":"colour","params":{"colour":"#102030"}},"out":{"type":"output","inputs":{"in":"c"}}}}}""";
            Assert.EndsWith("no faults: it evaluates", await Call(3, "define_graph", flat));
            var defined = await Call(4, "get_graph", "{}");
            Assert.StartsWith($"save: saved flat.texgraph to {Path.Combine(folder.Path, "flat.texgraph")}", await Call(5, "save", """{"path":"flat"}"""));
            await Call(6, "define_graph", """{"graph":{"version":1,"size":32,"seed":0,"nodes":{}}}""");
            await Call(7, "open", """{"path":"flat"}""");
            var reopened = await Call(8, "get_graph", "{}");
            Assert.Equal(defined[defined.IndexOf('\n')..], reopened[reopened.IndexOf('\n')..]);
            Assert.Contains($"; path {Path.Combine(folder.Path, "flat.texgraph")}; ", reopened);

            process.StandardInput.Close();
            using var exit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(exit.Token);
            while (await process.StandardOutput.ReadLineAsync(exit.Token) is { } rest)
            {
                using var _ = JsonDocument.Parse(rest);
            }
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}

/// <summary>The product-reference guard (carve's, T-1) on patina: no extension in its project, its closure or what it builds.</summary>
[Trait("Tier", "unit")]
public class PatinaReferencesNoExtensionTests
{
    private static string Project => Path.Combine(Paths.RepoRoot, "src/Patina.Server/Patina.Server.csproj");

    [Fact]
    public void Patina_s_project_and_its_closure_reference_no_extension()
    {
        var extensions = ProductReferenceGuard.ExtensionProjects(Paths.RepoRoot).ToList();

        Assert.Empty(ProductReferenceGuard.FindInProject(File.ReadAllText(Project)));
        Assert.Empty(ProductReferenceGuard.FindInProjectClosure(Project, extensions));
    }

    [Fact]
    public void Built_patina_references_no_extension_in_its_assembly_deps_or_output_folder()
    {
        var extensions = ProductReferenceGuard.ExtensionProjects(Paths.RepoRoot).ToList();

        Assert.Empty(ProductReferenceGuard.FindInAssembly(Paths.PatinaDll, extensions));
        Assert.Empty(ProductReferenceGuard.FindInDeps(Path.ChangeExtension(Paths.PatinaDll, ".deps.json"), extensions));
        Assert.Empty(ProductReferenceGuard.FindInOutputFolder(Path.GetDirectoryName(Paths.PatinaDll)!, extensions));
    }

    [Fact]
    public void Patina_hosts_through_the_host_library_and_reads_graphs_through_patina_graphs()
    {
        var references = ProductReferenceGuard.ReferencesOf(Paths.PatinaDll);

        Assert.Contains("Carve.Hosting", references);
        Assert.Contains("Patina.Graphs", references);
    }
}
