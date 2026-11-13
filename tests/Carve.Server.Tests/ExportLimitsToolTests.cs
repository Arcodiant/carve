using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>T-51: export warns on the limits of the game it writes into, read from that game's carve-limits.json.</summary>
[Trait("Tier", "regression")]
public sealed class ExportLimitsToolTests() : FileToolTests("carve-export-limits")
{
    private string Content => Path.Combine(Root, "game", "Content");

    private string LimitsFile => Path.Combine(Content, "carve-limits.json");

    private static string Args(object args) => JsonSerializer.Serialize(args);

    [Fact]
    public async Task A_static_export_warns_on_the_limits_found_above_it_and_still_writes()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Directory.CreateDirectory(Content);
        File.WriteAllText(LimitsFile, """{"max_triangles": 10, "max_submeshes": 1}""");

        var glb = Path.Combine(Content, "models", "crate.glb");
        var line = Ok(await carve.CallToolAsync("export", Args(new { path = glb })));
        Assert.Contains($"; limits from {LimitsFile}; 1 warning(s): 12 triangles, above the game's max_triangles 10", line);
        Assert.True(File.Exists(glb));

        // At the limit, nothing.
        File.WriteAllText(LimitsFile, """{"max_triangles": 12}""");
        var at = Ok(await carve.CallToolAsync("export", Args(new { path = glb, overwrite = true })));
        Assert.EndsWith($"; limits from {LimitsFile}", at);
    }

    [Fact]
    public async Task Write_limits_starts_the_file_then_fills_only_what_is_missing()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var first = Ok(await carve.CallToolAsync("export", Args(new { path = Path.Combine(Content, "models", "a.glb"), limits = Content, write_limits = true })));
        Assert.Contains($"; limits from {LimitsFile} (created with the starting numbers)", first);
        Assert.Equal("{\n  \"max_triangles\": 5000,\n  \"max_submeshes\": 8,\n  \"min_height\": 1.4,\n  \"max_height\": 2.4\n}\n", File.ReadAllText(LimitsFile));

        // The person edits the file to the game's own; a later write_limits keeps their numbers and fills the rest.
        File.WriteAllText(LimitsFile, """{"max_triangles": 11}""");
        var second = Ok(await carve.CallToolAsync("export", Args(new { path = Path.Combine(Content, "models", "b.glb"), write_limits = true })));
        Assert.Contains($"; limits from {LimitsFile} (filled max_submeshes, min_height, max_height with the starting numbers); 1 warning(s): 12 triangles, above the game's max_triangles 11", second);
        Assert.StartsWith("{\n  \"max_triangles\": 11,\n  \"max_submeshes\": 8,", File.ReadAllText(LimitsFile));
    }

    [Fact]
    public async Task Write_limits_with_no_file_found_creates_it_beside_the_glb()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var line = Ok(await carve.CallToolAsync("export", """{"path":"props/cube.glb","write_limits":true}"""));
        var file = Path.Combine(Root, "props", "carve-limits.json");
        Assert.Contains($"; limits from {file} (created with the starting numbers)", line);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task A_bad_limits_file_or_a_missing_named_one_is_an_error_that_writes_nothing()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Directory.CreateDirectory(Content);
        File.WriteAllText(LimitsFile, """{"max_triangles": "lots"}""");
        var glb = Path.Combine(Content, "crate.glb");

        Assert.Contains($"export: limits file {LimitsFile}: max_triangles: expected a whole number of at least 1, got \"lots\". Nothing was written.",
            Refused(await carve.CallToolAsync("export", Args(new { path = glb, write_limits = true }))));
        Assert.False(File.Exists(glb));
        Assert.Equal("""{"max_triangles": "lots"}""", File.ReadAllText(LimitsFile));

        var elsewhere = Path.Combine(Root, "elsewhere");
        Assert.Contains($"export: limits: no limits file at {Path.Combine(elsewhere, "carve-limits.json")}",
            Refused(await carve.CallToolAsync("export", Args(new { path = glb, limits = elsewhere }))));
        Assert.Contains("limits: expected a folder or a .json file as a string, got 3",
            Refused(await carve.CallToolAsync("export", Args(new { path = glb, limits = 3 }))));
        Assert.False(File.Exists(glb));
    }

    [Fact]
    public async Task A_refused_export_writes_no_limits_file_either()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Ok(await carve.CallToolAsync("export", """{"path":"cube.glb"}"""));
        Assert.Contains("already exists", Refused(await carve.CallToolAsync("export", """{"path":"cube.glb","write_limits":true}""")));
        Assert.False(File.Exists(Path.Combine(Root, "carve-limits.json")));
    }
}
