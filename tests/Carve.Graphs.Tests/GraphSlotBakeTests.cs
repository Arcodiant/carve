using System.Numerics;
using Carve.Core;
using Carve.Core.Baking;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Carve.Graphs.Tests;

/// <summary>A material's graph slot baked (T-163): the slot table, patina's bytes, padding, reads_mesh and the content key.</summary>
[Trait("Tier", "unit")]
public sealed class GraphSlotBakeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("carve-slotbake-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Planks => Path.Combine(MeshNodesTests.Root(), "samples", "graphs", "planks.texgraph");

    private string Write(string name, string nodes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, $$"""{ "version": 2, "size": 256, "seed": 1, "nodes": { {{nodes}} } }""");
        return path;
    }

    private string Curvature(string name = "curv.texgraph") => Write(name,
        """ "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } """);

    private static Model Barrel() => Model.Of("barrel", MeshNodesTests.PlainBarrel()).With("lid", TestMeshesCube());

    private static PolygonMesh TestMeshesCube() => Carve.Core.Tests.TestMeshes.Cube();

    [Theory]
    [InlineData("base_colour", GraphSlotBake.BaseColour)]
    [InlineData("normal", GraphSlotBake.Normal)]
    [InlineData("roughness", GraphSlotBake.MetallicRoughness)]
    [InlineData("occlusion", GraphSlotBake.Occlusion)]
    public void A_graph_with_no_mesh_node_gives_the_png_patina_exports(string output, string slot)
    {
        var png = GraphSlotBake.Png(new MaterialGraph(Planks, output, 256), slot, Model.Empty, null, TestContext.Current.CancellationToken);
        Assert.Equal(File.ReadAllBytes(Path.Combine(MeshNodesTests.Root(), "samples", "graphs", "planks", output + ".png")), png);
        Assert.False(GraphSlotBake.Read(Planks).ReadsMesh);
    }

    [Fact]
    public void The_slot_table_takes_and_refuses_outputs_by_kind()
    {
        var evaluator = new GraphEvaluator(TexGraphFile.ReadFile(Planks).Graph!);
        Assert.Equal(OutputRole.BaseColour, GraphSlotBake.PackingOf(GraphSlotBake.BaseColour, evaluator, "base_colour"));
        Assert.Equal(OutputRole.Occlusion, GraphSlotBake.PackingOf(GraphSlotBake.Emissive, evaluator, "roughness")); // grey as grey RGB
        Assert.Equal(OutputRole.Normal, GraphSlotBake.PackingOf(GraphSlotBake.Normal, evaluator, "normal"));
        Assert.Equal(OutputRole.BaseColour, GraphSlotBake.PackingOf(GraphSlotBake.Normal, evaluator, "base_colour"));
        Assert.Equal(OutputRole.Roughness, GraphSlotBake.PackingOf(GraphSlotBake.MetallicRoughness, evaluator, "roughness"));
        Assert.Equal(OutputRole.Occlusion, GraphSlotBake.PackingOf(GraphSlotBake.Occlusion, evaluator, "occlusion"));
        Assert.Contains("grey output", Assert.ThrowsAny<GraphSlotException>(() => GraphSlotBake.PackingOf(GraphSlotBake.Normal, evaluator, "occlusion")).Message);
        Assert.Contains("'roughness'", Assert.ThrowsAny<GraphSlotException>(() => GraphSlotBake.PackingOf(GraphSlotBake.MetallicRoughness, evaluator, "occlusion")).Message);
        Assert.Contains("colour output", Assert.ThrowsAny<GraphSlotException>(() => GraphSlotBake.PackingOf(GraphSlotBake.Occlusion, evaluator, "base_colour")).Message);
        Assert.Contains("no output 'rust'", Assert.ThrowsAny<GraphSlotException>(() => GraphSlotBake.PackingOf(GraphSlotBake.BaseColour, evaluator, "rust")).Message);

        var grey = GraphSlotBake.Rgba(new MaterialGraph(Planks, "occlusion", 64), GraphSlotBake.BaseColour, Model.Empty, null, TestContext.Current.CancellationToken);
        for (var i = 0; i < grey.Length; i += 4) Assert.True(grey[i] == grey[i + 1] && grey[i] == grey[i + 2] && grey[i + 3] == 255);
    }

    [Fact]
    public void A_file_gone_is_a_slot_exception()
    {
        var gone = Path.Combine(_dir, "gone.texgraph");
        Assert.Contains("is not there", Assert.ThrowsAny<GraphSlotException>(() => GraphSlotBake.Read(gone)).Message);
        Assert.ThrowsAny<GraphSlotException>(() => GraphSlotBake.Png(new MaterialGraph(gone, "occlusion", 64), GraphSlotBake.Occlusion, Model.Empty, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_mesh_reading_graph_is_padded_past_its_islands_and_one_that_is_not_tiles()
    {
        // fbm to the output, with a mesh node beside it: it reads the mesh, so only padding tells the two apart.
        var noise = """ "n": { "type": "fbm", "params": { "basis": "gradient", "cells": [4, 4], "octaves": 3, "seed": 2 } }, "o": { "type": "output", "inputs": { "in": "n" }, "params": { "role": "occlusion" } } """;
        var plain = Write("noise.texgraph", noise);
        var meshy = Write("noise-mesh.texgraph", noise + """, "m": { "type": "mesh_curvature" }, "m_out": { "type": "output", "inputs": { "in": "m" }, "params": { "name": "curv" } } """);
        Assert.True(GraphSlotBake.Read(meshy).ReadsMesh);
        var model = Barrel();
        var tiled = GraphSlotBake.Rgba(new MaterialGraph(plain, "occlusion", 256), GraphSlotBake.Occlusion, model, null, TestContext.Current.CancellationToken);
        var padded = GraphSlotBake.Rgba(new MaterialGraph(meshy, "occlusion", 256), GraphSlotBake.Occlusion, model, "barrel", TestContext.Current.CancellationToken);

        var layout = BakeLayout.Create(model.Find("barrel")!, 256).Layout!;
        var image = layout.NewImage(4);
        for (var i = 0; i < tiled.Length; i++) image.Values[i] = tiled[i];
        var expected = Bake.Pad(image, layout, MeshBakeSource.Padding).Values.ToArray().Select(v => (byte)v).ToArray();
        Assert.Equal(expected, padded);
        Assert.NotEqual(tiled, padded);
    }

    [Fact]
    public void Reads_mesh_sees_a_mesh_node_in_a_nested_file()
    {
        Curvature("inner.texgraph");
        var outer = Write("outer.texgraph",
            """ "g": { "type": "graph", "params": { "path": "inner.texgraph" } }, "o": { "type": "output", "inputs": { "in": "g.occlusion" }, "params": { "role": "occlusion" } } """);
        var read = GraphSlotBake.Read(outer);
        Assert.True(read.ReadsMesh);
        Assert.Equal([Path.Combine(_dir, "inner.texgraph")], read.Files);
    }

    [Fact]
    public void The_key_changes_for_each_edit_a_mesh_reading_slot_depends_on()
    {
        var files = GraphSlotBake.Read(Curvature());
        var model = Barrel();
        var mesh = model.Find("barrel")!;
        string Key(Model m) => GraphSlotBake.Key(files, "occlusion", 256, GraphSlotBake.Occlusion, m, "barrel");
        var at = Key(model);
        Assert.Equal(at, Key(Barrel()));
        Assert.NotEqual(at, GraphSlotBake.Key(files, "occlusion", 512, GraphSlotBake.Occlusion, model, "barrel"));

        var moved = mesh.WithVertices([.. mesh.Vertices.Select((v, i) => i == 0 ? v + new Vector3(0.01f, 0, 0) : v)]);
        var uvs = mesh.Uvs!.WithFace(mesh.Uvs.MappedFaces[0], [.. mesh.Uvs.Of(mesh.Uvs.MappedFaces[0])!.Select(u => u * 0.5f)]);
        var edge = (mesh.Faces[0].Corners[0], mesh.Faces[0].Corners[1]);
        var cube = TestMeshesCube();
        Model[] edits =
        [
            model.With("barrel", moved),
            model.With("barrel", mesh.WithUvs(uvs)),
            model.With("barrel", mesh.WithUvs(mesh.Uvs.WithSeams([edge], !mesh.Uvs.IsSeam(edge.Item1, edge.Item2)))),
            model.With("barrel", mesh.WithStyle((mesh.Style ?? FaceStyle.None).WithGroup("band", [0, 1]))),
            model.WithMaterial(new Material("iron")).With("barrel", mesh.WithStyle((mesh.Style ?? FaceStyle.None).WithMaterial([0], "iron"))),
            model.With("barrel", mesh.WithShade((mesh.Shade ?? MeshShade.Flat).WithMode(ShadingMode.Smooth))),
            model.With("barrel", mesh.WithShade((mesh.Shade ?? MeshShade.Flat).WithEdges([edge], true))),
            model.With("lid", cube.WithVertices([.. cube.Vertices.Select(v => v * 2)])),
        ];
        var keys = edits.Select(Key).ToList();
        Assert.DoesNotContain(at, keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void The_key_of_a_slot_that_does_not_read_the_mesh_ignores_the_mesh_and_follows_every_file()
    {
        var model = Barrel();
        var files = GraphSlotBake.Read(Planks);
        const string slot = GraphSlotBake.BaseColour;
        var at = GraphSlotBake.Key(files, "base_colour", 256, slot, model, null);
        var mesh = model.Find("barrel")!;
        Assert.Equal(at, GraphSlotBake.Key(files, "base_colour", 256, slot, model.With("barrel", mesh.WithVertices([.. mesh.Vertices.Select(v => v * 2)])), "barrel"));
        Assert.Equal(at, GraphSlotBake.Key(files, "base_colour", 256, slot, Model.Empty, null));
        Assert.NotEqual(at, GraphSlotBake.Key(files, "roughness", 256, slot, model, null));

        // One byte of a nested file.
        var inner = Write("inner.texgraph", """ "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } """);
        var outer = Write("outer.texgraph", """ "g": { "type": "graph", "params": { "path": "inner.texgraph" } }, "o": { "type": "output", "inputs": { "in": "g.base_colour" }, "params": { "role": "base_colour" } } """);
        var before = GraphSlotBake.Key(GraphSlotBake.Read(outer), "base_colour", 64, slot, model, null);
        File.WriteAllText(inner, File.ReadAllText(inner).Replace("#808080", "#808081"));
        Assert.NotEqual(before, GraphSlotBake.Key(GraphSlotBake.Read(outer), "base_colour", 64, slot, model, null));
    }

    [Fact]
    public void The_key_tells_apart_slots_that_pack_one_output_differently_and_only_those()
    {
        // RV-82's probe: roughness is grey RGB in base colour and emissive, G with metallic in B in metallic-roughness.
        var files = GraphSlotBake.Read(Planks);
        string Key(string slot) => GraphSlotBake.Key(files, "roughness", 64, slot, Model.Empty, null);
        byte[] Rgba(string slot) => GraphSlotBake.Rgba(new MaterialGraph(Planks, "roughness", 64), slot, Model.Empty, null, TestContext.Current.CancellationToken);
        Assert.NotEqual(Rgba(GraphSlotBake.Emissive), Rgba(GraphSlotBake.MetallicRoughness));
        Assert.NotEqual(Key(GraphSlotBake.Emissive), Key(GraphSlotBake.MetallicRoughness));
        Assert.NotEqual(Key(GraphSlotBake.BaseColour), Key(GraphSlotBake.MetallicRoughness));
        // Slots that pack alike give the same bytes, and share one bake.
        Assert.Equal(Rgba(GraphSlotBake.Emissive), Rgba(GraphSlotBake.BaseColour));
        Assert.Equal(Rgba(GraphSlotBake.Occlusion), Rgba(GraphSlotBake.BaseColour));
        Assert.Equal(Key(GraphSlotBake.BaseColour), Key(GraphSlotBake.Emissive));
        Assert.Equal(Key(GraphSlotBake.BaseColour), Key(GraphSlotBake.Occlusion));
    }

    /// <summary>
    /// The key's claim over the whole slot table: every slot (each texture-slot constant, so one added later is in it) given
    /// every output of the planks, grey and colour, is keyed apart, or refuses it, or gives base colour's bytes.
    /// </summary>
    [Fact]
    public void A_slot_keyed_as_base_colour_packs_every_output_as_base_colour_does_or_refuses_it()
    {
        var files = GraphSlotBake.Read(Planks);
        var slots = typeof(GraphSlotBake).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.Contains(GraphSlotBake.Occlusion, slots);
        var outputs = new GraphEvaluator(TexGraphFile.ReadFile(Planks, GraphSlotBake.Unbound).Graph!, GraphSlotBake.Unbound).Outputs.ToList();
        Assert.Contains("roughness", outputs);
        byte[]? Rgba(string slot, string output)
        {
            try { return GraphSlotBake.Rgba(new MaterialGraph(Planks, output, 32), slot, Model.Empty, null, TestContext.Current.CancellationToken); }
            catch (GraphOutputException) { return null; }
        }
        string Key(string slot, string output) => GraphSlotBake.Key(files, output, 32, slot, Model.Empty, null);
        var shared = 0;
        foreach (var output in outputs)
        {
            var baseColour = Rgba(GraphSlotBake.BaseColour, output);
            Assert.NotNull(baseColour);
            foreach (var slot in slots.Where(s => Key(s, output) == Key(GraphSlotBake.BaseColour, output)))
            {
                if (Rgba(slot, output) is { } bytes) Assert.True(bytes.AsSpan().SequenceEqual(baseColour), $"{slot} packs '{output}' unlike {GraphSlotBake.BaseColour} but shares its key");
                shared++;
            }
        }
        Assert.True(shared > outputs.Count, $"only {shared} slot and output pairs keyed as base colour");
    }

    [Fact]
    public void The_files_hash_follows_the_nesting_not_the_folder()
    {
        var inner = """ "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } """;
        var outer = """ "g": { "type": "graph", "params": { "path": "inner.texgraph" } }, "o": { "type": "output", "inputs": { "in": "g.base_colour" }, "params": { "role": "base_colour" } } """;
        Write("inner.texgraph", inner);
        var here = GraphSlotBake.Read(Write("outer.texgraph", outer)).Hash;
        Directory.CreateDirectory(Path.Combine(_dir, "moved"));
        Write(Path.Combine("moved", "inner.texgraph"), inner);
        Assert.Equal(here, GraphSlotBake.Read(Write(Path.Combine("moved", "outer.texgraph"), outer)).Hash);
    }

    [Fact]
    public void Validate_refuses_a_mesh_parameter_every_bake_would()
    {
        // RV-82's probe: width 0 and a bare mesh_mask passed Validate, then failed the bake.
        var path = Write("bad.texgraph", """ "w": { "type": "mesh_curvature", "params": { "width": 0 } }, "k": { "type": "mesh_mask" }, "o": { "type": "output", "inputs": { "in": "w" }, "params": { "role": "occlusion" } }, "ko": { "type": "output", "inputs": { "in": "k" }, "params": { "name": "mask" } } """);
        var e = Assert.Throws<GraphFaultException>(() => GraphSlotBake.Validate(new MaterialGraph(path, "occlusion", 64), GraphSlotBake.Occlusion));
        Assert.Equal(["node 'k': give exactly one of params 'group' and 'material'", "node 'w' parameter 'width': is 0, but must be above 0 metres"], e.Faults.Select(f => f.Message));
    }

    [Fact]
    public void Unchanged_holds_until_a_file_is_edited()
    {
        var files = GraphSlotBake.Read(Planks);
        Assert.True(GraphSlotBake.Unchanged(files));
        Assert.False(GraphSlotBake.Unchanged(files with { Hash = "0" }));
        Assert.False(GraphSlotBake.Unchanged(files with { Path = Path.Combine(_dir, "gone.texgraph") }));
    }
}
