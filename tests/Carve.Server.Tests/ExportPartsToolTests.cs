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

    /// <summary>RV-71: the tree export counts the game's limits on the file as drawn, so the four shared wheels are four wheels' triangles.</summary>
    [Fact]
    public async Task A_tree_export_counts_its_limits_as_drawn_with_each_instance_its_own_triangles()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        carve.Store.OpenModel(RoverSpec.Build(), "open");
        var game = Directory.CreateDirectory(Path.Combine(Root, "game")).FullName;
        var limits = Path.Combine(game, "carve-limits.json");
        File.WriteAllText(limits, """{"max_triangles": 575}""");
        var glb = Path.Combine(game, "rover.glb");
        var past = Ok(await carve.CallToolAsync("export", System.Text.Json.JsonSerializer.Serialize(new { path = glb })));
        Assert.Contains(", 6 nodes: ", past);
        Assert.Contains($"; limits from {limits}; 1 warning(s): 576 triangles, above the game's max_triangles 575", past);
        File.WriteAllText(limits, """{"max_triangles": 576}""");
        Assert.DoesNotContain("warning", Ok(await carve.CallToolAsync("export", System.Text.Json.JsonSerializer.Serialize(new { path = glb, overwrite = true }))));
    }

    /// <summary>A cube weighted wholly to the one root bone, on a part moved up 1 and marked moves.</summary>
    private static Model PlacedWeightedCube()
    {
        var skeleton = Skeleton.Create([new BoneSpec("Root", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        var cube = Carve.Core.Tests.TestMeshes.Cube();
        var weights = VertexWeights.None.With([.. Enumerable.Range(0, cube.Vertices.Count)], [new Influence(0, 1)]);
        var (model, errors) = Model.Create([new("Box", cube.WithWeights(weights))], skeleton, [], [],
            [new Part("Box") { Translation = new Vector3(0, 1, 0), Moves = true }]);
        Assert.True(model is not null, string.Join("; ", errors));
        return model!;
    }

    /// <summary>RV-71: a skinned export of a placed part says moves has no effect, and its bake keeps the weights, so nothing is unweighted.</summary>
    [Fact]
    public async Task A_skinned_export_of_a_placed_moving_part_warns_of_moves_and_keeps_its_weights()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        carve.Store.OpenModel(PlacedWeightedCube(), "open");
        var line = Ok(await carve.CallToolAsync("export", """{"path":"box.glb"}"""));
        Assert.StartsWith($"exported box.glb to {Path.Combine(Root, "box.glb")} skinned: 1 piece(s) 'Box', ", line);
        Assert.Contains("bounds (0, 1, 0) to (1, 2, 1)", line);
        Assert.Contains("moves has no effect on a skinned export, which bakes every part into the skinned mesh: 'Box'", line);
        Assert.DoesNotContain("allow_unweighted", line);
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
