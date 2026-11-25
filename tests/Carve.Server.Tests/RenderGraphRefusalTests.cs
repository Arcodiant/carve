namespace Carve.Server.Tests;

/// <summary>
/// RV-82, through a real render (the harness cannot drive render's frame): a bake that fails on the mesh, and a per-mesh copy whose
/// name the model already has, are render's tool errors in the words export gives, before anything is drawn.
/// </summary>
[Trait("Tier", "regression")]
public sealed class RenderGraphRefusalTests() : FileToolTests("carve-render-graph-refusal")
{
    [Fact]
    public async Task render_refuses_a_bake_that_fails_on_the_mesh_and_a_copy_whose_name_is_taken_in_exports_words()
    {
        File.WriteAllText(Path.Combine(Root, "curv.texgraph"),
            """{ "version": 2, "size": 64, "seed": 1, "nodes": { "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } } }""");
        var (carve, _) = await StartProcessAsync(BakeToolTests.Strip);
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"worn","occlusion_texture":{"graph":"curv.texgraph","output":"occlusion","size":64}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"worn","faces":[0,1,2]}"""));
        Assert.Equal(
            "materials['worn'].occlusion_texture: mesh 'strip': the mesh has no UVs, so it has nothing to bake into; give it UVs first (unwrap, project_uvs or set_uvs); the model is unchanged",
            Refused(await carve.CallToolAsync("render", """{"size":256}""")));

        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
        Ok(await carve.CallToolAsync("define_material", """{"name":"red","base_colour":"#FF0000"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"red","faces":[3]}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"at.carve"}"""));
        var path = Path.Combine(Root, "at.carve");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"red\"", "\"worn@strip\"", StringComparison.Ordinal));
        Ok(await carve.CallToolAsync("open", """{"path":"at.carve"}"""));
        Assert.Equal(
            "materials['worn']: its copy for mesh 'strip' would be named 'worn@strip', which is the material 'worn@strip' the model already has, "
            + "so nothing is replaced and the model is unchanged; rename the material 'worn', the mesh 'strip' or the material 'worn@strip'",
            Refused(await carve.CallToolAsync("render", """{"size":256}""")));
    }
}
