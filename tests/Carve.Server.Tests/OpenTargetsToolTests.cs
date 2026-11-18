using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>
/// open and save with morph targets (T-101): open refuses to replace a mesh that has targets, naming them, unless
/// drop_targets is true; undo brings them back either way; save mesh= carries the mesh's targets.
/// </summary>
[Trait("Tier", "unit")]
public sealed class OpenTargetsToolTests() : FileToolTests("carve-opentargets")
{
    /// <summary>A file of meshes 'arm' (targets Bulk and Lean) and 'plain' (none), written into the test folder.</summary>
    private string WriteArm(string file = "arm.carve")
    {
        var cube = TestMeshes.Cube();
        var bulk = MorphTarget.FromDeltas(cube, "Bulk", [KeyValuePair.Create(0, new Vector3(0.1f, 0, 0)), KeyValuePair.Create(6, new Vector3(0, -0.3f, 0.7f))]).Target!;
        var lean = MorphTarget.FromPositions(cube, "Lean", cube.Vertices.Select(v => v * 0.8f).ToArray()).Target!;
        var model = Model.Of("arm", cube.WithTargets(MorphTargets.Of([bulk, lean]))).With("plain", TestMeshes.Cube());
        var path = Path.Combine(Root, file);
        File.WriteAllText(path, CarveFile.Write(model));
        return path;
    }

    /// <summary>The model as save writes it, read back.</summary>
    private async Task<Model> Saved(ToolHarness carve, string file)
    {
        Ok(await carve.CallToolAsync("save", $$"""{"path":"{{file}}"}"""));
        var read = CarveFile.Read(File.ReadAllText(Path.Combine(Root, file)));
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        return read.Model!;
    }

    [Fact]
    public async Task Open_and_save_keep_targets_bit_exact()
    {
        var (carve, _) = await StartAsync(null);
        using var _carve = carve;
        var arm = WriteArm();
        Ok(await carve.CallToolAsync("open", """{"path":"arm.carve"}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"again.carve"}"""));
        Assert.Equal(File.ReadAllText(arm), File.ReadAllText(Path.Combine(Root, "again.carve")));
    }

    [Fact]
    public async Task Save_of_one_mesh_carries_its_targets()
    {
        var (carve, _) = await StartAsync(null);
        using var _carve = carve;
        WriteArm();
        Ok(await carve.CallToolAsync("open", """{"path":"arm.carve"}"""));
        Ok(await carve.CallToolAsync("save", """{"path":"one.carve","mesh":"arm"}"""));
        var read = CarveFile.Read(File.ReadAllText(Path.Combine(Root, "one.carve")));
        Assert.Equal(8, read.Version);
        Assert.Equal(["Bulk", "Lean"], read.Model!.Meshes.Single().Mesh.Targets!.Names);
        Ok(await carve.CallToolAsync("save", """{"path":"plain.carve","mesh":"plain"}"""));
        Assert.Equal(2, CarveFile.Read(File.ReadAllText(Path.Combine(Root, "plain.carve"))).Version);
    }

    [Fact]
    public async Task A_whole_file_open_over_targets_is_refused_naming_them_unless_drop_targets()
    {
        var (carve, _) = await StartAsync(null);
        using var _carve = carve;
        WriteArm();
        Ok(await carve.CallToolAsync("open", """{"path":"arm.carve"}"""));
        var before = await Saved(carve, "before.carve");
        File.WriteAllText(Path.Combine(Root, "cube.carve"), CarveFile.Write(TestMeshes.Cube()));

        var refused = Refused(await carve.CallToolAsync("open", """{"path":"cube.carve"}"""));
        Assert.Contains("drop_targets", refused);
        Assert.Contains("mesh 'arm': 'Bulk', 'Lean'", refused);
        Assert.DoesNotContain("plain", refused);
        Assert.Equal(CarveFile.Write(before), CarveFile.Write(await Saved(carve, "after-refusal.carve")));
        Refused(await carve.CallToolAsync("open", """{"path":"cube.carve","drop_targets":false}"""));
        Assert.Contains("drop_targets: expected true or false", Refused(await carve.CallToolAsync("open", """{"path":"cube.carve","drop_targets":"yes"}""")));

        Ok(await carve.CallToolAsync("open", """{"path":"cube.carve","drop_targets":true}"""));
        Assert.Null((await Saved(carve, "dropped.carve")).Meshes.Single().Mesh.Targets);

        Assert.Contains("\"undone\":\"open\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(CarveFile.Write(before), CarveFile.Write(await Saved(carve, "undone.carve")));
    }

    [Fact]
    public async Task A_named_open_over_a_mesh_with_targets_is_refused_unless_drop_targets()
    {
        var (carve, _) = await StartAsync(null);
        using var _carve = carve;
        WriteArm();
        Ok(await carve.CallToolAsync("open", """{"path":"arm.carve"}"""));
        var before = await Saved(carve, "before.carve");

        // Over 'plain', which has no targets, a named open goes ahead.
        Ok(await carve.CallToolAsync("open", """{"path":"arm.carve","mesh":"plain"}"""));

        var refused = Refused(await carve.CallToolAsync("open", """{"path":"arm.carve","mesh":"arm"}"""));
        Assert.Contains("mesh 'arm' has the morph target(s) 'Bulk', 'Lean'", refused);
        Assert.Contains("drop_targets: true", refused);

        File.WriteAllText(Path.Combine(Root, "cube.carve"), CarveFile.Write(Model.Of("arm", TestMeshes.Cube())));
        Ok(await carve.CallToolAsync("open", """{"path":"cube.carve","mesh":"arm","drop_targets":true}"""));
        var dropped = await Saved(carve, "dropped.carve");
        Assert.Null(dropped.Meshes.Single(m => m.Name == "arm").Mesh.Targets);

        Assert.Contains("\"undone\":\"open\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Equal(CarveFile.Write(before), CarveFile.Write(await Saved(carve, "undone.carve")));
    }

    [Fact]
    public async Task An_open_over_a_model_without_targets_needs_no_flag()
    {
        var (carve, _) = await StartAsync();
        using var _carve = carve;
        WriteArm();
        Ok(await carve.CallToolAsync("open", """{"path":"arm.carve"}"""));
    }
}
