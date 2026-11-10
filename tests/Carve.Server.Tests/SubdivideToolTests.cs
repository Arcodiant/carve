using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.ToolList;

namespace Carve.Server.Tests;

/// <summary>subdivide through the real server: its example, counts, the budget warning, undo in one step, and the gardener's path.</summary>
[Trait("Tier", "regression")]
public sealed class SubdivideToolTests() : TempFolderTests("carve-subdivide")
{
    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static void Accepted(JsonElement result) => Assert.False(IsError(result), TextOf(result));

    private async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync()
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        return (carve, tools);
    }

    [Fact]
    public async Task The_example_smooths_the_cube_in_one_step_and_one_undo_takes_it_back()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));

        var body = Ok(await carve.CallToolAsync("subdivide", Examples(tools, "subdivide").Single()));

        Assert.Equal(26, body.GetProperty("vertices").GetInt32());
        Assert.Equal(24, body.GetProperty("faces").GetInt32());
        Assert.Equal(48, body.GetProperty("edges").GetInt32());
        Assert.Equal(48, body.GetProperty("triangles").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(1, body.GetProperty("levels").GetInt32());
        Assert.Equal(8, body.GetProperty("created").GetProperty("vertices").GetProperty("first").GetInt32());
        Assert.Equal(18, body.GetProperty("created").GetProperty("vertices").GetProperty("count").GetInt32());
        Assert.Equal(6, body.GetProperty("created").GetProperty("faces").GetProperty("first").GetInt32());
        Assert.Equal(18, body.GetProperty("created").GetProperty("faces").GetProperty("count").GetInt32());
        Assert.DoesNotContain(body.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("triangles"));

        var undone = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("subdivide", undone.GetProperty("undone").GetString());
        Assert.Equal(8, undone.GetProperty("vertices").GetInt32());
        Assert.Equal(6, undone.GetProperty("faces").GetInt32());
    }

    [Fact]
    public async Task Past_5000_triangles_the_result_warns_and_still_subdivides()
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
        Assert.Equal("the model has 5376 triangles, above the 5000 a game character should keep to",
            body.GetProperty("warnings")[0].GetString());
    }

    [Theory]
    [InlineData(false, 5000)]
    [InlineData(true, 5001)]
    public async Task The_triangles_count_every_mesh_and_only_past_5000_warn(bool extra, int triangles)
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
        Assert.Equal(extra, body.GetProperty("warnings").EnumerateArray().Any(w => w.GetString()!.Contains("triangles")));
    }

    [Fact]
    public async Task The_result_folds_not_planar_warnings_into_one_line_and_check_still_lists_each_face()
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

        // The ruling folds subdivide's own result only: check still names each face.
        var check = Ok(await carve.CallToolAsync("check", """{"mesh":"Body"}"""));
        Assert.Equal(count, check.GetProperty("warnings").EnumerateArray().Count(w => w.GetString()!.Contains("face is not planar:")));
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
