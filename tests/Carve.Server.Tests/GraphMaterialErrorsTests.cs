using System.Diagnostics;
using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-166: what is wrong with a graph material, in words an agent with no file tools can act on, from define_material, set_material,
/// open, check and the bake render and export run (here <see cref="GraphBakes.Resolve"/>, which they call). The table of causes
/// and messages is held whole, here and in GraphSlotToolTests.
/// </summary>
[Trait("Tier", "unit")]
public class GraphMaterialErrorsTests : FileToolTests
{
    public GraphMaterialErrorsTests() : base("graphmaterialerrors")
    {
    }

    private string Write(string name, string nodes)
    {
        var path = Path.GetFullPath(Path.Combine(Root, name));
        File.WriteAllText(path, $$"""{ "version": 2, "size": 64, "seed": 1, "nodes": { {{nodes}} } }""");
        return path;
    }

    private const string Good = """ "g": { "type": "gradient" }, "c": { "type": "output", "inputs": { "in": "g" }, "params": { "role": "base_colour" } }, "r": { "type": "output", "inputs": { "in": "g" }, "params": { "role": "roughness" } } """;
    private const string Colour = """ "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } """;
    private const string Grey = """ "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } """;
    private const string Mask = """ "k": { "type": "mesh_mask", "params": { "group": "rim" } }, "o": { "type": "output", "inputs": { "in": "k" }, "params": { "role": "occlusion" } } """;
    private const string TwoFaults = """ "a": { "type": "output", "params": { "role": "base_colour" } }, "b": { "type": "output", "params": { "role": "roughness" } } """;
    private const string Nested = """ "g": { "type": "graph", "params": { "path": "gone.texgraph" } }, "o": { "type": "output", "inputs": { "in": "g.base_colour" }, "params": { "role": "base_colour" } } """;

    private const string OverlappingUvs = """{"faces":[{"face":0,"uv":[[0,0.75],[0.5,0.75],[0.5,0.25],[0,0.25]]},{"face":1,"uv":[[0.25,0.75],[0.75,0.75],[0.75,0.25],[0.25,0.25]]}]}""";

    private static string Slot(string graph, string output) => $$"""{"graph":"{{graph}}","output":"{{output}}"}""";

    private const string HalfUvs = """{"faces":[{"face":0,"uv":[[0,0.75],[0.25,0.75],[0.25,0.25],[0,0.25]]},{"face":1,"uv":[[0.25,0.75],[0.5,0.75],[0.5,0.25],[0.25,0.25]]}]}""";

    private async Task<ToolHarness> StartStripAsync(bool uvs, bool overlap = false, string? only = null)
    {
        var (carve, _) = await StartAsync(BakeToolTests.Strip);
        if (uvs) Ok(await carve.CallToolAsync("set_uvs", only ?? (overlap ? OverlappingUvs : BakeToolTests.StripUvs())));
        return carve;
    }

    private static string[] Lines(string json, string property) =>
        [.. JsonDocument.Parse(json).RootElement.GetProperty(property).EnumerateArray().Select(w => w.GetString()!)];

    [Fact]
    public async Task define_material_refuses_every_cause_in_its_own_words_and_changes_nothing()
    {
        Write("good.texgraph", Good);
        Write("grey.texgraph", Grey);
        Write("colour.texgraph", Colour);
        Write("faulty.texgraph", TwoFaults);
        Write("nested.texgraph", Nested);
        await using var carve = await StartStripAsync(uvs: true);
        var rows = new List<string>();
        foreach (var (slot, graph, output) in new[]
        {
            ("base_colour_texture", "good.texgraph", "wera"),
            ("normal_texture", "grey.texgraph", "occlusion"),
            ("occlusion_texture", "colour.texgraph", "base_colour"),
            ("metallic_roughness_texture", "good.texgraph", "base_colour"),
            ("base_colour_texture", "faulty.texgraph", "base_colour"),
            ("base_colour_texture", "nested.texgraph", "base_colour"),
        })
            rows.Add(Refused(await carve.CallToolAsync("define_material", $$"""{"name":"m","{{slot}}":{{Slot(graph, output)}}}""")).ReplaceLineEndings(" ").Split(": ", 2)[1].Replace(Root, "<root>"));
        Assert.Equal(
        [
            "base_colour_texture.output: the graph has no output 'wera'; its outputs are base_colour, roughness",
            "normal_texture.output: 'occlusion' is a grey output; normal_texture takes the 'normal' output or a colour one",
            "occlusion_texture.output: 'base_colour' is a colour output; occlusion_texture takes a grey one",
            "metallic_roughness_texture.output: metallic_roughness_texture takes the 'roughness' output (with 'metallic' packed in B), not 'base_colour'",
            "base_colour_texture.graph: the graph has faults: node 'a' input 'in': output needs it connected, and it is not; node 'b' input 'in': output needs it connected, and it is not",
            "base_colour_texture.graph: the graph has faults: node 'g' parameter 'path': no file at <root>\\gone.texgraph; the .texgraph files in <root> (5) are: colour.texgraph, faulty.texgraph, good.texgraph, grey.texgraph, nested.texgraph",
        ], rows);
        Assert.Empty(carve.Store.Current.Model.Materials);
    }

    [Fact]
    public async Task A_graph_that_reads_the_mesh_is_said_to_in_define_materials_result()
    {
        Write("grey.texgraph", Grey);
        await using var carve = await StartStripAsync(uvs: true);
        var result = Ok(await carve.CallToolAsync("define_material", $$"""{"name":"m","occlusion_texture":{{Slot("grey.texgraph", "occlusion")}}}"""));
        Assert.Equal(["occlusion_texture: the graph reads the mesh, so it is baked onto each mesh that wears 'm' (its UVs, its groups and materials); set_material it on a mesh with UVs that do not overlap, and check lists what stops it"], Lines(result, "warnings"));
    }

    [Fact]
    public async Task set_material_and_check_warn_of_each_mesh_cause_for_the_mesh_as_it_is()
    {
        Write("grey.texgraph", Grey);
        Write("mask.texgraph", Mask);
        var rows = new List<string>();
        foreach (var (uvs, overlap, graph, only) in new[] { (false, false, "grey", null), (true, true, "grey", null), (true, false, "mask", null), (true, false, "grey", HalfUvs) })
        {
            await using var carve = await StartStripAsync(uvs, overlap, only);
            Ok(await carve.CallToolAsync("define_material", $$"""{"name":"m","occlusion_texture":{{Slot(graph + ".texgraph", "occlusion")}}}"""));
            rows.Add("set_material: " + string.Join(" | ", Lines(Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[0,1,2,3]}""")), "warnings")));
            rows.Add("check: " + string.Join(" | ", Lines(Ok(await carve.CallToolAsync("check", "{}")), "warnings").Where(w => w.StartsWith("materials['m']"))));
        }
        const string NoUvs = "the mesh has no UVs, so it has nothing to bake into; give it UVs first (unwrap, project_uvs or set_uvs)";
        const string Overlap = "the UVs overlap, so a texel would show two faces: faces[0] and faces[1]; move the islands apart (unwrap or set_uvs) and bake again";
        const string Unmapped = "materials['m']: faces[2, 3] of 'strip' are unmapped, so 'm' bakes nothing onto them; give them UVs with project_uvs, unwrap or set_uvs";
        const string NoGroup = "materials['m'].occlusion_texture: nodes['k']: mesh 'strip' has no group 'rim'; its groups are none and its materials 'm'";
        const string BareFaces = "faces[0, 1, 2, 3]: no UVs, so the texture of 'm' is not mapped onto them; give them UVs with project_uvs, unwrap or set_uvs";
        const string BareHalf = "faces[2, 3]: no UVs, so the texture of 'm' is not mapped onto them; give them UVs with project_uvs, unwrap or set_uvs";
        Assert.Equal(
        [
            $"set_material: {BareFaces} | materials['m'].occlusion_texture: mesh 'strip': {NoUvs}",
            $"check: materials['m'].occlusion_texture: mesh 'strip': {NoUvs}",
            $"set_material: {BareHalf} | materials['m'].occlusion_texture: mesh 'strip': {Overlap}",
            $"check: materials['m'].occlusion_texture: mesh 'strip': {Overlap}",
            $"set_material: {NoGroup}",
            $"check: {NoGroup}",
            $"set_material: {BareHalf} | {Unmapped}",
            $"check: {Unmapped}",
        ], rows);
    }

    [Fact]
    public async Task A_graph_broken_or_gone_since_it_was_defined_fails_the_bake_and_is_warned_of_by_check_and_open()
    {
        var path = Write("good.texgraph", Good);
        await using var carve = await StartStripAsync(uvs: true);
        Ok(await carve.CallToolAsync("define_material", $$"""{"name":"m","base_colour_texture":{{Slot("good.texgraph", "base_colour")}}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[0,1,2,3]}"""));
        var bakes = new GraphBakes();
        var cancel = TestContext.Current.CancellationToken;
        bakes.Resolve(carve.Store.Current.Model, cancel);
        Assert.DoesNotContain(Lines(Ok(await carve.CallToolAsync("check", "{}")), "warnings"), w => w.Contains("materials['m']"));

        Write("good.texgraph", TwoFaults);
        var fault = Assert.Throws<GraphSlotProblemException>(() => bakes.Resolve(carve.Store.Current.Model, cancel)).Message;
        File.Delete(path);
        var gone = Assert.Throws<GraphSlotProblemException>(() => bakes.Resolve(carve.Store.Current.Model, cancel)).Message;
        var warned = Lines(Ok(await carve.CallToolAsync("check", "{}")), "warnings").Single(w => w.Contains("materials['m']"));
        const string Unchanged = "; the model is unchanged: define the material again with a graph that bakes, or restore the file";
        const string NoFile = "no file at <root>\\good.texgraph; there are no .texgraph files in <root>";
        Assert.Equal(
        [
            "materials['m'].base_colour_texture: the graph has faults: node 'a' input 'in': output needs it connected, and it is not; node 'b' input 'in': output needs it connected, and it is not" + Unchanged,
            "materials['m'].base_colour_texture: " + NoFile + Unchanged,
            "materials['m'].base_colour_texture: " + NoFile,
        ], new[] { fault, gone, warned }.Select(r => r.Replace(Root, "<root>")));
    }

    [Fact]
    public async Task check_is_silent_on_a_healthy_graph_material_and_fast()
    {
        Write("grey.texgraph", Grey);
        await using var carve = await StartStripAsync(uvs: true);
        Ok(await carve.CallToolAsync("define_material", $$"""{"name":"m","occlusion_texture":{{Slot("grey.texgraph", "occlusion")}}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[0,1,2,3]}"""));
        var watch = Stopwatch.StartNew();
        var warnings = Lines(Ok(await carve.CallToolAsync("check", "{}")), "warnings").Where(w => w.Contains("materials['m']")).ToArray();
        Assert.Empty(warnings);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.Elapsed.ToString());
    }

    [Fact]
    public async Task export_fails_with_the_words_render_uses_when_the_graph_has_gone()
    {
        var path = Write("good.texgraph", Good);
        await using var carve = await StartStripAsync(uvs: true);
        Ok(await carve.CallToolAsync("define_material", $$"""{"name":"m","base_colour_texture":{{Slot("good.texgraph", "base_colour")}}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"m","faces":[0,1,2,3]}"""));
        File.Delete(path);
        var message = Refused(await carve.CallToolAsync("export", """{"path":"out.glb"}""")).Replace(Root, "<root>");
        Assert.Equal(
            "materials['m'].base_colour_texture: no file at <root>\\good.texgraph; there are no .texgraph files in <root>"
            + "; the model is unchanged: define the material again with a graph that bakes, or restore the file", message);
        Assert.False(File.Exists(Path.Combine(Root, "out.glb")));
    }

    [Fact]
    public async Task check_is_silent_on_every_committed_sample_and_fast_on_the_barrel_wearing_a_mesh_graph()
    {
        await using var carve = await StartStripAsync(uvs: false);
        foreach (var sample in Directory.GetFiles(Path.Combine(Paths.RepoRoot, "samples"), "*.carve"))
        {
            Ok(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = sample, drop_targets = true })));
            var result = await carve.CallToolAsync("check", "{}");
            // a model of several meshes names them in its refusal; check each
            var meshes = IsError(result) ? System.Text.RegularExpressions.Regex.Matches(TextOf(result).Split("meshes:")[1], "'([^']+)'").Select(m => (string?)m.Groups[1].Value).ToArray() : [null];
            foreach (var mesh in meshes)
                Assert.DoesNotContain(Lines(Ok(mesh is null ? result : await carve.CallToolAsync("check", JsonSerializer.Serialize(new { mesh }))), "warnings"), w => w.Contains("graph"));
        }

        File.Copy(Path.Combine(Paths.RepoRoot, "samples", "barrel.carve"), Path.Combine(Root, "barrel.carve"));
        File.Copy(Path.Combine(Paths.RepoRoot, "samples", "graphs", "weathered-iron.texgraph"), Path.Combine(Root, "weathered-iron.texgraph"));
        Ok(await carve.CallToolAsync("open", """{"path":"barrel.carve","drop_targets":true}"""));
        Ok(await carve.CallToolAsync("unwrap", "{}"));
        Ok(await carve.CallToolAsync("define_material", $$"""{"name":"iron","base_colour_texture":{{Slot("weathered-iron.texgraph", "base_colour")}}}"""));
        var faces = JsonDocument.Parse(Ok(await carve.CallToolAsync("get_mesh", "{}"))).RootElement.GetProperty("faces").GetArrayLength();
        Ok(await carve.CallToolAsync("set_material", $$"""{"material":"iron","faces":[{{string.Join(",", Enumerable.Range(0, faces))}}]}"""));
        var watch = Stopwatch.StartNew();
        var warnings = Lines(Ok(await carve.CallToolAsync("check", "{}")), "warnings").Where(w => w.Contains("materials['iron']")).ToArray();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.Elapsed.ToString());
        Assert.Empty(warnings);
    }
}
