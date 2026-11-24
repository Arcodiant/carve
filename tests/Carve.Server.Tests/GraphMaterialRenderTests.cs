using System.Text.Json;
using Carve.Core;
using Carve.Graphs;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>
/// T-163 on the real process: T-162's weathered-iron on the hoops and weathered-staves on the staves of the raised-hoop barrel
/// (samples/barrel.carve, both hoop bands extruded 0.015 and unwrapped through the tools) drawn at 512 equal, under D-6's frame
/// rule, the same model with the bakes' PNGs named as ordinary PNG textures, and the PNGs of the neutral graphs (the mesh nodes
/// unbound) instead must fail it; render answers with <c>baked</c> once and not again. A fine graph texture on a box face is
/// drawn as its own PNG is (the baked upload, mipmaps and sampler).
/// </summary>
[Trait("Tier", "regression")]
public sealed class GraphMaterialRenderTests() : FileToolTests("carve-graph-material-render")
{
    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "graph-material-render-tests")).FullName;

    /// <summary>Each material's slots: the graph, and per slot field the output it takes (T-162's As built).</summary>
    private static readonly (string Material, string Graph, (string Field, string Output)[] Slots)[] Weathered =
    [
        ("iron", "weathered-iron.texgraph", [(GraphSlotBake.BaseColour, "base_colour"), (GraphSlotBake.MetallicRoughness, "roughness")]),
        ("staves", "weathered-staves.texgraph", [(GraphSlotBake.BaseColour, "base_colour"), (GraphSlotBake.MetallicRoughness, "roughness"), (GraphSlotBake.Normal, "normal"), (GraphSlotBake.Occlusion, "occlusion")]),
    ];

    private static ImageResult Frame(JsonElement result, string keepAs)
    {
        Assert.False(IsError(result), IsError(result) ? TextOf(result) : "");
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        return ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
    }

    private static string Caption(JsonElement result) => result.GetProperty("content")[1].GetProperty("text").GetString()!;

    /// <summary>D-6's frame rule: fails past 0.5 % of the pixels differing or an 8-connected patch past 50.</summary>
    private static bool Passes(ImageResult a, ImageResult b, out string measured)
    {
        var (differing, patch) = RenderGlbTargetsTests.Measure(a, b);
        measured = $"{differing} px differ (limit {a.Width * a.Height * 0.005}), largest patch {patch} (limit 50)";
        return differing <= a.Width * a.Height * 0.005 && patch <= 50;
    }

    private static string Graphs(string material, string graph, (string Field, string Output)[] slots, int size = 512) =>
        $$"""{"name":"{{material}}",{{string.Join(",", slots.Select(s => $$"""
            "{{s.Field}}":{"graph":"{{graph}}","output":"{{s.Output}}","size":{{size}}}
            """))}}}""";

    /// <summary>Bakes each slot for the saved model (on <paramref name="mesh"/>, or neutral when null) and names the PNGs as a PNG material does.</summary>
    private string Pngs(Model model, string? mesh, string material, string graph, (string Field, string Output)[] slots, string prefix)
    {
        var path = Path.Combine(Root, graph);
        foreach (var (field, output) in slots)
            File.WriteAllBytes(Path.Combine(Root, $"{prefix}.{material}.{field}.png"), GraphSlotBake.Png(new MaterialGraph(path, output, 512), field, model, mesh, TestContext.Current.CancellationToken));
        return $$"""{"name":"{{material}}",{{string.Join(",", slots.Select(s => $$"""
            "{{s.Field}}":"{{prefix}}.{{material}}.{{s.Field}}.png"
            """))}}}""";
    }

    /// <summary>The raised-hoop barrel built through the tools, iron on the hoops and staves on the rest, each slot at <paramref name="size"/>.</summary>
    private async Task BarrelAsync(CarveProcess carve, int size)
    {
        File.Copy(Path.Combine(Paths.RepoRoot, "samples", "barrel.carve"), Path.Combine(Root, "barrel.carve"));
        foreach (var graph in new[] { "weathered-iron", "weathered-staves", "planks" })
            File.Copy(Path.Combine(Paths.RepoRoot, "samples", "graphs", graph + ".texgraph"), Path.Combine(Root, graph + ".texgraph"));
        Ok(await carve.CallToolAsync("open", """{"path":"barrel.carve"}"""));
        int[] bands = [.. Enumerable.Range(0, 8), .. Enumerable.Range(24, 8)];
        Ok(await carve.CallToolAsync("extrude", $$"""{"faces":[{{string.Join(",", bands)}}],"distance":0.015}"""));
        Ok(await carve.CallToolAsync("unwrap", "{}"));
        var faces = JsonDocument.Parse(TextOf(await carve.CallToolAsync("get_mesh", "{}"))).RootElement.GetProperty("faces").GetArrayLength();
        int[] hoops = [.. bands, .. Enumerable.Range(34, faces - 34)];
        int[] staves = [.. Enumerable.Range(0, faces).Except(hoops)];
        foreach (var (material, graph, slots) in Weathered) Ok(await carve.CallToolAsync("define_material", Graphs(material, graph, slots, size)));
        Ok(await carve.CallToolAsync("set_material", $$"""{"material":"iron","faces":[{{string.Join(",", hoops)}}]}"""));
        Ok(await carve.CallToolAsync("set_material", $$"""{"material":"staves","faces":[{{string.Join(",", staves)}}]}"""));
    }

    /// <summary>The bake times of T-162's graphs at 1024 (planning's warning size), kept beside the frames for the record; the 512 ones are the test below's.</summary>
    [Fact]
    public async Task Weathered_graphs_bake_on_the_barrel_at_1024()
    {
        var (process, _) = await StartProcessAsync(mesh: null);
        using var carve = process;
        await BarrelAsync(carve, 1024);
        var caption = Caption(await carve.CallToolAsync("render", """{"view":"front","size":256}"""));
        File.WriteAllText(Path.Combine(Frames, "barrel-1024-baked.txt"), caption);
        TestContext.Current.TestOutputHelper?.WriteLine(caption); // the bake times in the test log, which a runner returns
        Assert.True(caption.Split("\"size\":1024").Length - 1 == 6, caption);
    }

    [Fact]
    public async Task Weathered_graphs_on_the_raised_hoop_barrel_draw_as_their_bakes_named_as_pngs()
    {
        var (process, _) = await StartProcessAsync(mesh: null);
        using var carve = process;
        await BarrelAsync(carve, 512);

        var first = await carve.CallToolAsync("render", """{"view":"front","size":512}""");
        var drawn = Frame(first, "barrel-graph");
        var caption = Caption(first);
        File.WriteAllText(Path.Combine(Frames, "barrel-512-baked.txt"), caption);
        TestContext.Current.TestOutputHelper?.WriteLine(caption); // the bake times in the test log, which a runner returns
        foreach (var (material, _, slots) in Weathered)
            foreach (var (field, _) in slots)
                Assert.Contains($$"""{"material":"{{material}}","mesh":"mesh","slot":"{{field}}","size":512,"ms":""", caption);
        Assert.DoesNotContain("baked:", Caption(await carve.CallToolAsync("render", """{"view":"front","size":512}""")));

        Ok(await carve.CallToolAsync("save", """{"path":"weathered.carve"}"""));
        var model = CarveFile.Read(File.ReadAllText(Path.Combine(Root, "weathered.carve")), Root).Model!;

        foreach (var (material, graph, slots) in Weathered) Ok(await carve.CallToolAsync("define_material", Pngs(model, "mesh", material, graph, slots, "baked")));
        var png = Frame(await carve.CallToolAsync("render", """{"view":"front","size":512}"""), "barrel-png");
        Assert.True(Passes(drawn, png, out var measured), $"the graph materials against their bakes as PNGs: {measured}; {caption}");

        foreach (var (material, graph, slots) in Weathered) Ok(await carve.CallToolAsync("define_material", Pngs(model, null, material, graph, slots, "neutral")));
        var control = Frame(await carve.CallToolAsync("render", """{"view":"front","size":512}"""), "barrel-neutral");
        Assert.False(Passes(drawn, control, out var controlled), $"the control (the neutral graphs' PNGs) must fail: {controlled}");
        File.AppendAllText(Path.Combine(Frames, "barrel-512-baked.txt"), $"\nagainst PNGs: {measured}\ncontrol: {controlled}\n");
    }

    /// <summary>T-164: the barrel exported at 512 and drawn by render_glb agrees with render, front and three-quarter; the file exported with the graphs' wear and grime at 0 does not.</summary>
    [Fact]
    public async Task The_exported_barrel_is_drawn_by_render_glb_as_render_draws_it()
    {
        var (process, _) = await StartProcessAsync(mesh: null);
        using var carve = process;
        await BarrelAsync(carve, 512);
        string[] views = ["""{"view":"front","size":512}""", """{"direction":[1,0.6,1],"size":512}"""];
        var drawn = views.Select((v, i) => Frame(carve.CallToolAsync("render", v).GetAwaiter().GetResult(), $"barrel-export-render-{i}")).ToArray();

        var exported = TextOf(await carve.CallToolAsync("export", """{"path":"barrel.glb"}"""));
        TestContext.Current.TestOutputHelper?.WriteLine(exported);
        Assert.DoesNotContain("baked:", exported); // the renders above baked every slot already
        Assert.DoesNotContain("@", exported); // one mesh, so no copies
        for (var i = 0; i < views.Length; i++)
        {
            var glb = Frame(await carve.CallToolAsync("render_glb", views[i].Replace("{", "{\"path\":\"barrel.glb\",")), $"barrel-export-glb-{i}");
            Assert.True(Passes(drawn[i], glb, out var measured), $"render_glb against render, view {i}: {measured}");
        }

        // The control: the graphs copied with wear_amount and grime_amount at 0, the same model exported through them.
        foreach (var (material, graph, slots) in Weathered)
        {
            var text = File.ReadAllText(Path.Combine(Root, graph)).Replace("\"wear_amount\": { \"value\": 1", "\"wear_amount\": { \"value\": 0").Replace("\"grime_amount\": { \"value\": 1", "\"grime_amount\": { \"value\": 0");
            Assert.NotEqual(File.ReadAllText(Path.Combine(Root, graph)), text);
            File.WriteAllText(Path.Combine(Root, "neutral-" + graph), text);
            Ok(await carve.CallToolAsync("define_material", Graphs(material, "neutral-" + graph, slots)));
        }
        Ok(await carve.CallToolAsync("export", """{"path":"control.glb"}"""));
        var control = Frame(await carve.CallToolAsync("render_glb", """{"path":"control.glb","view":"front","size":512}"""), "barrel-export-control");
        Assert.False(Passes(drawn[0], control, out var controlled), $"the control (wear and grime at 0) must fail: {controlled}");
    }

    [Fact]
    public async Task A_fine_graph_texture_is_drawn_as_its_bake_named_as_a_png_is()
    {
        // Fine fbm at 1024 on one face, so the bake's upload, mip chain and sampler are what the frame shows.
        File.WriteAllText(Path.Combine(Root, "fine.texgraph"), """{ "version": 2, "size": 1024, "seed": 3, "nodes": { "n": { "type": "fbm", "params": { "basis": "gradient", "cells": [96, 96], "octaves": 3, "seed": 2 } }, "c": { "type": "output", "inputs": { "in": "n" }, "params": { "role": "occlusion" } } } }""");
        var (process, _) = await StartProcessAsync(mesh: null);
        using var carve = process;
        Ok(await carve.CallToolAsync("define_mesh", MaterialRenderTests.Box("mesh", 0)));
        Ok(await carve.CallToolAsync("set_uvs", MaterialRenderTests.FrontUvs("mesh")));
        Ok(await carve.CallToolAsync("define_material", """{"name":"fine","base_colour_texture":{"graph":"fine.texgraph","output":"occlusion","size":1024}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[1],"material":"fine"}"""));
        var first = await carve.CallToolAsync("render", """{"view":"+z"}""");
        var graph = Frame(first, "fine-graph");
        Assert.Contains("""baked: [{"material":"fine","mesh":null,"slot":"base_colour_texture","size":1024,"ms":""", Caption(first));

        var bytes = GraphSlotBake.Png(new MaterialGraph(Path.Combine(Root, "fine.texgraph"), "occlusion", 1024), GraphSlotBake.BaseColour, Model.Empty, null, TestContext.Current.CancellationToken);
        File.WriteAllBytes(Path.Combine(Root, "fine.png"), bytes);
        Ok(await carve.CallToolAsync("define_material", """{"name":"fine","base_colour_texture":"fine.png"}"""));
        var png = Frame(await carve.CallToolAsync("render", """{"view":"+z"}"""), "fine-png");
        Assert.True(Passes(graph, png, out var measured), $"a fine graph texture against its bake as a PNG: {measured}");
    }
}
