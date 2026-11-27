using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Carve.Server.Tests.VerbosityCalls;

namespace Carve.Server.Tests;

/// <summary>
/// T-206: M-23's pipeline on fps-arms' cage and tool sources through the real <c>carve</c> over stdio, each call at
/// <c>summary</c>, <c>silent</c> and <c>full</c>. Writes <c>dense-run.tsv</c> (call, level, characters) beside the test assembly.
/// </summary>
[Trait("Tier", "regression")]
public sealed class DenseRunProcessTests
{
    private static readonly string[] Levels = ["summary", "silent", "full"];

    private static string Samples(string file) => Path.Combine(Paths.RepoRoot, "samples", file);

    /// <summary>The run: (label, tool, arguments), the same at every level. Floods are on the cage after two subdivides.</summary>
    private static List<(string Label, string Tool, string Arguments)> Sequence()
    {
        var even = JsonSerializer.Serialize(new { faces = Enumerable.Range(0, 200).Select(i => i * 2) });
        var box = JsonSerializer.Serialize(new { method = "box", faces = Enumerable.Range(0, 100) });
        var calls = new List<(string, string, string)>
        {
            ("open arms-cage", "open", JsonSerializer.Serialize(new { path = Samples("arms-cage.carve") })),
            ("check cage", "check", "{}"),
            ("subdivide 1", "subdivide", """{"levels":1}"""),
            ("subdivide 2", "subdivide", """{"levels":1}"""),
            ("check cage x2", "check", "{}"),
            ("move vertex 0", "move", """{"vertices":[0],"by":[0,0.001,0]}"""),
            ("save moved", "save", """{"path":"moved.carve"}"""),
            ("open moved", "open", """{"path":"moved.carve"}"""),
            ("export skinned", "export", """{"path":"cage.glb"}"""),
            ("delete 200 faces (798 open edges)", "delete", even),
            ("check open edges", "check", "{}"),
            ("undo", "undo", "{}"),
            ("project_uvs box (280 overlaps)", "project_uvs", box),
            ("check overlaps", "check", "{}"),
        };
        foreach (var sample in new[] { "wrench.carve", "cutter-src.carve", "toolkit-src.carve", "scanner-src.carve" })
        {
            var name = Path.GetFileNameWithoutExtension(sample);
            calls.Add(($"open {name}", "open", JsonSerializer.Serialize(new { path = Samples(sample) })));
            calls.Add(($"unwrap {name}", "unwrap", "{}"));
            calls.Add(($"check {name}", "check", "{}"));
        }

        return calls;
    }

    private static int Triangles(string glb)
    {
        var bytes = File.ReadAllBytes(glb);
        var json = JsonDocument.Parse(Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12))).RootElement;
        return json.GetProperty("meshes").EnumerateArray().SelectMany(m => m.GetProperty("primitives").EnumerateArray())
            .Sum(p => json.GetProperty("accessors")[p.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32() / 3);
    }

    [Fact]
    public async Task Every_call_of_the_dense_run_answers_within_the_budget_silently_and_in_full()
    {
        var root = Directory.CreateTempSubdirectory("carve-dense-run-").FullName;
        var rows = new List<string> { "call\tlevel\tcharacters" };
        var oks = new Dictionary<string, bool>();
        var counts = new Dictionary<string, int>();
        try
        {
            foreach (var level in Levels)
            {
                var folder = Directory.CreateDirectory(Path.Combine(root, level)).FullName;
                using var carve = new CarveProcess([], workingDirectory: folder);
                await carve.InitializeAsync();
                foreach (var (label, tool, arguments) in Sequence())
                {
                    var result = await carve.CallToolAsync(tool, With(arguments, level));
                    var text = CarveProcess.TextOf(result);
                    Assert.False(CarveProcess.IsError(result), $"{level} {label}: {text}");
                    rows.Add($"{label}\t{level}\t{text.Length}");
                    var key = label;
                    switch (level)
                    {
                        case "summary":
                            Assert.True(text.Length <= DenseMeshBudgetTests.Budget, $"{label}: {text.Length} characters, over {DenseMeshBudgetTests.Budget}: {text[..Math.Min(600, text.Length)]}");
                            if (tool == "check") oks[key] = JsonDocument.Parse(text).RootElement.GetProperty("ok").GetBoolean();
                            if (label.StartsWith("delete") || label.StartsWith("check ") && label is "check open edges" or "check overlaps")
                                counts[key] = JsonDocument.Parse(text).RootElement.GetProperty("warnings").EnumerateArray()
                                    .Select(w => Regex.Match(w.GetString()!, @"^(\d+) (edges belong|faces have UVs)")).Where(m => m.Success).Sum(m => int.Parse(m.Groups[1].Value));
                            break;
                        case "silent":
                            Assert.Equal(Silent(tool, tool == "check" && oks[key]), text);
                            break;
                        default:
                            if (counts.TryGetValue(key, out var n) && n > 0)
                            {
                                var lines = JsonDocument.Parse(text).RootElement.GetProperty("warnings").EnumerateArray()
                                    .Count(w => w.GetString()!.Contains("belongs to only one face") || w.GetString()!.Contains("overlap"));
                                Assert.True(lines >= n, $"{label} full: {lines} warning lines for {n} counted at summary");
                            }
                            break;
                    }

                    if (level == "summary" && label == "check cage x2")
                        Assert.True(JsonDocument.Parse(text).RootElement.GetProperty("ok").GetBoolean(), text);
                }

                if (level == "summary") Assert.Equal(18976, Triangles(Path.Combine(folder, "cage.glb")));
            }

            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "dense-run.tsv"), rows);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } // the killed process may still hold its folder; it is temp
        }
    }

    /// <summary>The lengths of every served description, written to <c>descriptions.tsv</c>; the limit itself is held by <see cref="HelpThroughProcessTests"/>.</summary>
    [Fact]
    public async Task The_served_descriptions_are_measured_and_none_is_over_the_limit()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        var rows = (await carve.ListToolsAsync()).EnumerateArray()
            .Select(t => (Name: t.GetProperty("name").GetString()!, Length: t.GetProperty("description").GetString()!.Length)).OrderByDescending(t => t.Length).ToList();
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "descriptions.tsv"), rows.Select(r => $"{r.Name}	{r.Length}"));
        Assert.All(rows, r => Assert.True(r.Length <= HelpToolTests.Limit, $"{r.Name} {r.Length}"));
    }
}
