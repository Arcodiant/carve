using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public class EditToolDefinitionTests
{
    private static async Task<(ToolHarness Carve, JsonElement Tools)> StartAsync(bool define = true)
    {
        var carve = new ToolHarness();
        try
        {
            await carve.InitializeAsync();
            var tools = await carve.ListToolsAsync();
            if (define) Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
            return (carve, tools);
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(CarveProcess.IsError(result), CarveProcess.TextOf(result));
        return JsonDocument.Parse(CarveProcess.TextOf(result)).RootElement;
    }

    private static void ExpectCleanClosed(JsonElement body)
    {
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(0, body.GetProperty("warnings").GetArrayLength());
    }

    [Theory]
    [InlineData("move")]
    [InlineData("extrude")]
    [InlineData("get_mesh")]
    public async Task Every_worked_example_in_a_description_runs_on_the_cube_closed_and_warning_free(string tool)
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;

        var examples = Examples(tools, tool);
        Assert.NotEmpty(examples);
        foreach (var example in examples) ExpectCleanClosed(Ok(await carve.CallToolAsync(tool, example)));
    }

    [Fact]
    public async Task Every_edit_report_shape_in_a_description_names_deleted_and_split()
    {
        // Every edit's body carries deleted and split (MeshToolSupport.Edit, define_mesh, subdivide), so every
        // description whose "Returns JSON" is an edit report, {vertices, faces, edges, ...}, names both.
        var (carve, tools) = await StartAsync(define: false);
        using var _ = carve;
        var reports = tools.EnumerateArray()
            .Select(t => (Name: t.GetProperty("name").GetString()!, Text: t.GetProperty("description").GetString()!))
            .Where(t => t.Text.Contains("Returns JSON {vertices, faces, edges,", StringComparison.Ordinal))
            .ToArray();
        Assert.Superset(new HashSet<string> { "define_mesh", "move", "extrude", "subdivide", "delete", "merge_faces", "bridge", "plane_cut", "loop_cut", "knife" },
            reports.Select(t => t.Name).ToHashSet());
        foreach (var (name, text) in reports)
        {
            var shape = text[text.IndexOf("Returns JSON {vertices, faces, edges,", StringComparison.Ordinal)..];
            Assert.True(shape.Contains("deleted:{vertices:", StringComparison.Ordinal), name + ": " + shape);
            Assert.True(shape.Contains("split:[", StringComparison.Ordinal), name + ": " + shape);
        }
    }

    [Fact]
    public async Task Render_names_every_edit_and_get_mesh_s_shape_reads_in_one_piece()
    {
        var (carve, tools) = await StartAsync(define: false);
        using var _ = carve;
        var render = Description(tools, "render");
        var first = render[..render.IndexOf(". ", StringComparison.Ordinal)];
        foreach (var edit in new[] { "move", "extrude", "subdivide", "delete", "merge_faces", "bridge", "plane_cut", "loop_cut", "knife", "undo" })
            Assert.Contains(edit, first);

        var getMesh = Description(tools, "get_mesh");
        var start = getMesh.IndexOf("Returns JSON {", StringComparison.Ordinal);
        var shape = getMesh[start..(getMesh.IndexOf("}. Here vertices", start, StringComparison.Ordinal) + 1)];
        Assert.Contains("edges:[{vertices:[a,b]", shape);
        Assert.DoesNotContain("null in its slot", shape);
        Assert.Contains("null in its slot", getMesh);
    }

    [Fact]
    public async Task The_undo_example_undoes_an_extrude()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        var example = Examples(tools, "undo").Single();
        Ok(await carve.CallToolAsync("extrude", Examples(tools, "extrude")[0]));

        var body = Ok(await carve.CallToolAsync("undo", example));

        Assert.Equal("extrude", body.GetProperty("undone").GetString());
        ExpectCleanClosed(body);
    }

    [Fact]
    public async Task Move_returns_the_report_and_the_ids_it_moved_and_the_example_raises_the_top()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;

        var body = Ok(await carve.CallToolAsync("move", Examples(tools, "move").Single()));

        Assert.Equal(8, body.GetProperty("vertices").GetInt32());
        Assert.Equal(1.5f, body.GetProperty("bounds").GetProperty("max")[1].GetSingle());
        Assert.Empty(body.GetProperty("created").GetProperty("vertices").EnumerateArray());
        Assert.Empty(body.GetProperty("created").GetProperty("faces").EnumerateArray());
        Assert.Equal([2, 3, 6, 7], body.GetProperty("moved").EnumerateArray().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task The_get_mesh_sample_output_matches_the_real_output_on_the_cube()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        const string marker = "Output: ";
        var line = Description(tools, "get_mesh").Split('\n').Single(l => l.StartsWith(marker, StringComparison.Ordinal));
        var sample = JsonDocument.Parse(line[marker.Length..].Replace(", ...", "", StringComparison.Ordinal)).RootElement;
        var real = Ok(await carve.CallToolAsync("get_mesh", "{}"));

        static string Raw(JsonElement x) => JsonSerializer.Serialize(x);
        foreach (var v in sample.GetProperty("vertices").EnumerateArray())
            Assert.Equal(Raw(v), Raw(real.GetProperty("vertices")[v.GetProperty("id").GetInt32()]));
        foreach (var f in sample.GetProperty("faces").EnumerateArray())
            Assert.Equal(Raw(f), Raw(real.GetProperty("faces")[f.GetProperty("id").GetInt32()]));
        foreach (var e in sample.GetProperty("edges").EnumerateArray())
            Assert.Contains(Raw(e), real.GetProperty("edges").EnumerateArray().Select(Raw));
        foreach (var key in new[] { "closed", "bounds", "warnings" })
            Assert.Equal(Raw(sample.GetProperty(key)), Raw(real.GetProperty(key)));
    }

    [Fact]
    public async Task Tools_list_shows_each_schema()
    {
        var (carve, tools) = await StartAsync(define: false);
        using var _ = carve;

        var by = Tool(tools, "move").GetProperty("inputSchema").GetProperty("properties").GetProperty("by");
        Assert.Equal(3, by.GetProperty("minItems").GetInt32());
        Assert.Equal(3, by.GetProperty("maxItems").GetInt32());
        var pair = Tool(tools, "move").GetProperty("inputSchema").GetProperty("properties").GetProperty("edges").GetProperty("items");
        Assert.Equal(2, pair.GetProperty("minItems").GetInt32());
        Assert.Equal(2, pair.GetProperty("maxItems").GetInt32());
        Assert.Equal(["by"], Tool(tools, "move").GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(x => x.GetString()));

        var extrude = Tool(tools, "extrude").GetProperty("inputSchema");
        Assert.Equal(["faces", "distance"], extrude.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(0, extrude.GetProperty("properties").GetProperty("inset").GetProperty("default").GetInt32());

        foreach (var name in new[] { "get_mesh", "undo" })
            Assert.Equal("object", Tool(tools, name).GetProperty("inputSchema").GetProperty("type").GetString());
        foreach (var name in new[] { "move", "extrude", "get_mesh", "undo" })
            Assert.Contains("Worked example", Description(tools, name));
        foreach (var name in new[] { "define_mesh", "render", "move", "extrude", "get_mesh", "undo" })
            Assert.False(Tool(tools, name).GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean(), name);
        Assert.Equal(["view", "direction", "zoom", "target", "projection", "size", "mode", "highlight", "labels", "views", "columns", "mesh", "pose", "clip", "time", "targets", "bones", "bone_labels", "weights"],
            Tool(tools, "render").GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Contains("ids are the positions given here", Description(tools, "define_mesh"));
    }
}
