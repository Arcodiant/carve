using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.SkinnedExportToolTests;

namespace Carve.Server.Tests;

/// <summary><c>export</c> with <c>mode: piece</c> through the real <c>carve</c>: the file, the result line and each refusal writing nothing.</summary>
[Trait("Tier", "unit")]
public sealed class PieceExportToolTests() : TempFolderTests("carve-piece")
{
    [Fact]
    public async Task A_piece_is_written_with_every_bone_and_no_clips_and_the_model_file_is_untouched()
    {
        using var carve = await StartChainAsync(Root);
        Ok(await carve.CallToolAsync("export", """{"path":"chain.glb"}"""));
        var modelBytes = File.ReadAllBytes(Path.Combine(Root, "chain.glb"));

        var result = await carve.CallToolAsync("export", """{"path":"chain.upper.glb","mode":"piece","meshes":["upper"]}""");
        Ok(result);
        Assert.Contains("as a piece", TextOf(result));
        Assert.Contains("1 piece(s) 'upper'", TextOf(result));
        Assert.Contains("0 clip(s)", TextOf(result));
        var model = GlbExportTests.Parser.Parse(modelBytes, Empyrean.ModelFormat.Gltf);
        var piece = GlbExportTests.Parser.Parse(File.ReadAllBytes(Path.Combine(Root, "chain.upper.glb")), Empyrean.ModelFormat.Gltf);
        Assert.Empty(piece.Warnings);
        Assert.Equal(["base", "joint"], piece.Skin!.Skeleton.Bones.Select(b => b.Name));
        Assert.Equal(model.Skin!.Skeleton.Bones, piece.Skin.Skeleton.Bones);
        Assert.Empty(piece.Skin.Animations);
        Assert.Equal(modelBytes, File.ReadAllBytes(Path.Combine(Root, "chain.glb")));
    }

    /// <summary>
    /// RV-82: a piece of a mesh wearing a material (a plain one and a graph one) is built and carries both. Before T-164 gave
    /// <c>PieceExport.Select</c> the model's materials, the piece could not be built and export threw.
    /// </summary>
    [Fact]
    public async Task A_piece_wearing_a_plain_and_a_graph_material_is_written_with_both()
    {
        File.WriteAllText(Path.Combine(Root, "flat.texgraph"), """{ "version": 2, "size": 64, "seed": 1, "nodes": { "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } } }""");
        using var carve = await StartChainAsync(Root);
        Ok(await carve.CallToolAsync("define_material", """{"name":"paint","base_colour":"#FF0000"}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"flat","base_colour_texture":{"graph":"flat.texgraph","output":"base_colour","size":64}}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"paint","faces":[0,1,2],"mesh":"upper"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"material":"flat","faces":[3,4,5],"mesh":"upper"}"""));

        Ok(await carve.CallToolAsync("export", """{"path":"upper.glb","mode":"piece","meshes":["upper"]}"""));
        var root = SharpGLTF.Schema2.ModelRoot.ParseGLB(File.ReadAllBytes(Path.Combine(Root, "upper.glb")));
        Assert.Equal(["flat", "paint"], root.LogicalMaterials.Select(m => m.Name).Order(StringComparer.Ordinal));
        Assert.NotNull(root.LogicalMaterials.Single(m => m.Name == "flat").FindChannel("BaseColor")!.Value.Texture);
    }

    [Theory]
    [InlineData("""{"path":"p.glb","mode":"piece","meshes":[]}""", "meshes is empty")]
    [InlineData("""{"path":"p.glb","mode":"piece"}""", "meshes is empty")]
    [InlineData("""{"path":"p.glb","mode":"piece","meshes":["cape"]}""", "no mesh named 'cape'; the model's meshes are 'lower', 'upper'")]
    [InlineData("""{"path":"p.glb","mode":"piece","meshes":[1]}""", "meshes: expected an array of mesh names")]
    [InlineData("""{"path":"p.glb","meshes":["upper"]}""", "meshes: only mode piece takes meshes")]
    public async Task Refusals_write_nothing(string arguments, string expected)
    {
        using var carve = await StartChainAsync(Root);
        var refused = await carve.CallToolAsync("export", arguments);
        Assert.True(IsError(refused), TextOf(refused));
        Assert.Contains(expected, TextOf(refused));
        Assert.False(File.Exists(Path.Combine(Root, "p.glb")));
    }

    [Theory]
    [InlineData("Copy", "export: mode piece: the model's part 'Copy' is an instance of 'upper', with no geometry of its own; export 'upper', and every instance shows it. Nothing was written.")]
    [InlineData("Empty", "export: mode piece: the model's part 'Empty' is an empty part with no geometry; a piece is one part's own geometry, so name a part that has some. Nothing was written.")]
    public async Task A_piece_of_an_instance_or_an_empty_is_refused_as_save_refuses_it(string name, string expected)
    {
        using var carve = await StartChainAsync(Root);
        Ok(await carve.CallToolAsync("define_part", """{"part":"Copy","instance_of":"upper","translation":[2,0,0]}"""));
        Ok(await carve.CallToolAsync("define_part", """{"part":"Empty"}"""));
        var refused = await carve.CallToolAsync("export", $$"""{"path":"p.glb","mode":"piece","meshes":["lower","{{name}}"]}""");
        Assert.True(IsError(refused), TextOf(refused));
        Assert.Equal(expected, TextOf(refused));
        Assert.False(File.Exists(Path.Combine(Root, "p.glb")));
    }

    [Fact]
    public async Task A_piece_of_a_model_with_no_skeleton_is_refused()
    {
        using var carve = new ToolHarness(workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Cube("lower", 0.2f)));
        var refused = await carve.CallToolAsync("export", """{"path":"p.glb","mode":"piece","meshes":["lower"]}""");
        Assert.True(IsError(refused), TextOf(refused));
        Assert.Contains("no skeleton", TextOf(refused));
        Assert.False(File.Exists(Path.Combine(Root, "p.glb")));
    }

    [Fact]
    public async Task Unweighted_vertices_of_a_chosen_mesh_are_refused_but_those_of_another_are_not()
    {
        using var carve = await StartChainAsync(Root, weighUpper: false);
        var refused = await carve.CallToolAsync("export", """{"path":"p.glb","mode":"piece","meshes":["upper"]}""");
        Assert.True(IsError(refused), TextOf(refused));
        Assert.Contains("mesh 'upper' vertices 3, 6", TextOf(refused));
        Assert.False(File.Exists(Path.Combine(Root, "p.glb")));
        Ok(await carve.CallToolAsync("export", """{"path":"p.glb","mode":"piece","meshes":["lower"]}"""));
        Ok(await carve.CallToolAsync("export", """{"path":"p.glb","mode":"piece","meshes":["upper"],"allow_unweighted":true,"overwrite":true}"""));
    }
}
