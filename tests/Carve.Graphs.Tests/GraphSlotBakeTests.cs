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
        string Key(Model m) => GraphSlotBake.Key(files, "occlusion", 256, m, "barrel");
        var at = Key(model);
        Assert.Equal(at, Key(Barrel()));
        Assert.NotEqual(at, GraphSlotBake.Key(files, "occlusion", 512, model, "barrel"));

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
        var at = GraphSlotBake.Key(files, "base_colour", 256, model, null);
        var mesh = model.Find("barrel")!;
        Assert.Equal(at, GraphSlotBake.Key(files, "base_colour", 256, model.With("barrel", mesh.WithVertices([.. mesh.Vertices.Select(v => v * 2)])), "barrel"));
        Assert.Equal(at, GraphSlotBake.Key(files, "base_colour", 256, Model.Empty, null));
        Assert.NotEqual(at, GraphSlotBake.Key(files, "roughness", 256, model, null));

        // One byte of a nested file.
        var inner = Write("inner.texgraph", """ "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } """);
        var outer = Write("outer.texgraph", """ "g": { "type": "graph", "params": { "path": "inner.texgraph" } }, "o": { "type": "output", "inputs": { "in": "g.base_colour" }, "params": { "role": "base_colour" } } """);
        var before = GraphSlotBake.Key(GraphSlotBake.Read(outer), "base_colour", 64, model, null);
        File.WriteAllText(inner, File.ReadAllText(inner).Replace("#808080", "#808081"));
        Assert.NotEqual(before, GraphSlotBake.Key(GraphSlotBake.Read(outer), "base_colour", 64, model, null));
    }
}
