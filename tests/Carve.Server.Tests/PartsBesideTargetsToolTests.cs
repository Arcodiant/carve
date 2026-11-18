using System.Text;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>
/// T-132's <c>save</c> and <c>open</c> of a part tree beside morph targets, in-process at <c>unit</c> tier: the whole model as
/// version 9 where T-92 refused it, and a piece of a part with targets (<see cref="PartTreeSample.TargetedTree"/>).
/// </summary>
[Trait("Tier", "unit")]
public sealed class PartsBesideTargetsToolTests() : FileToolTests("carve-parts-targets")
{
    private async Task<ToolHarness> StartWithTargetedTreeAsync()
    {
        var (carve, _) = await StartAsync(null);
        carve.Store.OpenModel(PartTreeSample.TargetedTree(), "test");
        return carve;
    }

    private string FileText(string name) => File.ReadAllText(Path.Combine(Root, name), Encoding.UTF8);

    [Fact]
    public async Task Save_and_open_keep_a_part_tree_with_targets_byte_for_byte()
    {
        using var carve = await StartWithTargetedTreeAsync();
        Assert.StartsWith("saved tree.carve to ", Ok(await carve.CallToolAsync("save", """{"path":"tree.carve"}""")));
        var text = FileText("tree.carve");
        Assert.Equal(CarveFile.Write(PartTreeSample.TargetedTree()), text);
        Assert.StartsWith("{\n  \"carve\": 9,\n", text);

        carve.Store.OpenModel(Model.Of("other", TestMeshes.Cube()), "test");
        Ok(await carve.CallToolAsync("open", """{"path":"tree.carve"}"""));
        Assert.Equal(text, CarveFile.Write(carve.Store.Current.Model));
        Ok(await carve.CallToolAsync("save", """{"path":"again.carve"}"""));
        Assert.Equal(text, FileText("again.carve"));
    }

    [Fact]
    public async Task Save_mesh_and_open_mesh_carry_a_parts_targets_and_levels()
    {
        using var carve = await StartWithTargetedTreeAsync();
        Ok(await carve.CallToolAsync("save", """{"path":"body.carve","mesh":"Body"}"""));
        var text = FileText("body.carve");
        Assert.Equal(CarveFile.Write(PartTreeSample.TargetedTree().PartPiece("Body", out _)!), text);
        Assert.Equal(9, CarveFile.Read(text).Version);

        // Into a model whose Body has no targets: its targets and levels come in, its place stays.
        carve.Store.OpenModel(PartTreeSample.Tree(), "test");
        Ok(await carve.CallToolAsync("open", """{"path":"body.carve","mesh":"Body"}"""));
        var model = carve.Store.Current.Model;
        Assert.Equal(["Bulge"], model.Find("Body")!.Targets!.Names);
        Assert.Equal(["Bulge"], model.FindPart("Body")!.Levels[1].Mesh.Targets!.Names);
        Assert.Equal(PartTreeSample.Tree().FindPart("Body")!.Envelope, model.FindPart("Body")!.Envelope);
    }
}
