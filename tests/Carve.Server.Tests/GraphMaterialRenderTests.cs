using System.Text.Json;
using Carve.Core;
using Carve.Graphs;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>
/// T-163 on the real process: a graph material on the raised-hoop barrel (samples/barrel.carve, both hoop bands extruded 0.015
/// and unwrapped through the tools) drawn at 512 equals, under D-6's frame rule, the same model with the bake's PNGs named as
/// ordinary PNG textures, and the PNGs of a neutral graph instead must fail it; render answers with <c>baked</c> once and
/// not again. A fine graph texture on a box face is drawn as its own PNG is (the baked upload, mipmaps and sampler).
/// </summary>
/// <remarks>shortcut: graphs of this test's own until T-162's weathered-iron and weathered-staves land on the milestone; then switch.</remarks>
[Trait("Tier", "regression")]
public sealed class GraphMaterialRenderTests() : FileToolTests("carve-graph-material-render")
{
    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "graph-material-render-tests")).FullName;

    /// <summary>Position as base colour and occlusion from the mesh: both slots read the mesh.</summary>
    private const string Weathered = """{ "version": 2, "size": 512, "seed": 1, "nodes": { "p": { "type": "mesh_position" }, "c": { "type": "output", "inputs": { "in": "p" }, "params": { "role": "base_colour" } }, "a": { "type": "mesh_occlusion" }, "o": { "type": "output", "inputs": { "in": "a" }, "params": { "role": "occlusion" } } } }""";

    /// <summary>The neutral graph, for the control: mid grey and no occlusion, read from nothing.</summary>
    private const string Neutral = """{ "version": 2, "size": 512, "seed": 1, "nodes": { "g": { "type": "colour", "params": { "colour": "#808080" } }, "c": { "type": "output", "inputs": { "in": "g" }, "params": { "role": "base_colour" } }, "w": { "type": "colour", "params": { "colour": "#FFFFFF" } }, "o": { "type": "output", "inputs": { "in": "w" }, "params": { "role": "occlusion" } } } }""";

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

    /// <summary>Bakes <paramref name="graph"/>'s slots for the saved model and writes them beside it, as the PNG material names them.</summary>
    private string BakeToPngs(Model model, string mesh, string graph, string prefix)
    {
        var cancel = TestContext.Current.CancellationToken;
        var path = Path.Combine(Root, graph);
        var reads = GraphSlotBake.Read(path).ReadsMesh ? mesh : null;
        File.WriteAllBytes(Path.Combine(Root, prefix + ".base.png"), GraphSlotBake.Png(new MaterialGraph(path, "base_colour", 512), GraphSlotBake.BaseColour, model, reads, cancel));
        File.WriteAllBytes(Path.Combine(Root, prefix + ".occlusion.png"), GraphSlotBake.Png(new MaterialGraph(path, "occlusion", 512), GraphSlotBake.Occlusion, model, reads, cancel));
        return $$"""{"name":"w","base_colour_texture":"{{prefix}}.base.png","occlusion_texture":"{{prefix}}.occlusion.png"}""";
    }

    [Fact]
    public async Task A_graph_material_on_the_raised_hoop_barrel_draws_as_its_bakes_named_as_pngs()
    {
        File.Copy(Path.Combine(Paths.RepoRoot, "samples", "barrel.carve"), Path.Combine(Root, "barrel.carve"));
        File.WriteAllText(Path.Combine(Root, "weathered.texgraph"), Weathered);
        File.WriteAllText(Path.Combine(Root, "neutral.texgraph"), Neutral);
        var (process, _) = await StartProcessAsync(mesh: null);
        using var carve = process;
        Ok(await carve.CallToolAsync("open", """{"path":"barrel.carve"}"""));
        Ok(await carve.CallToolAsync("extrude", $$"""{"faces":[{{string.Join(",", Enumerable.Range(0, 8).Concat(Enumerable.Range(24, 8)))}}],"distance":0.015}"""));
        Ok(await carve.CallToolAsync("unwrap", "{}"));
        Ok(await carve.CallToolAsync("define_material", """{"name":"w","base_colour_texture":{"graph":"weathered.texgraph","output":"base_colour","size":512},"occlusion_texture":{"graph":"weathered.texgraph","output":"occlusion","size":512}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"w","group":"all"}"""));

        var first = await carve.CallToolAsync("render", """{"view":"front","size":512}""");
        var graph = Frame(first, "barrel-graph");
        var caption = Caption(first);
        TestContext.Current.TestOutputHelper?.WriteLine(caption);
        Assert.Contains("""baked: [{"material":"w","mesh":"mesh","slot":"base_colour_texture","size":512,"ms":""", caption);
        Assert.Contains("""{"material":"w","mesh":"mesh","slot":"occlusion_texture","size":512,"ms":""", caption);
        var again = await carve.CallToolAsync("render", """{"view":"front","size":512}""");
        Assert.DoesNotContain("baked:", Caption(again));

        Ok(await carve.CallToolAsync("save", """{"path":"weathered.carve"}"""));
        var model = CarveFile.Read(File.ReadAllText(Path.Combine(Root, "weathered.carve"))).Model!;

        Ok(await carve.CallToolAsync("define_material", BakeToPngs(model, "mesh", "weathered.texgraph", "weathered")));
        var png = Frame(await carve.CallToolAsync("render", """{"view":"front","size":512}"""), "barrel-png");
        Assert.True(Passes(graph, png, out var measured), $"the graph material against its bakes as PNGs: {measured}");

        Ok(await carve.CallToolAsync("define_material", BakeToPngs(model, "mesh", "neutral.texgraph", "neutral")));
        var control = Frame(await carve.CallToolAsync("render", """{"view":"front","size":512}"""), "barrel-neutral");
        Assert.False(Passes(graph, control, out var controlled), $"the control (the neutral graph's PNGs) must fail: {controlled}");
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
