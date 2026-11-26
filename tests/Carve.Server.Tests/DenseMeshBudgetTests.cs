using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-197: on fps-arms' cage (<c>samples/arms-cage.carve</c>) every result of the dense-mesh run M-23 measured at planning is
/// <see cref="Budget"/> characters or less: open, check, subdivide twice, check, export, delete of 200 faces (798 open edges;
/// 41,310 characters at planning), check, undo, project_uvs box on faces 0 to 99 then check (280 overlaps; 43,700 at planning),
/// and a delete of 200 other faces then check (both at once; 61,363 at planning).
/// </summary>
[Trait("Tier", "unit")]
public sealed class DenseMeshBudgetTests
{
    /// <summary>The most characters of result text a call may answer at the default verbosity (M-23's ruling 5).</summary>
    public const int Budget = 2000;

    private readonly List<(string Call, int Length)> _seen = [];

    private async Task<JsonElement> Call(ToolHarness carve, string tool, string arguments)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        var text = string.Concat(result.GetProperty("content").EnumerateArray().Select(c => c.GetProperty("text").GetString()));
        Assert.False(result.TryGetProperty("isError", out var error) && error.GetBoolean(), $"{tool}: {text}");
        _seen.Add(($"{tool} {arguments}", text.Length));
        Assert.True(text.Length <= Budget, $"{tool} {arguments}: {text.Length} characters, over {Budget}: {text[..Math.Min(text.Length, 600)]}");
        return text.StartsWith('{') ? JsonDocument.Parse(text).RootElement : default;
    }

    [Fact]
    public async Task Every_result_on_the_cage_is_within_the_budget()
    {
        var folder = Directory.CreateTempSubdirectory("carve-dense-").FullName;
        try
        {
            await using var carve = new ToolHarness(folder);
            await carve.InitializeAsync();
            var cage = Path.Combine(Paths.RepoRoot, "samples", "arms-cage.carve");

            await Call(carve, "open", JsonSerializer.Serialize(new { path = cage }));
            await Call(carve, "check", "{}");
            await Call(carve, "subdivide", """{"levels":1}""");
            var twice = await Call(carve, "subdivide", """{"levels":1}""");
            Assert.Equal((9492, 9488), (twice.GetProperty("vertices").GetInt32(), twice.GetProperty("faces").GetInt32()));
            // T-198: the cage is smooth, so its not-planar faces are a note and not a warning, and it reads ok.
            var cageCheck = await Call(carve, "check", "{}");
            Assert.True(cageCheck.GetProperty("ok").GetBoolean(), cageCheck.ToString());
            Assert.Contains(cageCheck.GetProperty("notes").EnumerateArray(), n => n.GetString()!.Contains(" faces are not planar: worst faces[") && n.GetString()!.EndsWith("expected on a smooth mesh, which export triangulates"));
            await Call(carve, "export", """{"path":"cage.glb"}""");

            var deleted = await Call(carve, "delete", JsonSerializer.Serialize(new { faces = Enumerable.Range(0, 200).Select(i => i * 2) }));
            var open = Assert.Single(deleted.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("only one face")).GetString();
            Assert.StartsWith("798 edges belong to only one face: e.g. ", open);
            await Call(carve, "check", "{}");

            // Faces 0 to 99 are half deleted now, so the delete is undone first (planning measured the two apart, then both).
            await Call(carve, "undo", "{}");
            await Call(carve, "project_uvs", JsonSerializer.Serialize(new { method = "box", faces = Enumerable.Range(0, 100) }));
            var check = await Call(carve, "check", "{}");
            Assert.Contains(check.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains(" faces have UVs that overlap another face: e.g. faces["));

            await Call(carve, "delete", JsonSerializer.Serialize(new { faces = Enumerable.Range(0, 200).Select(i => i * 2 + 1) }));
            await Call(carve, "check", "{}");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
