using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>T-51: export warns on the limits of the game it writes into, read from that game's carve-limits.json.</summary>
[Trait("Tier", "regression")]
public sealed class ExportLimitsToolTests() : FileToolTests("carve-export-limits")
{
    private string Content => Path.Combine(Root, "game", "Content");

    private string LimitsFile => Path.Combine(Content, "carve-limits.json");

    private static string Args(object args) => JsonSerializer.Serialize(args);

    /// <summary>
    /// The upward search runs past the temp folder to the drive's root, so a carve-limits.json above it would be read,
    /// and filled by write_limits. Each test that searches checks first that there is none, and fails without writing.
    /// </summary>
    private void NoLimitsFileAboveRoot()
    {
        for (var folder = new DirectoryInfo(Root).Parent; folder is not null; folder = folder.Parent)
            Assert.False(File.Exists(Path.Combine(folder.FullName, "carve-limits.json")), $"a carve-limits.json in {folder.FullName} would be found by these tests");
    }

    [Fact]
    public async Task A_static_export_warns_on_the_limits_found_above_it_and_still_writes()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Directory.CreateDirectory(Content);
        File.WriteAllText(LimitsFile, """{"max_triangles": 10, "max_submeshes": 2}""");

        var glb = Path.Combine(Content, "models", "crate.glb");
        var line = Ok(await carve.CallToolAsync("export", Args(new { path = glb })));
        Assert.Contains($"; limits from {LimitsFile}; 1 warning(s): 12 triangles, above the game's max_triangles 10", line);
        Assert.True(File.Exists(glb));

        // At the limit, nothing.
        File.WriteAllText(LimitsFile, """{"max_triangles": 12}""");
        var at = Ok(await carve.CallToolAsync("export", Args(new { path = glb, overwrite = true })));
        Assert.EndsWith($"; limits from {LimitsFile}", at);

        // Two materials ('carve' and one colour) past max_submeshes 1.
        Ok(await carve.CallToolAsync("set_colour", """{"faces":[0],"colour":"#FF0000"}"""));
        File.WriteAllText(LimitsFile, """{"max_submeshes": 1}""");
        var coloured = Ok(await carve.CallToolAsync("export", Args(new { path = glb, overwrite = true })));
        Assert.EndsWith($"; limits from {LimitsFile}; 1 warning(s): 2 submeshes and 1 colours, above the game's max_submeshes 1; share colours between pieces where you can", coloured);
    }

    [Fact]
    public async Task A_skinned_export_warns_only_on_the_games_file_and_clips_mode_reads_and_writes_it()
    {
        NoLimitsFileAboveRoot();
        using var carve = await SkinnedExportToolTests.StartChainAsync(Root);

        // No file: no warnings at all, whatever the size.
        var none = Ok(await carve.CallToolAsync("export", """{"path":"chain.glb"}"""));
        Assert.Contains("; no game limits: no carve-limits.json in ", none);
        Assert.DoesNotContain("warning(s)", none);

        File.WriteAllText(Path.Combine(Root, "carve-limits.json"), """{"max_triangles": 23, "max_submeshes": 1}""");
        var limited = Ok(await carve.CallToolAsync("export", """{"path":"chain.glb","overwrite":true}"""));
        Assert.Contains($"; limits from {Path.Combine(Root, "carve-limits.json")}; 2 warning(s): 24 triangles, above the game's max_triangles 23; 2 submeshes and 0 colours, above the game's max_submeshes 1", limited);

        // Clips mode has no mesh, so no limit warnings, but it fills the file as model mode does.
        var clips = Ok(await carve.CallToolAsync("export", """{"path":"chain.clips.glb","mode":"clips","write_limits":true}"""));
        Assert.EndsWith($"; limits from {Path.Combine(Root, "carve-limits.json")} (filled min_height, max_height with the starting numbers)", clips);
        Assert.Equal(new GameLimits(23, 1, 1.4f, 2.4f), GameLimits.Parse(File.ReadAllText(Path.Combine(Root, "carve-limits.json")), out _));
    }

    [Fact]
    public async Task Write_limits_creates_a_named_json_file_that_is_not_there_yet()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var named = Path.Combine(Root, "shared", "props-limits.json");
        var line = Ok(await carve.CallToolAsync("export", Args(new { path = Path.Combine(Content, "a.glb"), limits = named, write_limits = true })));
        Assert.Contains($"; limits from {named} (created with the starting numbers)", line);
        Assert.Equal(GameLimits.Starting, GameLimits.Parse(File.ReadAllText(named), out _));
        Assert.False(File.Exists(LimitsFile));
    }

    [Fact]
    public async Task With_no_file_a_model_past_every_starting_number_gets_no_limit_warning()
    {
        NoLimitsFileAboveRoot();
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        // A flat grid of 51 by 51 quads, 5202 triangles, and ten colours: past Orcward's 5000 and 8.
        var vertices = new List<float[]>();
        for (var y = 0; y <= 51; y++) for (var x = 0; x <= 51; x++) vertices.Add([x, y, 0]);
        var faces = new List<int[]>();
        for (var y = 0; y < 51; y++) for (var x = 0; x < 51; x++) faces.Add([y * 52 + x, y * 52 + x + 1, (y + 1) * 52 + x + 1, (y + 1) * 52 + x]);
        Ok(await carve.CallToolAsync("define_mesh", Args(new { vertices, faces })));
        for (var c = 0; c < 10; c++) Ok(await carve.CallToolAsync("set_colour", Args(new { faces = new[] { c }, colour = $"#0000{c:X2}" })));

        var line = Ok(await carve.CallToolAsync("export", """{"path":"big.glb"}"""));
        Assert.Contains("5202 triangles", line);
        Assert.Contains("; no game limits: no carve-limits.json in ", line);
        Assert.DoesNotContain("above the game's", line);
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
        NoLimitsFileAboveRoot();
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
        File.WriteAllText(Path.Combine(Root, "notes.txt"), "x");
        Assert.Contains("is a file but not a .json one",
            Refused(await carve.CallToolAsync("export", Args(new { path = glb, limits = Path.Combine(Root, "notes.txt"), write_limits = true }))));
        Assert.Contains("the file being exported",
            Refused(await carve.CallToolAsync("export", Args(new { path = glb, limits = glb, write_limits = true }))));
        Assert.False(File.Exists(glb));
    }

    [Fact]
    public async Task A_refused_export_writes_no_limits_file_either()
    {
        NoLimitsFileAboveRoot();
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        Ok(await carve.CallToolAsync("export", """{"path":"cube.glb"}"""));
        Assert.Contains("already exists", Refused(await carve.CallToolAsync("export", """{"path":"cube.glb","write_limits":true}""")));
        Assert.False(File.Exists(Path.Combine(Root, "carve-limits.json")));
    }
}
