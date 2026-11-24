using System.Text.Json;
using Carve.Core;

namespace Carve.Server.Tests;

/// <summary>
/// T-163: the server's cache of graph slot bakes, driven by the tools on the in-process harness and resolved as render
/// resolves before it queues a frame: what is baked first and then found, which edits bake again (a move, an unwrap, a graph
/// file and a nested file rewritten) and which do not (undo to a state already baked), the drawn model's per-mesh copy, the
/// 5 s warning through an injected clock, a slot that cannot be baked, and reads_mesh in get_mesh.
/// </summary>
[Trait("Tier", "unit")]
public class GraphBakesTests : FileToolTests
{
    public GraphBakesTests() : base("graphbakes")
    {
    }

    private string Write(string name, string nodes)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllText(path, $$"""{ "version": 2, "size": 64, "seed": 1, "nodes": { {{nodes}} } }""");
        return path;
    }

    private const string Curvature = """ "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } """;
    private const string Inner = """ "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } """;
    private const string Outer = """ "g": { "type": "graph", "params": { "path": "inner.texgraph" } }, "o": { "type": "output", "inputs": { "in": "g.base_colour" }, "params": { "role": "base_colour" } } """;

    /// <summary>The strip with UVs wearing 'm': a flat nested graph in base colour, a curvature graph (reads the mesh) in occlusion.</summary>
    private async Task<ToolHarness> StartStripAsync()
    {
        Write("inner.texgraph", Inner);
        Write("flat.texgraph", Outer);
        Write("curv.texgraph", Curvature);
        var (carve, _) = await StartAsync(BakeToolTests.Strip);
        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
        Ok(await carve.CallToolAsync("define_material", """{"name":"m","base_colour_texture":{"graph":"flat.texgraph","output":"base_colour","size":64},"occlusion_texture":{"graph":"curv.texgraph","output":"occlusion","size":64}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[0,1,2,3]}"""));
        return carve;
    }

    private static string[] Slots(GraphBakes.Drawn drawn) => [.. drawn.Baked.Select(b => $"{b.Material}/{b.Mesh}/{b.Slot}/{b.Size}")];

    [Fact]
    public async Task A_slot_is_baked_once_and_again_only_after_an_edit_it_depends_on()
    {
        await using var carve = await StartStripAsync();
        var bakes = new GraphBakes();
        var cancel = TestContext.Current.CancellationToken;
        GraphBakes.Drawn Resolve() => bakes.Resolve(carve.Store.Current.Model, cancel);

        // Before any render resolves it, the frame thread draws the graph slots empty and bakes nothing.
        Assert.Null(bakes.DrawnOf(carve.Store.Current.Model).FindMaterial("m")!.BaseColourTexture);
        Assert.Equal(0, bakes.Count);

        var first = Resolve();
        Assert.Equal(["m//base_colour_texture/64", "m/strip/occlusion_texture/64"], Slots(first).Order());
        Assert.StartsWith("""

            baked: [{"material":"m","mesh":null,"slot":"base_colour_texture","size":64,"ms":
            """.ReplaceLineEndings("\n"), GraphBakes.Text(first.Baked));
        Assert.Empty(Resolve().Baked);
        Assert.Equal("", GraphBakes.Text([]));

        // The material reads the mesh, so the strip wears its own copy, each graph slot a PNG of its bake.
        var copy = first.Model.FindMaterial("m@strip")!;
        Assert.All(new[] { copy.BaseColourTexture, copy.OcclusionTexture }, t => Assert.IsType<MaterialTexture>(t));
        Assert.All(Enumerable.Range(0, 4), f => Assert.Equal("m@strip", first.Model.Find("strip")!.Style!.Materials[f]));

        Ok(await carve.CallToolAsync("move", """{"vertices":[0],"by":[0,0,0.5]}"""));
        Assert.Equal(["m/strip/occlusion_texture/64"], Slots(Resolve()));
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Empty(Resolve().Baked);

        Ok(await carve.CallToolAsync("unwrap", "{}"));
        Assert.Equal(["m/strip/occlusion_texture/64"], Slots(Resolve()));

        var curv = Path.Combine(Root, "curv.texgraph");
        File.WriteAllText(curv, File.ReadAllText(curv).Replace("\"seed\": 1", "\"seed\": 2"));
        Assert.Equal(["m/strip/occlusion_texture/64"], Slots(Resolve()));

        Write("inner.texgraph", Inner.Replace("#808080", "#808081"));
        Assert.Equal(["m//base_colour_texture/64"], Slots(Resolve()));
        Assert.Empty(Resolve().Baked);
    }

    [Fact]
    public async Task A_material_over_five_seconds_warns_naming_the_costliest_slot_and_node()
    {
        await using var carve = await StartStripAsync();
        var now = 0.0;
        var bakes = new GraphBakes(() => now += 3000);
        var drawn = bakes.Resolve(carve.Store.Current.Model, TestContext.Current.CancellationToken);
        // Each clock read is 3 s on: the flat slot reads it twice (3 s), the curvature slot four times (9 s, its node 3 s); both count toward the strip's copy.
        Assert.Equal(["material 'm' on 'strip' took 12.0 s to bake (occlusion_texture 64: mesh_curvature 'm' 3.0 s); lower its size or the node's rays"], drawn.Warnings);
        Assert.EndsWith("\nwarning: " + drawn.Warnings[0], GraphBakes.Text(drawn.Baked));
    }

    [Fact]
    public async Task A_slot_that_cannot_be_baked_names_the_material_slot_and_cause()
    {
        await using var carve = await StartStripAsync();
        File.Delete(Path.Combine(Root, "curv.texgraph"));
        var e = Assert.Throws<GraphSlotProblemException>(() => new GraphBakes().Resolve(carve.Store.Current.Model, TestContext.Current.CancellationToken));
        Assert.StartsWith("material 'm', occlusion_texture: ", e.Message);
    }

    [Fact]
    public async Task Get_mesh_reports_whether_each_graph_slot_reads_the_mesh()
    {
        await using var carve = await StartStripAsync();
        var material = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_mesh", "{}"))).RootElement.GetProperty("materials")[0];
        Assert.False(material.GetProperty("base_colour_texture").GetProperty("reads_mesh").GetBoolean());
        Assert.True(material.GetProperty("occlusion_texture").GetProperty("reads_mesh").GetBoolean());
    }
}
