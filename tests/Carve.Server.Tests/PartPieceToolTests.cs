using System.Text;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>
/// T-92's <c>save</c> and <c>open</c> on a part tree, in-process at <c>unit</c> tier: the whole tree as version 7, and a piece
/// (<c>mesh=</c>) as one part's own geometry and kept levels; an instance or an empty refused, a placed part's placement kept.
/// The tree is <see cref="PartTreeSample.Tree"/> (Body with levels 0 and 1 and an envelope; Arm placed under it; Empty; Copy, an instance of Arm).
/// </summary>
[Trait("Tier", "unit")]
public sealed class PartPieceToolTests() : FileToolTests("carve-parts")
{
    private async Task<ToolHarness> StartWithTreeAsync()
    {
        var (carve, _) = await StartAsync(null);
        carve.Store.OpenModel(PartTreeSample.Tree(), "test");
        return carve;
    }

    private string FileText(string name) => File.ReadAllText(Path.Combine(Root, name), Encoding.UTF8);

    [Fact]
    public async Task Save_and_open_keep_the_whole_tree_byte_for_byte()
    {
        using var carve = await StartWithTreeAsync();
        var line = Ok(await carve.CallToolAsync("save", """{"path":"tree.carve"}"""));
        Assert.StartsWith("saved tree.carve to ", line);
        var text = FileText("tree.carve");
        Assert.Equal(CarveFile.Write(PartTreeSample.Tree()), text);
        Assert.StartsWith("{\n  \"carve\": 7,\n", text);

        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Ok(await carve.CallToolAsync("open", """{"path":"tree.carve"}"""));
        Assert.Equal(text, CarveFile.Write(carve.Store.Current.Model));
        Ok(await carve.CallToolAsync("save", """{"path":"again.carve"}"""));
        Assert.Equal(text, FileText("again.carve"));
    }

    [Fact]
    public async Task Save_mesh_writes_the_parts_own_geometry_and_levels_as_a_top_level_part()
    {
        using var carve = await StartWithTreeAsync();
        Ok(await carve.CallToolAsync("save", """{"path":"body.carve","mesh":"Body"}"""));
        var text = FileText("body.carve");
        Assert.Equal(CarveFile.Write(PartTreeSample.Tree().PartPiece("Body", out _)!), text);
        var read = CarveFile.Read(text);
        Assert.Equal(7, read.Version);
        var part = Assert.Single(read.Model!.Parts);
        Assert.Equal(new Part("Body") { Levels = part.Levels }, part); // no envelope, parent, transform or children
        Assert.Equal([0, 1], part.Levels.Select(l => l.Number));

        // A placed part with no levels: its geometry alone at the origin, written as a plain mesh is.
        Ok(await carve.CallToolAsync("save", """{"path":"arm.carve","mesh":"Arm"}"""));
        Assert.Equal(CarveFile.Write(Model.Of("Arm", TestMeshes.Cube())), FileText("arm.carve"));
    }

    [Theory]
    [InlineData("Copy", "mesh: 'Copy' is an instance of 'Arm'; edit 'Arm', and every instance follows")]
    [InlineData("Empty", "mesh: 'Empty' is an empty part with no geometry; define_mesh mesh=Empty gives it some")]
    public async Task Save_mesh_of_an_instance_or_an_empty_is_a_tool_error(string name, string expected)
    {
        using var carve = await StartWithTreeAsync();
        Assert.Contains(expected, Refused(await carve.CallToolAsync("save", $$"""{"path":"piece.carve","mesh":"{{name}}"}""")));
        Assert.False(File.Exists(Path.Combine(Root, "piece.carve")));
    }

    [Fact]
    public async Task Open_mesh_replaces_a_placed_parts_geometry_and_levels_and_keeps_its_placement()
    {
        using var carve = await StartWithTreeAsync();
        File.WriteAllText(Path.Combine(Root, "arm.carve"), CarveFile.Write(Model.Of("Arm", PartTreeSample.Big()).WithLevel("Arm", 2)));
        var before = carve.Store.Current.Model;
        Ok(await carve.CallToolAsync("open", """{"path":"arm.carve","mesh":"Arm"}"""));
        var after = carve.Store.Current.Model;
        var arm = after.FindPart("Arm")!;
        Assert.Equal(before.FindPart("Arm")! with { Levels = arm.Levels }, arm);
        Assert.Equal([2], arm.Levels.Select(l => l.Number));
        Assert.Equal(PartTreeSample.Big().Vertices, after.Find("Arm")!.Vertices);
        Assert.Equal(before.Parts.Select(p => p.Name), after.Parts.Select(p => p.Name));

        // A piece saved from a part opens back into a placed part of that name, levels and all.
        Ok(await carve.CallToolAsync("save", """{"path":"body.carve","mesh":"Body"}"""));
        Ok(await carve.CallToolAsync("open", """{"path":"body.carve","mesh":"Body"}"""));
        var body = carve.Store.Current.Model.FindPart("Body")!;
        Assert.Equal(before.FindPart("Body")! with { Levels = body.Levels }, body);
        Assert.Equal([0, 1], body.Levels.Select(l => l.Number));
    }

    [Theory]
    [InlineData("Copy", "mesh: the model's part 'Copy' is an instance of 'Arm', with no geometry of its own; open into 'Arm', and every instance follows")]
    [InlineData("Empty", "mesh: the model's part 'Empty' is an empty part with no geometry; a piece is one part's own geometry, so name a part that has some")]
    public async Task Open_mesh_into_an_instance_or_an_empty_is_a_tool_error(string name, string expected)
    {
        using var carve = await StartWithTreeAsync();
        File.WriteAllText(Path.Combine(Root, "piece.carve"), CarveFile.Write(Model.Of(name, TestMeshes.Cube())));
        var before = carve.Store.Current;
        Assert.Contains(expected, Refused(await carve.CallToolAsync("open", """{"path":"piece.carve","mesh":"NAME"}""".Replace("NAME", name))));
        Assert.Same(before, carve.Store.Current);
    }

    [Theory]
    [InlineData("Copy", "mesh: the file's part 'Copy' is an instance of 'Arm', with no geometry of its own; open 'Arm', and every instance follows")]
    [InlineData("Empty", "mesh: the file's part 'Empty' is an empty part with no geometry; a piece is one part's own geometry, so name a part that has some")]
    public async Task Open_mesh_naming_an_instance_or_an_empty_of_the_file_is_a_tool_error(string name, string expected)
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        File.WriteAllText(Path.Combine(Root, "tree.carve"), CarveFile.Write(PartTreeSample.Tree()));
        var before = carve.Store.Current;
        Assert.Contains(expected, Refused(await carve.CallToolAsync("open", """{"path":"tree.carve","mesh":"NAME"}""".Replace("NAME", name))));
        Assert.Same(before, carve.Store.Current);
    }
}
