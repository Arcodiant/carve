using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.ToolList;

namespace Carve.Server.Tests;

/// <summary>subdivide through the real server: its example, counts, the budget warning, undo in one step, and the gardener's path.</summary>
[Trait("Tier", "unit")]
public sealed class SubdivideToolTests() : TempFolderTests("carve-subdivide")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static void Accepted(JsonElement result) => Assert.False(IsError(result), TextOf(result));

    private async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        return (carve, tools);
    }

    [Fact]
    public async Task After_a_delete_the_report_counts_live_triangles_and_created_names_the_appended_block()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        Ok(await carve.CallToolAsync("delete", """{"faces":[3]}"""));

        var body = Ok(await carve.CallToolAsync("subdivide", """{"levels":1}"""));

        // Five live quads make 20 quads, 40 triangles; the deleted face's slot makes none.
        Assert.Equal(40, body.GetProperty("triangles").GetInt32());
        // Face points 8..13 (11 is the deleted face's, a tombstone), then 12 edge points 14..25.
        var created = body.GetProperty("created").GetProperty("vertices");
        Assert.Equal(8, created.GetProperty("first").GetInt32());
        Assert.Equal(18, created.GetProperty("count").GetInt32());

        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var slots = mesh.GetProperty("vertices").EnumerateArray().ToArray();
        Assert.Equal(26, slots.Length);
        Assert.Equal(JsonValueKind.Null, slots[11].ValueKind);
        Assert.NotEqual(JsonValueKind.Null, slots[8].ValueKind);
    }

    [Fact]
    public async Task After_a_delete_each_level_makes_a_deleted_face_point_and_the_description_says_so()
    {
        // RV-32: two levels after one deleted face leave two tombstones in the block, one per level.
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Assert.Contains("one deleted slot (null in get_mesh) per deleted face per level", Description(tools, "subdivide"));
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        Ok(await carve.CallToolAsync("delete", """{"faces":[3]}"""));

        var body = Ok(await carve.CallToolAsync("subdivide", """{"levels":2}"""));
        var created = body.GetProperty("created").GetProperty("vertices");
        Assert.Equal(8, created.GetProperty("first").GetInt32());
        Assert.Equal(83, created.GetProperty("count").GetInt32());

        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var slots = mesh.GetProperty("vertices").EnumerateArray().ToArray();
        Assert.Equal(91, slots.Length);
        Assert.Equal([11, 29], Enumerable.Range(0, slots.Length).Where(i => slots[i].ValueKind == JsonValueKind.Null));
    }

    [Fact]
    public async Task Past_5000_triangles_the_result_gives_the_count_and_no_limit_warning()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        // A flat 7 by 6 grid of quads: 42 quads, 2688 after three levels, 5376 triangles.
        var vertices = new List<float[]>();
        for (var y = 0; y <= 6; y++) for (var x = 0; x <= 7; x++) vertices.Add([x, y, 0]);
        var faces = new List<int[]>();
        for (var y = 0; y < 6; y++) for (var x = 0; x < 7; x++) faces.Add([y * 8 + x, y * 8 + x + 1, (y + 1) * 8 + x + 1, (y + 1) * 8 + x]);
        Ok(await carve.CallToolAsync("define_mesh", JsonSerializer.Serialize(new { vertices, faces })));

        var body = Ok(await carve.CallToolAsync("subdivide", """{"levels":3}"""));

        Assert.Equal(2688, body.GetProperty("faces").GetInt32());
        Assert.Equal(5376, body.GetProperty("triangles").GetInt32());
        // T-51: limits are the game's, which subdivide cannot know, so only export warns on them.
        Assert.DoesNotContain(body.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("triangles"));
    }

    [Theory]
    [InlineData(false, 5000)]
    [InlineData(true, 5001)]
    public async Task The_triangles_count_every_mesh(bool extra, int triangles)
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        // "pad": a flat 48 by 52 grid, 2496 quads, 4992 triangles, and one loose triangle more when extra.
        var vertices = new List<float[]>();
        for (var y = 0; y <= 52; y++) for (var x = 0; x <= 48; x++) vertices.Add([x, y, 0]);
        var faces = new List<int[]>();
        for (var y = 0; y < 52; y++) for (var x = 0; x < 48; x++) faces.Add([y * 49 + x, y * 49 + x + 1, (y + 1) * 49 + x + 1, (y + 1) * 49 + x]);
        if (extra)
        {
            faces.Add([vertices.Count, vertices.Count + 1, vertices.Count + 2]);
            vertices.AddRange([[60, 0, 0], [61, 0, 0], [60, 1, 0]]);
        }
        Ok(await carve.CallToolAsync("define_mesh", JsonSerializer.Serialize(new { name = "pad", vertices, faces })));
        // "tile": one quad, four after one level, so 8 triangles of the model's total.
        Ok(await carve.CallToolAsync("define_mesh", """{"name":"tile","vertices":[[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,1,2,3]]}"""));

        var body = Ok(await carve.CallToolAsync("subdivide", """{"mesh":"tile","levels":1}"""));

        Assert.Equal(4, body.GetProperty("faces").GetInt32());
        Assert.Equal(triangles, body.GetProperty("triangles").GetInt32());
        Assert.DoesNotContain(body.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("triangles"));
    }

    [Fact]
    public async Task The_result_folds_not_planar_warnings_into_one_line_and_check_folds_by_its_own_rule()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        var sample = Path.Combine(Paths.RepoRoot, "samples", "gardener.carve");
        Accepted(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = sample })));

        var body = Ok(await carve.CallToolAsync("subdivide", """{"mesh":"Body","levels":1}"""));

        var warnings = body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToList();
        Assert.DoesNotContain(warnings, w => w.Contains("face is not planar:"));
        var folded = Assert.Single(warnings, w => w.EndsWith("not planar (expected after subdivide)"));
        Assert.Equal(warnings.Count - 1, warnings.IndexOf(folded));
        var selected = Ok(await carve.CallToolAsync("select", """{"mesh":"Body","kind":"faces","where":"non_planar"}"""));
        var count = selected.GetProperty("count").GetInt32();
        Assert.True(count > 1, $"only {count} non-planar faces");
        Assert.Equal($"{count} faces are not planar (expected after subdivide)", folded);

        // Subdivide keeps its own fold (T-43); check folds by T-72's rule: 10 named, the rest one line, adding up to select's count.
        var check = Ok(await carve.CallToolAsync("check", """{"mesh":"Body"}"""));
        var lines = check.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToList();
        Assert.Equal(10, lines.Count(w => w.Contains("face is not planar:")));
        Assert.Contains($"and {count - 10} more faces are not planar; select faces where non_planar names them", lines);
    }

    [Theory]
    [InlineData("{}", "levels: required but missing")]
    [InlineData("""{"levels":0}""", "levels: 0 is outside 1 to 3")]
    [InlineData("""{"levels":4}""", "levels: 4 is outside 1 to 3")]
    [InlineData("""{"levels":1.5}""", "levels: expected a whole number 1 to 3, got 1.5")]
    [InlineData("""{"levels":"two"}""", "levels: expected a whole number 1 to 3")]
    [InlineData("""{"levels":1,"level":2}""", "level: not an argument of subdivide; its arguments are levels, mesh")]
    [InlineData("""{"levels":1,"mesh":"nope"}""", "nope")]
    public async Task Bad_input_is_a_tool_error_and_leaves_the_mesh(string arguments, string message)
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));

        var result = await carve.CallToolAsync("subdivide", arguments);

        Assert.True(IsError(result), TextOf(result));
        Assert.Contains(message, TextOf(result));
        Assert.Equal(8, Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("vertices").GetArrayLength());
    }

    [Fact]
    public async Task The_gardener_subdivides_by_mesh_saves_reopens_and_exports_skinned_with_every_vertex_weighted()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        var sample = Path.Combine(Paths.RepoRoot, "samples", "gardener.carve");
        Accepted(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = sample })));

        var body = Ok(await carve.CallToolAsync("subdivide", """{"mesh":"Body","levels":1}"""));
        Assert.Equal(1, body.GetProperty("levels").GetInt32());
        Accepted(await carve.CallToolAsync("subdivide", """{"mesh":"Hat","levels":1}"""));

        Accepted(await carve.CallToolAsync("save", """{"path":"soft.carve"}"""));
        Accepted(await carve.CallToolAsync("open", """{"path":"soft.carve"}"""));
        // With a skeleton, export refuses any unweighted vertex: every new vertex took weights from its sources.
        var export = await carve.CallToolAsync("export", """{"path":"soft.glb"}""");
        Accepted(export);
        Assert.True(File.Exists(Path.Combine(Root, "soft.glb")));
        Assert.Contains("skinned: 2 piece(s)", TextOf(export));
        Assert.Contains("2 clip(s)", TextOf(export));
    }
}
