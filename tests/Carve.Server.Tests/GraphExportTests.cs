using SharpGLTF.Schema2;

namespace Carve.Server.Tests;

/// <summary>
/// T-164: export bakes each graph slot and writes it as an ordinary PNG: the three naming cases (a material that does not read the mesh,
/// one a single mesh wears, one several wear) read back from the written .glb, images shared where the bytes are equal, nothing baked
/// again after a render's or an earlier export's bake, and equal bytes on two runs (the file's name is in it, so the second run is in another folder under the same name).
/// </summary>
[Trait("Tier", "unit")]
public sealed class GraphExportTests() : FileToolTests("carve-graph-export")
{
    private const string Flat = """ "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } """;
    private const string Curvature = """ "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } """;

    private void Write(string name, string nodes) =>
        File.WriteAllText(Path.Combine(Root, name), $$"""{ "version": 2, "size": 64, "seed": 1, "nodes": { {{nodes}} } }""");

    /// <summary>The strip with UVs wearing 'worn' (flat base colour, curvature occlusion: reads the mesh) and 'plain' (flat only), plus a second mesh when asked.</summary>
    private async Task<ToolHarness> StartAsync(bool second, string wornOn = "strip")
    {
        Write("flat.texgraph", Flat);
        Write("curv.texgraph", Curvature);
        var (carve, _) = await StartAsync(BakeToolTests.Strip);
        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
        if (second)
        {
            Ok(await carve.CallToolAsync("define_mesh", BakeToolTests.Strip.Replace("\"strip\"", "\"second\"")));
            Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs().TrimEnd('}') + ",\"mesh\":\"second\"}"));
        }
        Ok(await carve.CallToolAsync("define_material", """{"name":"worn","base_colour_texture":{"graph":"flat.texgraph","output":"base_colour","size":64},"occlusion_texture":{"graph":"curv.texgraph","output":"occlusion","size":64}}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"plain","base_colour_texture":{"graph":"flat.texgraph","output":"base_colour","size":64}}"""));
        return carve;
    }

    private async Task Wear(ToolHarness carve, string material, string mesh) =>
        Ok(await carve.CallToolAsync("set_material", $$"""{"material":"{{material}}","faces":[0,1,2,3],"mesh":"{{mesh}}"}"""));

    private ModelRoot Read(string file) => ModelRoot.ParseGLB(File.ReadAllBytes(Path.Combine(Root, file)));

    private static string[] Names(ModelRoot root) => [.. root.LogicalMaterials.Select(m => m.Name)];

    [Fact]
    public async Task A_material_one_mesh_wears_is_written_under_its_own_name_and_the_bake_is_not_repeated()
    {
        var carve = await StartAsync(second: true);
        using var scope = carve;
        await Wear(carve, "worn", "strip");
        await Wear(carve, "plain", "second");

        var line = Ok(await carve.CallToolAsync("export", """{"path":"a.glb"}"""));
        Assert.Equal(["plain", "worn"], Names(Read("a.glb")).Order().ToArray());
        Assert.Contains("baked: [", line);
        Assert.Contains("\"material\":\"worn\",\"mesh\":\"strip\",\"slot\":\"occlusion_texture\"", line);
        Assert.DoesNotContain("@", line);

        // A second export finds every bake cached, and writes the same bytes.
        var again = Ok(await carve.CallToolAsync("export", """{"path":"again/a.glb"}"""));
        Assert.DoesNotContain("baked:", again);
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root, "a.glb")), File.ReadAllBytes(Path.Combine(Root, "again", "a.glb")));
    }

    [Fact]
    public async Task A_mesh_reading_material_several_meshes_wear_is_written_once_per_mesh_and_named_in_a_warning()
    {
        var carve = await StartAsync(second: true);
        using var scope = carve;
        await Wear(carve, "worn", "strip");
        await Wear(carve, "worn", "second");

        var line = Ok(await carve.CallToolAsync("export", """{"path":"c.glb"}"""));
        var root = Read("c.glb");
        Assert.Equal(["worn@second", "worn@strip"], Names(root).Order(StringComparer.Ordinal).ToArray());
        Assert.Contains("material 'worn' reads the mesh and is used by several meshes, so it is written once per mesh as 'worn@strip', 'worn@second'", line);
        // The copies' flat base colour is the same bytes, so one image serves both.
        Assert.Same(root.LogicalMaterials[0].FindChannel("BaseColor")!.Value.Texture!.PrimaryImage, root.LogicalMaterials[1].FindChannel("BaseColor")!.Value.Texture!.PrimaryImage);
        Assert.Equal(root.LogicalImages.Select(i => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(i.Content.Content.Span))).Distinct().Count(), root.LogicalImages.Count); // no two images hold equal bytes
        Ok(await carve.CallToolAsync("export", """{"path":"again/c.glb"}"""));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root, "c.glb")), File.ReadAllBytes(Path.Combine(Root, "again", "c.glb")));
    }

    [Fact]
    public async Task A_material_that_does_not_read_the_mesh_is_one_material_whatever_wears_it()
    {
        var carve = await StartAsync(second: true);
        using var scope = carve;
        await Wear(carve, "plain", "strip");
        await Wear(carve, "plain", "second");

        var line = Ok(await carve.CallToolAsync("export", """{"path":"e.glb"}"""));
        Assert.Equal(["plain"], Names(Read("e.glb")));
        Assert.DoesNotContain("reads the mesh", line);
        Assert.Contains("\"mesh\":null", line);
    }
}
