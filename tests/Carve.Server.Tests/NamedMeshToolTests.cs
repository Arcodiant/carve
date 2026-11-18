using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.NamedMeshTests;

namespace Carve.Server.Tests;

/// <summary>NamedMeshTests' checks that need no engine, on the harness: aiming each tool, the refusals and undo across meshes.</summary>
[Trait("Tier", "unit")]
public sealed class NamedMeshToolTests() : TempFolderTests("carve-named-tools")
{
    [Theory]
    [InlineData("move", """{"faces":[0],"by":[0,1,0]}""")]
    [InlineData("extrude", """{"faces":[0],"distance":0.5}""")]
    [InlineData("get_mesh", "{}")]
    [InlineData("check", "{}")]
    [InlineData("select", """{"kind":"faces","where":"normal.y > 0.9"}""")]
    [InlineData("set_colour", """{"colour":"#3C78C8","faces":[0]}""")]
    [InlineData("define_group", """{"name":"top","faces":[0]}""")]
    [InlineData("set_shading", """{"shading":"smooth"}""")]
    [InlineData("mark_edges", """{"edges":[[0,1]]}""")]
    [InlineData("set_weights", """{"vertices":[0],"weights":{"base":1}}""")]
    [InlineData("weigh", "{}")]
    [InlineData("get_weights", "{}")]
    public async Task Every_tool_aimed_at_one_mesh_refuses_to_guess_among_several_and_names_them(string tool, string arguments)
    {
        var (carve, _) = await StartWithCrateAndLidOnHarnessAsync(Root);
        using var __ = carve;
        if (tool is "set_weights" or "weigh" or "get_weights")
            Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"base","position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        Assert.False(IsError(await carve.CallToolAsync("save", """{"path":"crate.carve","mesh":"crate"}""")));

        var text = Refused(await carve.CallToolAsync(tool, arguments));

        Assert.Contains(Names, text);
        Assert.Contains("mesh", text);
        var named = arguments.Insert(1, arguments.Length > 2 ? "\"mesh\":\"crate\"," : "\"mesh\":\"crate\"");
        Assert.False(IsError(await carve.CallToolAsync(tool, named)), $"{tool} {named}");
    }

    [Fact]
    public async Task An_unknown_name_and_an_empty_name_are_refused()
    {
        var (carve, _) = await StartWithCrateAndLidOnHarnessAsync();
        using var __ = carve;

        Assert.Contains("no mesh named 'hat'; the model's meshes are " + Names,
            Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"hat"}""")));
        Assert.Contains("name: a mesh name must not be empty",
            Refused(await carve.CallToolAsync("define_mesh", """{"name":"","vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""")));
        Assert.Contains("mesh: expected a string",
            Refused(await carve.CallToolAsync("get_mesh", """{"mesh":3}""")));
    }

    [Fact]
    public async Task A_mesh_name_with_a_colon_is_refused_by_define_mesh_and_the_tools_that_name_a_mesh()
    {
        // RV-24 B4: '<mesh>:' prefixes a skinned export's material names.
        var (carve, _) = await StartWithCrateAndLidOnHarnessAsync();
        using var __ = carve;
        const string colon = "a mesh name must not contain ':' (a skinned export names materials <mesh>:<group>)";

        Assert.Contains("name: " + colon,
            Refused(await carve.CallToolAsync("define_mesh", """{"name":"crate:lid","vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""")));
        Assert.Contains("mesh: " + colon, Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"crate:lid"}""")));
        Assert.Contains("mesh: " + colon, Refused(await carve.CallToolAsync("open", """{"path":"x.carve","mesh":"crate:lid"}""")));
    }

    [Fact]
    public async Task Undo_steps_back_across_meshes_one_step_per_call()
    {
        var (carve, tools) = await StartWithCrateAndLidOnHarnessAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("move", TwoMeshExample(tools)[2].Arguments));
        Ok(await carve.CallToolAsync("extrude", """{"mesh":"crate","faces":[3],"distance":0.5}"""));

        var first = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("extrude", first.GetProperty("undone").GetString());
        var meshes = first.GetProperty("meshes");
        Assert.Equal(["crate", "lid"], meshes.EnumerateArray().Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(6, meshes[0].GetProperty("faces").GetInt32());
        Assert.Equal(1.2f, meshes[1].GetProperty("bounds").GetProperty("max")[1].GetSingle(), 5);

        Assert.Equal("move", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
        Assert.Equal(new float[] { -0.1f, 1f, -0.1f }, Point(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"lid"}""")), 0));

        var last = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("define_mesh", last.GetProperty("undone").GetString());
        Assert.False(last.TryGetProperty("meshes", out var __)); // one mesh left: define_mesh's own report
        Assert.Equal(8, last.GetProperty("vertices").GetInt32());
        Assert.Contains("no mesh named 'lid'", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"lid"}""")));
        Assert.True(IsError(await carve.CallToolAsync("undo", "{}")));
    }

    [Fact]
    public async Task A_defined_name_is_replaced_in_place_and_open_takes_a_mesh_by_its_name_in_the_file()
    {
        var (carve, _) = await StartWithCrateAndLidOnHarnessAsync(Root);
        using var __ = carve;
        Assert.False(IsError(await carve.CallToolAsync("save", """{"path":"lid.carve","mesh":"lid"}""")));

        var redefined = Ok(await carve.CallToolAsync("define_mesh", """{"name":"crate","vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Assert.Equal(3, redefined.GetProperty("vertices").GetInt32());
        // open with mesh takes that mesh from the file by its name there (the person's ruling, 2026-11-09): a name the file
        // has not is refused, listing the file's meshes, and the file's 'lid' replaces the model's in its place.
        Assert.Contains("mesh: the file has no mesh named 'spare'; its meshes are 'lid'", Refused(await carve.CallToolAsync("open", """{"path":"lid.carve","mesh":"spare"}""")));
        Ok(await carve.CallToolAsync("open", """{"path":"lid.carve","mesh":"lid"}"""));

        var undo = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(["crate", "lid"], undo.GetProperty("meshes").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(3, undo.GetProperty("meshes")[0].GetProperty("vertices").GetInt32());
    }
}
