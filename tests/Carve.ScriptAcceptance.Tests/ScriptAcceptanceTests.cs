using System.Numerics;
using System.Text;
using System.Text.Json;
using Carve.Core;
using Carve.Core.Scripts;
using Carve.Server.Tests;
using Xunit;

namespace Carve.ScriptAcceptance.Tests;

/// <summary>
/// T-235's checks on a fresh agent's round folder (<c>CARVE_SCRIPT_ACCEPTANCE_RUN</c>): its two scripts, run from clear in
/// <see cref="ToolHarness"/>, give its two Carve files byte for byte; the edited script changes only values; and the change
/// did what was asked, as ruled 2026-11-28: the compartment backs are 3 then 4, equal in height, in the same bounds.
/// </summary>
[Trait("Tier", "unit")]
public sealed class ScriptAcceptanceTests
{
    private static string Round => Environment.GetEnvironmentVariable(RunAttribute.Variable)!;

    private static string In(string name) => Path.Combine(Round, name);

    [Run]
    public async Task The_exported_script_gives_prop_carve_byte_for_byte() =>
        Assert.Equal(File.ReadAllBytes(In("prop.carve")), await Replay("prop.carvescript"));

    [Run]
    public async Task The_edited_script_gives_prop_edited_carve_byte_for_byte() =>
        Assert.Equal(File.ReadAllBytes(In("prop-edited.carve")), await Replay("prop-edited.carvescript"));

    [Run]
    public void The_edited_script_keeps_every_exported_statement_in_order_and_the_diff_is_written()
    {
        // Ruled 2026-11-28 (Q1 A): each exported statement is kept, as it was or with new values, in order; statements may be added.
        var before = Statements("prop.carvescript");
        var after = Statements("prop-edited.carvescript");
        var diff = new StringBuilder();
        var j = 0;
        foreach (var (s, i) in before.Select((s, i) => (s, i)))
        {
            for (; j < after.Count && after[j].Tool != s.Tool; j++) diff.Append($"@@ added, line {after[j].Line}\n+ {ScriptWriter.Write(after[j])}\n");
            Assert.True(j < after.Count, $"statement {i + 1} ({s.Tool}, line {s.Line}) of prop.carvescript is not kept in order");
            var (a, b) = (ScriptWriter.Write(s), ScriptWriter.Write(after[j]));
            if (a != b) diff.Append($"@@ statement {i + 1}, line {s.Line} -> {after[j].Line}\n- {a}\n+ {b}\n");
            j++;
        }

        for (; j < after.Count; j++) diff.Append($"@@ added, line {after[j].Line}\n+ {ScriptWriter.Write(after[j])}\n");
        Directory.CreateDirectory(In("acceptance"));
        File.WriteAllText(In("acceptance/script.diff"), diff.ToString());
        Assert.True(diff.Length > 0, "the edited script is the exported one");
    }

    [Run]
    public void The_change_gives_four_equal_compartments_in_the_same_bounds()
    {
        var rows = new StringBuilder("file\tbacks\theights\tmin\tmax\n");
        foreach (var (file, expected) in new[] { ("prop.carve", 3), ("prop-edited.carve", 4) })
        {
            var mesh = CarveFile.Read(File.ReadAllText(In(file))).Mesh ?? throw new InvalidOperationException($"{file}: not one mesh");
            var heights = Backs(mesh).ToList();
            var (min, max) = Bounds(mesh);
            rows.Append($"{file}\t{heights.Count}\t{string.Join(",", heights.Select(h => h.ToString("0.####")))}\t{min}\t{max}\n");
            Directory.CreateDirectory(In("acceptance"));
            File.WriteAllText(In("acceptance/measure.tsv"), rows.ToString());
            Assert.True(heights.Count == expected, $"{file}: {heights.Count} compartment backs, not {expected}");
            Assert.True(heights.Max() - heights.Min() <= 0.001, $"{file}: back heights {string.Join(", ", heights)} differ past 1 mm");
            Assert.True(Near(min, new Vector3(-0.4f, 0, 0)) && Near(max, new Vector3(0.4f, 1.2f, 0.3f)), $"{file}: bounds {min} to {max}");
        }
    }

    [Run]
    public void Every_file_tool_path_is_inside_the_sessions_folder()
    {
        string? cwd = null;
        var outside = new List<string>();
        foreach (var line in File.ReadLines(In("transcript.jsonl")).Where(l => l.Length > 0))
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("cwd", out var c) && cwd is null) cwd = Path.GetFullPath(c.GetString()!);
            if (!root.TryGetProperty("message", out var m) || !m.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var use in content.EnumerateArray().Where(u => u.TryGetProperty("type", out var t) && t.GetString() == "tool_use"))
            {
                var name = use.GetProperty("name").GetString()!;
                if (name.StartsWith("mcp__carve__", StringComparison.Ordinal)) continue;
                var path = use.GetProperty("input").TryGetProperty("file_path", out var p) ? p.GetString() : null;
                if (cwd is null || path is null || !Path.GetFullPath(path, cwd).StartsWith(cwd + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    outside.Add($"{name} {path}");
            }
        }

        Assert.NotNull(cwd);
        Assert.Empty(outside);
    }

    private static IReadOnlyList<ScriptStatement> Statements(string name)
    {
        var (script, error) = ScriptReader.Read(File.ReadAllText(In(name)));
        return script?.Statements ?? throw new InvalidOperationException($"{name}: {error}");
    }

    /// <summary>clear, run_script of the round's script, save to a scratch folder; the saved bytes.</summary>
    private static async Task<byte[]> Replay(string script)
    {
        var scratch = Directory.CreateTempSubdirectory("t235-").FullName;
        await using var carve = new ToolHarness(workingDirectory: scratch);
        await carve.InitializeAsync();
        await Say(carve, "clear", "{}");
        await Say(carve, "run_script", JsonSerializer.Serialize(new { path = In(script) }));
        await Say(carve, "save", """{"path":"out.carve"}""");
        return File.ReadAllBytes(Path.Combine(scratch, "out.carve"));
    }

    private static async Task Say(ToolHarness carve, string tool, string arguments)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        var error = result.TryGetProperty("isError", out var e) && e.GetBoolean();
        Assert.False(error, $"{tool} {arguments}: {result}");
    }

    /// <summary>Heights of the faces facing +z that lie 0.2 or more behind the front (z = 0.3): the compartment backs.</summary>
    private static IEnumerable<float> Backs(PolygonMesh mesh)
    {
        foreach (var face in mesh.Faces.Where(f => !f.IsDeleted))
        {
            var points = face.Loop.Select(i => mesh.Vertices[i]).ToList();
            var normal = Vector3.Zero;
            for (var i = 0; i < points.Count; i++) normal += Vector3.Cross(points[i], points[(i + 1) % points.Count]);
            normal = Vector3.Normalize(normal);
            if (normal.Z > 1 - 1e-6 && points.All(p => p.Z <= 0.1f)) yield return points.Max(p => p.Y) - points.Min(p => p.Y);
        }
    }

    private static (Vector3 Min, Vector3 Max) Bounds(PolygonMesh mesh)
    {
        var live = Enumerable.Range(0, mesh.Vertices.Count).Where(i => !mesh.IsVertexDeleted(i)).Select(i => mesh.Vertices[i]).ToList();
        return (live.Aggregate(Vector3.Min), live.Aggregate(Vector3.Max));
    }

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= 1e-6f;
}

/// <summary>A fact skipped when no round folder is given.</summary>
public sealed class RunAttribute : FactAttribute
{
    public const string Variable = "CARVE_SCRIPT_ACCEPTANCE_RUN";

    public RunAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) is null) Skip = $"{Variable} is unset";
    }
}
