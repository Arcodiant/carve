using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.SkinnedExportToolTests;

namespace Carve.Server.Tests;

/// <summary><c>export</c> with <c>mode: piece</c> through the real <c>carve</c>: the file, the result line and each refusal writing nothing.</summary>
[Trait("Tier", "regression")]
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

    [Fact]
    public async Task A_piece_of_a_model_with_no_skeleton_is_refused()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
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
