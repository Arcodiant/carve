using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class SaveOpenToolDefinitionTests() : FileToolTests("carve-saveopen")
{
    private static async Task ExtrudeAndRaise(ToolHarness carve)
    {
        Ok(await carve.CallToolAsync("extrude", """{"faces":[1],"distance":-0.2}"""));
        Ok(await carve.CallToolAsync("move", """{"faces":[3],"by":[0,0.5,0]}"""));
    }

    [Fact]
    public async Task The_descriptions_worked_example_runs_as_written_from_an_empty_file_to_a_game_folder()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _carve = carve;
        var save = Description(tools, "save");
        Assert.Contains("models.LoadModel(new ContentPath(\"models/crate.glb\"))", save);
        // The cube's file text is what the writer gives, with LF line ends on any checkout.
        Assert.Contains(CarveFile.Write(TestMeshes.Cube()), save);
        Assert.DoesNotContain(Convert.ToChar(13), save);
        Assert.Contains("save's description", Description(tools, "export"));
        Assert.Contains("save's description", Description(tools, "open"));

        var steps = Regex.Matches(save, @"^\d\. (?:In a later session, )?(\w+) (\{.*\})$", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        Assert.Equal(["save", "open", "export"], steps.Keys.ToArray());
        Assert.Equal(steps["open"], Assert.Single(Examples(tools, "open")));

        var game = Path.Combine(Root, "game").Replace('\\', '/');
        // 1 and 2: the cube from its description, then an edit.
        Ok(await carve.CallToolAsync("define_mesh", Cube(tools)));
        await ExtrudeAndRaise(carve);
        // 3: save; 4: open (as a later session would, after a different mesh was current).
        Assert.StartsWith("saved crate.carve to ", Ok(await carve.CallToolAsync("save", steps["save"])));
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Ok(await carve.CallToolAsync("open", steps["open"]));
        // 5: export into the game's content folder.
        var line = Ok(await carve.CallToolAsync("export", steps["export"].Replace("<game>", game)));
        var glb = Path.Combine(Root, "game", "Content", "models", "crate.glb");
        Assert.StartsWith($"exported crate.glb to {glb.Replace('/', Path.DirectorySeparatorChar)}: ", line);
        Assert.True(File.Exists(Path.Combine(Root, "crate.carve")));
        var bytes = File.ReadAllBytes(glb);
        Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.NotNull(GlbExportTests.Parser.Parse(bytes, Empyrean.ModelFormat.Gltf));
    }

    [Fact]
    public async Task Tools_list_lists_save_and_open_with_closed_schemas()
    {
        var (carve, tools) = await StartAsync(mesh: null);
        using var _carve = carve;
        foreach (var name in new[] { "save", "open" })
        {
            var schema = Tool(tools, name).GetProperty("inputSchema");
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(name == "open" ? ["path", "mesh", "drop_targets"] : ["path", "mesh"], schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Contains($"unknown: not an argument of {name}", Refused(await carve.CallToolAsync(name, """{"path":"a.carve","unknown":1}""")));
            Assert.Contains("path: required but missing", Refused(await carve.CallToolAsync(name, "{}")));
        }
    }
}
