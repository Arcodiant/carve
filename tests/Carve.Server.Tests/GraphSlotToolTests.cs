using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-160: define_material's graph slots ({graph, output, size} in place of a PNG path) on the in-process harness: the worked
/// example as written, every refusal in its words, get_mesh, save and open (version 10, and the warning for a missing or
/// broken graph), undo and redo.
/// </summary>
[Trait("Tier", "unit")]
public class GraphSlotToolTests : FileToolTests
{
    public GraphSlotToolTests() : base("graphslot")
    {
    }

    /// <summary>A graph of size <paramref name="size"/> with base_colour and roughness outputs, written into the working folder.</summary>
    private string WriteGraph(string relative = "graphs/iron.texgraph", int size = 512)
    {
        var path = Path.GetFullPath(Path.Combine(Root, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{ "version": 1, "size": {{size}}, "seed": 0, "nodes": { "g": { "type": "gradient" }, "c": { "type": "output", "inputs": { "in": "g" }, "params": { "role": "base_colour" } }, "r": { "type": "output", "inputs": { "in": "g" }, "params": { "role": "roughness" } } } }""");
        return path;
    }

    private static JsonElement Body(string text) => JsonDocument.Parse(text).RootElement;

    private static JsonElement Slot(JsonElement material, string field) => material.GetProperty(field);

    private const string Iron = """{"name":"iron","base_colour_texture":{"graph":"graphs/iron.texgraph","output":"base_colour","size":256}}""";

    [Theory]
    [InlineData(32, 64)]
    [InlineData(8192, 4096)]
    public async Task A_graphs_own_size_outside_the_range_is_clamped_into_it(int own, int taken)
    {
        WriteGraph(size: own);
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var result = Body(Ok(await carve.CallToolAsync("define_material", """{"name":"iron","base_colour_texture":{"graph":"graphs/iron.texgraph","output":"base_colour"}}""")));
        Assert.Equal(taken, Slot(result.GetProperty("material"), "base_colour_texture").GetProperty("size").GetInt32());
    }

    [Fact]
    public async Task The_worked_example_defines_two_graph_slots_and_get_mesh_lists_them_the_same_way()
    {
        var graph = WriteGraph();
        var (carve, tools) = await StartAsync();
        using var scope = carve;

        var examples = Examples(tools, "define_material");
        Assert.Equal(2, examples.Length);
        var result = Body(Ok(await carve.CallToolAsync("define_material", examples[1])));
        var material = result.GetProperty("material");
        Assert.Equal("weathered-iron", material.GetProperty("name").GetString());
        Assert.Equal(graph, Slot(material, "base_colour_texture").GetProperty("graph").GetString());
        Assert.Equal("base_colour", Slot(material, "base_colour_texture").GetProperty("output").GetString());
        Assert.Equal(256, Slot(material, "base_colour_texture").GetProperty("size").GetInt32());
        // Left out, the size is the graph's own, 512, as the example says.
        Assert.Equal("roughness", Slot(material, "metallic_roughness_texture").GetProperty("output").GetString());
        Assert.Equal(512, Slot(material, "metallic_roughness_texture").GetProperty("size").GetInt32());
        Assert.Equal(JsonValueKind.Null, Slot(material, "normal_texture").ValueKind);
        Assert.Empty(result.GetProperty("warnings").EnumerateArray());
        Assert.Contains("graphs/iron.texgraph a graph of size 512", Description(tools, "define_material"));

        var listed = Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("materials").EnumerateArray().Single();
        Assert.Equal(material.GetRawText(), listed.GetRawText());
    }

    [Fact]
    public async Task A_graph_and_a_png_mix_and_an_emissive_graph_makes_the_emissive_white()
    {
        WriteGraph();
        File.Copy(Path.Combine(Paths.RepoRoot, "tests", "Carve.Server.Tests", "Fixtures", "swatch.png"), Path.Combine(Root, "swatch.png"));
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var material = Body(Ok(await carve.CallToolAsync("define_material",
            """{"name":"glow","normal_texture":"swatch.png","emissive_texture":{"graph":"graphs/iron.texgraph","output":"base_colour"}}"""))).GetProperty("material");
        Assert.Equal("#FFFFFF", material.GetProperty("emissive").GetString());
        Assert.Equal(4, Slot(material, "normal_texture").GetProperty("width").GetInt32());
        Assert.Equal(512, Slot(material, "emissive_texture").GetProperty("size").GetInt32());
    }

    [Theory]
    [InlineData("""{"graph":"graphs/iron.texgraph","output":"base_colour","seed":3}""", "base_colour_texture.seed: a graph slot takes only graph, output and size")]
    [InlineData("""{"output":"base_colour"}""", "base_colour_texture: a graph slot needs graph")]
    [InlineData("""{"graph":"graphs/iron.texgraph"}""", "base_colour_texture: a graph slot needs output")]
    [InlineData("""{"graph":7,"output":"base_colour"}""", "base_colour_texture.graph: expected a string, got")]
    [InlineData("""{"graph":"graphs/iron.texgraph","output":"base_colour","size":"big"}""", "base_colour_texture.size: expected an integer, got")]
    [InlineData("""{"graph":"graphs/iron.texgraph","output":"base_colour","size":100}""", "base_colour_texture: size is 100; it is a power of two from 64 to 4096")]
    [InlineData("""{"graph":"graphs/iron.texgraph","output":"base_colour","size":8192}""", "base_colour_texture: size is 8192; it is a power of two from 64 to 4096")]
    [InlineData("""{"graph":"graphs/iron.texgraph","output":"Base Colour"}""", "base_colour_texture: output 'Base Colour' is not an output name")]
    [InlineData("""{"graph":"graphs/iron.png","output":"base_colour"}""", "base_colour_texture.graph: path \"graphs/iron.png\"")]
    [InlineData("""{"graph":"graphs","output":"base_colour"}""", "base_colour_texture.graph: path \"graphs\"")]
    public async Task A_malformed_graph_slot_is_refused_naming_the_argument(string slot, string expected)
    {
        WriteGraph();
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var error = Refused(await carve.CallToolAsync("define_material", $$"""{"name":"iron","base_colour_texture":{{slot}}}"""));
        Assert.Contains(expected, error);
        Assert.Empty(Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).TryGetProperty("materials", out var m) ? m.EnumerateArray() : []);
    }

    [Fact]
    public async Task A_missing_graph_lists_the_texgraph_files_in_the_folder_it_looked_in()
    {
        WriteGraph("graphs/worn-steel.texgraph");
        WriteGraph("graphs/planks.texgraph");
        File.WriteAllText(Path.Combine(Root, "graphs", "notes.png"), "");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var error = Refused(await carve.CallToolAsync("define_material", """{"name":"iron","base_colour_texture":{"graph":"graphs/steel-worn.texgraph","output":"base_colour"}}"""));
        var folder = Path.Combine(Root, "graphs");
        Assert.Contains($"base_colour_texture.graph: no file at {Path.Combine(folder, "steel-worn.texgraph")}; the .texgraph files in {folder} (2) are: planks.texgraph, worn-steel.texgraph", error);

        var none = Refused(await carve.CallToolAsync("define_material", """{"name":"iron","base_colour_texture":{"graph":"nowhere/iron.texgraph","output":"base_colour"}}"""));
        Assert.Contains($"the folder {Path.Combine(Root, "nowhere")} does not exist", none);
    }

    [Fact]
    public async Task A_file_that_is_not_a_texture_graph_is_refused_with_its_load_error()
    {
        Directory.CreateDirectory(Path.Combine(Root, "graphs"));
        File.WriteAllText(Path.Combine(Root, "graphs", "iron.texgraph"), "not a graph");
        var (carve, _) = await StartAsync();
        using var scope = carve;
        var error = Refused(await carve.CallToolAsync("define_material", Iron));
        Assert.Contains($"base_colour_texture.graph: {Path.Combine(Root, "graphs", "iron.texgraph")} is not a texture graph: line 1, column", error);
    }

    [Fact]
    public async Task An_at_in_a_new_name_is_refused()
    {
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Assert.Contains("name: a material name must not contain '@' (an export names a material's per-mesh copies <material>@<mesh>)",
            Refused(await carve.CallToolAsync("define_material", """{"name":"iron@hoop"}""")));
    }

    [Fact]
    public async Task Undo_takes_back_a_graph_define_and_a_replace_of_it()
    {
        // carve has no redo (undo's description): a define and a replace of it are each one undo step.
        WriteGraph();
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", Iron));
        Assert.True(Body(Ok(await carve.CallToolAsync("define_material", """{"name":"iron"}"""))).GetProperty("replaced").GetBoolean());
        Ok(await carve.CallToolAsync("undo", "{}"));
        var listed = Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("materials").EnumerateArray().Single();
        Assert.Equal(256, Slot(listed, "base_colour_texture").GetProperty("size").GetInt32());
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.False(Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).TryGetProperty("materials", out _));
    }

    [Fact]
    public async Task Save_writes_version_10_and_open_brings_the_slot_back()
    {
        var graph = WriteGraph();
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", Iron));
        Ok(await carve.CallToolAsync("save", """{"path":"iron.carve"}"""));
        var text = File.ReadAllText(Path.Combine(Root, "iron.carve"));
        Assert.StartsWith("{\n  \"carve\": 10,\n", text);
        Assert.Contains("\"base_colour_texture\": {\"graph\": \"graphs/iron.texgraph\", \"output\": \"base_colour\", \"size\": 256}", text);

        Ok(await carve.CallToolAsync("define_material", """{"name":"iron"}"""));
        var opened = Body(Ok(await carve.CallToolAsync("open", """{"path":"iron.carve"}""")));
        Assert.False(opened.TryGetProperty("material_warnings", out _));
        var listed = Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("materials").EnumerateArray().Single();
        Assert.Equal(graph, Slot(listed, "base_colour_texture").GetProperty("graph").GetString());
    }

    [Fact]
    public async Task Open_keeps_a_slot_whose_graph_is_missing_or_broken_and_warns_naming_material_slot_and_path()
    {
        var graph = WriteGraph();
        var (carve, _) = await StartAsync();
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", Iron));
        Ok(await carve.CallToolAsync("save", """{"path":"iron.carve"}"""));

        File.Delete(graph);
        var opened = Body(Ok(await carve.CallToolAsync("open", """{"path":"iron.carve"}""")));
        var warning = opened.GetProperty("material_warnings").EnumerateArray().Single().GetString();
        Assert.Equal($"materials['iron'].base_colour_texture: no file at {graph}; there are no .texgraph files in {Path.GetDirectoryName(graph)}; the slot is kept, and render and export fail on it until the file is there and right, or define_material names another", warning);
        var listed = Body(Ok(await carve.CallToolAsync("get_mesh", "{}"))).GetProperty("materials").EnumerateArray().Single();
        Assert.Equal(graph, Slot(listed, "base_colour_texture").GetProperty("graph").GetString());

        File.WriteAllText(graph, "{");
        var broken = Body(Ok(await carve.CallToolAsync("open", """{"path":"iron.carve"}""")));
        Assert.StartsWith($"materials['iron'].base_colour_texture: the graph file '{graph}' cannot be read: line ", broken.GetProperty("material_warnings").EnumerateArray().Single().GetString());
    }
}
