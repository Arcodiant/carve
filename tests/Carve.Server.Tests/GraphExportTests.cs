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

    /// <summary>Saves the model, renames in the file's text and opens it again: an '@' name comes only from a file (T-160).</summary>
    private async Task Reopen(ToolHarness carve, params (string From, string To)[] names)
    {
        Ok(await carve.CallToolAsync("save", """{"path":"at.carve"}"""));
        var path = Path.Combine(Root, "at.carve");
        var text = File.ReadAllText(path);
        foreach (var (from, to) in names) text = text.Replace($"\"{from}\"", $"\"{to}\"", StringComparison.Ordinal);
        File.WriteAllText(path, text);
        Ok(await carve.CallToolAsync("open", """{"path":"at.carve"}"""));
    }

    private static string[] Warnings(string json) =>
        [.. System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!)];

    /// <summary>
    /// RV-82, the reviewer's probe: faces 0-2 wear 'worn' (reads the mesh), face 3 a red 'worn@strip' from an older file. Export wrote
    /// one material 'worn' and the red was gone; now export and render refuse, naming all three, the model unchanged, and check warns.
    /// </summary>
    [Fact]
    public async Task A_copy_whose_name_the_model_already_has_is_refused_by_export_and_render_and_warned_of_by_check()
    {
        var carve = await StartAsync(second: false);
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"red","base_colour":"#FF0000"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"worn","faces":[0,1,2]}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"red","faces":[3]}"""));
        await Reopen(carve, ("red", "worn@strip"));
        var model = carve.Store.Current.Model;

        const string Taken = "materials['worn']: its copy for mesh 'strip' would be named 'worn@strip', which is the material 'worn@strip' the model already has, so ";
        const string Rename = "; rename the material 'worn', the mesh 'strip' or the material 'worn@strip'";
        Assert.Equal(Taken + "nothing is replaced and the model is unchanged" + Rename, Refused(await carve.CallToolAsync("export", """{"path":"a.glb"}""")));
        Assert.False(File.Exists(Path.Combine(Root, "a.glb")));
        Assert.Equal(Taken + "nothing is replaced and the model is unchanged" + Rename,
            Assert.Throws<GraphSlotProblemException>(() => new GraphBakes().Resolve(model, TestContext.Current.CancellationToken)).Message);
        Assert.Same(model, carve.Store.Current.Model);
        Assert.Contains(Taken + "render and export refuse it" + Rename, Warnings(Ok(await carve.CallToolAsync("check", "{}"))));
    }

    /// <summary>RV-82: a kept level's copy (<c>worn@strip@0</c>) whose name the model already has is refused as a mesh's copy is.</summary>
    [Fact]
    public async Task A_kept_levels_copy_whose_name_the_model_already_has_is_refused()
    {
        var carve = await StartAsync(second: false);
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"red","base_colour":"#FF0000"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"worn","faces":[0,1,2]}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"red","faces":[3]}"""));
        Ok(await carve.CallToolAsync("keep_level", """{"level":0}"""));
        await Reopen(carve, ("red", "worn@strip@0"));
        var model = carve.Store.Current.Model;

        Assert.Equal(
            "materials['worn']: its copy for part 'strip' level 0 would be named 'worn@strip@0', which is the material 'worn@strip@0' the model already has, "
            + "so nothing is replaced and the model is unchanged; rename the material 'worn', the part 'strip' or the material 'worn@strip@0'",
            Refused(await carve.CallToolAsync("export", """{"path":"l.glb"}""")));
        Assert.False(File.Exists(Path.Combine(Root, "l.glb")));
        Assert.Same(model, carve.Store.Current.Model);
    }

    /// <summary>RV-82: two copies of one name (mesh 'zz@second' wearing 'worn', mesh 'second' wearing 'worn@zz') are refused too, the second naming the first.</summary>
    [Fact]
    public async Task Two_copies_of_one_name_are_refused_naming_both()
    {
        var carve = await StartAsync(second: true);
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"other","occlusion_texture":{"graph":"curv.texgraph","output":"occlusion","size":64}}"""));
        await Wear(carve, "worn", "strip");
        await Wear(carve, "other", "second");
        await Reopen(carve, ("strip", "zz@second"), ("other", "worn@zz"));
        Assert.Equal(
            "materials['worn@zz']: its copy for mesh 'second' would be named 'worn@zz@second', which is the name of the copy of 'worn' for mesh 'zz@second', "
            + "so nothing is replaced and the model is unchanged; rename the material 'worn@zz', the mesh 'second' or the mesh 'zz@second'",
            Refused(await carve.CallToolAsync("export", """{"path":"b.glb"}""")));
        Assert.False(File.Exists(Path.Combine(Root, "b.glb")));
    }

    /// <summary>RV-82: a graph material whose own name has '@' (a hand-edited file) is written under that name, not cut at its first '@'.</summary>
    [Fact]
    public async Task A_graph_material_named_with_an_at_keeps_its_whole_name()
    {
        var carve = await StartAsync(second: false);
        using var scope = carve;
        await Wear(carve, "worn", "strip");
        await Reopen(carve, ("worn", "wo@rn"));
        Ok(await carve.CallToolAsync("export", """{"path":"d.glb"}"""));
        Assert.Equal(["wo@rn"], Names(Read("d.glb")));
    }

    /// <summary>
    /// RV-83, the reviewer's probe: 'worn' reads the mesh but no face wears it, so no copy is made, and a red material named
    /// 'worn@strip' in the file is on faces 0-3. It is the user's, so it is written under its own name and 'worn' replaces nothing.
    /// </summary>
    [Fact]
    public async Task A_users_at_material_is_not_taken_for_a_copy_of_a_mesh_reading_material_no_mesh_wears()
    {
        var carve = await StartAsync(second: false);
        using var scope = carve;
        Ok(await carve.CallToolAsync("define_material", """{"name":"red","base_colour":"#FF0000"}"""));
        await Wear(carve, "red", "strip");
        await Reopen(carve, ("red", "worn@strip"));

        Ok(await carve.CallToolAsync("export", """{"path":"u.glb"}"""));

        Assert.Equal(["worn@strip"], Names(Read("u.glb")));
    }

    /// <summary>
    /// RV-83, the reviewer's probe: base colour and occlusion share a key, so once 'x' turns from grey to colour, base colour's bake
    /// of it was served to occlusion from the cache. Occlusion refuses a colour output before the cache is asked, by export and render.
    /// </summary>
    [Fact]
    public async Task A_slot_that_refuses_its_output_refuses_it_from_the_cache_too()
    {
        var carve = await StartAsync(second: false);
        using var scope = carve;
        Write("g.texgraph", """ "m": { "type": "mesh_curvature" }, "x": { "type": "output", "inputs": { "in": "m" }, "params": { "name": "x" } } """);
        Ok(await carve.CallToolAsync("define_material", """{"name":"m","base_colour_texture":{"graph":"g.texgraph","output":"x","size":64},"occlusion_texture":{"graph":"g.texgraph","output":"x","size":64}}"""));
        await Wear(carve, "m", "strip");
        Ok(await carve.CallToolAsync("export", """{"path":"a.glb"}"""));
        Write("g.texgraph", """ "c": { "type": "colour", "params": { "colour": "#808080" } }, "x": { "type": "output", "inputs": { "in": "c" }, "params": { "name": "x" } } """);

        const string Takes = "'x' is a colour output; occlusion_texture takes a grey one";
        Assert.Contains(Takes, Refused(await carve.CallToolAsync("export", """{"path":"b.glb"}""")));
        Assert.False(File.Exists(Path.Combine(Root, "b.glb")));
        // Base colour's bake of the colour 'x' is cached now: a material with occlusion alone on it still refuses, render's path too.
        var x = new Carve.Core.MaterialGraph(Path.Combine(Root, "g.texgraph"), "x", 64);
        var bakes = new GraphBakes();
        bakes.Resolve(Carve.Core.Model.Empty.WithMaterial(new Carve.Core.Material("bc", BaseColourTexture: x)), TestContext.Current.CancellationToken);
        var solo = Carve.Core.Model.Empty.WithMaterial(new Carve.Core.Material("solo", OcclusionTexture: x));
        Assert.Contains(Takes, Assert.Throws<GraphSlotProblemException>(() => bakes.Resolve(solo, TestContext.Current.CancellationToken)).Message);
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

/// <summary>RV-82, RV-83: export's cancellation. A whole 2048 bake, so it is a regression test (RV-83: it took 6 s in a unit class).</summary>
[Trait("Tier", "regression")]
public sealed class GraphExportCancelTests() : FileToolTests("carve-graph-export-cancel")
{
    /// <summary>
    /// RV-82: an export named in notifications/cancelled (sent by hand: the SDK's client sends none) stops its bake and writes no .glb.
    /// A second export of the same model, started after, bakes the same and answers; uncancelled, the first (half a second ahead) has written by then.
    /// </summary>
    [Fact]
    public async Task An_export_named_in_notifications_cancelled_writes_no_glb()
    {
        File.WriteAllText(Path.Combine(Root, "curv.texgraph"),
            """{ "version": 2, "size": 64, "seed": 1, "nodes": { "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } } }""");
        var (carve, _) = await StartAsync(BakeToolTests.Strip);
        using var scope = carve;
        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
        Ok(await carve.CallToolAsync("define_material", """{"name":"big","occlusion_texture":{"graph":"curv.texgraph","output":"occlusion","size":2048}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"big","faces":[0,1,2,3],"mesh":"strip"}"""));

        using var end = new CancellationTokenSource();
        var export = carve.CallToolWithIdAsync("big-export", "export", """{"path":"cancelled.glb"}""", end.Token);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await carve.CancelAsync("big-export");
        Ok(await carve.CallToolAsync("export", """{"path":"after.glb"}"""));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(Path.Combine(Root, "cancelled.glb")), "the cancelled export wrote its .glb");
        // A cancelled request gets no answer; the client stops waiting for one here.
        end.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
    }
}
