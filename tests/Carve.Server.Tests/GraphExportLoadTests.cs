using Carve.Core;
using Carve.Graphs;
using Empyrean;
using Microsoft.Extensions.DependencyInjection;

namespace Carve.Server.Tests;

/// <summary>
/// T-164 (UvExportTests' manner): a mesh-reading graph material on two meshes exported through the tools and read back through the
/// engine's IModelGeometryLoader.Load from a content folder as a game would: each copy's base colour is byte-equal to the bake's PNG
/// for its mesh, and the loader has no warning.
/// </summary>
[Trait("Tier", "regression")]
public sealed class GraphExportLoadTests() : FileToolTests("carve-graph-export-load")
{
    private const string Curvature = """{ "version": 2, "size": 64, "seed": 1, "nodes": { "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "base_colour" } } } }""";

    [Fact]
    public async Task Each_copy_of_a_graph_material_loads_as_the_bake_of_its_mesh()
    {
        File.WriteAllText(Path.Combine(Root, "curv.texgraph"), Curvature);
        var (carve, _) = await StartAsync(BakeToolTests.Strip);
        using var scope = carve;
        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
        // A second, bent strip.
        Ok(await carve.CallToolAsync("define_mesh", BakeToolTests.Strip.Replace("\"strip\"", "\"second\"").Replace("[2,1,0]", "[2,2,1]")));
        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs().TrimEnd('}') + ",\"mesh\":\"second\"}"));
        Ok(await carve.CallToolAsync("define_material", """{"name":"worn","base_colour_texture":{"graph":"curv.texgraph","output":"base_colour","size":64}}"""));
        foreach (var mesh in new[] { "strip", "second" })
            Ok(await carve.CallToolAsync("set_material", $$"""{"material":"worn","faces":[0,1,2,3],"mesh":"{{mesh}}"}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"two.carve"}"""));
        Ok(await carve.CallToolAsync("export", """{"path":"models/two.glb"}"""));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmpyreanCore();
        services.AddEmpyreanGeometry();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IContentRegistry>().Mount(ContentSources.Directory(Root));
        var parsed = provider.GetRequiredService<IModelGeometryLoader>().Load(new ContentPath("models/two.glb"));
        Assert.Empty(parsed.Warnings);

        var model = CarveFile.Read(File.ReadAllText(Path.Combine(Root, "two.carve")), Root).Model!;
        var graph = new MaterialGraph(Path.Combine(Root, "curv.texgraph"), "base_colour", 64);
        Assert.Equal(2, parsed.SubmeshMaterials.Length);
        for (var i = 0; i < 2; i++)
        {
            var mesh = new[] { "strip", "second" }[i];
            Assert.Equal(GraphSlotBake.Png(graph, GraphSlotBake.BaseColour, model, mesh, TestContext.Current.CancellationToken), parsed.SubmeshMaterials[i]!.TextureBytes);
        }
    }
}
