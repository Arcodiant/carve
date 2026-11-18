using System.Text.Json;
using Xunit;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.CarveFileVersion2Tests;

namespace Carve.Server.Tests;

/// <summary>The Carve file, version 2: save and open of one mesh by name, through the real tools on the harness.</summary>
[Trait("Tier", "unit")]
public sealed class CarveFileVersion2ToolTests() : FileToolTests("carve-file-v2-tools")
{
    [Fact]
    public async Task Save_with_mesh_writes_that_mesh_with_the_skeleton_and_clips_its_weights_need()
    {
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        await BuildChainAsync(carve);

        Assert.Equal($"saved upper.carve to {Path.Combine(Root, "upper.carve")}: 8 vertices, 6 faces, 2 bones, 1 clip",
            Ok(await carve.CallToolAsync("save", """{"path":"upper.carve","mesh":"upper"}""")));
        var upper = Core.CarveFile.Read(File.ReadAllText(Path.Combine(Root, "upper.carve"))).Model!;
        Assert.Equal(["upper"], upper.Names);
        Assert.Equal(["base", "joint"], upper.Skeleton!.Names);
        Assert.Equal("wave", Assert.Single(upper.Clips).Name);

        Ok(await carve.CallToolAsync("save", """{"path":"plain.carve","mesh":"plain"}"""));
        var plain = Core.CarveFile.Read(File.ReadAllText(Path.Combine(Root, "plain.carve"))).Model!;
        Assert.Equal(["plain"], plain.Names);
        Assert.Null(plain.Skeleton);
        Assert.Empty(plain.Clips);

        Assert.Contains("no mesh named 'hat'", Refused(await carve.CallToolAsync("save", """{"path":"hat.carve","mesh":"hat"}""")));
        Assert.False(File.Exists(Path.Combine(Root, "hat.carve")));
    }

    [Fact]
    public async Task Open_with_mesh_takes_that_mesh_onto_the_models_bones_by_name_or_is_refused_leaving_the_model()
    {
        var (first, _) = await StartAsync(mesh: null);
        using (first)
        {
            await BuildChainAsync(first);
            Ok(await first.CallToolAsync("save", """{"path":"chain.carve"}"""));
        }

        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        Ok(await carve.CallToolAsync("define_mesh", Box("other", 0, 1)));
        var before = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}"""));

        // No skeleton: the weights name bones the model does not have.
        var refused = Refused(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"upper"}"""));
        Assert.Contains("mesh: the file's mesh 'upper' has weights on the bones 'base', 'joint', and the model has no skeleton", refused);
        Assert.Equal(before, Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.Contains("nothing to undo", Refused(await carve.CallToolAsync("undo", "{}")));

        // A skeleton lacking 'joint' is still refused, naming it.
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"base","position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        Assert.Contains("does not have: 'joint'", Refused(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"upper"}""")));
        Assert.Contains("the file has no mesh named 'hat'; its meshes are 'mesh', 'upper', 'plain'",
            Refused(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"hat"}""")));

        // The same bones in the other order: the weights follow the names.
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"joint","position":[0,1,0],"rotation":[0,0,0,1]},{"name":"base","position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        var opened = JsonDocument.Parse(Ok(await carve.CallToolAsync("open", """{"path":"chain.carve","mesh":"upper"}"""))).RootElement;
        Assert.Equal(8, opened.GetProperty("vertices").GetInt32());
        Assert.Equal(before, Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}""")));
        var upper = JsonDocument.Parse(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"upper"}"""))).RootElement.ToString();
        Assert.Contains("#3C78C8", upper);
        // get_mesh does not show weights; the model saved again does, on the model's own bone indices (joint 0, base 1).
        Ok(await carve.CallToolAsync("save", """{"path":"after.carve"}"""));
        var weights = Core.CarveFile.Read(File.ReadAllText(Path.Combine(Root, "after.carve"))).Model!.Find("upper")!.Weights!;
        Assert.Equal([new Core.Influence(0, 1)], weights.Of(2));
        Assert.Equal([new Core.Influence(0, 2.0 / 3), new Core.Influence(1, 1.0 / 3)], weights.Of(0));

        Assert.Contains("\"undone\":\"open\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Contains("no mesh named 'upper'", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"upper"}""")));
    }

    [Fact]
    public async Task Open_with_mesh_places_a_version_1_files_one_mesh_under_the_name_given()
    {
        // The person's ruling (2026-11-09): a version 1 file's single mesh is taken whatever the name, and placed under it.
        var (carve, _) = await StartAsync(mesh: null);
        using var _carve = carve;
        Ok(await carve.CallToolAsync("define_mesh", Box("crate", 0, 1)));
        File.WriteAllText(Path.Combine(Root, "old.carve"),
            """{"carve": 1, "vertices": [[0, 0, 0], [1, 0, 0], [0, 1, 0]], "faces": [[0, 1, 2]]}""");
        Ok(await carve.CallToolAsync("define_mesh", Box("other", 1, 2)));
        var other = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}"""));

        // Replacing: 'crate' becomes the file's triangle, in its place; 'other' stays.
        var replaced = JsonDocument.Parse(Ok(await carve.CallToolAsync("open", """{"path":"old.carve","mesh":"crate"}"""))).RootElement;
        Assert.Equal(3, replaced.GetProperty("vertices").GetInt32());
        Assert.Equal(1, JsonDocument.Parse(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"crate"}"""))).RootElement.GetProperty("faces").GetArrayLength());
        Assert.Equal(other, Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"other"}""")));

        // Adding: a new name, after the others.
        Ok(await carve.CallToolAsync("open", """{"path":"old.carve","mesh":"lid"}"""));
        Assert.Contains("'crate', 'other', 'lid'", Refused(await carve.CallToolAsync("get_mesh", "{}")));

        Assert.Contains("\"undone\":\"open\"", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Contains("no mesh named 'lid'", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"lid"}""")));
    }
}
