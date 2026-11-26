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

    /// <summary>T-200's silent column: the same run on a second carve, each call at <c>verbosity: "silent"</c>; null to skip it.</summary>
    private ToolHarness? _silent;

    /// <summary>The tools of the run that take verbosity (T-200); project_uvs gets it in T-201.</summary>
    private static readonly string[] Quietened = ["open", "check", "subdivide", "export", "delete", "undo"];

    private static string TextOf(JsonElement result) =>
        string.Concat(result.GetProperty("content").EnumerateArray().Select(c => c.GetProperty("text").GetString()));

    private async Task<JsonElement> Call(ToolHarness carve, string tool, string arguments)
    {
        var result = await carve.CallToolAsync(tool, arguments);
        var text = TextOf(result);
        Assert.False(result.TryGetProperty("isError", out var error) && error.GetBoolean(), $"{tool}: {text}");
        _seen.Add(($"{tool} {arguments}", text.Length));
        Assert.True(text.Length <= Budget, $"{tool} {arguments}: {text.Length} characters, over {Budget}: {text[..Math.Min(text.Length, 600)]}");
        var body = text.StartsWith('{') ? JsonDocument.Parse(text).RootElement : default;
        if (_silent is not null)
        {
            var quiet = Quietened.Contains(tool);
            var silent = await _silent.CallToolAsync(tool, quiet ? arguments[..^1] + (arguments == "{}" ? "" : ",") + "\"verbosity\":\"silent\"}" : arguments);
            Assert.False(silent.TryGetProperty("isError", out var e) && e.GetBoolean(), $"{tool} silent: {TextOf(silent)}");
            // check answers its own ok (M-23's ruling 3, read in T-200); every other result is exactly {"ok":true}.
            var expected = tool == "check" ? $"{{\"ok\":{(body.GetProperty("ok").GetBoolean() ? "true" : "false")}}}" : """{"ok":true}""";
            if (quiet) Assert.Equal(expected, TextOf(silent));
        }
        return body;
    }

    [Fact]
    public async Task Every_result_on_the_cage_is_within_the_budget()
    {
        var folder = Directory.CreateTempSubdirectory("carve-dense-").FullName;
        try
        {
            await using var carve = new ToolHarness(folder);
            await carve.InitializeAsync();
            await using var silent = new ToolHarness(Directory.CreateDirectory(Path.Combine(folder, "silent")).FullName);
            await silent.InitializeAsync();
            _silent = silent;
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

            // The silent run made the same changes.
            Assert.Equal(TextOf(await carve.CallToolAsync("check", "{}")), TextOf(await silent.CallToolAsync("check", "{}")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>T-202: unwrap on the four tool samples (39, 95, 135 and 67 islands) is within the budget, and says the island count.</summary>
    [Theory]
    [InlineData("wrench.carve", 39)]
    [InlineData("cutter-src.carve", 95)]
    [InlineData("toolkit-src.carve", 135)]
    [InlineData("scanner-src.carve", 67)]
    public async Task Unwrap_on_a_tool_sample_is_within_the_budget(string sample, int islands)
    {
        var folder = Directory.CreateTempSubdirectory("carve-unwrap-").FullName;
        try
        {
            await using var carve = new ToolHarness(folder);
            await carve.InitializeAsync();
            await Call(carve, "open", JsonSerializer.Serialize(new { path = Path.Combine(Paths.RepoRoot, "samples", sample) }));
            var made = await Call(carve, "unwrap", "{}");
            Assert.Equal(islands, made.GetProperty("islands").GetInt32());
            Assert.Equal(5, made.GetProperty("largest").GetArrayLength());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
