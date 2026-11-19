using System.Numerics;
using Carve.Core;
using Carve.Rover;
using static Carve.Server.Tests.ToolList;

namespace Carve.Server.Tests;

/// <summary>T-95: export's result line and description for a part tree, through the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class ExportPartsToolTests() : FileToolTests("carve-export-parts")
{
    [Fact]
    public async Task The_rovers_line_says_how_many_nodes_were_written_and_names_the_moving_parts()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        carve.Store.OpenModel(RoverSpec.Build(), "open");
        var line = Ok(await carve.CallToolAsync("export", """{"path":"rover.glb"}"""));
        Assert.StartsWith($"exported rover.glb to {Path.Combine(Root, "rover.glb")}: 576 triangles, ", line);
        Assert.Contains(", 6 nodes: 'rover' and the moving part(s) 'Mast', 'Wheel_FL', 'Wheel_FR', 'Wheel_RL', 'Wheel_RR'; no game limits", line);
        Assert.True(File.Exists(Path.Combine(Root, "rover.glb")));
    }

    [Fact]
    public async Task A_sheared_moving_part_is_refused_and_nothing_is_written()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        var cube = Carve.Core.Tests.TestMeshes.Cube();
        var (model, _) = Model.Create([new("Base", cube), new("Arm", cube)], null, [], [],
            [new Part("Base") { Scale = new Vector3(2, 1, 1) },
             new Part("Arm") { Parent = "Base", Moves = true, Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f) }]);
        carve.Store.OpenModel(model!, "open");
        var refused = Refused(await carve.CallToolAsync("export", """{"path":"sheared.glb"}"""));
        Assert.StartsWith("export: part 'Arm' moves, and the non-uniform scale of its static ancestor 'Base'", refused);
        Assert.EndsWith(". Nothing was written.", refused);
        Assert.False(File.Exists(Path.Combine(Root, "sheared.glb")));
    }

    [Fact]
    public async Task A_model_with_nothing_marked_moves_gives_todays_line()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        var line = Ok(await carve.CallToolAsync("export", """{"path":"cube.glb"}"""));
        Assert.DoesNotContain("nodes", line);
    }

    [Fact]
    public async Task Morph_targets_with_moving_parts_warn_that_a_split_load_refuses_them()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        var cube = Carve.Core.Tests.TestMeshes.Cube();
        var bulged = cube.WithTargets(MorphTargets.Of([MorphTarget.Of("Bulge", [KeyValuePair.Create(0, new Vector3(0, 1, 0))])]));
        var (model, _) = Model.Create([new("Body", cube), new("Lid", bulged)], null, [], [],
            [new Part("Body"), new Part("Lid") { Moves = true, Translation = new Vector3(0, 2, 0) }]);
        carve.Store.OpenModel(model!, "open");
        var line = Ok(await carve.CallToolAsync("export", """{"path":"lid.glb"}"""));
        Assert.Contains("a split load (ModelParseOptions SplitParts) refuses a file with morph targets", line);
    }

    [Fact]
    public async Task The_description_says_how_the_tree_becomes_nodes_with_the_rover()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _carve = carve;
        var text = Description(tools, "export");
        Assert.Contains("the .glb keeps a node only where a part moves", text);
        Assert.Contains("root 'rover' holds the Chassis and the Cab merged", text);
        Assert.Contains("the four wheels sharing one mesh", text);
        Assert.Contains("moves has no effect there", text);
    }
}
